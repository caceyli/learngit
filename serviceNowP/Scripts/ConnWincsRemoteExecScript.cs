#region Copyright

/******************************************************************
*
*          Module: Windows Connection Script 
*                  that will verify remote exec privilege, 
*                  as well as WMI connection permission.
* Original Author: Alexander Meau
*   Creation Date: 2006/07/05
*
* Current Status
*       $Revision: 1.25 $
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
using System.Collections;
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
    public class ConnWincsRemoteExecScript : IConnectionScriptRuntime {
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
                                  "Task Id {0}: Connection script ConnWincsRemoteExecScript.",
                                  m_taskId);


            if (null == connectionParameterSets) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to ConnWincsRemoteExecScript.",
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
                                          "Task Id {0}: Executing ConnWincsRemoteExecScript with {1} credential sets.",
                                          m_taskId,
                                          connectionParameterSets.Length.ToString());
                    //IDictionary<string, string>[] orderedConnectionParameterSets = this.reorderCredentials(connectionParameterSets);
                    List<int> orderedConnections = this.reorderCredentials(connectionParameterSets);
                    //
                    // Loop to process credential sets until a successful
                    // WMI connection is made.
                    for (int j = 0;
                        orderedConnections.Count > j;
                        ++j) {
                        ConnectionOptions co = new ConnectionOptions();
                        co.Impersonation = ImpersonationLevel.Impersonate;
                        co.Authentication = AuthenticationLevel.Packet;
                        co.Timeout = Lib.WmiConnectTimeout;

                        int i = orderedConnections[j];
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
                        if (string.IsNullOrEmpty(userName)) {
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Unexpected Error: Receiving null username at Credential set {1}: user=\"{2}\".",
                                                  m_taskId,
                                                  i.ToString(),
                                                  userName);
                            continue;
                        }
                        Stopwatch sw = new Stopwatch();
                        ManagementPath mp = null;
                        try {
                            string server = connectionParameterSets[i][@"address"];
                            string domain = string.Empty;
                            if (connectionParameterSets[i].ContainsKey(@"osWkgrpDomain")) {
                                domain = connectionParameterSets[i][@"osWkgrpDomain"];
                                if (domain == @"__BDNA_DEFAULT__") {
                                    domain = "";
                                }
                            }
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Attempting connection to computer in domain {1}.",
                                                  m_taskId,
                                                  domain);

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
                            string tempDir = null;

                            if (ResultCodes.RC_SUCCESS == ValidateTemporaryDirectory(cimvScope,
                                                                                     connectionParameterSets[i],
                                                                                     connectionDic,
                                                                                     ref tempDir)) {
                                string batchFile = BuildBatchFile();
                                if (ResultCodes.RC_SUCCESS == ExecuteRemoteProcess(cimvScope,
                                                                                   batchFile,
                                                                                   connectionDic,
                                                                                   tftpPath,
                                                                                   tftpPath_login,
                                                                                   tftpPath_password,
                                                                                   tftpDispatcher)) {
                                    result = new ConnectionScriptResults(connectionDic,
                                                                         ResultCodes.RC_SUCCESS,
                                                                         0,
                                                                         i);
                                    break;
                                }
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
                                //break;
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
                            //break;
                        } catch (Exception ex) {
                            Lib.LogException(m_taskId,
                                             sw,
                                             String.Format("Connect to {0} failed", mp),
                                             ex);
                            //break;
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
                                          "Task Id {0}: Unhandled exception in ConnWincsRemoteExecScript.\n{1}",
                                          m_taskId,
                                          e.ToString());

                    //
                    // This is really an unanticipated fail safe.  We're
                    // going to report that *no* credentials were tried, which
                    // actually may not be true...
                    result = new ConnectionScriptResults(null,
                                                         ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR,
                                                         0,
                                                         -1);
                }
            }

            Debug.Assert(null != result);
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Connection script ConnWincsRemoteExecScript.  Elapsed time {1}.  Result code {2}.",
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
                ManagementScope                 scope,
                IDictionary<string, string>     connectionParameterSet,
                Dictionary<string, object>      scriptParameters,
                ref string                      tempDir) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            tempDir = @"%TMP%";

            //
            // If a temporary directory is not present in the credential
            // set, create one and set it to the TMP environment variable.
            if (!connectionParameterSet.ContainsKey("TemporaryDirectory")) {
                scriptParameters["TemporaryDirectory"] = tempDir;

            //
            // We can only validate directories that are not specified
            // by an environment variable.  Ane we're going to
            // assume the only environment variable anyone would ever
            // set is %TMP%
            } else if (!scriptParameters[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                tempDir = scriptParameters[@"TemporaryDirectory"].ToString();

                if (!Lib.ValidateDirectory(m_taskId, tempDir, scope)) {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                }
            }
            return resultCode;
        }

               
        /// <summary>
        /// Generate the temporary batch file to execute on
        /// the remote host.  This batch file will attempt to
        /// run a simple command remotely to validate permission level.
        /// </summary>
        /// 
        /// <returns>Operation result code.</returns>
        private string BuildBatchFile() {
            StringBuilder strBatchFile = new StringBuilder();
            strBatchFile.AppendLine(@"@ECHO OFF");
            strBatchFile.AppendLine(@"ECHO ^<BDNA^>");
            strBatchFile.AppendLine(@"DIR C:\");
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
                ManagementScope                 scope,
                string                          batchFile,
                IDictionary<string, object>     scriptParameters,
                string                          tftpPath,
                string                          tftpPath_login, 
                string                          tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
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
                        stdout = stdout.Replace("\r\n", "");
                        if (!stdout.Contains(@"Execution completed")) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Exception with batch file return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                  m_taskId);
                            resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                        }
                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                              m_taskId);
                        resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
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
                if (0 >= stdout.Length || !s_bdnaRegex.IsMatch(stdout)) {
                    resultCode = ResultCodes.RC_LOGIN_FAILED; // @todo Did login really fail?  Perhaps this s/b processing exception
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Re-order connection credential so that credential of same domain will be executed first.
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        private List<int> reorderCredentials(IDictionary<string, string>[] connectionParameters) {
            //IDictionary<string, string>[] orderedCredentials = null;
            ArrayList remainingCredentials = new ArrayList();
            bool hasReorderRecord = false;
            int index = 0;
            List<int> credOrder = null;
            List<int> remainingCred = null;

            if (connectionParameters != null && connectionParameters.Length > 0) {
                //orderedCredentials = new IDictionary<string, string>[connectionParameters.Length];
                credOrder = new List<int>();
                remainingCred = new List<int>();

                foreach (IDictionary<string, string> cred in connectionParameters) {
                    if (cred.ContainsKey(@"osWkgrpDomain") && cred.ContainsKey(@"userName")) {
                        if (cred[@"osWkgrpDomain"] != @"__BDNA_DEFAULT__") {
                            string domainName = string.Empty;
                            if (cred[@"userName"].Contains(@"\")) {
                                domainName = cred[@"userName"].Substring(0, cred["userName"].IndexOf(@"\"));
                            }
                            if (cred["osWkgrpDomain"].Equals(domainName, StringComparison.InvariantCultureIgnoreCase)) {
                                //orderedCredentials[index++] = cred;
                                credOrder.Add(index++);
                                hasReorderRecord = true;
                            } else {
                                remainingCredentials.Add(cred);
                                remainingCred.Add(index++);
                            }
                        }
                    }
                }
                if (remainingCredentials.Count > 0) {
                    int counter = 0;
                    foreach (IDictionary<string, string> cred in remainingCredentials) {
                        //orderedCredentials[index++] = (IDictionary<string, string>)cred;
                        credOrder.Add(remainingCred[counter++]);
                        hasReorderRecord = true;
                    }
                }
            }
            if (!hasReorderRecord) {
                for (int i = 0; connectionParameters.Length > i; ++i) {
                    credOrder.Add(i);
                }
            }
            return credOrder;
        }

        private string m_taskId;
        private static readonly Regex s_bdnaRegex = new Regex("<BDNA>.*</BDNA>", RegexOptions.Compiled);
    }
}
