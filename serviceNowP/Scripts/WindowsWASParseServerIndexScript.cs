#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Suma Manvi
*   Creation Date: 2007/10/04
*
* Current Status
*       $Revision: 1.11 $
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

namespace bdna.Scripts
{
    public class WindowsWASParseServerIndexScript : ICollectionScriptRuntime
    {

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
                ITftpDispatcher tftpDispatcher)
        {
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
                                  "Task Id {0}: Collection script WindowsWASParseServerIndexScript.",
                                  m_taskId);
            try
            {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                // Check ManagementScope CIMV
                if (null == connection)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsWASParseServerIndexScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey(@"cimv2"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey(@"default"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          m_taskId);
                }
                else
                {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    defaultScope = connection[@"default"] as ManagementScope;

                    if (!cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              m_taskId);
                    }
                    else if (!defaultScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed.",
                                              m_taskId);
                    }
                }

                //Check Script attributes
                if (resultCode.Equals(ResultCodes.RC_SUCCESS))
                {
                    if (scriptParameters.ContainsKey("profilePath"))
                    {
                        m_profileHome = scriptParameters[@"profilePath"];
                    }
                    else
                    {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Profile Path parameter.",
                                              m_taskId);
                    }
                    if (scriptParameters.ContainsKey("cellName"))
                    {
                        m_cell = scriptParameters[@"cellName"];
                    }
                    else
                    {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Cell Name parameter.",
                                              m_taskId);
                    }
                    if (scriptParameters.ContainsKey("nodeName"))
                    {
                        m_node = scriptParameters[@"nodeName"];
                    }
                    else
                    {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Node Name parameter.",
                                              m_taskId);
                    }
                    if (scriptParameters.ContainsKey("appsrv_Name"))
                    {
                        m_server = scriptParameters[@"appsrv_Name"];
                    }
                    else
                    {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Missing parameter WAS Server Name parameter.",
                                              m_taskId);
                    }
                }
                // Check Remote Process Temp Directory
                if (resultCode.Equals(ResultCodes.RC_SUCCESS))
                {
                    if (!connection.ContainsKey(@"TemporaryDirectory"))
                    {
                        connection[@"TemporaryDirectory"] = @"%TMP%";
                    }
                    else
                    {
                        if (!connection[@"TemporaryDirectory"].Equals(@"%TMP%"))
                        {
                            if (!Lib.ValidateDirectory(m_taskId, connection[@"TemporaryDirectory"].ToString(), cimvScope))
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
                                                      "Task Id {0}: User specified temp directory has been validated.",
                                                      m_taskId);
                            }
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS)
                {
                    // Gather Variables Xml path for the cell and node
                    StringBuilder nVarPath = new StringBuilder();          
                    nVarPath.Append(m_profileHome).Append(@"\config\cells\").Append(m_cell).Append(@"\nodes\").Append(m_node).Append(@"\variables.xml").Append(@"=nodeVarData");
                    BuildDataRow(s_nodeVarPath, nVarPath);

                    // Get the resources file path
                    StringBuilder rsrcPath = new StringBuilder();
                    rsrcPath.Append(m_profileHome).Append(@"\config\cells\").Append(m_cell).Append(@"\nodes\").Append(m_node).Append(@"\servers\").Append(m_server).Append(@"\resources.xml").Append(@"=rsrcFileData");
                    BuildDataRow(s_rsrcFilePath, rsrcPath);                    

                    // Get the virtual hosts file path
                    StringBuilder virtHostsPath = new StringBuilder();
                    virtHostsPath.Append(m_profileHome).Append(@"\config\cells\").Append(m_cell).Append(@"\virtualhosts.xml").Append(@"=virtHostsFileData");
                    BuildDataRow(s_virtHostsFilePath, virtHostsPath);

                    // Parse ServerIndex file
                    StringBuilder fileContent = new StringBuilder();
                    StringBuilder serverIndx = new StringBuilder();
                    serverIndx.Append(m_profileHome).Append(@"\config\cells\").Append(m_cell).Append(@"\nodes\").Append(m_node).Append(@"\serverindex.xml");

                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Attempting to retrieve file {1}.",
                                          m_taskId,
                                          serverIndx.ToString());

                    if (Lib.ValidateFile(m_taskId, serverIndx.ToString(), cimvScope))
                    {
                        using (IRemoteProcess rp =
                                RemoteProcess.GetRemoteFile(m_taskId, cimvScope, serverIndx.ToString(), connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher))
                        {
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
                        // Parse ServerIndex file content
                        resultCode = parseServerIndxFile(fileContent.ToString(), m_server);
                    }
                    else
                    {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: WAS Server Index file: {1} does not exist.",
                                              m_taskId,
                                              serverIndx.ToString());
                        resultCode = ResultCodes.RC_SUCCESS;
                    }
                }
            }
            catch (Exception ex)
            {
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in WindowsWASParseServerIndexScript",
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
                                  "Task Id {0}: Collection script WindowsWASProfileFootprintScript2.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
        #endregion ICollectionScriptRuntime Members


        /// <summary>
        /// Parse WAS Server Index File.
        /// </summary>
        /// <param name="strFileContent">File Content</param>
        private ResultCodes parseServerIndxFile(string srvIndxContent, string server)
        {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REMOTE_FILE;
            if (String.IsNullOrEmpty(srvIndxContent)) return resultCode;
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Begin parsing WAS Server Index file.",
                                  m_taskId,
                                  server);
            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.LoadXml(srvIndxContent);
                XmlNodeList nodes = xDoc.GetElementsByTagName("serverEntries");
                StringBuilder portData = new StringBuilder();
                foreach (XmlNode node in nodes)
                {
                    string sName = node.Attributes["serverName"].Value;
                    string sType = node.Attributes["serverType"].Value;

                    if (string.Equals(server, sName))
                    {
                        StringBuilder st = new StringBuilder();
                        st.Append(sType);
                        BuildDataRow(s_serverType, st);
                        XmlNodeList cNodes = node.ChildNodes;
                        foreach (XmlNode child in cNodes)
                        {
                            if (string.Equals(child.Name, "specialEndpoints"))
                            {
                                StringBuilder pValue = new StringBuilder();
                                string pName = child.Attributes["endPointName"].Value;
                                XmlNodeList epNodes = child.ChildNodes;
                                foreach (XmlNode ep in epNodes)
                                {
                                    if (string.Equals(ep.Name, "endPoint"))
                                    {
                                        string pNum = ep.Attributes["port"].Value;
                                        StringBuilder port = new StringBuilder();
                                        port.Append(pNum);
                                        pValue.Append(@"PortName=").Append(pName).Append(@"<BDNA,1>").Append(@"PortNumber=").Append(pNum);
                                        /*if (string.Equals(pName, "WC_defaulthost")) {                                            
                                            BuildDataRow(s_port, port);
                                        } */                                       
                                        if (portData.Length > 0)
                                        {
                                            portData.Append(BdnaDelimiters.DELIMITER_TAG);
                                        }
                                        portData.Append(pValue.ToString());
                                    }
                                }
                            }
                        }

                        if (portData.Length > 0)
                        {
                            BuildDataRow(s_portsUsed, portData);
                        }
                    }
                }
                resultCode = ResultCodes.RC_SUCCESS;
            }
            catch (XmlException xex)
            {
                Lib.LogException(m_taskId,
                                 m_executionTimer,
                                 "Unhandled exception in WindowsWASParseServerIndexScript",
                                 xex);
            }
            return resultCode;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        ///
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, StringBuilder collectedData)
        {
            Console.WriteLine(collectedData);
            if (!m_attributes.ContainsKey(attributeName))
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            }
            else if (collectedData == null || collectedData.Length <= 0)
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

        /// <summary>WebSphere Application Server Profile path. </summary>
        private string m_profileHome = String.Empty;

        /// <summary>WebSphere Application Server Cell Name. </summary>
        private string m_cell = String.Empty;

        /// <summary>WebSphere Application Server Node Name. </summary>
        private string m_node = String.Empty;

        /// <summary>WebSphere Application Server Name. </summary>
        private string m_server = String.Empty;

        /// <summary>Execution Timer</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId = String.Empty;

        private static string s_serverType = @"appSrv_serverType";

        private static string s_portsUsed = @"portsUsed";

        // private static string s_port = @"appSrv_Port";

        private static string s_nodeVarPath = @"nodeVarPath";

        private static string s_rsrcFilePath = @"rsrcFilePath";
 
        private static string s_virtHostsFilePath = @"virtHostsFilePath";

    }
}


