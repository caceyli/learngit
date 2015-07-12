#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/07/05
*
* Current Status
*       $Revision: 1.2 $
*           $Date: 2014/07/16 23:02:43 $
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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class WindowsOracleListenerNamesScript : ICollectionScriptRuntime {
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
        public CollectionScriptResults ExecuteTask(
                long taskId,
                long cleId,
                long elementId,
                long databaseTimestamp,
                long localTimestamp,
                IDictionary<string, string> attributes,
                IDictionary<string, string> scriptParameters,
                IDictionary<string, object> connection,
                string tftpPath,
                string tftpPath_login,
                string tftpPath_password,
                ITftpDispatcher tftpDispatcher) {
            string taskIdString = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            StringBuilder resultBuffer = new StringBuilder();
            IDictionary<string, string> resultDic = new Dictionary<string, string>();
            Stopwatch executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string strOracleHome = null;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleListenerNamesScript.",
                                  taskIdString);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleListenerNamesScript is null.",
                                          taskIdString);
                } 
                else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          taskIdString);
                }
                else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              taskIdString);
                    }
                }

                //Check OracleHome attributes
                if (!scriptParameters.ContainsKey("OracleHome")) {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing OracleHome parameter",
                                          taskIdString);
                }
                else {
                    strOracleHome = scriptParameters["OracleHome"].Trim();
                    if (strOracleHome.EndsWith(@"\")) {
                        strOracleHome = strOracleHome.Substring(0, strOracleHome.Length - 1);
                    }
                }

                // Check Remote Process Temp Directory
                if (!connection.ContainsKey(@"TemporaryDirectory")) {
                    connection[@"TemporaryDirectory"] = @"%TMP%";
                }
                else {
                    if (!connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                        if (!Lib.ValidateDirectory(taskIdString, connection[@"TemporaryDirectory"].ToString(), cimvScope)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} is not valid.",
                                                  taskIdString,
                                                  connection[@"TemporaryDirectory"].ToString());
                            resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                        }
                        else {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} has been validated.",
                                                  taskIdString,
                                                  connection[@"TemporaryDirectory"].ToString());
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string strLISTENERORA = strOracleHome.Trim() + @"\NETWORK\ADMIN\listener.ora";
                    if (!Lib.ValidateFile(taskIdString, strLISTENERORA, cimvScope)) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Oracle Home does not contain a server installation.  listener.ora is missing.\nSkipping the rest of the validation.",
                                              taskIdString);
                    }
                    else {
                        string commandLine = @"cmd /q /e:off /C " + @"type " + strOracleHome.Trim() + @"\NETWORK\ADMIN\listener.ora";                        
                        StringBuilder stdoutData = new StringBuilder();
                        using (IRemoteProcess rp = RemoteProcess.NewRemoteProcess(
                                            taskIdString,           // Task Id to log against.
                                            cimvScope,              // assuming Remote process uses cimv2 management scope
                                            commandLine,            // script supplied command line.
                                            null,                   // Optional working directory
                                            StdioRedirection.STDOUT,// Flags for what stdio files you want
                                            null,                   // Data to pass for stdin.
                                            connection,             // Script parameters passed to all collection script by WinCs
                                            tftpPath,
                                            tftpPath_login,
                                            tftpPath_password,
                                            tftpDispatcher)) {
                            //This method will block until the entire remote process operation completes.                        
                            resultCode = rp.Launch();
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Remote process operation completed with result code {1}.",
                                                  taskIdString,
                                                  resultCode.ToString());

                            if (resultCode == ResultCodes.RC_SUCCESS) {
                                stdoutData.Append(rp.Stdout.ToString());
                                if (rp.Stdout == null || rp.Stdout.Length <= 0) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: No data returned from remote process execution.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                          taskIdString);
                                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                                }
                            }
                            else {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Remote process execution failed.  Result code {1}.\n{2}",
                                                      taskIdString,
                                                      resultCode.ToString(),
                                                      rp.Stdout.ToString());
                            }
                        }

                        if (stdoutData != null && stdoutData.Length > 0) {
                            StringBuilder sb = new StringBuilder();
                            string[] arrOutput = stdoutData.ToString().Split("\r\n".ToCharArray());
                            string strListener = null;
                            bool matched = false;
                            foreach (string line in arrOutput) {
                                if (s_listenerName.IsMatch(line)) {
                                    if (!s_listenerNoName.IsMatch(line)) {
                                        Match m = s_listenerName.Match(line);
                                        strListener = m.Groups[1].Value;
                                        sb.AppendLine(strListener);
                                        resultDic[strListener] = strListener;
                                    }
                                }
                            }
                            if (0 < sb.Length ) {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Listener matched:\n{1}",
                                                      taskIdString,
                                                      sb.ToString());
                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: No Listener services matched.",
                                                      taskIdString);
                            }
                            foreach (KeyValuePair<string, string> kvp in resultDic)
                            {
                                if (resultBuffer.Length > 0)
                                {
                                    resultBuffer.Append(BdnaDelimiters.DELIMITER_TAG);
                                }
                                resultBuffer.Append(kvp.Value);
                            }

                            if (!attributes.ContainsKey("listenerNames")) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Attribute \"listenerNames\" missing from attributeSet.",
                                                      taskIdString);
                            } else if (0 == resultBuffer.Length) {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                                      taskIdString);
                            } else {
                                dataRow.Append(elementId).Append(',')
                                    .Append(attributes["listenerNames"]).Append(',')
                                    .Append(scriptParameters["CollectorId"]).Append(',')
                                    .Append(taskId).Append(',')
                                    .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                                    .Append("listenerNames").Append(',')
                                    .Append(BdnaDelimiters.BEGIN_TAG).Append(resultBuffer.ToString()).Append(BdnaDelimiters.END_TAG);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleListenerNamesScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          taskIdString,
                                          executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleListenerNamesScript.  Elapsed time {1}.\n{2}",
                                          taskIdString,
                                          executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleListenerNamesScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        private static readonly Regex s_listenerName = new Regex(@"^(\w+)\s+=$", RegexOptions.Compiled);
        private static readonly Regex s_listenerNoName = new Regex(@"ADR_BASE|SID_LIST", RegexOptions.Compiled);

    }
}

