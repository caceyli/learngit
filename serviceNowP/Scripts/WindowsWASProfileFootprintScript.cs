#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Suma Manvi
*   Creation Date: 2007/10/04
*
* Current Status
*       $Revision: 1.8 $
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
    public class WindowsWASProfileFootprintScript : ICollectionScriptRuntime {
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
            m_executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsWASProfileFootprintScript.",
                                  m_taskId);
            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                // Check ManagementScope CIMV
                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsWASProfileFootprintScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"default")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    defaultScope = connection[@"default"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              m_taskId);
                    } else if (!defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed.",
                                              m_taskId);
                    }
                }

                //Check WAS Installation Path attribute
                if (resultCode.Equals(ResultCodes.RC_SUCCESS)) {
                    if (scriptParameters.ContainsKey("profilePath")) {
                        m_profileHome = scriptParameters[@"profilePath"];
                    } else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Profile Path parameter.",
                                              m_taskId);
                    }
                }

                // Check Remote Process Temp Directory
                if (resultCode.Equals(ResultCodes.RC_SUCCESS)) {
                    if (!connection.ContainsKey(@"TemporaryDirectory")) {
                        connection[@"TemporaryDirectory"] = @"%TMP%";
                    } else {
                        if (!connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                            if (!Lib.ValidateDirectory(m_taskId, connection[@"TemporaryDirectory"].ToString(), cimvScope))
                            {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} is not valid.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: User specified temp directory has been validated.",
                                                      m_taskId);
                            }
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Attempting to retrieve file {1}.",
                                          m_taskId,
                                          m_profileHome);
                    StringBuilder fileContent = new StringBuilder();
                    StringBuilder setupCmd = new StringBuilder();
                    setupCmd.Append((string)m_profileHome).Append(@"\bin\setupCmdLine.bat");

                    if (Lib.ValidateFile(m_taskId, setupCmd.ToString(), cimvScope)) {
                        using (IRemoteProcess rp =
                                RemoteProcess.GetRemoteFile(m_taskId, cimvScope, setupCmd.ToString(), connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher))
                        {
                            //
                            // Launch the remote process.
                            // This method will block until the entire remote process operation completes.
                            resultCode = rp.Launch();
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Remote file retrieval operation completed with result code {1}.",
                                                  m_taskId,
                                                  resultCode.ToString());
                            fileContent.Append(rp.Stdout);
                        }
                        // Parse file content
                        resultCode = parseCmdlineFile(fileContent.ToString());

                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: WAS setupCmdline file: {1} does not exist.",
                                              m_taskId,
                                              m_profileHome);
                        resultCode = ResultCodes.RC_SUCCESS;
                    }
                }
            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in WindowsWASProfileFootprintScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            CollectionScriptResults result = new CollectionScriptResults(resultCode,
                                                                         0,
                                                                         null,
                                                                         null,
                                                                         null,
                                                                         false,
                                                                         m_dataRow.ToString());
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsWASProfileFootprintScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
        #endregion ICollectionScriptRuntime Members

        /// <summary>
        /// Parse WAS profile setupCmdline file.
        /// </summary>
        /// <param name="strFileContent">File Content</param>
        private ResultCodes parseCmdlineFile(string cmdFileContent)  {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REMOTE_FILE;
            if (String.IsNullOrEmpty(cmdFileContent)) return resultCode;

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Begin parsing WAS setupCmdline file.",
                                  m_taskId);
            try  {
                StringBuilder strFileContent = new StringBuilder();
                if (!String.IsNullOrEmpty(cmdFileContent))  {
                    string[] strLines = cmdFileContent.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    string cellName = String.Empty;
                    string nodeName = String.Empty;
                    foreach (string strOneLine in strLines)  {
                        if (String.IsNullOrEmpty(strOneLine)) continue;
                        string strLine = strOneLine.Trim();

                        // Grab matching cell and node names
                        if (s_cellPattern.IsMatch(strLine)) {
                            Match match1 = s_cellPattern.Match(strLine);
                            cellName = match1.Groups["cellName"].ToString();
                            BuildDataRow(s_cellName, cellName);
                         }
                        if (s_nodePattern.IsMatch(strLine)) {
                            Match match2 = s_nodePattern.Match(strLine);
                            nodeName = match2.Groups["nodeName"].ToString();
                            BuildDataRow(s_nodeName, nodeName);
                        }
                    }
                    StringBuilder cellPath = new StringBuilder();
                    cellPath.Append(m_profileHome).Append(@"\config\cells\").Append(cellName).Append(@"\cell.xml").Append(@"=cellData");
                    BuildDataRow(s_cellPath, cellPath.ToString());

                }

                resultCode = ResultCodes.RC_SUCCESS;
            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in WindowsWASProfileFootprint",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        ///
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData)  {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            }
            else if (string.IsNullOrEmpty(collectedData)) {
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

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>WebSphere Application Server profile path. </summary>
        private string m_profileHome = String.Empty;

        /// <summary>Execution Timer</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId = String.Empty;

        private static string s_cellName = @"cellName";
        private static string s_nodeName = @"nodeName";
        private static string s_cellPath = @"cellPath";
        private static Regex s_cellPattern = new Regex(@"WAS_CELL=(?<cellName>.+)",
                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex s_nodePattern = new Regex(@"WAS_NODE=(?<nodeName>.+)",
                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
    }

