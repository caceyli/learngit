#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.18 $
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
using System.Diagnostics;
using System.Management;
using System.Text;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Summary description for WinTypingStaticScript.
    /// </summary>
    public class PotentialWinTypingStaticScript : ICollectionScriptRuntime {

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
                string                          tftpPath,
                string                          tftpPath_login,
                string                          tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script PotentialWinTypingStaticScript.",
                                  m_taskId);

            try {
                ManagementScope cimvScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to PotentialWinTypingStaticScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    } else {
                        StringBuilder idString = new StringBuilder();
                        resultCode = GetIdString(cimvScope, idString);

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"typingData"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"typingData")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG)
                                   .Append(idString)
                                   .Append(BdnaDelimiters.END_TAG);
                        }

                    }

                }

            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in PotentialWinTypingStaticScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            CollectionScriptResults result = new CollectionScriptResults(resultCode,
                                                                         0,
                                                                         null,
                                                                         null,
                                                                         null,
                                                                         false,
                                                                         dataRow.ToString());
            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script PotentialWinTypingStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        #endregion

        /// <summary>
        /// Use WMI to get Windows typing information.
        /// </summary>
        /// 
        /// <param name="scope">WMI connection.</param>
        /// <param name="results">Target buffer for result.</param>
        /// 
        /// <returns>Operation resutl code.</returns>
        public ResultCodes GetIdString(
                ManagementScope                 scope,
                StringBuilder                   results) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            ManagementObjectCollection moc = null;
            ManagementObjectSearcher mos = new ManagementObjectSearcher(scope,
                                                                        new SelectQuery(s_win32OperatingSystemClassName,
                                                                                        null,
                                                                                        s_win32OsProperties));

            using (mos) {
                resultCode = Lib.ExecuteWqlQuery(m_taskId, mos, out moc);
            }

            if (null != moc) {

                using (moc) {

                    if (ResultCodes.RC_SUCCESS == resultCode) {

                        foreach (ManagementObject mo in moc) {
                            results.Append(s_dataRowItemName)
                                   .Append(@"=")
                                   .Append(mo.Properties[s_win32OsProperties[0]].Value)
                                   .Append(@"(")
                                   .Append(mo.Properties[s_win32OsProperties[1]].Value)
                                   .Append(@")");
                        }

                    }

                }

            }

            return resultCode;
        }

        /// <summary>Database assigned task Id.</summary>
        private string                          m_taskId;

        /// <summary>WMI class to query.</summary>
        private static readonly string          s_win32OperatingSystemClassName = @"Win32_OperatingSystem";

        /// <summary>WMI properties we're interested in.</summary>
        private static readonly string[]        s_win32OsProperties = new string[] {@"Caption",
                                                                                    @"BuildNumber"};

        /// <summary>Data row item name.</summary>
        private static readonly string          s_dataRowItemName = @"operatingSystem.idString";
    }

}
