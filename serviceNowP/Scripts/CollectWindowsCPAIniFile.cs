using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.Remoting;
using System.Text;
using System.Text.RegularExpressions;
using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection task for Windows remote profile cleanup.  Does a blind
    /// delete of the remote directory corresponding to the Windows profile
    /// for the user specified in the connection object.
    /// </summary>
    public class CollectWindowsCPAIniFile : ICollectionScriptRuntime {

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
                ITftpDispatcher tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            string taskIdString = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            StringBuilder parsedIniFileData = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script CollectWindowsCPAINIFile.",
                                  taskIdString);

            Lib.Logger.TraceEvent(TraceEventType.Information, 0, "Task Id {0}: Hash Dump = {1}", taskIdString, scriptParameters.ToString());
            string remoteFileName = scriptParameters[@"CPAWindowsIniFilePath"];
            StringBuilder collectedCPAttr = new StringBuilder();
            string logData = null;

            Lib.Logger.TraceEvent(TraceEventType.Information,
                                  0,
                                  "Passed file name into the script : {0}",
                                  remoteFileName);

            Lib.Logger.TraceEvent(TraceEventType.Information,
                                  0,
                                  "Task Id {0}: Running TFTP to collect the contents of CPA INI file on a remote system",
                                  taskIdString);


            MemoryStream ms = new MemoryStream(4096);

            using (TraceListener tl = new TextWriterTraceListener(ms)) {
                tl.TraceOutputOptions = TraceOptions.DateTime | TraceOptions.ProcessId | TraceOptions.ThreadId;

                try {
                    Lib.Logger.Listeners.Add(tl);
                    //
                    // Create a remote process object to manage the 
                    // transfer of the remote file contents to our
                    // script.
                    // 
                    // Many of these values are passed to the script from
                    // WinCs to give the Remote Process library access to
                    // Facilities outside the script sandbox.  The script
                    // need not care what these values are, just pass them
                    // through.
                    //
                    // Wrap the RemoteProcess in a using clause so that resources
                    // are automatically released when the remote process
                    // operation completes.

                    ManagementScope cimvScope = connection[@"cimv2"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              taskIdString);
                        return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
                    }
                    connection[@"TemporaryDirectory"] = @"c:\";

                    // collect data from multiple files
                    if (remoteFileName.Contains(",")) {
                        Lib.Logger.TraceEvent(TraceEventType.Information,
                                              0,
                                              "CPAIniFilePath has multiple INI files path (" + remoteFileName + ")",
                                              taskIdString);
                        string[] iniFiles = remoteFileName.Split(',');
                        for (int fileCount = 0; fileCount < iniFiles.Length; fileCount++) {
                            String fileName = iniFiles[fileCount];
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                              0,
                                              "CPA file path is " + fileName,
                                              taskIdString);
                            using (IRemoteProcess rp =
                            RemoteProcess.GetRemoteFile(taskIdString,        // Task Id to log against.
                                                        cimvScope,   // WMI connection, passed to collection script from WinCs
                                                        fileName,    // Name of remote file to retrieve.
                                                        connection, // credential hash passed to all collection/connection scripts
                                                        tftpPath,
                                                        tftpPath_login,
                                                        tftpPath_password,
                                                        tftpDispatcher)) {      // TFTP listener, passed to script from WinCs

                                //
                                // Launch the remote process.  This method will
                                // block until the entire remote process operation
                                // completes.
                                ResultCodes rc = rp.Launch();

                                //
                                // Once you get control back, there are properties that
                                // can be checked to information about the outcome of the
                                // operation, obtain log data, and retrieve collection results.
                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Remote process operation completed with result code" + rc.ToString(),
                                                      taskIdString);

                                if (null != collectedCPAttr) {
                                    collectedCPAttr.Append(rp.Stdout);
                                }
                            }
                        }
                        if (null != collectedCPAttr) {

                            int MIN_SECTIONS = 1;
                            // string collectedCPAttr = sb.ToString();
                            // System.Console.WriteLine(collectedCPAttr);

                            string[] cpAttrSections = new Regex(@"\[").Split(collectedCPAttr.ToString());
                            // System.Console.WriteLine("{0} sections in text:", cpAttrSections.Length);
                            if (cpAttrSections.Length > 0) {
                                for (int secCtr = 0; secCtr < cpAttrSections.Length; secCtr++) {
                                    // System.Console.WriteLine("This Section");
                                    // System.Console.WriteLine(cpAttrSections[secCtr]);
                                    string[] secEntries = cpAttrSections[secCtr].Split('\n');
                                    if (secEntries.Length == 1) {
                                        continue;
                                    }


                                    for (int entryCtr = 0; entryCtr < secEntries.Length; entryCtr++) {
                                        string tEntry = secEntries[entryCtr].Trim();
                                        if (tEntry.Length > 0) {
                                            if (tEntry.EndsWith("]")) {
                                                if (secCtr > MIN_SECTIONS) {
                                                    parsedIniFileData.Append("<BDNA,>");
                                                }
                                                parsedIniFileData.Append("Section=");
                                                parsedIniFileData.Append(tEntry.TrimEnd(']'));
                                            } else {
                                                if (secEntries[entryCtr - 1].Trim().EndsWith("]")) {
                                                    parsedIniFileData.Append("<BDNA,1>");
                                                } else {
                                                    parsedIniFileData.Append("<BDNA,2>");
                                                }
                                                parsedIniFileData.Append(tEntry);

                                            }
                                        }
                                    }

                                }
                            }

                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Parsing Succeeded. Parsed INI File Data is : " + parsedIniFileData.ToString(),
                                                  parsedIniFileData.ToString());

                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                dataRow.Append(elementId).Append(',')
                                       .Append(attributes[@"collectedCPAttr"])
                                       .Append(',')
                                       .Append(scriptParameters[@"CollectorId"]).Append(',')
                                       .Append(taskId).Append(',')
                                       .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                       .Append(',')
                                       .Append(@"collectedCPAttr")
                                       .Append(',')
                                       .Append(BdnaDelimiters.BEGIN_TAG)
                                       .Append(collectedCPAttr)
                                       .Append(BdnaDelimiters.END_TAG);

                                dataRow.Append(elementId).Append(',')
                                       .Append(attributes[@"parsedCPAttr"])
                                       .Append(',')
                                       .Append(scriptParameters[@"CollectorId"]).Append(',')
                                       .Append(taskId).Append(',')
                                       .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                       .Append(',')
                                       .Append(@"parsedCPAttr")
                                       .Append(',')
                                       .Append(BdnaDelimiters.BEGIN_TAG)
                                       .Append(parsedIniFileData)
                                       .Append(BdnaDelimiters.END_TAG);
                            }
                        }
                    } else {

                        using (IRemoteProcess rp =
                                RemoteProcess.GetRemoteFile(taskIdString,        // Task Id to log against.
                                                            cimvScope,   // WMI connection, passed to collection script from WinCs
                                                            remoteFileName,    // Name of remote file to retrieve.
                                                            connection, // credential hash passed to all collection/connection scripts
                                                            tftpPath,
                                                            tftpPath_login,
                                                            tftpPath_password,
                                                            tftpDispatcher)) {      // TFTP listener, passed to script from WinCs

                            //
                            // Launch the remote process.  This method will
                            // block until the entire remote process operation
                            // completes.
                            ResultCodes rc = rp.Launch();

                            //
                            // Once you get control back, there are properties that
                            // can be checked to information about the outcome of the
                            // operation, obtain log data, and retrieve collection results.
                            Lib.Logger.TraceEvent(TraceEventType.Information,
                                                  0,
                                                  "Remote process operation completed with result code" + rc.ToString(),
                                                  taskIdString);

                            if (null != collectedCPAttr) {
                                collectedCPAttr.Append(rp.Stdout);
                                int MIN_SECTIONS = 1;
                                // string collectedCPAttr = sb.ToString();
                                // System.Console.WriteLine(collectedCPAttr);

                                string[] cpAttrSections = new Regex(@"\[").Split(collectedCPAttr.ToString());
                                // System.Console.WriteLine("{0} sections in text:", cpAttrSections.Length);
                                if (cpAttrSections.Length > 0) {
                                    for (int secCtr = 0; secCtr < cpAttrSections.Length; secCtr++) {
                                        // System.Console.WriteLine("This Section");
                                        // System.Console.WriteLine(cpAttrSections[secCtr]);
                                        string[] secEntries = cpAttrSections[secCtr].Split('\n');
                                        if (secEntries.Length == 1) {
                                            continue;
                                        }


                                        for (int entryCtr = 0; entryCtr < secEntries.Length; entryCtr++) {
                                            string tEntry = secEntries[entryCtr].Trim();
                                            if (tEntry.Length > 0) {
                                                if (tEntry.EndsWith("]")) {
                                                    if (secCtr > MIN_SECTIONS) {
                                                        parsedIniFileData.Append("<BDNA,>");
                                                    }
                                                    parsedIniFileData.Append("Section=");
                                                    parsedIniFileData.Append(tEntry.TrimEnd(']'));
                                                } else {
                                                    if (secEntries[entryCtr - 1].Trim().EndsWith("]")) {
                                                        parsedIniFileData.Append("<BDNA,1>");
                                                    } else {
                                                        parsedIniFileData.Append("<BDNA,2>");
                                                    }
                                                    parsedIniFileData.Append(tEntry);

                                                }
                                            }
                                        }

                                    }
                                }

                                Lib.Logger.TraceEvent(TraceEventType.Information,
                                                      0,
                                                      "Parsing Succeeded. Parsed INI File Data is : " + parsedIniFileData.ToString(),
                                                      parsedIniFileData.ToString());

                                if (ResultCodes.RC_SUCCESS == resultCode) {
                                    dataRow.Append(elementId).Append(',')
                                           .Append(attributes[@"collectedCPAttr"])
                                           .Append(',')
                                           .Append(scriptParameters[@"CollectorId"]).Append(',')
                                           .Append(taskId).Append(',')
                                           .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                           .Append(',')
                                           .Append(@"collectedCPAttr")
                                           .Append(',')
                                           .Append(BdnaDelimiters.BEGIN_TAG)
                                           .Append(collectedCPAttr)
                                           .Append(BdnaDelimiters.END_TAG);

                                    dataRow.Append(elementId).Append(',')
                                           .Append(attributes[@"parsedCPAttr"])
                                           .Append(',')
                                           .Append(scriptParameters[@"CollectorId"]).Append(',')
                                           .Append(taskId).Append(',')
                                           .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                           .Append(',')
                                           .Append(@"parsedCPAttr")
                                           .Append(',')
                                           .Append(BdnaDelimiters.BEGIN_TAG)
                                           .Append(parsedIniFileData)
                                           .Append(BdnaDelimiters.END_TAG);
                                }

                            } else {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "CPA INI File not found on target machine.",
                                                      taskIdString);
                                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                            }


                        }
                    }
                } finally {
                    tl.Flush();
                    Lib.Logger.Listeners.Remove(tl);
                }

                logData = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }

            Lib.Logger.TraceEvent(TraceEventType.Information,
                                  0,
                                  "CPA INI File data returned: ",
                                  taskIdString);
            Lib.Logger.TraceEvent(TraceEventType.Information,
                                  0,
                                  collectedCPAttr.ToString(),
                                  taskIdString);
            Lib.Logger.TraceEvent(TraceEventType.Information,
                                  0,
                                  "Log data returned: ",
                                  taskIdString);
            Lib.Logger.TraceEvent(TraceEventType.Information,
                                  0,
                                  logData,
                                  taskIdString);

            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());

        }


    }

        #endregion ICollectionScriptRuntime

}

