using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts
{
    public class HyperV_VMStateScript : ICollectionScriptRuntime
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
        /// <returns>Collection results.</returns>
        public CollectionScriptResults ExecuteTask(long taskId, long cleId, long elementId, long databaseTimestamp,
                long localTimestamp, IDictionary<string, string> attributes, IDictionary<string, string> scriptParameters,
                IDictionary<string, object> connection, string tftpPath, string tftpPath_login, string tftpPath_password, 
                ITftpDispatcher tftpDispatcher) 
        {
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_executionTimer = Stopwatch.StartNew();

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script HyperV_VMStateScript.",
                                  m_taskId);
            try
            {
                // Check ManagementScope virtualization
                if (connection == null)
                {

                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to HyperV_VMStateScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey("virtualization"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for virtualization namespace is not present in connection object.",
                                          m_taskId);
                }
                else
                {
                   
                    m_virtualizeScope = connection[@"virtualization"] as ManagementScope;
                    if (!m_virtualizeScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to virtualization namespace failed",
                                              m_taskId);
                    }
                    if (!connection.ContainsKey("v2"))
                    {
                    //resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Management scope for virtualization v2 namespace is not present in connection object.",
                                              m_taskId);
                    }
                    else {
                         m_virtualizev2Scope = connection[@"v2"] as ManagementScope;
                         if (!m_virtualizev2Scope.IsConnected) {
                             resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                             Lib.Logger.TraceEvent(TraceEventType.Error,
                                                   0,
                                                   "Task Id {0}: Connection to virtualization v2 namespace failed",
                                                   m_taskId);
                         }
                    }
                }      

                if (resultCode == ResultCodes.RC_SUCCESS)
                {

     /*                         ConnectionOptions connectionOptions = new ConnectionOptions();

                              connectionOptions.Username = "administrator";

                              connectionOptions.Password = @"Simple.0";
                              ManagementScope m_virtualizeScope = new ManagementScope(@"\\Hyper-v9140\root\virtualization", connectionOptions);
                              m_virtualizeScope.Connect();     */
                      ObjectQuery query = new ObjectQuery("SELECT * FROM MsVM_ComputerSystem WHERE Caption LIKE 'Virtual%' ");
                      ManagementObjectSearcher searcher = null;
                      ManagementObjectCollection moc = null;
                      if (m_virtualizeScope != null)
                      {
                          searcher = new ManagementObjectSearcher(m_virtualizeScope, query);
                          moc = searcher.Get();
                          using (searcher)
                          {
                              resultCode = Lib.ExecuteWqlQuery(m_taskId, searcher, out moc);
                          }
                      }
                      if (m_virtualizev2Scope != null && ResultCodes.RC_SUCCESS != resultCode && ResultCodes.RC_WMI_QUERY_TIMEOUT != resultCode)
                      {
                          searcher = new ManagementObjectSearcher(m_virtualizev2Scope, query);
                          moc = searcher.Get();
                          using (searcher)
                          {
                              resultCode = Lib.ExecuteWqlQuery(m_taskId, searcher, out moc);
                          }
                      }
                      StringBuilder sbVmInfo = new StringBuilder();
                      long runningVMsCount = 0;
                      if (ResultCodes.RC_SUCCESS == resultCode && null != moc)
                      {
                          using (moc)
                          {
                              foreach (ManagementObject mo in moc)
                              {
                                  sbVmInfo.Append("<BDNA,>");
                                  sbVmInfo.Append(@"ElementName=""" + mo.GetPropertyValue("ElementName").ToString() + @"""");
                                  sbVmInfo.Append("<BDNA,1>");
                                  sbVmInfo.Append(@"UpTime=""" + mo.GetPropertyValue("OnTimeInMilliseconds").ToString() + @"""");
                                  sbVmInfo.Append("<BDNA,1>");
                                  sbVmInfo.Append(@"GUID=""" + mo.GetPropertyValue("Name").ToString() + @"""");
                                  sbVmInfo.Append("<BDNA,1>");
                                  String state = mo.GetPropertyValue("EnabledState").ToString();

                                  if (state.Equals(Convert.ToString(0)))
                                  {
                                      state = "Unknown";
                                  }
                                  else if (state.Equals(Convert.ToString(2)))
                                  {
                                      state = "Enabled";
                                      runningVMsCount++;
                                  }
                                  else if (state.Equals(Convert.ToString(3)))
                                  {
                                      state = "Disabled";
                                  }
                                  else if (state.Equals(Convert.ToString(32768)))
                                  {
                                      state = "Paused";
                                  }
                                  else if (state.Equals(Convert.ToString(32769)))
                                  {
                                      state = "Suspended";
                                  }
                                  else if (state.Equals(Convert.ToString(32770)))
                                  {
                                      state = "Starting";
                                  }
                                  else if (state.Equals(Convert.ToString(32771)))
                                  {
                                      state = "Snapshotting";
                                  }
                                  else if (state.Equals(Convert.ToString(32773)))
                                  {
                                      state = "Saving";
                                  }
                                  else if (state.Equals(Convert.ToString(32774)))
                                  {
                                      state = "Stopping";
                                  }
                                  else if (state.Equals(Convert.ToString(32776)))
                                  {
                                      state = "Pausing";
                                  }
                                  else if (state.Equals(Convert.ToString(32777)))
                                  {
                                      state = "Resuming";
                                  }
                                  sbVmInfo.Append(@"State=""" + state + @"""");
                              }
                              sbVmInfo.Append("<BDNA,C>");
                              sbVmInfo.Append(@"runningVMsCount=""" + runningVMsCount + @"""");
                              sbVmInfo.Append("<BDNA,C>");


                              //                     Console.WriteLine(sbVmInfo);
                              //                    System.IO.File.WriteAllText("d:\\testscript.txt", sbVmInfo.ToString());


                              //
                              // Package data into CLE format to be returned.
                              if (sbVmInfo.Length > 0)
                              {
                                  //       BuildDataRow(s_attributeName, sbVmInfo.ToString());
                                  BuildDataRow(s_attributeName, sbVmInfo.ToString());
                              }
                         }
                    }
                }
            }
            catch (ManagementException mex)
            {
                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: no configuration files(c:\\programdata\\microsoft\\windows\\hyper-v\\virtual machines\\*.xml) in exception in HyperV_VMStateScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          mex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                }

            }
            catch (Exception ex)
            {
                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in HyperV_VMStateScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                }
                else
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in HyperV_VMStateScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script HyperV_VMStateScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }


        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData)
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

        /// <summary>output data buffer.</summary>
        private StringBuilder m_outputData = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Management Scope </summary>
        private ManagementScope m_virtualizeScope = null;

        private ManagementScope m_virtualizev2Scope = null;
        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;


        private List<FileInfo> vmsList = new List<FileInfo>();

        public static string s_attributeName = @"VMsState";


    }
}
