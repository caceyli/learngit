#region Copyright
/******************************************************************
*
*          Module: Windows Oracle Application Server Static Scripts
* Original Author: Alexander Meau
*   Creation Date: 2007/02/14
*
* Current Status
*       $Revision: 1.7 $
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
using System.Collections.Specialized;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;
using System.Xml;
using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    public class WindowsOracleAppConfigFootprintStaticScript : ICollectionScriptRuntime {

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
        public CollectionScriptResults ExecuteTask(
                long                            taskId,
                long                            cleId,
                long                            elementId,
                long                            databaseTimestamp,
                long                            localTimestamp,
                IDictionary<string, string>     attributes,
                IDictionary<string, string>     scriptParameters,
                IDictionary<string, object>     connection,
                string tftpPath,
                string tftpPath_login,
                string tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {
            m_executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleAppConfigFootprintStaticScript.",
                                  m_taskId);
            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                // Check ManagementScope CIMV
                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsOracleAppConfigFootprintStaticScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"default")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    defaultScope = connection[@"default"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              m_taskId);
                    } else if (!defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed.",
                                              m_taskId);
                    } 
                }

                //Check Oracle App Server Config File Path attribute
                if (resultCode.Equals(ResultCodes.RC_SUCCESS)) {
                    if (scriptParameters.ContainsKey("OracleAppServerConfigFilePath")) {
                        m_oracleAppServerConfigFilePath = scriptParameters[@"OracleAppServerConfigFilePath"];
                    }
                    else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter OracleAppServerConfigFilePath parameter.",
                                              m_taskId);
                    }
                }

                // Check Remote Process Temp Directory
                if (resultCode.Equals(ResultCodes.RC_SUCCESS)) {
                    if (!connection.ContainsKey(@"TemporaryDirectory")) {
                        connection[@"TemporaryDirectory"] = @"%TMP%";
                    }
                    else {
                        if (!connection[@"TemporaryDirectory"].Equals(@"%TMP%")) {
                            if (!Lib.ValidateDirectory(m_taskId, connection[@"TemporaryDirectory"].ToString(), cimvScope)) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Temporary directory {1} is not valid.",
                                                      m_taskId,
                                                      connection[@"TemporaryDirectory"].ToString());
                                resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                            }
                            else {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: User specified temp directory has been validated.",
                                                      m_taskId);
                            }
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Attempting to retrieve file {1}.",
                                          m_taskId,
                                          m_oracleAppServerConfigFilePath);
                    StringBuilder fileContent = new StringBuilder();

                    if (Lib.ValidateFile(m_taskId, m_oracleAppServerConfigFilePath, cimvScope)) {
                        using (IRemoteProcess rp =
                                RemoteProcess.GetRemoteFile(m_taskId, cimvScope, m_oracleAppServerConfigFilePath, connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                            //
                            // Launch the remote process.  
                            // This method will block until the entire remote process operation completes.
                            resultCode = rp.Launch();
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Remote file retrieval operation completed with result code {1}.",
                                                  m_taskId,
                                                  resultCode.ToString());
                            fileContent.Append(rp.Stdout);
                        }
                        // Parse config file content
                        resultCode = parseAppConfigFile(fileContent.ToString());

                    }
                    else {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Oracle Application Server Config file: {1} does not exist.",
                                              m_taskId,
                                              m_oracleAppServerConfigFilePath);
                        // Some Oracle Administrator might have corrupted config file or registry settings.
                        // In order not to confuse scan result with actual error, return RC_SUCCESS instead.
                        resultCode = ResultCodes.RC_SUCCESS;
                    }
                }
            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in WindowsOracleAppConfigFootprintStaticScript",
                                 ex);
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REMOTE_FILE;
            }

            CollectionScriptResults result = new CollectionScriptResults(resultCode,
                                                                         0,
                                                                         null,
                                                                         null,
                                                                         null,
                                                                         false,
                                                                         m_dataRow.ToString());
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsOracleAppConfigFootprintStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
        #endregion ICollectionScriptRuntime Members

        /// <summary>
        /// Parse Oracle Configuration File for main products.
        /// </summary>
        /// <param name="strFileContent">File Content</param>
        private ResultCodes parseAppConfigFile(string configFileContent) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REMOTE_FILE;
            if (String.IsNullOrEmpty(configFileContent)) return resultCode;

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Begin parsing oracle application server configuration file.",
                                  m_taskId);            
            try {
                XmlDocument xDoc = new XmlDocument();
                xDoc.LoadXml(configFileContent);
                StringBuilder oracleServerProducts = parseAppServerComponents(xDoc, @"//PRD_LIST/TL_LIST/COMP");
                if (oracleServerProducts != null && oracleServerProducts.Length > 0) {
                    BuildDataRow(s_oracleAppServerDetails, oracleServerProducts);
                }

                if (oracleServerProducts != null) {
                    StringBuilder oracleServerComponents = parseAppServerComponents(xDoc, @"//PRD_LIST/COMP_LIST/COMP");
                    if (oracleServerComponents != null && oracleServerComponents.Length > 0) {
                        BuildDataRow(s_oracleAppServerComponentsDetails, oracleServerComponents);
                    }
                }                
                resultCode = ResultCodes.RC_SUCCESS;
            } catch (XmlException xex) {
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in WindowsOracleAppConfigFootprintStaticScript",
                                 xex);                
            }
            return resultCode;
        }


        /// <summary>
        /// Parse Oracle Configuration Xml Document for all server components.
        /// </summary>
        /// <param name="strFileContent">File Content</param>
        /// <returns>List of components</returns>
        private StringBuilder parseAppServerComponents(XmlDocument xDoc, String xPath) {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Begin parsing configuration file from path {1}.",
                                  m_taskId,
                                  xPath);            
            Debug.Assert(xDoc != null && !String.IsNullOrEmpty(xPath));
            StringBuilder components = new StringBuilder();
            XmlNodeList nodes = xDoc.SelectNodes(xPath);
            int iComponentCount = 0;
            foreach (XmlNode node in nodes) {
                StringBuilder oneComponent = new StringBuilder();
                String eltName = String.Empty;
                String productName = String.Empty;

                XmlNode eltNameNode = node.Attributes.GetNamedItem("NAME");
                if (eltNameNode != null) {
                    eltName = eltNameNode.InnerText;
                    if (!string.IsNullOrEmpty(eltName)) {
                        eltName = eltName.Replace(@".", @"_");
                    }
                }
                XmlNode nameNode = node.SelectSingleNode(@"EXT_NAME");
                if (nameNode != null) {
                    productName = nameNode.InnerText;
                }
                if (!String.IsNullOrEmpty(productName) && !string.IsNullOrEmpty(eltName)) {
                    oneComponent.Append(BdnaDelimiters.DELIMITER1_TAG);
                    oneComponent.AppendFormat("componentName=\"{0}\"", eltName);
                    oneComponent.Append(BdnaDelimiters.DELIMITER2_TAG);
                    oneComponent.AppendFormat("name=\"{0}\"", productName);
                }

                // Parse component details.
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Reading detail information of component: {1}.",
                                      m_taskId,
                                      productName);            

                if (oneComponent.Length > 0) {
                    foreach (XmlAttribute xAttr in node.Attributes) {
                        Debug.Assert(!String.IsNullOrEmpty(xAttr.Name));
                        if (!String.IsNullOrEmpty(xAttr.Value)) {
                            switch (xAttr.Name) {
                                case "VER": oneComponent.Append(BdnaDelimiters.DELIMITER2_TAG);
                                    oneComponent.AppendFormat("version=\"{0}\"", xAttr.Value);
                                    break;
                                case "RELEASE": oneComponent.Append(BdnaDelimiters.DELIMITER2_TAG);
                                    oneComponent.AppendFormat("release=\"{0}\"", xAttr.Value);
                                    break;
                                case "LANGS": oneComponent.Append(BdnaDelimiters.DELIMITER2_TAG);
                                    oneComponent.AppendFormat("lang=\"{0}\"", xAttr.Value);
                                    break;
                                case "INSTALL_TIME": oneComponent.Append(BdnaDelimiters.DELIMITER2_TAG);
                                    oneComponent.AppendFormat("installDate=\"{0}\"", xAttr.Value);
                                    break;
                                case "INST_LOC": String installDir = xAttr.Value;
                                    if (installDir.EndsWith(@"\")) {
                                        installDir = installDir.Substring(0, installDir.Length - 1);
                                    }
                                    int index = installDir.LastIndexOf(@"\");
                                    if (index < installDir.Length && index != 2) {
                                        installDir = installDir.Substring(0, installDir.LastIndexOf(@"\"));
                                    }
                                    oneComponent.Append(BdnaDelimiters.DELIMITER2_TAG);
                                    oneComponent.AppendFormat("installDirectory=\"{0}\"", installDir);
                                    break;
                            }
                        }
                    }
                }

                if (oneComponent.Length > 0) {
                    iComponentCount++;
                    components.Append(oneComponent);
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                      0,
                      "Task Id {0}: {1} components found total.",
                      m_taskId,
                      iComponentCount);            
            return components;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, StringBuilder collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            }
            else if (collectedData == null || collectedData.Length <= 0) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            }
            else {
                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

        /// <summary>Database Server regex </summary>
        //private static readonly Regex s_versionRegex =
        //    new Regex(@"^Oracle Database .+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        /// <summary>Oracle Application Server Config File path. </summary>
        private string m_oracleAppServerConfigFilePath = String.Empty;

        /// <summary>Execution Timer</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId = String.Empty;

        private static string s_oracleAppServerDetails = @"OracleAppServerDetails";
        private static string s_oracleAppServerComponentsDetails = @"OracleAppServerComponentsDetails";
    }
}
