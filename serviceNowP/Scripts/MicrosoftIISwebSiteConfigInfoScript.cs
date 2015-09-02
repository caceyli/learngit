#region Copyright
/******************************************************************
*
*          Module: MicrosoftIISwebSiteConfigScript
* Original Author: Rekha Rani
*   Creation Date: 2009/10/05
*
* Current Status
*       $Revision: 1.10 $
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

using System.Runtime.InteropServices;



namespace bdna.Scripts {
    public class MicrosoftIISwebSiteConfigInfoScript : ICollectionScriptRuntime {
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
        public CollectionScriptResults ExecuteTask(long taskId, 
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
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_executionTimer = Stopwatch.StartNew();
            ManagementScope cimvScope = null;
            ManagementScope defaultScope = null;
            ManagementScope iisScope = null;

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script MicrosoftIISwebSiteConfigInfoScript.",
                                  m_taskId);
            try {                
                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to MicrosoftIISwebSiteConfigInfoScript is null.",
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
                    iisScope = connection[@"iis"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV2 namespace failed",
                                              m_taskId);
                    } else if (!iisScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to IIS namespace failed",
                                              m_taskId);
                    } 
                    StringBuilder WebSitesConfig = new StringBuilder();

                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Attempting to connect to MicrosoftIISV2 namespace",
                                          m_taskId);

                        /**** Collect IIS website configuration info ****/

                        //get IIS setting 
                        System.Management.ObjectQuery oQuery = new System.Management.ObjectQuery("SELECT * FROM IISWebServerSetting");

                        //Execute the query                  
                        ManagementObjectSearcher oSearcher = new ManagementObjectSearcher(iisScope, oQuery);

                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Query IISWebServerSetting object.",
                                                      m_taskId);

                        //Get the results
                        ManagementObjectCollection oReturnCollection = oSearcher.Get();

                        Dictionary<string, string> dic = new Dictionary<string, string>();
                        Dictionary<string, Dictionary<string, string>> dic1 = new Dictionary<string, Dictionary<string, string>>();

                        /*
                         *  The ServerAutoStart property indicates if the server instance should start automatically when the service is started.
                         *  This property is reset automatically when a server instance is stopped or restarted, to maintain state across service restarts. For example, if a server is stopped, the value of this property is set to false so that if the service is stopped and restarted, the server will remain stopped. Likewise, if a server is started, this property is set to true, allowing the server to remain running whenever the service is running.                
                         */

                        foreach (ManagementObject oReturn in oReturnCollection) {
                            Dictionary<string, string> dic0 = new Dictionary<string, string>();

                            //Servercomment is shown as Description in IIS Manager                    
                            dic0.Add("ServerComment", oReturn["ServerComment"].ToString());
                            dic0.Add("ServerAutoStart", oReturn["ServerAutoStart"].ToString());

                            // ServerBindings[] array of ServerBinding
                            // The ServerBindings property specifies a string that IIS uses to determine which network endpoints are used by the server instance. The string format is IP:Port:Hostname.

                            ManagementBaseObject[] ServerBindings = (ManagementBaseObject[])oReturn["ServerBindings"];

                            String serverBinding = null;
                            for (int i = 0; i < ServerBindings.Length; i++) {
                                PropertyDataCollection pc = ServerBindings[i].Properties;

                                String ip = null;
                                String port = null;
                                String hostname = null;

                                foreach (PropertyData property in pc) {
                                    if (property.Name.Equals("Hostname")) {
                                        hostname = (string)property.Value;
                                    }

                                    if (property.Name.Equals("IP")) {
                                        ip = (string)property.Value;
                                    }

                                    if (property.Name.Equals("Port")) {
                                        port = (string)property.Value;
                                    }

                                }
                                if (serverBinding == null) {
                                    serverBinding = ip + ":" + port + ":" + hostname;
                                } else {
                                    serverBinding += "<BDNA,2>" + ip + ":" + port + ":" + hostname;
                                }
                            }


                            //SecureBindings[] array of SecureBinding
                            //The SecureBindings property specifies a string that is used by IIS to determine which secure network endpoints are used by the server instance. The string format is IP: Port.

                            ManagementBaseObject[] SecureBindings = (ManagementBaseObject[])oReturn["SecureBindings"];

                            String SecureBinding = null;
                            for (int i = 0; i < SecureBindings.Length; i++) {
                                PropertyDataCollection pc = SecureBindings[i].Properties;

                                String ip = null;
                                String port = null;
                                String hostname = null;

                                foreach (PropertyData property in pc) {
                                    if (property.Name.Equals("Hostname")) {
                                        hostname = (string)property.Value;
                                    }

                                    if (property.Name.Equals("IP")) {
                                        ip = (string)property.Value;
                                    }

                                    if (property.Name.Equals("Port")) {
                                        port = (string)property.Value;
                                    }
                                }

                                if (SecureBinding == null) {
                                    SecureBinding = ip + ":" + port + ":" + hostname;
                                } else {
                                    SecureBinding += "<BDNA,2>" + ip + ":" + port + ":" + hostname;
                                }
                            }

                            dic0.Add("ServerBindings", serverBinding);
                            dic0.Add("SecureBindings", SecureBinding);

                            dic1.Add(oReturn["Name"].ToString(), dic0);

                            dic.Add(oReturn["Name"].ToString(), oReturn["ServerComment"].ToString());

                        }

