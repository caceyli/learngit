#region Copyright
/******************************************************************
*
*          Module: VMware Config File Dynamic Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/09/09
*
* Current Status
*       $Revision: 1.4 $
*           $Date: 2014/07/16 23:02:42 $
*         $Author: ameau $
*
*******************************************************************
*
* Copyright (c) 2007-2008 BDNA Corporation.
* All Rights Reserved. BDNA products and services are protected
* by the following U.S. patent: #6,988,134. BDNA is trademark of 
* BDNA Corporation.
*
* ******BDNA CONFIDENTIAL******
*
* The following code was developed and is owned by BDNA Corporation
* This code is confidential and may contain
* trade secrets. The code must not be distributed to any party
* outside of BDNA Corporation Inc. without written
* permission from BDNA.  The code may be covered by patents,
* patents pending, or patents applied for in the US or elsewhere.
*
******************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class VMwareConfigFileDynamicScript : ICollectionScriptRuntime {
        /// <summary>
        /// Perform collection task specific processing.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="cleId">Database Id of owning Collection Engine.</param>
        /// <param name="elementId">Database Id of element being collected.</param>
        /// <param name="databaseTimestamp">Database relatvie task dispatch timestamp.</param>
        /// <param name="localTimestamp">Local task dispatch timestamp.</param>
        /// <param name="attributes">Map of attribute names to Id for attributes being collected.</param>
        /// <param name="scriptParameters">Collection script specific parameters (name/value pairs).</param>
        /// <param name="connection">Connection script results (null if this script does not
        ///     require a remote host connection).</param>
        /// <param name="tftpDispatcher">Dispatcher for TFTP transfer requests.</param>
        /// <returns>Collection results.</returns>
        public CollectionScriptResults ExecuteTask(long taskId, long cleId, long elementId, long databaseTimestamp,
                long localTimestamp, IDictionary<string, string> attributes, IDictionary<string, string> scriptParameters,
                IDictionary<string, object> connection, string tftpPath, string tftpPath_login, string tftpPath_password, ITftpDispatcher tftpDispatcher) {
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_executionTimer = Stopwatch.StartNew();

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script VMwareConfigFileDynamicScript.",
                                  m_taskId);
            try {
                // Check ManagementScope CIMV
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to VMwareConfigFileDynamicScript is null.",
                                          m_taskId);
                } 
                else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else {
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    StringBuilder strFiles = new StringBuilder();
                    StringCollection arrDirs = this.FindLocalUserDirectories();

                    // If this VMware installation is VMServer, 
                    // there is a centralize vm listing file called, "vm-list"
                    if (isVMConfigFileReadable(s_vmserver_central_listing_file_path)) {
                        if (strFiles.Length > 0) {
                            strFiles.Append(BdnaDelimiters.DELIMITER_TAG);
                        }
                        strFiles.Append(s_vmserver_central_listing_file_path);
                    }

                    // If this VMware installation is VMGSXServer, 
                    // there is a centralize vm listing file called, "vm-list"
                    if (isVMConfigFileReadable(s_vmgsxserver_central_listing_file_path)) {
                        if (strFiles.Length > 0) {
                            strFiles.Append(BdnaDelimiters.DELIMITER_TAG);
                        }
                        strFiles.Append(s_vmgsxserver_central_listing_file_path);
                    }

                    //
                    // Retrieving property of each file if it exists..
                    foreach (string strSubDir in arrDirs) {
                        if (!String.IsNullOrEmpty(strSubDir)) {
                            String strFavoriteFilePath = strSubDir + @"\Application Data\VMware\favorites.vmls";
                            strFavoriteFilePath.Replace(@"\", @"\\");
                            if (isVMConfigFileReadable(strFavoriteFilePath)) {
                                if (strFiles.Length > 0) {
                                    strFiles.Append(BdnaDelimiters.DELIMITER_TAG);
                                }
                                strFiles.Append(strFavoriteFilePath);
                            }
                        }
                    }

                    //
                    // Package data into CLE format to be returned.
                    if (strFiles.Length > 0) {
                        BuildDataRow(s_attributeName, strFiles.ToString());
                    }
                }
            }
            catch (Exception ex) {
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in VMwareConfigFileDynamicScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in VMwareConfigFileDynamicScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script VMwareConfigFileDynamicScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }


        /// <summary>
        /// Find all Documents and Setting directories of all user that has ever logon locally to this machine. 
        /// Each user might have different preference file of VMware.
        /// </summary>
        /// <returns>Collections of directories</returns>
        private StringCollection FindLocalUserDirectories() {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Searching all subdirectories that might contain VMware configuration files.",
                                  m_taskId);
            StringCollection arrLocalDirs = new StringCollection();
            try {
                using (ManagementObject moDocDir = new ManagementObject(@"Win32_Directory='C:\documents and settings'")) {
                    moDocDir.Scope = m_cimvScope;
                    moDocDir.Get();

                    foreach (ManagementObject moSubDirs in
                        moDocDir.GetRelated("Win32_Directory", null, null, null, "PartComponent", "GroupComponent", false, null)) {
                        string strSubDirPath = moSubDirs[@"Name"].ToString();
                        if (!String.IsNullOrEmpty(strSubDirPath)) {
                            arrLocalDirs.Add(strSubDirPath);
                        }
                    }
                }
            }
            catch (ManagementException mex) {
                StringBuilder props = new StringBuilder();
                foreach (PropertyData mexProp in mex.ErrorInformation.Properties) {
                    if (mexProp != null && !mexProp.IsArray) {

                        //
                        // Do NOT call ToString on the values because they may
                        // be null. StringBuilder handles null values just fine.
                        // Besides, calling ToString explicitly on the Names or
                        // Values of this collection buys you absolutely *nothing*.
                        // These are already objects, so there is no boxing to
                        // be avoided.
                        props.Append(mexProp.Name).Append(@"=").Append(mexProp.Value).AppendLine();
                    }
                }
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for VMware configuration files.\n{1}\n{2}",
                                      m_taskId,
                                      mex.ToString(),
                                      props.ToString());
            }
            catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for VMware configuration files.\n{1}",
                                      m_taskId,
                                      ex.ToString());
            }
            return arrLocalDirs;
        }

        /// <summary>
        /// Validate Configuration file is readable by current user credential. 
        /// </summary>
        /// <param name="strFilePath">File Path</param>
        /// <returns>True if file is valid, and readable; False otherwise.</returns>
        private bool isVMConfigFileReadable(string strFilePath) {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Attempting to retrieve file property of path {1}.",
                                  m_taskId,
                                  strFilePath);
            bool bStatus = false;
            try {
                using (ManagementObject moVMConfigFile = new ManagementObject(@"CIM_DataFile.Name='" + strFilePath + @"'")) {
                    moVMConfigFile.Scope = m_cimvScope;
                    moVMConfigFile.Get();
                    if (moVMConfigFile[@"Readable"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase)) {
                        bStatus = true;
                    }
                }
            }
            catch (ManagementException mex) {
                if (mex.Message.Trim().Equals(@"Not found")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run VMware.\nDetail Exception Message: {2} not found.",
                                          m_taskId,
                                          strFilePath,
                                          strFilePath);
                } else {
                    StringBuilder props = new StringBuilder();
                    foreach (PropertyData mexProp in mex.ErrorInformation.Properties) {
                        if (mexProp != null && !mexProp.IsArray) {

                            //
                            // Do NOT call ToString on the values because they may
                            // be null. StringBuilder handles null values just fine.
                            // Besides, calling ToString explicitly on the Names or
                            // Values of this collection buys you absolutely *nothing*.
                            // These are already objects, so there is no boxing to
                            // be avoided.
                            props.Append(mexProp.Name).Append(@"=").Append(mexProp.Value).AppendLine();
                        }
                    }

                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run VMware.\nDetail Exception Message: .\n{2}\n{3}",
                                          m_taskId,
                                          strFilePath,
                                          mex.Message,
                                          props.ToString());
                }
            }
            catch (Exception ex) {
                if (ex.Message.Trim().Equals(@"Not found")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run VMware.\nDetail Exception Message: {2} not found.",
                                          m_taskId,
                                          strFilePath,
                                          strFilePath);
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run VMware.\nDetail Exception Message:\n{2}",
                                          m_taskId,
                                          strFilePath,
                                          ex.Message);
                }
            }
            return bStatus;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            } else if (string.IsNullOrEmpty(collectedData)) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            } else {
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        /// <summary>CLE element id.</summary>
        private long m_cleId;

        /// <summary>Id of element being collected.</summary>
        private long m_elementId;

        /// <summary>Database relative task dispatch timestamp.</summary>
        private long m_databaseTimestamp;

        /// <summary>CLE local dispatch timestamp.</summary>
        private long m_localTimestamp;

        /// <summary>Map of attribute names to attribute element ids.</summary>
        private IDictionary<string, string> m_attributes;

        /// <summary>Map of collection script specific parameters.</summary>
        private IDictionary<string, string> m_scriptParameters;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>output data buffer.</summary>
        private StringBuilder m_outputData = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Management Scope </summary>
        private ManagementScope m_cimvScope = null;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;

        public static string s_attributeName = @"VMLSFilePaths";
        public static string s_vmserver_central_listing_file_path = 
              @"c:\documents and settings\all users\Application Data\VMware\VMware Server\vm-list";
        public static string s_vmgsxserver_central_listing_file_path = 
              @"c:\documents and settings\all users\Application Data\VMware\VMware GSX Server\vm-list";
    }
}
