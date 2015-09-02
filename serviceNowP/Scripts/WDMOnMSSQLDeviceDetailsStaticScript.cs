using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class WDMOnMSSQLDeviceDetailsStaticScript : ICollectionScriptRuntime {
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
                long taskId, long cleId, long elementId, long databaseTimestamp, long localTimestamp,
                IDictionary<string, string> attributes, IDictionary<string, string> scriptParameters,
                IDictionary<string, object> connection, string tftpPath, string tftpPath_login,
                string tftpPath_password, ITftpDispatcher tftpDispatcher) {
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
                                  "Task Id {0}: Collection script WDMOnMSSQLDeviceDetailsStaticScript.",
                                  m_taskId);

            try {

                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                } else {
                    //Check connection attributes
                    if (connection.ContainsKey("dbConnection")) {
                        dbConnection = connection[@"dbConnection"] as DbConnection;
                    } else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing dbConnection script parameter.",
                                              m_taskId);
                    }
                }
                if (dbConnection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                } else if (dbConnection.State != ConnectionState.Open) {
                    resultCode = ResultCodes.RC_HOST_CONNECT_FAILED;
                } else {
                    using (dbConnection) {
                        resultCode = GetDeviceDetails(dbConnection);
                    }
                }

                // Return RC_SUCCESS even if the query failed, as the database
                // may be inaccessible
                resultCode = ResultCodes.RC_SUCCESS;

                StringBuilder dataRow = new StringBuilder();
                dataRow.Append(elementId)
                       .Append(',')
                       .Append(m_attributes[@"deviceDetails"])
                       .Append(',')
                       .Append(m_scriptParameters[@"CollectorId"])
                       .Append(',')
                       .Append(m_taskId)
                       .Append(',')
                       .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds)
                       .Append(',')
                       .Append(@"deviceDetails")
                       .Append(',')
                       .Append(BdnaDelimiters.BEGIN_TAG)
                       .Append(m_deviceDetails.ToString())
                       .Append(BdnaDelimiters.END_TAG);
                result = new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
            } catch (Exception e) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unhandled exception in WDMOnMSSQLDeviceDetailsStaticScript.\n{1}",
                                      m_taskId,
                                      e.ToString());
                result = new CollectionScriptResults
                    (ResultCodes.RC_PROCESS_EXEC_FAILED, 0, null, null, null, false, null);
            }

            Debug.Assert(null != result);
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Connection script WDMOnMSSQLDeviceDetailsStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        #endregion

        private ResultCodes GetDeviceDetails(DbConnection dbConnection) {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            Stopwatch sw = new Stopwatch();
            int recordCount = 0;

            try {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                          0,
                          "Task Id {0}: Executing device details query.",
                          m_taskId);
                sw.Start();

                m_databaseName = m_scriptParameters["databaseName"];
                Lib.Logger.TraceEvent(TraceEventType.Information,
                          0,
                          "Task Id {0}: Connecting to database {1}.",
                          m_taskId,
                          m_databaseName);
                dbConnection.ChangeDatabase(m_databaseName);

                DbCommand cmd = dbConnection.CreateCommand();
                cmd.CommandText =
                    @"select client.ClientID, client.Name, network.IP, network.MAC, " +
                    @"platform.Description as Platform, os.Description as OS, " +
                    @"client.Image, hardware.CPU, hardware.CPUSpeed, " +
                    @"hardware.RAM, storage.TotalSpace, hardware.Serial, " +
                    @"hardware.BIOS, hardware.Manufactured " +
                    @"from Client client, ClientHardware hardware, " +
                    @"ClientNetwork network, ClientStorage storage, " +
                    @"Platform platform, OSType os " +
                    @"where client.ClientID = hardware.ClientID " +
                    @"and client.ClientID = network.ClientID " +
                    @"and client.ClientID = storage.ClientID " +
                    @"and hardware.PlatformID = platform.PlatformID " +
                    @"and client.OSTypeID = os.OSTypeID ";
                cmd.CommandType = CommandType.Text;

                DbDataReader reader = cmd.ExecuteReader();
                Debug.Assert(null != reader);
                using (reader) {
                    for (recordCount = 0; reader.Read(); ++recordCount) {
                        if (m_deviceDetails.Length > 0) {
                            m_deviceDetails.Append(BdnaDelimiters.DELIMITER_TAG);
                        }
                        m_deviceDetails.Append("ClientID=").Append(reader[@"ClientID"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("Name=").Append(reader[@"Name"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("IPAddress=").Append(reader[@"IP"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("MACAddress=").Append(reader[@"MAC"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("Platform=").Append(reader[@"Platform"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("OS=").Append(reader[@"OS"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("Image=").Append(reader[@"Image"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("CPU=").Append(reader[@"CPU"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("CPUSpeed=").Append(reader[@"CPUSpeed"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("RAM=").Append(reader[@"RAM"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("MediaSize=").Append(reader[@"TotalSpace"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("SerialNumber=").Append(reader[@"Serial"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("BIOS=").Append(reader[@"BIOS"].ToString()).Append(BdnaDelimiters.DELIMITER1_TAG);
                        m_deviceDetails.Append("ManufacturedOn=").Append(reader[@"Manufactured"].ToString());
                    }
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                        0,
                        "Task Id {0}: Processing device details complete.",
                        m_taskId);
                }
                resultCode = ResultCodes.RC_SUCCESS;
            } catch (DbException dbEx) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Device detail query failed. Elapsed time {1}.\n{2}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      FormatDbException(dbEx));
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Device detail query failed. Elapsed time {1}.\nRecords processed {2}\n{3}",
                                      m_taskId,
                                      sw.Elapsed.ToString(),
                                      recordCount.ToString(),
                                      ex.ToString());
            }

            return resultCode;
        }

        private String FormatDbException(DbException dbex) {
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

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>The name of the Rapport database to query.</summary>
        private string m_databaseName;

        /// <summary>Thin client device details in UDT form</summary>
        private StringBuilder m_deviceDetails = new StringBuilder();
    }
}
