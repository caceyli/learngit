#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.42 $
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
* outside of bDNA Corporation Inc. without written
* permission from BDNA.  The code may be covered by patents,
* patents pending, or patents applied for in the US or elsewhere.
*
******************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Data;
//using System.Data.OleDb;
using System.Data.Common;
//using System.Data.SqlClient;
using System.Diagnostics;
using System.Management;
using System.Text;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Class to generate an OLE database connection to a running
    /// SQL Server instance.
    /// </summary>
    public class MSSQLDbConnectionScript : IConnectionScriptRuntime {

        /// <summary>Connect method invoked by WinCs. </summary>
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="connectionParameterSets">List of credential sets</param>
        /// <param name="tftpDispatcher">Tftp Listener</param>
        /// <returns>Operation results.</returns>
        public ConnectionScriptResults Connect(
                long taskId,
                IDictionary<string, string>[] connectionParameterSets,
                string tftpPath,
                string tftpPath_login,
                string tftpPath_password,
                ITftpDispatcher tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Connection script MSSQLDbConnectionScript.",
                                  m_taskId);
            ConnectionScriptResults result = null;

            if (null == connectionParameterSets) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Null credential set passed to MSSQLDbConnectionScript",
                                      m_taskId);
                result = new ConnectionScriptResults(null,
                                                     ResultCodes.RC_NULL_PARAMETER_SET,
                                                     0,
                                                     -1);
            } else {
                try {
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Executing MSSQLDbConnectionScript with {1} credential sets.",
                                          m_taskId,
                                          connectionParameterSets.Length.ToString());

                    //
                    // Loop to process credential sets until a successful
                    // connection is made.
                    for (int i = 0;
                         connectionParameterSets.Length > i;
                         ++i) {
                        
                        try {

                            Dictionary<string, object> connectionDic = new Dictionary<string, object>();
                            foreach (KeyValuePair<string, string> kvp in connectionParameterSets[i]) {
                                connectionDic.Add(kvp.Key, kvp.Value);
                            }
                            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
                            StringBuilder stdoutData = new StringBuilder();

                            //
                            // Using OLEDB Connection as default provider for SQL Server connection
                            // Reason is that SQL Server provider is tune for SQL Server 7.0+ only.
                            //  Our FP should maintain compatibility with earlier releases.
                            //
                            string strDbServerName = connectionParameterSets[i][@"hostName"];
                            connectionDic[@"hostName"] = strDbServerName;
                            string strDbInstanceName = connectionParameterSets[i][@"instanceName"];
                            if (!String.IsNullOrEmpty(strDbInstanceName)) {
                                if (strDbInstanceName.ToUpper() != @"MSSQLSERVER") {
                                    strDbServerName = strDbServerName + @"\" + strDbInstanceName;
                                } 
                            }
                            string strClusterIP = connectionParameterSets[i][@"clusterIP"];
                            string strClusterName = connectionParameterSets[i][@"clusterName"];
                            string strDbUserName = connectionParameterSets[i][@"dbUserName"];
                            string strDbUserPassword = connectionParameterSets[i][@"dbUserPassword"];
                            if (strDbUserPassword == "NO_PASSWORD_SPECIFIED") {
                                strDbUserPassword = "";
                            }
                            string strOsAuthentication = connectionParameterSets[i][@"OSAuthentication"];
                            if (strDbUserName == "NO_NAME_SPECIFIED") {
                                strOsAuthentication = @"1";
                            }

                            ConnectionState enumConnState = ConnectionState.Closed;
                            DbConnection dbConnection = null;
                            if (strDbUserName != @"NO_NAME_SPECIFIED") {
                                if (strClusterName != @"N/A")
                                {
                                    enumConnState = connectToDatabase
                                            (out dbConnection, s_defaultSqlConnectionProvider,
                                            strClusterName, strDbUserName, strDbUserPassword, s_InitialCatalog);
                                    if (enumConnState != ConnectionState.Open)
                                    {
                                        foreach (String line in strClusterIP.Split(' '))
                                        {
                                            enumConnState = connectToDatabase
                                                (out dbConnection, s_defaultSqlConnectionProvider,
                                                line, strDbUserName, strDbUserPassword, s_InitialCatalog);
                                            if (enumConnState == ConnectionState.Open)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    enumConnState = connectToDatabase
                                        (out dbConnection, s_defaultSqlConnectionProvider,
                                        strDbServerName, strDbUserName, strDbUserPassword, s_InitialCatalog);
                                }
                            }

                            if (resultCode == ResultCodes.RC_SUCCESS && enumConnState == ConnectionState.Open) {
                                connectionDic[@"dbConnection"] = dbConnection;
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: SQL connection creation using credential at index {1}.\n{2}",
                                                      m_taskId,
                                                      i.ToString(),
                                                      strDbUserName);
                                result = new ConnectionScriptResults(connectionDic,
                                                                     ResultCodes.RC_SUCCESS,
                                                                     0,
                                                                     i);
                                break;
                            } else if (strOsAuthentication == @"1") {
                                if (strClusterName != @"N/A")
                                {
                                    enumConnState = connectToDatabase
                                            (out dbConnection, s_defaultSqlConnectionProvider,
                                            strClusterName, strDbUserName, strDbUserPassword, s_InitialCatalog);
                                    if (enumConnState != ConnectionState.Open)
                                    {
                                        foreach (String line in strClusterIP.Split(' '))
                                        {
                                            enumConnState = connectToDatabase
                                                (out dbConnection, s_defaultSqlConnectionProvider,
                                                line, strDbUserName, strDbUserPassword, s_InitialCatalog);
                                            if (enumConnState == ConnectionState.Open)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    enumConnState = connectToDatabase
                                        (out dbConnection, s_defaultSqlConnectionProvider, strDbServerName, s_InitialCatalog);
                                }
                                if (resultCode == ResultCodes.RC_SUCCESS && enumConnState == ConnectionState.Open) {
                                    connectionDic[@"dbConnection"] = dbConnection;
                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: SQL connection creation using pass-thru security .\n",
                                                          m_taskId);

                                    result = new ConnectionScriptResults(connectionDic,
                                                                         ResultCodes.RC_SUCCESS,
                                                                         0,
                                                                         i);
                                    break;
                                }
                            }
                        } catch (Exception ex) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Attempting SQL connection using credential at index {1} resulted in an exception:\n{2}",
                                                  m_taskId,
                                                  i.ToString(),
                                                  ex.ToString());
                        }
                    }

                    //
                    // Connect failed after all credentials attempted.
                    if (null == result) {
                        result = new ConnectionScriptResults(null,
                                                             ResultCodes.RC_HOST_CONNECT_FAILED,
                                                             0,
                                                             connectionParameterSets.Length);
                    }
                } catch (Exception e) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in MSSQLDbConnectionScript.\n{1}",
                                          m_taskId,
                                          e.ToString());

                    //
                    // This is really an unanticipated fail safe.  We're
                    // going to report that *no* credentials were tried, which
                    // actually may not be true...
                    result = new ConnectionScriptResults(null,
                                                         ResultCodes.RC_LOGIN_FAILED,
                                                         0,
                                                         -1);
                }
            }

            Debug.Assert(null != result);
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Connection script MSSQLDbConnectionScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        /// <summary>
        /// Generic database connection method using a specific username and password (Standard Security)
        /// </summary>
        /// <param name="strProviderName">Provider Name</param>
        /// <param name="strDBServer">Server Name</param>
        /// <param name="strDbUserName">Login Name</param>
        /// <param name="strDbUserPassword">Login Password</param>
        /// <returns></returns>
        private ConnectionState connectToDatabase
                (out DbConnection oDbConnection, string strProviderName, 
                 string strDBServer, string strDbUserName, string strDbUserPassword, string strInitialCatalog) {
            Debug.Assert(strProviderName != null);
            Stopwatch sw = new Stopwatch();
            ConnectionState state = ConnectionState.Closed;
            oDbConnection = null;
            try {
                DbProviderFactory oDbFactory = null;
                string strOLEProviderString = null;
                foreach (string strDefaultOleDbProviderName in Enum.GetNames(typeof(oleDBProvider))) {
                    if (strDefaultOleDbProviderName.Equals(strProviderName)) {
                        oDbFactory = DbProviderFactories.GetFactory(@"System.Data.OleDb");
                        if (strDefaultOleDbProviderName.Equals(oleDBProvider.ASEOLEDBProvider.ToString())) {
                            strOLEProviderString = @"Sybase.ASEOLEDBProvider";
                        }
                        else {
                            strOLEProviderString = strDefaultOleDbProviderName;
                        }
                        break;
                    }
                }
                if (oDbFactory == null) {
                    oDbFactory = DbProviderFactories.GetFactory(strProviderName);
                }
                oDbConnection = oDbFactory.CreateConnection();
                DbConnectionStringBuilder oDbConnStrBuilder = oDbFactory.CreateConnectionStringBuilder();
                // Provider is only used for Ole DB Connection
                if (oDbConnStrBuilder.GetType().Equals(typeof(System.Data.OleDb.OleDbConnectionStringBuilder))) {
                    oDbConnStrBuilder[@"Provider"] = strOLEProviderString;
                    if (strOLEProviderString.Equals("DB2OLEDB")) {
                        oDbConnStrBuilder[@"Network Transport Library"] = @"TCPIP";
                    }
                }
                if (!string.IsNullOrEmpty(strDBServer)) {
                    if (strDBServer.Contains(@"\MSSQLSERVER(DEFAULT)")) {
                        strDBServer.Replace(@"\MSSQLSERVER(DEFAULT)", string.Empty);
                    }
                }                       
                oDbConnStrBuilder[@"Data Source"] = strDBServer;
                oDbConnStrBuilder[@"User Id"] = strDbUserName;
                oDbConnStrBuilder[@"Initial Catalog"] = strInitialCatalog;
                if (String.IsNullOrEmpty(strDbUserPassword)) {
                    oDbConnStrBuilder[@"Password"] = "";
                } else {
                    oDbConnStrBuilder[@"Password"] = strDbUserPassword;
                }
                string debugString = string.Copy(oDbConnStrBuilder.ConnectionString);
                debugString = debugString.Replace(strDbUserPassword, "XXXX");
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Attempting to connect to database using connectionString {1}.",
                                      m_taskId,
                                      debugString);
                oDbConnection.ConnectionString = oDbConnStrBuilder.ConnectionString;
                sw.Start();
                oDbConnection.Open();
                sw.Stop();
                state = oDbConnection.State;
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Connection result {1}.  Elapsed time {2}.",
                                      m_taskId,
                                      oDbConnection.State.ToString(),
                                      sw.Elapsed.ToString());
            }
            catch (DbException sqlEx) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Database connection failed!  Elapsed time {1}.\n{2}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      FormatDbException(sqlEx));
            }
            catch (Exception ex) {
                Lib.LogException(m_taskId, sw, "Database connection failed", ex);
            }
            return state;
        }

        /// <summary>
        /// Generic database connection method using OS user (Trusted Security)
        /// </summary>
        /// <param name="strProviderName">Provider Name</param>
        /// <param name="strDBServer">Server Name</param>
        /// <returns></returns>
        private ConnectionState connectToDatabase
                (out DbConnection oDbConnection, string strProviderName, string strDBServer, string strInitialCatalog) {
            Debug.Assert(strProviderName != null);
            Stopwatch sw = new Stopwatch();
            ConnectionState state = ConnectionState.Closed;
            oDbConnection = null;
            try {
                DbProviderFactory oDbFactory = null;
                string strOLEProviderString = null;
                foreach (string strDefaultOleDbProviderName in Enum.GetNames(typeof(oleDBProvider))) {
                    if (strDefaultOleDbProviderName.Equals(strProviderName)) {
                        oDbFactory = DbProviderFactories.GetFactory(@"System.Data.OleDb");
                        if (strDefaultOleDbProviderName.Equals(oleDBProvider.ASEOLEDBProvider.ToString())) {
                            strOLEProviderString = @"Sybase.ASEOLEDBProvider";
                        } else {
                            strOLEProviderString = strDefaultOleDbProviderName;
                        }
                        break;
                    }
                }
                if (oDbFactory == null) {
                    oDbFactory = DbProviderFactories.GetFactory(strProviderName);
                }
                oDbConnection = oDbFactory.CreateConnection();
                DbConnectionStringBuilder oDbConnStrBuilder = oDbFactory.CreateConnectionStringBuilder();
                // Provider is only used for Ole DB Connection
                if (oDbConnStrBuilder.GetType().Equals(typeof(System.Data.OleDb.OleDbConnectionStringBuilder))) {
                    oDbConnStrBuilder[@"Provider"] = strOLEProviderString;
                    if (strOLEProviderString.Equals("DB2OLEDB")) {
                        oDbConnStrBuilder[@"Network Transport Library"] = @"TCPIP";
                    }
                }
                if (!string.IsNullOrEmpty(strDBServer)) {
                    if (strDBServer.Contains(@"\MSSQLSERVER(DEFAULT)")) {
                        strDBServer.Replace(@"\MSSQLSERVER(DEFAULT)", string.Empty);
                    }
                }                       
                oDbConnStrBuilder[@"Data Source"] = strDBServer;
                oDbConnStrBuilder[@"Initial Catalog"] = strInitialCatalog;
                oDbConnStrBuilder[@"Integrated Security"] = @"SSPI";
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Attempting to connect to database using connectionString {1}.",
                                      m_taskId,
                                      oDbConnStrBuilder.ConnectionString);
                string connString = oDbConnStrBuilder.ConnectionString;
                oDbConnection.ConnectionString = oDbConnStrBuilder.ConnectionString;
                sw.Start();
                oDbConnection.Open();
                sw.Stop();
                state = oDbConnection.State;
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Connection result {1}.  Elapsed time {2}.",
                                      m_taskId,
                                      oDbConnection.State.ToString(),
                                      sw.Elapsed.ToString());
            } catch (DbException sqlEx) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Database connection failed!  Elapsed time {1}.\n{2}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      FormatDbException(sqlEx));
            } catch (Exception ex) {
                Lib.LogException(m_taskId, sw, "Database connection failed", ex);
            }
            return state;
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

        private enum oleDBProvider {
            SQLOLEDB,    //SQL Server OLE DB provider string
            MySQLProv,   //MySQL OLE DB Provider string
            OraOLEDB,    //Oracle OLE DB Provider string
            sibprovider, //Interbase OLE DB Provider string
            DB2OLEDB,    //DB2 OLE DB Provider string
            ASEOLEDBProvider, //Sybase Server Enterprise OLE DB Provider String
            Ifxoledbc,   // Informix OLE DB Provider String
            IBMDA400,    // AS/400, IBM Client Access OLE DB Provider String
            Pervasive    // Pervasive OLE DB Provider String
        }

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;
        /// <summary>Log data buffer.</summary>
        private static string s_InitialCatalog = @"master";
        private static string s_defaultSqlConnectionProvider = @"SQLOLEDB";       
        //private static string s_defaultSqlConnectionProvider = @"System.Data.SqlClient";
    }
}
