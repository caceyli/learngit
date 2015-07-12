#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
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
    public class WindowsOracleInstallationDynamicScript : ICollectionScriptRuntime {
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
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_tftpPath = tftpPath;
            m_tftpPath_login = tftpPath_login;
            m_tftpPath_password = tftpPath_password;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            string strOracleHome = null, validatedInstances = null;
            string[] strRunningInstances = null;

            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstallationDynamicScript.",
                                  m_taskId);
            try {
                // Check ManagementScope CIMV
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstallationDynamicScript is null.",
                                          m_taskId);
                } 
                else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else {
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }

                //Check Oracle Home attribute
                if (!scriptParameters.ContainsKey("OracleHome")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"OracleHome\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strOracleHome = scriptParameters["OracleHome"];
                }

                if (!scriptParameters.ContainsKey("runningInstances")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"runningInstances\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else if (!String.IsNullOrEmpty(scriptParameters["runningInstances"])) {
                    strRunningInstances = scriptParameters["runningInstances"]
                        .Split(new String[] { BdnaDelimiters.DELIMITER_TAG }, StringSplitOptions.RemoveEmptyEntries);
                }

                // Check Remote Process Temp Directory
                if (!m_connection.ContainsKey("TemporaryDirectory")) {
                    m_connection["TemporaryDirectory"] = @"%TMP%";
                }
                else {
                    if (!m_connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                        if (!Lib.ValidateDirectory(m_taskId, m_connection[@"TemporaryDirectory"].ToString(), m_cimvScope)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} is not valid.",
                                                  m_taskId,
                                                  m_connection[@"TemporaryDirectory"].ToString());
                            resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                        }
                        else {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} has been validated.",
                                                  m_taskId,
                                                  m_connection[@"TemporaryDirectory"].ToString());
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    // find all instance from give Oracle installation.
                    string strAllInstance = null;
                    resultCode = validateOracleRunningInstance(strOracleHome, out strAllInstance);
                    if (!String.IsNullOrEmpty(strAllInstance)) {
                        validatedInstances = strAllInstance.ToUpper();
                    }

                    //validate given instance with verified instance list.
                    foreach (string strName in strRunningInstances) {
                        string strInstance = BdnaDelimiters.DELIMITER_TAG + strName + BdnaDelimiters.DELIMITER_TAG;
                        if (validatedInstances.Contains(strInstance.ToUpper())) {
                            if (m_outputData.Length > 1) {
                                m_outputData.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            m_outputData.Append(strName);
                        }
                    }
                    if (m_outputData.Length > 0) {
                        BuildDataRow("validatedInstances", m_outputData.ToString());
                    }
                }
            }
            catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstallationDynamicScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstallationDynamicScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstallationDynamicScript.  Elapsed time {1}. Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());

            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            } else if (string.IsNullOrEmpty(collectedData)) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            } else {
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary>
        /// Validate running instances
        /// </summary>
        /// <param name="strOracleHomeDir">directory path</param>
        /// <returns>Instance that is running on that installation.</returns>
        private ResultCodes validateOracleRunningInstance(string strOracleHomeDir, out string strInstances) {
            string commandLine = @"cmd /q /e:off /C " + strOracleHomeDir.Trim() + @"\BIN\LSNRCTL.EXE status";
            strInstances = null;
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            try {
                StringBuilder stdoutData = new StringBuilder();
                using (IRemoteProcess rp = RemoteProcess.NewRemoteProcess(
                                    m_taskId,                 // Task Id to log against.
                                    m_cimvScope,              // assuming Remote process uses cimv2 management scope
                                    commandLine,              // script supplied command line.
                                    null,                     // Optional working directory
                                    StdioRedirection.STDOUT,  // Flags for what stdio files you want
                                    m_connection,             // connection dictionary.
                                    m_tftpPath,
                                    m_tftpPath_login,
                                    m_tftpPath_password,
                                    m_tftpDispatcher)) {
                    //This method will block until the entire remote process operation completes.
                    resultCode = rp.Launch();
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Remote process operation completed with result code {1}.",
                                          m_taskId,
                                          resultCode.ToString());

                    if (rp != null && rp.Stdout != null && resultCode == ResultCodes.RC_SUCCESS) {
                        strInstances = parseRunningInstances(rp.Stdout.ToString());
                    }
                    else {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Invalid Oracle home: {1}",
                                              m_taskId,
                                              strOracleHomeDir);
                    }
                }
            }
            catch (Exception ex) {
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstallationDynamicScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstallationDynamicScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Parse query result for current running instances.
        /// </summary>
        /// <param name="queryOutput">Output</param>
        /// <returns>Running Instances</returns>
        private string parseRunningInstances(string queryOutput) {
            string pattern = "Instance \"(?<InstanceName>.+?)\", status READY";
            string pattern8x = @"\s*(?<InstanceName8x>.+?)\s+has (\d+) service handler\(s\)";
            string pattern10gExpress = @"Default Service\s+(?<InstanceName10gExpress>.+)";
            StringCollection strInstanceNames = new StringCollection();
            StringBuilder result = new StringBuilder(BdnaDelimiters.DELIMITER_TAG);
            if (!String.IsNullOrEmpty(queryOutput)) {
                foreach (String line in queryOutput.Split('\n', '\r')) {
                    if (s_instanceNoCaseRegex_en.IsMatch(line)) {
                        Match match = s_instanceNoCaseRegex_en.Match(line);
                        string name = match.Groups["InstanceName"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instanceNoCaseRegex_fr.IsMatch(line)) {
                        Match match = s_instanceNoCaseRegex_fr.Match(line);
                        string name = match.Groups["InstanceName"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instanceNoCaseRegex_de.IsMatch(line)) {
                        Match match = s_instanceNoCaseRegex_de.Match(line);
                        string name = match.Groups["InstanceName"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instanceNoCaseRegex_it.IsMatch(line)) {
                        Match match = s_instanceNoCaseRegex_it.Match(line);
                        string name = match.Groups["InstanceName"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instance8xNoCaseRegex_en.IsMatch(line)) {
                        Match match = s_instance8xNoCaseRegex_en.Match(line);
                        string name = match.Groups["InstanceName8x"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instance8xNoCaseRegex_fr.IsMatch(line)) {
                        Match match = s_instance8xNoCaseRegex_fr.Match(line);
                        string name = match.Groups["InstanceName8x"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instance8xNoCaseRegex_de.IsMatch(line)) {
                        Match match = s_instance8xNoCaseRegex_de.Match(line);
                        string name = match.Groups["InstanceName8x"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instance8xNoCaseRegex_it.IsMatch(line)) {
                        Match match = s_instance8xNoCaseRegex_it.Match(line);
                        string name = match.Groups["InstanceName8x"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    } else if (s_instance10gExpressNoCaseRegex.IsMatch(line)) {
                        Match match = s_instance10gExpressNoCaseRegex.Match(line);
                        string name = match.Groups["InstanceName10gExpress"].ToString();
                        if (!strInstanceNames.Contains(name)) {
                            strInstanceNames.Add(name);
                        }
                    }
                }

                foreach (string name in strInstanceNames) {
                    if (!string.IsNullOrEmpty(name)) {
                        result.Append(name).Append(BdnaDelimiters.DELIMITER_TAG);
                    }
                }
            }
            return result.ToString();
        }
        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        /// <summary>CLE element id.</summary>
        private long m_cleId;

        /// <summary>Id of element being collected.</summary>
        private long m_elementId;

        /// <summary>Database relative task dispatch timestamp.</summary>
        private long m_databaseTimestamp;

        /// <summary>CLE local dispatch timestamp.</summary>
        private long m_localTimestamp;

        /// <summary>Map of attribute names to attribute element ids.</summary>
        private IDictionary<string, string> m_attributes;

        /// <summary>Map of collection script specific parameters.</summary>
        private IDictionary<string, string> m_scriptParameters;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>output data buffer.</summary>
        private StringBuilder m_outputData = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Management Scope </summary>
        private ManagementScope m_cimvScope = null;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;

        private string m_tftpPath = string.Empty;
        private string m_tftpPath_login = string.Empty;
        private string m_tftpPath_password = string.Empty;

        private static readonly Regex s_instanceNoCaseRegex_en = new Regex("Instance \"(?<InstanceName>.+?)\", status READY", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_instanceNoCaseRegex_fr = new Regex("L.instance \"(?<InstanceName>.+?)\", statut READY", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_instanceNoCaseRegex_de = new Regex("Instan..? \"(?<InstanceName>.+?)\", Status READY", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_instanceNoCaseRegex_it = new Regex("L.istanza \"(?<InstanceName>.+?)\", stato READY", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex s_instance8xNoCaseRegex_en = new Regex(@"\s*(?<InstanceName8x>.+?)\s+has (\d+) service handler\(s\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_instance8xNoCaseRegex_fr = new Regex(@"\s*(?<InstanceName8x>.+?)\s+\\\s+(\d+) gestionnaires de services", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_instance8xNoCaseRegex_de = new Regex(@"\s*(?<InstanceName8x>.+?)\s+has (\d+)-Dienstroutine(n)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_instance8xNoCaseRegex_it = new Regex(@"\s*(?<InstanceName8x>.+?)\s+ha handler di servizio (\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex s_instance10gExpressNoCaseRegex = new Regex(@"Default Service\s+(?<InstanceName10gExpress>.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
