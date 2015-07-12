#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.16 $
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
    public class WindowsOracleFootprintDynamicScript : ICollectionScriptRuntime {
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
            m_tftpPath = tftpPath;
            m_tftpPath_login = tftpPath_login;
            m_tftpPath_password = tftpPath_password;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_scriptParameters = scriptParameters;
            string[] strPotentialOracleHomes = null;

            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleFootprintDynamicScript.",
                                  m_taskId);
            try {
                // Check ManagementScope CIMV
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleFootprintDynamicScript is null.",
                                          m_taskId);
                } 
                else if (!m_connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else {
                    m_cimvScope = m_connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              m_taskId);
                    }
                }

                //Check Potential Oracle Homes attribute
                if (scriptParameters.ContainsKey("PotentialOracleHomes")) {
                    strPotentialOracleHomes = scriptParameters["PotentialOracleHomes"].Split(new String[] { BdnaDelimiters.DELIMITER_TAG }, StringSplitOptions.RemoveEmptyEntries);
                }
                else {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Script parameter PotentialOracleHomes is not present in the parameter hash.",
                                          m_taskId);
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
                                                  "Task Id {0}: User specified temp directory has been validated.",
                                                  m_taskId);
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    foreach (string strOracleHome in strPotentialOracleHomes) {
                        string strInstallDir = strOracleHome.Trim();
                        if (strInstallDir.EndsWith(@"\")) {
                            strInstallDir = strInstallDir.Substring(0, strOracleHome.Length - 1);
                        }

                        string strHomeVer = null;
                        resultCode = validateOracleInstallation(strInstallDir, out strHomeVer);

                        if (!String.IsNullOrEmpty(strHomeVer)) {
                            resultCode = ResultCodes.RC_SUCCESS;
                            if (m_outputData.Length > 0) {
                                m_outputData.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            m_outputData.Append(strHomeVer).Append(BdnaDelimiters.DELIMITER_TAG).Append(strInstallDir);
                        }
                    }

                    if (m_outputData.Length > 0) {
                        BuildDataRow("OracleHomes", m_outputData.ToString());
                    }
                }
            }
            catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleFootprintDynamicScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleFootprintDynamicScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleFootprintDynamicScript.  Elapsed time {1}.  Result code {2}.",
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
        /// Validate Oracle installation
        /// </summary>
        /// <param name="strOracleHomeDir">directory path</param>
        /// <returns>version of validated oracle installation, null if error.</returns>
        private ResultCodes validateOracleInstallation(string strOracleHomeDir, out string strVersion) {
            strVersion = null;
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string commandLine = @"cmd /q /e:off /C " + strOracleHomeDir + @"\BIN\SQLPLUS -V";
            try {

                using (IRemoteProcess rp = RemoteProcess.NewRemoteProcess(
                                m_taskId,                      // Task Id to log against.
                                m_cimvScope,                   // assuming Remote process uses cimv2 management scope
                                commandLine,                   // script supplied command line.
                                null,                          // Optional working directory
                                StdioRedirection.STDOUT,       // Flags for what stdio files you want
                                m_connection,                  // Data to pass for stdin.
                                m_tftpPath,
                                m_tftpPath_login,
                                m_tftpPath_password,
                                m_tftpDispatcher)) {
                    resultCode = rp.Launch();
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Remote process operation completed with result code {1}.",
                                          m_taskId,
                                          resultCode.ToString());

                    if (rp != null && rp.Stdout.Length > 0 && resultCode == ResultCodes.RC_SUCCESS) {
                        strVersion = parseOracleHomeVersion(rp.Stdout.ToString());
                    }
                    else {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Execution error with SQLPLUS -V\n{1}",
                                              m_taskId,
                                              rp.Stdout.ToString());
                    }
                }
            }
            catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Executing command <{1}> resulted in an exception.\n{2}",
                                      m_taskId,
                                      commandLine,
                                      ex.ToString());
            }


            // Try svrmgrl for earlier release of Oracle.
            try {
                if (String.IsNullOrEmpty(strVersion)) {
                    commandLine = @"cmd /q /e:off /C " + strOracleHomeDir + @"\BIN\SVRMGRL -?";
                    using (IRemoteProcess rp = RemoteProcess.NewRemoteProcess(
                                    m_taskId,                   // Task Id to log against.
                                    m_cimvScope,                // assuming Remote process uses cimv2 management scope
                                    commandLine,                // script supplied command line.
                                    null,                       // Optional working directory
                                    StdioRedirection.STDOUT,    // Flags for what stdio files you want
                                    m_connection,               // Data to pass for stdin.
                                    m_tftpPath,
                                    m_tftpPath_login,
                                    m_tftpPath_password,
                                    m_tftpDispatcher)) {
                        resultCode = rp.Launch();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote process operation completed with result code {1}.",
                                              m_taskId,
                                              resultCode.ToString());

                        if (rp != null && rp.Stdout.Length > 0 && resultCode == ResultCodes.RC_SUCCESS) {
                            strVersion = parseOracleHomeVersionEarlierRelease(rp.Stdout.ToString());
                        }
                        else {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Execution error with SVRMGRL -?\n{1}",
                                                  m_taskId,
                                                  rp.Stdout.ToString());
                        }
                    }
                }
            }
            catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Executing command <{1}> resulted in an exception.\n{2}",
                                      m_taskId,
                                      commandLine,
                                      ex.ToString());
            }
            return resultCode;
        }

        /// <summary>
        /// Parse query result for Oracle Home version for earlier release
        /// Compatible for version 9i or above
        /// </summary>
        /// <param name="queryOutput">Output</param>
        private string parseOracleHomeVersionEarlierRelease(string queryOutput) {
            StringBuilder result = new StringBuilder();
            if (!String.IsNullOrEmpty(queryOutput)) {
                foreach (String line in queryOutput.Split('\n', '\r')) {
                    if (s_releaseRegex.IsMatch(line)) {
                        Match match = s_releaseRegex.Match(line);
                        if (match.Length > 3) {
                            switch(match.Groups[1].ToString()) {
                                case "2" : result.Append("7"); break;
                                case "3" : result.Append("8"); break;
                                default: result.Append(match.Groups[1].ToString()); break;
                            }
                            result.Append(".").Append(match.Groups[2].ToString())
                                .Append(".").Append(match.Groups[3].ToString());
                            return result.ToString();
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Parse query result for Oracle Home version 
        /// Compatible for version 9.2 or later.
        /// </summary>
        /// <param name="queryOutput">Output</param>
        private string parseOracleHomeVersion(string queryOutput) {
            StringBuilder result = new StringBuilder();
            if (!String.IsNullOrEmpty(queryOutput)) {
                foreach (String line in queryOutput.Split('\n', '\r')) {
                    if (s_releaseRegex.IsMatch(line)) {
                        Match match = s_releaseRegex.Match(line);
                        if (match.Length > 3) {
                            result.Append(match.Groups[1].ToString()).Append(".")
                                .Append(match.Groups[2].ToString()).Append(".")
                                .Append(match.Groups[3].ToString());
                            return  result.ToString();
                        }
                    }
                }
            }
            return null;
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

        /// <summary>Map of collection script specific parameters.</summary>
        private IDictionary<string, string> m_scriptParameters;

        /// <summary>Map of attribute names to attribute element ids.</summary>
        private IDictionary<string, string> m_attributes;

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

        private static readonly Regex s_releaseRegex = new Regex(@"Release (\d+)\.(\d+)\.(\d+)\.\d+\.\d+", RegexOptions.Compiled);
    }
}
