#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alex Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.9 $
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
    public class WindowsOracleInstanceLMSOptions3StaticScript  : ICollectionScriptRuntime {

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
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions3StaticScript.",
                                  m_taskId);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceLMSOptions3StaticScript is null.",
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
                                                          "Task Id {0}: Exception with batch return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
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
                        this.processLabelSecurityCollectedData();
                        if (!ver8_pattern.IsMatch(m_strVersion)) {
                            this.processDatabaseVaultCollectedData();
                            this.processAuditVaultCollectedData();
                        }
                        foreach (KeyValuePair<string, string> kvp in m_collectedData) {
                            this.BuildDataRow(kvp.Key, kvp.Value);
                        }
                    }
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions3StaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions3StaticScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions3StaticScript.  Elapsed time {1}.  Result code {2}.",
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
        /// Process Label Security Collected Data
        /// </summary>
        private void processLabelSecurityCollectedData() {
            StringBuilder lbl_security_query = new StringBuilder();
            lbl_security_query.AppendLine("CHECKING TO SEE IF LABEL SECURITY IS INSTALLED...").AppendLine();
            lbl_security_query.Append("SQL> SELECT 'ORACLE LABEL SECURITY INSTALLED: '||VALUE \"LABEL SECURITY\" FROM V$OPTION WHERE ")
                              .Append("PARAMETER ='Oracle Label Security';\n\n");

            if (CollectedData.ContainsKey(@"lbl_security_installed")) {
                if (!ora_error_pattern.IsMatch(CollectedData[@"lbl_security_installed"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"lbl_security_installed"])) {

                    lbl_security_query.Append("LABEL SECURITY\n")
                                      .Append("-------------------------------------\n");
                } 
                lbl_security_query.Append(CollectedData[@"lbl_security_installed"]).AppendLine();
                if (CollectedData[@"lbl_security_installed"] == @"TRUE") {
                    CollectedData[@"lbl_security_installed"] = @"1";
                    CollectedData[@"b_lbl_security_installed"] = @"1";
                } else {
                    CollectedData[@"lbl_security_installed"] = @"0";
                }
            } else {
                CollectedData[@"lbl_security_installed"] = @"0";
            }
            lbl_security_query.AppendLine().AppendLine();

            CollectedData[@"lbl_security_used"] = @"0";
            lbl_security_query.AppendLine("CHECKING TO SEE IF LABEL SECURITY OPTIONS IS BEING USED...").AppendLine();
            lbl_security_query.Append("SQL> SELECT COUNT(*) \"Count\" FROM LBACSYS.LBAC$POLT WHERE OWNER <> 'SA_DEMO';\n\n");
            if (CollectedData.ContainsKey(@"lbl_security_pol_count")) {
                if (!ora_error_pattern.IsMatch(CollectedData[@"lbl_security_pol_count"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"lbl_security_pol_count"])) {

                    lbl_security_query.Append("Count\n")
                                      .Append("-------------------------------------\n");
                    lbl_security_query.Append(CollectedData[@"lbl_security_pol_count"]).AppendLine();
                    if (CollectedData[@"lbl_security_pol_count"] != @"0") {
                        CollectedData[@"lbl_security_used"] = @"1";
                        CollectedData[@"b_lbl_security_used"] = @"1";
                    }
                } else {
                    lbl_security_query.Append(CollectedData[@"lbl_security_pol_count"]).AppendLine();
                    CollectedData[@"lbl_security_pol_count"] = @"0";
                }
            }
            lbl_security_query.AppendLine().AppendLine();
            CollectedData[@"lbl_security_query"] = lbl_security_query.ToString();
        }

        /// <summary>
        /// Process Database Vault Collected Data
        /// </summary>
        private void processDatabaseVaultCollectedData() {
            StringBuilder dbVaultQuery = new StringBuilder();
            bool dvSys = false, dvf = false;
            CollectedData[@"dbVaultInstalled"] = @"0";
            CollectedData[@"dbVaultUsed"] = @"0";
            dbVaultQuery.AppendLine("CHECKING IF DATABASE VAULT SCHEMAS ARE INSTALLED..").AppendLine();

            dbVaultQuery.Append("SQL> SELECT DECODE(UPPER(MAX(USERNAME)), 'DVSYS', 'Database Vault Schema DVSYS exist', ")
                        .Append("'Database Vault schema DVSYS does not exist') \"DVSYS\" FROM DBA_USERS WHERE UPPER(USERNAME)='DVSYS';\n\n");
            if (CollectedData.ContainsKey(@"dvSys")) {
                if (!ora_error_pattern.IsMatch(CollectedData[@"dvSys"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"dvSys"])) {
                    dbVaultQuery.Append("DVSYS\n")
                                .Append("-------------------------------------\n");
                    if (CollectedData[@"dvSys"] == @"Database Vault Schema DVSYS exist") {
                        dvSys = true;
                    }
                }
                dbVaultQuery.Append(CollectedData[@"dvSys"]).AppendLine();
            }
            dbVaultQuery.AppendLine();
            dbVaultQuery.Append("SQL> SELECT DECODE(UPPER(MAX(USERNAME)), 'DVF', 'Database Vault Schema DVF exist', ")
                        .Append("'Database Vault schema DVF does not exist') \"DVF\" FROM DBA_USERS WHERE UPPER(USERNAME)='DVF';\n\n");

            if (CollectedData.ContainsKey(@"dvf")) {
                if (!ora_error_pattern.IsMatch(CollectedData[@"dvf"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"dvf"])) {

                    dbVaultQuery.Append("DVF\n")
                                .Append("-------------------------------------\n");
                    if (CollectedData[@"dvf"] == @"Database Vault Schema DVF exist") {
                        dvf = true;
                    }
                }
                dbVaultQuery.Append(CollectedData[@"dvf"]).AppendLine();
            }
            dbVaultQuery.AppendLine().AppendLine();

            if (dvSys && dvf) {
                CollectedData[@"dbVaultInstalled"] = @"1";
                CollectedData[@"b_dbVaultInstalled"] = @"1";
                dbVaultQuery.AppendLine("CHECKING IF DATABASE VAULT REALMS CREATED..").AppendLine();
                dbVaultQuery.Append("SQL> SELECT DECODE(COUNT(*), 0, 'No realms were created', count(*)||' Realms were created')")
                            .Append("\"DBA_DV_REALM\" FROM DVSYS.DBA_DV_REALM;\n\n");
                if (CollectedData.ContainsKey(@"dvRealm")) {
                    if (!ora_error_pattern.IsMatch(CollectedData[@"dvRealm"]) &&
                        !no_row_selected_pattern.IsMatch(CollectedData[@"dvRealm"])) {

                        dbVaultQuery.Append("DBA_DV_REALM\n")
                                    .Append("-------------------------------------\n");
                        if (CollectedData[@"dvRealm"] != @"No realms were created") {
                            CollectedData[@"dbVaultUsed"] = @"1";
                            CollectedData[@"b_dbVaultUsed"] = @"1";
                        }
                    }
                    dbVaultQuery.Append(CollectedData[@"dvRealm"]).AppendLine();
                }
            }
            dbVaultQuery.AppendLine().AppendLine();
            CollectedData[@"dbVaultQuery"] = dbVaultQuery.ToString();
        }

        /// <summary>
        /// Process Database Vault Collected Data
        /// </summary>
        private void processAuditVaultCollectedData()  {
            StringBuilder auditVaultQuery = new StringBuilder();
            auditVaultQuery.AppendLine("CHECKING TO SEE IF AUDIT VAULT SCHEMAS ARE INSTALLED/USED..\n\n")
                           .Append("SQL> SELECT USERNAME FROM DBA_USERS WHERE UPPER(USERNAME)='AVSYS';\n\n");

            CollectedData[@"auditVaultUsed"] = @"0";
            CollectedData[@"auditVaultInstalled"] = @"0";

            if (CollectedData.ContainsKey(@"auditVault")) {
                if (!ora_error_pattern.IsMatch(CollectedData[@"auditVault"]) &&
                    !no_row_selected_pattern.IsMatch(CollectedData[@"auditVault"])) {
                    auditVaultQuery.Append("USERNAME\n")
                                   .Append("-------------------------------------\n");

                    CollectedData[@"auditVaultUsed"] = @"1";
                    CollectedData[@"b_auditVaultUsed"] = @"1";
                    CollectedData[@"auditVaultInstalled"] = @"1";
                    CollectedData[@"b_auditVaultInstalled"] = @"1";

                }
                auditVaultQuery.Append(CollectedData[@"auditVault"]).AppendLine(); ;
            } 
            auditVaultQuery.AppendLine().AppendLine();
            CollectedData[@"auditVaultQuery"] = auditVaultQuery.ToString();
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
                    strBatchFile.Append("ECHO PROMPT " + strName + @"_BEGIN___;")
                                .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    strBatchFile.Append("ECHO ");
                    strBatchFile.Append(strQuery.Trim().Replace("<", "^<")
                        .Replace(">", "^>").Replace("|", @"^|"));
                    if (!strQuery.EndsWith(";")) {
                        strBatchFile.Append(";");
                    }
                    strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    strBatchFile.Append("ECHO PROMPT " + @"___" + strName + @"_END;")
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
            string endStr = @"___" + attributeName + @"_END";
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
            (WindowsOracleInstanceLMSOptions3StaticScript scriptInstance, String attributeName, String queryOutput) {

            string value = null;
            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);

            //
            // Never compile a regular expression is not assigned to
            // a static reference.  Otherwise you will leak an Assembly.
            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$");

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
        private delegate void QueryResultHandler(WindowsOracleInstanceLMSOptions3StaticScript scriptInstance, string attributeName, string outputData);

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
        static WindowsOracleInstanceLMSOptions3StaticScript() {
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

        /// <summary>
        /// This table pairs up known attribute names with the query string needed to get the correct value from an Oracle
        /// database.  This table exists merely to seed the attribute map which will be used by the task execution code.
        /// </summary>
        private static readonly KeyValuePair<string, QueryTableEntry>[] s_queryTable = {
            new KeyValuePair<string, QueryTableEntry>(@"lbl_security_installed",
            new QueryTableEntry(@"select '<BDNA>lbl_security_installed<BDNA>'||value||'<BDNA>' FROM V$OPTION WHERE PARAMETER='Oracle Label Security';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"lbl_security_pol_count",
            new QueryTableEntry(@"SELECT '<BDNA>lbl_security_pol_count<BDNA>'||COUNT(*)||'<BDNA>' FROM LBACSYS.LBAC$POLT;",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"auditVault",
            new QueryTableEntry(@"select '<BDNA>auditVault<BDNA>'||USERNAME||'<BDNA>' FROM DBA_USERS WHERE UPPER(USERNAME)='AVSYS';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"dvSys",
            new QueryTableEntry(@"select '<BDNA>dvSys<BDNA>'||DECODE(UPPER(MAX(USERNAME)), 'DVSYS', 'Database Vault Schema DVSYS exist', "+
                                @"'Database Vault schema DVSYS does not exist')||'<BDNA>' FROM DBA_USERS WHERE UPPER(USERNAME)='DVSYS';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"dvf",
            new QueryTableEntry(@"select '<BDNA>dvf<BDNA>'||DECODE(UPPER(MAX(USERNAME)), 'DVF', 'Database Vault Schema DVF exist', "+
                                @" 'Database Vault schema DVF does not exist')||'<BDNA>' FROM DBA_USERS WHERE UPPER(USERNAME)='DVF';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"dvRealm",
            new QueryTableEntry(@"select '<BDNA>dvRealm<BDNA>'||DECODE(COUNT(*), 0, 'No realms were created', count(*)||" +
                                @"' Realms were created')||'<BDNA>' FROM DVSYS.DBA_DV_REALM;",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler)))
        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();
    }
}

