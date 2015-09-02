#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.36 $
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

    public class WindowsOracleListenerStaticScript : ICollectionScriptRuntime {
        const int REGTIMEOUT = 20000;

        #region ICollectionScriptRuntime Members

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

            Stopwatch executionTimer = Stopwatch.StartNew();
            string taskIdString = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            string strListenerName = null, strOracleHome = null;
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleListenerStaticScript.",
                                  taskIdString);

            try {

                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleListenerStaticScript is null.",
                                          taskIdString);
                } else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          taskIdString);
                } else {
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
                                          "Task Id {0}: Missing OracleHome script parameter.",
                                          taskIdString);
                } else {
                    strOracleHome = scriptParameters["OracleHome"].Trim();
                    if (strOracleHome.EndsWith(@"\")) {
                        strOracleHome = strOracleHome.Substring(0, strOracleHome.Length - 1);
                    }
                }

                //Check Listener Name attribute
                if (!scriptParameters.ContainsKey("name")) {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing Listener Name script parameter.",
                                          taskIdString);
                } else {
                    strListenerName = scriptParameters["name"];
                }

                // Check Remote Process Temp Directory
                if (!connection.ContainsKey(@"TemporaryDirectory")) {
                    connection[@"TemporaryDirectory"] = @"%TMP%";
                } else {
                    if (!connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                        if (!Lib.ValidateDirectory(taskIdString, connection[@"TemporaryDirectory"].ToString(),cimvScope)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} is not valid.",
                                                  taskIdString,
                                                  connection[@"TemporaryDirectory"].ToString());
                            resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} has been validated.",
                                                  taskIdString,
                                                  connection[@"TemporaryDirectory"].ToString());
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string commandLine = @"cmd /q /e:off /C " + strOracleHome.Trim() + @"\BIN\LSNRCTL.EXE status " + strListenerName;
                    StringBuilder stdoutData = new StringBuilder();
                    using (IRemoteProcess rp =
                        RemoteProcess.NewRemoteProcess(taskIdString, cimvScope, commandLine,
                             null, StdioRedirection.STDOUT, null, connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                        //This method will block until the entire remote process operation completes.
                        resultCode = rp.Launch();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote process operation completed with result code {1}.",
                                              taskIdString,
                                              resultCode.ToString());

                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            stdoutData.Append(rp.Stdout);
                            if (rp.Stdout != null && rp.Stdout.Length > 0) {
                                if (!rp.Stdout.ToString().ToUpper().Contains(@"PARAMETER FILE")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: SQLPLUS exception, no proper data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          taskIdString,
                                                          rp.Stdout.ToString());
                                    //resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                                }
                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                      taskIdString);
                                resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                            }
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Remote execution error.\n{1}",
                                                  taskIdString,
                                                  rp.Stdout.ToString());
                        }
                    }
                     
                    if (resultCode == ResultCodes.RC_SUCCESS && stdoutData.Length > 0) {
                        string[] arrOutputLine = stdoutData.ToString().Split("\r\n".ToCharArray());
                        string strListenerAddress = "", strParameterFile = "", strInstances = "", strPort = "";

                        for (int i = 0; i < arrOutputLine.Length; i++) {
                            string output = arrOutputLine[i];

                            if (s_connectingRegex_en.IsMatch(output)) {
                                MatchCollection mtc = s_connectingRegex_en.Matches(output);
                                foreach (Match m in mtc) {
                                    strListenerAddress = "" + m.Groups["addr"].Value;
                                }
                            } else if (s_connectingRegex_fr.IsMatch(output)) {
                                MatchCollection mtc = s_connectingRegex_fr.Matches(output);
                                foreach (Match m in mtc) {
                                    strListenerAddress = "" + m.Groups["addr"].Value;
                                }
                            } else if (s_connectingRegex_de.IsMatch(output)) {
                                MatchCollection mtc = s_connectingRegex_de.Matches(output);
                                foreach (Match m in mtc) {
                                    strListenerAddress = "" + m.Groups["addr"].Value;
                                }
                            } else if (s_connectingRegex_it.IsMatch(output)) {
                                MatchCollection mtc = s_connectingRegex_it.Matches(output);
                                foreach (Match m in mtc) {
                                    strListenerAddress = "" + m.Groups["addr"].Value;
                                }
                            }

                            if (s_listenerRegex_en.IsMatch(output)) {
                                MatchCollection mtc = s_listenerRegex_en.Matches(output);
                                foreach (Match m in mtc) {
                                    strParameterFile += "" + m.Groups["parameterFile"].Value;
                                }
                            } else if (s_listenerRegex_fr.IsMatch(output)) {
                                MatchCollection mtc = s_listenerRegex_fr.Matches(output);
                                foreach (Match m in mtc) {
                                    strParameterFile += "" + m.Groups["parameterFile"].Value;
                                }
                            } else if (s_listenerRegex_de.IsMatch(output)) {
                                MatchCollection mtc = s_listenerRegex_de.Matches(output);
                                foreach (Match m in mtc) {
                                    strParameterFile += "" + m.Groups["parameterFile"].Value;
                                }
                            } else if (s_listenerRegex_it.IsMatch(output)) {
                                MatchCollection mtc = s_listenerRegex_it.Matches(output);
                                foreach (Match m in mtc) {
                                    strParameterFile += "" + m.Groups["parameterFile"].Value;
                                }
                            }

                            if (!String.IsNullOrEmpty(strListenerAddress) && s_listeningRegex_en.IsMatch(output)) {
                                int j = 0;
                                for (j = i; j < arrOutputLine.Length; j++) {
                                    string ucaseOutput = arrOutputLine[j].ToUpper();

                                    if (!s_keyExtProcRegex_en.IsMatch(ucaseOutput)) {
                                        if (s_descriptionRegex_en.IsMatch(ucaseOutput)) {
                                            MatchCollection mtc = s_descriptionRegex_en.Matches(ucaseOutput);
                                            foreach (Match m in mtc) {
                                                strListenerAddress = m.Groups["addr"].Value;
                                            }
                                            break;
                                        }
                                    }
                                    i = j;
                                }
                            } else if (!String.IsNullOrEmpty(strListenerAddress) && s_listeningRegex_fr.IsMatch(output)) {
                                int j = 0;
                                for (j = i; j < arrOutputLine.Length; j++) {
                                    string ucaseOutput = arrOutputLine[j].ToUpper();

                                    if (!s_keyExtProcRegex_fr.IsMatch(ucaseOutput)) {
                                        if (s_descriptionRegex_fr.IsMatch(ucaseOutput)) {
                                            MatchCollection mtc = s_descriptionRegex_fr.Matches(ucaseOutput);
                                            foreach (Match m in mtc) {
                                                strListenerAddress = m.Groups["addr"].Value;
                                            }
                                            break;
                                        }
                                    }
                                    i = j;
                                }
                            } else if (!String.IsNullOrEmpty(strListenerAddress) && s_listeningRegex_de.IsMatch(output)) {
                                int j = 0;
                                for (j = i; j < arrOutputLine.Length; j++) {
                                    string ucaseOutput = arrOutputLine[j].ToUpper();

                                    if (!s_keyExtProcRegex_de.IsMatch(ucaseOutput)) {
                                        if (s_descriptionRegex_de.IsMatch(ucaseOutput)) {
                                            MatchCollection mtc = s_descriptionRegex_de.Matches(ucaseOutput);
                                            foreach (Match m in mtc) {
                                                strListenerAddress = m.Groups["addr"].Value;
                                           }
                                            break;
                                        }
                                    }
                                    i = j;
                                }
                            } else if (!String.IsNullOrEmpty(strListenerAddress) && s_listeningRegex_it.IsMatch(output)) {
                                int j = 0;
                                for (j = i; j < arrOutputLine.Length; j++) {
                                    string ucaseOutput = arrOutputLine[j].ToUpper();

                                    if (!s_keyExtProcRegex_it.IsMatch(ucaseOutput)) {
                                        if (s_descriptionRegex_it.IsMatch(ucaseOutput)) {
                                            MatchCollection mtc = s_descriptionRegex_it.Matches(ucaseOutput);
                                            foreach (Match m in mtc) {
                                                strListenerAddress = m.Groups["addr"].Value;
                                           }
                                            break;
                                        }
                                    }
                                    i = j;
                                }
                            }

                            if (s_portRegex_en.IsMatch(strListenerAddress))
                            {
                                Match m0 = s_portRegex_en.Match(strListenerAddress);
                                strPort = m0.Groups[1].Value;
                            }
                            else if (s_portRegex_fr.IsMatch(strListenerAddress))
                            {
                                Match m0 = s_portRegex_fr.Match(strListenerAddress);
                                strPort = m0.Groups[1].Value;
                            }
                            else if (s_portRegex_de.IsMatch(strListenerAddress))
                            {
                                Match m0 = s_portRegex_de.Match(strListenerAddress);
                                strPort = m0.Groups[1].Value;
                            }
                            else if (s_portRegex_it.IsMatch(strListenerAddress))
                            {
                                Match m0 = s_portRegex_it.Match(strListenerAddress);
                                strPort = m0.Groups[1].Value;
                            }

                            if (s_instancesRegex_en.IsMatch(output))
                            {
                                Match m0 = s_instancesRegex_en.Match(output);
                                string strinstance = m0.Groups[1].Value;
                                if (!String.IsNullOrEmpty(strInstances))
                                {
                                    if (!strInstances.Contains(strinstance))
                                    {
                                        strInstances += " " + strinstance;
                                    }
                                }
                                else
                                {
                                    strInstances = strinstance;
                                }
                            }
                        }

                        dataRow.Append(elementId).Append(',')
                            .Append(attributes["listenerAddress"]).Append(',')
                            .Append(scriptParameters["CollectorId"]).Append(',')
                            .Append(taskId).Append(',')
                            .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                            .Append("listenerAddress").Append(',')
                            .Append(BdnaDelimiters.BEGIN_TAG).Append(strListenerAddress).Append(BdnaDelimiters.END_TAG);

                        dataRow.Append(elementId).Append(',')
                            .Append(attributes["parameterFilePath"]).Append(',')
                            .Append(scriptParameters["CollectorId"]).Append(',')
                            .Append(taskId).Append(',')
                            .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                            .Append("parameterFilePath").Append(',')
                            .Append(BdnaDelimiters.BEGIN_TAG).Append(strParameterFile).Append(BdnaDelimiters.END_TAG);

                        dataRow.Append(elementId).Append(',')
                            .Append(attributes["port"]).Append(',')
                            .Append(scriptParameters["CollectorId"]).Append(',')
                            .Append(taskId).Append(',')
                            .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                            .Append("port").Append(',')
                            .Append(BdnaDelimiters.BEGIN_TAG).Append(strPort).Append(BdnaDelimiters.END_TAG);

                        dataRow.Append(elementId).Append(',')
                            .Append(attributes["associateInstances"]).Append(',')
                            .Append(scriptParameters["CollectorId"]).Append(',')
                            .Append(taskId).Append(',')
                            .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                            .Append("associateInstances").Append(',')
                            .Append(BdnaDelimiters.BEGIN_TAG).Append(strInstances).Append(BdnaDelimiters.END_TAG);
                    }
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleListenerStaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          taskIdString,
                                          executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleListenerStaticScript.  Elapsed time {1}.\n{2}",
                                          taskIdString,
                                          executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleListenerStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion

        private static readonly Regex s_connectingRegex_en = new Regex("Connecting to(\\s*)(?<addr>.+)$", RegexOptions.Compiled);
        private static readonly Regex s_connectingRegex_fr = new Regex("Connexion \\S(\\s*)(?<addr>.+)$", RegexOptions.Compiled);
        private static readonly Regex s_connectingRegex_de = new Regex("Anmeldung bei(\\s*)(?<addr>.+)$", RegexOptions.Compiled);
        private static readonly Regex s_connectingRegex_it = new Regex("Connessione a(\\s*)(?<addr>.+)$", RegexOptions.Compiled);

        private static readonly Regex s_listenerRegex_en = new Regex(@"Listener Parameter File(\s*)(?<parameterFile>.+)$", RegexOptions.Compiled);
        private static readonly Regex s_listenerRegex_fr = new Regex(@"Fichier de param\Stres du processus d'\Scoute(\s*)(?<parameterFile>.+)$", RegexOptions.Compiled);
        private static readonly Regex s_listenerRegex_de = new Regex(@"Parameterdatei des Listener(\s*)(?<parameterFile>.+)$", RegexOptions.Compiled);
        private static readonly Regex s_listenerRegex_it = new Regex(@"File di parametri listener(\s*)(?<parameterFile>.+)$", RegexOptions.Compiled);

        private static readonly Regex s_instancesRegex_en = new Regex("Instance \"(.*)\", status .*, has .* handler", RegexOptions.Compiled);

        private static readonly Regex s_listeningRegex_en = new Regex("Listening Endpoints Summary", RegexOptions.Compiled);
        private static readonly Regex s_listeningRegex_fr = new Regex("R capitulatif services", RegexOptions.Compiled);
        private static readonly Regex s_listeningRegex_de = new Regex("Services  bersicht", RegexOptions.Compiled);
        private static readonly Regex s_listeningRegex_it = new Regex("Summary table degli endpoint di ascolto", RegexOptions.Compiled);

        private static readonly Regex s_keyExtProcRegex_en = new Regex("PROTOCOL=IPC", RegexOptions.Compiled);
        private static readonly Regex s_keyExtProcRegex_fr = new Regex("PROTOCOL=IPC", RegexOptions.Compiled);
        private static readonly Regex s_keyExtProcRegex_de = new Regex("PROTOCOL=IPC", RegexOptions.Compiled);
        private static readonly Regex s_keyExtProcRegex_it = new Regex("PROTOCOL=IPC", RegexOptions.Compiled);

        private static readonly Regex s_portRegex_en = new Regex("PORT=(\\d+)", RegexOptions.Compiled);
        private static readonly Regex s_portRegex_fr = new Regex("PORT=(\\d+)", RegexOptions.Compiled);
        private static readonly Regex s_portRegex_de = new Regex("PORT=(\\d+)", RegexOptions.Compiled);
        private static readonly Regex s_portRegex_it = new Regex("PORT=(\\d+)", RegexOptions.Compiled);

        private static readonly Regex s_descriptionRegex_en = new Regex(@"(?<addr>\(DESCRIPTION=\(ADDRESS=.*)$", RegexOptions.Compiled);
        private static readonly Regex s_descriptionRegex_fr = new Regex(@"(?<addr>\(DESCRIPTION=\(ADDRESS=.*)$", RegexOptions.Compiled);
        private static readonly Regex s_descriptionRegex_de = new Regex(@"(?<addr>\(DESCRIPTION=\(ADDRESS=.*)$", RegexOptions.Compiled);
        private static readonly Regex s_descriptionRegex_it = new Regex(@"(?<addr>\(DESCRIPTION=\(ADDRESS=.*)$", RegexOptions.Compiled);
    }
}

