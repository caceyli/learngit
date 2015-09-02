#region Copyright
/******************************************************************
*
*          Module: ESX Web Services Connection Script
* Original Author: Rekha Rani
*   Creation Date: 2008/06/24
*
* Current Status
*       $Revision: 1.9 $
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
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

using System.Web.Services.Protocols;
using VimApi; 

/// VimApi for the VirtualInfrastructureManagement Service Reference

namespace bdna.Scripts {

    /// <summary>
    /// Connection class to test multiple ESX Credential
    /// </summary>
    public class ESXWebServicesConnectionScript : IConnectionScriptRuntime {

        // VIM variablres
        protected VimService vim_svc = null;
        protected ServiceContent vim_svc_content = null;
        protected ManagedObjectReference vim_svc_ref = null;

        /// <summary>
        /// Connect method to connect to ESX Web Services.
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
                long taskId,
                IDictionary<string, string>[] connectionParameterSets,
                string tftpPath,
                string tftpPath_login, 
                string tftpPath_password,
                ITftpDispatcher tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            string taskIdString = taskId.ToString();
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Connection script ESXWebServicesConnectionScript.",
                                  taskIdString);
            ConnectionScriptResults result = null;

            if (null == connectionParameterSets) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to ESXWebServicesConnectionScript",
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
                                          "Task Id {0}: Executing ESXWebServicesConnectionScript with {1} credential sets.",
                                          taskIdString,
                                          connectionParameterSets.Length.ToString());

                    //
                    // Loop to process credential sets until a successful
                    // connection is made.
                    for (int i = 0;
                         connectionParameterSets.Length > i;
                         ++i) {
                        Dictionary<string, object> connectionDic = new Dictionary<string, object>();
                        foreach (KeyValuePair<string, string> kvp in connectionParameterSets[i]) {
                            connectionDic.Add(kvp.Key, kvp.Value);
                        }

                        string userName = connectionParameterSets[i][@"userName"];
                        string password = connectionParameterSets[i][@"password"];

                        String protocol = null;
                        String port = null;
                        String hostIP = null;
                        String url = null;

                        protocol = connectionParameterSets[i][@"protocol"];
                        port = connectionParameterSets[i][@"port"];
                        hostIP = connectionParameterSets[i][@"address"] as string;

                        if (hostIP != null) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: hostIP is \"{1}\".",
                                                  taskIdString,
                                                  hostIP);
                        } else {
                            resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Missing parameter - server address/name.",
                                                  taskIdString);
                        }

                        url = protocol + @"://" + hostIP + @":" + port + @"/sdk";

                        Lib.Logger.TraceEvent(TraceEventType.Information,
                                              0,
                                              "Task Id {0}: Processing credential set {1}: user=\"{2}\" for esx web service url={3}.",
                                              taskIdString,
                                              i.ToString(),
                                              userName,
                                              url);

                        Stopwatch sw = new Stopwatch();

                        try {

                            // Manage certificates:
                            System.Net.ServicePointManager.CertificatePolicy = new CertPolicy();

                            vim_svc_ref = new ManagedObjectReference();
                            vim_svc_ref.type = "ServiceInstance";

                            // could be ServiceInstance for "HostAgent" and "VPX" for VPXd
                            vim_svc_ref.Value = "ServiceInstance";

                            // connect to esx web service:
                            // if vim_svc not null then disconnect
                            if (vim_svc != null) {
                                Disconnect();
                            }


                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Instantiate VimService object.  Elapsed time {1} ms.",
                                                  taskIdString,
                                                  executionTimer.ElapsedMilliseconds);

                            vim_svc = new VimService();

                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Instantiated VimService object.  Elapsed time {1} ms.",
                                                  taskIdString,
                                                  executionTimer.ElapsedMilliseconds);

                            vim_svc.Url = url;
                            vim_svc.CookieContainer = new System.Net.CookieContainer();

                            vim_svc_content = vim_svc.RetrieveServiceContent(vim_svc_ref);

                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: RetrievedServiceContent.  Elapsed time {1} ms.",
                                                  taskIdString,
                                                  executionTimer.ElapsedMilliseconds);

                            UserSession userSessionObj = null;
                            if (vim_svc_content.sessionManager != null) {
                                sw.Start();
                                userSessionObj = vim_svc.Login(vim_svc_content.sessionManager, userName, password, null);
                                sw.Stop();

                                if (userSessionObj != null) {
                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Connection to {1} with user name {2} succeeded.  Elapsed time {3}.",
                                                          taskIdString,
                                                          url,
                                                          userName,
                                                          sw.Elapsed.ToString());

                                    connectionDic.Add("VimServiceObj", vim_svc);
                                    connectionDic.Add("VimServiceContentObj", vim_svc_content);
                                    connectionDic.Add("userSessionObj", userSessionObj);
                                    connectionDic.Add("WebServiceUrl", url);

                                    result = new ConnectionScriptResults(connectionDic, ResultCodes.RC_SUCCESS, 0, i);

                                    break;
                                } else {
                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Connection to {1} failed with user name {2}.  Elapsed time {3}.\n",
                                                          taskIdString,
                                                          url,
                                                          userName,
                                                          sw.Elapsed.ToString()
                                                         );
                                }
                            }
                        } catch (SoapException se) {
                            ///Console.WriteLine("DEBUG Caught SoapException - " + se.ToString() + "\n Msg = " + se.Message.ToString() + "Code = " + se.Code.ToString());

                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                       0,
                                                       "Task Id {0}: Caught SoapException in ESXWebServicesConnectionScript.  Elapsed time {1}.\n<Exception detail:  [se: {2}]\n[Message: {3}]\n[Code: {4}]\n[Detail XML(OuterXml): {5}] Exception detail end>",
                                                       taskIdString,
                                                       executionTimer,
                                                       se.ToString(),
                                                       se.Message.ToString(),
                                                       se.Code.ToString(),
                                                       se.Detail.OuterXml.ToString());
                           
                            if(se.Message.Contains("Login failed") || se.Message.Contains("incorrect user name or password")) {
                                resultCode = ResultCodes.RC_LOGIN_FAILED;
                            } else if(se.Message.Contains("Unsupported namespace")) {
                                resultCode = ResultCodes.RC_UNSUPPORTED_MESSAGE_TYPE;
                            } else if(se.Message.Contains("Permission to perform this operation was denied")) {
                                resultCode = ResultCodes.RC_LOGIN_FAILED;
                            } else if (se.Code.ToString().Contains("ServerFaultCode")) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: SoapException (ServerFaultCode) in ESXWebServicesConnectionScript.  Elapsed time {1}.\n <More detail:  [InnerText: {2}]\n[InnerXml: {3}]  end>",
                                                          taskIdString,
                                                          executionTimer,
                                                          se.Detail.InnerText,
                                                          se.Detail.InnerXml);

                                resultCode = ResultCodes.RC_ESX_WEB_SERVICE_QUERY_FAILURE;
                            } else {
                                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                            }

                        } catch (Exception ex) {
                            Lib.LogException(taskIdString,
                                             sw,
                                             String.Format("Connect to {0} failed", url),
                                             ex);
                            //break;
                        } finally {
                            if (vim_svc != null) {
                               vim_svc.Dispose();
                               vim_svc = null;
                               vim_svc_content = null;
                            }
                       }
                    }  // end of for loop

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
                                          "Task Id {0}: Unhandled exception in ESXWebServicesConnectionScript.  Elapsed time {1}.\n Result code changed to RC_PROCESSING_EXECEPTION. <EXP  {2} EXP>",
                                          taskIdString,
                                          executionTimer,
                                          e.ToString());

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
                                  "Task Id {0}: Connection script ESXWebServicesConnectionScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;

        }

        private void Disconnect() {
            // Disconnect from the service:
            if (vim_svc != null) {
                vim_svc.Logout(vim_svc_content.sessionManager);
                vim_svc.Dispose();
                vim_svc = null;
                vim_svc_content = null;
            }
        }
    }
}
