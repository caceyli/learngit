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
    public class WindowsOracleInstanceLMSOptions5StaticScript : ICollectionScriptRuntime {
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
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions5StaticScript.",
                                  m_taskId);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceLMSOptions5StaticScript is null.",
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
                                    //} else if (!rp.Stdout.ToString().Contains(@"BDNA")) {
                                    //    Lib.Logger.TraceEvent(TraceEventType.Error,
                                    //                          0,
                                    //                          "Task Id {0}: SQLPLUS exception, no proper data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                    //                          m_taskId,
                                    //                          rp.Stdout.ToString());
                                    //    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
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
                        this.processOEMCollectedData();
                        this.processOEMPACKCollectedData();
                        this.processOEMACCESSCollectedData();
                        foreach (KeyValuePair<string, string> kvp in m_collectedData) {
                            this.BuildDataRow(kvp.Key, kvp.Value);
                        }
                        //Console.WriteLine(CollectedData[@"ConfigPkInstalled"]);
                        //Console.WriteLine(CollectedData[@"ConfigPkUsed"]);
                        //Console.WriteLine(CollectedData[@"DiagPkInstalled"]);
                        //Console.WriteLine(CollectedData[@"DiagPkUsed"]);
                        //Console.WriteLine(CollectedData[@"tuningPackInstalled"]);
                        //Console.WriteLine(CollectedData[@"tuningPackUsed"]);
                        //Console.WriteLine(CollectedData[@"oemInstalled"]);
                        //Console.WriteLine(CollectedData[@"oemUsed"]);                        
                    }
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions5StaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions5StaticScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions5StaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Process OEM PACK collected Result.
        /// </summary>
        private void processOEMPACKCollectedData() {
            StringBuilder lmsOEMQuery = new StringBuilder();
            if (ver1011_pattern.IsMatch(m_strVersion)) {
                lmsOEMQuery.Append("CHECK TO SEE IF OEM PROGRAMS ARE RUNNING DURING THE MEASUREMENT PERIOD...\n\n");
                if (CollectedData.ContainsKey(@"OEMOWNER")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"OEMOWNER"])) {
                        lmsOEMQuery.Append("SQL> col OEMOWNER new_val OEMOWNER format a30 wrap").AppendLine();
                        lmsOEMQuery.AppendLine("select 'OEM REPOSITORY SCHEMA:' C_, owner as OEMOWNER from dba_tables where table_name = 'MGMT_ADMIN_LICENSES';");
                        lmsOEMQuery.Append("C_                           OEMOWNER                           \n");
                        lmsOEMQuery.Append("---------------------------- -----------------------------------\n");
                        lmsOEMQuery.Append("OEM REPOSITORY SCHEMA:       ");
                        lmsOEMQuery.AppendLine(CollectedData[@"OEMOWNER"]).AppendLine();
                    }
                }

                if (CollectedData.ContainsKey(@"OEMPACK")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"OEMPACK"])) {
                        lmsOEMQuery.Append("SQL> select a.pack_display_label as OEM_PACK, \n" +
                                        "       decode(b.pack_name, null, 'NO', 'YES') as PACK_ACCESS_GRANTED, \n" +
                                        "       PACK_ACCESS_AGREED \n" +
                                        "  from SYSMAN.MGMT_LICENSE_DEFINITIONS a, \n" +
                                        "       SYSMAN.MGMT_ADMIN_LICENSES      b, \n" +
                                        "      (select decode(count(*), 0, 'NO', 'YES') as PACK_ACCESS_AGREED \n" +
                                        "       from .MGMT_LICENSES where upper(I_AGREE)='YES') c \n" +
                                        "  where a.pack_label = b.pack_name   (+) \n" +
                                        "  / \n\n");

                        lmsOEMQuery.Append("OEM_PACK").Append("                          ")
                                   .Append("PACK_ACCESS_GRANTED").Append("                       ")
                                   .AppendLine("PACK_ACESS_AGREED");
                        lmsOEMQuery.Append("-----------------------------------------")
                                   .Append("---------------------------------------")
                                   .AppendLine("----------------------------");
                        if (no_row_selected_pattern.IsMatch(CollectedData[@"OEMPACK"])) {
                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"OEMPACK"], no_row_selected_pattern));
                        } else if (ora_error_pattern.IsMatch(CollectedData[@"OEMPACK"])) {
                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"OEMPACK"], ora_error_pattern));
                        } else {
                            CollectedData[@"oemInstalled"] = @"0";
                            CollectedData[@"oemUsed"] = @"0";
                            CollectedData[@"tuningPackInstalled"] = @"0";
                            CollectedData[@"tuningPackUsed"] = @"0";
                            CollectedData[@"DiagPkInstalled"] = @"0";
                            CollectedData[@"DiagPkUsed"] = @"0";
                            CollectedData[@"ConfigPkInstalled"] = @"0";
                            CollectedData[@"ConfigPkUsed"] = @"0";
                            foreach (String line in CollectedData[@"OEMPACK"].Split(new string[] { "<BDNA,>" }, StringSplitOptions.RemoveEmptyEntries)) {
                                string option = string.Empty;
                                foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                                    if (s_pattern.IsMatch(field)) {
                                        Match match = s_pattern.Match(field);
                                        string strName = match.Groups["name"].ToString();
                                        string strValue = match.Groups["value"].ToString();

                                        if (strName == @"OEM_PACK") {
                                            option = strValue;
                                            lmsOEMQuery.Append(strValue).Append("                          ");
                                        } else if (strName == @"ACCESS_GRANTED") {
                                            if (option == @"Database Tuning Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"tuningPackInstalled"] = @"1";
                                                    CollectedData[@"b_tuningPackInstalled"] = @"1";
                                                    //CollectedData[@"oemInstalled"] = @"1";
                                                    //CollectedData[@"b_oemInstalled"] = @"1";
                                                }
                                            } else if (option == @"Tuning Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"tuningPackInstalled"] = @"1";
                                                    CollectedData[@"b_tuningPackInstalled"] = @"1";
                                                    //CollectedData[@"oemInstalled"] = @"1";
                                                    //CollectedData[@"b_oemInstalled"] = @"1";
                                                }
                                            } else if (option == @"Database Diagnostics Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"DiagPkInstalled"] = @"1";
                                                    CollectedData[@"b_DiagPkInstalled"] = @"1";
                                                }
                                            } else if (option == @"Diagnostics Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"DiagPkInstalled"] = @"1";
                                                    CollectedData[@"b_DiagPkInstalled"] = @"1";
                                                }
                                            } else if (option.Contains("Database Configuration Pack")) {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkInstalled"] = @"1";
                                                    CollectedData[@"b_ConfigPkInstalled"] = @"1";
                                                }
                                            } else if (option.Contains("Configuration Management Pack")) {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkInstalled"] = @"1";
                                                    CollectedData[@"b_ConfigPkInstalled"] = @"1";
                                                }
                                            } else if (option.Contains("Database Configuration Management Pack")) {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkInstalled"] = @"1";
                                                    CollectedData[@"b_ConfigPkInstalled"] = @"1";
                                                }
                                            } else if (option.Contains("Configuration Pack")) {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkInstalled"] = @"1";
                                                    CollectedData[@"b_ConfigPkInstalled"] = @"1";
                                                }
                                            } else if (option.Contains("Change Management Pack")) {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ChgPkInstalled"] = @"1";
                                                    CollectedData[@"b_ChgPkInstalled"] = @"1";
                                                }
                                            }
                                            lmsOEMQuery.Append(strValue).Append("                          ");
                                        } else if (strName == @"ACCESS_AGREED") {
                                            if (option == @"Database Tuning Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"tuningPackUsed"] = @"1";
                                                    CollectedData[@"b_tuningPackUsed"] = @"1";
                                                }
                                            } else if (option == @"Tuning Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"DiagPkUsed"] = @"1";
                                                    CollectedData[@"b_DiagPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Database Diagnostic Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"DiagPkUsed"] = @"1";
                                                    CollectedData[@"b_DiagPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Diagnostic Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"DiagPkUsed"] = @"1";
                                                    CollectedData[@"b_DiagPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Database Configuration Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"DiagPkUsed"] = @"1";
                                                    CollectedData[@"b_DiagPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Configuration Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkUsed"] = @"1";
                                                    CollectedData[@"b_ConfigPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Configuration Management Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkUsed"] = @"1";
                                                    CollectedData[@"b_ConfigPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Database Configuration Management Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ConfigPkUsed"] = @"1";
                                                    CollectedData[@"b_ConfigPkUsed"] = @"1";
                                                }
                                            } else if (option == @"Change Management Pack") {
                                                if (strValue == "YES") {
                                                    CollectedData[@"ChgPkUsed"] = @"1";
                                                    CollectedData[@"b_ChgPkUsed"] = @"1";
                                                }
                                            }
                                            lmsOEMQuery.AppendLine(strValue);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    lmsOEMQuery.AppendLine().AppendLine();
                }
                if (CollectedData.ContainsKey(@"PACKAGG")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"PACKAGG"])) {
                        lmsOEMQuery.Append("OEM PACK ACCESS AGREEMENTS (10g or higher)").AppendLine().AppendLine();
                        lmsOEMQuery.Append("SQL> col I_AGREE format a10 wrap ").AppendLine();
                        lmsOEMQuery.Append("select USERNAME, TIMESTAMP, I_AGREE from SYSMAN.MGMT_LICENSES;").AppendLine().AppendLine();

                        lmsOEMQuery.Append("USERNAME").Append("                          ")
                                   .Append("TIMESTAMP").Append("                       ")
                                   .AppendLine("AGREED");
                        lmsOEMQuery.Append("-----------------------------------------")
                                   .Append("---------------------------------------")
                                   .AppendLine("----------------------------");
                        if (no_row_selected_pattern.IsMatch(CollectedData[@"PACKAGG"])) {
                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"PACKAGG"], no_row_selected_pattern));
                        } else if (ora_error_pattern.IsMatch(CollectedData[@"PACKAGG"])) {
                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"PACKAGG"], ora_error_pattern));
                        } else {
                            foreach (String line in CollectedData[@"PACKAGG"].Split(new string[] { "<BDNA,>" }, StringSplitOptions.RemoveEmptyEntries)) {
                                foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                                    if (s_pattern.IsMatch(field)) {
                                        Match match = s_pattern.Match(field);
                                        string strName = match.Groups["name"].ToString();
                                        string strValue = match.Groups["value"].ToString();

                                        if (strName == @"USERNAME") {
                                            lmsOEMQuery.Append(strValue).Append("                          ");
                                        } else if (strName == @"TIMESTAMP") {
                                            lmsOEMQuery.Append(strValue).Append("                          ");
                                        } else if (strName == @"AGREED") {
                                            lmsOEMQuery.Append(strValue);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    lmsOEMQuery.AppendLine().AppendLine();
                }


                if (CollectedData.ContainsKey(@"PACKAD")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"PACKAD"])) {
                        lmsOEMQuery.Append("OEM MANAGED DATABASES (10g or higher)").AppendLine().AppendLine();
                        lmsOEMQuery.Append("SQL> col I_AGREE format a10 wrap ").AppendLine();
                        lmsOEMQuery.Append("select TARGET_NAME, HOST_NAME, LOAD_TIMESTAMP from SYSMAN.MGMT_TARGETS where TARGET_TYPE = 'oracle_database'\n\n");

                        lmsOEMQuery.Append("OEM_PACK").Append("                          ")
                                   .Append("PACK_ACCESS_GRANTED").Append("                       ")
                                   .AppendLine("PACK_ACESS_AGREED");
                        lmsOEMQuery.Append("-----------------------------------------")
                                   .Append("---------------------------------------")
                                   .AppendLine("----------------------------");
                        if (no_row_selected_pattern.IsMatch(CollectedData[@"PACKAD"])) {
                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"PACKAD"], no_row_selected_pattern));
                        } else if (ora_error_pattern.IsMatch(CollectedData[@"PACKAD"])) {
                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"PACKAD"], ora_error_pattern));
                        } else {
                            foreach (String line in CollectedData[@"PACKAGG"].Split(new string[] { "<BDNA,>" }, StringSplitOptions.RemoveEmptyEntries)) {

                                foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.RemoveEmptyEntries)) {

                                    if (s_pattern.IsMatch(field)) {

                                        Match match = s_pattern.Match(field);

                                        string strName = match.Groups["name"].ToString();

                                        string strValue = match.Groups["value"].ToString();



                                        if (strName == @"OEM_PACK") {

                                            lmsOEMQuery.Append(strValue).Append("                          ");

                                        } else if (strName == @"PACK_ACCESS_GRANTED") {

                                            lmsOEMQuery.Append(strValue).Append("                          ");

                                        } else if (strName == @"PACK_ACESS_AGREED") {

                                            lmsOEMQuery.Append(strValue);

                                        }

                                    }

                                }

                            }

                        }

                    }

                    lmsOEMQuery.AppendLine().AppendLine();

                }





                if (CollectedData.ContainsKey(@"tuningPackUsed")) {

                    CollectedData[@"tuningPackQuery"] = lmsOEMQuery.ToString();

                }

                if (CollectedData.ContainsKey(@"DiagPkUsed")) {

                    CollectedData[@"DiagPkQuery"] = lmsOEMQuery.ToString();

                }

                if (CollectedData.ContainsKey(@"ConfigPkUsed")) {

                    CollectedData[@"ConfigPkQuery"] = lmsOEMQuery.ToString();

                }

                CollectedData[@"OEMQuery"] = lmsOEMQuery.ToString();

            }

        }



        /// <summary>

        /// Process OEM ACCESS collected Result.

        /// </summary>

        private void processOEMACCESSCollectedData() {

            StringBuilder lmsOEMQuery = new StringBuilder();

            if (ver11_pattern.IsMatch(m_strVersion)) {

                bool oemInstalled = false, oemUsed = false;

                lmsOEMQuery.Append("CHECK TO SEE IF OEM PROGRAMS ARE RUNNING DURING THE MEASUREMENT PERIOD...\n\n")

                           .Append("SQL> SELECT NAME, VALUE, ISDEFAULT FROM V$PARAMETER WHERE UPPER(NAME) LIKE '%CONTROL_MANAGEMENT_PACK_ACCESS%';");



                if (CollectedData.ContainsKey(@"ACCESS")) {

                    if (!string.IsNullOrEmpty(CollectedData[@"ACCESS"])) {

                        lmsOEMQuery.Append("NAME").Append("                          ")

                                   .Append("VALUE").Append("                       ")

                                   .AppendLine("ISDEFAULT");

                        lmsOEMQuery.Append("-----------------------------------------")

                                   .Append("---------------------------------------")

                                   .AppendLine("----------------------------");

                        if (no_row_selected_pattern.IsMatch(CollectedData[@"ACCESS"])) {

                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"ACCESS"], no_row_selected_pattern));

                        } else if (ora_error_pattern.IsMatch(CollectedData[@"ACCESS"])) {

                            lmsOEMQuery.Append(matchFirstGroup(CollectedData[@"ACCESS"], ora_error_pattern));

                        } else {

                            foreach (String line in CollectedData[@"ACCESS"].Split(new string[] { "<BDNA,>" }, StringSplitOptions.RemoveEmptyEntries)) {

                                oemUsed = oemInstalled = true;

                                string name = null;

                                string packName = null;



                                foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.None)) {

                                    if (s_pattern.IsMatch(field)) {

                                        Match match = s_pattern.Match(field);

                                        string strName = match.Groups["name"].ToString();

                                        string strValue = match.Groups["value"].ToString();



                                        if (strName == @"NAME") {

                                            name = strValue;

                                            lmsOEMQuery.Append(strValue).Append("                          ");

                                        }

                                        if (strName == @"VALUE") {

                                            packName = strValue;

                                            lmsOEMQuery.Append(strValue).Append("                          ");

                                        }

                                        if (strName == @"ISDEFAULT") {

                                            if (strValue == @"TRUE") {

                                                if (packName.Contains("DIAGNOSTIC")) {

                                                    CollectedData[@"DiagPkUsed"] = @"1";

                                                    CollectedData[@"b_DiagPkUsed"] = @"1";

                                                }

                                                if (packName.Contains("TUNING")) {

                                                    CollectedData[@"tuningPackUsed"] = @"1";

                                                    CollectedData[@"b_tuningPackUsed"] = @"1";

                                                }

                                            }

                                            lmsOEMQuery.Append(strValue);

                                        }

                                        lmsOEMQuery.Append(field).Append("                          ");

                                    }

                                }

                            }

                        }

                    }

                }



                if (CollectedData.ContainsKey(@"oemUsed")) {

                    if (oemUsed && CollectedData[@"oemUsed"] == @"0") {

                        CollectedData[@"oemUsed"] = @"1";

                        CollectedData[@"b_oemUsed"] = @"1";

                    }

                } else {

                    if (oemUsed) {

                        CollectedData[@"oemUsed"] = @"1";

                        CollectedData[@"b_oemUsed"] = @"1";

                    } else {

                        CollectedData[@"oemUsed"] = @"0";

                    }

                }

                if (CollectedData.ContainsKey(@"oemInstalled")) {

                    if (oemUsed && CollectedData[@"oemInstalled"] == @"0") {

                        CollectedData[@"oemInstalled"] = @"1";

                        CollectedData[@"b_oemInstalled"] = @"1";

                    }

                } else {

                    if (oemInstalled) {

                        CollectedData[@"oemInstalled"] = @"1";

                        CollectedData[@"b_oemInstalled"] = @"1";

                    } else {

                        CollectedData[@"oemInstalled"] = @"0";

                    }

                }

                if (CollectedData.ContainsKey(@"OEMQuery")) {

                    CollectedData[@"OEMQuery"] += lmsOEMQuery.ToString();

                } else {

                    CollectedData[@"OEMQuery"] = lmsOEMQuery.ToString();

                }

            }

        }





        /// <summary>

        /// Process OEM collected Result.

        /// </summary>

        private void processOEMCollectedData() {

            StringBuilder lmsOEMQuery = new StringBuilder();

            if (ver89_pattern.IsMatch(m_strVersion)) {

                bool oemInstalled = false, oemUsed = false;

                lmsOEMQuery.Append("CHECK TO SEE IF OEM PROGRAMS ARE RUNNING DURING THE MEASUREMENT PERIOD...\n\n");

                lmsOEMQuery.Append("SQL> SELECT DISTINCT '<BDNA>'||PROGRAM||'<BDNA>' FROM V$SESSION \n" +

                                    " WHERE UPPER(PROGRAM) LIKE '%XPNI.EXE%' \n" +

                                    " OR UPPER(PROGRAM) LIKE '%VMS.EXE%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%EPC.EXE%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%TDVAPP.EXE%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%VDOSSHELL%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%VMQ%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%VTUSHELL%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%JAVAVMQ%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%XPAUTUNE%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%XPCOIN%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%XPKSH%'\n" +

                                    " OR UPPER(PROGRAM) LIKE '%XPUI%'\n");



                if (CollectedData.ContainsKey(@"PROGRAM")) {

                    if (!string.IsNullOrEmpty(CollectedData[@"PROGRAM"])) {

                        if (!ora_error_pattern.IsMatch(CollectedData[@"PROGRAM"]) &&

                            !no_row_selected_pattern.IsMatch(CollectedData[@"PROGRAM"])) {

                            foreach (string line in CollectedData[@"PROGRAM"].Split(new string[] { "<BDNA,1>" }, StringSplitOptions.RemoveEmptyEntries)) {

                                oemInstalled = oemUsed = true;

                                lmsOEMQuery.AppendLine(line);

                            }

                        } else {

                            lmsOEMQuery.AppendLine(CollectedData[@"PROGRAM"]);

                        }

                    }

                }



                lmsOEMQuery.AppendLine();

                lmsOEMQuery.AppendLine("CHECKING FOR OEM REPOSITORIES");

                lmsOEMQuery.AppendLine("SQL> ");

                lmsOEMQuery.AppendLine("DECLARE  \n" +

                                 "cursor1 integer; \n" +

                                 "v_count number(1); \n" +

                                 "v_schema dba_tables.owner%TYPE; \n" +

                                 "v_version varchar2(10); \n" +

                                 "v_component varchar2(20); \n" +

                                 "v_i_name varchar2(10); \n" +

                                 "v_h_name varchar2(30); \n" +

                                 "stmt varchar2(200); \n" +

                                 "rows_processed integer; \n" +

                                 " \n" +

                                 "CURSOR schema_array IS \n" +

                                 "SELECT owner  \n" +

                                 "FROM dba_tables WHERE table_name = 'SMP_REP_VERSION'; \n" +

                                 " \n" +

                                 "CURSOR schema_array_v2 IS \n" +

                                 "SELECT owner  \n" +

                                 "FROM dba_tables WHERE table_name = 'SMP_VDS_REPOS_VERSION'; \n" +

                                 " \n" +

                                 "BEGIN \n" +

                                 "DBMS_OUTPUT.PUT_LINE ('.'); \n" +

                                 "DBMS_OUTPUT.PUT_LINE ('OEM REPOSITORY LOCATIONS'); \n" +

                                 " \n" +

                                 "select instance_name,host_name into v_i_name, v_h_name from \n" +

                                 "v$instance; \n" +

                                 "DBMS_OUTPUT.PUT_LINE ('Instance: '||v_i_name||' on host: '||v_h_name); \n" +

                                 " \n" +

                                 "OPEN schema_array; \n" +

                                 "OPEN schema_array_v2; \n" +

                                 " \n" +

                                 "cursor1:=dbms_sql.open_cursor; \n" +

                                 " \n" +

                                 "v_count := 0; \n" +

                                 " \n" +

                                 "LOOP -- this loop steps through each valid schema. \n" +

                                 "FETCH schema_array INTO v_schema; \n" +

                                 "EXIT WHEN schema_array%notfound; \n" +

                                 "v_count := v_count + 1; \n" +

                                 "dbms_sql.parse(cursor1,'select c_current_version, c_component from \n" +

                                 "'||v_schema||'.smp_rep_version', dbms_sql.native); \n" +

                                 "dbms_sql.define_column(cursor1, 1, v_version, 10); \n" +

                                 "dbms_sql.define_column(cursor1, 2, v_component, 20); \n" +

                                 " \n" +

                                 "rows_processed:=dbms_sql.execute ( cursor1 ); \n" +

                                 " \n" +

                                 "loop -- to step through cursor1 to find console version. \n" +

                                 "if dbms_sql.fetch_rows(cursor1) >0 then \n" +

                                 "dbms_sql.column_value (cursor1, 1, v_version); \n" +

                                 "dbms_sql.column_value (cursor1, 2, v_component); \n" +

                                 "if v_component = 'CONSOLE' then \n" +

                                 "dbms_output.put_line ('Schema '||rpad(v_schema,15)||' has a repository \n" +

                                 "version '||v_version); \n" +

                                 "exit; \n" +

                                 " \n" +

                                 "end if; \n" +

                                 "else \n" +

                                 "exit; \n" +

                                 "end if; \n" +

                                 "end loop; \n" +

                                 " \n" +

                                 "END LOOP; \n" +

                                 " \n" +

                                 "LOOP -- this loop steps through each valid V2 schema. \n" +

                                 "FETCH schema_array_v2 INTO v_schema; \n" +

                                 "EXIT WHEN schema_array_v2%notfound; \n" +

                                 " \n" +

                                 "v_count := v_count + 1; \n" +

                                 "dbms_output.put_line ( 'Schema '||rpad(v_schema,15)||' has a repository \n" +

                                 "version 2.x' ); \n" +

                                 "end loop; \n" +

                                 " \n" +

                                 "dbms_sql.close_cursor (cursor1); \n" +

                                 "close schema_array; \n" +

                                 "close schema_array_v2; \n" +

                                 "if v_count = 0 then \n" +

                                 "dbms_output.put_line ( 'There are NO OEM repositories on this instance.'); \n" +

                                 "end if; \n" +

                                 "end;");

                Console.WriteLine("Query is: " + lmsOEMQuery.ToString());

                if (CollectedData.ContainsKey(@"OEM")) {

                    if (!string.IsNullOrEmpty(CollectedData[@"OEM"])) {

                        if (!ora_error_pattern.IsMatch(CollectedData[@"OEM"]) &&

                            !no_row_selected_pattern.IsMatch(CollectedData[@"OEM"])) {

                            oemInstalled = oemUsed = true;

                            lmsOEMQuery.AppendLine(CollectedData[@"OEM"]);

                        }

                    } else {

                        lmsOEMQuery.AppendLine(CollectedData[@"OEM"]);

                    }

                }



                if (oemUsed) {

                    CollectedData[@"oemUsed"] = @"1";

                    CollectedData[@"b_oemUsed"] = @"1";

                } else {

                    CollectedData[@"oemUsed"] = @"0";

                }

                if (oemInstalled) {

                    CollectedData[@"oemInstalled"] = @"1";

                    CollectedData[@"b_oemInstalled"] = @"1";

                } else {

                    CollectedData[@"oemInstalled"] = @"0";

                }

                CollectedData[@"OEMQuery"] = lmsOEMQuery.ToString();

            }

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

            strBatchFile.Append("ECHO SET col OEMOWNER new_val OEMOWNER format a30 wrap;")

                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            strBatchFile.Append("ECHO SET col I_AGREE format a10 wrap;")

                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            strBatchFile.Append("ECHO SET col I_AGREE format a10 wrap;")

                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");



            foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable) {

                string strQuery = entry.Value.QueryString;

                string strName = entry.Key;

                Regex regex = entry.Value.regex;

                if ((!string.IsNullOrEmpty(strQuery)) && (regex.IsMatch(m_strVersion))) {

                    strBatchFile.Append("ECHO PROMPT " + strName + @"_BEGIN___;")

                                .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

                    strBatchFile.Append("ECHO ");

                    strBatchFile.Append(strQuery.Trim()

                        .Replace("<", "^<").Replace(">", "^>").Replace("|", @"^|")

                        .Replace("&", "^&").Replace("%", "%%"));

                    strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

                    if (!strQuery.EndsWith(";")) {

                        strBatchFile.Append("ECHO ").Append("/ ")

                                    .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

                    }





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

        /// Parse query result for a running OEM program value.

        /// </summary>

        /// <param name="scriptInstance">script reference</param>

        /// <param name="attributeNames">attribute</param>

        /// <param name="queryOutput">Output</param>

        private static void ProgramValueHandler

            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {



            StringBuilder logData = new StringBuilder();

            string output = ExtractQueryOutput(attributeName, queryOutput);

            StringBuilder result = new StringBuilder();



            //

            // Never compile a regular expression is not assigned to

            // a static reference.  Otherwise you will leak an Assembly.

            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$");



            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {

                    string value = matchFirstGroup(line, r);

                    if (result.Length > 0) {

                        result.Append("<BDNA,1>");

                    }

                    result.Append(value);

                    logData.AppendFormat("{0}: {1}\n", attributeName, value);



                } else if (no_row_selected_pattern.IsMatch(line)) {

                    result.Append(matchFirstGroup(line, no_row_selected_pattern));

                    logData.AppendLine("No rows selected.");

                    break;

                } else if (ora_error_pattern.IsMatch(line)) {

                    result.Append(matchFirstGroup(line, ora_error_pattern));

                    logData.AppendLine("Oracle error..");

                }

            }

            Lib.Logger.TraceEvent(TraceEventType.Verbose,

                                  0,

                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",

                                  scriptInstance.m_taskId,

                                  attributeName,

                                  logData.ToString());

            if (result.Length > 0) {

                scriptInstance.SaveCollectedData(attributeName, result.ToString());

            }

        }





        /// <summary>

        /// Parse query result for a OEM value.

        /// </summary>

        /// <param name="scriptInstance">script reference</param>

        /// <param name="attributeNames">attribute</param>

        /// <param name="queryOutput">Output</param>

        private static void SingleValueHandler

            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {



            string value = null;

            StringBuilder logData = new StringBuilder();

            string output = ExtractQueryOutput(attributeName, queryOutput);



            //

            // Never compile a regular expression is not assigned to

            // a static reference.  Otherwise you will leak an Assembly.

            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$");



            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {

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

        /// Parse query result for a OEM value.

        /// </summary>

        /// <param name="scriptInstance">script reference</param>

        /// <param name="attributeNames">attribute</param>

        /// <param name="queryOutput">Output</param>

        private static void OEMPACKValueHandler

            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {



            StringBuilder logData = new StringBuilder();

            string output = ExtractQueryOutput(attributeName, queryOutput);

            StringBuilder result = new StringBuilder();



            //

            // Never compile a regular expression is not assigned to

            // a static reference.  Otherwise you will leak an Assembly.

            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {

                    Match match = r.Match(line);

                    if (match.Length > 1) {

                        String OEM_PACK = match.Groups[1].ToString();

                        String ACCESS_GRANTED = match.Groups[2].ToString();

                        String ACCESS_AGREED = match.Groups[3].ToString();



                        if (result.Length > 0) {

                            result.Append(@"<BDNA,>");

                        }

                        result.Append(@"OEM_PACK=").Append(OEM_PACK);

                        result.Append(@"<BDNA,1>ACCESS_GRANTED=").Append(ACCESS_GRANTED);

                        result.Append(@"<BDNA,1>ACCESS_AGREED=").Append(ACCESS_AGREED);

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

            Lib.Logger.TraceEvent(TraceEventType.Verbose,

                                  0,

                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",

                                  scriptInstance.m_taskId,

                                  attributeName,

                                  logData.ToString());

            if (result.Length > 0) {

                scriptInstance.SaveCollectedData(attributeName, result.ToString());

            }

        }



        /// <summary>

        /// Parse query result for a OEM value.

        /// </summary>

        /// <param name="scriptInstance">script reference</param>

        /// <param name="attributeNames">attribute</param>

        /// <param name="queryOutput">Output</param>

        private static void OEMPACKAGGValueHandler

            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {



            StringBuilder logData = new StringBuilder();

            string output = ExtractQueryOutput(attributeName, queryOutput);

            StringBuilder result = new StringBuilder();



            //

            // Never compile a regular expression is not assigned to

            // a static reference.  Otherwise you will leak an Assembly.

            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {

                    Match match = r.Match(line);

                    if (match.Length > 1) {

                        String USERNAME = match.Groups[1].ToString();

                        String TIMESTAMP = match.Groups[2].ToString();

                        String AGREED = match.Groups[3].ToString();



                        if (result.Length > 0) {

                            result.Append(@"<BDNA,>");

                        }

                        result.Append(@"USERNAME=").Append(USERNAME);

                        result.Append(@"<BDNA,1>TIMESTAMP=").Append(TIMESTAMP);

                        result.Append(@"<BDNA,1>AGREED=").Append(AGREED);

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

            Lib.Logger.TraceEvent(TraceEventType.Verbose,

                                  0,

                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",

                                  scriptInstance.m_taskId,

                                  attributeName,

                                  logData.ToString());

            if (result.Length > 0) {

                scriptInstance.SaveCollectedData(attributeName, result.ToString());

            }

        }



        /// <summary>

        /// Parse query result for a OEM value.

        /// </summary>

        /// <param name="scriptInstance">script reference</param>

        /// <param name="attributeNames">attribute</param>

        /// <param name="queryOutput">Output</param>

        private static void OEMPACKDBValueHandler

            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {



            StringBuilder logData = new StringBuilder();

            string output = ExtractQueryOutput(attributeName, queryOutput);

            StringBuilder result = new StringBuilder();



            //

            // Never compile a regular expression is not assigned to

            // a static reference.  Otherwise you will leak an Assembly.

            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {

                    Match match = r.Match(line);

                    if (match.Length > 1) {

                        String USERNAME = match.Groups[1].ToString();

                        String TIMESTAMP = match.Groups[2].ToString();

                        String AGREED = match.Groups[3].ToString();



                        if (result.Length > 0) {

                            result.Append(@"<BDNA,>");

                        }

                        result.Append(@"USERNAME=").Append(USERNAME);

                        result.Append(@"<BDNA,1>TIMESTAMP=").Append(TIMESTAMP);

                        result.Append(@"<BDNA,1>AGREED=").Append(AGREED);

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

            Lib.Logger.TraceEvent(TraceEventType.Verbose,

                                  0,

                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",

                                  scriptInstance.m_taskId,

                                  attributeName,

                                  logData.ToString());

            if (result.Length > 0) {

                scriptInstance.SaveCollectedData(attributeName, result.ToString());

            }

        }



        /// <summary>

        /// Parse query result for a OEM value.

        /// </summary>

        /// <param name="scriptInstance">script reference</param>

        /// <param name="attributeNames">attribute</param>

        /// <param name="queryOutput">Output</param>

        private static void OEMValueHandler

            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {



            StringBuilder logData = new StringBuilder();

            string output = ExtractQueryOutput(attributeName, queryOutput);

            StringBuilder result = new StringBuilder();



            //

            // Never compile a regular expression is not assigned to

            // a static reference.  Otherwise you will leak an Assembly.

            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$");



            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {

                if (no_row_selected_pattern.IsMatch(line)) {

                    result.Append(matchFirstGroup(line, no_row_selected_pattern));

                    logData.AppendLine("No rows selected.");

                    break;

                } else if (ora_error_pattern.IsMatch(line)) {

                    result.Append(matchFirstGroup(line, ora_error_pattern));

                    logData.AppendLine("Oracle error..");

                } else {

                    if (result.Length > 0) {

                        result.Append("<BDNA,1>");

                    }

                    result.Append(line);

                    logData.AppendFormat("{0}: {1}\n", attributeName, line);

                }

            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",
                                  scriptInstance.m_taskId,
                                  attributeName,
                                  logData.ToString());
            if (result.Length > 0) {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
           }
        }
        /// <summary>
        /// Parse query result for a OEM Access value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void OEMOWNERValueHandler
            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {

            string value = null;
            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);

            //
            // Never compile a regular expression is not assigned to
            // a static reference.  Otherwise you will leak an Assembly.
            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {
                if (r.IsMatch(line)) {
                    value = matchFirstGroup(line, r);
                    logData.AppendFormat("{0}: {1}\n", attributeName, value);
                    break;
                } else if (no_row_selected_pattern.IsMatch(line)) {
                    value = matchFirstGroup(line, no_row_selected_pattern);
                    logData.Append("No rows selected.");
                    break;
                } else if (ora_error_pattern.IsMatch(line)) {
                    value = matchFirstGroup(line, ora_error_pattern);
                    logData.Append("Oracle error..");
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
        /// Parse query result for a OEM Access value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void OEMACCESSValueHandler
            (WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, String attributeName, String queryOutput) {

            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);
            StringBuilder result = new StringBuilder();

            //
            // Never compile a regular expression is not assigned to
            // a static reference.  Otherwise you will leak an Assembly.
            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries)) {
                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String NAME = match.Groups[1].ToString();
                        String VALUE = match.Groups[2].ToString();
                        String ISDEFAULT = match.Groups[3].ToString();

                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(@"NAME=").Append(NAME);
                        result.Append(@"<BDNA,1>VALUE=").Append(VALUE);
                        result.Append(@"<BDNA,1>ISDEFAULT=").Append(ISDEFAULT);
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
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Parse query results for attribute {1}:\n{2}",
                                  scriptInstance.m_taskId,
                                  attributeName,
                                  logData.ToString());
            if (result.Length > 0) {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
            }
        }

        /// <summary>
        /// Signature for query result handlers.
        /// </summary>
        private delegate void QueryResultHandler(WindowsOracleInstanceLMSOptions5StaticScript scriptInstance, string attributeName, string outputData);

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

            /// <summary>
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
        static WindowsOracleInstanceLMSOptions5StaticScript() {
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
        private static Regex ver89_pattern = new Regex(@"^9\..*|^8\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver9_pattern = new Regex(@"^9\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ver1011_pattern = new Regex(@"^10\..*|^11\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
            new KeyValuePair<string, QueryTableEntry>(@"PROGRAM",
            new QueryTableEntry(@"SELECT DISTINCT '<BDNA>'||PROGRAM||'<BDNA>' FROM V$SESSION" +
                                " WHERE UPPER(PROGRAM) LIKE '%XPNI.EXE%'"+
                                " OR UPPER(PROGRAM) LIKE '%VMS.EXE%'"+
                                " OR UPPER(PROGRAM) LIKE '%EPC.EXE%'"+
                                " OR UPPER(PROGRAM) LIKE '%TDVAPP.EXE%'"+
                                " OR UPPER(PROGRAM) LIKE '%VDOSSHELL%'"+
                                " OR UPPER(PROGRAM) LIKE '%VMQ%'"+
                                " OR UPPER(PROGRAM) LIKE '%VTUSHELL%'"+
                                " OR UPPER(PROGRAM) LIKE '%JAVAVMQ%'"+
                                " OR UPPER(PROGRAM) LIKE '%XPAUTUNE%'"+
                                " OR UPPER(PROGRAM) LIKE '%XPCOIN%'"+
                                " OR UPPER(PROGRAM) LIKE '%XPKSH%'" +
                                " OR UPPER(PROGRAM) LIKE '%XPUI%';",
            ver89_pattern,
            new QueryResultHandler(ProgramValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"OEM",
            new QueryTableEntry("DECLARE  " +
                                 "cursor1 integer; " +
                                 "v_count number(1); " +
                                 "v_schema dba_tables.owner%TYPE; " +
                                 "v_version varchar2(10); " +
                                 "v_component varchar2(20); " +
                                 "v_i_name varchar2(10); " +
                                 "v_h_name varchar2(30); " +
                                 "stmt varchar2(200); " +
                                 "rows_processed integer; " +
                                 "CURSOR schema_array IS " +
                                 "SELECT owner  " +
                                 "FROM dba_tables WHERE table_name = 'SMP_REP_VERSION'; " +
                                 "CURSOR schema_array_v2 IS " +
                                 "SELECT owner  " +
                                 "FROM dba_tables WHERE table_name = 'SMP_VDS_REPOS_VERSION'; " +
                                 "BEGIN " +
                                 "DBMS_OUTPUT.PUT_LINE ('.'); " +
                                 "DBMS_OUTPUT.PUT_LINE ('OEM REPOSITORY LOCATIONS'); " +
                                 "select instance_name,host_name into v_i_name, v_h_name from " +
                                 "v$instance; " +
                                 "DBMS_OUTPUT.PUT_LINE ('Instance: '||v_i_name||' on host: '||v_h_name); " +
                                 "OPEN schema_array; " +
                                 "OPEN schema_array_v2; " +
                                 "cursor1:=dbms_sql.open_cursor; " +
                                 "v_count := 0; " +
                                 "LOOP -- this loop steps through each valid schema. " +
                                 "FETCH schema_array INTO v_schema; " +
                                 "EXIT WHEN schema_array%notfound; " +
                                 "v_count := v_count + 1; " +
                                 "dbms_sql.parse(cursor1,'select c_current_version, c_component from " +
                                 "'||v_schema||'.smp_rep_version', dbms_sql.native); " +
                                 "dbms_sql.define_column(cursor1, 1, v_version, 10); " +
                                 "dbms_sql.define_column(cursor1, 2, v_component, 20); " +
                                 "rows_processed:=dbms_sql.execute ( cursor1 ); " +
                                 "loop -- to step through cursor1 to find console version. " +
                                 "if dbms_sql.fetch_rows(cursor1) >0 then " +
                                 "dbms_sql.column_value (cursor1, 1, v_version); " +
                                 "dbms_sql.column_value (cursor1, 2, v_component); " +
                                 "if v_component = 'CONSOLE' then " +
                                 "dbms_output.put_line ('Schema '||rpad(v_schema,15)||' has a repository " +
                                 "version '||v_version); " +
                                 "exit; " +
                                 " " +
                                 "end if; " +
                                 "else " +
                                 "exit; " +
                                 "end if; " +
                                 "end loop; " +
                                 "END LOOP; " +
                                 "LOOP -- this loop steps through each valid V2 schema. " +
                                 "FETCH schema_array_v2 INTO v_schema; " +
                                 "EXIT WHEN schema_array_v2%notfound; " +
                                 "v_count := v_count + 1; " +
                                 "dbms_output.put_line ( 'Schema '||rpad(v_schema,15)||' has a repository " +
                                 "version 2.x' ); " +
                                 "end loop; " +
                                 "dbms_sql.close_cursor (cursor1); " +
                                 "close schema_array; " +
                                 "close schema_array_v2; " +
                                 "if v_count = 0 then " +
                                 "dbms_output.put_line ( 'There are NO OEM repositories on this instance.'); " +
                                 "end if; " +
                                 "end",
            ver9_pattern,
            new QueryResultHandler(OEMValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"OEMOWNER",
            new QueryTableEntry("select '<BDNA>OEMOWNER<BDNA>'||owner||'<BDNA>' from dba_tables where table_name = 'MGMT_ADMIN_LICENSES';",
            ver1011_pattern,
            new QueryResultHandler(SingleValueHandler))),

            new KeyValuePair<string, QueryTableEntry>(@"OEMPACK",
            new QueryTableEntry("select '<BDNA>OEMPACK<BDNA>'||a.pack_display_label||'<BDNA>'||decode(b.pack_name, null, 'NO', 'YES')||'<BDNA>'||PACK_ACCESS_AGREED||'<BDNA>'"+
                                "  from SYSMAN.MGMT_LICENSE_DEFINITIONS a,"+
                                "  SYSMAN.MGMT_ADMIN_LICENSES  b,"+
                                "  (select decode(count(*), 0, 'NO', 'YES') as PACK_ACCESS_AGREED from SYSMAN.MGMT_LICENSES where upper(I_AGREE)='YES') c"+
                                "  where a.pack_label = b.pack_name (+)",
            ver1011_pattern,
            new QueryResultHandler(OEMPACKValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"PACKAGG",
            new QueryTableEntry("select '<BDNA>PACKAGG<BDNA>'||USERNAME||'<BDNA>'||TIMESTAMP||'<BDNA>'||I_AGREE||'<BDNA>' from SYSMAN.MGMT_LICENSES;",
            ver1011_pattern,
            new QueryResultHandler(OEMPACKAGGValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"PACKDB",
            new QueryTableEntry("select '<BDNA>PACKDB<BDNA>'||TARGET_NAME||'<BDNA>'||HOST_NAME||'<BDNA>'||LOAD_TIMESTAMP||'<BDNA>' from SYSMAN.MGMT_TARGETS "+
                                "  where TARGET_TYPE = 'oracle_database';",
            ver1011_pattern,
            new QueryResultHandler(OEMPACKDBValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"ACCESS",
            new QueryTableEntry("SELECT '<BDNA>ACCESS<BDNA>'||NAME||'<BDNA>'||VALUE||'<BDNA>'||ISDEFAULT||'<BDNA>' "+
                                " FROM V$PARAMETER WHERE UPPER(NAME) LIKE '%CONTROL_MANAGEMENT_PACK_ACCESS%';",
            ver11_pattern,
            new QueryResultHandler(OEMACCESSValueHandler)))
        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();
    }
}



