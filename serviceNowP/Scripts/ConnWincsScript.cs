#region Copyright

/******************************************************************

*

*          Module: Windows Collection Scripts

* Original Author: Mike Frost

*   Creation Date: 2006/01/17

*

* Current Status

*       $Revision: 1.41 $

*           $Date: 2014/10/11 07:46:03 $

*         $Author: MiyaChen $

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
using System.Collections;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Class to create WMI connections for use by collection
    /// scripts.  Connections are created for both the cimv2
    /// and default scopes.
    /// </summary>
    public class ConnWincsScript : IConnectionScriptRuntime {

        /// <summary>
        /// Connect method invoked by WinCs.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="connectionParameterSets">List of credential sets to use for connecting to the
        ///     remote database server.</param>
        /// <param name="tftpDispatcher">TFTP transfer request listener for dispatching TFTP transfer
        ///     requests.</param>
        /// 
        /// <returns>Operation results.</returns>
        public ConnectionScriptResults Connect(
                long                            taskId,
                IDictionary<string, string>[]   connectionParameterSets,
                string tftpPath,
                string tftpPath_login,
                string tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            string taskIdString = taskId.ToString();
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Connection script ConnWincsScript.",
                                  taskIdString);
            ConnectionScriptResults result = null;

            if (null == connectionParameterSets) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to ConnWincsScript",
                                      taskIdString);
                result = new ConnectionScriptResults(null,
                                                     ResultCodes.RC_NULL_PARAMETER_SET,
                                                     0,
                                                     -1);
            } else {
                try {
                    ResultCodes resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Executing ConnWincsScript with {1} credential sets.",
                                          taskIdString,
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
                        co.EnablePrivileges = true;  // @todo configuration

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
                                              taskIdString,
                                              i.ToString(),
                                              userName);

                        if (string.IsNullOrEmpty(userName)) {

                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Unexpected Error: Receiving null username at Credential set {1}: user=\"{2}\".",
                                                  taskIdString,
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
                                                  taskIdString,
                                                  domain);

                            //
                            // Create a scope for the cimv2 namespace.
                            mp = new ManagementPath();
                            mp.Server = server;
                            mp.NamespacePath = ManagementScopeNames.CIMV;

                            ManagementScope cimvScope = new ManagementScope(mp, co);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Attempting connection to namespace {1}.",
                                                  taskIdString,
                                                  mp.ToString());
                            sw.Start();
                            cimvScope.Connect();
                            sw.Stop();
                            Debug.Assert(cimvScope.IsConnected);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                  taskIdString,
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
                                                  taskIdString,
                                                  mp.ToString());
                            sw.Reset();
                            defaultScope.Connect();
                            sw.Stop();
                            Debug.Assert(defaultScope.IsConnected);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                  taskIdString,
                                                  mp.ToString(),
                                                  sw.Elapsed.ToString());

                            ManagementScope tsScope = null;
                            try {
                                //
                                // Create a scope for the TerminalServices namespace.
                                mp = new ManagementPath();
                                mp.Server = server;
                                mp.NamespacePath = @"root\CIMV2\TerminalServices";
                                co.Authentication = AuthenticationLevel.PacketPrivacy;
                                tsScope = new ManagementScope(mp, co);
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Attempting connection to namespace {1}.",
                                                      taskIdString,
                                                      mp.ToString());
                                sw.Reset();
                                tsScope.Connect();
                                sw.Stop();
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                      taskIdString,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString());
                            } catch (Exception ex) {
                                tsScope = null;
                                Lib.LogException(taskIdString,
                                                 sw,
                                                 String.Format("Connect to {0} failed", mp.ToString()),
                                                 ex);
                            }


                            ManagementScope virtualizScope = null;
                            try   {
                                //
                                // Create a scope for the virtualization namespace.
                                mp = new ManagementPath();
                                mp.Server = server;
                                mp.NamespacePath = @"root\virtualization";
                                co.Authentication = AuthenticationLevel.PacketPrivacy;
                                virtualizScope = new ManagementScope(mp, co);
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Attempting connection to namespace {1}.",
                                                      taskIdString,
                                                      mp.ToString());
                                sw.Reset();
                                virtualizScope.Connect();
                                sw.Stop();
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                      taskIdString,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString());
                            }   catch (Exception ex)   {
                                virtualizScope = null;
                                Lib.LogException(taskIdString,
                                                 sw,
                                                 String.Format("Connect to {0} failed", mp.ToString()),
                                                 ex);
                            }

                            ManagementScope virtualizv2Scope = null;
                            try   {
                                //
                                // Create a scope for the virtualization\v2 namespace.
                                mp = new ManagementPath();
                                mp.Server = server;
                                mp.NamespacePath = @"root\virtualization\v2";
                                co.Authentication = AuthenticationLevel.PacketPrivacy;
                                virtualizv2Scope = new ManagementScope(mp, co);
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Attempting connection to namespace {1}.",
                                                      taskIdString,
                                                      mp.ToString());
                                sw.Reset();
                                virtualizv2Scope.Connect();
                                sw.Stop();
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Connect to {1} succeeded. Elapsed time {2}.",
                                                      taskIdString,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString());
                            }   catch (Exception ex)   {
                                virtualizv2Scope = null;
                                Lib.LogException(taskIdString,
                                                 sw,
                                                 String.Format("Connect to {0} failed", mp.ToString()),
                                                 ex);
                            }

                            //
                            // We have everything we need.  Create a dictionary
                            // to return as the "connection" and get out of
                            // loop.
                            Dictionary<string, object> connectionDic = new Dictionary<string, object>();
                            foreach (KeyValuePair<string, string> kvp in connectionParameterSets[i]) {
                                connectionDic.Add(kvp.Key, kvp.Value);
                            }
                            connectionDic[@"cimv2"] = cimvScope;
                            connectionDic[@"default"] = defaultScope;
                            if (tsScope != null) {
                                connectionDic[@"TerminalServices"] = tsScope;
                            }
                            if (virtualizScope != null)   {
                                connectionDic[@"virtualization"] = virtualizScope;
                            }
                            if (virtualizv2Scope != null)   {
                                connectionDic[@"v2"] = virtualizv2Scope;
                            }
                            result = new ConnectionScriptResults(connectionDic,
                                                                 ResultCodes.RC_SUCCESS,
                                                                 0,
                                                                 i);
                            break;
                        } catch (ManagementException me) {
                            if (ManagementStatus.AccessDenied == me.ErrorCode) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nWMI Error Code {3}",
                                                      taskIdString,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString(),
                                                      me.ErrorCode.ToString());
                                resultCode = ResultCodes.RC_LOGIN_FAILED;
                            } else {
                                Lib.LogManagementException(taskIdString,
                                                           sw,
                                                           String.Format("Connect to {0} failed.", mp.ToString()),
                                                           me);
                                //break ;
                            }
                        } catch (UnauthorizedAccessException uae) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nMessage: {3}.",
                                                  taskIdString,
                                                  mp.ToString(),
                                                  sw.Elapsed.ToString(),
                                                  uae.Message);
                            resultCode = ResultCodes.RC_LOGIN_FAILED;
                        } catch (COMException ce) {
                            if (0x800706BA == (UInt32)ce.ErrorCode) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nCOM Exception {3}.\n" +
                                                      "WMI port (135) may be closed or DCOM may not be properly configured",
                                                      taskIdString,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString(),
                                                      ce.Message);
                            } else if (0x80040154 == (UInt32)ce.ErrorCode) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nCOM Exception {3}.\n" +
                                                      "WMI Management Class not registered. WMI provider not found on the remote machine.",
                                                      taskIdString,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString(),
                                                      ce.Message);
                            } else {
                                Lib.LogException(taskIdString,
                                                 sw,
                                                 String.Format("Connect to {0} failed", mp.ToString()),
                                                 ce);
                            }
                        } catch (Exception ex) {
                            Lib.LogException(taskIdString,
                                             sw,
                                             String.Format("Connect to {0} failed", mp.ToString()),
                                             ex);
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
                                          "Task Id {0}: Unhandled exception in ConnWincsScript.\n{1}",
                                          taskIdString,
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
                                  "Task Id {0}: Connection script ConnWincsScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
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
    }
}

