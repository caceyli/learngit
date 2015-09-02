#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.23 $
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
using System.Management;
using System.Text;
using System.Runtime.InteropServices;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script for level 2 Windows type information.
    /// </summary>
    public class WinTypingStaticScript : ICollectionScriptRuntime {

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
            string taskIdString = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WinTypingStaticScript.",
                                  taskIdString);

            try {
                ManagementScope cimvScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WinTypingStaticScript is null.",
                                          taskIdString);
                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          taskIdString);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              taskIdString);
                    } else {
                        StringBuilder idString = new StringBuilder();
                        resultCode = GetIdString(taskIdString, cimvScope, idString);

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"windowsTypingData"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"windowsTypingData")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG)
                                   .Append(idString)
                                   .Append(BdnaDelimiters.END_TAG);
                        }

                    }

                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access Win32_OperatingSystem WMI Class.\nMessage: {1}",
                                      taskIdString,
                                      me.Message);
                if (me.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          taskIdString,
                                          me.InnerException.Message);
                }
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;
            } catch (COMException ce) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Not enough privilege to access run WMI query.\nMessage: {1}.",
                                      taskIdString,
                                      ce.Message);
                if (ce.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          taskIdString,
                                          ce.InnerException.Message);
                }
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;
            } catch (Exception ex) {
                Lib.LogException(taskIdString,
                                 executionTimer,
                                 "Unhandled exception in WinTypingStaticScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WinTypingStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }
        #endregion

        /// <summary>
        /// Use WMI to get Windows typing information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="scope">WMI connection.</param>
        /// <param name="results">Target buffer for result.</param>
        /// 
        /// <returns>Operation resutl code.</returns>
        public ResultCodes GetIdString(
                string                          taskId,
                ManagementScope                 scope,
                StringBuilder                   results) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            ManagementObjectCollection moc = null;
            ManagementObjectSearcher mos = new ManagementObjectSearcher(scope,
                                                                        new SelectQuery(s_win32OperatingSystemClassName,
                                                                                        null,
                                                                                        s_win32OsProperties));

            using (mos) {
                resultCode = Lib.ExecuteWqlQuery(taskId, mos, out moc);
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

        /// <summary>WMI class to query.</summary>
        private static readonly string          s_win32OperatingSystemClassName = @"Win32_OperatingSystem";

        /// <summary>WMI properties we're interested in.</summary>
        private static readonly string[]        s_win32OsProperties = new string[] {@"Caption",
                                                                                    @"BuildNumber"};

        /// <summary>Data row item name.</summary>
        private static readonly string          s_dataRowItemName = @"operatingSystem.idString";
    }

}
