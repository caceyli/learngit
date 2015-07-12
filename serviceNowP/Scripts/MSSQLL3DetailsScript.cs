#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.15 $
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
    public class MSSQLL3DetailsScript : ICollectionScriptRuntime {

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
            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_BAD_COLLECTION_SCRIPT;
            DbConnection dbConnection = null;
            CollectionScriptResults result = null;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script MSSQLL3DetailsScript.",
                                  m_taskId);

            try {

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
                            this.BuildDataRow();
                        }
                    }
                }

                result = new CollectionScriptResults
                    (resultCode, 0, null, null, null, false, m_dataRow.ToString());
            }
            catch (Exception e) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unhandled exception in MSSQLL3DetailsScript.\n{1}",
                                      m_taskId,
                                      e.ToString());
                result = new CollectionScriptResults
                    (ResultCodes.RC_PROCESS_EXEC_FAILED, 0, null, null, null, false, null);
            }

            Debug.Assert(null != result);
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Connection script MSSQLL3DetailsScript.  Elapsed time {1}.  Result code {2}.",
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
        private void BuildDataRow() {
            foreach (KeyValuePair<string, string> kvp in m_databaseProperties) {
                string attributeName = kvp.Key, attributeValue = kvp.Value;
                if (!m_attributes.ContainsKey(attributeName)) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                          m_taskId,
                                          attributeName);
                    continue;
                }
                if (String.IsNullOrEmpty(attributeValue)) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attribute value for \"{1}\" is null.",
                                          m_taskId,
                                          attributeValue);
                    continue;
                }
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG)
                         .Append(attributeValue)
                         .Append(BdnaDelimiters.END_TAG);
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
                                      "Task Id {0}: Executing sql command select @@version {1} on {2}.",
                                      m_taskId,
                                      dbConnection.Database,
                                      dbConnection.DataSource);
                sw.Start();
                DbCommand cmd = dbConnection.CreateCommand();
                cmd.CommandText = @"select @@VERSION as VERSION";
                cmd.CommandType = CommandType.Text;
                DbDataReader reader = cmd.ExecuteReader();
                Debug.Assert(reader != null);
                using (reader) {
                    for (recordCount = 0; reader.Read(); recordCount++) {
                        String versionString = reader[@"version"] as string;
                        Match matchVersionString = s_versionRegex.Match(versionString);
                        if (matchVersionString.Success) {
                            this.addDatabaseProperties(@"version", matchVersionString.Groups[1].Value);
                        }
                    }
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Processing select @@version complete.  Elapsed time {1}.\nTotal record count {2}.",
                                          m_taskId,
                                          sw.Elapsed.ToString(),
                                          recordCount.ToString());
                }

                // The query below is expected to fail for SQL Server version 7.0 or earlier
                try {
                    DbCommand cmd2 = dbConnection.CreateCommand();
                    cmd2.CommandText =
                      @"select SERVERPROPERTY('PRODUCTVERSION') as PRODUCTVERSION, "+
                      @"SERVERPROPERTY('PRODUCTLEVEL') as PRODUCTLEVEL, "+
                      @"SERVERPROPERTY('EDITION') as EDITION, "+
                      @"SERVERPROPERTY('ENGINE EDITION') as ENGINEEDITION, "+
                      //@"SERVERPROPERTY('SERVERNAME') as SERVERNAME, " +
                      //@"SERVERPROPERTY('INSTANCENAME') as INSTANCENAME, " +
                      @"SERVERPROPERTY('LICENSETYPE') as LICENSETYPE, "+
                      @"SERVERPROPERTY('NUMLICENSES') as NUMLICENSES, "+
                      @"SERVERPROPERTY('ComputerNamePhysicalNetBIOS') as ComputerNamePhysicalNetBIOS, "+
                      @"SERVERPROPERTY('ISFULLTEXTINSTALLED') as ISFULLTEXTINSTALLED, "+
                      @"SERVERPROPERTY('ISSINGLEUSER') as ISSINGLEUSER, "+
                      @"SERVERPROPERTY('ISSYNCWITHBACKUP') as ISSYNCWITHBACKUP, "+
                      @"SERVERPROPERTY('ISCLUSTERED') as ISCLUSTERED, " +
                      @"* FROM fn_servershareddrives(),fn_virtualservernodes() ";

                    String alexText = cmd2.CommandText;
                    cmd2.CommandType = CommandType.Text;
                    DbDataReader reader2 = cmd2.ExecuteReader();
                    Debug.Assert(reader2 != null);
                    using (reader2) {
                        String NodeNames = null;
                        for (recordCount = 0; reader2.Read(); recordCount++) {
                            // Version
                            this.addDatabaseProperties("version", reader2[@"PRODUCTVERSION"] as string);

                            // Service pack
                            String productLevel = reader2[@"PRODUCTLEVEL"].ToString();
                            Match matchServicePack = s_servicePackRegex.Match(productLevel);
                            if (matchServicePack.Success) {
                                this.addDatabaseProperties(@"servicePack", @"Service Pack "+matchServicePack.Groups[1].Value);
                            } 
                            Match matchBeta = s_betaRegex.Match(productLevel);
                            if (matchBeta.Success) {
                                this.addDatabaseProperties(@"servicePack", @"Beta "+matchBeta.Groups[1].Value);
                            }

                            this.addDatabaseProperties("edition", reader2[@"EDITION"].ToString());
                            this.addDatabaseProperties("engineEdition", reader2[@"ENGINEEDITION"].ToString());
                            //this.addDatabaseProperties("serverName", reader2[@"SERVERNAME"].ToString());
                            //this.addDatabaseProperties("instanceName", reader2[@"INSTANCENAME"].ToString());
                            this.addDatabaseProperties("numLicenses", reader2[@"NUMLICENSES"].ToString());
                            this.addDatabaseProperties("ComputerNamePhysicalNetBIOS", reader2[@"ComputerNamePhysicalNetBIOS"].ToString());
                            this.addDatabaseProperties("isFullTextInstalled", reader2[@"ISFULLTEXTINSTALLED"].ToString().Equals("1").ToString());
                            this.addDatabaseProperties("isSingleUserMode", reader2[@"ISSINGLEUSER"].ToString().Equals("1").ToString());
                            this.addDatabaseProperties("isSyncWithBackup", reader2[@"ISSYNCWITHBACKUP"].ToString().Equals("1").ToString());
                            this.addDatabaseProperties("isClustered", reader2[@"ISCLUSTERED"].ToString().Equals("1").ToString());
                            if (!String.IsNullOrEmpty(reader2[@"DriveName"].ToString()))
                            {
                                this.addDatabaseProperties("DriveName", reader2[@"DriveName"].ToString());
                            }
                            else
                            {
                                this.addDatabaseProperties("DriveName", "Not Applicable");
                            }
                            if (String.IsNullOrEmpty(NodeNames))
                            {
                                NodeNames = reader2[@"NodeName"].ToString();
                            }
                            else
                            {
                                NodeNames = NodeNames + @" ";
                                NodeNames = NodeNames + reader2[@"NodeName"].ToString();
                            }
                        }
                        int NumNodes = recordCount;
                        this.addDatabaseProperties("NumNodes", NumNodes.ToString());
                        this.addDatabaseProperties("NodesOfCluster", NodeNames);
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Processing select Serverproperty complete.  Elapsed time {1}.\n{2}",
                                              m_taskId,
                                              sw.Elapsed.ToString(),
                                              recordCount.ToString());
                    }
                } catch (Exception ex) {
                    Lib.LogException(m_taskId, sw, "SQL Command Failed", ex);
                }




                resultCode = ResultCodes.RC_SUCCESS;
            }
            catch (DbException dbException) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: SQL Command Failed!  Elapsed time {1}.\n{2}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      FormatDbException(dbException));
            }
            catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: SQL Command Failed!  Elapsed time {1}.\nRecords processed {2}\n{3}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      recordCount.ToString(),
                                      ex.ToString());
            }
            return resultCode;
        }

        private String FormatDbException
            (DbException dbex) {
            StringBuilder sb = new StringBuilder();
            sb.Append("HRESULT: ").AppendLine(dbex.ErrorCode.ToString())
              .Append("Message: ").AppendLine(dbex.Message);
            if (!String.IsNullOrEmpty(dbex.HelpLink)) {
                sb.Append("Help Link: ").AppendLine(dbex.HelpLink);
            }

            if (!String.IsNullOrEmpty(dbex.Source)) {
                sb.Append("Source: ").AppendLine(dbex.Source);
            }

            if (null != dbex.TargetSite) {
                sb.Append("Target Site:").AppendLine(dbex.TargetSite.ToString());
            }

            if (null != dbex.InnerException) {
                sb.AppendLine("Inner exception:").AppendLine(dbex.ToString());
            }
            sb.AppendLine("StackTrace:").AppendLine(dbex.StackTrace);
            return sb.ToString();
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
        private void addDatabaseProperties(string propertyName, string propertyValue) {
            m_databaseProperties[propertyName] = propertyValue;
        }
        private IDictionary<string, string> m_databaseProperties = new Dictionary<string, string>();

        /// <summary>Database server name.</summary>
        private string m_dbServerName;

        ///// <summary>List of processed records needed to generate final data row.</summary>
        //private List<string> m_dataRowEntries = new List<string>();

        private static readonly Regex s_versionRegex = new Regex(@"Microsoft SQL Server.*-\s*(\S+)",
                                                                 RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_servicePackRegex = new Regex(@"SP(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_betaRegex = new Regex(@"B(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
