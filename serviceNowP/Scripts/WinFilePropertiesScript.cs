#region Copyright

/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/10/17
*
* Current Status
*       $Revision: 1.18 $
*           $Date: 2014/07/16 23:02:42 $
*         $Author: ameau $
*
******************************************************************
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
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script to scavenge Windows level 2 registry data.
    /// </summary>
    public class WinFilePropertiesScript : ICollectionScriptRuntime {
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
                string tftpPath_password, ITftpDispatcher tftpDispatcher) {
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
                                  "Task Id {0}: Collection script WinFilePropertiesScript.",
                                  m_taskId);
            try {
                // Check ManagementScope CIMV
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WinFilePropertiesScript is null.",
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

                string filePath = string.Empty;
                string fileCommand = string.Empty;

                foreach (KeyValuePair<string, string> kvp in scriptParameters) {
                    string key = kvp.Key;
                    if (key.Contains(":")) {
                        int i = key.IndexOf(':');
                        key = key.Substring(i + 1);
                    }
                    if (key == @"filePath") {
                        filePath = kvp.Value;
                    } else if (key == @"fileCommand") {
                        fileCommand = kvp.Value;
                    }
                }
                if (string.IsNullOrEmpty(filePath)) {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task id {0}: Missing file path parameter.",
                                          m_taskId);
                }

                if (string.IsNullOrEmpty(fileCommand)) {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task id {0}: Missing file command parameter.",
                                          m_taskId);
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string collectedData = string.Empty;
                    if (fileCommand == @"fileProperties") {
                        resultCode = GetFileProperties(filePath, out collectedData);
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            BuildDataRow(@"fileProperties", collectedData);
                        } else {
                            BuildDataRow(@"fileProperties", @"NotFound");
                        }
                    } else if (fileCommand == "dirListing") {
                        resultCode = GetDirListing(filePath, out collectedData);
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            BuildDataRow(@"fileProperties", collectedData);
                        } else {
                            BuildDataRow(@"fileProperties", @"NotFound");
                        }
                    } else if (fileCommand == "fileContent") {
                        resultCode = GetFileContent(filePath, out collectedData);
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            BuildDataRow(@"fileContent", collectedData);
                        }
                    } else if (fileCommand == "executeCommand") {
                        resultCode = ExecuteCommand(filePath, out collectedData);
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            BuildDataRow(@"commandResult", collectedData);
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access file property.\nMessage: {1}",
                                      m_taskId,
                                      me.Message);
                if (me.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          me.InnerException.Message);
                }
                BuildDataRow(@"fileProperties", @"NotFound");
                resultCode = ResultCodes.RC_SUCCESS;
            } catch (COMException ce) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Not enough privilege to access run WMI query.\nMessage: {1}.",
                                      m_taskId,
                                      ce.Message);
                if (ce.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          ce.InnerException.Message);
                }
                BuildDataRow(@"fileProperties", @"NotFound");
                resultCode = ResultCodes.RC_SUCCESS;
            } catch (Exception ex) {

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WinFilePropertiesScript.  Elapsed time {1}.\nResult code changed to RC_PROCESSING_EXCEPTION.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex);
                    resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REMOTE_FILE;
                } else {
                    Lib.LogException(m_taskId,
                                     m_executionTimer,
                                     "Unhandled exception in WinFilePropertiesScript",
                                     ex);
                }
                BuildDataRow(@"fileProperties", @"NotFound");
                resultCode = ResultCodes.RC_SUCCESS;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WinFilePropertiesScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Retrive Directory Listing
        /// (Note that Directory Path must be aboslute path)
        /// </summary>
        /// <param name="dirPath">Directory Path</param>
        /// <param name="collectedData">Collecteed Result.</param>
        /// <returns></returns>
        private ResultCodes GetDirListing(string dirPath, out string collectedData) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            IList<string> listing = Lib.RetrieveFileListings(m_taskId, m_cimvScope, dirPath);
            StringBuilder buffer = new StringBuilder();

            foreach (string dir in listing) {
                if (buffer.Length > 0) {
                    buffer.Append(BdnaDelimiters.DELIMITER1_TAG);
                }
                buffer.Append(dir);
            }
            collectedData = buffer.ToString();
            return resultCode;
        }

        /// <summary>
        /// Execute command
        /// (Note that file path must be absolute path)
        /// </summary>
        /// <param name="filePath">File Path</param>
        /// <param name="collectedData">Collected Result.</param>
        /// <returns></returns>
        private ResultCodes ExecuteCommand(string userCommandLine, out string collectedData) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            collectedData = string.Empty;
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
                                              "Task Id {0}: Temporary directory {1} has been validated.",
                                              m_taskId,
                                              m_connection[@"TemporaryDirectory"].ToString());
                    }
                }
            }

            if (resultCode == ResultCodes.RC_SUCCESS) {
                string strTempDir = m_connection["TemporaryDirectory"].ToString().Trim();
                if (strTempDir.EndsWith(@"\")) {
                    strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
                }
                string strBatchFileContent = buildBatchFile(userCommandLine);
                StringBuilder stdoutData = new StringBuilder();
                using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile
                        (m_taskId, m_cimvScope, strBatchFileContent, m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher)) {
                    //This method will block until the entire remote process operation completes.
                    resultCode = rp.Launch();

                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Remote process operation completed with result code {1}.",
                                          m_taskId,
                                          resultCode.ToString());

                    if (rp != null && rp.Stdout != null && resultCode == ResultCodes.RC_SUCCESS) {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote processExec completed with result <{1}>.",
                                              m_taskId,
                                              rp.Stdout.ToString());

                        collectedData = rp.Stdout.ToString();
                        //string output = rp.Stdout.ToString();
                        //if (output.IndexOf(s_endTag) != -1) {
                        //    collectedData = output.Substring(0, output.Length - s_endTag.Length - 2);
                        //} else {
                        //    resultCode = ResultCodes.RC_PROCESS_EXEC_FAILED;
                        //    Lib.Logger.TraceEvent(TraceEventType.Error,
                        //                          0,
                        //                          "Task Id {0}: Remote execution error.\nSTDOUT.STDERR:\n{1}",
                        //                          m_taskId,
                        //                          rp.Stdout.ToString());
                        //}
                    } else {
                        resultCode = ResultCodes.RC_PROCESS_EXEC_FAILED;
                    }
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Build temporary batch file.
        /// </summary>
        /// <param name="strTempDir"></param>
        private string buildBatchFile(string cmdLine) {
            StringBuilder strBatchFile = new StringBuilder();
            strBatchFile.AppendLine(@"@ECHO OFF");
            strBatchFile.AppendLine(@"CD %TMP%");
            if (cmdLine.Length > 0) {
                strBatchFile.Append(cmdLine).AppendLine(@"  2>&1");
            }
            strBatchFile.AppendLine();
            //strBatchFile.Append(@"ECHO ").AppendLine(s_endTag);

            return strBatchFile.ToString();
        }


        /// <summary>
        /// Get File Content
        /// (Note that file path must be absolute path)
        /// (Note that file path should not have double quote in general, but it has escaped the double quote to be convenient).
        /// </summary>
        /// <param name="filePath">File Path</param>
        /// <param name="collectedData">Collected Result.</param>
        /// <returns></returns>
        private ResultCodes GetFileContent(string filePath, out string collectedData) {
            collectedData = string.Empty;
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
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

            if (filePath.EndsWith("\"") && filePath.StartsWith("\"")) {
                filePath = filePath.Substring(1, filePath.Length - 2);
            }
            if (Lib.ValidateFile(m_taskId, filePath, m_cimvScope)) {
                using (IRemoteProcess rp =
                        RemoteProcess.GetRemoteFile(m_taskId, m_cimvScope, filePath, m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher)) {
                    //
                    // Launch the remote process.  
                    // This method will block until the entire remote process operation completes.
                    resultCode = rp.Launch();
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Remote file retrieval operation completed with result code {1}.",
                                          m_taskId,
                                          resultCode.ToString());
                    collectedData = rp.Stdout.ToString();
                }
            } else {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: filepath: {1} does not exist.",
                                      m_taskId,
                                      filePath);
            }

            return resultCode;
        }

        /// <summary>
        /// Retrieve File Properties
        /// (Note that file path must be absolute path)
        /// </summary>
        /// <param name="filePath">File Path</param>
        /// <param name="collectedData">Collected Result</param>
        /// <returns></returns>
        private ResultCodes GetFileProperties(string filePath, out string collectedData) {
            Dictionary<string, string> fileProperties = new Dictionary<string, string>();
            ResultCodes resultCode = Lib.RetrieveFileProperties(m_taskId, m_cimvScope, filePath, out fileProperties);
            StringBuilder buffer = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in fileProperties) {
                if (buffer.Length > 0) {
                    buffer.Append(BdnaDelimiters.DELIMITER1_TAG);
                }
                buffer.Append(entry.Key + "=\"" + entry.Value + "\"");
            }
            collectedData = buffer.ToString();
            return resultCode;
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
                         .Append(attributeName)
                         .Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary> End Execution Tag </summary>
        private static string s_endTag = @"__BDNA__Execution_completed__BDNA__";

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

    }
}
