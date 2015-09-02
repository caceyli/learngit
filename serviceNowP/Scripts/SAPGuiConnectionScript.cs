#region Copyright
/******************************************************************
*
*          Module: SAP Gui Connection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.8 $
*           $Date: 2008/04/25 12:28:42 $
*         $Author: dchou $
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
using System.Data;
using System.Diagnostics;
using System.Management;
using System.Text;

using bdna.ScriptLib;
using bdna.Shared;
using BDNASAP.SAPGuiScripting;

namespace bdna.Scripts {

    /// <summary>
    /// Connection class to test multiple SAP L3 Credential
    /// </summary>
    public class SAPGuiConnectionScript : IConnectionScriptRuntime {
 
        /// <summary>Connect method invoked by WinCs. </summary>
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="connectionParameterSets">List of credential sets</param>
        /// <param name="tftpDispatcher">Tftp Listener</param>
        /// <returns>Operation results.</returns>
        public ConnectionScriptResults Connect(
                long taskId,
                IDictionary<string, string>[] connectionParameterSets,
                ITftpDispatcher tftpDispatcher) {
            string taskIdString = taskId.ToString();
            ConnectionScriptResults result = null;
            Stopwatch executionTimer = Stopwatch.StartNew();
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Connection script SAPGuiConnectionScript.",
                                  taskIdString);

            if (connectionParameterSets == null) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to SAPGuiConnectionScript.",
                                      taskIdString);
                result = new ConnectionScriptResults(null, ResultCodes.RC_NULL_PARAMETER_SET, 0, -1);
           } else {
                try {
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Executing SAPGuiConnectionScript with {1} credential sets.",
                                          taskIdString,
                                          connectionParameterSets.Length.ToString());

                    //
                    // Loop to process credential sets until a successful
                    // connection is made.
                    bool bConnectSuccess = false;
                    for (int i = 0; i < connectionParameterSets.Length && !bConnectSuccess; i++) {
                        Dictionary<string, object> connectionDic = new Dictionary<string, object>();
                        foreach (KeyValuePair<string, string> kvp in connectionParameterSets[i]) {
                            connectionDic.Add(kvp.Key, kvp.Value);
                        }
                        string strSAPUserName = connectionParameterSets[i][@"sapUserName"] as string;
                        string strSAPUserPassword = connectionParameterSets[i][@"sapUserPassword"] as string;
                        string strApplicationServer = connectionParameterSets[i][@"address"] as string;
                        string strInstanceNumber = connectionParameterSets[i][@"systemNumber"] as string;
                        if (strInstanceNumber.Length != 2) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Invalid System number <{1}>.\nSystem Number should always be two digits even if the number is 00.",
                                                  taskIdString,
                                                  strInstanceNumber);
                            break;
                        }
                        string strConnectionString = @"/H/"+ strApplicationServer.Trim()  + @"/S/32"+strInstanceNumber.Trim();

                        if (!string.IsNullOrEmpty(strApplicationServer) && !string.IsNullOrEmpty(strSAPUserName) && !string.IsNullOrEmpty(strSAPUserPassword)) {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Processing credential set {1}:\nConnecting to remote machine using {2}, SAP user name {3}.",
                                                  taskIdString,
                                                  i.ToString(),
                                                  strConnectionString,
                                                  strSAPUserName);

                            using (SAPGuiConnection oConn = new SAPGuiConnection()) {
                                Stopwatch sw = Stopwatch.StartNew();
                                oConn.Connect(strConnectionString, strSAPUserName.Trim(), strSAPUserPassword.Trim());
                                sw.Stop();
                                bConnectSuccess = oConn.isConnected;
                                if (oConn.isConnected) {
                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Connection to {1} with user name {2} succeeded.  Elapsed time {3}.",
                                                          taskIdString,
                                                          strApplicationServer,
                                                          strSAPUserName,
                                                          sw.Elapsed.ToString());
                                    result = new ConnectionScriptResults(connectionDic, ResultCodes.RC_SUCCESS, 0, i);
                                    break;
                                }
                                else {
                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Connection to {1} failed.  Elapsed time {2}.\n{3}",
                                                          taskIdString,
                                                          strApplicationServer,
                                                          sw.Elapsed.ToString(),
                                                          oConn.logData.ToString());
                                }
                            }
                        } 
                        else {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Skipping credential set {1} which contains null credential information.",
                                                  taskIdString,
                                                  i.ToString());
                        }
                    }
                    //
                    // Connect failed after all credentials attempted.
                    if (null == result) {
                        result = new ConnectionScriptResults(null, ResultCodes.RC_HOST_CONNECT_FAILED,
                                                             0, connectionParameterSets.Length);
                    }

                } catch (Exception e) {
                    Lib.LogException(taskIdString,
                                     executionTimer,
                                     "Unhandled exception in SAPGuiConnectionScript",
                                     e);

                    //
                    // This is really an unanticipated fail safe.  We're
                    // going to report that *no* credentials were tried, which
                    // actually may not be true...
                    result = new ConnectionScriptResults(null, ResultCodes.RC_PROCESSING_EXCEPTION, 0, -1);
                }

            }

            Debug.Assert(null != result);
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Connection script SAPGuiConnectionScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
    }
}
