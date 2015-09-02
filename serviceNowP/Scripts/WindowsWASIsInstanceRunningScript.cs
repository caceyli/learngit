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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    public class WindowsWASIsInstanceRunningScript : ICollectionScriptRuntime    {

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
                ITftpDispatcher tftpDispatcher)         {

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
                                  "Task Id {0}: Collection script WindowsWASIsInstanceRunningScript.",
                                  m_taskId);

            try  {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null)  {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsWASIsInstanceRunningScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey("cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else  {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed.",
                                              m_taskId);
                    }
                }

                //Check WAS Profile path attribute
                if (!scriptParameters.ContainsKey("installDirectory")) {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing Profile Path Script parameter.",
                                          m_taskId);
                } else {
                    m_installHome = scriptParameters[@"installDirectory"];
                }

                //Check Server Name attribute
                if (!scriptParameters.ContainsKey("appsrv_Name"))    {
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing Server Name script parameter.",
                                          m_taskId);
                } else {
                    m_server = scriptParameters[@"appsrv_Name"];                  
                }

                // Check Remote Process Temp Directory
                if (!connection.ContainsKey(@"TemporaryDirectory"))  {
                    connection[@"TemporaryDirectory"] = @"%TMP%";
                }  else  {
                    if (!connection[@"TemporaryDirectory"].Equals(@"%TMP%"))  {
                        if (!Lib.ValidateDirectory(m_taskId, connection[@"TemporaryDirectory"].ToString(), cimvScope)) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} is not valid.",
                                                  m_taskId,
                                                  connection[@"TemporaryDirectory"].ToString());
                            resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;  //@TODO: change to RC_TEMP_DIRECTORY_NOT_EXIST
                        } else  {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Temporary directory {1} has been validated.",
                                                  m_taskId,
                                                  connection[@"TemporaryDirectory"].ToString());
                        }
                    }
                }

                if (resultCode == ResultCodes.RC_SUCCESS)  {
                    // EXTRA code for wsadmin
                    string strTempDir = connection["TemporaryDirectory"].ToString().Trim();
                    if (strTempDir.EndsWith(@"\"))  {
                        strTempDir = strTempDir.Substring(0, strTempDir.Length - 1);
                    }

                    string running = "False";
                    //string batchFile =  m_profilePath.Trim() + @"\bin\serverStatus.bat " + m_server + @" -user bdna -password bdna";
                    string s1 = m_installHome.Trim();
                    string s2 = s1.Replace(" ", "^ ");
                    string batchFile =  s2 + @"\bin\serverStatus.bat " + m_server;
                    //Console.WriteLine(batchFile);
                    using (IRemoteProcess rp =
                        RemoteProcess.ExecuteBatchFile(m_taskId, cimvScope, batchFile.ToString(),
                                                       connection, tftpPath, tftpPath_login, tftpPath_password, tftpDispatcher)) {
                        //This method will block until the entire remote process operation completes.
                        resultCode = rp.Launch();
                        
                        
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Remote process operation completed with result code {1}.",
                                              m_taskId,
                                              resultCode.ToString());

                        string[] arrOutputLine = rp.Stdout.ToString().Split("\r\n".ToCharArray());                            
                        for (int i = 0; i < arrOutputLine.Length; i++)  {
                            string output = arrOutputLine[i];
                            if (s_instanceRegex.IsMatch(output))  {
                                Match match = s_instanceRegex.Match(output);
                                string name = match.Groups["server"].ToString();
                                if (string.Equals(m_server, name)) {
                                    running = "True";
                                }
                            }
                        }
                    } 

                    //Console.WriteLine(running);
                    m_dataRow.Append(m_elementId).Append(',')
                        .Append(m_attributes["appSrv_isRunning"]).Append(',')
                        .Append(m_scriptParameters["CollectorId"]).Append(',')
                        .Append(m_taskId).Append(',')
                        .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                        .Append("appSrv_isRunning").Append(',')
                        .Append(BdnaDelimiters.BEGIN_TAG).Append(running).Append(BdnaDelimiters.END_TAG);                    
                }
            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsWASIsInstanceRunningScript.  Elapsed time {1}.\n{2}\nResult code changed to RC_PROCESSING_EXCEPTION.",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                    resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Unhandled exception in WindowsWASIsInstanceRunningScript.  Elapsed time {1}.\n{2}",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          ex.ToString());
                }
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsWASIsInstanceRunningScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, m_dataRow.ToString());
        
        }

        #endregion

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

        /// <summary>WebSphere Application Server Installation path. </summary>
        private string m_installHome = String.Empty;

        /// <summary>WebSphere Application Server Installation path. </summary>
        private string m_server = String.Empty;

        /// <summary>Execution Timer</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId = String.Empty;

        private static readonly Regex s_instanceRegex = new Regex("The Application Server \"(?<server>.+)\" is STARTED", RegexOptions.Compiled);
        
    }
}

