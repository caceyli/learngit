#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: David Chou
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.7 $
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Management;
using System.Text;
using bdna.ScriptLib;
using bdna.Shared;
using System.Text.RegularExpressions;

namespace bdna.Scripts
{

    /// <summary>
    /// Collect software usage information from the QPD agent
    /// </summary>
    public class SoftwareUsageScript : ICollectionScriptRuntime
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

            m_taskId = taskId.ToString();
            m_connection = connection;
            m_tftpDispatcher = tftpDispatcher;
            m_tftpPath = tftpPath;
            m_tftpPath_login = tftpPath_login;
            m_tftpPath_password = tftpPath_password;

            Stopwatch executionTimer = Stopwatch.StartNew();
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script SoftwareUsageScript.",
                                  m_taskId);

            try
            {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (null == connection)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to SoftwareUsageScript is null.",
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
                    else
                    {

                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null))
                        {
                            resultCode = GetAgentPath(wmiRegistry);
                        }
                        if (!String.IsNullOrEmpty(m_serviceDataPath))
                        {
                            if (ResultCodes.RC_SUCCESS == resultCode)
                            {
                                resultCode = GetUsageStatistics(cimvScope);

                                if (ResultCodes.RC_SUCCESS != resultCode)
                                {

                                    m_resultFileContents = @"";

                                    resultCode = ResultCodes.RC_SUCCESS;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in SoftwareUsageScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            dataRow.Append(elementId)

                   .Append(',')

                   .Append(attributes[@"serviceSUData"])

                   .Append(',')

                   .Append(scriptParameters[@"CollectorId"])

                   .Append(',')

                   .Append(taskId)

                   .Append(',')

                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)

                   .Append(',')

                   .Append(@"serviceSUData")

                   .Append(',')

                   .Append(BdnaDelimiters.BEGIN_TAG)

                   .Append(m_resultFileContents)

                   .Append(BdnaDelimiters.END_TAG);

            CollectionScriptResults result = new CollectionScriptResults(resultCode,
                                                                         0,
                                                                         null,
                                                                         null,
                                                                         null,
                                                                         false,
                                                                         dataRow.ToString());
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script SoftwareUsageScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
        #endregion

        /// <summary>
        /// Get agent install location.
        /// </summary>
        /// <param name="wmiRegistry">Remote registry connection.</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes GetAgentPath(ManagementClass wmiRegistry)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            resultCode = Lib.GetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"SoftwareUsagePath", out m_serviceDataPath);
            if (!String.IsNullOrEmpty(m_serviceDataPath))
            {
                m_isAgentInstalled = true;
            }
            else
            {
                resultCode = Lib.GetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"LogPath", out m_serviceDataPath);
            }
            return resultCode;
        }

        /// <summary>
        /// Collect usage statistics from agent.
        /// </summary>
        /// <param name="cimvScope">WMI connection.</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes GetUsageStatistics(ManagementScope cimvScope)
        {
            Lib.Logger.TraceEvent(TraceEventType.Error,
                                  0,
                                  "Task Id {0}: Attempting to get usage statistics",
                                  m_taskId);
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            if (String.IsNullOrEmpty(m_serviceDataPath))
            {
                return resultCode;
            }

            String resultPath = m_serviceDataPath + "result.cvs";
            StringBuilder fileContent = new StringBuilder();

            if (Lib.ValidateFile(m_taskId, resultPath, cimvScope))
            {
                using (IRemoteProcess rp =
                        RemoteProcess.GetRemoteFile(m_taskId, cimvScope, resultPath, m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher))
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
                string[] lines = fileContent.ToString().Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                m_resultFileContents = String.Join(BdnaDelimiters.DELIMITER_TAG, lines);

                resultCode = ResultCodes.RC_SUCCESS;
            }

            return resultCode;
        }

        /// <summary>Registry path for QPD agent.</summary>
        private static readonly string s_agentRegistryPath = @"software\PSSOFT\QPDiscovery\AGENT";

        /// <summary>Database assigned task id.</summary>
        private string m_taskId;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;
        private string m_tftpPath = string.Empty;
        private string m_tftpPath_login = string.Empty;
        private string m_tftpPath_password = string.Empty;

        /// <summary>Flag to indicate if the agent is installed</summary>
        private bool m_isAgentInstalled;

        /// <summary>Agent installation directory.</summary>
        private string m_serviceDataPath;
        
        /// <summary>Contents of the results.cvs file.</summary>
        private string m_resultFileContents;
    }
}
