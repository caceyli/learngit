#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.9 $
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

#endregion Copyright


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {
    /// <summary>
    /// Collection script for Windows ODBC Drivers.
    /// </summary>
    public class ODBCDriversCollectionScript  : ICollectionScriptRuntime {
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
                                  "Task Id {0}: Collection script ODBCDriversCollectionScript.",
                                  m_taskId);

            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to ODBCDriversCollectionScript is null.",
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
                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, 
                                                                                 new ManagementPath(@"StdRegProv"), 
                                                                                 null)) {
                            Debug.Assert(null != wmiRegistry);
                            IDictionary<string, IDictionary<string, string>> odbcDrivers = 
                                new Dictionary<string, IDictionary<string, string>>();

                            IDictionary<string, IDictionary<string, string>> systemDSNs = 
                                new Dictionary<string, IDictionary<string, string>>();

                            // Retrieve all ODBC Driver and its version
                            GetODBCDrivers(m_taskId, cimvScope, wmiRegistry, odbcDrivers);
                            // Retrieve all System DSN Information
                            GetSystemDSN(m_taskId, wmiRegistry, odbcDrivers, systemDSNs);
                            // Package all collected results into BDNA UDT Format
                            string odbcDriversDetails = PackageOutputData(odbcDrivers);
                            if (!string.IsNullOrEmpty(odbcDriversDetails)) {
                                dataRow.Append(elementId)
                                       .Append(',')
                                       .Append(attributes[@"odbcDriversDetails"])
                                       .Append(',')
                                       .Append(scriptParameters[@"CollectorId"])
                                       .Append(',')
                                       .Append(taskId)
                                       .Append(',')
                                       .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                       .Append(',')
                                       .Append(@"odbcDriversDetails")
                                       .Append(',')
                                       .Append(BdnaDelimiters.BEGIN_TAG)
                                       .Append(odbcDriversDetails)
                                       .Append(BdnaDelimiters.END_TAG);
                            }
                            string systemDSNDetails = PackageOutputData(systemDSNs);
                            if (!string.IsNullOrEmpty(systemDSNDetails)) {

                                dataRow.Append(elementId)
                                       .Append(',')
                                       .Append(attributes[@"systemDSNDetails"])
                                       .Append(',')
                                       .Append(scriptParameters[@"CollectorId"])
                                       .Append(',')
                                       .Append(taskId)
                                       .Append(',')
                                       .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                       .Append(',')
                                       .Append(@"systemDSNDetails")
                                       .Append(',')
                                       .Append(BdnaDelimiters.BEGIN_TAG)
                                       .Append(systemDSNDetails)
                                       .Append(BdnaDelimiters.END_TAG);
                            }
                        }
                    }
                }
            } catch (ManagementException me) {
                if (ManagementStatus.AccessDenied == me.ErrorCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0, 
                                          "Task Id {0}: Not enough privilege to read ODBC registry. {1}",
                                          m_taskId,
                                          me.ErrorCode.ToString());
                    resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;
                } else {
                    Lib.LogManagementException(m_taskId,
                                               executionTimer,
                                               "Cannot read remote ODBC registry.",
                                               me);
                    resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
                }

            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in ODBCDriverCollectionScript",
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
                                  "Task Id {0}: Collection script ODBCDriverCollectionScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        #endregion ICollectionScriptRuntime Members



        private static string PackageOutputData(IDictionary<string, IDictionary<string, string>> queryResults) {
            StringBuilder buffer = new StringBuilder();
            foreach (string name in queryResults.Keys) {
                StringBuilder oneDriver = new StringBuilder();
                foreach (KeyValuePair<string, string> kvp in queryResults[name]) {
                    string attributeName = kvp.Key;
                    string attributeValue = kvp.Value;
                    if (oneDriver.Length > 0) {
                        oneDriver.Append(BdnaDelimiters.DELIMITER2_TAG);
                    }
                    oneDriver.Append(attributeName +"=\"" + attributeValue +"\"");
                }
                if (oneDriver.Length > 0) {
                    if (buffer.Length > 0) {
                        buffer.Append(BdnaDelimiters.DELIMITER1_TAG);
                    }
                    buffer.Append(oneDriver);
                }
            }
            return buffer.ToString();
        }

        /// <summary>
        /// Retrieve System DSN 
        /// </summary>
        /// <param name="taskID">Task ID</param>
        /// <param name="wmiRegistry">WMI Registry</param>
        /// <param name="queryResults">Result Buffer</param>
        /// <returns></returns>
        private static ResultCodes GetSystemDSN(string taskID,
                                                ManagementClass wmiRegistry,
                                                IDictionary<string, IDictionary<string, string>> odbcDrivers,
                                                IDictionary<string, IDictionary<string, string>> systemDSNs) {
            string dsnRegPath = @"SOFTWARE\ODBC\ODBC.INI\";
            string[] dsnNames = null;
            RegistryTypes[] registryTypes = null;
            ResultCodes resultCode = Lib.GetRegistryImmediateKeyValues(taskID,
                                                                       wmiRegistry,
                                                                       dsnRegPath + @"ODBC Data Sources",
                                                                       out dsnNames,
                                                                       out registryTypes);
            if (resultCode != ResultCodes.RC_SUCCESS) { 
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      @"task ID {0} : Error collecting System DSN from registry HKLM\Software\ODBC.INI\ODBC Data Sources.",
                                      taskID);
                return ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            }
            if (dsnNames != null) {
                foreach (string dsnName in dsnNames) {
                    string driverType = null;
                    resultCode = Lib.GetRegistryStringValue(taskID,
                                                            wmiRegistry,
                                                            dsnRegPath + @"ODBC Data Sources",
                                                            dsnName,
                                                            out driverType);

                    if (string.IsNullOrEmpty(driverType)) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              @"ID {0} : Error collecting DSN DriverType: {1}",
                                              dsnName);
                        continue;
                    }

                    systemDSNs[dsnName] = new Dictionary<string, string>();
                    systemDSNs[dsnName]["name"] = dsnName;
                    systemDSNs[dsnName]["driverType"] = driverType;

                    if (odbcDrivers.ContainsKey(driverType)) {
                        if (!odbcDrivers[driverType].ContainsKey(@"systemDSN")) {
                            odbcDrivers[driverType][@"systemDSN"] = dsnName;
                        } else {
                            if (!string.IsNullOrEmpty(odbcDrivers[driverType]["systemDSN"])) {
                                odbcDrivers[driverType]["systemDSN"] += ", ";
                            }
                            odbcDrivers[driverType]["systemDSN"] += dsnName;
                        }
                    }

                    string[] subKeyNames = null;
                    RegistryTypes[] keyDataTypes;
                    resultCode = Lib.GetRegistryImmediateKeyValues(taskID,
                                                                   wmiRegistry,
                                                                   dsnRegPath + dsnName,
                                                                   out subKeyNames,
                                                                   out keyDataTypes);
                    for (int i = 0; i < subKeyNames.Length; i++) {
                        string keyName = subKeyNames[i];
                        RegistryTypes regType = keyDataTypes[i];

                        if (regType == RegistryTypes.REG_SZ) {
                            string keyValue = string.Empty;
                            Lib.GetRegistryStringValue(taskID,
                                                       wmiRegistry,
                                                       dsnRegPath + dsnName,
                                                       keyName,
                                                       out keyValue);
                            systemDSNs[dsnName][keyName] = keyValue;
                        }
                    }
                }
            }
            return ResultCodes.RC_SUCCESS;
        }

        /// <summary>
        /// Retrieve all ODBC Drivers (name, version, filePath, installDate, manufacturer)
        /// </summary>
        /// <param name="taskID">Task ID</param>
        /// <param name="cimvScope">ManagementScope</param>
        /// <param name="wmiRegistry">WMI Registry</param>
        /// <param name="queryResults">Result Buffer</param>
        /// <returns></returns>
        private static ResultCodes GetODBCDrivers(string taskID,
                                                  ManagementScope cimvScope,
                                                  ManagementClass wmiRegistry,
                                                  IDictionary<string, IDictionary<string, string>> queryResults) {
            string odbcRegPath = @"SOFTWARE\ODBC\ODBCINST.INI\";
            string[] driverNamesArray = null;
            RegistryTypes[] registryTypes = null;
            ResultCodes resultCode = Lib.GetRegistryImmediateKeyValues(taskID,
                                                                       wmiRegistry,
                                                                       odbcRegPath + @"ODBC Drivers",
                                                                       out driverNamesArray,
                                                                       out registryTypes);
            if (resultCode != ResultCodes.RC_SUCCESS) { 
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      @"task ID {0} : Error collecting ODBC Drivers from registry HKLM\Software\ODBC\ODBCINST.INI\ODBC Drivers",
                                      taskID);
                return ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            }

            for (int i=0; i < driverNamesArray.Length; i++) {
                string driverName = driverNamesArray[i];
                string installStatus = string.Empty;
                resultCode = Lib.GetRegistryStringValue(taskID,
                                                        wmiRegistry,
                                                        odbcRegPath + @"ODBC Drivers",
                                                        driverName,
                                                        out installStatus);
                if (resultCode != ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          @"taskID {0} Error collecting Driver Install Status: {1}",
                                          taskID,
                                          driverName);
                    continue;
                }
                if (!string.IsNullOrEmpty(installStatus) && installStatus == "Installed") {
                    queryResults[driverName] = new Dictionary<string, string>();
                    queryResults[driverName]["name"] = driverName;

                    // Retrieve ODBC property                        
                    string driverFilePath = string.Empty;
                    resultCode = Lib.GetRegistryStringValue(taskID,
                                                            wmiRegistry,
                                                            odbcRegPath + driverName,
                                                            @"Driver",
                                                            out driverFilePath);
                    if (resultCode != ResultCodes.RC_SUCCESS) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              @"taskID {0} Error collecting Driver <{1}> Detail from registry {2}",
                                              taskID,
                                              driverName,
                                              odbcRegPath + driverName);
                        continue;
                    }
                    try {
                        queryResults[driverName]["filePath"] = driverFilePath;
                        Dictionary<string, string> fileProperties = null;
                        resultCode = Lib.RetrieveFileProperties(taskID,
                                                                cimvScope,
                                                                driverFilePath,
                                                                out fileProperties);

                        if (fileProperties.ContainsKey(@"Version")) {
                            queryResults[driverName]["version"] = fileProperties[@"Version"];
                        }
                        if (fileProperties.ContainsKey(@"InstallDate")) {
                            queryResults[driverName]["InstallDate"] = fileProperties[@"InstallDate"];
                        }
                        if (fileProperties.ContainsKey(@"Manufacturer")) {
                            queryResults[driverName]["Manufacturer"] = fileProperties[@"Manufacturer"];
                        }
                    } catch (Exception ex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              @"taskID {0} Error retrieving file properties for driver: {1} : {2}",
                                              taskID,
                                              driverName,
                                              driverFilePath);
                    }
                }                 
            }
            return ResultCodes.RC_SUCCESS;
        }

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;
    }
}