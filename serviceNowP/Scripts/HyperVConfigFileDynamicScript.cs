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
    public class HyperVConfigFileDynamicScript : ICollectionScriptRuntime
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
                                  "Task Id {0}: Collection script HyperVConfigFileDynamicScript.",
                                  m_taskId);
            try
            {
                // Check ManagementScope CIMV
                if (connection == null)
                {

                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to HyperVConfigFileDynamicScript is null.",
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
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!m_cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    }
                }           

            if (resultCode == ResultCodes.RC_SUCCESS)
            {
                String strResult = this.findXMLFilePaths();
   //             Console.WriteLine(strResult);
                //
                // Package data into CLE format to be returned.
                if (strResult.Length > 0)
                {
                    BuildDataRow(s_attributeName, strResult);
                }
            }
            }
            catch (ManagementException mex)
            {
                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: no configuration files(c:\\programdata\\microsoft\\windows\\hyper-v\\virtual machines\\*.xml) in exception in HyperVConfigFileDynamicScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
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
                                          "Task Id {0}: Unhandled exception in HyperVConfigFileDynamicScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                }
                else
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in HyperVConfigFileDynamicScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script HyperVConfigFileDynamicScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        private string findXMLFilePaths() {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                      0,
                      "Task Id {0}: Attempting to retrieve Hyper-v XML config file paths!",
                      m_taskId);
            StringBuilder strFiles = new StringBuilder();

        //    string wqlSelect = @"select * FROM CIM_DataFile where Extension  = 'xml' and Name like '%\\programdata\\microsoft\\windows\\hyper-v\\virtual machines\\%'";
            string wqlSelect = @"select * FROM CIM_DataFile where (Extension  = 'xml' and Drive = 'C:' and Name like '%\\programdata\\microsoft\\windows\\hyper-v\\virtual machines\\%') or (Extension  = 'xml' and Drive = 'C:' and Name like '%\\Microsoft\\Windows\\Hyper-V\\Virtual Machines\\%')";
            try
            {
                System.Management.ObjectQuery oQuery = new ObjectQuery(wqlSelect);
                ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(m_cimvScope, oQuery);
            //    ManagementObjectCollection oReturnCollection = oSearcher.Get();
                using (ManagementObjectCollection oReturnCollection = oSearcher.Get()) {
                    foreach (ManagementObject moConfigfile in oReturnCollection)
                    {
                        string strConfigFile = moConfigfile[@"Name"].ToString();
                        strFiles.Append(BdnaDelimiters.DELIMITER_TAG);
                        strFiles.Append(strConfigFile);
                    }
                }
            }
            catch (ManagementException mex)
            {
                 Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to look for xml config file resulted in an exception.\nException is expected if this local user never run Hyper-V.\nDetails:{1}",
                                          m_taskId,
                                          mex.ToString());              
            }
            catch (Exception ex)
            {

                Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to look for xml config file resulted in an exception(not ManagementException).\nException is expected if this local user never run Hyper-V.\nDetails:{1}",
                                          m_taskId,
                                          ex.ToString());
            }
            return strFiles.ToString();
        }       

        /// <summary>
        /// Validate Configuration file is readable by current user credential. 
        /// </summary>
        /// <param name="strFilePath">File Path</param>
        /// <returns>True if file is valid, and readable; False otherwise.</returns>
        private bool isVMConfigFileReadable(string strFilePath)
        {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Attempting to retrieve file property of path {1}.",
                                  m_taskId,
                                  strFilePath);
            bool bStatus = false;
            try
            {
                using (ManagementObject moVMConfigFile = new ManagementObject(@"CIM_DataFile.Name='" + strFilePath + @"'"))
                {
                    moVMConfigFile.Scope = m_cimvScope;
                    moVMConfigFile.Get();
                    if (moVMConfigFile[@"Readable"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        bStatus = true;
                    }
                }
            }
            catch (ManagementException mex)
            {
                if (mex.Message.Trim().Equals(@"Not found"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run Hyper-V.\nDetail Exception Message: {2} not found.",
                                          m_taskId,
                                          strFilePath,
                                          strFilePath);
                }
                else
                {
                    StringBuilder props = new StringBuilder();
                    foreach (PropertyData mexProp in mex.ErrorInformation.Properties)
                    {
                        if (mexProp != null && !mexProp.IsArray)
                        {

                            //
                            // Do NOT call ToString on the values because they may
                            // be null. StringBuilder handles null values just fine.
                            // Besides, calling ToString explicitly on the Names or
                            // Values of this collection buys you absolutely *nothing*.
                            // These are already objects, so there is no boxing to
                            // be avoided.
                            props.Append(mexProp.Name).Append(@"=").Append(mexProp.Value).AppendLine();
                        }
                    }

                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run Hyper-V.\nDetail Exception Message: .\n{2}\n{3}",
                                          m_taskId,
                                          strFilePath,
                                          mex.Message,
                                          props.ToString());
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Trim().Equals(@"Not found"))
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run Hyper-V.\nDetail Exception Message: {2} not found.",
                                          m_taskId,
                                          strFilePath,
                                          strFilePath);
                }
                else
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to read file properties of {1} resulted in an exception.\nException is expected if this local user never run Hyper-V.\nDetail Exception Message:\n{2}",
                                          m_taskId,
                                          strFilePath,
                                          ex.Message);
                }
            }
            return bStatus;
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
        private ManagementScope m_cimvScope = null;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;


        public static string s_attributeName = @"VMConfigFilePaths";


    }
}
