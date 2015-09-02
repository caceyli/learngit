#region Copyright
/******************************************************************
*
*          Module: Windows Oracle Remote Execution Connection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/07/05
*
* Current Status
*       $Revision: 1.20 $
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
    /// Validate credential for Windows Oracle connections to be used by Windows Oracle collection scripts.
    /// Connections to WMI cimv2 and default scope is created.
    /// </summary>
    public class WindowsOracleRemoteExecConnectionScript : IConnectionScriptRuntime {


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
                                  "Task Id {0}: Connection script WindowsOracleRemoteExecConnectionScript.",
                                  m_taskId);

            if (null == connectionParameterSets) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to WindowsOracleRemoteExecConnectionScript.",
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
                                          "Task Id {0}: Executing WindowsOracleRemoteExecConnectionScript with {1} credential sets.",
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
                        //co.Timeout = Lib.WmiConnectTimeout;

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
                            string server = connectionParameterSets[i][@"hostName"];

                            //
                            // Create a scope for the cimv2 namespace.
                            mp = new ManagementPath();
                            mp.Server = server;
                            mp.NamespacePath = ManagementScopeNames.CIMV;

                            ManagementScope cimvScope = new ManagementScope(mp, co);
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Task Id {0}: Attempting connection to namespace {1}.",
                                                  m_taskId,
                                                  mp.ToString());
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

                            string oracleHome = null;
                            string schemaName = null;
                            string schemaPassword = null;
                            string tempDir = null;

                            resultCode = ValidateScriptParameters(connectionDic,
                                                                  ref oracleHome,
                                                                  ref schemaName,
                                                                  ref schemaPassword);

                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                resultCode = ValidateTemporaryDirectory(cimvScope,
                                                                        connectionParameterSets[i],
                                                                        connectionDic,
                                                                        ref tempDir);
                            }

                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                string batchFile = BuildBatchFile(tempDir,
                                                                  oracleHome,
                                                                  schemaName,
                                                                  schemaPassword);
                                resultCode = ExecuteRemoteProcess(cimvScope,
                                                                  batchFile,
                                                                  schemaName,
                                                                  connectionDic,
                                                                  tftpPath,
                                                                  tftpPath_login,
                                                                  tftpPath_password,
                                                                  tftpDispatcher);
                            }

                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                result = new ConnectionScriptResults(connectionDic,
                                                                     ResultCodes.RC_SUCCESS,
                                                                     0,
                                                                     i);
                                break;
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
                                break;
                            }

                        } catch (UnauthorizedAccessException uae) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Connect to {1} failed.  Elapsed time {2}.\nMessage: {3}.",
                                                  m_taskId,
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
                                                      m_taskId,
                                                      mp.ToString(),
                                                      sw.Elapsed.ToString(),
                                                      ce.Message);
                            } else {
                                Lib.LogException(m_taskId,
                                                 sw,
                                                 String.Format("Connect to {0} failed", mp.ToString()),
                                                 ce);
                            }

                            break;
                        } catch (Exception ex) {
                            Lib.LogException(m_taskId,
                                             sw,
                                             String.Format("Connect to {0} failed", mp.ToString()),
                                             ex);
                            break;
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
                                          "Task Id {0}: Unhandled exception in WindowsOracleRemoteExecConnectionScript.\n{1}",
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
                                  "Task Id {0}: Connection script WindowsOracleRemoteExecConnectionScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
                       

        }

        /// <summary>
        /// Verify that required script parameters are present
        /// and normalize the values.
        /// </summary>
        /// 
        /// <param name="scriptParameters">Script parameters.</param>
        /// <param name="oracleHome">Normalized Oracle home parameter.</param>
        /// <param name="schemaName">Normalized schema name.</param>
        /// <param name="schemaPassword">Normalized schema password.</param>
        /// 
        /// <returns>Result code (one of the RC_ constants).</returns>
        private ResultCodes ValidateScriptParameters(
                Dictionary<string, object>      scriptParameters,
                ref string                      oracleHome,
                ref string                      schemaName,
                ref string                      schemaPassword) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            if (!scriptParameters.ContainsKey("OracleHome")) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Missing script parameter \"OracleHome\".",
                                      m_taskId);
                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
            } else {
                oracleHome = scriptParameters["OracleHome"].ToString().Trim();

                if (oracleHome.EndsWith(@"\")) {
                    oracleHome.Remove(oracleHome.Length - 1, 1);
                }

                scriptParameters["OracleHome"] = oracleHome;
            }

            if (!scriptParameters.ContainsKey("schemaName")) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Missing script parameter \"schemaName\".",
                                      m_taskId);
                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
            } else {
                schemaName = scriptParameters["schemaName"].ToString().Trim();
                scriptParameters["schemaName"] = schemaName;
            }

            if (!scriptParameters.ContainsKey("schemaPassword")) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Missing script parameter \"schemaPassword\".",
                                      m_taskId);
                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
            } else {
                schemaPassword = scriptParameters["schemaPassword"].ToString().Trim();
                scriptParameters["schemaPassword"] = schemaPassword;
            }

            return resultCode;
        }

        /// <summary>\
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
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Temporary directory {1} is not valid.",
                                          m_taskId,
                                          tempDir);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Temporary directory {1} has been validated.",
                                          m_taskId,
                                          tempDir);
                }

            }

            return resultCode;
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
                string                          strTempDir,
                string                          strOracleHome,
                string                          strSchemaName,
                string                          strSchemaPassword) {
            string strQuery = "select '<BDNA>' || DUMMY || '</BDNA>' from dual";
            StringBuilder strBatchFile = new StringBuilder();

            if (!String.IsNullOrEmpty(strTempDir)) {

                if (strTempDir.EndsWith(@"\")) {
                    strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
                }

            }

            strBatchFile.AppendLine(@"@ECHO OFF");
            strBatchFile.AppendLine(@"IF (%1) == () GOTO :ERROR_NULL_PARAMETER");
            strBatchFile.Append(@"IF EXIST ").Append(strTempDir)
                        .AppendLine(@"\%1 GOTO :ERROR_CACHE_DIR_EXISTS");
            strBatchFile.AppendLine();
            strBatchFile.Append(@"MKDIR ").Append(strTempDir).AppendLine(@"\%1");
            strBatchFile.Append(@"SET TNS_ADMIN=").Append(strTempDir).AppendLine(@"\%1");

            for (int i = 0; i < 4; i++) {
                strBatchFile.Append(@"ECHO QUIT >> ").Append(strTempDir).Append(@"\%1").AppendLine(@"\CMDLINE.TXT");
            }

            strBatchFile.Append(@"ECHO # EMPTY FILE >> ")
                         .Append(strTempDir).Append(@"\%1").AppendLine(@"\SQLNET.ORA");
            strBatchFile.AppendLine();

            strBatchFile.Append("ECHO ");
            strBatchFile.Append(strQuery.Trim().Replace("<", "^<").Replace(">", "^>").Replace("|", @"^|"));
            strBatchFile.Append(";");
            strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            strBatchFile.Append(@"ECHO QUIT; >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.AppendLine();
            strBatchFile.Append(@"CD ").Append(strTempDir).AppendLine(@"\%1");
            strBatchFile.AppendLine();
            strBatchFile.Append(strOracleHome.Trim()).Append(@"\BIN\SQLPLUS ")
                        .Append(strSchemaName).Append(@"/").Append(strSchemaPassword).Append(@" ")
                        .Append(@"@").Append(@"QUERY.SQL ")
                        .Append(@" 0<").AppendLine(@"CMDLINE.TXT 2>&1");
            strBatchFile.AppendLine();

            strBatchFile.Append(@"CD ").AppendLine(strTempDir);
            strBatchFile.Append(@"DEL ").Append(strTempDir).AppendLine(@"\%1\CMDLINE.TXT");
            strBatchFile.Append(@"DEL ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append(@"DEL ").Append(strTempDir).Append(@"\%1").AppendLine(@"\SQLNET.ORA");
            strBatchFile.Append(@"RMDIR ").Append(strTempDir).AppendLine(@"\%1");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@"GOTO :SUCCESS");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":ERROR_NULL_PARAMETER");
            strBatchFile.AppendLine(@"ECHO ERROR- null batch parameter.");
            strBatchFile.AppendLine(@"GOTO :END");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":ERROR_CACHE_DIR_EXISTS");
            strBatchFile.AppendLine(@"ECHO ERROR- cache directory exists.");
            strBatchFile.AppendLine(@"GOTO :END");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":SUCCESS");
            strBatchFile.AppendLine(@"ECHO Execution completed.");
            strBatchFile.AppendLine();
            strBatchFile.AppendLine(@":END");

            return strBatchFile.ToString();
        }

        /// <summary>
        /// Spawn a remote process to execute the L3 validation
        /// batch file and process the results.
        /// </summary>
        /// 
        /// <param name="scope">WMI connection to target host.</param>
        /// <param name="batchFile">Batch file contents to execute.</param>
        /// <param name="schemaName">Schema name to validate.</param>
        /// <param name="scriptParameters">Script parameters to use (contains temporary directory
        ///     parameter).</param>
        /// <param name="tftpDispatcher">TFTP dispatcher singleton.</param>
        /// 
        /// <returns>Operation result code.</returns>
        private ResultCodes ExecuteRemoteProcess(
                ManagementScope                 scope,
                string                          batchFile,
                string                          schemaName,
                IDictionary<string, object>     scriptParameters,
                string                          tftpPath,
                string                          tftpPath_login,
                string                          tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string stdout = String.Empty;
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Attempt to establish L3 connection using {1}.",
                                  m_taskId,
                                  schemaName);

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

                        if (stdout.Contains("ORA-01017")) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Oracle L3 credential invalid.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                  m_taskId,
                                                  stdout);
                            resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                        } else if (stdout.Contains("ERROR-")) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Batch file execution exception.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                  m_taskId,
                                                  stdout);
                            resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                        } else if (!stdout.Contains(@"BDNA")) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: SQLPLUS exception, no proper data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                  m_taskId,
                                                  stdout);
                            resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                        } else if (!stdout.Contains(@"Execution completed")) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Exception with batch file return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                  m_taskId);
                            resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
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
                if (0 >= stdout.Length || !s_bdnaRegex.IsMatch(stdout)) {
                    resultCode = ResultCodes.RC_LOGIN_FAILED; // @todo Did login really fail?  Perhaps this s/b processing exception
                }

            }

            return resultCode;
        }

        private string m_taskId;

        private static readonly Regex s_bdnaRegex = new Regex("<BDNA>X.*</BDNA>", RegexOptions.Compiled);
    }

}
