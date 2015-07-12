#region Copyright
/******************************************************************
*
*          Module: Windows WAS ADM Hosts Lookup Script
* Original Author: Suma Manvi
*   Creation Date: 2009/10/26
*
* Current Status
*       $Revision: 1.6 $
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

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts
{
    public class WindowsWASHostsLookupScript : ICollectionScriptRuntime
    {
        /// <summary>
        /// Execute nslookup command remotely.
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
                                  "Task Id {0}: Collection script WindowsWASHostsLookupScript.",
                                  m_taskId);
            try
            {
                // Check ManagementScope CIMV
                ManagementScope cimvScope = null;
                if (connection == null)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WindowsWASHostsLookupScript is null.",
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
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    if (!cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                     }
                 }
                 
                 //Check script parameter
                 if (!scriptParameters.ContainsKey("hostsToLookup"))
                 {
                     resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                     Lib.Logger.TraceEvent(TraceEventType.Error,
                                           0,
                                           "Task Id {0}: Missing parameter hostsToLookup.",
                                            m_taskId);
                 }
                 else
                 {
                     m_hostsToLookup = scriptParameters[@"hostsToLookup"];
                 }

                 // Check Remote Process Temp Directory
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
                                                   "Task Id {0}: Temporary directory {1} has been validated.",
                                                    m_taskId,
                                                    connection[@"TemporaryDirectory"].ToString());
                         }
                     }
                 }

                 if (resultCode == ResultCodes.RC_SUCCESS)
                 {
                     StringBuilder val = new StringBuilder();
                     String[] hosts = m_hostsToLookup.Split(',');
                     foreach (string host in hosts)
                     {
                         StringBuilder commandLine = new StringBuilder();
                         if (val.ToString().Length != 0)
                         {
                             val.Append(@"<BDNA,>");
                         }
                         val.Append(host).Append(@"<BDNA,1>");                            
                         commandLine.Append(@"nslookup ").Append(host);                            
                         Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                               0,
                                               "Task Id {0}: Attempting to retrieve command output {1}.",
                                               m_taskId,
                                               commandLine);
                         String collectedData = "";
                         using (IRemoteProcess rp = RemoteProcess.ExecuteBatchFile(
                                               m_taskId,                   // Task Id to log against.
                                               cimvScope,                  // assuming Remote process uses cimv2 management scope
                                               commandLine.ToString(),     // batch file
                                               connection,                 // connection dictionary.
                                               tftpPath,
                                               tftpPath_login,
                                               tftpPath_password,
                                               tftpDispatcher))
                         {
                             //This method will block until the entire remote process operation completes.
                             resultCode = rp.Launch();

                             Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                   0,
                                                   "Task Id {0}: Batch file executed completed with result code {1}.",
                                                   m_taskId,
                                                   resultCode.ToString());

                             collectedData = rp.Stdout.ToString();

                             if (string.IsNullOrEmpty(collectedData))
                             {
                                 Lib.Logger.TraceEvent(TraceEventType.Error,
                                                       0,
                                                       "Task Id {0}: Script completed sucessfully with no data to return.",
                                                        m_taskId);
                                 resultCode = ResultCodes.RC_REMOTE_COMMAND_EXECUTION_ERROR;
                             }
                             else
                             {
                                 string[] collectedDataArr = collectedData.Split("\r\n".ToCharArray());
                                 bool flag = false;
                                 for (int i = 0; i < collectedDataArr.Length; i++)
                                 {
                                     string output = collectedDataArr[i];                                                                              
                                     if (s_nameRegex.IsMatch(output))
                                     {
                                         Match match = s_nameRegex.Match(output);
                                         string name = match.Groups["name"].ToString();
                                         val.Append(@"DBHost_DNSHostName=").Append(name);
                                         flag = true;
                                     }
                                     if ((s_addrRegex.IsMatch(output)) && (flag))
                                     {
                                         Match match = s_addrRegex.Match(output);
                                         string addr = match.Groups["address"].ToString();
                                         val.Append(@"<BDNA,1>DBHost_IPAddr=").Append(addr);
                                     }                                        
                                 }
                             }
                         }
                     }
                        
                     m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes["lookedUpHosts"]).Append(',')
                         .Append(m_scriptParameters["CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append("lookedUpHosts").Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(val).Append(BdnaDelimiters.END_TAG);
                  }
            }
            catch (Exception ex)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unhandled exception in WindowsWASHostsLookupScript.  Elapsed time {1}.\n{2}",
                                      m_taskId,
                                      m_executionTimer.Elapsed.ToString(),
                                      ex.ToString());
            }
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WindowsWASHostsLookupScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  m_executionTimer.Elapsed.ToString(),
                                  resultCode);
            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_dataRow.ToString());

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

        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Stopwatch for tracking all time since start of script execution.</summary>
        private Stopwatch m_executionTimer = null;

        /// <summary>WebSphere Application Server lookup hosts. </summary>
        private string m_hostsToLookup = String.Empty;

        private static readonly Regex s_nameRegex = new Regex("name:\\s*(?<name>\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_addrRegex = new Regex("address:\\s*(?<address>\\S+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    }
}

