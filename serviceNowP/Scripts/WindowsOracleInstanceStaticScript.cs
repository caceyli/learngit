#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.34 $
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
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    public class  WindowsOracleInstanceStaticScript : ICollectionScriptRuntime {

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

            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_connection = connection;
            string strOracleHome = null, strSchemaName = null, strSchemaPassword = null;

            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceStaticScript.",
                                  m_taskId);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceStaticScript is null.",
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
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }

                if (!scriptParameters.ContainsKey("OracleHome")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"OracleHome\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strOracleHome = scriptParameters["OracleHome"].Trim();
                    if (strOracleHome.EndsWith(@"\")) {
                        strOracleHome = strOracleHome.Substring(0, strOracleHome.Length - 1);
                    }
                }

                if (!connection.ContainsKey("schemaName")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"schemaName\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strSchemaName = connection["schemaName"].ToString().Trim();
                }

                if (!connection.ContainsKey("schemaPassword")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"schemaPassword\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    strSchemaPassword = connection["schemaPassword"].ToString().Trim();
                }


                if (ResultCodes.RC_SUCCESS == resultCode) {
                    // Check Remote Process Temp Directory
                    if (!connection.ContainsKey("TemporaryDirectory")) {
                        connection["TemporaryDirectory"] = @"%TMP%";
                    } else {
                        if (!m_connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                            if (!Lib.ValidateDirectory(m_taskId, m_connection[@"TemporaryDirectory"].ToString(), cimvScope)) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} is not valid.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} has been validated.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                            }
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    string strTempDir = connection["TemporaryDirectory"].ToString().Trim();
                    if (strTempDir.EndsWith(@"\")) {
                        strTempDir = strTempDir.Substring(0, strTempDir.Length-1);
                    }

                    string strBatchFileContent = buildBatchFile(strTempDir, strOracleHome, strSchemaName, strSchemaPassword);
                    StringBuilder stdoutData = new StringBuilder();
                    using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile
                        (m_taskId, cimvScope, strBatchFileContent, connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                        //This method will block until the entire remote process operation completes.
                        resultCode = rp.Launch();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote process operation completed with result code {1}.",
                                              m_taskId,
                                              resultCode.ToString());
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            stdoutData.Append(rp.Stdout);
                            if (rp.Stdout != null && rp.Stdout.Length > 0) {
                                if (rp.Stdout.ToString().Contains("ORA-01017")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Oracle L3 credential is invalid.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                                }
                                else if (rp.Stdout.ToString().Contains("ERROR-")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Batch file execution exception.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                }
                                else if (!rp.Stdout.ToString().Contains(@"BDNA")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: SQLPLUS exception, no proper data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                }
                                else if (!rp.Stdout.ToString().Contains(@"Execution completed")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Exception with batch file return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                          m_taskId);

                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                }
                            }
                            else {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                      m_taskId);
                                resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                            }
                        }
                        else {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Remote execution error.\nSTDOUT.STDERR:\n{1}",
                                                  m_taskId,
                                                  rp.Stdout.ToString());
                        }
                    }
                    if (resultCode == ResultCodes.RC_SUCCESS && stdoutData.Length > 0) {
                        foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                            entry.Value.ResultHandler(this, entry.Key, stdoutData.ToString());
                        }
                    }
                }
            }
            catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceStaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceStaticScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Set TFTP UNC Path
        /// </summary>
        /// <param name="TftpPath">TFTP Path</param>
        public void SetTFTPPath(string TftpPath) {
            m_tftpUNCPath = TftpPath;
        }

        /// <summary>
        /// Build temporary batch file.
        /// </summary>
        /// <param name="strTempDir"></param>
        private string buildBatchFile(string strTempDir, string strOracleHome, string strSchemaName, string strSchemaPassword) {
            StringBuilder strBatchFile = new StringBuilder();
            if (!String.IsNullOrEmpty(strTempDir)) {
                if (strTempDir.EndsWith(@"\")) {
                    strTempDir = strTempDir.Substring(0, strTempDir.Length-1);
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

            foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                string strQuery = entry.Value.QueryString;
                if (!string.IsNullOrEmpty(strQuery)) {
                    strBatchFile.Append("ECHO ");
                    strBatchFile.Append(strQuery.Trim().Replace("<", "^<")
                        .Replace(">", "^>").Replace("|", @"^|"));
                    if (!strQuery.EndsWith(";")) {
                        strBatchFile.Append(";");
                    }
                    strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                }
            }
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
        /// This is a helper method that will return first group that matches given expression
        /// </summary>
        /// 
        /// <param name="line">single line to be matched</param>
        /// <param name="regex">Regular expression to use.</param>
        /// 
        /// <returns>First group</returns>
        private static string matchFirstGroup(string line, Regex regex) {
            String ret = "";
            MatchCollection matches = regex.Matches(line);
            foreach (Match m in matches) {
                if (m.Groups.Count > 0) {
                    ret = m.Groups[1].ToString().Trim();
                }
                break;
            }
            return ret;
        }


        /// <summary>
        /// Result parser for v$parameter table.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void tableVParameterHandler
            (WindowsOracleInstanceStaticScript scriptInstance, String attributeNames, String queryOutput) {
            IDictionary<string, string> result = new Dictionary<string, string>();
            StringBuilder logData = new StringBuilder();
            foreach (String line in queryOutput.Split('\n', '\r')) {
                if (s_enterpriseRegex.IsMatch(line)) {
                    result["edition"] = "Enterprise";
                }
                else if (s_personalRegex.IsMatch(line)) {
                    result["edition"] = "Personal";
                }
                else if (s_expressRegex.IsMatch(line)) {
                    result["edition"] = "Express";
                }

                if (s_dbNameRegex.IsMatch(line)) {
                    result["databaseName"] = matchFirstGroup(line, s_dbNameRegex);
                    logData.AppendFormat("db_name: {0}\n", result["databaseName"]);
                }

                if (s_dbDomain.IsMatch(line)) {
                    result["databaseDomain"] = matchFirstGroup(line, s_dbDomain);
                    logData.AppendFormat("db_domain: {0}\n", result["databaseDomain"]);
                }

                if (s_mtsServiceRegex.IsMatch(line)) {
                    result["serviceName"] = matchFirstGroup(line, s_mtsServiceRegex);
                    logData.AppendFormat("mts_service: {0}\n", result["serviceName"]);
                }

                if (result.ContainsKey("serviceName") == false) {
                    if (s_serviceNameRegex.IsMatch(line)) {
                        result["serviceName"] = matchFirstGroup(line, s_serviceNameRegex);
                        logData.AppendFormat("service_names: {0}\n", result["serviceName"]);
                    }
                }

                if (s_compatibleRegex.IsMatch(line)) {
                    result["compatibleVersion"] = matchFirstGroup(line, s_compatibleRegex);
                    logData.AppendFormat("compatible: {0}\n", result["compatibleVersion"]);
                }

                if (s_instanceNumberRegex.IsMatch(line)) {
                    result["instanceNumber"] = matchFirstGroup(line, s_instanceNumberRegex);
                    logData.AppendFormat("instance number: {0}\n", result["instanceNumber"]);
                }

                if (s_clusterRegex.IsMatch(line)) {
                    result["clusterDatabaseInstances"] = matchFirstGroup(line, s_clusterRegex);
                    logData.AppendFormat("instance number: {0}\n", result["clusterDatabaseInstances"]);
                }
            }
            if (result.Count > 0 && !result.ContainsKey("edition")) {
                result["edition"] = "Standard";
            }

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: v$parameter parser results:\n{1}",
                                  scriptInstance.m_taskId,
                                  logData.ToString());
            // Package output data
            foreach (String name in attributeNames.Split(new String[] { "<BDNA>" }, StringSplitOptions.RemoveEmptyEntries)) {
                if (result.ContainsKey(name) && !String.IsNullOrEmpty(result[name])) {
                    scriptInstance.BuildDataRow(name, result[name]);
                }
            }
        }

        /// <summary>
        /// Result parser for v$parameter table.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void tableVOptionHandler
            (WindowsOracleInstanceStaticScript scriptInstance, String attributeName, String queryOutput) {
            string RACEnabled = "0",  Partitioning = "0";
            foreach (String line in queryOutput.Split('\n', '\r')) {
                if (s_realCluster.IsMatch(line)) {
                    RACEnabled = "1";
                }

                if (s_partioningRegex.IsMatch(line)) {
                    Partitioning = "1";
                }
            }
            scriptInstance.BuildDataRow("RACEnabled", RACEnabled);
            scriptInstance.BuildDataRow("Partitioning", Partitioning);
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: RACEnabled: {1}, Partitioning: {2}",
                                  scriptInstance.m_taskId,
                                  RACEnabled,
                                  Partitioning);
        }

        /// <summary>
        /// Result parser for v$dba_tab_partitions table.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void tableDBA_TAB_PARTITIONS_Handler
            (WindowsOracleInstanceStaticScript scriptInstance, String attributeName, String queryOutput) {

            StringBuilder strPartitionSchema = new StringBuilder();
            foreach (String line in queryOutput.Split('\n', '\r')) {
                if (s_tabPartionRegex.IsMatch(line)) {
                    MatchCollection matches = s_tabPartionRegex.Matches(line);
                    foreach (Match m in matches) {
                        if (m.Groups.Count > 1) {
                            string owner = m.Groups[1].ToString().Trim();
                            string count = m.Groups[2].ToString().Trim();
                            if (strPartitionSchema.Length > 0) {
                                strPartitionSchema.Append(BdnaDelimiters.DELIMITER_TAG);
                            }
                            //strPartitionSchema.Append(owner).Append(@", ").Append(count);
                            strPartitionSchema.Append(owner).Append(BdnaDelimiters.DELIMITER_TAG).Append(count);
                        }
                    }
                }
            }
            if (strPartitionSchema.Length > 0) {
                scriptInstance.BuildDataRow("partitionedSchemas", strPartitionSchema.ToString());
                scriptInstance.BuildDataRow("PartitioningApplied", "1");
            }
            else {
                scriptInstance.BuildDataRow("PartitioningApplied", "0");
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: PartitioningSchema {1}.",
                                  scriptInstance.m_taskId,
                                  strPartitionSchema);
        }

        /// <summary>
        /// Parse query result for a single value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void SingleValueHandler
            (WindowsOracleInstanceStaticScript scriptInstance, String attributeName, String queryOutput) {
            string value = null;
            StringBuilder logData = new StringBuilder();
            Regex r = new Regex(@"^<BDNA>"+ attributeName +@"<BDNA>(.*?)<BDNA>$");
            
            foreach (String line in queryOutput.Split('\n', '\r')) {
                if (r.IsMatch(line)) {
                    value = matchFirstGroup(line, r);
                    logData.AppendFormat("{0}: {1}\n", attributeName, value);
                    break;
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",
                                  scriptInstance.m_taskId,
                                  attributeName,
                                  logData.ToString());
            if (!String.IsNullOrEmpty(value)) {
                scriptInstance.BuildDataRow(attributeName, value);
            }
        }

        /// <summary>
        /// Parse query result for multiple values.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void MultiValueHandler
            (WindowsOracleInstanceStaticScript scriptInstance, String attributeNames, String queryOutput) {
            IDictionary<string, string> patterns = new Dictionary<string, string>();
            foreach (String name in attributeNames.Split(new String[] { "<BDNA>" }, StringSplitOptions.RemoveEmptyEntries)) {
                if (!String.IsNullOrEmpty(name)) {
                    patterns[name] = @"<BDNA>"+name.Trim()+@"<BDNA>(.*?)<BDNA>";
                }
            }

            StringBuilder logData = new StringBuilder();

            foreach (string line in queryOutput.Split('\n', '\r')) {
                foreach (KeyValuePair<string, string> pattern in patterns) {
                    Regex r = new Regex(pattern.Value);
                    if (r.IsMatch(line)) {
                        string name = pattern.Key;
                        string result = matchFirstGroup(line, r);
                        scriptInstance.BuildDataRow(name, result);
                        logData.AppendFormat("{0}: {1}\n", name, result);
                    }
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Multiple value parser results:\n{1}",
                                  scriptInstance.m_taskId,
                                  logData.ToString());
        }

        /// <summary>
        /// Signature for query result handlers.
        /// </summary>
        private delegate void QueryResultHandler(WindowsOracleInstanceStaticScript scriptInstance, string attributeName, string outputData);

        /// <summary>
        /// Helper class to match up a query with the correct result handler.
        /// </summary>
        private class QueryTableEntry {
            public QueryTableEntry(string queryString, QueryResultHandler resultHandler) {
                m_queryString = queryString;
                m_resultHandler = resultHandler;
            }

            /// <summary>
            /// Gets the query string.
            /// </summary>
            public string QueryString {
                get { return m_queryString; }
            }

            /// <summary>\
            /// Gets the result handler.
            /// </summary>
            public QueryResultHandler ResultHandler {
                get { return m_resultHandler; }
            }

            /// <summary>Query string.</summary>
            private readonly string m_queryString;

            /// <summary>Result handler.</summary>
            private readonly QueryResultHandler m_resultHandler;
        }

        /// <summary>
        /// Static initializer to build up a map of supported attribute
        /// names to their associated query strings at class load time.
        /// </summary>
        static WindowsOracleInstanceStaticScript() {
            foreach (KeyValuePair<string, QueryTableEntry> kvp in s_queryTable) {
                s_attributeMap[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>TFTP.exe UNC path</summary>
        private string m_tftpUNCPath = string.Empty;

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

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>
        /// This table pairs up known attribute names with the query string needed to get the correct value from an Oracle
        /// database.  This table exists merely to seed the attribute map which will be used by the task execution code.
        /// </summary>
        private static readonly KeyValuePair<string, QueryTableEntry>[] s_queryTable = {
            new KeyValuePair<string, QueryTableEntry>
            (@"edition<BDNA>databaseName<BDNA>databaseDomain<BDNA>compatibleVersion<BDNA>instanceNumber<BDNA>clusterDatabaseInstances<BDNA>serviceName",
            new QueryTableEntry(@"select '<BDNA>' || name || '<BDNA>' || value || '<BDNA>' from v$parameter "+
                                @" where name in " +
                                @" ('db_name', 'db_domain', 'mts_service', 'compatible', 'instance_number', 'service_names', 'cluster_database_instances') order by name",
                                new QueryResultHandler(tableVParameterHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"RACEnabled<BDNA>Partitioning", 
            new QueryTableEntry(@"select '<BDNA>' || parameter || '<BDNA>' || value || '<BDNA>'"+
                                @" from v$option where parameter in ('Real Application Clusters',"+
                                @" 'Partitioning');",
                                new QueryResultHandler(tableVOptionHandler))),
            new KeyValuePair<string, QueryTableEntry>("partitionedSchemas<BDNA>PartitioningApplied",
            new QueryTableEntry(@"select '<BDNA>' || 'TAB_PARTITION' || '<BDNA>' || table_owner || '<BDNA>' || to_char(count(distinct table_name)) || '<BDNA>' from dba_tab_partitions "+
                                @" where table_owner <> 'SYSTEM' "+
                                @" group by table_owner;",
                                new QueryResultHandler(tableDBA_TAB_PARTITIONS_Handler))),
            new KeyValuePair<string, QueryTableEntry>("version",
            new QueryTableEntry(@"select '<BDNA>' || 'version' || '<BDNA>' || version || '<BDNA>' from v$instance;",
                                new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>("dbRole",
            new QueryTableEntry(@"select '<BDNA>' || 'database_role' || '<BDNA>' from v$database;",
                                new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>("startDate<BDNA>upTime",
            new QueryTableEntry(@"select '<BDNA>startDate<BDNA>' || startup_time || '<BDNA>upTime<BDNA>' || ((sysdate-startup_time)*86400) || '<BDNA>' from v$instance;",
                                new QueryResultHandler(MultiValueHandler)))

        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();

        private static readonly Regex s_enterpriseRegex = new Regex("Enterprise Edition Release", RegexOptions.Compiled);

        private static readonly Regex s_personalRegex = new Regex(@"Personal .+ Release", RegexOptions.Compiled);

        private static readonly Regex s_expressRegex = new Regex("Express Edition Release", RegexOptions.Compiled);

        private static readonly Regex s_dbNameRegex = new Regex(@"<BDNA>db_name<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_dbDomain = new Regex(@"^<BDNA>db_domain<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_mtsServiceRegex = new Regex(@"^<BDNA>mts_service<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_serviceNameRegex = new Regex(@"^<BDNA>service_names<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_compatibleRegex = new Regex(@"^<BDNA>compatible<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_instanceNumberRegex = new Regex(@"^<BDNA>instance_number<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_clusterRegex = new Regex(@"^<BDNA>cluster_database_instances<BDNA>(.*)<BDNA>$", RegexOptions.Compiled);

        private static readonly Regex s_realCluster = new Regex(@"^<BDNA>Real Application Clusters<BDNA>TRUE.*$", RegexOptions.Compiled);

        private static readonly Regex s_partioningRegex = new Regex(@"^<BDNA>Partitioning<BDNA>TRUE.*$", RegexOptions.Compiled);

        private static readonly Regex s_tabPartionRegex = new Regex(@"^<BDNA>TAB_PARTITION<BDNA>(.*)<BDNA>(\d+)<BDNA>$", RegexOptions.Compiled);
    }
}