/////  ##### RR NEW DEBUG Oct18

                        //// ************** #3 Find IIS Filter information: *****************

                        System.Management.ObjectQuery filterQuery = new System.Management.ObjectQuery("SELECT * FROM IIsFilterSetting");

                        //Execute the query                  
                        ManagementObjectSearcher filter = new ManagementObjectSearcher(iisScope, filterQuery);

                        //Get the results
                        ManagementObjectCollection ReturnFilter = filter.Get();

                        Dictionary<string, string> dicFilter = new Dictionary<string, string>();

                        //loop through found info  
                        foreach (ManagementObject oReturn in ReturnFilter) {                      
                            String name = oReturn["Name"].ToString();
                            String filterPath = oReturn["FilterPath"].ToString();

                            if (filterPath.Contains("iisWASPlugin_http.dll")) {
                                foreach (String s in dic.Keys) {
                                    if (name.StartsWith(s + "/Filters") || name.StartsWith(s + "/filters") || name.StartsWith(s + "/FILTERS")) {
                                        dicFilter.Add(s, filterPath);                                                                         
                                    }
                                }
                            }
                        }

                        /***** Find directory Path for web sites *****/
                        /** comment out following code because on some Windows 2003 server following WQL is not returning all records
                        /*
                        System.Management.ObjectQuery pathQuery = new System.Management.ObjectQuery("SELECT Path, name, ScriptMaps FROM IIsWebVirtualDirSetting");

                        //Execute the query                  
                        ManagementObjectSearcher path = new ManagementObjectSearcher(Scope, pathQuery);

                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Query IIsWebVirtualDirSetting object.",
                                                      m_taskId);

                        //Get the results
                        ManagementObjectCollection ReturnPath = path.Get();
                        */

                        Dictionary<string, string> dicPath = new Dictionary<string, string>();
                        Dictionary<string, string> dicScriptProcessor = new Dictionary<string, string>();

                        foreach (String d_key in dic.Keys) {
                            //String sql = "SELECT * FROM IIsWebVirtualDirSetting where name = '" + d_key + "/ROOT'";
                            String sql = "SELECT Path, name, ScriptMaps FROM IIsWebVirtualDirSetting where name = '" + d_key + "/ROOT'";

                            System.Management.ObjectQuery pathQuery = new System.Management.ObjectQuery(sql);

                            //Execute the query                  
                            ManagementObjectSearcher path = new ManagementObjectSearcher(iisScope, pathQuery);

                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Query IIsWebVirtualDirSetting object.",
                                                          m_taskId);

                            //Get the results
                            ManagementObjectCollection ReturnPath = path.Get();

                        //loop through found info                
                            foreach (ManagementObject oReturn in ReturnPath) {
                                String name = (String)oReturn["name"];
                                String Path = (String)oReturn["Path"];

                                //// RR NEW

                                String iisproxyPath_ScriptProcessor = null;

                                ManagementBaseObject[] scriptMaps = (ManagementBaseObject[])oReturn["ScriptMaps"];

                                for (int i = 0; i < scriptMaps.Length; i++) {
                                    PropertyDataCollection pc = scriptMaps[i].Properties;
                                    foreach (PropertyData property in pc) {
                                        if (property.Name.Equals("ScriptProcessor") && property.Value.ToString().Contains("iisproxy.dll")) {
                                            iisproxyPath_ScriptProcessor = (String)property.Value;
                                            break;

                                        }
                                    }
                                }

                                //// RR NEW END

                                foreach (String s in dic.Keys) {
                                    if (name.Equals(s + "/ROOT") || name.Equals(s + "/root")) {
                                        dicPath.Add(s, Path);
                                        dicScriptProcessor.Add(s, iisproxyPath_ScriptProcessor);
                                    }
                                }
                            }
                        }


                        foreach (String s in dic1.Keys) {
                            WebSitesConfig.Append("Name=").Append(s).Append("<BDNA,1>");
                            WebSitesConfig.Append("Desc=").Append(dic1[s]["ServerComment"]).Append("<BDNA,1>");
                            WebSitesConfig.Append("ServerBindings=").Append(dic1[s]["ServerBindings"]).Append("<BDNA,1>");
                            WebSitesConfig.Append("SecureBindings=").Append(dic1[s]["SecureBindings"]).Append("<BDNA,1>");
                            WebSitesConfig.Append("ServerAutoStart=").Append(dic1[s]["ServerAutoStart"]).Append("<BDNA,1>");

                            if (dicPath.ContainsKey(s)) {
                                WebSitesConfig.Append("Local Path=").Append(dicPath[s]).Append("<BDNA,1>");
                            }
                            if (dicScriptProcessor.ContainsKey(s)) {
                                WebSitesConfig.Append("iisproxyPath_ScriptProcessor=").Append(dicScriptProcessor[s]).Append("<BDNA,1>");
                            }

                            if (dicFilter.ContainsKey(s)) {
                                WebSitesConfig.Append("iisWASPlugin_http_dll_Path=").Append(dicFilter[s]).Append("<BDNA,1>");
                            }

                            WebSitesConfig.Append("<BDNA,>");
                        }

                        // RR DEBUG ONLY:
                        //Console.WriteLine("\nWebSitesConfigInfo=" + WebSitesConfig);

                        this.BuildDataRow(@"websiteDetail", WebSitesConfig);

                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in MicrosoftIISwebSiteConfigInfoScript.  Elapsed time {1}.\n{2}Result code changed to RC_PROCESSING_EXECEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in MicrosoftIISwebSiteConfigInfoScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script MicrosoftIISWebSiteConfigScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());

            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_dataRow.ToString());
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        /// 

        private void BuildDataRow(string attributeName, StringBuilder collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
                //else if (string.IsNullOrEmpty(collectedData)) {
            } else if (collectedData == null || collectedData.Length <= 0) {
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
                         .Append(attributeName)
                         .Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }
        

        /// <summary>server and credential. </summary>

        private string server;
        private string userName;
        private string password;

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

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;
        
    }
}
