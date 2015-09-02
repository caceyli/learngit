#region Copyright
/******************************************************************
*
*          Module: WinFileContentScript
* Original Author: Rekha rani
*   Creation Date: 2006/12/30
*
* Current Status
*       $Revision: 1.5 $
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
    public class CitrixLicensingFileContentScript : ICollectionScriptRuntime {
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
        public CollectionScriptResults ExecuteTask(long taskId, long cleId, long elementId, long databaseTimestamp, long localTimestamp, IDictionary<string, string> attributes, IDictionary<string, string> scriptParameters, IDictionary<string, object> connection, string tftpPath, string tftpPath_login, string tftpPath_password, ITftpDispatcher tftpDispatcher) {
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_tftpPath = tftpPath;
            m_tftpPath_login = tftpPath_login;
            m_tftpPath_password = tftpPath_password;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_executionTimer = Stopwatch.StartNew();

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script CitrixLicensing.",
                                  m_taskId);
            try {
                // Check ManagementScope CIMV
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to CitrixLicensing is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }


                if (scriptParameters.ContainsKey("LicenseFileDir")) {
                    m_Dirs = scriptParameters[@"LicenseFileDir"];
                } else {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter LicenseFileDir.",
                                          m_taskId);
                }

                // Check Remote Process Temp Directory
                if (!m_connection.ContainsKey(@"TemporaryDirectory")) {
                    m_connection[@"TemporaryDirectory"] = @"%TMP%";
                } else {
                    if (!m_connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                        if (!Lib.ValidateDirectory(m_taskId, m_connection[@"TemporaryDirectory"].ToString(), m_cimvScope)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} is not valid.",
                                                  m_taskId,
                                                  m_connection[@"TemporaryDirectory"].ToString());
                            resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: User specified temp directory has been validated.",
                                                  m_taskId);
                        }
                    }
                }

                StringBuilder fileContent = new StringBuilder();

                if (resultCode == ResultCodes.RC_SUCCESS) {

                    String[] m_Dirs_list = m_Dirs.Split(';');  ///just in case files exist in more than one location and each is seperated by ;

                    foreach (String m_Dir in m_Dirs_list) {
                        ////m_Dir = "C:\\Program Files\\Citrix\\Licensing\\MyFiles";   /////DEBUG ONLY         
                        ////StringBuilder fileContent = new StringBuilder();

                        if (!string.IsNullOrEmpty(m_Dir)) {

                            if (Lib.ValidateDirectory(m_taskId, m_Dir, m_cimvScope)) {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Attempting to retrieve file from directory {1}.",
                                                  m_taskId,
                                                  m_Dir);

                                ////Console.WriteLine("RR DEBUG Dir={0}", m_Dir);

                                using (ManagementObject moDir = new ManagementObject(@"Win32_Directory='" + m_Dir + @"'")) {
                                    moDir.Scope = m_cimvScope;
                                    //moDir.Get();   //// DEBUG RR this is for what

                                    foreach (ManagementObject b in moDir.GetRelated("CIM_DataFile")) {
                                        ////Console.WriteLine("DEBUG: Object in the given dir : {0}", b.ClassPath);                                    

                                        string FileName = b[@"Name"].ToString();
                                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                                      0,
                                                                      "Task Id {0}: DataFile name {1}.",
                                                                      m_taskId,
                                                                      FileName);

                                        ////Console.WriteLine("RR DEBUG DataFileName={0}", FileName);

                                        //license file must have a .lic extension
                                        if (FileName.EndsWith(".lic")) {

                                            using (IRemoteProcess rp =
                                            RemoteProcess.GetRemoteFile(m_taskId, m_cimvScope, FileName, m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher)) {

                                                //
                                                // Launch the remote process.  
                                                // This method will block until the entire remote process operation completes.
                                                ResultCodes rc = rp.Launch();
                                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                                      0,
                                                                      "Task Id {0}: Remote file ({2}) retrieval operation completed with result code {1}.",
                                                                      m_taskId,
                                                                      rc.ToString(),
                                                                      FileName);

                                                fileContent.Append("<BDNA_FileName>").Append(FileName)
                                                           .Append("<BDNA_FileContent>").Append(rp.Stdout);
                                            }
                                        }

                                    }

                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Remote directoy's files content read with result code {1}.",
                                                          m_taskId,
                                                          resultCode);

                                }

                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Directory {1} does not exist.",
                                                      m_taskId,
                                                      m_Dir);
                                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
                            }

                        }

                    }

                    this.BuildDataRow(@"LicenseFileContent", fileContent);
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access file property station.exe.\nMessage: {1}",
                                      m_taskId,
                                      me.Message);
                if (me.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          me.InnerException.Message);
                }
                resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;

            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in CitrixLicensing.  Elapsed time {1}.\n{2}Result code changed to RC_PROCESSING_EXECEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in CitrixLicensing.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script CitrixLicensingFileContentScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());

            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, StringBuilder collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
                //else if (string.IsNullOrEmpty(collectedData)) 
            } else if (collectedData == null || collectedData.Length <= 0) {
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
                         .Append(attributeName)
                         .Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary>File path. </summary>
        private string m_Dirs;

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
        private string m_tftpPath = string.Empty;
        private string m_tftpPath_login = string.Empty;
        private string m_tftpPath_password = string.Empty;

        /// <summary>
        /// Delimiter used to strip bogus data from the beginning
        /// of some collection parameter set table entries.
        /// </summary>
        private static readonly char[] s_collectionParameterSetDelimiter = new char[] { ':' };
    }
}
