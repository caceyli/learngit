#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Suma Manvi
*   Creation Date: 2007/10/04
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

namespace bdna.Scripts
{
    public class MSVirtualServerVMsFromShortcutPathScript : ICollectionScriptRuntime
    {
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
                IDictionary<string, object> connection, string tftpPath, string tftpPath_login,
                string tftpPath_password, ITftpDispatcher tftpDispatcher)
        {
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
                                  "Task Id {0}: Collection script MSVirtualServerVMsFromShortcutPathScript.",
                                  m_taskId);
            try
            {
                //Check ManagementScope CIMV
                //ManagementScope cimvScope = null;
                if (connection == null)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to MSVirtualServerVMsFromShortcutPathScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey("cimv2"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else
                {
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
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

                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    StringBuilder vmDirPath = new StringBuilder();
                    StringBuilder vmList = new StringBuilder();
                    //vmDirPath.Append(@"C:\Documents and Settings\All Users\Application Data\Microsoft\Virtual Server\Virtual Machines");
                    vmDirPath.Append(@"%ALLUSERSPROFILE%").Append(@"\Application Data\Microsoft\Virtual Server\Virtual Machines");
                    StringCollection vmNames = findVMs(vmDirPath.ToString());
                    foreach (string vm in vmNames) {
                        if (!String.IsNullOrEmpty(vm)) {
                            if (vmList.Length > 0) {
                                vmList.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            vmList.Append(vm);
                        }
                    }
                    if (vmList.Length > 0) {
                        BuildDataRow(s_vmcFilePaths, vmList.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in MSVirtualServerVMsFromShortcutPathScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXECEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REMOTE_FILE;
                }
                else
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in MSVirtualServerVMsFromShortcutPathScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script MSVirtualServerVMsFromShortcutPathScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }


        /// <summary>
        /// Find all virtual machines in the default directory.
        /// </summary>
        /// <returns>Collections of directories</returns>
        private StringCollection findVMs(string dirPath)
        {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Listing all shortcuts to Virtual Machines.",
                                  m_taskId);
            StringCollection vmPaths = new StringCollection();
            try
            {
                StringBuilder sb = new StringBuilder();                
                sb.Append(@"SELECT * FROM CIM_Datafile WHERE Drive = 'C:' and Path = '\\Documents and Settings\\All Users\\Application Data\\Microsoft\\Virtual Server\\Virtual Machines\\'");
                ObjectQuery oQuery = new ObjectQuery(sb.ToString());
                ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(m_cimvScope, oQuery);
                ManagementObjectCollection oReturnCollection = oSearcher.Get();

                foreach (ManagementObject oReturn in oReturnCollection)
                {
                    String filePath = oReturn["Name"].ToString();
                    Dictionary<string, string> fileProperties = new Dictionary<string, string>();
                    Lib.RetrieveFileProperties(m_taskId, m_cimvScope, filePath, out fileProperties);
                    if (fileProperties != null && fileProperties.ContainsKey(@"Target"))
                    {
                        String strTarget = String.Empty;              
                        strTarget = fileProperties[@"Target"];
                        vmPaths.Add(strTarget);                      
                    }                    
                }
            }
            catch (ManagementException mex)
            {
                StringBuilder props = new StringBuilder();
                foreach (PropertyData mexProp in mex.ErrorInformation.Properties)
                {
                    if (mexProp != null && !mexProp.IsArray)
                    {
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
                                      "Task Id {0}: Unexpected error while searching for MSVirtualServerVMsFromShortcutPathScript.\n{1}\n{2}",
                                      m_taskId,
                                      mex.ToString(),
                                      props.ToString());
            }
            catch (Exception ex)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for MSVirtualServerVMsFromShortcutPathScript.\n{1}",
                                      m_taskId,
                                      ex.ToString());
            }
            return vmPaths;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        ///
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData)
        {
            Console.WriteLine(collectedData);
            if (!m_attributes.ContainsKey(attributeName))
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            }
            else if (string.IsNullOrEmpty(collectedData))
            {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            }
            else
            {
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

        /// <summary>WebSphere Application Server Profile path. </summary>
        private string m_profileHome = String.Empty;

        /// <summary>WebSphere Application Server Cell Name. </summary>
        private string m_cell = String.Empty;

        /// <summary>output data buffer.</summary>
        private StringBuilder m_outputData = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Management Scope </summary>
        private ManagementScope m_cimvScope = null;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;

        /// <summary>VMs and their corresponding Paths</summary>
        public static string s_vmcFilePaths = @"vmcFilePaths";        

    }
}


