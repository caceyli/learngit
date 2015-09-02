#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.33 $
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

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script for Microsoft SQL Server level2 data.
    /// </summary>
    public class MSSQLServerBaseInstallationScript : ICollectionScriptRuntime {

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
                                  "Task Id {0}: Collection script MSSQLServerBaseInstallationScript.",
                                  m_taskId);
            try {

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to MSSQLServerBaseInstallationScript is null.",
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
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;
                    m_defaultScope = connection[@"default"] as ManagementScope;

                    if (!m_cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    } else if (!m_defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed",
                                              m_taskId);
                    } else {
                        m_wmiRegistry = new ManagementClass(m_defaultScope, new ManagementPath(@"StdRegProv"), null);

                        using (m_wmiRegistry) {
                            GetServiceInfo();
                            resultCode = GetBaseMSSQLDetails();
                        }

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"BaseSQLServerDetails"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"BaseSQLServerDetails")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG)
                                   .Append(m_collectedData)
                                   .Append(BdnaDelimiters.END_TAG);
                        }

                    }

                }

            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in MSSQLServerBaseInstallationScript",
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
                                  "Task Id {0}: MSSQLServerBaseInstallationScript processing complete.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        #endregion ICollectionScriptRuntime Members

        /// <summary>
        /// Method to harvest the start mode and current state of
        /// SQL Server related services.  This data is optional, any
        /// errors encountered are logged but task execution will
        /// continue.
        /// </summary>
        private void GetServiceInfo() {
            Stopwatch queryTime = new Stopwatch();

            try {
                ResultCodes resultCode = ResultCodes.RC_SUCCESS;
                ManagementObjectCollection moc = null;
                ManagementClass mc = new ManagementClass(m_cimvScope, new ManagementPath(@"Win32_OperatingSystem"), null);

                using (mc) {
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Retrieving class Win32_OperatingSystem",
                                          m_taskId);
                    queryTime.Start();
                    moc = mc.GetInstances();
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Query for Win32_OperatingSystem complete.  Elapsed time {1}.",
                                          m_taskId,
                                          queryTime.Elapsed.ToString());
                }

                ManagementObject win32Os = null;

                if (null != moc) {
                    using (moc) {
                        foreach (ManagementObject mo in moc) {
                            win32Os = mo;
                        }
                    }
                }

                Debug.Assert(null != win32Os);

                m_computerName = win32Os.Properties[@"CSName"].Value as string;
                Debug.Assert(null != m_computerName);

                string osVersion = win32Os.Properties[@"Version"].Value as string;
                Debug.Assert(null != osVersion);

                bool useLikeConstraint = false;
                string[] versionNumbers = osVersion.Split('.');

                if (2 <= versionNumbers.Length) {
                    int majorVersion = 0;
                    int minorVersion = 0;

                    if (Int32.TryParse(versionNumbers[0], out majorVersion)
                            && Int32.TryParse(versionNumbers[1], out minorVersion)) {
                        useLikeConstraint = 5 < majorVersion || 5 == majorVersion && 0 < minorVersion;
                    }
                }

                //
                // We use a different query for Windows XP and later
                // OSs.  Old OSs like w2k don't support the LIKE constraint
                // so we have to pull accross the entire service table and
                // search for what we want.  With XP and later we only pull
                // accross SQL Server related services (less traffic and
                // less work).
                string query = (useLikeConstraint)
                    ? @"SELECT Name,StartMode,State FROM Win32_Service WHERE Name LIKE ""%MSSQL%"""
                    : @"SELECT Name,StartMode,State FROM Win32_Service";

                ManagementObjectSearcher mos = new ManagementObjectSearcher(m_cimvScope,
                                                                            new ObjectQuery(query));

                using (mos) {
                    resultCode = Lib.ExecuteWqlQuery(m_taskId, mos, out moc);
                }

                if (null != moc) {
                    using (moc) {
                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            foreach (ManagementObject serviceInfo in moc) {
                                string serviceName = serviceInfo.Properties[@"Name"].Value as string;
                                if (serviceName.Contains(@"ADHelper") || serviceName.Contains(@"OLAPService")) continue;

                                //
                                // Yeah, this test is redundant when the Like clause is used...
                                if (0 <= serviceName.ToUpper().IndexOf(@"MSSQL")) {
                                    m_results[serviceName.ToUpper() + @"_startMode"] = serviceInfo.Properties[@"StartMode"].Value as string;
                                    m_results[serviceName.ToUpper() + @"_state"] = serviceInfo.Properties[@"State"].Value as string;
                                }
                            }
                        }
                    }
                }
            } catch (ManagementException mex) {
                Lib.LogManagementException(m_taskId,
                                           queryTime,
                                           "Query for Win32_OperatingSystem failed",
                                           mex);
            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 queryTime,
                                 "Query for Win32_OperatingSystem failed",
                                 ex);
            }

        }

        private ResultCodes GetBaseMSSQLDetails() {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string[] installedInstanceNames = null;
            string regPath = s_registryKeyMicrosoftSqlServer;

            // Get CD Key information
            IDictionary<string, string> CDKeys = new Dictionary<string, string>();
            resultCode = Lib.GetRegistryStringArrayValue(m_taskId, m_wmiRegistry, regPath, @"InstalledInstances", out installedInstanceNames);
            
            // Default Instance
            if (resultCode == ResultCodes.RC_SUCCESS && installedInstanceNames == null) {
                regPath = s_registryKeyMicrosoftSqlServer64;
                resultCode = Lib.GetRegistryStringArrayValue(m_taskId, m_wmiRegistry, regPath, @"InstalledInstances", out installedInstanceNames);
                if (resultCode == ResultCodes.RC_SUCCESS && installedInstanceNames == null) {
                    installedInstanceNames = new string[] { s_defaultInstanceName };
                }
                string tempValue = null;
                resultCode = Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, @"software\Wow6432Node\Microsoft\IE Setup\Setup", @"Path", out tempValue);
                if (string.IsNullOrEmpty(tempValue)) {
                    regPath = s_registryKeyMicrosoftSqlServer;
                }
            }

            // Find CD Keys
            FindRegistryWithCDKey(regPath.Substring(0, regPath.Length - 1), CDKeys);
            StringBuilder keyFound = new StringBuilder();
            foreach (KeyValuePair<string, string> kvp in CDKeys) {
                Lib.Logger.TraceEvent(TraceEventType.Information,
                                      0,
                                      "CD Key Path<{0}><{1}>",
                                      kvp.Key,
                                      kvp.Value);
                if (!keyFound.ToString().Contains(kvp.Value)) {
                    if (keyFound.Length > 0) {
                        keyFound.Append(@", ");
                    }
                    keyFound.Append(kvp.Value);
                }
            }

            // Get Instances Details
            foreach (string instanceName in installedInstanceNames) {
                if (regPath == s_registryKeyMicrosoftSqlServer) {
                    resultCode = GetInstanceDetails(instanceName, regPath, keyFound.ToString());
                } else {
                    resultCode = GetInstanceDetails(instanceName, s_registryKeyMicrosoftSqlServer64, keyFound.ToString());
                }
                if (ResultCodes.RC_SUCCESS != resultCode) {
                    break;
                }
            }

            if (ResultCodes.RC_SUCCESS == resultCode) {
                resultCode = GetBaseInstallationDetails(s_registryKeyUninstall64);
                resultCode = GetBaseInstallationDetails(s_registryKeyUninstall);
            }

            if (ResultCodes.RC_SUCCESS == resultCode) {
                foreach (string instanceName in installedInstanceNames) {
                    PackageOutputInfo(instanceName);
                }
            }
            return resultCode;
        }

        /**
         * Find Registry with CD Key value.
         */
        private ResultCodes FindRegistryWithCDKey(string registryPath,
                       IDictionary<string, string> CDKeys) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            try {
                string[] subKeys = null;
                resultCode = FindCDKeyValues(registryPath, CDKeys);
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    resultCode = Lib.GetRegistryImmediateSubKeys(m_taskId, m_wmiRegistry, registryPath, out subKeys);
                    if ((ResultCodes.RC_SUCCESS == resultCode) && (subKeys != null)) {
                        foreach (string subKey in subKeys) {
                            resultCode = FindRegistryWithCDKey(registryPath + @"\" + subKey, CDKeys);
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Exception caught in FindCDKeyValues from path {1}, Error: {2}",
                                      m_taskId,
                                      registryPath,
                                      ex);
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            }
            return resultCode;
        }

        /**
         * Find CD Key Value.
         */
        private ResultCodes FindCDKeyValues(string registryPath,
                       IDictionary<string, string> CDKeys) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            try {                
                string[] keyNames = null;
                RegistryTypes[] keyTypes = null;
                resultCode = Lib.GetRegistryImmediateKeyValues(m_taskId, m_wmiRegistry, registryPath, out keyNames, out keyTypes);

                if ((ResultCodes.RC_SUCCESS == resultCode) && (keyNames != null) && (keyTypes != null)) {
                    for (int i = 0; i < keyNames.Length; i++) {
                        string keyName = keyNames[i];
                        byte[] digitalProductID = null;
                        RegistryTypes keyType = keyTypes[i];

                        if (keyName.Trim().ToLower().StartsWith(@"digitalproductid") &&
                            keyType == RegistryTypes.REG_BINARY) {
                            resultCode = Lib.GetRegistryBinaryValue(m_taskId,
                                                                    m_wmiRegistry,
                                                                    registryPath,
                                                                    keyName,
                                                                    out digitalProductID);
                            if (resultCode == ResultCodes.RC_SUCCESS && digitalProductID != null) {
                                string CDKey = Lib.ExtractLicenseKeyFromMSDigitalProductID(m_taskId, digitalProductID);
                                if (!String.IsNullOrEmpty(CDKey)) {
                                    CDKeys[registryPath] = CDKey;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Exception caught in FindCDKeyValues from path {1}, Error: {2}",
                                      m_taskId,
                                      registryPath,
                                      ex);
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            }
            return resultCode;
        }

        /**
         * Get MSSQL Server Instance Details
         */ 
        private ResultCodes GetInstanceDetails(
                string                          instanceName,
                string                          registryPath,
                string                          CDKey) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string baseInstanceKey = null;

            m_results[instanceName + @"_serverName"] = (String.IsNullOrEmpty(m_computerName))? @"(Default)" : m_computerName;

            if (instanceName.Equals(s_defaultInstanceName, StringComparison.OrdinalIgnoreCase)) {
                if (registryPath == s_registryKeyMicrosoftSqlServer) {
                    baseInstanceKey = s_registryKeyMSSqlServer;
                } else {
                    baseInstanceKey = s_registryKeyMSSqlServer64;
                }                
                m_results[instanceName + @"_instanceName"] = @"MSSQLSERVER";
            } else {
                if (registryPath == s_registryKeyMicrosoftSqlServer) {
                    baseInstanceKey = s_registryKeyMicrosoftSqlServer;
                } else {
                    baseInstanceKey = s_registryKeyMicrosoftSqlServer64;
                }
                m_results[instanceName + @"_instanceName"] = instanceName;
            }

            m_results[instanceName + @"_cdKey"] = CDKey;
            string fullInstanceKey = baseInstanceKey + @"MSSQLServer";
            string versionKey = fullInstanceKey + @"\CurrentVersion";
            string setupKey = baseInstanceKey + @"Setup";
            string editionKey = null;
            string strVersion = null;
            resultCode = Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, versionKey, @"CurrentVersion", out strVersion);

            if (!String.IsNullOrEmpty(strVersion)) {
                m_results[instanceName + @"_version"] = strVersion;
            } else {
                //try path for SQL Server 2005/2008/2012
                fullInstanceKey = baseInstanceKey + instanceName +@"\MSSQLServer";
                setupKey = baseInstanceKey + instanceName + @"\Setup";
                versionKey = fullInstanceKey + @"\CurrentVersion";
                resultCode = Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, versionKey, @"CurrentVersion", out strVersion);

                if (!String.IsNullOrEmpty(strVersion)) {
                    m_results[instanceName + @"_version"] = strVersion;
                }
            }
            if (m_results.ContainsKey(instanceName + @"_version")) {
                if (!String.IsNullOrEmpty(m_results[instanceName + @"_version"])) {
                    if (m_results[instanceName + @"_version"].StartsWith(@"10")) {
                        if (registryPath == s_registryKeyMicrosoftSqlServer) {
                            baseInstanceKey = s_registryKeyMicrosoftSqlServer;
                        } else {
                            baseInstanceKey = s_registryKeyMicrosoftSqlServer64;
                        }
                        fullInstanceKey = baseInstanceKey + @"MSSQL10." + instanceName + @"\MSSQLServer";
                        setupKey = baseInstanceKey + @"MSSQL10." + instanceName + @"\Setup";
                    }
                }
            }
            string sqlSubPath = null;
            resultCode = Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, baseInstanceKey + @"Instance Names\SQL", instanceName, out sqlSubPath);

            if (!String.IsNullOrEmpty(sqlSubPath)) {
                fullInstanceKey = baseInstanceKey + sqlSubPath + @"\MSSQLServer";
                editionKey = baseInstanceKey + sqlSubPath + @"\Setup";
            }
            // Backup Dir
            string backupDir = String.Empty;
            Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, fullInstanceKey, @"BackupDirectory", out backupDir);
            if (!string.IsNullOrEmpty(backupDir)) {
                m_results[instanceName + @"_backupDirectory"] = backupDir;
            }
            ManagementBaseObject inputParameters = m_wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
            ManagementBaseObject outputParameters = null;


            //Edition
            string edition = String.Empty;
            Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, editionKey, @"Edition", out edition);
            if (string.IsNullOrEmpty(edition)) {
                string tempInstanceKey = null, tempSubKeyValue = null;
                if (baseInstanceKey == s_registryKeyMSSqlServer) {
                    tempInstanceKey = s_registryKeyMicrosoftSqlServer;
                } else {
                    tempInstanceKey = s_registryKeyMicrosoftSqlServer64;
                }
                Lib.GetRegistryStringValue(m_taskId,
                                           m_wmiRegistry,
                                           tempInstanceKey + @"Instance Names\SQL",
                                           instanceName,
                                           out tempSubKeyValue);
                if (!string.IsNullOrEmpty(tempSubKeyValue)) {
                    tempInstanceKey += (tempSubKeyValue + @"\Setup");
                    Lib.GetRegistryDWordStringValue(m_taskId, m_wmiRegistry, tempInstanceKey, @"Edition", out edition);
                }
            }

            if (!string.IsNullOrEmpty(edition)) {
                m_results[instanceName + @"_edition"] = edition;
            }

            // DefaultDomain
            string defaultDomain = String.Empty;
            Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, fullInstanceKey, @"DefaultDomain", out defaultDomain);
            if (!string.IsNullOrEmpty(defaultDomain)) {
                m_results[instanceName + @"_defaultDomain"] = defaultDomain;
            }

            // Login Mode
            string loginMode = String.Empty;
            Lib.GetRegistryDWordStringValue(m_taskId, m_wmiRegistry, fullInstanceKey, @"LoginMode", out loginMode);
            if (string.IsNullOrEmpty(loginMode)) {
                string tempInstanceKey = null, tempSubKeyValue = null;
                if (baseInstanceKey == s_registryKeyMSSqlServer) {
                    tempInstanceKey = s_registryKeyMicrosoftSqlServer;
                } else {
                    tempInstanceKey = s_registryKeyMicrosoftSqlServer64;
                }
                Lib.GetRegistryStringValue(m_taskId, 
                                           m_wmiRegistry, 
                                           tempInstanceKey + @"Instance Names\SQL", 
                                           instanceName, 
                                           out tempSubKeyValue);
                if (!string.IsNullOrEmpty(tempSubKeyValue)) {
                    tempInstanceKey += (tempSubKeyValue + @"\MSSQLServer");
                    Lib.GetRegistryDWordStringValue(m_taskId, m_wmiRegistry, tempInstanceKey, @"LoginMode", out loginMode);
                }
            }
            if (!string.IsNullOrEmpty(loginMode)) {
                switch (loginMode) {
                    case "00000000":
                        m_results[instanceName + @"_loginMode"] = @"SQL Server Authentication Mode";
                        break;

                    case "00000001":
                        m_results[instanceName + @"_loginMode"] = @"Windows Authentication Mode";
                        break;

                    case "00000002":
                        m_results[instanceName + @"_loginMode"] = @"Mixed Authentication Mode";
                        break;
                }
            }

            string[] protocolList = null;
            string registryKeySocketLib = fullInstanceKey + @"\SuperSocketNetLib";
            Lib.GetRegistryStringArrayValue(m_taskId, m_wmiRegistry, registryKeySocketLib, @"ProtocolList", out protocolList);
            bool emptyArray = (null == protocolList);
            if (protocolList != null) {
                if (protocolList.Length == 0) {
                    emptyArray = true;
                }
            }
            if (emptyArray) {
                Lib.GetRegistryStringArrayValue(m_taskId, m_wmiRegistry, fullInstanceKey, @"ListenOn", out protocolList);
            }
            emptyArray = (null == protocolList);
            if (protocolList != null) {
                if (protocolList.Length == 0) {
                    emptyArray = true;
                }
            }
            //
            // SQL Server 2005
            if (emptyArray) {
                string[] subKeys = null;
                Lib.GetRegistrySubkeyName(m_taskId, m_wmiRegistry, registryKeySocketLib, out subKeys);
                if (subKeys != null && subKeys.Length > 0) {
                    protocolList = new string[subKeys.Length];
                    for (int i = 0; i < subKeys.Length; i++) {
                        if (!subKeys[i].Equals(@"AdminConnection")) {
                            protocolList[i] = subKeys[i];
                        } else {
                            protocolList[i] = "";
                        }
                    }
                }
            }

            StringBuilder protocols = new StringBuilder();
            if (null != protocolList) {
                foreach (string protocol in protocolList) {
                    if (string.IsNullOrEmpty(protocol)) continue;
                    if (protocols.Length > 0) {
                        protocols.Append(',');
                    }

                    if (protocol.Equals(@"np", StringComparison.OrdinalIgnoreCase)) {

                        protocols.Append(@"Named Pipes");
                        string registryKeyNamedPipes = registryKeySocketLib + @"\Np";
                        string pipeName = null;
                        string clusterName = @"N/A";
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyNamedPipes, @"PipeName", out pipeName);
                        if (!string.IsNullOrEmpty(pipeName)) {
                            m_results[instanceName + @"_pipeName"] = pipeName;
                            Match clusterNameMatch = s_clusterNameRegex.Match(pipeName);
                            if (s_clusterNameRegex.IsMatch(pipeName)) {
                                clusterName = clusterNameMatch.Groups[1].Value;
                                if (s_clusterNameWithInstanceRegex.IsMatch(clusterName)) {
                                    Match clusterNameWithInstanceMatch = s_clusterNameWithInstanceRegex.Match(clusterName);
                                    clusterName = clusterNameWithInstanceMatch.Groups[1].Value + clusterNameWithInstanceMatch.Groups[2].Value;
                                }
                            }
                            m_results[instanceName + @"_clusterName"] = clusterName;
                        }
                    } else if (protocol.Equals(@"tcp", StringComparison.OrdinalIgnoreCase)) {
                        protocols.Append(@"TCP/IP");
                        string portString = null;
                        string registryKeyTcp = registryKeySocketLib + @"\Tcp";
                        string clusterIP = @"N/A";
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp, @"TcpPort", out portString);
                        if (!String.IsNullOrEmpty(portString)) {
                            m_results[instanceName + @"_port"] = portString;
                        }

                        //
                        // SQL Server 2005
                        if (!m_results.ContainsKey(instanceName + @"_port")) {
                            Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IPAll", @"TcpPort", out portString);
                            if (string.IsNullOrEmpty(portString)) {
                                Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IPAll", @"TcpDynamicPorts", out portString);
                                if (!string.IsNullOrEmpty(portString)) {
                                    m_results[instanceName + @"_port"] = portString;
                                }
                            }
                        }

                        uint tcpHideFlag = 0;
                        Lib.GetRegistryDWordStringValue(m_taskId, m_wmiRegistry, registryKeyTcp, @"TcpHideFlag", out tcpHideFlag);
                        m_results[instanceName + @"_hideServer"] = (1 == tcpHideFlag) ? @"True" : @"False";
                        string clusterIPLine;
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP1", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP2", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine) && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP3", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP4", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP5", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP6", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP7", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP8", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP9", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP10", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP11", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP12", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, registryKeyTcp + @"\IP13", @"IpAddress", out clusterIPLine);
                        if (!string.IsNullOrEmpty(clusterIPLine)  && s_IPRegex.IsMatch(clusterIPLine))
                        {
                            if (clusterIP == @"N/A")
                            {
                                clusterIP = clusterIPLine;
                            }
                            else
                            {
                                clusterIP = clusterIP + @" " + clusterIPLine;
                            }
                        }
                        m_results[instanceName + @"_clusterIP"] = clusterIP;
                    } else if (protocol.Equals(@"rpc", StringComparison.OrdinalIgnoreCase)) {
                        protocols.Append(@"Multiprotocol");
                    } else if (protocol.Equals(@"spx", StringComparison.OrdinalIgnoreCase)) {
                        protocols.Append(@"NWLink IPX/SPX");
                    } else if (protocol.Equals(@"adsp", StringComparison.OrdinalIgnoreCase)) {
                        protocols.Append(@"AppleTalk");
                    } else if (protocol.Equals(@"bv", StringComparison.OrdinalIgnoreCase)) {
                        protocols.Append(@"Banyan Vines");
                    } else if (protocol.ToLower().Equals(@"via")) {
                        protocols.Append(@"VIA");
                    } else if (protocol.ToLower().Equals(@"sm")) {
                        protocols.Append(@"SM");
                    } else {
                        // @todo try to verify that these only need to
                        // be executed if all of the above checks fail.
                        MatchCollection mc = s_ssnmpnRegex.Matches(protocol);
                        if (0 < mc.Count) {
                            protocols.Append(@"Named Pipes");
                            m_results[instanceName + @"_pipeName"] = mc[0].Groups[0].Value;
                        }

                        MatchCollection mc2 = s_ssmssoRegex.Matches(protocol);
                        if (0 < mc2.Count) {
                            if (mc.Count > 0) {
                                protocols.Append(',');
                            }
                            protocols.Append(@"TCP/IP");
                            m_results[instanceName + @"_port"] = mc2[0].Groups[0].Value;
                        }

                        MatchCollection mc3 = s_ssmsrpRegex.Matches(protocol);
                        if (0 < mc3.Count) {
                            if (mc.Count > 0 || mc2.Count > 0) {
                                protocols.Append(',');
                            }
                            protocols.Append(@"Multiprotocol");
                        }

                        MatchCollection mc4 = s_ssmsadRegex.Matches(protocol);
                        if (0 < mc4.Count) {
                            if (mc.Count > 0 || mc2.Count > 0 || mc3.Count > 0) {
                                protocols.Append(',');
                            }
                            protocols.Append(@"AppleTalk");
                        }

                        MatchCollection mc5 = s_ssmsspRegex.Matches(protocol);
                        if (0 < mc5.Count) {
                            if (mc.Count > 0 || mc2.Count > 0 || mc3.Count > 0 || mc4.Count > 0) {
                                protocols.Append(',');
                            }
                            protocols.Append(@"NWLink IPX/SPX");
                        }

                        MatchCollection mc6 = s_ssmsviRegex.Matches(protocol);
                        if (0 < mc6.Count) {
                            if (mc.Count > 0 || mc2.Count > 0 || mc3.Count > 0 || mc4.Count > 0 || mc5.Count > 0) {
                                protocols.Append(',');
                            }
                            protocols.Append(@"Banyan Vines");
                            m_results[instanceName + @"_pipeName"] = mc6[0].Groups[0].Value;
                        }
                    }
                }
            }
            m_results[instanceName + @"_protocols"] = protocols.ToString();

            //
            // Install Directory
            String strInstallDir = null;
            Lib.GetRegistryStringValue(m_taskId, m_wmiRegistry, setupKey, @"SQLPath", out strInstallDir);
            if (!String.IsNullOrEmpty(strInstallDir)) {
                m_results[instanceName + @"_installDirectory"] = strInstallDir;
            }
            return resultCode;
        }

        /// <summary>
        /// Find SQL Server from uninstall string.
        /// </summary>
        /// <param name="uninstallKey"></param>
        /// <returns></returns>
        private ResultCodes GetBaseInstallationDetails(string uninstallKey) {
            string[] subKeys = null;
            ResultCodes resultCode = Lib.GetRegistrySubkeyName(m_taskId, m_wmiRegistry, uninstallKey, out subKeys);
            if (ResultCodes.RC_SUCCESS == resultCode && subKeys != null) {
                if (subKeys.Length > 0) {
                    foreach (string subKey in subKeys) {
                        string displayName = null, instanceName = "";
                        string uninstallKeyName = uninstallKey + subKey;
                        resultCode = Lib.GetRegistryStringValue(m_taskId, 
                                                                m_wmiRegistry, 
                                                                uninstallKeyName, 
                                                                @"DisplayName", 
                                                                out displayName);
                        if (resultCode == ResultCodes.RC_SUCCESS && !string.IsNullOrEmpty(displayName)) {
                            if (!s_sqlServerRegex.IsMatch(displayName)) {
                                continue;
                            }

                            //
                            // MSSQL 2005
                            if (s_sqlServer2005Regex.IsMatch(displayName)) {

                                // skip generic entry
                                if (s_2005Regex.IsMatch(displayName) && subKey.Equals(displayName)) {
                                    continue;
                                }
                                // skip Extra Services
                                if (s_sqlServer2005ExtrasRegex.IsMatch(displayName))
                                    continue;
                            }

                            if (s_exclusionRegex.IsMatch(displayName)) {
                                continue;
                            }

                            MatchCollection mcInt = s_msSqlServerRegex.Matches(displayName);

                            if (mcInt.Count > 0) {
                                instanceName = mcInt[0].Groups[1].Value;
                            } else {
                                instanceName = s_defaultInstanceName;
                            }
                            if (instanceName.Contains("bit")) {
                                instanceName = s_defaultInstanceName;
                            }

                            Match match = s_editionRegex.Match(displayName);
                            if (match.Success) {
                                if (!m_results.ContainsKey(instanceName + @"_edition")) {
                                    m_results[instanceName + @"_edition"] = match.Groups[@"edition"] + @" Edition";
                                }
                            }

                            string version = null;
                            if (!m_results.ContainsKey(instanceName + @"_version")) {
                                resultCode = Lib.GetRegistryStringValue(m_taskId, 
                                                                        m_wmiRegistry, 
                                                                        uninstallKeyName, 
                                                                        @"DisplayVersion", 
                                                                        out version);
                                if (String.IsNullOrEmpty(version) && s_versionRegex.IsMatch(displayName)) {
                                    Match versionMatch = s_versionRegex.Match(displayName);
                                    version = versionMatch.Groups[0].Value as string;
                                }

                                if (!String.IsNullOrEmpty(version)) {
                                    if (!m_results.ContainsKey(instanceName + @"_version")) {
                                        m_results[instanceName + @"_version"] = version;
                                    }
                                }
                            }
                            string installDirectory = null;
                            if (!m_results.ContainsKey(instanceName + @"_installDirectory")) {
                                resultCode = Lib.GetRegistryStringValue(m_taskId, 
                                                                        m_wmiRegistry, 
                                                                        uninstallKeyName, 
                                                                        @"InstallLocation", 
                                                                        out installDirectory);
                                if (!String.IsNullOrEmpty(installDirectory)) {
                                    if (!m_results.ContainsKey(instanceName + @"_installDirectory")) {
                                        m_results[instanceName + @"_installDirectory"] = installDirectory;
                                    }
                                }
                            }

                            string installDate = null;
                            if (!m_results.ContainsKey(instanceName + @"_installDate")) {
                                resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                                        m_wmiRegistry,
                                                                        uninstallKeyName,
                                                                        @"InstallDate",
                                                                        out installDate);
                                if (!String.IsNullOrEmpty(installDirectory)) {
                                    if (!m_results.ContainsKey(instanceName + @"_installDate")) {
                                        m_results[instanceName + @"_installDate"] = installDate;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return resultCode;
        }

        private void PackageOutputInfo(
                string                          instanceName) {
            StringBuilder tempBuffer = new StringBuilder();
            string instanceNameKey = instanceName + @"_instanceName";
            tempBuffer.Append(BdnaDelimiters.DELIMITER1_TAG);

            if (m_results.ContainsKey(instanceNameKey)) {
                string instanceNameValue = m_results[instanceNameKey];

                if (null != instanceNameValue && !instanceNameValue.Equals(@"MSSQLSERVER")) {
                    tempBuffer.Append(@"elementName=""MSSQL(")
                                   .Append(instanceNameValue)
                                   .Append(@")""");
                } else {
                    tempBuffer.Append(@"elementName=""MSSQL""");
                }

            } else {
                tempBuffer.Append(@"elementName=""MSSQL""");
            }

            string bizVersion = null;
            string versionKey = instanceName + @"_version";

            if (m_results.ContainsKey(versionKey)) {
                string version = m_results[versionKey];

                if (null != version && 0 <= version.IndexOf('.')) {
                    Int32 i = 0;

                    if (Int32.TryParse(version.Substring(0, version.IndexOf('.')), out i)) {

                        switch (i) {
                            case 6:
                                bizVersion = @"6.5";
                                break;

                            case 7:
                                bizVersion = @"7";
                                break;

                            case 8:
                                bizVersion = @"2000";
                                break;

                            case 9:
                                bizVersion = @"2005";
                                break;
                            case 10:
                                bizVersion = @"2008";
                                break;
                            case 11:
                                bizVersion = @"2012";
                                break;
                        }

                    }

                }

            }

            string nameKey = instanceName + @"_name";
            tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"name=""");

            if (m_results.ContainsKey(nameKey)) {
                tempBuffer.Append(m_results[nameKey])
                               .Append('"');
            } else {
                tempBuffer.Append(@"Microsoft SQL Server ")
                               .Append(bizVersion)
                               .Append('"');
            }

            string serverNameKey = instanceName + @"_serverName";

            if (m_results.ContainsKey(serverNameKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"serverName=""")
                               .Append(m_results[serverNameKey])
                               .Append('"');
            }

            tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG);

            if (m_results.ContainsKey(instanceNameKey)) {
                tempBuffer.Append(@"instanceName=""")
                               .Append(m_results[instanceNameKey])
                               .Append('"');
            } else {
                tempBuffer.Append(@"instanceName=""MSSQLSERVER""");
            }

            string editionKey = instanceName + @"_edition";
            if (m_results.ContainsKey(editionKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"edition=""")
                               .Append(m_results[editionKey])
                               .Append('"');
            }

            if (m_results.ContainsKey(versionKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"version=""")
                               .Append(m_results[versionKey])
                               .Append('"');
            }

            string installDateKey = instanceName + @"_installDate";
            if (m_results.ContainsKey(installDateKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"installDate=""")
                               .Append(m_results[installDateKey])
                               .Append('"');
            }

            string installDirectoryKey = instanceName + @"_installDirectory";
            if (m_results.ContainsKey(installDirectoryKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"installDirectory=""")
                               .Append(m_results[installDirectoryKey])
                               .Append('"');
            }

            string cdKey = instanceName + @"_cdKey";
            if (m_results.ContainsKey(cdKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"cdKey=""")
                               .Append(m_results[cdKey])
                               .Append('"');
            }

            string backupDirectoryKey = instanceName + @"_backupDirectory";
            if (m_results.ContainsKey(backupDirectoryKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"backupDirectory=""")
                               .Append(m_results[backupDirectoryKey])
                               .Append('"');
            }

            string defaultDomainKey = instanceName + @"_defaultDomain";

            if (m_results.ContainsKey(defaultDomainKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"defaultDomain=""")
                               .Append(m_results[defaultDomainKey])
                               .Append('"');
            }

            string loginModeKey = instanceName + @"_loginMode";

            if (m_results.ContainsKey(loginModeKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"loginMode=""")
                               .Append(m_results[loginModeKey])
                               .Append('"');
            }

            string protocolsKey = instanceName + @"_protocols";

            if (m_results.ContainsKey(protocolsKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"protocols=""")
                               .Append(m_results[protocolsKey])
                               .Append('"');
            }

            string portKey = instanceName + @"_port";

            if (m_results.ContainsKey(portKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"port=""")
                               .Append(m_results[portKey])
                               .Append('"');
            }

            string pipeNameKey = instanceName + @"_pipeName";

            if (m_results.ContainsKey(pipeNameKey)) {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"namedPipe=""")
                               .Append(m_results[pipeNameKey])
                               .Append('"');
            }

            string clusterNameKey = instanceName + @"_clusterName";

            if (m_results.ContainsKey(clusterNameKey))
            {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"clusterName=""")
                               .Append(m_results[clusterNameKey])
                               .Append('"');
            }

            string clusterIPKey = instanceName + @"_clusterIP";

            if (m_results.ContainsKey(clusterIPKey))
            {
                tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                               .Append(@"clusterIP=""")
                               .Append(m_results[clusterIPKey])
                               .Append('"');
            }

            if (instanceName.ToUpper().Equals(s_defaultInstanceName)) {
                string defaultInstanceStartModeKey = s_defaultInstanceName + @"_startMode";

                if (m_results.ContainsKey(defaultInstanceStartModeKey)) {
                    tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                                   .Append(@"startMode=""")
                                   .Append(m_results[defaultInstanceStartModeKey])
                                   .Append('"');
                }

                string defaultInstanceStateKey = s_defaultInstanceName + @"_state";

                if (m_results.ContainsKey(defaultInstanceStateKey)) {
                    tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                                   .Append(@"state=""")
                                   .Append(m_results[defaultInstanceStateKey])
                                   .Append('"');
                }

            } else {
                string startModeKey = @"MSSQL$" + instanceName.ToUpper() + @"_startMode";

                if (m_results.ContainsKey(startModeKey)) {
                    tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                                   .Append(@"startMode=""")
                                   .Append(m_results[startModeKey])
                                   .Append('"');
                }

                string stateKey = @"MSSQL$" + instanceName.ToUpper() + @"_state";

                if (m_results.ContainsKey(stateKey)) {
                    tempBuffer.Append(BdnaDelimiters.DELIMITER2_TAG)
                                   .Append(@"state=""")
                                   .Append(m_results[stateKey])
                                   .Append('"');
                }

            }
            if (!tempBuffer.ToString().Contains(@"name=""Microsoft SQL Server """) ||
                !tempBuffer.ToString().Contains(@"instanceName=""MSSQLSERVER""")) {
                m_collectedData.Append(tempBuffer);
            }

        }

        /**
         * Translate 64 bit string to 32 path
         */
        private string TranslateWow6432RegistryPath(string registryPath) {
            if (registryPath.StartsWith(s_registrySoftwarePath6432)) {
                return s_registrySoftwarePath + registryPath.Substring(s_registrySoftwarePath6432.Length);
            }
            return registryPath;
        }

        private static readonly string s_registrySoftwarePath6432 = @"SOFTWARE\Wow6432Node\";
        private static readonly string s_registrySoftwarePath = @"SOFTWARE\";
        private StringBuilder                   m_collectedData = new StringBuilder();
        ManagementScope                         m_cimvScope;
        ManagementScope                         m_defaultScope;
        private ManagementClass                 m_wmiRegistry;
        private string                          m_computerName;
        private string                          m_taskId;
        private IDictionary<string, string>     m_results = new Dictionary<string, string>();
        private static readonly string          s_defaultInstanceName = @"MSSQLSERVER";
        private static readonly string          s_registryKeyMicrosoftSqlServer = @"software\Microsoft\Microsoft SQL Server\";
        private static readonly string          s_registryKeyMicrosoftSqlServer64 = @"software\Wow6432Node\Microsoft\Microsoft SQL Server\";
        private static readonly string          s_registryKeyMSSqlServer = @"software\Microsoft\MSSQLServer\";
        private static readonly string          s_registryKeyMSSqlServer64 = @"software\Wow6432Node\Microsoft\MSSQLServer\";
        private static readonly string          s_registryKeyUninstall = @"software\Microsoft\Windows\CurrentVersion\Uninstall\";
        private static readonly string          s_registryKeyUninstall64 = @"software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\";
        private static readonly Regex           s_ssnmpnRegex = new Regex(@"SSNMPN\d+,\s*(.*)", RegexOptions.Compiled);
        private static readonly Regex           s_ssmssoRegex = new Regex(@"SSMSSO\d+,\s*(.*)", RegexOptions.Compiled);
        private static readonly Regex           s_ssmsrpRegex = new Regex(@"SSMSRP\d+,\s*(.*)", RegexOptions.Compiled);
        private static readonly Regex           s_ssmsadRegex = new Regex(@"SSMSAD\d+,\s*(.*)", RegexOptions.Compiled);
        private static readonly Regex           s_ssmsspRegex = new Regex(@"SSMSSP\d+,\s*(.*)", RegexOptions.Compiled);
        private static readonly Regex           s_ssmsviRegex = new Regex(@"SSMSVI\d+,\s*(.*)", RegexOptions.Compiled);

        private static readonly Regex           s_sqlServerRegex = new Regex(@"Microsoft SQL Server",
                                                                             RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex           s_sqlServer2005Regex = new Regex(@"Microsoft SQL Server (2005|2008|2012)",
                                                                                 RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex           s_2005Regex = new Regex(@"2005|2008|2012", RegexOptions.Compiled);

        private static readonly Regex           s_sqlServer2005ExtrasRegex = new Regex("Microsoft SQL Server (2005|2008|2012) \\w+ Services.*", RegexOptions.Compiled);

        private static readonly Regex           s_exclusionRegex = new Regex("(Client|VSS|Books|Express|Light|Compact|Tools|Support|Upgrade|Report|Management|Studio|Driver|Agent|Backward compatibility)",
                                                                             RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex           s_msSqlServerRegex = new Regex(@"Microsoft SQL Server.+\((.+)\).*",
                                                                               RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex           s_editionRegex = new Regex(@"(?<edition>Developer|Enterprise|Standard|Express)",
                                                                           RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex           s_versionRegex = new Regex(@"\d[\d\.]+", RegexOptions.Compiled);
        private static readonly Regex           s_clusterNameRegex = new Regex(@"\\\\\.\\pipe\\\$\$\\(.+)\\sql\\query", RegexOptions.Compiled);
        private static readonly Regex           s_clusterNameWithInstanceRegex = new Regex(@"(.+)MSSQL\$(.+)", RegexOptions.Compiled);
        private static readonly Regex           s_IPRegex = new Regex(@"^\d+\.\d+\.\d+\.\d+", RegexOptions.Compiled);

    }

}
