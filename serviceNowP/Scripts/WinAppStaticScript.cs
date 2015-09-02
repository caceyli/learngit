#region Copyright
/******************************************************************
*
*          Module: Windows Applications Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2009/09/25
*
* Current Status
*       $Revision: 1.10 $
*           $Date: 2014/08/27 03:51:07 $
*         $Author: vivi_liu $
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

namespace bdna.Scripts {

    /// <summary>
    /// Broad spectrum collection script for grabbing the bulk of
    /// level two data for Windows.
    /// </summary>
    public class WinAppStaticScript : ICollectionScriptRuntime {

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
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            bool cleanupProfile = false;
            string profileDirectory = null;
            ManagementScope cimvScope = null;
            ManagementScope defaultScope = null;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WinAppStaticScript.",
                                  taskIdString);

            try {
                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WinAppStaticScript is null.",
                                          taskIdString);

                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          taskIdString);
                } else if (!connection.ContainsKey(@"default")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          taskIdString);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    defaultScope = connection[@"default"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              taskIdString);
                    } else if (!defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed",
                                              taskIdString);
                    } else {
                        if (Lib.EnableProfileCleanup) {
                            string userName = connection[@"userName"] as string;
                            Debug.Assert(null != userName);
                            int domainSeparatorPosition = userName.IndexOf('\\');
                            if (-1 != domainSeparatorPosition) {
                                userName = userName.Substring(domainSeparatorPosition + 1);
                            }
                            profileDirectory = @"c:\Documents and Settings\" + userName;
                            cleanupProfile = !Lib.ValidateDirectory(taskIdString, profileDirectory, cimvScope);
                        }
                        IDictionary<string, string> queryResults = new Dictionary<string, string>();
                        //
                        // Loop through the WMI query table and perform
                        // each query.
                        foreach (CimvQueryTableEntry cqte in s_cimvQueryTable) {
                            //
                            // we currently ignore the result code from the wmi
                            // queries and simply return whatever data we managed
                            // to get.
                            /*resultCode = */
                            cqte.ExecuteQuery(taskIdString, cimvScope, queryResults);
                        }

                        String installedSoftwareDetails = String.Empty, installedHotfixes = String.Empty;
                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null)) {
                            Debug.Assert(null != wmiRegistry);
                            //
                            // Get Installed Software.
                            resultCode = GetInstalledSoftwareHotfix(taskIdString, 
                                                                    cimvScope, 
                                                                    wmiRegistry, 
                                                                    queryResults, 
                                                                    out installedSoftwareDetails,
                                                                    out installedHotfixes);
                        }
                        if (String.IsNullOrEmpty(installedHotfixes)) {
                            installedHotfixes = @"No Hotfix found.";
                        } 

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            //
                            // InstalledSoftware Details
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"installedSoftwareDetails"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"installedSoftwareDetails")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG)
                                   .Append(installedSoftwareDetails)
                                   .Append(BdnaDelimiters.END_TAG);
                            //
                            // Hotfix Details
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"patches"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"patches")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG)
                                   .Append(installedHotfixes)
                                   .Append(BdnaDelimiters.END_TAG);
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.LogException(taskIdString,
                                 executionTimer,
                                 "Unhandled exception in WinAppStaticScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            if (cleanupProfile && null != profileDirectory && null != cimvScope) {
                uint wmiMethodResultCode = 0;
                Lib.DeleteDirectory(taskIdString, profileDirectory, cimvScope, out wmiMethodResultCode);
            }


            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WinAppStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());

            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString().Replace("\n", "").Replace("\r", ""));
        }
        #endregion ICollectionScriptRuntime


        /// <summary>
        /// Registry query to get Windows installed software information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetInstalledSoftwareHotfix(string taskId,
                                                              ManagementScope scope,
                                                              ManagementClass wmiRegistry,
                                                              IDictionary<string, string> queryResults,
                                                              out String installedApps,
                                                              out String installedHotfixes) {

            Stopwatch sw = Stopwatch.StartNew();
            StringBuilder sb = new StringBuilder(), hotfixBuilder = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            installedApps = string.Empty; 
            installedHotfixes = String.Empty;

            try {
                IDictionary<string, StringBuilder> buf = new Dictionary<string, StringBuilder>();
                IDictionary<string, string> hotFixes = new Dictionary<string, string>();

                resultCode = GetUninstallRegistry(taskId,
                                                  wmiRegistry,
                                                  s_registryKeyUninstall,
                                                  buf,
                                                  hotFixes);
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    resultCode = GetUninstallRegistry(taskId,
                                                      wmiRegistry,
                                                      s_registryKeyUninstall64,
                                                      buf,
                                                      hotFixes);
                }

                if (queryResults.ContainsKey(@"operatingSystem.idString")) {
                    //if (!s_2000Regex.IsMatch(queryResults[@"operatingSystem.idString"])) {
                    if (s_vistaRegex.IsMatch(queryResults[@"operatingSystem.idString"]) ||
                        s_2008Regex.IsMatch(queryResults[@"operatingSystem.idString"])  ||
                        s_2012Regex.IsMatch(queryResults[@"operatingSystem.idString"])  ||
                        s_7Regex.IsMatch(queryResults[@"operatingSystem.idString"])     ||
                        s_8Regex.IsMatch(queryResults[@"operatingSystem.idString"])) {
                        s_hotFixQuery.ExecuteQuery(taskId, scope, hotFixes);
                    }
                }
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    foreach (KeyValuePair<string, StringBuilder> kvp in buf) {
                        if (kvp.Value != null) {
                            sb.Append(kvp.Value);
                        }
                    }

                    foreach (KeyValuePair<string, string> kvp in hotFixes) {
                        // For 5.0, get hotfix ID only.
                        if (hotfixBuilder.Length > 0) {
                            hotfixBuilder.Append(BdnaDelimiters.DELIMITER2_TAG);
                        }
                        hotfixBuilder.Append(kvp.Value);
                        //if (!string.IsNullOrEmpty(kvp.Value)) {
                        //    if (hotfixBuilder.Length > 0) {
                        //        hotfixBuilder.Append(BdnaDelimiters.DELIMITER1_TAG);
                        //    }
                        //    hotfixBuilder.Append(kvp.Value);
                        //}
                    }
                    if (hotfixBuilder.Length > 0) {
                        installedHotfixes = hotfixBuilder.ToString();
                    } else {
                        installedHotfixes = @"No Hotfix found.";
                    }
                }
            } catch (ManagementException me) {
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetInstalledSoftwareHotfix failed",
                                           me);
            } catch (Exception ex) {
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetInstalledSoftwareHotfix failed",
                                 ex);
            }
            if (sb.Length != 0) {
                installedApps = sb.ToString(8, sb.Length - 8);
            }
            //result = sb.ToString();
            return resultCode;
        }

        /// <summary>
        /// This is the signature for all WMI query result handlers.
        /// </summary>
        private delegate void CimvQueryResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults);
        /// <summary>
        /// This is the signature for all of the registry query
        /// methods.
        /// </summary>
        private delegate ResultCodes RegistryQuery(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> processedResults);

        /// <summary>
        /// This class is the table entry for the CIMV query table.
        /// It essentially binds a class to query with the properties
        /// of interest and a handler to process the query results.
        /// </summary>
        private class CimvQueryTableEntry {
            /// <summary>
            /// Construct a table entry with default query
            /// enumeration options.
            /// </summary>
            /// 
            /// <param name="className">WMI class name to query.</param>
            /// <param name="classProperties">String array of WMI properties to query the WMI class for.</param>
            /// <param name="dataRowItemNames">List of matching data row item names.  These are the names
            ///     of the name/value pairs in the resulting data row.  The
            ///     order of these names MUST match the order of property
            ///     names in classProperties.</param>
            /// <param name="resultHandler">Delegate to process the query results.</param>
            /// <param name="enumerationOptions">Query enumeration options.</param>
            public CimvQueryTableEntry(
                    string className,
                    string[] classProperties,
                    string[] dataRowItemNames,
                    CimvQueryResultHandler resultHandler)
                : this(className,
                           classProperties,
                           dataRowItemNames,
                           resultHandler,
                           new EnumerationOptions(null, Lib.WmiMethodTimeout, Lib.WmiBlockSize, true, true, false, false, false, false, false)) {
            }

            /// <summary>
            /// Full constructor with all values specified.
            /// </summary>
            /// 
            /// <param name="className">WMI class name to query.</param>
            /// <param name="classProperties">String array of WMI properties to query the WMI class for.</param>
            /// <param name="dataRowItemNames">List of matching data row item names.  These are the names
            ///     of the name/value pairs in the resulting data row.  The
            ///     order of these names MUST match the order of property
            ///     names in classProperties.</param>
            /// <param name="resultHandler">Delegate to process the query results.</param>
            /// <param name="enumerationOptions">Query enumeration options.</param>
            public CimvQueryTableEntry(
                    string className,
                    string[] classProperties,
                    string[] dataRowItemNames,

                    CimvQueryResultHandler resultHandler,
                    EnumerationOptions enumerationOptions) {

                m_className = className;
                m_resultHandler = resultHandler;
                m_enumerationOptions = enumerationOptions;
                int mapSize = (null == classProperties) ? 0 : classProperties.Length;
                if (0 == mapSize) {
                    m_propertyMap = new NameValueCollection(1);
                    m_propertyMap[@"*"] = null;
                } else {
                    m_propertyMap = new NameValueCollection(mapSize);
                    for (int i = 0;
                         classProperties.Length > i;
                         ++i) {
                        m_propertyMap[classProperties[i]] = (null == dataRowItemNames || dataRowItemNames.Length <= i)
                            ? null
                            : dataRowItemNames[i];
                    }
                }
            }

            /// <summary>
            /// Execute the WMI query for this table entry.
            /// </summary>
            /// 
            /// <param name="taskId">Database assigned task Id.</param>
            /// <param name="scope">WMI connection to use.</param>
            /// <param name="results">Target collection to populate with results.</param>
            /// 
            /// <returns>Operation result code.</returns>
            public ResultCodes ExecuteQuery(
                    string taskId,
                    ManagementScope scope,
                    IDictionary<string, string> results) {

                ResultCodes resultCode = ResultCodes.RC_SUCCESS;
                ManagementObjectCollection moc = null;
                ManagementObjectSearcher mos = null;

                mos = new ManagementObjectSearcher(scope, new SelectQuery(m_className,
                                                                          null,
                                                                          m_propertyMap.AllKeys));
                using (mos) {
                    resultCode = Lib.ExecuteWqlQuery(taskId, mos, m_enumerationOptions, out moc);
                }

                //
                // Retry on any failure except query timeout.
                if (ResultCodes.RC_SUCCESS != resultCode && ResultCodes.RC_WMI_QUERY_TIMEOUT != resultCode) {
                    string originalQuery = mos.Query.QueryString;
                    mos = new ManagementObjectSearcher(scope,
                                                       new SelectQuery(m_className));

                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Retrying failed query {1} as\n{2}",
                                          taskId,
                                          originalQuery,
                                          mos.Query.QueryString);

                    using (mos) {
                        resultCode = Lib.ExecuteWqlQuery(taskId, mos, m_enumerationOptions, out moc);
                    }
                }

                if (null != moc) {
                    using (moc) {
                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            m_resultHandler(moc, m_propertyMap, results);
                        }
                    }
                }

                // @todo for now, always return success.  We've
                // observed cases where an individual query will
                // fail, but all the others will work.  Gather
                // and return as much data as possible.  We
                // should create a warning result code.
                return ResultCodes.RC_SUCCESS;
            }

            /// <summary>WMI class name to query.</summary>
            private string m_className;

            /// <summary>
            /// Delegate to call to process the records returned by our
            /// WQL query.
            /// </summary>
            private CimvQueryResultHandler m_resultHandler;

            /// <summary>
            /// Maps the properties used WQL query to the item names used
            /// in generating the data row.  This collection caches the
            /// array of property names, which is perfect for our needs.
            /// </summary>
            private NameValueCollection m_propertyMap;

            private EnumerationOptions m_enumerationOptions;
        }

        /// <summary>Simple handler to match up query results to data row entries.</summary>
        private static CimvQueryResultHandler s_defaultResultHandler = new CimvQueryResultHandler(DefaultResultHandler);

        /// <summary>Handler for the Win32_OperatingSystem query.</summary>
        private static CimvQueryResultHandler s_osInformationResultHandler = new CimvQueryResultHandler(OsInformationResultHandler);

        /// <summary>Handler for the Windows Hotfix query.</summary>
        private static CimvQueryResultHandler s_hotfixResultHandler = new CimvQueryResultHandler(HotfixResultHandler);

        /// <summary>
        /// WMI query table.  This table contains an entry for 
        /// each WMI query we want to make.  Each entry specifies
        /// which WMI class to query and what properties from the
        /// class we are interested in.  The properties we're
        /// interested in are bound to the associated data row
        /// item names and a specific handler to process the query
        /// results into a data row entry.
        /// </summary>
        private static CimvQueryTableEntry[] s_cimvQueryTable = {
            new CimvQueryTableEntry(@"Win32_OperatingSystem",
                                    new string[] {@"Version",
                                                  @"CSDVersion",
                                                  @"Caption",
                                                  @"BuildNumber"},
                                    new string[] {@"operatingSystem.version",
                                                  @"operatingSystem.patchLevel"},
                                    s_osInformationResultHandler)
        };

        private static CimvQueryTableEntry s_hotFixQuery = new CimvQueryTableEntry(@"Win32_QuickFixEngineering",
                                                                                   new string[] {@"HotFixID",
                                                                                                 @"Description",
                                                                                                 @"InstalledOn"},
                                                                                   null,
                                                                                   s_hotfixResultHandler);

        /// <summary>
        /// Get uninstall registry from given registry path.
        /// </summary>
        /// <param name="taskId">taskId</param>
        /// <param name="wmiRegistry">WMI Registry</param>
        /// <param name="uninstallRegistryPath">Uninstall registry path.</param>
        /// <param name="registryBuffer">Buffer</param>
        /// <returns></returns>
        private static ResultCodes GetUninstallRegistry(
                string taskId,
                ManagementClass wmiRegistry,
                string uninstallRegistryPath,
                IDictionary<string, StringBuilder> registryBuffer,
                IDictionary<string, string> hotFixBuffer) {

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
            inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
            inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, uninstallRegistryPath);

            ManagementBaseObject outputParameters = null;
            resultCode = Lib.InvokeRegistryMethod(taskId,
                                                  wmiRegistry,
                                                  RegistryMethodNames.ENUM_KEY,
                                                  s_registryKeyUninstall,
                                                  inputParameters,
                                                  out outputParameters);

            if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                string[] subKeys = null;
                using (outputParameters) {
                    subKeys = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                }

                if (null != subKeys && 0 < subKeys.Length) {
                    foreach (string subKey in subKeys) {
                        StringBuilder sb = new StringBuilder();
                        string displayNameString = null, subKeyString = subKey, installDate = null;
                        string subkeyPath = uninstallRegistryPath + @"\" + subKey;
                        sb.Append(BdnaDelimiters.DELIMITER1_TAG)
                          .Append(@"SubKeyLabel")
                          .Append(BdnaDelimiters.DELIMITER2_TAG);
                        sb.Append(subKey);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);

                        outputParameters = null;
                        resultCode = Lib.InvokeRegistryMethod(taskId,
                                                              wmiRegistry,
                                                              RegistryMethodNames.ENUM_VALUES,
                                                              subkeyPath,
                                                              inputParameters,
                                                              out outputParameters);

                        if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                            string[] valueNames = null;
                            uint[] valueTypes = null;

                            using (outputParameters) {
                                valueNames = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                                valueTypes = outputParameters.GetPropertyValue(RegistryPropertyNames.TYPES) as uint[];
                            }

                            if (null != valueNames && 0 < valueNames.Length && null != valueTypes && 0 < valueTypes.Length) {
                                Debug.Assert(valueNames.Length == valueTypes.Length);

                                for (int i = 0; valueNames.Length > i; ++i) {
                                    string valueNamesRegistryMethod;
                                    switch ((RegistryTypes)valueTypes[i]) {
                                        case RegistryTypes.REG_SZ:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_STRING_VALUE;
                                            break;
                                        case RegistryTypes.REG_EXPAND_SZ:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_EXPANDED_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_EXPANDED_STRING_VALUE;
                                            break;
                                        case RegistryTypes.REG_BINARY:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_BINARY_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_BINARY_VALUE;
                                            break;
                                        case RegistryTypes.REG_DWORD:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_DWORD_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_DWORD_VALUE;
                                            break;
                                        case RegistryTypes.REG_MULTI_SZ:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_MULTI_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_MULTI_STRING_VALUE;
                                            break;
                                        default:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_STRING_VALUE;
                                            break;
                                    }
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                    outputParameters = null;
                                    resultCode = Lib.InvokeRegistryMethod(taskId,
                                                                          wmiRegistry,
                                                                          valueNamesRegistryMethod,
                                                                          subkeyPath,
                                                                          inputParameters,
                                                                          out outputParameters);

                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        string installedItemValue = String.Empty;
                                        using (outputParameters) {
                                            switch ((RegistryTypes)valueTypes[i]) {
                                                case RegistryTypes.REG_SZ:
                                                case RegistryTypes.REG_EXPAND_SZ:
                                                case RegistryTypes.REG_MULTI_SZ:
                                                    installedItemValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                                    break;
                                                case RegistryTypes.REG_BINARY:
                                                case RegistryTypes.REG_DWORD:
                                                    object dwBinValue = outputParameters.GetPropertyValue(RegistryPropertyNames.U_VALUE);
                                                    if (null != dwBinValue) {
                                                        installedItemValue = dwBinValue.ToString();
                                                    }
                                                    break;
                                                default:
                                                    installedItemValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                                    break;
                                            }
                                        }

                                        if (!String.IsNullOrEmpty(installedItemValue)) {
                                            sb.Append(BdnaDelimiters.DELIMITER2_TAG)
                                              .Append(valueNames[i])
                                              .Append(BdnaDelimiters.DELIMITER2_TAG)
                                              .Append(installedItemValue);
                                            if (valueNames[i] == @"DisplayName") {
                                                displayNameString = installedItemValue;
                                            } else if (valueNames[i] == @"InstallDate") {
                                                installDate = installedItemValue;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        // Merge result from 64 bits and 32 bits.
                        if (registryBuffer.ContainsKey(subKey)) {
                            if (registryBuffer[subKey].Length < sb.Length) {
                                registryBuffer[subKey] = sb;
                            }
                        } else {
                            registryBuffer.Add(subKey, sb);
                        }

                        // Get Hotfix 
                        string hotFixId = null;
                        if (string.IsNullOrEmpty(displayNameString)) {
                            displayNameString = subKeyString;
                        }
                        if (!string.IsNullOrEmpty(displayNameString)) {
                            if (s_hotFixRegex.IsMatch(displayNameString)) {
                                MatchCollection mc = s_hotFixRegex.Matches(displayNameString);
                                if (0 < mc.Count) {
                                    hotFixId = mc[0].Groups[1].Value;
                                    StringBuilder tempSB = new StringBuilder();
                                    tempSB.Append(@"Desc=""").Append(displayNameString).Append('"')
                                          .Append(BdnaDelimiters.DELIMITER1_TAG);
                                    tempSB.Append(@"HotFixID=""").Append(hotFixId).Append('"');
                                    if (!string.IsNullOrEmpty(installDate)) {
                                        tempSB.Append(BdnaDelimiters.DELIMITER1_TAG)
                                              .Append(@"InstallDate=""").Append(installDate).Append('"');
                                    }

                                    if (!hotFixBuffer.ContainsKey(hotFixId) && tempSB.Length > 0) {
                                        hotFixBuffer.Add(hotFixId, tempSB.ToString());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return resultCode;
        }
        /// <summary>
        /// Handler for the Win32_OperatingSystem query.  Uses query
        /// results to compute OS related data row items.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void OsInformationResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            StringBuilder sb = new StringBuilder();
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults) {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    addSequenceNumber,
                                    count);
                ++count;
                sb.Length = 0;
                processedResults[@"operatingSystem.idString"] = sb.Append(mo.Properties[@"Caption"].Value)
                                                                  .Append(@"(")
                                                                  .Append(mo.Properties[@"BuildNumber"].Value)
                                                                  .Append(@")")
                                                                  .ToString();

            }
        }
        /// <summary>
        /// Helper method to build up list name value pairs by
        /// merging value collected via WMI with names expected in
        /// the resulting data row.
        /// </summary>
        /// 
        /// <param name="propertyDataCollection">Collection of properties from a ManagementObject returned
        ///     by a WMI query.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names. Essentially
        ///     the key value from this map is a property name in the 
        ///     propertyDataCollection and the value in this map is
        ///     name required in the resulting data row.</param>
        /// <param name="processedResults">Collection of resulting name value pairs.</param>
        /// <param name="addSequenceNumber">Set to true to add a sequence number "(9)" to the end
        ///     of each data row item name.</param>
        private static void ProcessPropertyData(
                PropertyDataCollection propertyDataCollection,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults,
                bool addSequenceNumber,
                int sequenceNumber) {

            //
            // Step through each property returned by WMI.
            foreach (PropertyData pd in propertyDataCollection) {
                //
                // Find the data row name (our name) for this
                // property by using the map of WMI names to 
                // data row names.
                string dataRowItemName = propertyMap[pd.Name];
                if (null != dataRowItemName && null != pd.Value) {
                    string processedResultKey = (addSequenceNumber)
                        ? dataRowItemName + sequenceNumber
                        : dataRowItemName;
                    string processedResultValue = null;
                    if (pd.IsArray && CimType.String == pd.Type) {
                        string[] strArray = new string[((IList<object>)pd.Value).Count];
                        for (int i = 0; i < ((IList<object>)pd.Value).Count; ++i) {
                            strArray[i] = ((IList<object>)pd.Value)[i].ToString();
                        }

                        processedResultValue = String.Join(",", strArray);
                    } else {
                        processedResultValue = pd.Value.ToString();
                    }
                    processedResults[processedResultKey] = processedResultValue;
                }
            }
        }

        private static void HotfixResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults) {
                //
                // Step through each property returned by WMI.
                StringBuilder sb = new StringBuilder();
                string hotfixID = null;
                foreach (PropertyData pd in mo.Properties) {
                    string propertyName = pd.Name;
                    if (null != propertyName && null != pd.Value) {
                        string propertyValue = pd.Value.ToString();
                        if (pd.IsArray && CimType.String == pd.Type) {
                            propertyValue = String.Join(",", pd.Value as string[]);
                        } else if (propertyName == @"HotFixID") {
                            hotfixID = propertyValue;
                        } else if (propertyName == @"InstalledOn") {
                            try {
                                propertyName = @"InstallDate";
                                Int64 iValue = Int64.Parse(pd.Value.ToString(),
                                                           System.Globalization.NumberStyles.AllowHexSpecifier);
                                DateTime dValue = DateTime.FromFileTimeUtc(iValue);
                                propertyValue = dValue.ToShortDateString();
                            } catch (Exception ex) {
                                Lib.Logger.TraceEvent(TraceEventType.Warning,
                                                      0,
                                                      "Collection script WindowsStaticScript. Hotfix ID {0} dateTime value {1} not valid. {2}",
                                                      hotfixID,
                                                      pd.Value.ToString(),
                                                      ex.Message);
                            }
                        } else if (propertyName == @"Description") {
                            propertyName = @"Desc";
                            if (string.IsNullOrEmpty(propertyValue)) {
                                propertyValue = @"Update";
                            }
                        }

                        if (!string.IsNullOrEmpty(propertyValue)) {
                            if (sb.Length > 0) {
                                sb.Append(BdnaDelimiters.DELIMITER1_TAG);
                            }
                            sb.Append(propertyName + @"=""" + propertyValue + @"""");
                        }
                    }
                }
                if (!string.IsNullOrEmpty(hotfixID)) {
                    if (!processedResults.ContainsKey(hotfixID)) {
                        processedResults[hotfixID] = hotfixID;
                    }
                }

                if (!string.IsNullOrEmpty(hotfixID) && sb.Length > 0) {
                    if (!processedResults.ContainsKey(hotfixID)) {
                        processedResults[hotfixID] = sb.ToString();
                    } else {
                        // replace hotfix data with one with more information.
                        if (processedResults[hotfixID].ToString().Length < sb.ToString().Length) {
                            processedResults[hotfixID] = sb.ToString();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The default handle for WMI query results just generates
        /// the name value pairs for the data row with no additional
        /// manipulation.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void DefaultResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults) {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    addSequenceNumber,
                                    count);
                count++;
            }
        }

        /// <summary>
        /// List of registry value names we're interested in for
        /// installed software.
        /// </summary>
        private static readonly string[] s_installedSoftwareDataRowItemNames = {@"DisplayName",
                                                                                @"DisplayVersion",
                                                                                @"InstallDate",
                                                                                @"InstallLocation",
                                                                                @"Publisher"};

        /// <summary>Registry path for installed software information.</summary>
        private static readonly string s_registryKeyUninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private static readonly string s_registryKeyUninstall64 = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
        private static readonly Regex s_hotFixRegex = new Regex(@".*(Q\d+|KB\d+)", RegexOptions.Compiled);
        private static readonly Regex s_dateTimeRegex = new Regex(@"(\d\d\d\d)(\d\d)(\d\d)(\d\d)(\d\d).*");
        private static readonly Regex s_2000Regex = new Regex(@"(2000)", RegexOptions.Compiled);
        private static readonly Regex s_vistaRegex = new Regex(@"([v|V]ista)", RegexOptions.Compiled);
        private static readonly Regex s_2008Regex = new Regex(@"(2008)", RegexOptions.Compiled);
        private static readonly Regex s_2012Regex = new Regex(@"(2012)", RegexOptions.Compiled);
        private static readonly Regex s_7Regex = new Regex(@"([wW][iI][nN][dD][oO][wW][sS]\s*7)", RegexOptions.Compiled);
        private static readonly Regex s_8Regex = new Regex(@"([wW][iI][nN][dD][oO][wW][sS]\s*8)", RegexOptions.Compiled);
    }
}
