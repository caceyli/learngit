#region Copyright
/******************************************************************
*
*          Module: Windows Remote Command Execution Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/10/10
*
* Current Status
*       $Revision: 1.11 $
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
    public class WindowsRemoteCommandExecutionScript : ICollectionScriptRuntime {
        /// <summary>
        /// Execute an arbitrary command remotely.
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

            m_taskId = taskId.ToString();
            m_executionTimer = Stopwatch.StartNew();
            string commandLine = string.Empty;

            ResultCodes resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsRemoteCommandExecutionScript.",
                                  m_taskId);
            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsRemoteCommandExecutionScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }

                    //
                    // We have to massage the script parameter set, replace
                    // any keys with a colon by just the part after the colon.
                    Dictionary<string, string> d = new Dictionary<string, string>();

                    foreach (KeyValuePair<string, string> kvp in scriptParameters) {
                        string[] sa = kvp.Key.Split(s_collectionParameterSetDelimiter,
                                                    StringSplitOptions.RemoveEmptyEntries);
                        Debug.Assert(sa.Length > 0);
                        d[sa[sa.Length - 1]] = kvp.Value;
                    }

                    scriptParameters = d;

                    //Check commandLine parameter
                    if (!scriptParameters.ContainsKey("commandLine")) {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter command Line.",
                                              m_taskId);
                    } else {
                        commandLine = scriptParameters[@"commandLine"];

                        // Both Working directory and temporary need to be collected as part of Windows Credential.
                        // BUG 14064 has been filed to track this problem. 
                        // Check working and temporary directory parameter
                        if (!connection.ContainsKey(@"WorkingDirectory") || string.IsNullOrEmpty(connection[@"WorkingDirectory"].ToString())) {
                            connection[@"WorkingDirectory"] = @"%TMP%";
                        }
                        if (!connection.ContainsKey(@"TemporaryDirectory") || string.IsNullOrEmpty(connection[@"TemporaryDirectory"].ToString())) {
                            connection[@"TemporaryDirectory"] = @"%TMP%";
                        }

                        if (!attributes.ContainsKey(@"commandResult")) {
                            resultCode = ResultCodes.RC_NULL_ATTRIBUTE_SET;
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Missing command result attribute.",
                                                  m_taskId);
                        } else {

                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Attempting to retrieve command output {1}.",
                                                  m_taskId,
                                                  commandLine);

                            String batchFileContent = this.BuildBatchFile(connection[@"WorkingDirectory"].ToString(),
                                                                          scriptParameters[@"commandLine"].ToString());
                            String collectedData = "";
                            using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile(
                                                m_taskId,                   // Task Id to log against.
                                                cimvScope,                  // assuming Remote process uses cimv2 management scope
                                                batchFileContent,           // batch file
                                                connection,                 // connection dictionary.
                                                tftpPath,
                                                tftpPath_login,
                                                tftpPath_password,
                                                tftpDispatcher)) {
                                //This method will block until the entire remote process operation completes.
                                resultCode = rp.Launch();
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Batch file executed completed with result code {1}.",
                                                      m_taskId,
                                                      resultCode.ToString());

                                if (resultCode == ResultCodes.RC_SUCCESS) {
                                    collectedData = rp.Stdout.ToString();
                                    if (string.IsNullOrEmpty(collectedData)) {
                                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                                              0,
                                                              "Task Id {0}: Script completed sucessfully with no data to return.",
                                                              m_taskId);
                                        resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                    } else if (!collectedData.Contains(@"Execution completed")) {
                                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                                              0,
                                                              "Task Id {0}: Exception with batch file return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION. Partial result: {1}",
                                                              m_taskId,
                                                              collectedData);
                                        resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                    } else {
                                        collectedData = collectedData.Substring(0, collectedData.Length - @"Execution completed.\r\n".Length);
                                        m_outputData.Append(elementId).Append(',')
                                            .Append(attributes[@"commandResult"]).Append(',')
                                            .Append(scriptParameters[@"CollectorId"]).Append(',')
                                            .Append(m_taskId).Append(',')
                                            .Append(databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                                            .Append(@"commandResult").Append(',')
                                            .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
                                    }
                                } else {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Error during command execution. Elapsed time {1}.  Result code {2}.",
                                                          m_taskId,
                                                          m_executionTimer.Elapsed.ToString(),
                                                          resultCode);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unhandled exception in WindowsRemoteCommandExecutionScript.  Elapsed time {1}.\n{2}",
                                      m_taskId,
                                      m_executionTimer.Elapsed.ToString(),
                                      ex.ToString());
            }
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsRemoteCommandExecutionScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode);
            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_outputData.ToString());
                  
        }


        /// <summary>
        /// Generate the temporary batch file to execute on
        /// the remote host.  This batch file will attempt to
        /// login to Oracle to validate our credentials.
        /// </summary>
        /// 
        /// <param name="strTempDir">Temporary directory on remote host.</param>
        /// <param name="strOracleHome">Oracle home directory location.</param>
        /// <param name="strSchemaName">Schema name to login in with.</param>
        /// <param name="strSchemaPassword">Schema password to login with.</param>
        /// 
        /// <returns>Operation result code.</returns>
        private string BuildBatchFile(
                string strWorkingDir,
                string strCommandLine) {
            StringBuilder strBatchFile = new StringBuilder();
            strBatchFile.AppendLine(@"@ECHO OFF");
            if (strWorkingDir != @"%TMP%") {
                strBatchFile.Append(@"IF NOT EXIST ").Append(strWorkingDir)
                            .AppendLine(@"\%1 GOTO :ERROR_WORKING_DIR_NOT_EXISTS");
                strBatchFile.AppendLine();
            }

            strBatchFile.AppendLine(@":EXECUTION");
            strBatchFile.Append(@"CD ").AppendLine(strWorkingDir);
            strBatchFile.AppendLine(strCommandLine + @" 2>&1");
            strBatchFile.AppendLine(@"GOTO :SUCCESS");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":ERROR_NULL_PARAMETER");
            strBatchFile.AppendLine(@"ECHO ERROR- null batch parameter.");
            strBatchFile.AppendLine(@"GOTO :END");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":ERROR_WORKING_DIR_NOT_EXISTS");
            strBatchFile.AppendLine(@"ECHO ERROR- working directory does not exists.");
            strBatchFile.AppendLine(@"CD %TMP%");
            strBatchFile.AppendLine(@"GOTO :EXECUTION");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":SUCCESS");
            strBatchFile.AppendLine(@"ECHO Execution completed.");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":END");

            return strBatchFile.ToString();
        }

        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        /// <summary>output data buffer.</summary>
        private StringBuilder m_outputData = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>
        /// Delimiter used to strip bogus data from the beginning
        /// of some collection parameter set table entries.
        /// </summary>
        private static readonly char[] s_collectionParameterSetDelimiter = new char[] { ':' };
    }
}
