#region Copyright
/******************************************************************
*
*          Module: Windows Siebel Server Remote Execution Connection Script 
*                  It will verify siebel server manager remote exec privilege, 
*                  as well as WMI connection permission.
* Original Author: Alexander Meau
*   Creation Date: 2006/07/20
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
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    /// <summary>
    /// Validate credential for WMI Remote Execution and default WMI connection. 
    /// At the end, Connections to WMI cimv2 and default scope is created.
    /// </summary>
    public class WindowsSiebelSvrRemoteExecConnScript : IConnectionScriptRuntime {
        /// <summary>Connect method invoked by WinCs. </summary>
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="connectionParameterSets">List of credential sets</param>
        /// <param name="tftpDispatcher">Tftp Listener</param>
        /// <returns>Operation results.</returns>
        public ConnectionScriptResults Connect(
                long taskId,
                IDictionary<string, string>[] connectionParameterSets,
                string tftpPath,
                string tftpPath_login, 
                string tftpPath_password,
                ITftpDispatcher tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            ConnectionScriptResults result = null;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Connection script WindowsSiebelSvrRemoteExecConnScript.",
                                  m_taskId);

            if (null == connectionParameterSets) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to WindowsSiebelSvrRemoteExecConnScript.",
                                      m_taskId);
                result = new ConnectionScriptResults(null,
                                                     ResultCodes.RC_NULL_PARAMETER_SET,
                                                     0,
                                                     -1);
            } else {
                try {
                    ResultCodes resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Executing WindowsSiebelSvrRemoteExecConnScript with {1} credential sets.",
                                          m_taskId,
                                          connectionParameterSets.Length.ToString());

                    //
                    // Loop to process credential sets until a successful
                    // WMI connection is made.
                    for (int i = 0;
                         connectionParameterSets.Length > i;
                         ++i) {
                        ConnectionOptions co = new ConnectionOptions();
                        co.Impersonation = ImpersonationLevel.Impersonate;
                        co.Authentication = AuthenticationLevel.Packet;
                        co.Timeout = Lib.WmiConnectTimeout;

                        string userName = connectionParameterSets[i][@"userName"];
                        string password = connectionParameterSets[i][@"password"];
                        if (null != userName && null != password && !("." == userName && "." == password)) {
                            co.Username = userName;
                            co.Password = password;
                        }

                        Lib.Logger.TraceEvent(TraceEventType.Information,
                                              0,
                                              "Task Id {0}: Processing credential set {1}: user=\"{2}\".",
                                              m_taskId,
                                              i.ToString(),
                                              (null == co.Username) ? String.Empty : co.Username);

                        Stopwatch sw = new Stopwatch();
                        ManagementPath mp = null;

                        try {
                            string server = connectionParameterSets[i][@"address"];
                            //string server = connectionParameterSets[i][@"hostName"];

                            //
                            // Create a scope for the cimv2 namespace.
                            mp = new ManagementPath();
                            mp.Server = server;
                            mp.NamespacePath = ManagementScopeNames.CIMV;
                            ManagementScope cimvScope = new ManagementScope(mp, co);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Attempting connection to namespace {1}: server {2}.",
                                                  m_taskId,
                                                  mp.ToString(), server);
                            sw.Start();
                            cimvScope.Connect();
                            sw.Stop();

                            Debug.Assert(cimvScope.IsConnected);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                  m_taskId,
                                                  mp.ToString(),
                                                  sw.Elapsed.ToString());

                            //
                            // Create a scope for the default namespace.
                            mp = new ManagementPath();
                            mp.Server = server;
                            mp.NamespacePath = ManagementScopeNames.DEFAULT;
                            ManagementScope defaultScope = new ManagementScope(mp, co);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Attempting connection to namespace {1}.",
                                                  m_taskId,
                                                  mp.ToString());
                            sw.Reset();
                            defaultScope.Connect();
                            sw.Stop();
                            Debug.Assert(defaultScope.IsConnected);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                  m_taskId,
                                                  mp.ToString(),
                                                  sw.Elapsed.ToString());

                            Dictionary<string, object> connectionDic = new Dictionary<string, object>();
                            foreach (KeyValuePair<string, string> kvp in connectionParameterSets[i]) {
                                connectionDic.Add(kvp.Key, kvp.Value);
                            }
                            connectionDic[@"cimv2"] = cimvScope;
                            connectionDic[@"default"] = defaultScope;

                            //
                            // Ensure siebel credential is not null.
                            //
                            string[] credentialVariables = new String[] {@"installDirectory",
                                                                         @"SiebelUserName",
                                                                         @"SiebelUserPassword",
                                                                         @"gatewayServerName",
                                                                         @"serverName",
                                                                         @"enterpriseName"};                            
                            if (ResultCodes.RC_SUCCESS != this.ValidateScriptParameters(connectionDic, credentialVariables)) {
                                break;
                            } else {
                                if (connectionDic[@"installDirectory"].ToString().EndsWith(@"\")) {
                                    connectionDic[@"siebelSrvrmgrPath"] = connectionDic[@"installDirectory"].ToString() + @"BIN\SRVRMGR.EXE";
                                } else {
                                    connectionDic[@"siebelSrvrmgrPath"] = connectionDic[@"installDirectory"].ToString() + @"\BIN\SRVRMGR.EXE";
                                }
                                //
                                // Ensure path to siebel server manger is correct.
                                if (ResultCodes.RC_SUCCESS != ValidateSiebelSvrmgrBinaryPath(cimvScope,
                                                                                             connectionDic["siebelSrvrmgrPath"].ToString())) {
                                    continue;
                                }
                            }

                            // Validate temporary directory
                            if (ResultCodes.RC_SUCCESS != ValidateTemporaryDirectory(cimvScope,
                                                                                     connectionDic)) {
                                continue;
                            }

                            // Validate Siebel credential
                            string batchFile = BuildBatchFile(connectionDic);
                            if (!string.IsNullOrEmpty(batchFile) &&
                                ResultCodes.RC_SUCCESS == ExecuteRemoteProcess(cimvScope,
                                                                               batchFile,
                                                                               connectionDic[@"SiebelUserName"].ToString(),
                                                                               connectionDic,
                                                                               tftpPath,
                                                                               tftpPath_login, 
                                                                               tftpPath_password,
                                                                               tftpDispatcher)) {

                                return new ConnectionScriptResults(connectionDic,
                                                                   ResultCodes.RC_SUCCESS,
                                                                   0,
                                                                   i);
                            }
                        } catch (ManagementException me) {
                            if (ManagementStatus.AccessDenied == me.ErrorCode) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nWMI Error Code {3}",
                                                      m_taskId,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString(),
                                                      me.ErrorCode.ToString());

                                resultCode = ResultCodes.RC_LOGIN_FAILED;
                            } else {
                                //
                                // If we received a management exception that is *not*
                                // access denied, then there is no point in attempting
                                // additional logins with different user names/passwords.
                                Lib.LogManagementException(m_taskId,
                                                           sw,
                                                           String.Format("Connect to {0} failed.", mp.ToString()),
                                                           me);
                                continue;
                            }
                        } catch (UnauthorizedAccessException uae) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nMessage: {3}.",
                                                  m_taskId,
                                                  mp,
                                                  sw.Elapsed.ToString(),
                                                  uae.Message);
                            resultCode = ResultCodes.RC_LOGIN_FAILED;
                        } catch (COMException ce) {
                            if (0x800706BA == (UInt32)ce.ErrorCode) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nCOM Exception {3}.\n" +
                                                      "WMI port (135) may be closed or DCOM may not be properly configured",
                                                      m_taskId,
                                                      mp,
                                                      sw.Elapsed.ToString(),
                                                      ce.Message);
                            } else {
                                Lib.LogException(m_taskId,
                                                 sw,
                                                 String.Format("Connect to {0} failed", mp),
                                                 ce);
                            }
                            continue;
                        } catch (Exception ex) {
                            Lib.LogException(m_taskId,
                                             sw,
                                             String.Format("Connect to {0} failed", mp),
                                             ex);
                            continue;
                        }
                    }

                    //
                    // Connect failed after all credentials attempted.
                    if (null == result) {
                        result = new ConnectionScriptResults(null,
                                                             resultCode,
                                                             0,
                                                             connectionParameterSets.Length);
                    }
                } catch (Exception e) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsSiebelSvrRemoteExecConnScript.\n{1}",
                                          m_taskId,
                                          e.ToString());
                    //
                    // This is really an unanticipated fail safe.  We're
                    // going to report that *no* credentials were tried, which
                    // actually may not be true...
                    result = new ConnectionScriptResults(null,
                                                         ResultCodes.RC_PROCESSING_EXCEPTION,
                                                         0,
                                                         -1);
                }
            }

            Debug.Assert(null != result);
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Connection script WindowsSiebelSvrRemoteExecConnScript. Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
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
                Dictionary<string, object> scriptParameters) {
            string tempDir = @"%TMP%";

            //
            // If a temporary directory is not present in the credential
            // set, create one and set it to the TMP environment variable.
            if (!scriptParameters.ContainsKey(@"TemporaryDirectory") ||
                scriptParameters[@"TemporaryDirectory"] == null) {
                scriptParameters["TemporaryDirectory"] = tempDir;
                //
                // We can only validate directories that are not specified
                // by an environment variable.  
            } else if (!scriptParameters[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                if (scriptParameters[@"TemporaryDirectory"].ToString().Contains(@"%")) {
                    Lib.Logger.TraceEvent(TraceEventType.Warning,
                                          0,
                                          "Task Id {0}: Cannot verify temporary variable that use environment variable. \"{1}\".",
                                          m_taskId,
                                          scriptParameters[@"TemporaryDirectory"]);
                } else {
                    if (!Lib.ValidateDirectory(m_taskId, tempDir, scope)) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: temporary path does not exists. \"{1}\".",
                                              m_taskId,
                                              scriptParameters[@"TemporaryDirectory"]);
                        return ResultCodes.RC_PROCESS_EXEC_FAILED;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                    }
                }
            }
            return ResultCodes.RC_SUCCESS;
        }

        /// <summary>
        /// Verify that required script parameters are present
        /// and normalize the values.
        /// </summary>
        /// <param name="scriptParameters">script parameters</param>
        /// <param name="variableNames">variables to be verified</param>
        /// <returns>Verification Result</returns>
        private ResultCodes ValidateScriptParameters(
                Dictionary<string, object> scriptParameters, 
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
        /// Verify path to the siebel server manager executable is correct.
        /// </summary>
        /// <param name="scope">Management Scope</param>
        /// <param name="srvrmgrPath">Absolute path to the executable</param>
        /// <returns>Verification Result.</returns>
        private ResultCodes ValidateSiebelSvrmgrBinaryPath(
                ManagementScope scope,
                string srvrmgrPath) {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Attempt to vaidate file path for {1}",
                                  m_taskId,
                                  srvrmgrPath);

            ResultCodes resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
            if (Lib.ValidateFile(m_taskId, srvrmgrPath, scope)) {
                return ResultCodes.RC_SUCCESS;
            }
            Lib.Logger.TraceEvent(TraceEventType.Error,
                                  0,
                                  "Task Id {0}: Svrmgr.exe path is not valid: {1}.",
                                  m_taskId, 
                                  srvrmgrPath);

            return resultCode;
        }

        /// <summary>
        /// Generate the temporary batch file to execute on
        /// the remote host.  This batch file will attempt to
        /// run a simple command remotely to validate permission level.
        /// </summary>
        /// 
        /// <returns>batch file content</returns>
        private string BuildBatchFile(Dictionary<string, object> scriptParameters) {
            if (!scriptParameters.ContainsKey(@"siebelSrvrmgrPath") || scriptParameters[@"siebelSrvrmgrPath"] == null ||
                !scriptParameters.ContainsKey(@"gatewayServerName") || scriptParameters[@"gatewayServerName"] == null ||
                !scriptParameters.ContainsKey(@"serverName") || scriptParameters[@"serverName"] == null ||
                !scriptParameters.ContainsKey(@"enterpriseName") || scriptParameters[@"enterpriseName"] == null ||
                !scriptParameters.ContainsKey(@"SiebelUserName") || scriptParameters[@"SiebelUserName"] == null ||
                !scriptParameters.ContainsKey(@"SiebelUserPassword") || scriptParameters[@"SiebelUserPassword"] == null) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Error during batch file building: parameter cannot be null. .",
                                      m_taskId);
                return null;
            }
            string strSiebelShowCommand = String.Format("{0} /g {1} /s {2} /e {3} /u {4} /p {5} /c show",
                                                        scriptParameters[@"siebelSrvrmgrPath"],
                                                        scriptParameters[@"gatewayServerName"],
                                                        scriptParameters[@"serverName"],
                                                        scriptParameters[@"enterpriseName"],
                                                        scriptParameters[@"SiebelUserName"],
                                                        scriptParameters[@"SiebelUserPassword"]);

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Building batch file with siebel show command: {1}",
                                  m_taskId,
                                  strSiebelShowCommand);
            StringBuilder strBatchFile = new StringBuilder();
            strBatchFile.AppendLine(@"@ECHO OFF");
            strBatchFile.AppendLine(@"ECHO ^<BDNA^>");
            strBatchFile.AppendLine(strSiebelShowCommand);
            strBatchFile.AppendLine(@"ECHO ^</BDNA^>");
            strBatchFile.AppendLine(@"ECHO Execution completed.");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":END");
            return strBatchFile.ToString();
        }

        /// <summary>
        /// Spawn a remote process to execute a simple command remotely to ensure success of collction.
        /// </summary>
        /// 
        /// <param name="scope">WMI connection to target host.</param>
        /// <param name="tftpDispatcher">TFTP dispatcher singleton.</param>
        /// 
        /// <returns>Operation result code.</returns>
        private ResultCodes ExecuteRemoteProcess(
                ManagementScope scope,
                string batchFile,
                string SiebelUserName,
                IDictionary<string, object> scriptParameters,
                string tftpPath, 
                string tftpPath_login, 
                string tftpPath_password,
                ITftpDispatcher tftpDispatcher) {
            ResultCodes resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
            string stdout = String.Empty;
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Attempt to execute remote command connection.",
                                  m_taskId);

            using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile(m_taskId, scope, batchFile, scriptParameters, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                Stopwatch sw = Stopwatch.StartNew();
                resultCode = rp.Launch();
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Remote process operation completed with result code {1}.  Elapsed time {2}.",
                                      m_taskId,
                                      resultCode.ToString(),
                                      sw.Elapsed.ToString());
                stdout = rp.Stdout.ToString();

                if (ResultCodes.RC_SUCCESS == resultCode) {
                    if (null != stdout && 0 < stdout.Length) {                        
                        if (!stdout.Contains(@"Execution completed")) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Data returned is shorter than expected, possibly due to transfer failure.",
                                                  m_taskId);
                            resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                        }
                        //
                        // Check for siebel login error code.
                        if (stdout.Contains(@"Failed to connect server") || s_invalidSiebelUsernamePasswordRegex.IsMatch(stdout)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  @"Task Id {0}: Incorrect Siebel username or password supplied.\nSTDOUT/STDERR:\n{1}",
                                                  m_taskId,
                                                  stdout);
                            resultCode = ResultCodes.RC_LOGIN_FAILED;
                        } else if (stdout.Contains(@"SBL-ADM-02071") || s_invalidSibelEnterpriseRegex.IsMatch(stdout)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  @"Task Id {0}: Invalid Siebel Enterprise name.\nSTDOUT/STDERR:\n{1}",
                                                  m_taskId,
                                                  stdout);
                            resultCode = ResultCodes.RC_LOGIN_FAILED;
                        } else if (stdout.Contains(@"Fatal error") || s_invalidSiebelGatewayRegex.IsMatch(stdout)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  @"Task Id {0}: Invalid Siebel Gateway Server.\nSTDOUT/STDERR:\n{1}",
                                                  m_taskId,
                                                  stdout);
                            resultCode = ResultCodes.RC_LOGIN_FAILED;
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  @"Task Id {0}: Login successful with username {1}",
                                                  m_taskId,
                                                  SiebelUserName);
                            resultCode = ResultCodes.RC_SUCCESS;
                        }
                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                              m_taskId);
                        resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                    }
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Remote execution error.\n{1}",
                                          m_taskId,
                                          stdout);
                }
            }

            if (ResultCodes.RC_SUCCESS == resultCode) {
                //
                // If we didn't get the output expected from execution
                // of the batch file.  Force the result code to some
                // error value so that we try the next credential set.
                stdout = stdout.Replace("\r\n", "");
                if (0 >= stdout.Length || !s_bdnaRegex.IsMatch(stdout)) {
                    resultCode = ResultCodes.RC_LOGIN_FAILED; // @todo Did login really fail?  Perhaps this s/b processing exception
                }
            }
            return resultCode;
        }

        private string m_taskId;
        private static readonly Regex s_bdnaRegex = 
            new Regex("<BDNA>.*</BDNA>", RegexOptions.Compiled);
        private static readonly Regex s_invalidSiebelUsernamePasswordRegex =
            new Regex("Failed to connect.*: Login failed", RegexOptions.Compiled);
        private static readonly Regex s_invalidSibelEnterpriseRegex = 
            new Regex("Enterprise server .*not found in gateway server", RegexOptions.Compiled);
        private static readonly Regex s_invalidSiebelSvrNameRegex =
            new Regex("Connection to server .*has been lost", RegexOptions.Compiled);
        private static readonly Regex s_invalidSiebelGatewayRegex =
            new Regex("Could not open connection to Siebel Gateway configuration", RegexOptions.Compiled);

    }
}
