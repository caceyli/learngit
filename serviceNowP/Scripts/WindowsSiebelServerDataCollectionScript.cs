#region Copyright
/******************************************************************
*
*          Module: Windows Siebel Server Data COllection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/07/17
*
* Current Status
*       $Revision: 1.9 $
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
using System.Collections;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class WindowsSiebelServerDataCollectionScript : ICollectionScriptRuntime {
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
        /// 
        /// <returns>Collection results.</returns>
        public CollectionScriptResults ExecuteTask(
                long                            taskId,
                long                            cleId,
                long                            elementId,
                long                            databaseTimestamp,
                long                            localTimestamp,
                IDictionary<string, string>     attributes,
                IDictionary<string, string>     scriptParameters,
                IDictionary<string, object>     connection,
                string                          tftpPath,
                string                          tftpPath_login,
                string                          tftpPath_password,
                ITftpDispatcher                 tftpDispatcher)  {
            m_taskId = taskId.ToString();
            Stopwatch executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            StringBuilder dataRow = new StringBuilder();

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsSiebelServerDataCollectionScript.",
                                  m_taskId);

            try {
                //
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsSiebelServerDataCollectionScript is null.",
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
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              m_taskId);
                    }
                }

                //
                // Check Siebel Credential
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string[] connectionVariables = new String[] {@"SiebelUserName",
                                                                 @"SiebelUserPassword",
                                                                 @"TemporaryDirectory",
                                                                 @"siebelSrvrmgrPath"};
                    resultCode = this.ValidateConnectionParameters(connection, connectionVariables);
                }

                //
                // Check Parameter variables.
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string[] paramVariables = new String[] { @"installDirectory", 
                                                             @"gatewayServerName",
                                                             @"serverName",
                                                             @"enterpriseName",
                                                             @"siebelServerCommand"};
                    resultCode = this.ValidateScriptParameters(scriptParameters, paramVariables);
                }

                //
                // Check Temporary Directory
                string tempDir = connection[@"TemporaryDirectory"].ToString();
                resultCode = this.ValidateTemporaryDirectory(cimvScope, ref tempDir);

                // Execute Siebel Command
                string strBatchFileContent = BuildBatchFile(connection[@"siebelSrvrmgrPath"].ToString(),
                                                  scriptParameters[@"gatewayServerName"],
                                                  scriptParameters[@"serverName"],
                                                  scriptParameters[@"enterpriseName"],
                                                  connection[@"SiebelUserName"].ToString(),
                                                  connection[@"SiebelUserPassword"].ToString(),
                                                  scriptParameters[@"siebelServerCommand"],
                                                  scriptParameters[@"outputDisplayColumns"]);

                string stdout = null;
                using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile
                    (taskId.ToString(), cimvScope, strBatchFileContent, connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                    //
                    //This method will block until the entire remote process operation completes.
                    resultCode = rp.Launch();
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Remote process operation completed with result code {1}.",
                                          m_taskId,
                                          resultCode.ToString());

                    if (resultCode == ResultCodes.RC_SUCCESS) {                        
                        if (rp.Stdout != null && rp.Stdout.Length > 0) {
                            stdout = rp.Stdout.ToString();
                            string commandOutput = null;
                            if (!stdout.Contains(@"Execution completed")) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Data returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                      m_taskId);
                                resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                            } else {
                                //
                                // package command output result.
                                if (ResultCodes.RC_SUCCESS == ParseCommandOutput(stdout, 
                                                                                 scriptParameters[@"siebelServerCommand"],
                                                                                 scriptParameters[@"outputDisplayColumns"],
                                                                                 out commandOutput)) {
                                    if (!attributes.ContainsKey(s_returnAttribute)) {
                                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                                              0,
                                                              "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                                              m_taskId,
                                                              s_returnAttribute);
                                    } else {
                                        dataRow.Append(elementId).Append(',')
                                            .Append(attributes[s_returnAttribute]).Append(',')
                                            .Append(scriptParameters[@"CollectorId"]).Append(',')
                                            .Append(taskId).Append(',')
                                            .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                                            .Append(s_returnAttribute).Append(',')
                                            .Append(BdnaDelimiters.BEGIN_TAG).Append(commandOutput).Append(BdnaDelimiters.END_TAG);
                                    }
                                }
                            }
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                  taskId.ToString());
                            resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                        }
                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Remote execution error.\nSTDOUT.STDERR:\n{1}",
                                              m_taskId,
                                              rp.Stdout.ToString());
                    }
                }
                //// post processing??
                //if (resultCode == ResultCodes.RC_SUCCESS && string.IsNullOrEmpty( > 0) {
                //    //foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                //    //    entry.Value.ResultHandler(this, entry.Key, stdoutData.ToString());
                //    //}
                //}

            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsSiebelServerDataCollectionScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsSiebelServerDataCollectionScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsSiebelServerDataCollectionScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        /// <summary>
        /// Parse Command Line output.
        /// </summary>
        /// <param name="strOutput">Command Output</param>
        /// <param name="strColumnHeader">Column Header</param>
        /// <param name="strFormmatedOutput">Formatted Output</param>
        /// <returns>Result Code</returns>
        private ResultCodes ParseCommandOutput(string strOutput, 
                                               string strServerCommand,
                                               string strColumnHeader,
                                               out string strFormmatedOutput) {            
            //
            // Return column header and command output cannot be null.
            strFormmatedOutput = null;
            if (string.IsNullOrEmpty(strOutput) || string.IsNullOrEmpty(strColumnHeader)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Header Text or Command Output is empty. <BDNA,>{1}<BDNA,>{2}<BDNA,>",
                                      m_taskId,
                                      strOutput,
                                      strColumnHeader);
                return ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
            }
            ArrayList resultArray = new ArrayList();
            string[] expectedHeaderTexts = strColumnHeader.Split(new string[] { @"<BDNA,>" }, 
                                                         StringSplitOptions.RemoveEmptyEntries);

            StringBuilder formattedOutputBuilder = new StringBuilder();
            strOutput = strOutput.Replace("\r", "");
            strOutput = strOutput.Replace("\n", BdnaDelimiters.DELIMITER_TAG);
            string[] commandStrings = strOutput.Split(new string[] { BdnaDelimiters.DELIMITER1_TAG },
                                                      StringSplitOptions.RemoveEmptyEntries);
            IList<string> columnNames = new List<string>();
            IList<string[]> valuesArrays = new List<string[]>();

            foreach (string commandString in commandStrings) {
                Match match = s_svrCommandFormat.Match(commandString);
                if (match.Success) {
                    // Since regular expression has requested non-empty string,
                    //  header and value should never be null.
                    string header = match.Groups[1].ToString().Trim();
                    string dataReturned = match.Groups[2].ToString();
                    string[] values = match.Groups[2].ToString().Split(new string[] {BdnaDelimiters.DELIMITER_TAG},
                                                                       StringSplitOptions.None);
                    // Since regular expression has requested integer,
                    //   parsing to integer should not give exception.
                    int rowsReturned = int.Parse(match.Groups[3].ToString());

                    if (values.Length != rowsReturned) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              @"Task Id {0}: Rows <{1}> returned <{2}> is different than actual values <{3}>",
                                              m_taskId,
                                              header,
                                              rowsReturned,
                                              values.Length);
                        break;
                    }
                    columnNames.Add(header);
                    valuesArrays.Add(values);                    
                }
            }
            
            //
            // It is important to validate rows returned from different views.
            // Otherwise, we will have collation error.
            int totalRows = 0;
            for (int i=0; i < valuesArrays.Count; i++) {
                if (i==0) {
                    totalRows = valuesArrays[i].Length;
                } else {
                    if (totalRows != valuesArrays[i].Length) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              @"Task Id {0}: Rows returned is not matching for different views. ",
                                              m_taskId);
                        return ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                    }
                }
            }
             
            StringBuilder builder = new StringBuilder();
            foreach (string name in columnNames) {
                if (builder.Length > 0) {
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG);
                }
                builder.Append(name.Trim());
            }
            for (int j=0; j < totalRows; j++) {
                builder.Append(BdnaDelimiters.DELIMITER1_TAG);
                bool firstRecod = true;
                foreach(string[] arr in valuesArrays) {
                    if (j < arr.Length) {
                        if (!firstRecod) {
                            builder.Append(BdnaDelimiters.DELIMITER2_TAG);
                        }
                        firstRecod = false;
                        builder.Append(arr[j].Trim());
                    }
                }
            }
            if (builder.Length > 0) {
                strFormmatedOutput = builder.ToString();
                return ResultCodes.RC_SUCCESS;
            }

            return ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
        }

        /// <summary>
        /// Verify that required script parameters are present
        /// and normalize the values.
        /// </summary>
        /// <param name="scriptParameters">script parameters</param>
        /// <param name="variableNames">variables to be verified</param>
        /// <returns>Verification Result</returns>
        private ResultCodes ValidateScriptParameters(
                IDictionary<string, string> scriptParameters,
                string[] variableNames) {
            foreach (string varName in variableNames) {
                if (!scriptParameters.ContainsKey(varName) || String.IsNullOrEmpty(scriptParameters[varName])) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"{1}\".",
                                          m_taskId,
                                          varName);
                    return ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    scriptParameters[varName] = scriptParameters[varName].Trim();
                }
            }
            return ResultCodes.RC_SUCCESS;
        }

        /// <summary>
        /// Verify that required connection parameters are present
        /// and normalize the values.
        /// </summary>
        /// <param name="scriptParameters">script parameters</param>
        /// <param name="variableNames">variables to be verified</param>
        /// <returns>Verification Result</returns>
        private ResultCodes ValidateConnectionParameters(
                IDictionary<string, object> scriptParameters,
                string[] variableNames) {
            foreach (string varName in variableNames) {
                if (!scriptParameters.ContainsKey(varName) || scriptParameters[varName] == null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"{1}\".",
                                          m_taskId,
                                          varName);
                    return ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    scriptParameters[varName] = scriptParameters[varName].ToString().Trim();
                }
            }
            return ResultCodes.RC_SUCCESS;
        }

        /// <summary>
        /// Validate temporary directory on remote host.
        /// </summary>
        /// 
        /// <param name="scope">WMI connection to remote host.</param>
        /// <param name="connectionParameterSet">Current credential set to validate.</param>
        /// <param name="scriptParameters">Script parameters to use.</param>
        /// <param name="tempDir">Temporary directory path on remote host.</param>
        /// 
        /// <returns>Operation result code.</returns>
        private ResultCodes ValidateTemporaryDirectory(
                ManagementScope scope,
                ref string strTempDir) {
            //
            // If a temporary directory is not present in the credential
            // set, create one and set it to the TMP environment variable.
            if (string.IsNullOrEmpty(strTempDir)) {
                strTempDir = @"%TMP%";
                //
                // We can only validate directories that are not specified
                // by an environment variable.  
            } else if (!strTempDir.Equals(@"%TMP%")) {
                if (strTempDir.Contains(@"%")) {
                    Lib.Logger.TraceEvent(TraceEventType.Warning,
                                          0,
                                          "Task Id {0}: Cannot verify temporary variable that use environment variable. \"{1}\".",
                                          m_taskId,
                                          strTempDir);
                } else {
                    if (!Lib.ValidateDirectory(m_taskId, strTempDir, scope)) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: temporary path does not exists. \"{1}\".",
                                              m_taskId,
                                              strTempDir);
                        return ResultCodes.RC_PROCESS_EXEC_FAILED;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                    }
                }
            }
            return ResultCodes.RC_SUCCESS;
        }


        /// <summary>
        /// Generate the temporary batch file to execute on
        /// the remote host.  This batch file will attempt to
        /// run a simple command remotely to validate permission level.
        /// </summary>
        /// 
        /// <param name="strSiebelSrvrmgrPath">Siebel Server Command</param>
        /// <param name="strGatewayServerName">Siebel Gateway Server Name</param>
        /// <param name="strServerName">Siebel Server Name</param>
        /// <param name="strEnterpriseName"></param>
        /// <param name="strSiebelUserName">Siebel User Name</param>
        /// <param name="strSiebelUserPassword">Siebel User Password</param>
        /// <param name="strSiebelServerCommand">Siebel Server Command</param>
        /// <returns>Batch File Content</returns>
        private string BuildBatchFile(string strSiebelSrvrmgrPath, 
                                      string strGatewayServerName,
                                      string strServerName,
                                      string strEnterpriseName,
                                      string strSiebelUserName,
                                      string strSiebelUserPassword,
                                      string strSiebelServerCommand,
                                      string strOutputDisplayColumns) {
            if (string.IsNullOrEmpty(strSiebelSrvrmgrPath) || string.IsNullOrEmpty(strGatewayServerName) ||
                string.IsNullOrEmpty(strServerName) || string.IsNullOrEmpty(strEnterpriseName) || 
                string.IsNullOrEmpty(strSiebelUserName) || string.IsNullOrEmpty(strSiebelUserPassword) ||
                string.IsNullOrEmpty(strSiebelServerCommand) || string.IsNullOrEmpty(strOutputDisplayColumns)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Error during batch file building: parameter cannot be null. .",
                                      m_taskId);
                return null;
            }
            StringBuilder strSiebelCommands = new StringBuilder();
            string[] displayColumns = strOutputDisplayColumns.Split(new string[] { "<BDNA,>" },
                                                                    StringSplitOptions.RemoveEmptyEntries);
            foreach (string strValue in displayColumns) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Building batch file with siebel show command: {1}",
                                      m_taskId,
                                      strSiebelCommands.ToString());

                if (strSiebelCommands.Length > 0) {
                    strSiebelCommands.AppendLine(@"ECHO ^<BDNA,1^>");
                }
                strSiebelCommands.AppendLine(String.Format(@"{0} /g {1} /s {2} /e {3} /u {4} /p {5} /c ""{6} SHOW {7}"" 2>&1",
                                                           strSiebelSrvrmgrPath,
                                                           strGatewayServerName,
                                                           strServerName,
                                                           strEnterpriseName,
                                                           strSiebelUserName,
                                                           strSiebelUserPassword,
                                                           strSiebelServerCommand,
                                                           strValue));
            }

            StringBuilder strBatchFile = new StringBuilder();
            strBatchFile.AppendLine(@"@ECHO OFF");
            strBatchFile.AppendLine(@"ECHO ^<BDNA^>");
            strBatchFile.AppendLine(strSiebelCommands.ToString());
            strBatchFile.AppendLine(@"ECHO ^</BDNA^>");
            strBatchFile.AppendLine(@"ECHO Execution completed.");
            return strBatchFile.ToString();
        }

        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        private static readonly string s_returnAttribute = @"siebelServerData";
        private static readonly Regex s_bdnaRegex =
            new Regex(@"<BDNA>.*</BDNA>", RegexOptions.Compiled);
        private static readonly Regex s_svrCommandFormat =
            new Regex(@"SHOW .+?<BDNA,><BDNA,>(.+?)<BDNA,>[\s\-]+<BDNA,>(.+)<BDNA,><BDNA,>(\d+) rows returned.", RegexOptions.Compiled);
            //new Regex(@"\n(.+?)\n[\s\-]+\n([.]+)?\n", RegexOptions.Compiled);
        private static readonly Regex s_outputRowEndMarkerRegex =
            new Regex(@"\\d+ rows returned.", RegexOptions.Compiled);
    }
}
