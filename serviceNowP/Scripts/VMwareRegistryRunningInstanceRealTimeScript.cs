#region Copyright
/******************************************************************
*
*          Module: VMware Registry Running Instance Real Time Script
* Original Author: Alexander Meau
*   Creation Date: 2006/09/09
*
* Current Status
*       $Revision: 1.6 $
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
using System.Collections.Specialized;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class VMwareRegistryRunningInstanceRealTimeScript : ICollectionScriptRuntime {
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
                IDictionary<string, object> connection, string tftpPath, string tftpPath_login,
                string tftpPath_password, ITftpDispatcher tftpDispatcher) {
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
                                  "Task Id {0}: Collection script VMwareRegistryRunningInstanceRealTimeScript.",
                                  m_taskId);
            try {
                // Check ManagementScope Default
                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to VMwareRegistryRunningInstanceRealTimeScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey("default")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for default namespace is not present in connection object.",
                                          m_taskId);
                }
                else {
                    m_defaultScope = connection[@"default"] as ManagementScope;
                    if (!m_defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to default namespace failed.",
                                              m_taskId);
                    } 
                    else {
                        try {
                            m_wmiRegistry = new ManagementClass(m_defaultScope, new ManagementPath(@"StdRegProv"), null);
                        }
                        catch (ManagementException mex) {
                            resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Cannot obtain registry management class.\nWMI Error Code {1}.\n{2}",
                                                  m_taskId,
                                                  mex.ErrorCode.ToString(),
                                                  mex.ToString());
                        }
                        catch (Exception ex) {
                            resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Cannot obtain registry management class.\n{1}",
                                                  m_taskId,
                                                  ex.ToString());
                        }
                    }
                }


                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Attempting to find all VMware running instance by registry.",
                                          m_taskId);
                    StringBuilder strResult = new StringBuilder();
                    using (m_wmiRegistry) {
                        ManagementBaseObject moInput = m_wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
                        moInput.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_USERS);
                        moInput.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, "");
                        ManagementBaseObject moOutput = m_wmiRegistry.InvokeMethod(RegistryMethodNames.ENUM_KEY, moInput, null);
                        if (moOutput != null) {
                            string[] strSubKeys = moOutput.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                            Debug.Assert(strSubKeys != null);
                            foreach (string key in strSubKeys) {
                                string strRunningVMPath = key + @"\Software\VMware, Inc.\Running VM List";
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Attempting to retrieve running VMs from path {1}.",
                                                      m_taskId,
                                                      strRunningVMPath);
                                string strRunningVMs = getRunningVMs(strRunningVMPath);
                                if (!String.IsNullOrEmpty(strRunningVMs)) {
                                    if (strResult.Length > 0) {
                                        strResult.Append(BdnaDelimiters.DELIMITER_TAG);
                                    }
                                    strResult.Append(strRunningVMs);
                                }
                            }                                
                        }
                    }

                    if (strResult.Length > 0) {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Total running virtual machines {1}.",
                                              m_taskId,
                                              strResult.Length.ToString());
                        this.BuildDataRow(s_attribute, strResult.ToString());
                    }
                    else {
                        this.BuildDataRow(s_attribute, @"<BDNA,>");
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: No running virtual machines found.",
                                              m_taskId);
                    }
                }
            }
            catch (ManagementException mex) {
                StringBuilder props = new StringBuilder();
                foreach (PropertyData mexProp in mex.ErrorInformation.Properties) {
                    if (mexProp != null && !mexProp.IsArray) {

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

                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in VMwareRegistryRunningInstanceRealTimeScript.  Elapsed time {1}.\n{2}\n{3}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          mex.ToString(),
                                          props.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in VMwareRegistryRunningInstanceRealTimeScript.  Elapsed time {1}.\n{2}\n{3}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          mex.ToString(),
                                          props.ToString());
                }
            }
            catch (Exception ex) {

                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in VMwareRegistryRunningInstanceRealTimeScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in VMwareRegistryRunningInstanceRealTimeScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script VMwareRegistryRunningInstanceRealTimeScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }


        /// <summary>
        /// Get List of running virtual machine config files.
        /// Each config file represent a running instance of virtual machine in memory.
        /// </summary>
        /// <param name="strKeyPath">Registry Path</param>
        /// <returns>List of Virtual Machines</returns>
        private string getRunningVMs(string strRegKeyPath) {
            StringBuilder builder = new StringBuilder();
            ManagementBaseObject moInput = m_wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
            moInput.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_USERS);
            moInput.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, strRegKeyPath);
            ManagementBaseObject moOutput = m_wmiRegistry.InvokeMethod(RegistryMethodNames.ENUM_VALUES, moInput, null);
            if (moOutput != null) {
                string[] strValues = moOutput.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                if (strValues != null && strValues.Length > 0) {
                    foreach (string strValue in strValues) {
                        if (builder.Length > 0) {
                            builder.Append(BdnaDelimiters.DELIMITER_TAG);
                        }
                        builder.Append(strValue);
                    }
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, string collectedData) {
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
        private ManagementScope m_defaultScope = null;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;

        /// <summary> Attribute value </summary>
        public string s_attribute = @"RunningVMXInstances";

        /// <summary> Registry Management class </summary>
        public ManagementClass m_wmiRegistry = null;
    }
}
