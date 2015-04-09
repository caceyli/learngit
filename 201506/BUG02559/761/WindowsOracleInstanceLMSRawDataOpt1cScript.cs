#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Suma Manvi
*   Creation Date: 2011/06/27
*
* Current Status
*       $Revision: 1.8 $
*           $Date: 2014/03/13 07:57:08 $
*         $Author: MiyaChen $
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

namespace bdna.Scripts
{
    public class WindowsOracleInstanceLMSRawDataOpt1cScript : ICollectionScriptRuntime
    {

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
                ITftpDispatcher tftpDispatcher)
        {

            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_connection = connection;
            string strHostName = null, strDBName = null, strOracleHome = null, strSchemaName = null, strSchemaPassword = null;

            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSRawDataOpt1cScript.",
                                  m_taskId);

            try
            {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleInstanceLMSRawDataOpt1cScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey("cimv2"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else
                {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }

                if (!scriptParameters.ContainsKey("version"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"version\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                }
                else
                {
                    m_strVersion = scriptParameters["version"].Trim();
                }

                if (!scriptParameters.ContainsKey("OracleHome"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"OracleHome\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                }
                else
                {
                    strOracleHome = scriptParameters["OracleHome"].Trim();
                    if (strOracleHome.EndsWith(@"\"))
                    {
                        strOracleHome = strOracleHome.Substring(0, strOracleHome.Length - 1);
                    }
                }

                if (!scriptParameters.ContainsKey("address"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"Host Address\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                }
                else
                {
                    strHostName = scriptParameters["address"].Trim();
                    if (strHostName.EndsWith(@"\"))
                    {
                        strHostName = strHostName.Substring(0, strHostName.Length - 1);
                    }
                }

                if (!scriptParameters.ContainsKey("name"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"Instance Name\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                }
                else
                {
                    strDBName = scriptParameters["name"].Trim();
                }

                if (!connection.ContainsKey("schemaName"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"schemaName\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                }
                else
                {
                    strSchemaName = connection["schemaName"].ToString().Trim();
                }

                if (!connection.ContainsKey("schemaPassword"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"schemaPassword\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                }
                else
                {
                    strSchemaPassword = connection["schemaPassword"].ToString().Trim();
                }


                if (ResultCodes.RC_SUCCESS == resultCode)
                {
                    // Check Remote Process Temp Directory
                    if (!connection.ContainsKey("TemporaryDirectory"))
                    {
                        connection["TemporaryDirectory"] = @"%TMP%";
                    }
                    else
                    {
                        if (!m_connection[@"TemporaryDirectory"].Equals(@"%TMP%"))
                        {
                            if (!Lib.ValidateDirectory(m_taskId, m_connection[@"TemporaryDirectory"].ToString(), cimvScope))
                            {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} is not valid.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                            }
                            else
                            {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} has been validated.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                            }
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    string strTempDir = connection["TemporaryDirectory"].ToString().Trim();
                    if (strTempDir.EndsWith(@"\"))
                    {
                        strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
                    }
                    Console.WriteLine("Temp directory is " + strTempDir);

                    string strBatchFileContent = buildBatchFile(strTempDir, strHostName, strDBName, strOracleHome, strSchemaName, strSchemaPassword);

                    StringBuilder stdoutData = new StringBuilder();
                    using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile
                        (m_taskId, cimvScope, strBatchFileContent, connection, tftpDispatcher))
                    {
                        //This method will block until the entire remote process operation completes.
                        resultCode = rp.Launch();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote process operation completed with result code {1}.",
                                              m_taskId,
                                              resultCode.ToString());

                        if (resultCode == ResultCodes.RC_SUCCESS)
                        {
                            stdoutData.Append(rp.Stdout);
                            if (rp.Stdout != null && rp.Stdout.Length > 0)
                            {
                                if (rp.Stdout.ToString().Contains("ORA-01017"))
                                {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Oracle L3 credential is invalid.\nResult code changed to RC_PROCESSING_EXCEPTION.\nSTDOUT/STDERR:\n{1}",
                                                          m_taskId,
                                                          rp.Stdout.ToString());
                                    resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                                }
                                else if (rp.Stdout.ToString().Contains("ERROR-"))
                                {
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
                                }
                                else if (!rp.Stdout.ToString().Contains(@"Execution completed"))
                                {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Exception with batch return data.\nData returned is shorter than expected, possibly due to transfer failure.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                          m_taskId);
                                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                                }
                            }
                            else
                            {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: No data returned.\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                                      m_taskId);
                                resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                            }
                        }
                        else
                        {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Remote execution error.\nSTDOUT.STDERR:\n{1}",
                                                  m_taskId,
                                                  rp.Stdout.ToString());
                        }
                    }
                    if (resultCode == ResultCodes.RC_SUCCESS && stdoutData.Length > 0)
                    {
                        foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable)
                        {
                            entry.Value.ResultHandler(this, entry.Key, stdoutData.ToString());
                        }
                        foreach (KeyValuePair<string, string> kvp in m_collectedData)
                        {
                            this.BuildDataRow(kvp.Key, kvp.Value);
                        }
                        //Console.WriteLine(CollectedData[@"lmsOptions"]);
                        //Console.WriteLine(CollectedData[@"lmsMachineID"]);
                        //Console.WriteLine(CollectedData[@"lmsDBName"]);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ResultCodes.RC_SUCCESS == resultCode)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSRawDataOpt1cScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                }
                else
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsOracleInstanceLMSRawDataOpt1cScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleInstanceLMSRawDataOpt1cScript.  Elapsed time {1}.  Result code {2}.",
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
        public void SaveCollectedData(string attributeName, string collectedData)
        {
            m_collectedData[attributeName] = collectedData;
        }

        public IDictionary<string, string> CollectedData
        {
            get
            {
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
        public void BuildDataRow(string attributeName, string collectedData)
        {
            if (!m_attributes.ContainsKey(attributeName))
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            }
            else if (string.IsNullOrEmpty(collectedData))
            {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            }
            else
            {
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
        private string buildBatchFile(string strTempDir, string strHostName, string strDBName, string strOracleHome, string strSchemaName, string strSchemaPassword)
        {
            StringBuilder strBatchFile = new StringBuilder();
            if (!String.IsNullOrEmpty(strTempDir))
            {
                if (strTempDir.EndsWith(@"\"))
                {
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
            for (int i = 0; i < 4; i++)
            {
                strBatchFile.Append(@"ECHO QUIT >> ").Append(strTempDir).Append(@"\%1").AppendLine(@"\CMDLINE.TXT");
            }
            strBatchFile.Append(@"ECHO # EMPTY FILE >> ")
                .Append(strTempDir).Append(@"\%1").AppendLine(@"\SQLNET.ORA");
            strBatchFile.AppendLine();

            strBatchFile.Append("ECHO SET SERVEROUTPUT ON;")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO SET LINESIZE 999;")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            strBatchFile.Append("ECHO define MACHINE_ID=" + strHostName + @";")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO define DB_NAME=" + strDBName + @";")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO define DBL='';")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO define TNS_NAME='';")
                        .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO DROP TABLE LMS_OPTIONS1C;")
            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            strBatchFile.Append("ECHO CREATE TABLE LMS_OPTIONS1C(MACHINE_ID VARCHAR2(255), DB_NAME VARCHAR2(255), ")
                        .Append("TIMESTAMP DATE, HOST_NAME VARCHAR2(255), INSTANCE_NAME VARCHAR2(255), ")
                        .Append("OPTION_NAME VARCHAR2(255), OPTION_QUERY VARCHAR2(255), SQL_ERR_CODE VARCHAR2(255), ")
                        .Append("SQL_ERR_MESSAGE VARCHAR2(255), COL010 VARCHAR2(255), COL020 VARCHAR2(255), ")
                        .Append("COL030 VARCHAR2(255), COL040 VARCHAR2(255), COL050 VARCHAR2(255), ")
                        .Append("COL060 VARCHAR2(255), COL070 VARCHAR2(255), COL080 VARCHAR2(255), ")
                        .Append("COL090 VARCHAR2(255), COL100 VARCHAR2(255)); ")
            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            strBatchFile.Append("ECHO DROP TABLE TMP_LMS_OPTIONS;")
            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            strBatchFile.Append("ECHO CREATE TABLE TMP_LMS_OPTIONS(MACHINE_ID VARCHAR2(255), DB_NAME VARCHAR2(255), ")
                        .Append("TIMESTAMP DATE, HOST_NAME VARCHAR2(255), INSTANCE_NAME VARCHAR2(255), ")
                        .Append("OPTION_NAME VARCHAR2(255), OPTION_QUERY VARCHAR2(255), SQL_ERR_CODE VARCHAR2(255), ")
                        .Append("SQL_ERR_MESSAGE VARCHAR2(255), COL010 VARCHAR2(255), COL020 VARCHAR2(255), ")
                        .Append("COL030 VARCHAR2(255), COL040 VARCHAR2(255), COL050 VARCHAR2(255), ")
                        .Append("COL060 VARCHAR2(255), COL070 VARCHAR2(255), COL080 VARCHAR2(255), ")
                        .Append("COL090 VARCHAR2(255), COL100 VARCHAR2(255)); ")
            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

            foreach (KeyValuePair<string, QueryTableEntry> entry in s_queryTable)
            {
                string strQArr = entry.Value.QueryString.ToString();
                string strName = entry.Key;
                Regex regex = entry.Value.regex;
                char[] splitchar = { '\n', '\r' };
                string[] str_Query = strQArr.Split(splitchar);

                strBatchFile.Append("ECHO PROMPT " + strName + @"_BEGIN___;")
                            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                for (int n = 0; n < str_Query.Length; n++)
                {
                    string strQuery = str_Query[n];
                    if ((!string.IsNullOrEmpty(strQuery)) && (regex.IsMatch(m_strVersion)))
                    {
                        strBatchFile.Append("ECHO ");
                        strBatchFile.Append(strQuery.Trim()
                        .Replace("<", "^<").Replace("%", "%%")
                        .Replace("&", "^&").Replace(">", "^>").Replace("|", @"^|"));
                        strBatchFile.Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
                    }
                }
                strBatchFile.Append("ECHO ").Append("/ ")
                            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");

                strBatchFile.Append("ECHO PROMPT " + @"___" + strName + @"_END;")
                            .Append(@" >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
            }
            strBatchFile.Append(@"ECHO COMMIT; >> ").Append(strTempDir).AppendLine(@"\%1\QUERY.SQL");
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
        private static string matchFirstGroup(string line, Regex regex)
        {
            String ret = "";
            MatchCollection matches = regex.Matches(line);
            foreach (Match m in matches)
            {
                if (m.Groups.Count > 0)
                {
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
        private static string ExtractQueryOutput(String attributeName, String queryOutput)
        {
            String output = string.Empty;

            int beginIndex = -1;
            int endIndex = -1;
            String section = string.Empty;
            string beginStr = attributeName + @"_BEGIN___";
            string endStr = @"___" + attributeName + @"_END";
            if (queryOutput.Contains(beginStr))
            {
                beginIndex = queryOutput.IndexOf(beginStr);
            }
            if (queryOutput.Contains(endStr))
            {
                endIndex = queryOutput.IndexOf(endStr);
            }

            if ((beginIndex != -1) && (endIndex != -1))
            {
                output = queryOutput.Substring(beginIndex + beginStr.Length, endIndex - beginIndex - beginStr.Length);
            }
            return output;
        }


        /// <summary>
        /// Parse query result for a OEM value.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void RawDataValueHandler
            (WindowsOracleInstanceLMSRawDataOpt1cScript scriptInstance, String attributeName, String queryOutput)
        {

            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);
            StringBuilder result = new StringBuilder();

            //
            // Never compile a regular expression is not assigned to
            // a static reference.  Otherwise you will leak an Assembly.
            Regex r = new Regex(@"^<BDNA>" + attributeName + @"<BDNA>(.*?)<BDNA>$");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries))
            {
                if (no_row_selected_pattern.IsMatch(line))
                {
                    result.Append(matchFirstGroup(line, no_row_selected_pattern));
                    logData.AppendLine("No rows selected.");
                    break;
                }
                else if (ora_error_pattern.IsMatch(line))
                {
                    result.Append(matchFirstGroup(line, ora_error_pattern));
                    logData.AppendLine("Oracle error..");
                }
                else
                {
                    if (result.Length > 0)
                    {
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
            if (result.Length > 0)
            {
                scriptInstance.SaveCollectedData(attributeName, result.ToString());
            }
        }


        /// <summary>
        /// Parse query result for a Raw Data options.
        /// </summary>
        /// <param name="scriptInstance">script reference</param>
        /// <param name="attributeNames">attribute</param>
        /// <param name="queryOutput">Output</param>
        private static void RawDataQueryValueHandler
            (WindowsOracleInstanceLMSRawDataOpt1cScript scriptInstance, String attributeName, String queryOutput)
        {

            StringBuilder result = new StringBuilder();
            StringBuilder logData = new StringBuilder();
            string output = ExtractQueryOutput(attributeName, queryOutput);

            //
            // Never compile a regular expression is not assigned to
            // a static reference.  Otherwise you will leak an Assembly.
            Regex r = new Regex(@"<BDNA=>(.*?)<=BDNA>");

            foreach (String line in output.Split((new char[] { '\n', '\r' }), StringSplitOptions.RemoveEmptyEntries))
            {
                if (r.IsMatch(line))
                {
                    Match match = r.Match(line);
                    if (match.Length > 1)
                    {
                        result.Append(match.Groups[1].ToString());
                        if (result.Length > 0)
                        {
                            result.Append(@"<BDNA,>");
                        }

                        logData.AppendFormat("{0}: {1}\n", attributeName, result.ToString());
                    }
                }
                else if (no_row_selected_pattern.IsMatch(line))
                {
                    result.Append(matchFirstGroup(line, no_row_selected_pattern));
                    logData.AppendLine("No rows selected.");
                    break;
                }
                else if (ora_error_pattern.IsMatch(line))
                {
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

            if (result.Length > 0)
            {
                String rawData = result.ToString();
                int len = rawData.Length;
                scriptInstance.SaveCollectedData(attributeName, rawData.Substring(0, (len - 7)));
            }
        }


        /// <summary>
        /// Signature for query result handlers.
        /// </summary>
        private delegate void QueryResultHandler(WindowsOracleInstanceLMSRawDataOpt1cScript scriptInstance, string attributeName, string outputData);

        /// <summary>
        /// Helper class to match up a query with the correct result handler.
        /// </summary>
        private class QueryTableEntry
        {

            public QueryTableEntry(StringBuilder queryString, Regex regex, QueryResultHandler resultHandler)
            {
                m_queryString = queryString;
                m_resultHandler = resultHandler;
                m_regex = regex;
            }

            /// <summary>
            /// Get regex 
            /// </summary>
            public Regex regex
            {
                get { return m_regex; }
            }

            /// <summary>
            /// Gets the query string.
            /// </summary>
            public StringBuilder QueryString
            {
                get { return m_queryString; }
            }

            /// <summary>\
            /// Gets the result handler.
            /// </summary>
            public QueryResultHandler ResultHandler
            {
                get { return m_resultHandler; }
            }

            /// <summary>Regex</summary>
            private readonly Regex m_regex;

            /// <summary>Query string.</summary>
            private readonly StringBuilder m_queryString;

            /// <summary>Result handler.</summary>
            private readonly QueryResultHandler m_resultHandler;
        }

        /// <summary>
        /// Static initializer to build up a map of supported attribute
        /// names to their associated query strings at class load time.
        /// </summary>
        static WindowsOracleInstanceLMSRawDataOpt1cScript()
        {
            ICollection<KeyValuePair<string, QueryTableEntry>> ic = (ICollection<KeyValuePair<string, QueryTableEntry>>)s_attributeMap;

            foreach (KeyValuePair<string, QueryTableEntry> kvp in s_queryTable)
            {
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
        //private static Regex ver89_pattern = new Regex(@"^9\..*|^8\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static Regex ver9_pattern = new Regex(@"^9\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static Regex ver1011_pattern = new Regex(@"^10\..*|^11\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static Regex ver10r1_pattern = new Regex(@"^10\.1.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static Regex ver10r2_pattern = new Regex(@"^10\.2.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static Regex ver11r2_pattern = new Regex(@"^11\.2.*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        //private static Regex ver11_pattern = new Regex(@"^11\..*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex no_row_selected_pattern = new Regex(@"^(no rows selected)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex rows_selected_pattern = new Regex(@"^(\d+ rows selected.)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex ora_error_pattern = new Regex(@"^(ORA-\d+: .*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Data row buffer.</summary>
        StringBuilder m_rawData = new StringBuilder();

        /// <summary>
        /// This table pairs up known attribute names with the query string needed to get the correct value from an Oracle
        /// database.  This table exists merely to seed the attribute map which will be used by the task execution code.
        /// </summary>
        private static readonly KeyValuePair<string, QueryTableEntry>[] s_queryTable = {
         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS5_1",            
         new QueryTableEntry(new StringBuilder(" SET ECHO OFF; ")            
.AppendLine()
.AppendLine(" SET TERMOUT ON; ")
.AppendLine(" SET VERIFY OFF; ")
.AppendLine(" SET TAB OFF; ")
.AppendLine(" SET TRIMOUT OFF; ")
.AppendLine(" SET TRIMSPOOL OFF; ")
.AppendLine(" SET PAGESIZE 5000; ")
//.AppendLine(" SET LINESIZE 300; ")
.AppendLine(" SET SERVEROUTPUT ON SIZE 1000000; ")
.AppendLine(" SET SERVEROUTPUT ON SIZE UNLIMITED; ")
.AppendLine(" SET FEEDBACK OFF; ")
.AppendLine(" alter session set GLOBAL_NAMES = FALSE; ")
.AppendLine(" alter session set NLS_LANGUAGE='AMERICAN'; ")
.AppendLine(" alter session set NLS_TERRITORY='AMERICA'; ")
.AppendLine(" alter session set NLS_DATE_FORMAT='YYYY-MM-DD_HH24:MI:SS'; ")
.AppendLine(" alter session set NLS_TIMESTAMP_FORMAT='YYYY-MM-DD_HH24:MI:SS'; ")
.AppendLine(" alter session set NLS_TIMESTAMP_TZ_FORMAT='YYYY-MM-DD_HH24:MI:SS_TZH:TZM'; ")
.AppendLine(" -- Get host_name and instance_name ")
.AppendLine(" define INSTANCE_NAME=ERR_NEW_VAL_INSTANCE_NAME; ")
.AppendLine(" col C1 new_val INSTANCE_NAME noprint; ")
.AppendLine(" define HOST_NAME=ERR_NEW_VAL_HOST_NAME; ")
.AppendLine(" col C2 new_val HOST_NAME noprint; ")
.AppendLine(" define SYS_TIME=ERR_NEW_VAL_SYS_TIME; ")
.AppendLine(" col C3 new_val SYS_TIME noprint; ")
.AppendLine(" select sysdate C3 from dual; ")
.AppendLine(" col DB_LINK new_val DBL noprint; ")
.AppendLine(" --SELECT '@'||'&TNS_NAME.' as DB_LINK FROM   LMS_VAR ")
.AppendLine(" --WHERE  nvl(upper(O_SID), 'nnuull') != nvl(upper('&TNS_NAME.'), 'nnuull'); ")
.AppendLine(" -- Oracle7 ")
.AppendLine(" SELECT min(machine) C2 FROM v$session&DBL WHERE type = 'BACKGROUND'; ")
.AppendLine(" SELECT name C1 FROM v$database&DBL; ")
.AppendLine(" -- Oracle8 and higher ")
.AppendLine(" SELECT instance_name C1, host_name C2 FROM v$instance&DBL; ")
.AppendLine(" -- Prepare to select DBA_USERS information from the proper source ")
.AppendLine(" define DBA_USERS_SOURCE=SYS.AUDIT_DBA_USERS&DBL; ")
.AppendLine(" col C1 new_val DBA_USERS_SOURCE noprint; ")
.AppendLine(" -- This query will work only if DBA_USERS is accessible ")
.AppendLine(" prompt Checking select on DBA_USERS privilege... ")
.AppendLine(" select 'DBA_USERS&DBL' C1 from DBA_USERS&DBL where rownum=1; ")
.AppendLine(" col C1 clear; ")
.AppendLine(" prompt Collecting from DB_LINK=[&DBL.] HOST_NAME=[&HOST_NAME.] ")
.AppendLine(" INSTANCE_NAME=[&INSTANCE_NAME.] DBA_USERS_SOURCE=[&DBA_USERS_SOURCE.] ")
.AppendLine()
.AppendLine(" prompt ")
.AppendLine("define OPTION_NAME=PARTITIONING ")
.AppendLine("define OPTION_QUERY=OLAP_AWS_SEGMENTS ")
.AppendLine("DECLARE ")
.AppendLine("        mycode number; ")
.AppendLine("        myerr varchar2 (3000); ")
.AppendLine("        mycount number; ")
.AppendLine("BEGIN ")
.AppendLine("  delete from TMP_LMS_OPTIONS; ")
.AppendLine("  -- Using dynamic SQL in order to capture 'ORA-00942: table or view does not exist' error ")
.AppendLine("  EXECUTE IMMEDIATE ('insert into TMP_LMS_OPTIONS ")
.AppendLine("    ( MACHINE_ID, DB_NAME, TIMESTAMP, HOST_NAME, INSTANCE_NAME, OPTION_NAME, OPTION_QUERY, ")
.AppendLine("      COL010, ")
.AppendLine("      COL020, ")
.AppendLine("      COL030, ")
.AppendLine("      COL040, ")
.AppendLine("      COL050, ")
.AppendLine("      COL060, ")
.AppendLine("      COL070 ")
.AppendLine("    ) ")
.AppendLine("    select ")
.AppendLine("      ''&MACHINE_ID.'', ''&DB_NAME.'', ''&SYS_TIME.'', ''&HOST_NAME.'', ''&INSTANCE_NAME.'', ''&OPTION_NAME.'', ''&OPTION_QUERY.'', ")
.AppendLine("      aw_owner      , ")
.AppendLine("      aw_name       , ")
.AppendLine("      aw_version    , ")
.AppendLine("      object_type   , ")
.AppendLine("      owner         , ")
.AppendLine("      object_name   , ")
.AppendLine("      table_name ")
.AppendLine("    from ( ")
.AppendLine("    select distinct ")
.AppendLine("           c.owner as aw_owner, ")
.AppendLine("           c.aw_name, ")
.AppendLine("           c.aw_version, ")
.AppendLine("           d.object_type, ")
.AppendLine("           d.owner, ")
.AppendLine("           d.object_name, ")
.AppendLine("           d.object_name as table_name ")
.AppendLine("      from dba_aws      c ")
.AppendLine("      join dba_objects  d on c.owner = d.owner and ''AW$''||c.aw_name = d.object_name ")
.AppendLine("      where d.object_type like ''%PARTITION%'' ")
.AppendLine("    union all ")
.AppendLine("    select distinct ")
.AppendLine("           e.owner as aw_owner, ")
.AppendLine("           e.aw_name, ")
.AppendLine("           e.aw_version, ")
.AppendLine("           g.object_type, ")
.AppendLine("           g.owner, ")
.AppendLine("           g.object_name, ")
.AppendLine("           f.table_name ")
.AppendLine("      from dba_aws            e ")
.AppendLine("      join dba_lobs           f on e.owner = f.owner and ''AW$''||e.aw_name = f.table_name ")
.AppendLine("      join dba_objects        g on f.owner = g.owner and f.segment_name = g.object_name ")
.AppendLine("      where g.object_type like ''%PARTITION%'' ")
.AppendLine("    union all ")
.AppendLine("    select distinct ")
.AppendLine("           e.owner as aw_owner, ")
.AppendLine("           e.aw_name, ")
.AppendLine("           e.aw_version, ")
.AppendLine("           g.object_type, ")
.AppendLine("           g.owner, ")
.AppendLine("           g.object_name, ")
.AppendLine("           f.table_name ")
.AppendLine("    from DBA_AWS&DBL        e ")
.AppendLine("    join DBA_INDEXES&DBL    f on e.owner = f.table_owner and ''AW$''||e.aw_name = f.table_name ")
.AppendLine("    join DBA_OBJECTS&DBL    g on f.owner = g.owner and f.index_name = g.object_name ")
.AppendLine("      where g.object_type like ''%PARTITION%'' ")
.AppendLine("      order by owner, aw_name, object_type, object_name ")
.AppendLine("         ) ")
.AppendLine("  '); ")
.AppendLine("  mycount := SQL%ROWCOUNT; ")
.AppendLine("  -- Recording 'no rows selected' case ")
.AppendLine("  IF mycount=0 THEN ")
.AppendLine("    INSERT INTO LMS_OPTIONS1C ")
.AppendLine("    ( MACHINE_ID, DB_NAME, TIMESTAMP, HOST_NAME, INSTANCE_NAME, OPTION_NAME, OPTION_QUERY, ")
.AppendLine("      SQL_ERR_CODE, ")
.AppendLine("      SQL_ERR_MESSAGE) ")
.AppendLine("    VALUES ")
.AppendLine("    ( '&MACHINE_ID.', '&DB_NAME.', '&SYS_TIME.', '&HOST_NAME.', '&INSTANCE_NAME.', '&OPTION_NAME.', '&OPTION_QUERY.', ")
.AppendLine("      0, ")
.AppendLine("      'no rows selected'); ")
.AppendLine("  ELSE ")
.AppendLine("    update TMP_LMS_OPTIONS set SQL_ERR_CODE=mycount, SQL_ERR_MESSAGE='count'; ")
.AppendLine("    insert into LMS_OPTIONS1C (select * from TMP_LMS_OPTIONS); ")
.AppendLine("  END IF; ")
.AppendLine("  dbms_output.put_line ('PL/SQL block completed for [&OPTION_NAME.].[&OPTION_QUERY.]. ' || mycount || ' rows collected.'); ")
.AppendLine("EXCEPTION ")
.AppendLine("    -- Recording Errors ")
.AppendLine("    when OTHERS then ")
.AppendLine("      mycode := SQLCODE; ")
.AppendLine("      myerr  := SQLERRM; ")
.AppendLine("      dbms_output.put_line ('PL/SQL block exception ['|| mycode ||'] for [&OPTION_NAME.].[&OPTION_QUERY.]'); ")
.AppendLine("      INSERT INTO LMS_OPTIONS1C ")
.AppendLine("      ( MACHINE_ID, DB_NAME, TIMESTAMP, HOST_NAME, INSTANCE_NAME, OPTION_NAME, OPTION_QUERY, ")
.AppendLine("        SQL_ERR_CODE, ")
.AppendLine("        SQL_ERR_MESSAGE) ")
.AppendLine("       VALUES ")
.AppendLine("      ( '&MACHINE_ID.', '&DB_NAME.', '&SYS_TIME.', '&HOST_NAME.', '&INSTANCE_NAME.', '&OPTION_NAME.', '&OPTION_QUERY.', ")
.AppendLine("         mycode, ")
.AppendLine("         myerr); ")
.AppendLine(" END; "),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS5_2",
         new QueryTableEntry(new StringBuilder(" ----------- ")
.AppendLine()
.AppendLine("define OPTION_QUERY=PARTITION_OBJ_RECYCLEBIN ")
.AppendLine("DECLARE ")
.AppendLine("        mycode number; ")
.AppendLine("        myerr varchar2 (3000); ")
.AppendLine("        mycount number; ")
.AppendLine("BEGIN ")
.AppendLine("  delete from TMP_LMS_OPTIONS; ")
.AppendLine("  -- Using dynamic SQL in order to capture 'ORA-00942: table or view does not exist' error ")
.AppendLine("  EXECUTE IMMEDIATE ('insert into TMP_LMS_OPTIONS ")
.AppendLine("    ( MACHINE_ID, DB_NAME, TIMESTAMP, HOST_NAME, INSTANCE_NAME, OPTION_NAME, OPTION_QUERY, ")
.AppendLine("      COL010, ")
.AppendLine("      COL020, ")
.AppendLine("      COL030, ")
.AppendLine("      COL040, ")
.AppendLine("      COL050, ")
.AppendLine("      COL060, ")
.AppendLine("      COL070, ")
.AppendLine("      COL080, ")
.AppendLine("      COL090 ")
.AppendLine("    ) ")
.AppendLine("    select ")
.AppendLine("      ''&MACHINE_ID.'', ''&DB_NAME.'', ''&SYS_TIME.'', ''&HOST_NAME.'', ''&INSTANCE_NAME.'', ''&OPTION_NAME.'', ''&OPTION_QUERY.'', ")
.AppendLine("      OWNER, ")
.AppendLine("      ORIGINAL_NAME, ")
.AppendLine("      OBJECT_NAME, ")
.AppendLine("      TYPE, ")
.AppendLine("      CREATETIME, ")
.AppendLine("      DROPTIME, ")
.AppendLine("      PARTITION_NAME, ")
.AppendLine("      SPACE, ")
.AppendLine("      CAN_UNDROP ")
.AppendLine("    from ( ")
.AppendLine("      select OWNER, ORIGINAL_NAME, OBJECT_NAME, TYPE, CREATETIME, DROPTIME, PARTITION_NAME, SPACE, CAN_UNDROP ")
.AppendLine("        from DBA_RECYCLEBIN&DBL ")
.AppendLine("        where TYPE not like ''%Partition%'' ")
.AppendLine("          and (OWNER, OBJECT_NAME) in (select OWNER, OBJECT_NAME from DBA_RECYCLEBIN where TYPE like ''%Partition%'') ")
.AppendLine("      union all ")
.AppendLine("      select OWNER, ORIGINAL_NAME, OBJECT_NAME, TYPE, CREATETIME, DROPTIME, PARTITION_NAME, SPACE, CAN_UNDROP ")
.AppendLine("        from DBA_RECYCLEBIN&DBL ")
.AppendLine("        where TYPE like ''%Partition%'' ")
.AppendLine("         ) ")
.AppendLine("  '); ")
.AppendLine("  mycount := SQL%ROWCOUNT; ")
.AppendLine("  -- Recording 'no rows selected' case ")
.AppendLine("  IF mycount=0 THEN ")
.AppendLine("    INSERT INTO LMS_OPTIONS1C ")
.AppendLine("    ( MACHINE_ID, DB_NAME, TIMESTAMP, HOST_NAME, INSTANCE_NAME, OPTION_NAME, OPTION_QUERY, ")
.AppendLine("      SQL_ERR_CODE, ")
.AppendLine("      SQL_ERR_MESSAGE) ")
.AppendLine("    VALUES ")
.AppendLine("    ( '&MACHINE_ID.', '&DB_NAME.', '&SYS_TIME.', '&HOST_NAME.', '&INSTANCE_NAME.', '&OPTION_NAME.', '&OPTION_QUERY.', ")
.AppendLine("      0, ")
.AppendLine("      'no rows selected'); ")
.AppendLine("  ELSE ")
.AppendLine("    update TMP_LMS_OPTIONS set SQL_ERR_CODE=mycount, SQL_ERR_MESSAGE='count'; ")
.AppendLine("    insert into LMS_OPTIONS1C (select * from TMP_LMS_OPTIONS); ")
.AppendLine("  END IF; ")
.AppendLine("  dbms_output.put_line ('PL/SQL block completed for [&OPTION_NAME.].[&OPTION_QUERY.]. ' || mycount || ' rows collected.'); ")
.AppendLine("EXCEPTION ")
.AppendLine("    -- Recording Errors ")
.AppendLine("    when OTHERS then ")
.AppendLine("      mycode := SQLCODE; ")
.AppendLine("      myerr  := SQLERRM; ")
.AppendLine("      dbms_output.put_line ('PL/SQL block exception ['|| mycode ||'] for [&OPTION_NAME.].[&OPTION_QUERY.]'); ")
.AppendLine("      INSERT INTO LMS_OPTIONS1C ")
.AppendLine("      ( MACHINE_ID, DB_NAME, TIMESTAMP, HOST_NAME, INSTANCE_NAME, OPTION_NAME, OPTION_QUERY, ")
.AppendLine("        SQL_ERR_CODE, ")
.AppendLine("        SQL_ERR_MESSAGE) ")
.AppendLine("       VALUES ")
.AppendLine("      ( '&MACHINE_ID.', '&DB_NAME.', '&SYS_TIME.', '&HOST_NAME.', '&INSTANCE_NAME.', '&OPTION_NAME.', '&OPTION_QUERY.', ")
.AppendLine("         mycode, ")
.AppendLine("         myerr); ")
.AppendLine(" END; "),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS6",
         new QueryTableEntry(new StringBuilder(" ----------- ")
.AppendLine()
.AppendLine(" -- OLAP                -- " )
.AppendLine(" ------------------------- " )
.AppendLine(" -- ANALYTIC WORKSPACES -- " )
.AppendLine(" ------------------------- " )
.AppendLine(" define OPTION_NAME=OLAP " )
.AppendLine(" define OPTION_QUERY=ANALYTIC_WORKSPACES " )
.AppendLine(" DECLARE " )
.AppendLine(" mycode number; " )
.AppendLine(" myerr varchar2 (3000); " )
.AppendLine(" does_not_exist exception; " )
.AppendLine(" PRAGMA EXCEPTION_INIT(does_not_exist, -942); " )
.AppendLine(" BEGIN " )
.AppendLine(" begin " )
.AppendLine(" execute immediate ('insert into lms_options1c " )
.AppendLine(" ( " )
.AppendLine(" MACHINE_ID, " )
.AppendLine(" DB_NAME, " )
.AppendLine(" TIMESTAMP, " )
.AppendLine(" HOST_NAME, " )
.AppendLine(" INSTANCE_NAME, " )
.AppendLine(" OPTION_NAME, " )
.AppendLine(" OPTION_QUERY, " )
.AppendLine(" COL010, " )
.AppendLine(" COL020, " )
.AppendLine(" COL030, " )
.AppendLine(" COL040, " )
.AppendLine(" COL050 " )
.AppendLine(" ) " )
.AppendLine(" SELECT " )
.AppendLine(" ''&MACHINE_ID.'', " )
.AppendLine(" ''&DB_NAME.'', " )
.AppendLine(" ''&SYS_TIME.'', " )
.AppendLine(" ''&HOST_NAME.'', " )
.AppendLine(" ''&INSTANCE_NAME.'', " )
.AppendLine(" ''&OPTION_NAME.'', " )
.AppendLine(" ''&OPTION_QUERY.'', " )
.AppendLine(" OWNER, " )
.AppendLine(" AW_NUMBER, " )
.AppendLine(" AW_NAME, " )
.AppendLine(" PAGESPACES, " )
.AppendLine(" GENERATIONS " )
.AppendLine(" FROM " )
.AppendLine(" DBA_AWS&DBL'); " )
.AppendLine(" EXCEPTION " )
.AppendLine(" when OTHERS then " )
.AppendLine(" mycode := SQLCODE; " )
.AppendLine(" myerr := SQLERRM; " )
.AppendLine(" INSERT INTO LMS_OPTIONS1C " )
.AppendLine(" (MACHINE_ID, " )
.AppendLine(" DB_NAME, " )
.AppendLine(" TIMESTAMP, " )
.AppendLine(" HOST_NAME, " )
.AppendLine(" INSTANCE_NAME, " )
.AppendLine(" OPTION_NAME, " )
.AppendLine(" OPTION_QUERY, " )
.AppendLine(" SQL_ERR_CODE, " )
.AppendLine(" SQL_ERR_MESSAGE) " )
.AppendLine(" VALUES " )
.AppendLine(" ('&MACHINE_ID.', " )
.AppendLine(" '&DB_NAME.', " )
.AppendLine(" '&SYS_TIME.', " )
.AppendLine(" '&HOST_NAME.', " )
.AppendLine(" '&INSTANCE_NAME.', " )
.AppendLine(" '&OPTION_NAME.', " )
.AppendLine(" '&OPTION_QUERY.', " )
.AppendLine(" mycode, " )
.AppendLine(" myerr); " )
.AppendLine(" GOTO abc7; " )
.AppendLine(" end; " )
.AppendLine(" IF SQL%ROWCOUNT=0 THEN " )
.AppendLine(" INSERT INTO LMS_OPTIONS1C " )
.AppendLine(" (MACHINE_ID, " )
.AppendLine(" DB_NAME, " )
.AppendLine(" TIMESTAMP, " )
.AppendLine(" HOST_NAME, " )
.AppendLine(" INSTANCE_NAME, " )
.AppendLine(" OPTION_NAME, " )
.AppendLine(" OPTION_QUERY, " )
.AppendLine(" SQL_ERR_CODE, " )
.AppendLine(" SQL_ERR_MESSAGE " )
.AppendLine(" ) " )
.AppendLine(" VALUES " )
.AppendLine(" ('&MACHINE_ID.', " )
.AppendLine(" '&DB_NAME.', " )
.AppendLine(" '&SYS_TIME.', " )
.AppendLine(" '&HOST_NAME.', " )
.AppendLine(" '&INSTANCE_NAME.', " )
.AppendLine(" '&OPTION_NAME.', " )
.AppendLine(" '&OPTION_QUERY.', " )
.AppendLine(" 0, " )
.AppendLine(" 'no rows selected'); " )
.AppendLine(" END IF; " )
.AppendLine(" <<abc7>> " )
.AppendLine(" dbms_output.put_line ('PL/SQL block completed for [&OPTION_NAME.].[&OPTION_QUERY.]'); " )
.AppendLine(" END; " ),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS8",            
         new QueryTableEntry(new StringBuilder(" ------------------------------------- " )
.AppendLine()
.AppendLine(" -- RAC (REAL APPLICATION CLUSTERS) --" )
.AppendLine(" -------------------------------------" )
.AppendLine(" define OPTION_NAME=RAC" )
.AppendLine(" define OPTION_QUERY=GV$INSTANCE" )
.AppendLine(" DECLARE" )
.AppendLine("         mycode number;" )
.AppendLine("         myerr varchar2 (3000);" )
.AppendLine("         does_not_exist exception;" )
.AppendLine("         PRAGMA EXCEPTION_INIT(does_not_exist, -942);" )
.AppendLine(" BEGIN" )
.AppendLine(" begin" )
.AppendLine("   execute immediate ('insert into lms_options1c" )
.AppendLine("     (" )
.AppendLine("       MACHINE_ID," )
.AppendLine("       DB_NAME," )
.AppendLine("       TIMESTAMP," )
.AppendLine("       HOST_NAME," )
.AppendLine("       INSTANCE_NAME," )
.AppendLine("       OPTION_NAME," )
.AppendLine("       OPTION_QUERY," )
.AppendLine("       COL010," )
.AppendLine("       COL020," )
.AppendLine("       COL030," )
.AppendLine("       COL040" )
.AppendLine("     )" )
.AppendLine("     SELECT" )
.AppendLine("       ''&MACHINE_ID.''," )
.AppendLine("       ''&DB_NAME.''," )
.AppendLine("       ''&SYS_TIME.''," )
.AppendLine("       ''&HOST_NAME.''," )
.AppendLine("       ''&INSTANCE_NAME.''," )
.AppendLine("       ''&OPTION_NAME.''," )
.AppendLine("       ''&OPTION_QUERY.''," )
.AppendLine("       INSTANCE_NAME," )
.AppendLine("       HOST_NAME," )
.AppendLine("       INST_ID," )
.AppendLine("       STATUS" )
.AppendLine("     FROM" )
.AppendLine("       GV$INSTANCE&DBL');" )
.AppendLine("   EXCEPTION" )
.AppendLine("     when OTHERS then" )
.AppendLine("       mycode := SQLCODE;" )
.AppendLine("       myerr  := SQLERRM;" )
.AppendLine("       INSERT INTO LMS_OPTIONS1C" )
.AppendLine("        (MACHINE_ID," )
.AppendLine("         DB_NAME," )
.AppendLine("         TIMESTAMP," )
.AppendLine("   HOST_NAME," )
.AppendLine("         INSTANCE_NAME," )
.AppendLine("         OPTION_NAME," )
.AppendLine("         OPTION_QUERY," )
.AppendLine("         SQL_ERR_CODE," )
.AppendLine("         SQL_ERR_MESSAGE)" )
.AppendLine("        VALUES" )
.AppendLine("         ('&MACHINE_ID.'," )
.AppendLine("          '&DB_NAME.'," )
.AppendLine("          '&SYS_TIME.'," )
.AppendLine("          '&HOST_NAME.'," )
.AppendLine("          '&INSTANCE_NAME.'," )
.AppendLine("          '&OPTION_NAME.'," )
.AppendLine("          '&OPTION_QUERY.'," )
.AppendLine("          mycode," )
.AppendLine("          myerr);" )
.AppendLine("        GOTO abc8;" )
.AppendLine(" END;" )
.AppendLine(" IF SQL%ROWCOUNT=0 THEN" )
.AppendLine("   INSERT INTO LMS_OPTIONS1C" )
.AppendLine("    (MACHINE_ID," )
.AppendLine("     DB_NAME," )
.AppendLine("     TIMESTAMP," )
.AppendLine("     HOST_NAME," )
.AppendLine("     INSTANCE_NAME," )
.AppendLine("     OPTION_NAME," )
.AppendLine("     OPTION_QUERY," )
.AppendLine("     SQL_ERR_CODE," )
.AppendLine("     SQL_ERR_MESSAGE" )
.AppendLine("    )" )
.AppendLine("   VALUES" )
.AppendLine("    ('&MACHINE_ID.'," )
.AppendLine("     '&DB_NAME.'," )
.AppendLine("     '&SYS_TIME.'," )
.AppendLine("     '&HOST_NAME.'," )
.AppendLine("     '&INSTANCE_NAME.'," )
.AppendLine("     '&OPTION_NAME.'," )
.AppendLine("     '&OPTION_QUERY.'," )
.AppendLine("     0," )
.AppendLine("     'no rows selected');" )
.AppendLine(" END IF;" )
.AppendLine(" <<abc8>>" )
.AppendLine("   dbms_output.put_line ('PL/SQL block completed for [&OPTION_NAME.].[&OPTION_QUERY.]');" )
.AppendLine(" END; " ),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS9",            
         new QueryTableEntry(new StringBuilder(" -------------------- " )
.AppendLine()
.AppendLine(" -- LABEL SECURITY -- " )
.AppendLine(" -------------------- " )
.AppendLine(" define OPTION_NAME=LABEL_SECURITY " )
.AppendLine(" define OPTION_QUERY=LBAC$POLT_COUNT " )
.AppendLine(" DECLARE " )
.AppendLine(" mycode number; " )
.AppendLine(" myerr varchar2 (3000); " )
.AppendLine(" does_not_exist exception; " )
.AppendLine(" PRAGMA EXCEPTION_INIT(does_not_exist, -942); " )
.AppendLine(" BEGIN " )
.AppendLine(" begin " )
.AppendLine(" execute immediate ('insert into lms_options1c " )
.AppendLine(" ( " )
.AppendLine(" MACHINE_ID, " )
.AppendLine(" DB_NAME, " )
.AppendLine(" TIMESTAMP, " )
.AppendLine(" HOST_NAME, " )
.AppendLine(" INSTANCE_NAME, " )
.AppendLine(" OPTION_NAME, " )
.AppendLine(" OPTION_QUERY, " )
.AppendLine(" COL010 " )
.AppendLine(" ) " )
.AppendLine(" SELECT " )
.AppendLine(" ''&MACHINE_ID.'', " )
.AppendLine(" ''&DB_NAME.'', " )
.AppendLine(" ''&SYS_TIME.'', " )
.AppendLine(" ''&HOST_NAME.'', " )
.AppendLine(" ''&INSTANCE_NAME.'', " )
.AppendLine(" ''&OPTION_NAME.'', " )
.AppendLine(" ''&OPTION_QUERY.'', " )
.AppendLine(" count(*) " )
.AppendLine(" FROM " )
.AppendLine(" LBACSYS.LBAC$POLT&DBL " )
.AppendLine(" WHERE OWNER <> ''SA_DEMO'''); " )
.AppendLine(" EXCEPTION " )
.AppendLine(" when OTHERS then " )
.AppendLine(" mycode := SQLCODE; " )
.AppendLine(" myerr := SQLERRM; " )
.AppendLine(" INSERT INTO LMS_OPTIONS1C " )
.AppendLine(" (MACHINE_ID, " )
.AppendLine(" DB_NAME, " )
.AppendLine(" TIMESTAMP, " )
.AppendLine(" HOST_NAME, " )
.AppendLine(" INSTANCE_NAME, " )
.AppendLine(" OPTION_NAME, " )
.AppendLine(" OPTION_QUERY, " )
.AppendLine(" SQL_ERR_CODE, " )
.AppendLine(" SQL_ERR_MESSAGE) " )
.AppendLine(" VALUES " )
.AppendLine(" ('&MACHINE_ID.', " )
.AppendLine(" '&DB_NAME.', " )
.AppendLine(" '&SYS_TIME.', " )
.AppendLine(" '&HOST_NAME.', " )
.AppendLine(" '&INSTANCE_NAME.', " )
.AppendLine(" '&OPTION_NAME.', " )
.AppendLine(" '&OPTION_QUERY.', " )
.AppendLine(" mycode, " )
.AppendLine(" myerr); " )
.AppendLine(" GOTO abc9; " )
.AppendLine(" END; " )
.AppendLine(" IF SQL%ROWCOUNT=0 THEN " )
.AppendLine(" INSERT INTO LMS_OPTIONS1C " )
.AppendLine(" (MACHINE_ID, " )
.AppendLine(" DB_NAME, " )
.AppendLine(" TIMESTAMP, " )
.AppendLine(" HOST_NAME, " )
.AppendLine(" INSTANCE_NAME, " )
.AppendLine(" OPTION_NAME, " )
.AppendLine(" OPTION_QUERY, " )
.AppendLine(" SQL_ERR_CODE, " )
.AppendLine(" SQL_ERR_MESSAGE " )
.AppendLine(" ) " )
.AppendLine(" VALUES " )
.AppendLine(" ('&MACHINE_ID.', " )
.AppendLine(" '&DB_NAME.', " )
.AppendLine(" '&SYS_TIME.', " )
.AppendLine(" '&HOST_NAME.', " )
.AppendLine(" '&INSTANCE_NAME.', " )
.AppendLine(" '&OPTION_NAME.', " )
.AppendLine(" '&OPTION_QUERY.', " )
.AppendLine(" 0, " )
.AppendLine(" 'no rows selected'); " )
.AppendLine(" END IF; " )
.AppendLine(" <<abc9>> " )
.AppendLine(" dbms_output.put_line ('PL/SQL block completed for [&OPTION_NAME.].[&OPTION_QUERY.]'); " )
.AppendLine(" END; " ),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS10",            
         new QueryTableEntry(new StringBuilder(" ----------------------------------------- " )
.AppendLine()
.AppendLine(" -- OEM" )
.AppendLine(" ------------------------------------------" )
.AppendLine(" -- Check for running known OEM Programs --" )
.AppendLine(" ------------------------------------------" )
.AppendLine(" define OPTION_NAME=OEM" )
.AppendLine(" define OPTION_QUERY=RUNNING_PROGRAMS" )
.AppendLine(" DECLARE" )
.AppendLine("         mycode number;" )
.AppendLine("         myerr varchar2 (3000);" )
.AppendLine("         does_not_exist exception;" )
.AppendLine("         PRAGMA EXCEPTION_INIT(does_not_exist, -942);" )
.AppendLine(" BEGIN" )
.AppendLine(" begin" )
.AppendLine("   execute immediate ('insert into lms_options1c" )
.AppendLine("     (" )
.AppendLine("       MACHINE_ID," )
.AppendLine("       DB_NAME," )
.AppendLine("       TIMESTAMP," )
.AppendLine("       HOST_NAME," )
.AppendLine("       INSTANCE_NAME," )
.AppendLine("       OPTION_NAME," )
.AppendLine("       OPTION_QUERY," )
.AppendLine("       COL010" )
.AppendLine("     )" )
.AppendLine("     SELECT" )
.AppendLine("       ''&MACHINE_ID.''," )
.AppendLine("       ''&DB_NAME.''," )
.AppendLine("       ''&SYS_TIME.''," )
.AppendLine("       ''&HOST_NAME.''," )
.AppendLine("       ''&INSTANCE_NAME.''," )
.AppendLine("       ''&OPTION_NAME.''," )
.AppendLine("       ''&OPTION_QUERY.''," )
.AppendLine("       PROGRAM" )
.AppendLine("     FROM" )
.AppendLine("       V$SESSION&DBL" )
.AppendLine("       WHERE" )
.AppendLine("         upper(program) LIKE ''%XPNI.EXE%''" )
.AppendLine("         OR upper(program) LIKE ''%VMS.EXE%''" )
.AppendLine("         OR upper(program) LIKE ''%EPC.EXE%''" )
.AppendLine("         OR upper(program) LIKE ''%TDVAPP.EXE%''" )
.AppendLine("         OR upper(program) LIKE ''VDOSSHELL%''" )
.AppendLine("         OR upper(program) LIKE ''%VMQ%''" )
.AppendLine("         OR upper(program) LIKE ''%VTUSHELL%''" )
.AppendLine("         OR upper(program) LIKE ''%JAVAVMQ%''" )
.AppendLine("         OR upper(program) LIKE ''%XPAUTUNE%''" )
.AppendLine("         OR upper(program) LIKE ''%XPCOIN%''" )
.AppendLine("         OR upper(program) LIKE ''%XPKSH%''" )
.AppendLine("         OR upper(program) LIKE ''%XPUI%''');" )
.AppendLine("   EXCEPTION" )
.AppendLine("     when OTHERS then" )
.AppendLine("       mycode := SQLCODE;" )
.AppendLine("       myerr  := SQLERRM;" )
.AppendLine("       INSERT INTO LMS_OPTIONS1C" )
.AppendLine("        (MACHINE_ID," )
.AppendLine("         DB_NAME," )
.AppendLine("         TIMESTAMP," )
.AppendLine("   HOST_NAME," )
.AppendLine("         INSTANCE_NAME," )
.AppendLine("         OPTION_NAME," )
.AppendLine("         OPTION_QUERY," )
.AppendLine("         SQL_ERR_CODE," )
.AppendLine("         SQL_ERR_MESSAGE)" )
.AppendLine("        VALUES" )
.AppendLine("         ('&MACHINE_ID.'," )
.AppendLine("          '&DB_NAME.'," )
.AppendLine("          '&SYS_TIME.'," )
.AppendLine("          '&HOST_NAME.'," )
.AppendLine("          '&INSTANCE_NAME.'," )
.AppendLine("          '&OPTION_NAME.'," )
.AppendLine("          '&OPTION_QUERY.'," )
.AppendLine("          mycode," )
.AppendLine("          myerr);" )
.AppendLine("        GOTO abc10;" )
.AppendLine(" END;" )
.AppendLine(" IF SQL%ROWCOUNT=0 THEN" )
.AppendLine("   INSERT INTO LMS_OPTIONS1C" )
.AppendLine("    (MACHINE_ID," )
.AppendLine("     DB_NAME," )
.AppendLine("     TIMESTAMP," )
.AppendLine("     HOST_NAME," )
.AppendLine("     INSTANCE_NAME," )
.AppendLine("     OPTION_NAME," )
.AppendLine("     OPTION_QUERY," )
.AppendLine("     SQL_ERR_CODE," )
.AppendLine("     SQL_ERR_MESSAGE" )
.AppendLine("    )" )
.AppendLine("   VALUES" )
.AppendLine("    ('&MACHINE_ID.'," )
.AppendLine("     '&DB_NAME.'," )
.AppendLine("     '&SYS_TIME.'," )
.AppendLine("     '&HOST_NAME.'," )
.AppendLine("     '&INSTANCE_NAME.'," )
.AppendLine("     '&OPTION_NAME.'," )
.AppendLine("     '&OPTION_QUERY.'," )
.AppendLine("     0," )
.AppendLine("     'no rows selected');" )
.AppendLine(" END IF;" )
.AppendLine(" <<abc10>>" )
.AppendLine("   dbms_output.put_line ('PL/SQL block completed for [&OPTION_NAME.].[&OPTION_QUERY.]');" )
.AppendLine(" END; " ),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"LMS_OPTIONS10b",            
         new QueryTableEntry(new StringBuilder(" ---------------------------------------------------------- " )
.AppendLine()
.AppendLine(" -- PL/SQL anonymous block to Check for known OEM tables --" )
.AppendLine(" ----------------------------------------------------------" )
.AppendLine(" define OPTION_NAME=OEM" )
.AppendLine(" define OPTION_QUERY=REPOSITORY" )
.AppendLine(" DECLARE" )
.AppendLine("     cursor1 integer;" )
.AppendLine("     v_count number(1);" )
.AppendLine("     v_schema dba_tables.owner%TYPE;" )
.AppendLine("     v_version varchar2(10);" )
.AppendLine("     v_component varchar2(20);" )
.AppendLine("     v_i_name varchar2(10);" )
.AppendLine("     v_h_name varchar2(30);" )
.AppendLine("     stmt varchar2(200);" )
.AppendLine("     rows_processed integer;" )
.AppendLine("     CURSOR schema_array IS" )
.AppendLine("     SELECT owner" )
.AppendLine("     FROM dba_tables&DBL WHERE table_name = 'SMP_REP_VERSION';" )
.AppendLine("     CURSOR schema_array_v2 IS" )
.AppendLine("     SELECT owner" )
.AppendLine("     FROM dba_tables&DBL WHERE table_name = 'SMP_VDS_REPOS_VERSION';" )
.AppendLine(" BEGIN" )
.AppendLine("     -- SELECT instance_name,host_name INTO v_i_name, v_h_name FROM v$instance&DBL; !!!! user does not have enough rights to access DBA_REGISTRY object" )
.AppendLine("     OPEN schema_array;" )
.AppendLine("     OPEN schema_array_v2;" )
.AppendLine("     cursor1 := dbms_sql.open_cursor;" )
.AppendLine("     v_count := 0;" )
.AppendLine("     LOOP -- this loop steps through each valid schema." )
.AppendLine("        FETCH schema_array INTO v_schema;" )
.AppendLine("        EXIT WHEN schema_array%notfound;" )
.AppendLine("        v_count := v_count + 1;" )
.AppendLine("        dbms_sql.parse(cursor1,'select c_current_version, c_component from '||v_schema||'.smp_rep_version&DBL', dbms_sql.native);" )
.AppendLine("        dbms_sql.define_column(cursor1, 1, v_version, 10);" )
.AppendLine("        dbms_sql.define_column(cursor1, 2, v_component, 20);" )
.AppendLine("        rows_processed:=dbms_sql.execute ( cursor1 );" )
.AppendLine("        loop -- to step through cursor1 to find console version." )
.AppendLine("           if dbms_sql.fetch_rows(cursor1) >0 then" )
.AppendLine("              dbms_sql.column_value (cursor1, 1, v_version);" )
.AppendLine("              dbms_sql.column_value (cursor1, 2, v_component);" )
.AppendLine("              if v_component = 'CONSOLE' then" )
.AppendLine("   --             dbms_output.put_line ('GREP_PREFIX.,OEM,REPOSITORY1,'||v_count||',dbms_output,Schema '||rpad(v_schema,15)||' has a repository version '||v_version||'');" )
.AppendLine("                 INSERT INTO LMS_OPTIONS1C" )
.AppendLine("                  (MACHINE_ID," )
.AppendLine("                   DB_NAME," )
.AppendLine("                   TIMESTAMP," )
.AppendLine("       HOST_NAME," )
.AppendLine("                   INSTANCE_NAME," )
.AppendLine("                   OPTION_NAME," )
.AppendLine("                   OPTION_QUERY," )
.AppendLine("                   SQL_ERR_CODE," )
.AppendLine("                   SQL_ERR_MESSAGE," )
.AppendLine("                   COL010)" )
.AppendLine("                 VALUES" )
.AppendLine("                  ('&MACHINE_ID.'," )
.AppendLine("                   '&DB_NAME.'," )
.AppendLine("                   '&SYS_TIME.'," )
.AppendLine("                   '&HOST_NAME.'," )
.AppendLine("                   '&INSTANCE_NAME.'," )
.AppendLine("                   '&OPTION_NAME.'," )
.AppendLine("                   'REPOSITORY1'," )
.AppendLine("                   0," )
.AppendLine("                   NULL," )
.AppendLine("                   'Schema '||rpad(v_schema,15)||' has a repository version '||v_version||'');" )
.AppendLine("                 exit;" )
.AppendLine("              end if;" )
.AppendLine("           else" )
.AppendLine("              exit;" )
.AppendLine("           end if;" )
.AppendLine("        end loop;" )
.AppendLine("     END LOOP;" )
.AppendLine("     LOOP -- this loop steps through each valid V2 schema." )
.AppendLine("       FETCH schema_array_v2 INTO v_schema;" )
.AppendLine("       EXIT WHEN schema_array_v2%notfound;" )
.AppendLine("       v_count := v_count + 1;" )
.AppendLine("       --dbms_output.put_line ('GREP_PREFIX.,OEM,REPOSITORY2,'||v_count||',dbms_output,Schema '||rpad(v_schema,15)||' has a repository version 2.x' );" )
.AppendLine("       INSERT INTO LMS_OPTIONS1C" )
.AppendLine("        (MACHINE_ID," )
.AppendLine("         DB_NAME," )
.AppendLine("         TIMESTAMP," )
.AppendLine("   HOST_NAME," )
.AppendLine("         INSTANCE_NAME," )
.AppendLine("         OPTION_NAME," )
.AppendLine("         OPTION_QUERY," )
.AppendLine("         SQL_ERR_CODE," )
.AppendLine("         SQL_ERR_MESSAGE," )
.AppendLine("         COL010)" )
.AppendLine("       VALUES" )
.AppendLine("        ('&MACHINE_ID.'," )
.AppendLine("         '&DB_NAME.'," )
.AppendLine("         '&SYS_TIME.'," )
.AppendLine("         '&HOST_NAME.'," )
.AppendLine("         '&INSTANCE_NAME.'," )
.AppendLine("         '&OPTION_NAME.'," )
.AppendLine("         'REPOSITORY2'," )
.AppendLine("         0," )
.AppendLine("         NULL," )
.AppendLine("         'Schema '||rpad(v_schema,15)||' has a repository version 2.x');" )
.AppendLine("     end loop;" )
.AppendLine("     dbms_sql.close_cursor (cursor1);" )
.AppendLine("     close schema_array;" )
.AppendLine("     close schema_array_v2;" )
.AppendLine("     if v_count = 0 then" )
.AppendLine("       --dbms_output.put_line ('GREP_PREFIX.,OEM,NO_REPOSITORY,'||v_count||',dbms_output,There are NO OEM repositories with version prior to 10g on this instance - '||v_i_name||' on host '||v_h_name||'');" )
.AppendLine("       INSERT INTO LMS_OPTIONS1C" )
.AppendLine("        (MACHINE_ID," )
.AppendLine("         DB_NAME," )
.AppendLine("         TIMESTAMP," )
.AppendLine("   HOST_NAME," )
.AppendLine("         INSTANCE_NAME," )
.AppendLine("         OPTION_NAME," )
.AppendLine("         OPTION_QUERY," )
.AppendLine("         SQL_ERR_CODE," )
.AppendLine("         SQL_ERR_MESSAGE," )
.AppendLine("         COL010)" )
.AppendLine("       VALUES" )
.AppendLine("        ('&MACHINE_ID.'," )
.AppendLine("         '&DB_NAME.'," )
.AppendLine("         '&SYS_TIME.'," )
.AppendLine("         '&HOST_NAME.'," )
.AppendLine("         '&INSTANCE_NAME.'," )
.AppendLine("         '&OPTION_NAME.'," )
.AppendLine("         'REPOSITORY2'," )
.AppendLine("         0," )
.AppendLine("         NULL," )
.AppendLine("         'There are NO OEM repositories with version prior to 10g on this instance - '||v_i_name||' on host '||v_h_name||'');" )
.AppendLine("     end if;" )
.AppendLine(" END; " ),
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"lmsOptions1c",           
         new QueryTableEntry(new StringBuilder("SELECT '<BDNA=>MACHINE_ID='||MACHINE_ID||'<BDNA,1>DB_NAME='||DB_NAME||'<BDNA,1>TIMESTAMP='||TIMESTAMP" +
                  @"||'<BDNA,1>HOST_NAME='||HOST_NAME||'<BDNA,1>INSTANCE_NAME='||INSTANCE_NAME||'<BDNA,1>OPTION_NAME='||OPTION_NAME" +
                  @"||'<BDNA,1>OPTION_QUERY='||OPTION_QUERY||'<BDNA,1>SQL_ERR_CODE='||SQL_ERR_CODE||'<BDNA,1>SQL_ERR_MESSAGE='||SQL_ERR_MESSAGE" +
                  @"||'<BDNA,1>COL010='||COL010||'<BDNA,1>COL020='||COL020||'<BDNA,1>COL030='||COL030" +
                  @"||'<BDNA,1>COL040='||COL040||'<BDNA,1>COL050='||COL050||'<BDNA,1>COL060='||COL060" +
                  @"||'<BDNA,1>COL070='||COL070||'<BDNA,1>COL080='||COL080||'<BDNA,1>COL090='||COL090" +
                  @"||'<BDNA,1>COL100='||COL100||'<=BDNA>' FROM LMS_OPTIONS1C "),
         all_ver_pattern,
         new QueryResultHandler(RawDataQueryValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"dropLMSOptions",            
         new QueryTableEntry(new StringBuilder("DROP TABLE LMS_OPTIONS1C;"),                              
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler))),

         new KeyValuePair<string, QueryTableEntry>(@"drop_tmpLMSOptions",            
         new QueryTableEntry(new StringBuilder("DROP TABLE TMP_LMS_OPTIONS;"),                              
         all_ver_pattern,
         new QueryResultHandler(RawDataValueHandler)))
        };

        /// <summary>Map of supported attribute names to associated query strings.</summary>
        private static readonly IDictionary<string, QueryTableEntry> s_attributeMap = new Dictionary<string, QueryTableEntry>();
    }
}

