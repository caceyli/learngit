#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/02/14
*
* Current Status
*       $Revision: 1.22 $
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
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script for Microsoft Outlook Express and Windows Mail level 2 information.
    /// "Outlook Express" has been renamed to "Windows Mail" in Windows Vista operating system.
    /// </summary>
    public class WinFileSearchScript : ICollectionScriptRuntime {

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
                ITftpDispatcher tftpDispatcher) {
            Stopwatch executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            //String discDrive = String.Empty, searchFilter = String.Empty, driveFilter = String.Empty;
            String driveName = String.Empty, mountPoint = String.Empty, filter= String.Empty;
            String isLocal = String.Empty, capacity = String.Empty;

            int splitQuery = 0;
            string startDateTime = DateTime.Now.ToShortDateString() + @" " + 
                                   DateTime.Now.ToShortTimeString();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WinFileSearchScript.",
                                  m_taskId);

            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (!scriptParameters.ContainsKey(@"filter")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"filter\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    filter = scriptParameters[@"filter"];
                }
                if (!scriptParameters.ContainsKey(@"driveName")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"driveName\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    driveName = scriptParameters[@"driveName"].Trim();
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Target drive name is {1}.",
                                          m_taskId,
                                          driveName);
                }
                if (!scriptParameters.ContainsKey(@"mountPoint")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"mountPoint\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    mountPoint = scriptParameters[@"mountPoint"].Trim();
                }
                if (!scriptParameters.ContainsKey(@"isLocal")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"isLocal\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    isLocal = scriptParameters[@"isLocal"].Trim();
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Target drive is local drive.",
                                          m_taskId);
                }
                if (!scriptParameters.ContainsKey(@"capacity")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Missing script parameter \"capacity\".",
                                          m_taskId);
                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                } else {
                    capacity = scriptParameters[@"capacity"].Trim();
                    Lib.Logger.TraceEvent(TraceEventType.Information,
                                          0,
                                          "Task Id {0}: Target search drive capacity: {1}.",
                                          m_taskId,
                                          capacity);
                }



                if (!scriptParameters.ContainsKey(@"splitQuery"))

                {

                    Lib.Logger.TraceEvent(TraceEventType.Error,

                                          0,

                                          "Task Id {0}: Missing script parameter \"splitQuery\".",

                                          m_taskId);

                    resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;

                }

                else

                {

                    splitQuery = int.Parse(scriptParameters[@"splitQuery"].Trim());

                    Lib.Logger.TraceEvent(TraceEventType.Information,

                                          0,

                                          "Task Id {0}: Whether to split query: {1}.",

                                          m_taskId,

                                          splitQuery);

                }

                if (!attributes.ContainsKey(@"fileSearchResult")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attribute \"fileSearchResult\" missing from attributeSet.",
                                          m_taskId);
                    resultCode = ResultCodes.RC_NULL_ATTRIBUTE_SET;
                }
                if (!attributes.ContainsKey(@"searchFilterString")) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attribute \"searchFilterString\" missing from attributeSet.",
                                          m_taskId);
                    resultCode = ResultCodes.RC_NULL_ATTRIBUTE_SET;
                }
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    if (null == connection) {
                        resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection object passed to WinFileSearchScript is null.",
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
                        } else {
                            IDictionary<string, string> queryResults = new Dictionary<string, string>();
                            using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null)) {
                                resultCode = SearchRemoteFiles(cimvScope, filter, mountPoint, splitQuery);
                            }
                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                dataRow.Append(BuildDataRow(taskId,
                                                            cleId,
                                                            elementId,
                                                            databaseTimestamp + executionTimer.ElapsedMilliseconds,
                                                            attributes,
                                                            scriptParameters,
                                                            @"startDateTime",
                                                            startDateTime));

                                dataRow.Append(BuildDataRow(taskId, 
                                                            cleId, 
                                                            elementId, 
                                                            databaseTimestamp + executionTimer.ElapsedMilliseconds,
                                                            attributes,
                                                            scriptParameters, 
                                                            @"fileSearchResult",
                                                            m_resultBuffer.ToString()));
                                dataRow.Append(BuildDataRow(taskId,
                                                            cleId,
                                                            elementId,
                                                            databaseTimestamp + executionTimer.ElapsedMilliseconds,
                                                            attributes,
                                                            scriptParameters,
                                                            @"searchFilterString",
                                                            m_searchFilterString));
                                dataRow.Append(BuildDataRow(taskId,
                                                            cleId,
                                                            elementId,
                                                            databaseTimestamp + executionTimer.ElapsedMilliseconds,
                                                            attributes,
                                                            scriptParameters,
                                                            @"endDateTime",
                                                            DateTime.Now.ToShortDateString() + @" " + 
                                                            DateTime.Now.ToShortTimeString()));
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in WinFileSearchScript",
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
                                  "Task Id {0}: Collection script WinFileSearchScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
        #endregion

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private string BuildDataRow(long taskId,
                                    long cleId,
                                    long elementId,
                                    long databaseTimestamp,
                                    IDictionary<string, string> attributes,
                                    IDictionary<string, string> scriptParameters,
                                    string attributeName, 
                                    string collectedData) {

            StringBuilder builder = new StringBuilder();
            if (!attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      taskId,
                                      attributeName);
            } else if (string.IsNullOrEmpty(collectedData)) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      taskId);
            } else {
                builder.Append(elementId).Append(',')
                       .Append(attributes[attributeName]).Append(',')
                       .Append(scriptParameters[@"CollectorId"]).Append(',')
                       .Append(taskId).Append(',')
                       .Append(databaseTimestamp).Append(',')
                       .Append(attributeName).Append(',')
                       .Append(BdnaDelimiters.BEGIN_TAG)
                       .Append(collectedData)
                       .Append(BdnaDelimiters.END_TAG);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Get install location information from the remote registry.
        /// </summary>
        /// <param name="wmiRegistry">Remote registry connection.</param>
        /// <param name="fileFilter">File Filter</param>
        /// <param name="discDrive">Disc Drives</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes SearchRemoteFiles(ManagementScope scope, String fileFilter, String drive, int splitQuery) {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            bool isExecutionComplete = false;
            m_resultBuffer = new StringBuilder();

             /// <summary>Temp File</summary>
            //string tempFileName = Path.GetTempFileName();
            //Lib.Logger.TraceEvent(TraceEventType.Warning,
            //                      0,
            //                      "Task Id {0}: Temporary file created is <{1}>.",
            //                      m_taskId,
            //                      tempFileName);
            
            //FileStream tempFile = new FileStream(tempFileName, FileMode.Create);
            //TextWriter logFile = new StreamWriter(tempFile);

            try {
                //for (int retryCount = 0; (retryCount < s_maximumExecutionCount) && (isExecutionComplete == false); retryCount++) {
                //    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                //                          0,
                //                          "Task Id {0}: Executing file search iteration: <{1}>",
                //                          m_taskId,
                //                          retryCount);
                    try {
                        ManagementObjectCollection moc = null;
                        string queryText = @"Select * from CIM_Datafile Where";
                        //queryText += @" (Path = '\\download\\') and";
                        queryText += @" (Drive='" + drive + @"')";

                        IDictionary<string, Regex> regexList = new Dictionary<string, Regex>();
                        string[] filters = fileFilter.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string filter in filters) {
                            if (m_searchFilterString.Length > 0) {
                                m_searchFilterString += @", ";
                            }
                            m_searchFilterString +=  filter;
                            string filterString = filter.Replace(@"*", @"\S+?");
                            filterString = filterString.Replace(@".", @"\.");
                            filterString = (@"^" + filterString + @"$");
                            Regex regex = new Regex(filterString);
                            regexList.Add(filter, (new Regex(filterString, RegexOptions.Compiled)));
                        }



                        IList<string> sizeFilters = new List<string>();

                        //sizeFilters.Add(" And FileSize < 16");

                        //for (long i = 16; i < 4294967296; i *= 16)

                        //{

                        //    sizeFilters.Add(" And FileSize >= " + i + " And FileSize < " + (i * 16));

                        //}

                        //sizeFilters.Add(" And FileSize >= 4294967296");



                        if (splitQuery > 0)

                        {

                            sizeFilters.Add(" and FileSize < 2048");

                            sizeFilters.Add(" and FileSize >= 2048 and FileSize < 16384");

                            sizeFilters.Add(" and FileSize >= 16384");

                        }

                        else

                        {

                            sizeFilters.Add("");

                        }

                        foreach (string sizeFilter in sizeFilters)

                        {

                            DateTime startTime = DateTime.Now;

                            SelectQuery sq = new SelectQuery(queryText + sizeFilter);

                            EnumerationOptions option = new EnumerationOptions();

                            option.Timeout = new TimeSpan(48, 0, 0);

                            ManagementObjectSearcher mos = new ManagementObjectSearcher(scope, sq, option);

                            moc = mos.Get();



                            int fileCount = 0;



                            foreach (ManagementObject mo in moc)

                            {

                                string name = mo.GetPropertyValue(@"FileName").ToString();

                                name += @"." + mo.GetPropertyValue(@"Extension");



                                //logFile.WriteLine(@"Determing file name : <" + name + @">");

                                //logFile.Flush();

                                ++fileCount;



                                bool matched = false;

                                string filter = string.Empty;

                                foreach (KeyValuePair<string, Regex> kvp in regexList)

                                {

                                    if (!string.IsNullOrEmpty(kvp.Key) && (kvp.Value != null))

                                    {

                                        if (kvp.Value.IsMatch(name))

                                        {

                                            matched = true;

                                            if (!string.IsNullOrEmpty(filter))

                                            {

                                                filter += @", ";

                                            }

                                            filter += kvp.Key;

                                        }

                                    }

                                }



                                if (matched)

                                {

                                    if (m_resultBuffer.Length > 0)

                                    {

                                        m_resultBuffer.Append(BdnaDelimiters.DELIMITER1_TAG);

                                    }

                                    m_resultBuffer.Append(mo.GetPropertyValue(@"Drive"))

                                                  .Append(BdnaDelimiters.DELIMITER2_TAG)

                                                  .Append(mo.GetPropertyValue(@"Path"))

                                                  .Append(BdnaDelimiters.DELIMITER2_TAG)

                                                  .Append(mo.GetPropertyValue(@"FileName"))

                                                  .Append(@".").Append(mo.GetPropertyValue(@"Extension"))

                                                  .Append(BdnaDelimiters.DELIMITER2_TAG)

                                                  .Append(mo.GetPropertyValue(@"Extension"))

                                                  .Append(BdnaDelimiters.DELIMITER2_TAG)

                                                  .Append(filter);

                                }

                                //logFile.Write(@"File name match search criteria: <");

                                //logFile.Flush();

                                //logFile.Write(mo.GetPropertyValue(@"Drive"));

                                //logFile.Write(mo.GetPropertyValue(@"Path"));

                                //logFile.Write(mo.GetPropertyValue(@"FileName"));

                                //logFile.WriteLine(@">");

                                //logFile.Flush();

                            }

                            TimeSpan duration = DateTime.Now - startTime;

                        }

                        resultCode = ResultCodes.RC_SUCCESS;
                        //isExecutionComplete = true;
                    } catch (Exception ex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Exception thrown during file search. Error Message <{1}>. Stack Trace {2}.",
                                              m_taskId,
                                              ex.Message,
                                              ex.StackTrace);
                        isExecutionComplete = true;
                        if (ex.Message.ToString().Trim() == @"Call cancelled") {
                        //    isExecutionComplete = false;
                        //    logFile.WriteLine(@"Call Cancelled is registered.");
                        //    logFile.Flush();
                            resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        }
                    }
                //}
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected Exception thrown during file search. Error Message <{1}>. Stack Trace {2}.",
                                      m_taskId,
                                      ex.Message,
                                      ex.StackTrace);
            } finally {
                //if (logFile != null) {
                //    logFile.Flush();
                //    logFile.Close();
                //}
            }
            return resultCode;
        }

        /// <summary>Retry execution Count</summary>
        private bool m_isExecutionComplete = false;

        /// <summary>Execution Count</summary>
        private static int s_maximumExecutionCount = 50;

        /// <summary>Filter Search String</summary>
        private string m_searchFilterString = String.Empty;

        /// <summary>Outlook Express installtion directory.</summary>
        private StringBuilder m_resultBuffer = new StringBuilder();

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;
    }
}
