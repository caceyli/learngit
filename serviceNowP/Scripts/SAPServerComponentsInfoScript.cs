#region Copyright
/******************************************************************
*
*          Module: SAP Server Components Info Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/10/27
*
* Current Status
*       $Revision: 1.9 $
*           $Date: 2010/05/19 19:53:47 $
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
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

using bdna.ScriptLib;
using bdna.Shared;
using BDNASAP.SAPGuiScripting;

namespace bdna.Scripts {
    public class SAPServerComponentsInfoScript : ICollectionScriptRuntime {
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
        public CollectionScriptResults ExecuteTask
            (long taskId,long cleId,long elementId,long databaseTimestamp,long localTimestamp,
            IDictionary<string, string> attributes,IDictionary<string, string> scriptParameters, 
            IDictionary<string, object> connection, ITftpDispatcher tftpDispatcher) {
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_executionTimer = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            try {
                Lib.Logger.TraceEvent(TraceEventType.Start,
                                      0,
                                      "Task Id {0}: Collection script SAPServerComponentsInfoScript.",
                                      m_taskId);

                if (connection == null) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                }
                else {
                    //Check host attributes
                    if (connection.ContainsKey(@"address")) {
                        m_strApplicationServer  = connection[@"address"] as String;
                    }
                    else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing SAP Application Server connection parameter.",
                                              m_taskId);
                    }

                    //Check InstanceNumber attributes
                    if (connection.ContainsKey(@"systemNumber")) {
                        m_strInstanceNumber = connection[@"systemNumber"] as String;
                        if (m_strInstanceNumber.Length != 2) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Invalid System number <{1}>.\nSystem Number should always be two digits even if the number is 00.",
                                                  m_taskId,
                                                  m_strInstanceNumber);
                            resultCode = ResultCodes.RC_INVALID_PARAMETER_TYPE;
                        }
                    }
                    else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing SAP system number connection parameter.",
                                              m_taskId);
                    }

                    //Check SAP Credential
                    if (connection.ContainsKey(@"sapUserName") && connection.ContainsKey(@"sapUserPassword")) {
                        m_strSAPUserName = connection[@"sapUserName"] as String;
                        m_strSAPUserPassword = connection[@"sapUserPassword"] as String;
                    }
                    else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing SAP User name/password connection parameter.",
                                              m_taskId);
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    using (SAPGuiConnection oConn = new SAPGuiConnection()) {
                        if (!String.IsNullOrEmpty(SAPConnectionString)) {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Connecting to remote machine using {1}, SAP user name {2}.",
                                                  m_taskId,
                                                  SAPConnectionString,
                                                  m_strSAPUserName);
                            Stopwatch sw = Stopwatch.StartNew();
                            oConn.Connect(SAPConnectionString, this.m_strSAPUserName, this.m_strSAPUserPassword);
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Connection to {1} complete.  Elapsed time {2}.\n{3}",
                                                  m_taskId,
                                                  m_strApplicationServer,
                                                  sw.Elapsed.ToString(),
                                                  oConn.logData.ToString());

                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Collecting SAP server component information from remote SAP server",
                                                  m_taskId);
                            sw.Reset();
                            sw.Start();
                            SAPServerComponentsInfoCollector oServerComponentsInfoCollector = new SAPServerComponentsInfoCollector(ref oConn.Session);
                            oServerComponentsInfoCollector.Collect();
                            m_sapServerComponentsInfoProperties = oServerComponentsInfoCollector.ResultString;
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Collection of SAP server component information complete.  Elapsed time {1}.\n{2}",
                                                  m_taskId,
                                                  sw.Elapsed.ToString(),
                                                  oServerComponentsInfoCollector.logData.ToString());
                        }                        
                    }
                    BuildDataRow();
                }
            }
            catch (Exception ex) {
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in SAPServerComponentsInfoScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in SAPServerComponentsInfoScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script SAPServerComponentsInfoScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
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
            if (!m_attributes.ContainsKey(s_attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      s_attributeName);
            } else if (String.IsNullOrEmpty(m_sapServerComponentsInfoProperties)) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            } else {
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[s_attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(s_attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG)
                         .Append(m_sapServerComponentsInfoProperties)
                         .Append(BdnaDelimiters.END_TAG);
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

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>
        /// SAP Credentials
        /// </summary>
        public string SAPConnectionString {
            get {
                if (String.IsNullOrEmpty(m_strApplicationServer) || string.IsNullOrEmpty(m_strInstanceNumber)) {
                    return String.Empty;
                }
                else {
                    return "/H/" + m_strApplicationServer.Trim() + "/S/32" + m_strInstanceNumber.Trim();
                }
            }
        }

        private string m_strInstanceNumber = String.Empty;
        private string m_strApplicationServer = String.Empty;
        private string m_strSAPUserName = String.Empty;
        private string m_strSAPUserPassword = String.Empty;

        /// <summary>
        /// Collected Data
        /// </summary>
        private string m_sapServerComponentsInfoProperties = String.Empty;

        static string s_attributeName = @"__SAPServerComponentsDetails";
    }
}
