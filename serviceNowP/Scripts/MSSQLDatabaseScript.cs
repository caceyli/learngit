#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.23 $
*           $Date: 2014/07/16 23:02:42 $
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
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Scrip to perform level 3 collection of Microsoft SQL Server databases.
    /// </summary>
    public class MSSQLDatabaseScript : ICollectionScriptRuntime {
        
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
        /// 
        public CollectionScriptResults ExecuteTask(
                long                            taskId,
                long                            cleId,
                long                            elementId,
                long                            databaseTimestamp,
                long                            localTimestamp,
                IDictionary<string, string>     attributes,
                IDictionary<string, string>     scriptParameters,
                IDictionary<string, object>     connection,
                string                          tftpPath,
                string                          tftpPath_login,
                string                          tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {

            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_BAD_COLLECTION_SCRIPT;
            DbConnection dbConnection = null;
            CollectionScriptResults result = null;

            try {
                    Lib.Logger.TraceEvent(TraceEventType.Start,
                                          0,
                                          "Task Id {0}: Collection script MSSQLDatabase.",
                                          m_taskId);

                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                }
                else {
                    //Check connection attributes
                    if (connection.ContainsKey("dbConnection")) {
                        dbConnection = connection[@"dbConnection"] as DbConnection;
                    }
                    else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing dbConnection script parameter.",
                                              m_taskId);
                    }
                }

                if (dbConnection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                }
                else if (dbConnection.State != ConnectionState.Open) {
                    resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                }
                else {
                    using (dbConnection) {
                        m_dbServerName = dbConnection.DataSource;
                        Debug.Assert(null != m_dbServerName);
                        resultCode = GetDatabases(dbConnection);
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            this.BuildDataRow(@"MSSQLL3DatabaseDetails");
                        }
                    }
                }

                result = new CollectionScriptResults
                    (resultCode, 0, null, null, null, false, m_dataRow.ToString());
            }
            catch (Exception e) {
                // This is really an unanticipated fail safe.
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in MSSQLDatabase",
                                 e);
                result = new CollectionScriptResults
                    (ResultCodes.RC_PROCESS_EXEC_FAILED, 0, null, null, null, false, null);
            }
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script MSSQLDatabase.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        #endregion ICollectionScriptRuntime Members

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            } else if (0 == m_databases.Count) {
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
                         .Append(BdnaDelimiters.BEGIN_TAG);
                StringBuilder builder = new StringBuilder();
                foreach (KeyValuePair<string, IDictionary<string, string>> record in m_databases) {
                    builder.Append(BdnaDelimiters.DELIMITER1_TAG);
                    string databaseName = record.Key.ToString();
                    IDictionary<string, string> details = record.Value;
                    foreach (KeyValuePair<string, string> entry in details) {
                        builder.Append(BdnaDelimiters.DELIMITER2_TAG);
                        builder.Append(entry.Key);
                        if (entry.Key.ToString().Equals(@"Name")) {
                            builder.Append(@"(").Append(m_dbServerName).Append(@")");
                        }
                        builder.Append(@"=").Append(entry.Value);
                    }
                }

                m_dataRow.Append(builder);
                m_dataRow.Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary>
        /// Collect information about the databases on the remote host.
        /// </summary>
        /// 
        /// <param name="dbConnection">Connection to database server.</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes GetDatabases(DbConnection dbConnection) {
            ResultCodes resultCode = ResultCodes.RC_SQL_SERVER_DATABASE_QUERY_ERROR;
            Stopwatch sw = new Stopwatch();
            int recordCount = 0;
            try {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Calling stored procedure sp_helpdb for {1} on {2}",
                                      m_taskId,
                                      dbConnection.Database,
                                      dbConnection.DataSource);
                sw.Start();                
                DbCommand cmd = dbConnection.CreateCommand();
                cmd.CommandText = @"sp_helpdb";
                cmd.CommandType = CommandType.StoredProcedure;
                DbDataReader reader = cmd.ExecuteReader();
                Debug.Assert(reader != null);

                using (reader) {
                    for(recordCount = 0; reader.Read(); recordCount++) {
                        string dbName = reader[@"name"] as string;

                        if (!String.IsNullOrEmpty(dbName)) {
                            Dictionary<string, string> record = new Dictionary<string, string>();
                            record[@"Name"] = dbName.Trim();
                            record[@"Size"] = reader[@"db_size"].ToString().Trim();
                            record[@"Owner"] = reader[@"owner"].ToString().Trim();
                            record[@"CreatedDate"] = reader[@"created"].ToString().Trim();
                            string dbStatus = reader[@"status"].ToString().Trim();
                            Match match = s_statusRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"Status"] = value.Substring(0, value.Length - 1);
                                } else {
                                    record[@"Status"] = value;
                                }
                            }
                            match = s_updateabilityRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"Updateability"] = value.Substring(0, value.Length - 1);
                                } else {
                                    record[@"Updateability"] = value;
                                }
                            }
                            match = s_userAccessRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"UserAccess"] = value.Substring(0, value.Length - 1);
                                } else {
                                    record[@"UserAccess"] = value;
                                }
                            }
                            match = s_recoverRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"Recovery"] = value.Substring(0, value.Length - 1);
                                } else {
                                    record[@"Recovery"] = value;
                                }
                            }
                            match = s_versionRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"Version"] = value.Substring(0, value.Length-1);
                                } else {
                                    record[@"Version"] = value;
                                }
                            }
                            match = s_collationRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"Collation"] = value.Substring(0, value.Length - 1);
                                } else {
                                    record[@"Collation"] = value;
                                }
                            }
                            match = s_sqlSortOrderRegex.Match(dbStatus);
                            if (match.Success) {
                                string value = match.Groups[1].Value.ToString().Trim();
                                if (value.EndsWith(@",")) {
                                    record[@"SQLSortOrder"] = value.Substring(0, value.Length - 1);
                                }
                                else {
                                    record[@"SQLSortOrder"] = value;
                                }
                            }
                            if (s_autoCreateStatisticsRegex.IsMatch(dbStatus)) {
                                record[@"IsAutoCreateStatistics"] = @"True";
                            } else {
                                record[@"IsAutoCreateStatistics"] = @"False";
                            }
                            if (s_autoUpdateStatisticsRegex.IsMatch(dbStatus)) {
                                record[@"IsAutoUpdateStatistics"] = @"True";
                            } else {
                                record[@"IsAutoUpdateStatistics"] = @"False";
                            }
                            if (s_fullTextEnabledRegex.IsMatch(dbStatus)) {
                                record[@"IsFullTextEnabled"] = @"True";
                            } else {
                                record[@"IsFullTextEnabled"] = @"False";
                            }
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Adding database record {1}.",
                                                  m_taskId,
                                                  dbName);
                            this.addDatabaseDetails(dbName, record);
                        }
                    }
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Processing of {1} on {2} complete.\nElapsed time: {3}\nTotal record count: {4}.",
                                          m_taskId,
                                          dbConnection.Database,
                                          dbConnection.DataSource,
                                          sw.Elapsed.ToString(),
                                          recordCount.ToString());
                }

                resultCode = ResultCodes.RC_SUCCESS;
            } catch (DbException dbException) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: SQL Command Failed!!!\nElapsed time: {1}\nHRESULT: {2}\nMessage: {3}\nHelp Link: {4}\nSource: {5}\nTarget Site: {6}\nStack:\n{7}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      dbException.ErrorCode.ToString(),
                                      dbException.Message,
                                      dbException.HelpLink,
                                      dbException.Source,
                                      dbException.TargetSite.ToString(),
                                      dbException.StackTrace);
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: SQL Command Failed!!!\nElapsed time: {1}\nRecord Count: {2} records processed\n{3}.",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      recordCount.ToString(),
                                      ex.ToString());
            }
            return resultCode;
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

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>
        /// Add Database Dictionary to final result.
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="details"></param>
        private void addDatabaseDetails(string databaseName, IDictionary<string, string> details) {
            m_databases[databaseName] = details;
        }
        private IDictionary<string, IDictionary<string, string>> m_databases 
            = new Dictionary<string, IDictionary<string, string>>();

        /// <summary>Database server name.</summary>
        private string m_dbServerName;

        ///// <summary>List of processed records needed to generate final data row.</summary>
        //private List<string> m_dataRowEntries = new List<string>();

        private static readonly Regex s_statusRegex = new Regex(@"Status=(\S+),", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_updateabilityRegex = new Regex(@"Updateability=(\S+),?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_userAccessRegex = new Regex(@"UserAccess=(\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_recoverRegex = new Regex(@"Recovery=(\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_versionRegex = new Regex(@"Version=(\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_collationRegex = new Regex(@"Collation=(\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_sqlSortOrderRegex = new Regex(@"SQLSortOrder=(\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_autoCreateStatisticsRegex = new Regex(@"IsAutoCreateStatistics", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_autoUpdateStatisticsRegex = new Regex(@"IsAutoUpdateStatistics", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_fullTextEnabledRegex = new Regex(@"IsFullTextEnabled", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
