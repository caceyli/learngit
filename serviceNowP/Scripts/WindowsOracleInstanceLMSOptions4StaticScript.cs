#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alex Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.10 $
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
    public class WindowsOracleInstanceLMSOptions4StaticScript : ICollectionScriptRuntime {

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
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions4StaticScript.",
                                  m_taskId);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceLMSOptions4StaticScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }
                if (!scriptParameters.ContainsKey("version")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"version\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    m_strVersion = scriptParameters["version"].Trim();
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
                        strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
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
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Task Id {0}: Query Output:\n{1}",
                                                      m_taskId,
                                                      rp.Stdout.ToString());

                                if (rp.Stdout.ToString().Contains("ORA-01017")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Oracle L3 credential is invalid.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                                } else if (rp.Stdout.ToString().Contains("ERROR-")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Batch file execution exception.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                } else if (!rp.Stdout.ToString().Contains(@"BDNA")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: SQLPLUS exception, no proper data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                } else if (!rp.Stdout.ToString().Contains(@"Execution completed")) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Exception with batch return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
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
                                                  "Task Id {0}: Remote execution error.\nSTDOUT.STDERR:\n{1}",
                                                  m_taskId,
                                                  rp.Stdout.ToString());
                        }
                    }
                    if (resultCode == ResultCodes.RC_SUCCESS && stdoutData.Length > 0) {
                        foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                            entry.Value.ResultHandler(this, entry.Key, stdoutData.ToString());
                        }
                        if (!ver8_pattern.IsMatch(m_strVersion)) {
                            this.processContentAndRecordDatabaseCollectedData();
                        }
                        this.processAdvancedSecurityDatabaseCollectedData();
                        foreach (KeyValuePair<string, string> kvp in m_collectedData) {
                            this.BuildDataRow(kvp.Key, kvp.Value);
                        }
                    }
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions4StaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions4StaticScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions4StaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Save collected data
        /// </summary>
        /// <param name="attributeName">attribute name</param>
        /// <param name="collectedData">collected data</param>
        public void SaveCollectedData(string attributeName, string collectedData) {
            m_collectedData[attributeName] = collectedData;
        }

        public IDictionary<string, string> CollectedData {
            get {
                return m_collectedData;
            }
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        public void BuildDataRow(string attributeName, string collectedData) {
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
        /// Process Content and Record Database Collected Data
        /// </summary>
        private void processContentAndRecordDatabaseCollectedData() {
            StringBuilder contentDBQuery = new StringBuilder();
            StringBuilder recordDBQuery = new StringBuilder();
            contentDBQuery.Append("SQL> SELECT DECODE(UPPER(MAX(USERNAME)), 'CONTENT', 'CONTENT schema exist',")
                          .Append(" 'CONTENT schema does not exist') \"CONTENT DB and RECORD DB\" FROM DBA_USERS WHERE UPPER(USERNAME)='CONTENT';\n\n");

            int recordDBUsed = 0, contentDBUsed = 0;
            int recordDBInstalled = 0, contentDBInstalled = 0;
            if (CollectedData.ContainsKey(@"contentDBInstalled")) {
                if (!ora_error_pattern.IsMatch(CollectedData[@"contentDBInstalled"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"contentDBInstalled"])) {

                    contentDBQuery.Append("CONTENT DB and RECORD DB\n")
                                  .Append("-------------------------------------\n");

                }
                contentDBQuery.Append(CollectedData[@"contentDBInstalled"]).AppendLine();
                if (CollectedData[@"contentDBInstalled"] == @"CONTENT schema exist") {
                    contentDBInstalled = recordDBInstalled = 1;
                } 
            }
            contentDBQuery.AppendLine().AppendLine();
            recordDBQuery.Append(contentDBQuery);

            if (CollectedData.ContainsKey(@"odmDocCustObjCnt")) {
                contentDBQuery.AppendLine(@"CHECKING TO SEE IF CONTENT DATABASE IS BEING USED:").AppendLine();
                contentDBQuery.Append("SQL> SELECT (COUNT(*)-9004) \"ODM_Document Customer Objects\" FROM CONTENT.ODM_DOCUMENT;").AppendLine();
                if (!ora_error_pattern.IsMatch(CollectedData[@"odmDocCustObjCnt"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"odmDocCustObjCnt"])) {

                    contentDBQuery.Append("ODM_Document Customer Objects").AppendLine();
                    contentDBQuery.AppendLine("-------------------------------------\n");
                    contentDBQuery.Append(CollectedData[@"odmDocCustObjCnt"]).AppendLine();
                    try {
                        int count = Convert.ToInt32(CollectedData[@"odmDocCustObjCnt"]);
                        if (count > 0) {
                            contentDBUsed = 1;
                        }
                    } catch { 
                        CollectedData[@"odmDocCustObjCnt"] = ""+0;                    
                    }
                } else {
                    contentDBQuery.Append(CollectedData[@"odmDocCustObjCnt"]).AppendLine();
                    CollectedData[@"odmDocCustObjCnt"] = ""+0;
                }
            }

            if (CollectedData.ContainsKey(@"recordODM_Rec_Cnt")) {
                recordDBQuery.AppendLine(@"CHECKING TO SEE IF RECORDS DATABASE IS BEING USED: ").AppendLine();
                recordDBQuery.Append("SQL> SELECT COUNT(*) \"ODM_RECORD Customer Objects\" FROM CONTENT.ODM_RECORD;").AppendLine();
                if (!ora_error_pattern.IsMatch(CollectedData[@"recordODM_Rec_Cnt"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"recordODM_Rec_Cnt"])) {

                    recordDBQuery.Append("ODM_RECORD Customer Objects").AppendLine();
                    recordDBQuery.AppendLine("-------------------------------------\n");
                    recordDBQuery.Append(CollectedData[@"recordODM_Rec_Cnt"]).AppendLine();
                    try {
                        int count = Convert.ToInt32(CollectedData[@"recordODM_Rec_Cnt"]);
                        if (count > 0) {
                            recordDBUsed = 1;
                        }
                    } catch {
                        CollectedData[@"recordODM_Rec_Cnt"] = ""+0;
                    }
                } else {
                    recordDBQuery.Append(CollectedData[@"recordODM_Rec_Cnt"]).AppendLine();
                    CollectedData[@"recordODM_Rec_Cnt"] = "" + 0;
                }
            }
            CollectedData[@"contentDBInstalled"] = contentDBInstalled.ToString();
            CollectedData[@"b_contentDBInstalled"] = contentDBInstalled.ToString();
            CollectedData[@"contentDBUsed"] = contentDBUsed.ToString();
            CollectedData[@"b_contentDBUsed"] = contentDBUsed.ToString();
            CollectedData[@"contentDBQuery"] = contentDBQuery.ToString() + "\n\n";
            CollectedData[@"recordDBInstalled"] = recordDBInstalled.ToString();
            CollectedData[@"b_recordDBInstalled"] = recordDBInstalled.ToString();
            CollectedData[@"recordDBUsed"] = recordDBUsed.ToString();
            CollectedData[@"b_recordDBUsed"] = recordDBUsed.ToString();
            CollectedData[@"recordDBQuery"] = recordDBQuery.ToString() + "\n\n";
        }


        /// <summary>
        /// Process Advanced Security Collected Data
        /// </summary>
        private void processAdvancedSecurityDatabaseCollectedData() {
            StringBuilder advSecQuery = new StringBuilder();
            int advSecUsed =0, advSecInstalled =0;
            advSecQuery.AppendLine("CHECKING IF ADVANCED SECURITY OPTION FUNCTIONALITIES ARE IN USE..").AppendLine();
            advSecQuery.AppendLine("LOOKING FOR SESSIONS USING NETWORK ENCRYPTION AND S. Auth").AppendLine();

            advSecQuery.Append("SQL> SELECT AUTHENTICATION_TYPE||','||SID||','||OSUSER||','||NETWORK_SERVICE_BANNER ").AppendLine()
                       .Append("\"Network Encryption and Strong Authentication\" FROM V$SESSION_CONNECT_INFO ").AppendLine()
                       .Append("WHERE LOWER(NETWORK_SERVICE_BANNER) LIKE '%authentication service%' OR ").AppendLine()
                       .Append("LOWER(NETWORK_SERVICE_BANNER) LIKE '%encryption service%' OR ").AppendLine()
                       .AppendLine("LOWER(NETWORK_SERVICE_BANNER) LIKE '%crypto_checksumming%' ORDER BY 1;").AppendLine();

            if (CollectedData.ContainsKey(@"advSecMode")) {
                if (!string.IsNullOrEmpty(CollectedData[@"advSecMode"])) {
                    if (!ora_error_pattern.IsMatch(CollectedData[@"advSecMode"]) &&
                        !no_row_selected_pattern.IsMatch(CollectedData[@"advSecMode"])) {

                        advSecQuery.Append("Network Encryption and S. Auth\n")
                                   .Append("-------------------------------------------------\n");
                        if (advSecurity_pattern.IsMatch(CollectedData[@"advSecMode"])) {
                            advSecUsed = advSecInstalled = 1;
                        }
                    }
                }
                advSecQuery.AppendLine(CollectedData[@"advSecMode"]);
                advSecQuery.AppendLine().AppendLine();
            }

            advSecQuery.AppendLine("LOOKING FOR TABLESPACES USING TDE (TRANSPARENT DATA ENCRYPTION)- 11G AND ABOVE").AppendLine();
            advSecQuery.Append("SQL> SELECT OWNER||','||TABLE_NAME||','||COLUMN_NAME \"Column Encryption\" FROM DBA_ENCRYPTED_COLUMNS ORDER BY 1;");
            advSecQuery.AppendLine().AppendLine();
            if (CollectedData.ContainsKey(@"columnEncryption")) {
                if (!string.IsNullOrEmpty(CollectedData[@"columnEncryption"])) {
                    if (!ora_error_pattern.IsMatch(CollectedData[@"columnEncryption"]) &&
                        !no_row_selected_pattern.IsMatch(CollectedData[@"columnEncryption"])) {
                        advSecQuery.Append("Column Encryption\n")
                                   .Append("-------------------------------------------------\n");

                        advSecUsed = advSecInstalled = 1;
                        if (CollectedData[@"columnEncryption"].Contains("<BDNA,>")) {
                            String[] table = CollectedData[@"columnEncryption"].Split(new string[] { "<BDNA,>" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (String line in table) {
                                advSecQuery.AppendLine(line);
                            }
                        } 
                    } else {
                        advSecQuery.AppendLine(CollectedData[@"columnEncryption"]);
                    }
                }
                advSecQuery.AppendLine().AppendLine();
            }

            advSecQuery.AppendLine("CHECKING IF ADVANCED SECURITY OPTION FUNCTIONALITIES ARE IN USE ...");
            advSecQuery.AppendLine("LOOKING FOR SESSIONS USING NETWORK ENCRYPTION AND STRONG AUTHENTICATION").AppendLine();
            advSecQuery.AppendLine("SQL> SELECT TABLESPACE_NAME||','||ENCRYPTED \"Tablespace Encryption\" FROM DBA_TABLESPACES WHERE ENCRYPTED='YES'").AppendLine();
            if (CollectedData.ContainsKey(@"tableEncryption")) {
                if (!string.IsNullOrEmpty(CollectedData[@"tableEncryption"])) {
                    if (!ora_error_pattern.IsMatch(CollectedData[@"tableEncryption"]) &&
                        !no_row_selected_pattern.IsMatch(CollectedData[@"tableEncryption"])) {
                    
                        advSecQuery.Append("Tablespace Encryption\n")
                                   .Append("-------------------------------------------------\n");

                        advSecUsed = advSecInstalled = 1;
                        if (CollectedData[@"tableEncryption"].Contains("<BDNA,>")) {
                            String[] table = CollectedData[@"tableEncryption"].Split(new string[] { "<BDNA,>" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (String line in table) {
                                advSecQuery.AppendLine(line);
                            }
                        } 
                    } else {
                        advSecQuery.AppendLine(CollectedData[@"tableEncryption"]);
                    }
                }
                advSecQuery.AppendLine().AppendLine();
            }

            CollectedData[@"advSecInstalled"] = advSecInstalled.ToString();
            CollectedData[@"b_advSecInstalled"] = advSecInstalled.ToString();
            CollectedData[@"advSecUsed"] = advSecUsed.ToString();
            CollectedData[@"b_advSecUsed"] = advSecUsed.ToString();
            CollectedData[@"advSecQuery"] = advSecQuery.ToString();
        }



        /// <summary>
        /// Build temporary batch file.
        /// </summary>
        /// <param name="strTempDir"></param>
        private string buildBatchFile(string strTempDir, string strOracleHome, string strSchemaName, string strSchemaPassword) {
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

            strBatchFile.Append("ECHO SET SERVEROUTPUT ON;")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO SET LINESIZE 999;")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");


            foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {
                string strQuery = entry.Value.QueryString;
                string strName = entry.Key;
                Regex regex = entry.Value.regex;
                if ((!string.IsNullOrEmpty(strQuery)) && (regex.IsMatch(m_strVersion))) {
                    strBatchFile.Append("ECHO PROMPT "+ strName + @"_BEGIN___;")
                                .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    strBatchFile.Append("ECHO ");
                    strBatchFile.Append(strQuery.Trim().Replace("<", "^<")
                        .Replace(">", "^>").Replace("|", @"^|"));
                    if (!strQuery.EndsWith(";")) {
                        strBatchFile.Append(";");
                    }
                    strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    strBatchFile.Append("ECHO PROMPT "+ @"___"+strName + @"_END;")
                                .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
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
        /// Parse query result for Column Encryption value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void columnEncryptionValueHandler
            (WindowsOracleInstanceLMSOptions4StaticScript scriptInstance, String attributeName, String queryOutput) {

            string output = ExtractQueryOutput(attributeName, queryOutput);
            Regex r = new Regex(@"^<BDNA>columnEncryption<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();
            foreach (String line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String OWNER = match.Groups[1].ToString();
                        String TABLE_NAME = match.Groups[2].ToString();
                        String COLUMN_NAME = match.Groups[3].ToString();
                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(OWNER);
                        result.Append(@",").Append(TABLE_NAME);
                        result.Append(@",").Append(COLUMN_NAME).AppendLine();
                    }
                } else if (no_row_selected_pattern.IsMatch(line)) {
                    result.Append(matchFirstGroup(line, no_row_selected_pattern)); 
                    logData.AppendLine("No rows selected.");
                    break;
                } else if (ora_error_pattern.IsMatch(line)) {
                    result.Append(matchFirstGroup(line, ora_error_pattern));
                    logData.AppendLine("Oracle error..");
                } 
            } 
            if (result.Length > 0) {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
            }
        }

        /// <summary>
        /// Parse query result for TableSpace Encryption value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void tablespaceEncryptionValueHandler
            (WindowsOracleInstanceLMSOptions4StaticScript scriptInstance, String attributeName, String queryOutput) {

            string output = ExtractQueryOutput(attributeName, queryOutput);
            Regex r = new Regex(@"^<BDNA>tableEncryption<BDNA>(.*?)<BDNA>(.*?)<BDNA>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();
            foreach (String line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String TABLESPACE_NAME = match.Groups[1].ToString();
                        String ENCRYPTED = match.Groups[2].ToString();
                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(TABLESPACE_NAME);
                        result.Append(@",").Append(ENCRYPTED).AppendLine();
                    }
                } else if (no_row_selected_pattern.IsMatch(line)) {
                    result.Append(matchFirstGroup(line, no_row_selected_pattern));
                    logData.AppendLine("No rows selected.");
                    break;
                } else if (ora_error_pattern.IsMatch(line)) {
                    result.Append(matchFirstGroup(line, ora_error_pattern));
                    logData.AppendLine("Oracle error..");
                }
            } 
            if (result.Length > 0) {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
            }
        }

        /// <summary>
        /// Extract output of one query from batch execution using standard format.
        /// </summary>
        /// <param name="attributeName">Attribute Name</param>
        /// <param name="queryOutput">Batched Query output</param>
        /// <returns></returns>
        private static string ExtractQueryOutput(String attributeName, String queryOutput) {
            String output = string.Empty;

            int beginIndex = -1;
            int endIndex = -1;
            String section = string.Empty;
            string beginStr = attributeName + @"_BEGIN___";
            string endStr = @"___"+attributeName + @"_END";
            if (queryOutput.Contains(beginStr)) {
                beginIndex = queryOutput.IndexOf(beginStr);
            }
            if (queryOutput.Contains(endStr)) {
                endIndex = queryOutput.IndexOf(endStr);
            }

            if ((beginIndex != -1) && (endIndex != -1)) {
                output = queryOutput.Substring(beginIndex + beginStr.Length, endIndex - beginIndex - beginStr.Length);
            }
            return output;
        }

        /// <summary>
        /// Parse query result for a single value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void SingleValueHandler
            (WindowsOracleInstanceLMSOptions4StaticScript scriptInstance, String attributeName, String queryOutput) {

            string value = null;
            StringBuilder logData = new StringBuilder();            

            //
            // Never compile a regular expression is not assigned to
            // a static reference.  Otherwise you will leak an Assembly.
            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            String output = ExtractQueryOutput(attributeName, queryOutput);

            if (!string.IsNullOrEmpty(output)) {
                foreach (String line in output.Split('\n', '\r')) {
                    if (r.IsMatch(line)) {
                        value = matchFirstGroup(line, r);
                        logData.AppendFormat("{0}: {1}\n", attributeName, value);
                        break;
                    } else if (no_row_selected_pattern.IsMatch(line)) {
                        value = matchFirstGroup(line, no_row_selected_pattern);
                        logData.AppendLine("No rows selected.");
                        break;
                    } else if (ora_error_pattern.IsMatch(line)) {
                        value = matchFirstGroup(line, ora_error_pattern);
                        logData.AppendLine("Oracle error..");
                    }
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",
                                  scriptInstance.m_taskId,
                                  attributeName,
                                  logData.ToString());
            if (!String.IsNullOrEmpty(value)) {
                scriptInstance.SaveCollectedData(attributeName, value);
            }
        }

        /// <summary>
        /// Signature for query result handlers.
        /// </summary>
        private delegate void QueryResultHandler(WindowsOracleInstanceLMSOptions4StaticScript scriptInstance, string attributeName, string outputData);

        /// <summary>
        /// Helper class to match up a query with the correct result handler.
        /// </summary>
        private class QueryTableEntry {

            public QueryTableEntry(string queryString, Regex regex, QueryResultHandler resultHandler) {
                m_queryString = queryString;
                m_resultHandler = resultHandler;
                m_regex = regex;
            }

            /// <summary>
            /// Get regex 
            /// </summary>
            public Regex regex {
                get { return m_regex; }
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

            /// <summary>Regex</summary>
            private readonly Regex m_regex;

            /// <summary>Query string.</summary>
            private readonly string m_queryString;

            /// <summary>Result handler.</summary>
            private readonly QueryResultHandler m_resultHandler;
        }

        /// <summary>
        /// Static initializer to build up a map of supported attribute
        /// names to their associated query strings at class load time.
        /// </summary>
        static WindowsOracleInstanceLMSOptions4StaticScript() {
            ICollection<KeyValuePair<string, QueryTableEntry>> ic = (ICollection<KeyValuePair<string, QueryTableEntry>>)s_attributeMap;

            foreach (KeyValuePair<string, QueryTableEntry> kvp in s_queryTable) {
                ic.Add(kvp);
            }
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

        /// <summary>Oracle version</summary>
        private string m_strVersion = String.Empty;

        /// <summary>Map of attribute names to attribute element ids.</summary>
        private IDictionary<string, string> m_attributes;

        /// <summary>Map of collection script specific parameters.</summary>
        private IDictionary<string, string> m_scriptParameters;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>Collected Data</summary>
        private IDictionary<string, string> m_collectedData = new Dictionary<string, string>();

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary> udt pattern</summary>
        private static Regex s_pattern = new Regex(@"(?<name>.+?)\s*=\s*(?<value>.+)",
                                                   RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex all_ver_pattern = new Regex(@".*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver8_pattern = new Regex(@"^8\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver9_pattern = new Regex(@"^9\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver10r1_pattern = new Regex(@"^10\.1.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver10r2_pattern = new Regex(@"^10\.2.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver11r2_pattern = new Regex(@"^11\.2.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver11_pattern = new Regex(@"^11\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex no_row_selected_pattern = new Regex(@"^(no rows selected)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex rows_selected_pattern = new Regex(@"^(\d+ rows selected.)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ora_error_pattern = new Regex(@"^(ORA-\d+: .*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex advSecurity_pattern = new Regex("(AES256|AES192|AES128|3DES168|3DES112|DES56C|DES40C|RC4_256|RC4_128|RC4_56|RC4_40|MD5|SHA1|RADIUS|Kerberos|SSL)", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        /// <summary>
        /// This table pairs up known attribute names with the query string needed to get the correct value from an Oracle
        /// database.  This table exists merely to seed the attribute map which will be used by the task execution code.
        /// </summary>
        private static readonly KeyValuePair<string, QueryTableEntry>[] s_queryTable = {
            new KeyValuePair<string, QueryTableEntry>(@"contentDBInstalled",
            new QueryTableEntry(@"SELECT '<BDNA>contentDBInstalled<BDNA>'||DECODE(UPPER(MAX(USERNAME)), 'CONTENT', 'CONTENT schema exist',"+
                                @" 'CONTENT schema does not exist')||'<BDNA>' FROM DBA_USERS WHERE UPPER(USERNAME)='CONTENT';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"odmDocCustObjCnt",
            new QueryTableEntry(@"SELECT '<BDNA>odmDocCustObjCnt<BDNA>'||(COUNT(*)-9004)||'<BDNA>' FROM CONTENT.ODM_DOCUMENT;",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"recordODM_Rec_Cnt",
            new QueryTableEntry(@"SELECT '<BDNA>recordODM_Rec_Cnt<BDNA>'||(COUNT(*)-9004)||'<BDNA>' FROM CONTENT.ODM_RECORD;",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"advSecMode",
            new QueryTableEntry(@"SELECT '<BDNA>advSecMode<BDNA>'||AUTHENTICATION_TYPE||','||SID||','||OSUSER||','||NETWORK_SERVICE_BANNER||'<BDNA>' "+
                                 "FROM V$SESSION_CONNECT_INFO "+
                                 "WHERE LOWER(NETWORK_SERVICE_BANNER) LIKE '%authentication service%' OR "+
                                 "LOWER(NETWORK_SERVICE_BANNER) LIKE '%encryption service%' OR "+
                                 "LOWER(NETWORK_SERVICE_BANNER) LIKE '%crypto_checksumming%' ORDER BY 1",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"columnEncryption",
            new QueryTableEntry(@"SELECT '<BDNA>columnEncryption<BDNA>'||OWNER||'<BDNA>'||TABLE_NAME||'<BDNA>'||COLUMN_NAME||'<BDNA>' "+
                                 "FROM DBA_ENCRYPTED_COLUMNS ORDER BY 1",
            all_ver_pattern,
            new QueryResultHandler(columnEncryptionValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"tableEncryption",
            new QueryTableEntry(@"SELECT '<BDNA>tableEncryption<BDNA>'||TABLESPACE_NAME||'<BDNA>'||ENCRYPTED||'<BDNA>' "+
                                @"FROM DBA_TABLESPACES WHERE ENCRYPTED='YES'",
            ver11_pattern,
            new QueryResultHandler(tablespaceEncryptionValueHandler)))
        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();
    }
}

