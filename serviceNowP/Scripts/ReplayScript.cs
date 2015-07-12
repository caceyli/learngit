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
using System.IO;
using System.Net.Sockets;
using System.Text;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    public class ReplayScript : ICollectionScriptRuntime, IConnectionScriptRuntime {

        #region IConnectionScriptRuntime

        /// <summary>
        /// Connect method invoked by WinCS.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="connectionParameterSets">List of credential sets to use for connecting to the
        ///     remote database server.</param>
        /// <param name="tftpDispatcher">TFTP transfer request listener for dispatching TFTP transfer
        ///     requests.</param>
        /// 
        /// <returns>Operation results.</returns>
        public ConnectionScriptResults Connect(
                long                            taskId,
                IDictionary<string, string>[]   connectionParameterSets,
                string                          tftpPath,
                string                          tftpPath_login,
                string                          tftpPath_password,
                ITftpDispatcher                 tftpDispatcher) {
            return new ConnectionScriptResults(new Dictionary<string, object>(), ResultCodes.RC_SUCCESS, 0, 0);
        }

        #endregion IConnectionScriptRuntime
        #region ICollectionScriptRuntime

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
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            StringBuilder dataRow = new StringBuilder();

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Replay collection script.",
                                  m_taskId);

            StringBuilder attributeNames = new StringBuilder();
            IEnumerator<string> ie = attributes.Keys.GetEnumerator();

            if (ie.MoveNext()) {
                attributeNames.Append(ie.Current);
            }

            while (ie.MoveNext()) {
                attributeNames.Append(',')
                              .Append(ie.Current);
            }

            //
            // This loop will perform retries for socket
            // exceptions only.
            for (int retryCount = 0;
                 s_socketErrorRetryLimit > retryCount;
                 ++retryCount) {

                try {
                    string replayData = RetrieveDataFromSocket(elementId.ToString(),
                                                               attributeNames.ToString());

                    if (String.IsNullOrEmpty(replayData)) {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: No data returned from Replay Server",
                                              m_taskId);
                    } else {
                        string[] dataRowValues = replayData.Split(s_separator, StringSplitOptions.RemoveEmptyEntries);

                        if (1 == dataRowValues.Length && replayData.Equals(dataRowValues[0])) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Invalid data returned from Replay Server.\nSplit using these delimiters \"{1}\" failed.\nData returned from Replay Server:\n{2}",
                                                  m_taskId,
                                                  s_separator,
                                                  replayData);
                        } else {

                            for (int i = 0;
                                 dataRowValues.Length > i;
                                 i += 2) {

                                if (!attributes.ContainsKey(dataRowValues[i])) {
                                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                                          0,
                                                          "Task Id {0}: Expected attribute name \"{1}\" not found in attribute map.",
                                                          m_taskId,
                                                          dataRowValues[i]);
                                } else {
                                    dataRow.Append(elementId)
                                           .Append(',')
                                           .Append(attributes[dataRowValues[i]])
                                           .Append(',')
                                           .Append(scriptParameters[@"CollectorId"])
                                           .Append(',')
                                           .Append(taskId)
                                           .Append(',')
                                           .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds) // @todo fix this
                                           .Append(',')
                                           .Append(dataRowValues[i])
                                           .Append(',')
                                           .Append(BdnaDelimiters.BEGIN_TAG)
                                           .Append(dataRowValues[i + 1])
                                           .Append(BdnaDelimiters.END_TAG);
                                }

                            }

                        }

                    }

                    break;
                } catch (SocketException sex) {

                    //
                    // Ignore socket errors until we reach the
                    // retry limit.
                    if (s_socketErrorRetryLimit <= retryCount + 1) {
                        resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                        StringBuilder sb = new StringBuilder();
                        sb.Append("Socket Exception Error Code: ")
                          .AppendLine(sex.ErrorCode.ToString())
                          .Append("Message: ")
                          .AppendLine(sex.Message)
                          .Append("Help Link: ")
                          .AppendLine(sex.HelpLink)
                          .Append("Source: ")
                          .AppendLine(sex.Source)
                          .Append("Target Site:")
                          .AppendLine((null == sex.TargetSite) ? null : sex.TargetSite.ToString())
                          .AppendLine("Stack Trace:")
                          .AppendLine(sex.StackTrace);
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Replay server connection failed.  Elapsed time {1}, retries attempted {2}.\n{3}",
                                              m_taskId,
                                              executionTimer.Elapsed.ToString(),
                                              s_socketErrorRetryLimit.ToString(),
                                              sb.ToString());
                    }

                } catch (Exception ex) {
                    resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                    Lib.LogException(m_taskId,
                                     executionTimer,
                                     "Unhandled exception in Replay script",
                                     ex);
                    break;
                }

            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Replay collection script.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion ICollectionScriptRuntime

        /// <summary>
        /// Connect to the Replay server and get the task
        /// result data for the specified task.
        /// </summary>
        /// 
        /// <param name="elementId">Element Id being collected.</param>
        /// <param name="attrNames">List of collected attribute names.</param>
        /// 
        /// <returns>Raw result string received from the Replay server.</returns>
        private string RetrieveDataFromSocket(
                string                          elementId,
                string                          attrNames) {
            string result = null;

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Connecting to {1}:{2}.",
                                  m_taskId,
                                  m_replayServer,
                                  m_replayPort.ToString());
            Stopwatch swatch = Stopwatch.StartNew();
            TcpClient client = null;

            try {
                client = new TcpClient(m_replayServer, int.Parse(m_replayPort));
            } finally {
                swatch.Stop();
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Connect operation to {1} complete. Elapsed time {2}.",
                                      m_taskId,
                                      m_replayServer,
                                      swatch.Elapsed.ToString());
            }

            using (client) {
                NetworkStream ns = client.GetStream();

                using (ns) {

                    swatch.Reset();
                    StreamWriter sw = new StreamWriter(ns);
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Sending {1} bytes to replay server.",
                                          m_taskId,
                                          (m_taskId.Length + elementId.Length + attrNames.Length + sw.NewLine.Length * 3).ToString());

                    try {
                        swatch.Start();
                        sw.WriteLine(m_taskId);
                        sw.WriteLine(elementId);
                        sw.WriteLine(attrNames);
                        sw.Flush();
                    } finally {
                        swatch.Stop();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Send operation complete.  Elapsed time {1}.",
                                              m_taskId,
                                              swatch.Elapsed.ToString());
                    }

                    swatch.Reset();
                    StreamReader sr = new StreamReader(ns);
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Posting read for replay server response.",
                                          m_taskId);

                    try {
                        swatch.Start();
                        result = sr.ReadToEnd();
                    } finally {
                        swatch.Stop();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Read operation complete, {1} bytes received.  Elapsed time {2}.",
                                              m_taskId,
                                              (String.IsNullOrEmpty(result)) ? 0 : result.Length,
                                              swatch.Elapsed.ToString());
                    }

                }

            }

            return result;
        }

        /// <summary>Replay server address.</summary>
        private string                          m_replayServer = @"$THIS_HOST$";

        /// <summary>Replay server listening port.</summary>
        private string                          m_replayPort = @"$THIS_PORT$";

        /// <summary>Task Id being collected against.</summary>
        private string                          m_taskId;

        /// <summary>Delimiters for parsing the string returned from the Replay server.</summary>
        private static readonly string[]        s_separator = new string[] {@"<BDNA,TASK,RESULT>"};

        /// <summary>Number of times we'll retry for socket errors.</summary>
        private static readonly int             s_socketErrorRetryLimit = 3;
    }

}
