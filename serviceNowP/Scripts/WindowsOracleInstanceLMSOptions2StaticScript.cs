#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alex Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.12 $
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
    public class WindowsOracleInstanceLMSOptions2StaticScript : ICollectionScriptRuntime {

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
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions2StaticScript.",
                                  m_taskId);

            try {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceLMSOptions2StaticScript is null.",
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
                        this.processRACCollectedData();
                        this.processSpatialCollectedData();
                        this.processDMCollectedData();
                        foreach(KeyValuePair<string, string> kvp in m_collectedData) {
                            this.BuildDataRow(kvp.Key, kvp.Value);
                        }
                    }
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions2StaticScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSOptions2StaticScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSOptions2StaticScript.  Elapsed time {1}.  Result code {2}.",
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
        /// Process RAC collected Result.
        /// </summary>
        private void processRACCollectedData() {
            StringBuilder lmsRACQuery = new StringBuilder();
            lmsRACQuery.AppendLine("CHECKING TO SEE IF RAC IS INSTALLED ..").AppendLine();
            lmsRACQuery.Append("SQL> SELECT 'ORACLE RAC INSTALLED: '||VALUE \"RAC(REAL APPLICATION CLUSTERS)\" FROM V$OPTION")
                       .Append("WHERE PARAMETER ='Real Application Clusters';\n\n");
            lmsRACQuery.Append("RAC(REAL APPLICATION CLUSTERS)\n")
                       .Append("-------------------------------------\n");

            if (CollectedData.ContainsKey(@"racInstalled")) {
                lmsRACQuery.Append(@"ORACLE RAC INSTALLED: ").Append(CollectedData[@"racInstalled"]).AppendLine();
                if (CollectedData[@"racInstalled"] == @"TRUE") {
                    CollectedData[@"racInstalled"] = @"1";
                    CollectedData[@"b_racInstalled"] = @"1";
                } else {
                    CollectedData[@"racInstalled"] = @"0";
                }
            } else {
                CollectedData[@"racInstalled"] = @"0";
            }
            lmsRACQuery.AppendLine().AppendLine();

            CollectedData[@"racUsed"] = @"0";
            lmsRACQuery.Append("CHECKING TO SEE IF RAC IS BEGIN USED...\n\n");
            lmsRACQuery.Append("SQL>  SELECT NAME, VALUE FROM GV$PARAMETER WHERE NAME='cluster_database'\n\n");
            lmsRACQuery.Append("NAME                  \tVALUE                 \n")
                       .Append("----------------------\t----------------------\n");

            if (CollectedData.ContainsKey(@"clusterDB")) {
                if (!string.IsNullOrEmpty(CollectedData[@"clusterDB"])) {
                    foreach (string line in CollectedData[@"clusterDB"].Split(new string[] { BdnaDelimiters.DELIMITER_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                        foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.None)) {
                            if (s_pattern.IsMatch(field)) {
                                Match match = s_pattern.Match(field);
                                string strName = match.Groups["name"].ToString();
                                string strValue = match.Groups["value"].ToString();

                                if (strName == @"NAME") {
                                    lmsRACQuery.Append(strValue).Append("\t           ");
                                } else if (strName == @"VALUE") {
                                    lmsRACQuery.Append(strValue).AppendLine();
                                    if (strValue == @"TRUE") {
                                        CollectedData[@"racUsed"] = @"1";
                                        CollectedData[@"b_racUsed"] = @"1";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            lmsRACQuery.AppendLine().AppendLine();

            lmsRACQuery.Append("SQL> SELECT INSTANCE_NAME, HOST_NAME FROM GV$INSTANCE;\n\n");
            lmsRACQuery.Append("INSTANCE_NAME         \tHOST_NAME\n")
                       .Append("----------------------\t----------------------\n");

            int count = 0;
            if (CollectedData.ContainsKey(@"clusterInst")) {
                if (!string.IsNullOrEmpty(CollectedData[@"clusterInst"])) {
                    foreach (string line in CollectedData[@"clusterInst"].Split(new string[] { BdnaDelimiters.DELIMITER_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                        foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                            if (s_pattern.IsMatch(field)) {
                                Match match = s_pattern.Match(field);
                                string strName = match.Groups["name"].ToString();
                                string strValue = match.Groups["value"].ToString();
                                
                                if (strName == @"INSTANCE_NAME") {
                                    lmsRACQuery.Append(strValue).Append("\t  ");
                                    count++;
                                } else if (strName == @"HOST_NAME") {
                                    lmsRACQuery.Append(strValue).AppendLine();
                                }
                                if (count > 1) {
                                    CollectedData[@"racUsed"] = @"1";
                                    CollectedData[@"b_racUsed"] = @"1";
                                }
                            }
                        }
                    }
                }
            }
            lmsRACQuery.AppendLine().AppendLine();
            CollectedData[@"lmsRACQuery"] = lmsRACQuery.ToString();
        }


        /// <summary>
        /// Process Spatial collected Result.
        /// </summary>
        private void processSpatialCollectedData()
        {
            StringBuilder lmsSpatialQuery = new StringBuilder();
            lmsSpatialQuery.Append("SQL> SELECT 'ORACLE SPATIAL INSTALLED: '||VALUE \"SPATIAL\" FROM V$OPTION WHERE PARAMETER ='Spatial';\n\n");

            if (CollectedData.ContainsKey(@"spatialInstalled")) {
                lmsSpatialQuery.Append("SPATIAL\n")
                               .Append("-------------------------------------\n");

                lmsSpatialQuery.Append(@"ORACLE SPATIAL INSTALLED: ").Append(CollectedData[@"spatialInstalled"]).AppendLine();
                if (CollectedData[@"spatialInstalled"] == @"TRUE") {
                    CollectedData[@"spatialInstalled"] = @"1";
                    CollectedData[@"b_spatialInstalled"] = @"1";
                } else {
                    CollectedData[@"spatialInstalled"] = @"0";
                }
            } else {
                CollectedData[@"spatialInstalled"] = @"0";
            }
            lmsSpatialQuery.AppendLine().AppendLine();

            CollectedData[@"spatialUsed"] = @"0";
            if (!ver11r2_pattern.IsMatch(m_strVersion)) {
                lmsSpatialQuery.Append("SQL> SELECT COUNT(*) \"ALL_SDO_GEOM_METADATA\" FROM ALL_SDO_GEOM_METADATA;\n\n");
                if (CollectedData.ContainsKey(@"CNT_SDO_GEOM_METADATA")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"CNT_SDO_GEOM_METADATA"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"CNT_SDO_GEOM_METADATA"]) &&
                        !no_row_selected_pattern.IsMatch(CollectedData[@"CNT_SDO_GEOM_METADATA"])) {

                            lmsSpatialQuery.Append("ALL_SDO_GEOM_METADATA\n")
                                       .Append("-------------------------------------\n");

                            lmsSpatialQuery.Append(CollectedData[@"CNT_SDO_GEOM_METADATA"]).AppendLine();
                            if ((CollectedData[@"CNT_SDO_GEOM_METADATA"] != @"0") && (!string.IsNullOrEmpty(CollectedData[@"CNT_SDO_GEOM_METADATA"]))) 
                            {
                                CollectedData[@"spatialUsed"] = @"1";
                                CollectedData[@"b_spatialUsed"] = @"1";
                            }
                        } else {
                            lmsSpatialQuery.AppendLine(CollectedData[@"CNT_SDO_GEOM_METADATA"]);
                        }
                    }
                }
                lmsSpatialQuery.AppendLine().AppendLine();
                CollectedData[@"lmsSpatialQuery"] = lmsSpatialQuery.ToString();
            } else {
                lmsSpatialQuery.AppendLine(@"CHECKING TO SEE IF SPATIAL OPTION IS USED..").AppendLine();
                lmsSpatialQuery.Append(@"SQL> SELECT NAME, DETECTED_USAGES, CURRENTLY_USED, FIRST_USAGE_DATE, LAST_USAGE_DATE ")
                          .Append("FROM DBA_FEATURE_USAGE_STATISTICS WHERE NAME='Spatial';\n\n");

                CollectedData[@"spatialUsed"] = @"0";
                if (CollectedData.ContainsKey(@"spatialDBAUsed"))  {
                    if (!string.IsNullOrEmpty(CollectedData[@"spatialDBAUsed"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"spatialDBAUsed"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"spatialDBAUsed"])) {

                                lmsSpatialQuery.Append(@"NAME").Append(@"                       ")
                                      .Append(@"DETECTED_USAGES").Append(@"  ")
                                      .Append(@"CURRENTLY_USED").Append(@"  ")
                                      .Append(@"FIRST_USAGE_DATE").Append(@"  ")
                                      .Append(@"LAST_USAGE_DATE").Append("\n");
                                lmsSpatialQuery.Append("--------------------------  ").Append("---------------  ")
                                      .Append("--------------  ").Append("-----------------  ").Append("-----------------\n");

                            foreach (string line in CollectedData[@"spatialDBAUsed"].Split(new string[] { BdnaDelimiters.DELIMITER_TAG }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.RemoveEmptyEntries))
                                {
                                    if (s_pattern.IsMatch(field)) {
                                        Match match = s_pattern.Match(field);
                                        string strName = match.Groups["name"].ToString();
                                        string strValue = match.Groups["value"].ToString();

                                        if (strName == @"FEATURE_NAME") {
                                            lmsSpatialQuery.Append(strValue).Append("                        ");
                                        }  else if (strName == @"DETECTED")  {
                                            lmsSpatialQuery.Append(strValue).Append(" ");
                                        }  else if (strName == @"CUR_USED")  {
                                            lmsSpatialQuery.Append(strValue).Append(" ");
                                            if (strValue == @"TRUE") {
                                                CollectedData[@"spatialUsed"] = @"1";
                                                CollectedData[@"b_spatialUsed"] = @"1";
                                            }
                                        }  else if (strName == @"FIRST_USE")  {
                                            lmsSpatialQuery.Append(strValue).Append(" ");
                                        }  else if (strName == @"LAST_USE") {
                                            lmsSpatialQuery.Append(strValue).AppendLine();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            lmsSpatialQuery.AppendLine(CollectedData[@"spatialDBAUsed"]).AppendLine();
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Process Data Mining collected Result.
        /// </summary>
        private void processDMCollectedData() {
            StringBuilder lmsDMQuery = new StringBuilder();
            lmsDMQuery.AppendLine(@"CHECKING TO SEE IF DATA MINING OPTION IS INSTALLED..").AppendLine();
            lmsDMQuery.Append("SELECT ORACLE DATA MINING INSTALLED FROM V$OPTION WHERE PARAMETER like '%Data Mining';\n\n");            
            lmsDMQuery.Append("DATA MINING\n")
                      .Append("-------------------------------------\n");

            if (CollectedData.ContainsKey(@"dmInstalled")) {
                if (!string.IsNullOrEmpty(CollectedData[@"dmInstalled"])) {
                    if (!ora_error_pattern.IsMatch(CollectedData[@"dmInstalled"]) &&
                        !no_row_selected_pattern.IsMatch(CollectedData[@"dmInstalled"])) {

                        lmsDMQuery.Append(@"ORACLE DATA MINING INSTALLED: ").Append(CollectedData[@"dmInstalled"]).AppendLine();
                        if (CollectedData[@"dmInstalled"] == @"TRUE") {
                            CollectedData[@"dmInstalled"] = @"1";
                            CollectedData[@"b_dmInstalled"] = @"1";
                        } else {
                            CollectedData[@"dmInstalled"] = @"0";
                        }
                    } else {
                        lmsDMQuery.AppendLine(CollectedData[@"dmInstalled"]);
                        CollectedData[@"dmInstalled"] = @"0";
                    }
                }
            } else {
                CollectedData[@"dmInstalled"] = @"0";
            }
            lmsDMQuery.AppendLine().AppendLine();

            CollectedData[@"dmUsed"] = @"0";
            if (ver9_pattern.IsMatch(m_strVersion)) {
                lmsDMQuery.AppendLine(@"CHECKING TO SEE IF DATA MINING OPTION IS USED..").AppendLine();
                lmsDMQuery.Append("SQL> SELECT COUNT(*) \"Data_Mining_Model\" FROM ODM.ODM_MINING_MODEL;\n\n");

                if (CollectedData.ContainsKey(@"CNT_DM_MDL9")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL9"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"CNT_DM_MDL9"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"CNT_DM_MDL9"])) {

                            lmsDMQuery.Append("Data_Mining_Model\n")
                                      .Append("-------------------------------------\n");

                            lmsDMQuery.Append(CollectedData[@"CNT_DM_MDL9"]).AppendLine();
                            if ((CollectedData[@"CNT_DM_MDL9"] != @"0") && (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL9"]))) {
                                CollectedData[@"dmUsed"] = @"1";
                                CollectedData[@"b_dmUsed"] = @"1";
                            }
                        } else {
                            lmsDMQuery.AppendLine(CollectedData[@"CNT_DM_MDL9"]);
                        }
                    }
                }
            } else if (ver10r1_pattern.IsMatch(m_strVersion)) {
                lmsDMQuery.AppendLine(@"CHECKING TO SEE IF DATA MINING OPTION IS USED..").AppendLine();
                lmsDMQuery.Append("SQL> SELECT COUNT(*) \"Data_Mining_Objects\" FROM DMSYS.DM$OBJECT;\n\n");

                if (CollectedData.ContainsKey(@"CNT_DM_OBJ10v1")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_OBJ10v1"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"CNT_DM_OBJ10v1"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"CNT_DM_OBJ10v1"])) {

                            lmsDMQuery.Append("Data_Mining_Model\n")
                                      .Append("-------------------------------------\n");

                            lmsDMQuery.Append(CollectedData[@"CNT_DM_OBJ10v1"]).AppendLine();
                            if ((CollectedData[@"CNT_DM_OBJ10v1"] != @"0") && (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_OBJ10v1"]))) {
                                CollectedData[@"dmUsed"] = @"1";
                                CollectedData[@"b_dmUsed"] = @"1";
                            }
                        } else {
                            lmsDMQuery.AppendLine(CollectedData[@"CNT_DM_OBJ10v1"]);
                        }
                    }
                }
                lmsDMQuery.AppendLine();
                lmsDMQuery.Append("SQL> SELECT COUNT(*) \"Data_Mining_Model\" FROM DMSYS.DM$MODEL;\n\n");

                if (CollectedData.ContainsKey(@"CNT_DM_MDL10v1")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL10v1"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"CNT_DM_MDL10v1"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"CNT_DM_MDL10v1"])) {

                            lmsDMQuery.Append("Data_Mining_Model\n")
                                      .Append("-------------------------------------\n");

                            lmsDMQuery.Append(CollectedData[@"CNT_DM_MDL10v1"]).AppendLine();
                            if ((CollectedData[@"CNT_DM_MDL10v1"] != @"0") && (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL10v1"]))) {
                                CollectedData[@"dmUsed"] = @"1";
                                CollectedData[@"b_dmUsed"] = @"1";
                            }
                        } else {
                            lmsDMQuery.AppendLine(CollectedData[@"CNT_DM_MDL10v1"]);
                        }
                    }
                }
            } else if (ver10r2_pattern.IsMatch(m_strVersion)) {
                lmsDMQuery.AppendLine(@"CHECKING TO SEE IF DATA MINING OPTION IS USED..").AppendLine();
                lmsDMQuery.Append("SQL> SELECT COUNT(*) \"Data_Mining_Model\" FROM DMSYS.DM$P_MODEL;\n\n");

                if (CollectedData.ContainsKey(@"CNT_DM_MDL10v2")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL10v2"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"CNT_DM_MDL10v2"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"CNT_DM_MDL10v2"])) {

                            lmsDMQuery.Append("Data_Mining_Model\n")
                                      .Append("-------------------------------------\n");

                            lmsDMQuery.Append(CollectedData[@"CNT_DM_MDL10v2"]).AppendLine();
                            if ((CollectedData[@"CNT_DM_MDL10v2"] != @"0") && (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL10v2"]))) {
                                CollectedData[@"dmUsed"] = @"1";
                                CollectedData[@"b_dmUsed"] = @"1";
                            }
                        } else {
                            lmsDMQuery.AppendLine(CollectedData[@"CNT_DM_MDL10v2"]);
                        }
                    }
                }
            } else if (ver11_pattern.IsMatch(m_strVersion)) {
                lmsDMQuery.AppendLine(@"CHECKING TO SEE IF DATA MINING OPTION IS USED..").AppendLine();
                lmsDMQuery.Append("SQL> SELECT COUNT(*) \"Data_Mining_Objects\" FROM SYS.MODEL$;\n\n");

                if (CollectedData.ContainsKey(@"CNT_DM_MDL11g"))
                {
                    if (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL11g"]))
                    {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"CNT_DM_MDL11g"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"CNT_DM_MDL11g"]))
                        {

                            lmsDMQuery.Append("Data_Mining_Model\n")
                                      .Append("-------------------------------------\n");

                            lmsDMQuery.Append(CollectedData[@"CNT_DM_MDL11g"]).AppendLine();
                            if ((CollectedData[@"CNT_DM_MDL11g"] != @"0") && (!string.IsNullOrEmpty(CollectedData[@"CNT_DM_MDL11g"])))
                            {
                                CollectedData[@"dmUsed"] = @"1";
                                CollectedData[@"b_dmUsed"] = @"1";
                            }
                        }
                        else
                        {
                            lmsDMQuery.AppendLine(CollectedData[@"CNT_DM_MDL11g"]);
                        }
                    }
                }
            } else {
                lmsDMQuery.AppendLine(@"CHECKING TO SEE IF DATA MINING OPTION IS USED..").AppendLine();
                lmsDMQuery.Append(@"SQL> SELECT NAME, DETECTED_USAGES, CURRENTLY_USED, FIRST_USAGE_DATE, LAST_USAGE_DATE ")
                          .Append("FROM DBA_FEATURE_USAGE_STATISTICS WHERE NAME='Data Mining';\n\n");

                CollectedData[@"dmUsed"] = @"0";
                if (CollectedData.ContainsKey(@"dmDBAUsed")) {
                    if (!string.IsNullOrEmpty(CollectedData[@"dmDBAUsed"])) {
                        if (!ora_error_pattern.IsMatch(CollectedData[@"dmDBAUsed"]) &&
                            !no_row_selected_pattern.IsMatch(CollectedData[@"dmDBAUsed"])) {

                            lmsDMQuery.Append(@"NAME").Append(@"                       ")
                                      .Append(@"DETECTED_USAGES").Append(@"  ")
                                      .Append(@"CURRENTLY_USED").Append(@"  ")
                                      .Append(@"FIRST_USAGE_DATE").Append(@"  ")
                                      .Append(@"LAST_USAGE_DATE").Append("\n");
                            lmsDMQuery.Append("--------------------------  ").Append("---------------  ")
                                      .Append("--------------  ").Append("-----------------  ").Append("-----------------\n");

                            foreach (string line in CollectedData[@"dmDBAUsed"].Split(new string[] { BdnaDelimiters.DELIMITER_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                                foreach (string field in line.Split(new String[] { BdnaDelimiters.DELIMITER1_TAG }, StringSplitOptions.RemoveEmptyEntries)) {
                                    if (s_pattern.IsMatch(field)) {
                                        Match match = s_pattern.Match(field);
                                        string strName = match.Groups["name"].ToString();
                                        string strValue = match.Groups["value"].ToString();

                                        if (strName == @"FEATURE_NAME") {
                                            lmsDMQuery.Append(strValue).Append("                        ");
                                        } else if (strName == @"DETECTED") {
                                            lmsDMQuery.Append(strValue).Append(" ");
                                        } else if (strName == @"CUR_USED") {
                                            lmsDMQuery.Append(strValue).Append(" ");
                                            if (strValue == @"TRUE") {
                                                CollectedData[@"dmUsed"] = @"1";
                                                CollectedData[@"b_dmUsed"] = @"1";
                                            }
                                        } else if (strName == @"FIRST_USE") {
                                            lmsDMQuery.Append(strValue).Append(" ");
                                        } else if (strName == @"LAST_USE") {
                                            lmsDMQuery.Append(strValue).AppendLine();
                                        }
                                    }
                                }
                            }
                        } else {
                            lmsDMQuery.AppendLine(CollectedData[@"dmDBAUsed"]).AppendLine();
                        }
                    }
                }
            }

            lmsDMQuery.AppendLine().AppendLine();
            CollectedData[@"lmsDMQuery"] = lmsDMQuery.ToString();
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
        /// Parse query result for a cluster db value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void ClusterDBValueHandler
            (WindowsOracleInstanceLMSOptions2StaticScript scriptInstance, String attributeName, String queryOutput) {

            Regex r = new Regex(@"^<BDNA>clusterDB<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);

            foreach (String line in output.Split('\n', '\r')) {
                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String NAME = match.Groups[1].ToString();
                        String VALUE = match.Groups[2].ToString();

                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(@"NAME=").Append(NAME);
                        result.Append(@"<BDNA,1>VALUE=").Append(VALUE);
                        logData.AppendFormat("{0}: {1}:{2}\n", attributeName, NAME, VALUE);
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
        /// Parse query result for a cluster inst value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void ClusterInstValueHandler
            (WindowsOracleInstanceLMSOptions2StaticScript scriptInstance, String attributeName, String queryOutput) {

            Regex r = new Regex(@"^<BDNA>clusterInst<BDNA>(.*?)<BDNA>(.*?)<BDNA>$");
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);

            foreach (String line in output.Split('\n', '\r')) {
                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String INSTANCE_NAME = match.Groups[1].ToString();
                        String HOST_NAME = match.Groups[2].ToString();

                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(@"INSTANCE_NAME=").Append(INSTANCE_NAME);
                        result.Append(@"<BDNA,1>HOST_NAME=").Append(HOST_NAME);
                        logData.AppendFormat("{0}: {1}:{2}\n", attributeName, INSTANCE_NAME, HOST_NAME);
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
        /// Parse query result for a single value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void SingleValueHandler
            (WindowsOracleInstanceLMSOptions2StaticScript scriptInstance, String attributeName, String queryOutput) {

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
        /// Parse query result for DBA_FEATURE_USAGE_STATISTICS value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void dbaFeatureValueHandler
            (WindowsOracleInstanceLMSOptions2StaticScript scriptInstance, String attributeName, String queryOutput) {

            Regex r = new Regex(@"^<BDNA>.*DBAUsed<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<BDNA>(.*?)<B");
            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);

            foreach (String line in output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {

                if (r.IsMatch(line)) {
                    Match match = r.Match(line);
                    if (match.Length > 1) {
                        String FEATURE_NAME = match.Groups[1].ToString();
                        String DETECTED = match.Groups[2].ToString();
                        String CUR_USED = match.Groups[3].ToString();
                        String FIRST_USE = match.Groups[4].ToString();
                        String LAST_USE = match.Groups[5].ToString();

                        if (result.Length > 0) {
                            result.Append(@"<BDNA,>");
                        }
                        result.Append(@"FEATURE_NAME=").Append(FEATURE_NAME);
                        result.Append(@"<BDNA,1>DETECTED=").Append(DETECTED);
                        result.Append(@"<BDNA,1>CUR_USED=").Append(CUR_USED);
                        result.Append(@"<BDNA,1>FIRST_USE=").Append(FIRST_USE);
                        result.Append(@"<BDNA,1>LAST_USE=").Append(LAST_USE);
                        logData.AppendFormat("{0}: {1}:{2}:{3}:{4}:{5}\n", attributeName, FEATURE_NAME, DETECTED, CUR_USED, FIRST_USE, LAST_USE);
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
        private delegate void QueryResultHandler(WindowsOracleInstanceLMSOptions2StaticScript scriptInstance, string attributeName, string outputData);

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
        static WindowsOracleInstanceLMSOptions2StaticScript() {
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
            new KeyValuePair<string, QueryTableEntry>(@"racInstalled",
            new QueryTableEntry(@"select '<BDNA>racInstalled<BDNA>'||value||'<BDNA>' FROM V$OPTION WHERE PARAMETER='Real Application Clusters';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"clusterDB",
            new QueryTableEntry(@"select '<BDNA>clusterDB<BDNA>'||NAME||'<BDNA>'||VALUE||'<BDNA>' FROM GV$PARAMETER WHERE NAME='cluster_database';",
            all_ver_pattern,
            new QueryResultHandler(ClusterDBValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"clusterInst",
            new QueryTableEntry(@"select '<BDNA>clusterInst<BDNA>'||INSTANCE_NAME||'<BDNA>'||HOST_NAME||'<BDNA>' FROM GV$INSTANCE;",
            all_ver_pattern,
            new QueryResultHandler(ClusterInstValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"spatialInstalled",
            new QueryTableEntry(@"select '<BDNA>spatialInstalled<BDNA>'||value||'<BDNA>' FROM V$OPTION WHERE PARAMETER='Spatial';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"CNT_SDO_GEOM_METADATA",
            new QueryTableEntry(@"select '<BDNA>CNT_SDO_GEOM_METADATA<BDNA>'||COUNT(*)||'<BDNA>' FROM ALL_SDO_GEOM_METADATA;",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"spatialDBAUsed",
            new QueryTableEntry(@"select '<BDNA>spatialDBAUsed<BDNA>'||NAME||'<BDNA>'||DETECTED_USAGES||'<BDNA>'"+
                                @"||CURRENTLY_USED||'<BDNA>'||FIRST_USAGE_DATE||'<BDNA>'||LAST_USAGE_DATE||'<BDNA>'"+
                                @" FROM DBA_FEATURE_USAGE_STATISTICS WHERE NAME='Spatial';",
            ver11r2_pattern,
            new QueryResultHandler(dbaFeatureValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"dmInstalled",
            new QueryTableEntry(@"select '<BDNA>dmInstalled<BDNA>'||value||'<BDNA>' FROM V$OPTION WHERE PARAMETER='Data Mining';",
            all_ver_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"CNT_DM_MDL9",
            new QueryTableEntry(@"select '<BDNA>CNT_DM_MDL9<BDNA>'||COUNT(*)||'<BDNA>' FROM ODM.ODM_MINING_MODEL;",
            ver9_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"CNT_DM_OBJ10v1",
            new QueryTableEntry(@"select '<BDNA>CNT_DM_OBJ10v1<BDNA>'||COUNT(*)||'<BDNA>' FROM DMSYS.DM$OBJECT;",
            ver10r1_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"CNT_DM_MDL10v1",
            new QueryTableEntry(@"select '<BDNA>CNT_DM_MDL10v1<BDNA>'||COUNT(*)||'<BDNA>' FROM DMSYS.DM$MODEL;",
            ver10r1_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"CNT_DM_MDL10v2",
            new QueryTableEntry(@"select '<BDNA>CNT_DM_MDL10v2<BDNA>'||COUNT(*)||'<BDNA>' FROM DMSYS.DM$P_MODEL;",
            ver10r2_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"CNT_DM_MDL11g",
            new QueryTableEntry(@"select '<BDNA>CNT_DM_MDL11g<BDNA>'||COUNT(*)||'<BDNA>' FROM SYS.MODEL$;",
            ver11_pattern,
            new QueryResultHandler(SingleValueHandler))),
            new KeyValuePair<string, QueryTableEntry>(@"dmDBAUsed",
            new QueryTableEntry(@"select '<BDNA>dmDBAUsed<BDNA>'||NAME||'<BDNA>'||DETECTED_USAGES||'<BDNA>'"+
                                @"||CURRENTLY_USED||'<BDNA>'||FIRST_USAGE_DATE||'<BDNA>'||LAST_USAGE_DATE||'<BDNA>'"+
                                @" FROM DBA_FEATURE_USAGE_STATISTICS WHERE NAME='Data Mining';",
            all_ver_pattern,
            new QueryResultHandler(dbaFeatureValueHandler)))        
        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();

    }
}
