#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/10/17
*
* Current Status
*       $Revision: 1.34 $
*           $Date: 2014/10/23 07:37:42 $
*         $Author: garyzhou $
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
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script to gather level 2 information for
    /// the Microsoft Office suite.
    /// </summary>
    public class MSOfficeStaticScript : ICollectionScriptRuntime {

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
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script MSOfficeStaticScript.",
                                  m_taskId);
            try {
                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to MSOfficeStaticScript is null.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV2 namespace is not present in connection object.",
                                          m_taskId);
                } else if (!connection.ContainsKey(@"default")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          m_taskId);
                } else {
                    m_defaultScope = connection[@"default"] as ManagementScope;
                    m_cimvScope = connection[@"cimv2"] as ManagementScope;

                    if (!m_cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV2 namespace failed.",
                                              m_taskId);
                    } else if (!m_defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed.",
                                              m_taskId);
                    } else {
                        m_wmiRegistry = new ManagementClass(m_defaultScope, new ManagementPath(@"StdRegProv"), null);

                        using (m_wmiRegistry) {
                            resultCode = GetInstalledDirectoriesAndVersion();
                            if (resultCode == ResultCodes.RC_SUCCESS) {
                                resultCode = GetBaseInstallationDetails(s_registryKeyUninstall);

                                foreach (KeyValuePair<string, string> entry in s_strOfficeFeaturesLookupTable) {

                                    if (string.IsNullOrEmpty(m_appData[@"Office" + entry.Value].Version)) {

                                        resultCode = GetBaseInstallationDetails(s_registryKeyUninstall64);

                                    }

                                }
                            }
                        }

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            string collectedData = PackageOfficeCollectedData();

                            if (attributes == null || !attributes.ContainsKey(@"installedOfficeDetails")) {
                                Lib.Logger.TraceEvent(TraceEventType.Error,
                                                      0,
                                                      "Task Id {0}: Attribute \"installedOfficeDetails\" missing from attributeSet.",
                                                      m_taskId);
                            } else if (string.IsNullOrEmpty(collectedData)) {
                                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                      0,
                                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                                      m_taskId);
                            } else {
                                dataRow.Append(elementId).Append(',')
                                         .Append(attributes[@"installedOfficeDetails"]).Append(',')
                                         .Append(scriptParameters[@"CollectorId"]).Append(',')
                                         .Append(taskId).Append(',')
                                         .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                                         .Append(@"installedOfficeDetails").Append(',')
                                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
                            }

                        }

                    }

                }

            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access Microsoft office exe file property.\nMessage: {1}",
                                      m_taskId,
                                      me.Message);
                if (me.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          me.InnerException.Message);
                }
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
            } catch (COMException ce) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Not enough privilege to access run WMI query.\nMessage: {1}.",
                                      m_taskId,
                                      ce.Message);
                if (ce.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          ce.InnerException.Message);
                }
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;
            } catch (Exception ex) {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in MSOfficeStaticScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script MSOfficeStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion


        /// <summary>
        /// Constructor
        /// </summary>
        public MSOfficeStaticScript() {
            foreach (KeyValuePair<string, string> entry in s_strOfficeAppsExeLookupTable) {

                foreach (KeyValuePair<string, string> entry2 in s_strOfficeFeaturesLookupTable) {
                    OfficeAppInfo oApp = new OfficeAppInfo(entry.Key + entry2.Value, entry.Value);
                    if (s_strOfficeAppsServicePackVersionLookupTable.ContainsKey(entry.Key)) {
                        oApp.ServicePackVersionLookupTable = s_strOfficeAppsServicePackVersionLookupTable[entry.Key];
                    }
                    m_appData[entry.Key + entry2.Value] = oApp;
                }
            }
        }

        /// <summary>
        /// Get potential office keys
        /// </summary>
        /// <param name="subkeys">Return key values</param>
        /// <returns>result code</returns>
        private ResultCodes GetPotentialOfficeRegistryKeys(out string[] subkeys) {
            string[] tempKeys = null, tempKeys64 = null;
            int returnArraySize = 0; subkeys = null;
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            // Collect office information from regular registry branch.
            resultCode = Lib.GetRegistryImmediateSubKeys(m_taskId,
                                                         m_wmiRegistry,
                                                         s_registryKeyOffice,
                                                         out tempKeys);
            if (resultCode == ResultCodes.RC_SUCCESS && tempKeys != null) {
                returnArraySize += tempKeys.Length;
            }

            // Collect office information for machine running 32 bits offices in 64 bits environment.
            Lib.GetRegistryImmediateSubKeys(m_taskId,
                                            m_wmiRegistry,
                                            s_registryKeyOffice64,
                                            out tempKeys64);
            if (resultCode == ResultCodes.RC_SUCCESS && tempKeys64 != null) {
                returnArraySize += tempKeys64.Length;
            }

            // Combine all temporary to final return array.
            if (returnArraySize > 0) {
                subkeys = new string[returnArraySize];
                int index = 0;
                if (tempKeys != null) {
                    foreach (string value in tempKeys) {
                        subkeys[index++] = s_registryKeyOffice + value;
                    }
                }
                if (tempKeys64 != null) {
                    foreach (string value in tempKeys64) {
                        subkeys[index++] = s_registryKeyOffice64 + value;
                    }
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Check to see if filePath is valid, and if it is valid, it will return its file version. 
        /// </summary>
        /// <param name="strDirPath">File Path</param>
        /// <returns>return true if exist; false otherwise.</returns>
        private string RetrieveExeFileVersion(string strDirPath) {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Checking if directory path exists: {1}",
                                  m_taskId,
                                  strDirPath);
            string exeVersion = String.Empty;
            if (!string.IsNullOrEmpty(strDirPath)) {
                try {
                    using (ManagementObject moExeFile = new ManagementObject(@"CIM_DataFile='" + strDirPath + @"'")) {
                        moExeFile.Scope = m_cimvScope;
                        moExeFile.Get();
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: Directory path {1} is valid.",
                                              m_taskId,
                                              strDirPath);

                        //return (string)moExeFile[@"Version"];
                        exeVersion = moExeFile[@"Version"] as string;
                    }
                } catch (ManagementException mex) {
                    StringBuilder props = new StringBuilder();
                    foreach (PropertyData mexProp in mex.ErrorInformation.Properties) {
                        if (mexProp != null && !mexProp.IsArray) {

                            //
                            // Do NOT call ToString on the values because they may
                            // be null. StringBuilder handles null values just fine.
                            // Besides, calling ToString explicitly on the Names or
                            // Values of this collection buys you absolutely *nothing*.
                            // These are already objects, so there is no boxing to
                            // be avoided.
                            props.Append(mexProp.Name).Append(@"=").Append(mexProp.Value).AppendLine();
                        }
                    }
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: File path given does not exist: {1}.\n{2}\n{3}",
                                          m_taskId,
                                          strDirPath,
                                          mex.ToString(),
                                          props.ToString());
                } catch (Exception ex) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: File path given does not exist: {1}.\n{2}",
                                          m_taskId,
                                          strDirPath,
                                          ex.ToString());
                }
            }
            return exeVersion;
        }

        /// <summary>
        /// Collect product name and license key information.
        /// </summary>
        /// <param name="keyPath">Registry Key path</param>
        /// <param name="licenseData">License Data</param>
        /// <returns></returns>
        private ResultCodes collectLicenseInfo(string keyPath) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            try {
                string[] subRegKeys = null;
                resultCode = Lib.GetRegistryImmediateSubKeys(m_taskId,
                                                             m_wmiRegistry,
                                                             keyPath,
                                                             out subRegKeys);
                if (resultCode == ResultCodes.RC_SUCCESS && null != subRegKeys) {
                    foreach (string subRegKey in subRegKeys) {
                        string strProductIDKeyPath = keyPath + @"\" + subRegKey;
                        string productName = string.Empty, productID = string.Empty;
                        byte[] digitalProductID = null;
                        resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                                m_wmiRegistry,
                                                                strProductIDKeyPath,
                                                                @"ProductName",
                                                                out productName);
                        if (resultCode == ResultCodes.RC_SUCCESS && !string.IsNullOrEmpty(productName)) {
                            Lib.GetRegistryBinaryValue(m_taskId,
                                                       m_wmiRegistry,
                                                       strProductIDKeyPath,
                                                       @"DigitalProductId",
                                                       out digitalProductID);
                            if (resultCode == ResultCodes.RC_SUCCESS && digitalProductID != null) {
                                string licenseKey = Lib.ExtractLicenseKeyFromMSDigitalProductID(m_taskId, digitalProductID);
                                if (!licenseData.ContainsKey(productName)) {
                                    licenseData.Add(productName, licenseKey);
                                } else {
                                    if (!string.IsNullOrEmpty(licenseKey)) {
                                        if (!string.IsNullOrEmpty(licenseData[productName])) {
                                            licenseData[productName] += BdnaDelimiters.DELIMITER_TAG;
                                        }
                                        licenseData[productName] += licenseKey;
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to read office license key.\nMessage: {1}",
                                      m_taskId,
                                      me.Message);
                if (me.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          me.InnerException.Message);
                }
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            } catch (COMException ce) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Not enough privilege to access run WMI query.\nMessage: {1}.",
                                      m_taskId,
                                      ce.Message);
                if (ce.InnerException != null) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Inner Exception Message: {1}.",
                                          m_taskId,
                                          ce.InnerException.Message);
                }
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;

            } catch (Exception ex) {
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected exception caught while parsing digital product id {1}.\n",
                                      m_taskId,
                                      ex.ToString());
            }
            return resultCode;
        }

        /// <summary>
        /// Retrieve Install Directory and Version information
        /// </summary>
        /// <returns>result Code</returns>
        private ResultCodes GetInstalledDirectoriesAndVersion() {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string[] subKeys = null;
            bool isAccessRT = IsAccessRunTimeInstalled(s_registryKeyUninstall);
            if (!isAccessRT)
                isAccessRT = IsAccessRunTimeInstalled(s_registryKeyUninstall64);
            string strARTVersion = null;
            if (isAccessRT && !string.IsNullOrEmpty(m_strARTVersion)) {
                string majorVersion = m_strARTVersion.Substring(0, m_strARTVersion.IndexOf('.'));
                strARTVersion = majorVersion + @".0";
            }

            resultCode = this.GetPotentialOfficeRegistryKeys(out subKeys);
            if (resultCode == ResultCodes.RC_SUCCESS && null != subKeys) {
                if (null != subKeys && 0 < subKeys.Length) {

                    // Loop through each subkey and see if it has an application specific key to get an install path
                    foreach (string subKey in subKeys) {
                        string strCommonInstallPath = null;
                        // Save off the common install root.  We'll use it for any application that doesn't have an application
                        // specific installation entry, e.g. HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Office\10.0\Common\InstallRoot
                        string strCommonInstallRootRegistryKeyPath = subKey + @"\Common\InstallRoot";
                        resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                                m_wmiRegistry,
                                                                strCommonInstallRootRegistryKeyPath,
                                                                @"Path",
                                                                out strCommonInstallPath);
                        if (resultCode == ResultCodes.RC_SUCCESS && !String.IsNullOrEmpty(strCommonInstallPath)) {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Considering Microsoft Office at install root: {1}.",
                                                  m_taskId,
                                                  strCommonInstallPath);


                            // Collect all license key from this registry branch.
                            this.collectLicenseInfo(subKey + @"\Registration");

                            // Find all the office application under this common installation root.
                            string[] strOneRootSubKeys = null;
                            resultCode = Lib.GetRegistryImmediateSubKeys(m_taskId,
                                                                          m_wmiRegistry,
                                                                          subKey,
                                                                          out strOneRootSubKeys);
                            ArrayList oAllAppsKeyUnderThisInstallRoot = new ArrayList(strOneRootSubKeys);
                            if (resultCode == ResultCodes.RC_SUCCESS && null != strOneRootSubKeys) {
                                foreach (KeyValuePair<string, string> entry in s_strOfficeAppsExeLookupTable) {
                                    string strAppName = entry.Key;
                                    string strAppExeName = entry.Value;
                                    string strAppInstallPath = String.Empty;
                                    StringBuilder strAppFullExePath = new StringBuilder();

                                    //Check if there is a specific Installation sub-path of this application Path
                                    if (oAllAppsKeyUnderThisInstallRoot.Contains(strAppName)) {
                                        string appInstallRootKeyName = subKey + @"\" + strAppName + @"\InstallRoot";
                                        resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                                                m_wmiRegistry,
                                                                                appInstallRootKeyName,
                                                                                @"Path",
                                                                                out strAppInstallPath);
                                        if (resultCode == ResultCodes.RC_SUCCESS && !String.IsNullOrEmpty(strAppInstallPath)) {
                                            strAppFullExePath.Append(strAppInstallPath.Trim());
                                        }
                                    } else {
                                        strAppInstallPath = strCommonInstallPath;
                                        strAppFullExePath.Append(strCommonInstallPath.Trim());
                                    }
                                    //// add code to validate access rutime uninstall key exist
                                    //// if exist, compare if strCommonInstallPath == strAppInstallPath
                                    //// if != access app exe path = strCommonInstallPath
                                    //// else access exe = null, no access found, exe is for runtime
                                    string subKeyVersinStr = subKey.Substring(subKey.LastIndexOf('\\') + 1);
                                    if (@"Access" == strAppName && isAccessRT) {
                                        if (subKeyVersinStr == strARTVersion) {
                                            if (strAppInstallPath == strCommonInstallPath) {
                                                strAppInstallPath = null;
                                            } else {
                                                strAppFullExePath.Remove(0, strAppInstallPath.Length);
                                                strAppInstallPath = strCommonInstallPath;
                                                strAppFullExePath.Append(strCommonInstallPath.Trim());
                                            }
                                        }
                                    }

                                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                          0,
                                                          "Task Id {0}: Attempting to validate application {1} at {2}.",
                                                          m_taskId,
                                                          strAppName,
                                                          strAppInstallPath);
                                    if (!String.IsNullOrEmpty(strAppInstallPath)) {

                                        string featureVersion = String.Empty;
                                        if (!strAppInstallPath.EndsWith(@"\")) {
                                            strAppFullExePath.Append(@"\");
                                        }
                                        strAppFullExePath.Append(strAppExeName);
                                        string strAppVersion = RetrieveExeFileVersion(strAppFullExePath.ToString());
                                        if (!string.IsNullOrEmpty(strAppVersion)) {
                                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                                  0,
                                                                  "Task Id {0}: File validation of {1} successful.",
                                                                  m_taskId,
                                                                  strAppName);
                                            if (!string.IsNullOrEmpty(strAppVersion)) {
                                                string majorVersion =

                                                    strAppVersion.Substring(0, strAppVersion.IndexOf('.'));
                                                if (s_strOfficeFeaturesLookupTable.ContainsKey(majorVersion)) {

                                                    featureVersion = s_strOfficeFeaturesLookupTable[majorVersion];

                                                    m_appData[strAppName + featureVersion].FeatureVersion = featureVersion;
                                                }
                                            }

                                            m_appData[strAppName + featureVersion].Version = strAppVersion;

                                            m_appData[strAppName + featureVersion].InstallDirectory = strAppInstallPath;



                                            if (string.IsNullOrEmpty(m_appData[@"Office" + featureVersion].InstallDirectory)) {

                                                m_appData[@"Office" + featureVersion].InstallDirectory = strAppInstallPath;
                                            }

                                            foreach (KeyValuePair<string, string> kvp in licenseData) {
                                                string name = kvp.Key, key = kvp.Value;
                                                if (!string.IsNullOrEmpty(name)) {
                                                    if (s_officeSuiteRegex.IsMatch(name) &&
                                                        !s_excludedAppRegex.IsMatch(name) &&
                                                        !s_servicePackRegex.IsMatch(name) &&
                                                        string.IsNullOrEmpty(m_appData[@"Office" + featureVersion].LicenseKey)) {
                                                        m_appData[@"Office" + featureVersion].LicenseKey = key;
                                                    } else if (name.ToUpper().Contains(strAppName.ToUpper())) {

                                                        m_appData[strAppName + featureVersion].LicenseKey = key;
                                                    }
                                                }
                                            }

                                        }
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
        /// Verify if "Access Runtime" exist in Unistall
        /// </summary>
        /// <returns></returns>
        private Boolean IsAccessRunTimeInstalled(string uninstallKeyRoot) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string[] strAddRemoveKeys = null;

            resultCode = Lib.GetRegistryImmediateSubKeys(m_taskId,
                                                         m_wmiRegistry,
                                                         uninstallKeyRoot,
                                                         out strAddRemoveKeys);
            if (resultCode == ResultCodes.RC_SUCCESS && strAddRemoveKeys != null) {
                foreach (string strSubKeyLabel in strAddRemoveKeys) {
                    string displayName = String.Empty;
                    string strUninstallKeyOneApp = uninstallKeyRoot + strSubKeyLabel;
                    resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                            m_wmiRegistry,
                                                            strUninstallKeyOneApp,
                                                            @"DisplayName",
                                                            out displayName);
                    if (string.IsNullOrEmpty(displayName)) {
                        displayName = strSubKeyLabel;
                    }

                    if (String.IsNullOrEmpty(displayName))
                        continue;
                    Match matchAccessRt = s_accessRunTimeRegex.Match(displayName);
                    if (matchAccessRt.Success) {
                        String strTemp = String.Empty;
                        resultCode = Lib.GetRegistryStringValue(m_taskId,
                                        m_wmiRegistry,
                                        strUninstallKeyOneApp,
                                        @"DisplayVersion",
                                        out strTemp);
                        if (!string.IsNullOrEmpty(strTemp))
                            m_strARTVersion = strTemp;
                        return matchAccessRt.Success;
                    }
                }
            }
            return false;
        }


        /// <summary>
        /// Get Base Microsoft Office Installation Information
        /// </summary>
        /// <returns></returns>
        private ResultCodes GetBaseInstallationDetails(string uninstallKeyRoot) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            string[] strAddRemoveKeys = null;

            resultCode = Lib.GetRegistryImmediateSubKeys(m_taskId,
                                                         m_wmiRegistry,
                                                         uninstallKeyRoot,
                                                         out strAddRemoveKeys);
            if (resultCode == ResultCodes.RC_SUCCESS && strAddRemoveKeys != null) {
                foreach (string strSubKeyLabel in strAddRemoveKeys) {





                    string displayVersion = String.Empty;

                    string featureVersion = String.Empty;
                    string strUninstallKeyOneApp = uninstallKeyRoot + strSubKeyLabel;

                    resultCode = Lib.GetRegistryStringValue(m_taskId,

                                                            m_wmiRegistry,

                                                            strUninstallKeyOneApp,

                                                            @"DisplayVersion",

                                                            out displayVersion);

                    if (string.IsNullOrEmpty(displayVersion))

                        continue;

                    if (!displayVersion.Contains(@"."))

                        continue;

                    string majorVersion = displayVersion.Substring(0, displayVersion.IndexOf('.'));

                    if (s_strOfficeFeaturesLookupTable.ContainsKey(majorVersion)) {

                        featureVersion = s_strOfficeFeaturesLookupTable[majorVersion];

                    }


                    string displayName = String.Empty;
                    resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                            m_wmiRegistry,
                                                            strUninstallKeyOneApp,
                                                            @"DisplayName",
                                                            out displayName);
                    if (string.IsNullOrEmpty(displayName)) {
                        displayName = strSubKeyLabel;
                    }

                    if (String.IsNullOrEmpty(displayName))
                        continue;
                    Match matchOffice = s_officeSuiteRegex.Match(displayName);
                    Match matchFrontPage = s_frontPageRegex.Match(displayName);
                    if (!matchOffice.Success && !matchFrontPage.Success)
                        continue;

                    bool bIsFrontPage = matchFrontPage.Success;
                    string strEdition = "";
                    if (!bIsFrontPage) {
                        strEdition = matchOffice.Groups[@"edition"].Value;
                    }

                    if (s_excludedAppRegex.IsMatch(displayName) && !bIsFrontPage)
                        continue;
                    bool bIncludeFrontPage = s_frontPageSimpleRegex.IsMatch(displayName);
                    if (!bIsFrontPage) {

                        m_appData[@"Office" + featureVersion].Name = displayName;

                        m_appData[@"Office" + featureVersion].Edition = strEdition + @" Edition";
                    } else {

                        m_appData[@"FrontPage" + featureVersion].Edition = strEdition + @" Edition";
                    }

                    if (!bIsFrontPage) {
                        Match oFeatureMatch = s_featureRegex.Match(displayName);
                        if (oFeatureMatch.Success) {
                            string strFeature = oFeatureMatch.Groups[@"feature"].Value as string;
                            foreach (OfficeAppInfo app in m_appData.Values) {
                                if (!app.Name.Contains(@"Office") && string.IsNullOrEmpty(app.FeatureVersion)) {
                                    app.FeatureVersion = strFeature;
                                }
                            }
                        }
                        //m_appData[@"Office"].Name = @"Microsoft Office " + strFeature;

                    }



                    if (bIsFrontPage) {

                        m_appData[@"FrontPage" + featureVersion].Version = displayVersion;

                    } else {

                        m_appData[@"Office" + featureVersion].Version = displayVersion;

                        if (bIncludeFrontPage) {

                            if (String.IsNullOrEmpty(m_appData[@"FrontPage" + featureVersion].Version)) {

                                m_appData[@"FrontPage" + featureVersion].Version = displayVersion;
                            }
                        }
                    }

                    string strTemp = string.Empty;
                    resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                            m_wmiRegistry,
                                                            strUninstallKeyOneApp,
                                                            @"InstallLocation",
                                                            out strTemp);
                    if (bIsFrontPage) {

                        m_appData[@"FrontPage" + featureVersion].InstallDirectory = strTemp;
                    } else {
                        if (strTemp == null) {
                            strTemp = "";
                        } else if (strTemp.Equals(@"InstallLocation", StringComparison.CurrentCultureIgnoreCase)) {
                            strTemp = "";
                        }

                        m_appData[@"Office" + featureVersion].InstallDirectory = strTemp;

                        if (bIncludeFrontPage) {

                            if (String.IsNullOrEmpty(m_appData[@"FrontPage" + featureVersion].InstallDirectory)) {

                                m_appData[@"FrontPage" + featureVersion].InstallDirectory = strTemp;
                            }
                        }





                        if (!String.IsNullOrEmpty(m_appData[@"Office" + featureVersion].InstallDirectory)) {
                            foreach (OfficeAppInfo app in m_appData.Values) {
                                if (String.IsNullOrEmpty(app.InstallDirectory) && !string.IsNullOrEmpty(app.Version)) {

                                    app.InstallDirectory = m_appData[@"Office" + featureVersion].InstallDirectory;
                                }
                            }
                        }
                    }

                    strTemp = string.Empty;
                    resultCode = Lib.GetRegistryStringValue(m_taskId,
                                                            m_wmiRegistry,
                                                            strUninstallKeyOneApp,
                                                            @"InstallDate",
                                                            out strTemp);
                    if (bIsFrontPage) {

                        m_appData[@"FrontPage" + featureVersion].InstallDate = strTemp;
                    } else {

                        m_appData[@"Office" + featureVersion].InstallDate = strTemp;

                        if (bIncludeFrontPage) {

                            if (String.IsNullOrEmpty(m_appData[@"FrontPage" + featureVersion].InstallDate)) {

                                m_appData[@"FrontPage" + featureVersion].InstallDate = strTemp;
                            }
                        }
                    }


                    string servicePack = string.Empty;

                    if (0 <= displayName.IndexOf(@"SR-1") || 0 <= displayName.IndexOf(@"SR1")) {
                        servicePack = @"Service Release 1 (SR-1)";
                    } else if (0 <= displayName.IndexOf(@"SR-2") || 0 <= displayName.IndexOf(@"SR2")) {
                        servicePack = @"Service Release 2 (SR-2)";
                    } else if (0 <= displayName.IndexOf(@"SR-3") || 0 <= displayName.IndexOf(@"SR3")) {
                        servicePack = @"Service Release 3 (SR-3)";
                    } else if (0 <= displayName.IndexOf(@"SR-4") || 0 <= displayName.IndexOf(@"SR4")) {
                        servicePack = @"Service Release 4 (SR-4)";
                    } else if (0 <= displayName.IndexOf(@"SP-1") || 0 <= displayName.IndexOf(@"SP1")) {
                        servicePack = @"Service Pack 1 (SP1)";
                    } else if (0 <= displayName.IndexOf(@"SP-2") || 0 <= displayName.IndexOf(@"SP2")) {
                        servicePack = @"Service Pack 2 (SP2)";
                    } else if (0 <= displayName.IndexOf(@"SP-3") || 0 <= displayName.IndexOf(@"SP3")) {
                        servicePack = @"Service Pack 3 (SP3)";
                    } else if (0 <= displayName.IndexOf(@"SP-4") || 0 <= displayName.IndexOf(@"SP4")) {
                        servicePack = @"Service Pack 4 (SP4)";
                    }

                    if (string.IsNullOrEmpty(servicePack))
                        servicePack = this.GetServicePackInstalled();
                    if (bIsFrontPage) {

                        m_appData[@"FrontPage" + featureVersion].ServicePackName = servicePack;
                    } else {

                        m_appData[@"Office" + featureVersion].ServicePackName = servicePack;
                    }
                }
            }
            return resultCode;
        }

        private string GetServicePackInstalled() {
            foreach (KeyValuePair<string, OfficeAppInfo> entry in m_appData) {
                Match match = s_servicePackRegex.Match(entry.Key);
                if (match.Success && !string.IsNullOrEmpty(entry.Value.ServicePackName)) {
                    return entry.Value.ServicePackName;
                }
            }
            return string.Empty;
        }



        /// <summary>
        /// Package output data.
        /// </summary>
        /// <returns>value</returns>
        private string PackageOfficeCollectedData() {
            StringBuilder strResults = new StringBuilder();
            foreach (KeyValuePair<string, OfficeAppInfo> kvp in m_appData) {
                OfficeAppInfo app = kvp.Value;
                if (string.IsNullOrEmpty(app.Version))
                    continue;

                strResults.Append(BdnaDelimiters.DELIMITER_TAG);
                string elementName = @"MS" + kvp.Key;
                strResults.Append(@"elementName=""").Append(@"MS").Append(kvp.Key).Append(@"""");
                strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"name=""").Append(app.Name).Append(@"""");
                strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"version=""").Append(app.Version).Append(@"""");

                if (!String.IsNullOrEmpty(app.Edition)) {
                    strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"edition=""").Append(app.Edition).Append(@"""");
                }
                if (!String.IsNullOrEmpty(app.InstallDirectory)) {
                    strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"installDirectory=""").Append(app.InstallDirectory).Append(@"""");
                }
                if (!String.IsNullOrEmpty(app.InstallDate)) {
                    strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"installDate=""").Append(app.InstallDate).Append(@"""");
                }
                if (!String.IsNullOrEmpty(app.ServicePackName)) {
                    strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"servicePack=""").Append(app.ServicePackName).Append(@"""");
                }
                if (!String.IsNullOrEmpty(app.LicenseKey)) {
                    strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"licenseKey=""").Append(app.LicenseKey).Append(@"""");
                }
                if (kvp.Key == @"Office") {
                    StringBuilder keyBuffer = new StringBuilder();
                    foreach (KeyValuePair<string, string> licesneRecord in licenseData) {
                        string name = licesneRecord.Key, key = licesneRecord.Value;
                        if (keyBuffer.Length > 0) {
                            keyBuffer.Append(BdnaDelimiters.DELIMITER1_TAG);
                        }
                        keyBuffer.Append(name).Append(BdnaDelimiters.DELIMITER2_TAG).Append(key);
                    }
                    strResults.Append(BdnaDelimiters.DELIMITER2_TAG).Append(@"licenseTable=""").Append(keyBuffer.ToString()).Append(@"""");
                }
            }
            return strResults.ToString();
        }



        /// <summary>
        /// Private class to store one office application information.
        /// </summary>
        private class OfficeAppInfo {
            public OfficeAppInfo(string strAppName, string strExePath) {
                m_strAppExePath = strExePath;
                m_strAppName = strAppName;
            }

            public IDictionary<string, string> ServicePackVersionLookupTable {
                get { return m_strAppServicePackLookupTable; }
                set { m_strAppServicePackLookupTable = value; }
            }

            public string ExePath {
                get { return m_strAppExePath; }
                set { if (!String.IsNullOrEmpty(value)) m_strAppExePath = value; }
            }

            public string strFullExePath {
                get { return m_strAppInstallDirectory + m_strAppExePath; }
            }

            public string EscapedFullExePath {
                get { return m_strAppInstallDirectory.Replace(@"\", @"\\") + m_strAppExePath; }
            }

            public string InstallDirectory {
                get { return m_strAppInstallDirectory; }
                set { if (!String.IsNullOrEmpty(value)) m_strAppInstallDirectory = value; }
            }

            public string Version {
                get { return m_strAppVersion; }
                set { if (!String.IsNullOrEmpty(value)) m_strAppVersion = value; }
            }

            public string Edition {
                get { return m_strAppEdition; }
                set { if (!String.IsNullOrEmpty(value)) m_strAppEdition = value; }
            }

            public string FeatureVersion {
                get { return m_strFeatureVersion; }
                set { if (!String.IsNullOrEmpty(value)) m_strFeatureVersion = value; }
            }

            public string LicenseKey {
                get { return m_strLicenseKey; }
                set { if (!String.IsNullOrEmpty(value)) m_strLicenseKey = value; }
            }

            public string Name {
                get {
                    if (!string.IsNullOrEmpty(m_strAppName)) {

                        if (!string.IsNullOrEmpty(m_strFeatureVersion) && m_strAppName.EndsWith(m_strFeatureVersion)) {

                            m_strAppName = m_strAppName.Substring(0, m_strAppName.LastIndexOf(m_strFeatureVersion));

                        }
                        if (m_strAppName.Contains(@"Microsoft")) {
                            return m_strAppName;
                        } else {
                            if (!string.IsNullOrEmpty(m_strFeatureVersion)) {
                                return @"Microsoft " + m_strAppName + @" " + m_strFeatureVersion;
                            } else {
                                return @"Microsoft " + m_strAppName;
                            }
                        }
                    }
                    return string.Empty;
                }

                set { if (!String.IsNullOrEmpty(value)) m_strAppName = value; }
            }

            public string elementName {
                get {
                    if (!string.IsNullOrEmpty(m_strAppName)) {
                        return m_strAppName.Replace(@" ", @"_").Replace(@".", @"_").Replace(@"\", @"_").Replace(@"/", @"_");
                    }
                    return m_strAppName;

                }
                set { if (!String.IsNullOrEmpty(value)) m_strAppName = value; }
            }

            public string InstallDate {
                get { return m_strAppInstallDate; }
                set {
                    if (!String.IsNullOrEmpty(value)) {
                        Match match = s_installDateRegex.Match(value);
                        if (match.Success && value.Length == 6) {
                            m_strAppInstallDate = value.Substring(0, 4) + "/" + value.Substring(4, 2) + "/" + value.Substring(6, 2);
                        }
                        m_strAppInstallDate = value;
                    }
                }
            }

            public string ServicePackName {
                get {
                    if (!String.IsNullOrEmpty(m_strAppServicePack))
                        return m_strAppServicePack;
                    if (!String.IsNullOrEmpty(m_strAppVersion) && m_strAppServicePackLookupTable != null) {
                        if (m_strAppServicePackLookupTable.Keys.Contains(m_strAppVersion)) {
                            return m_strAppServicePackLookupTable[m_strAppVersion];
                        }
                    }
                    return string.Empty;
                }
                set { m_strAppServicePack = value; }
            }

            private string m_strFeatureVersion = String.Empty;
            private string m_strAppExePath = String.Empty;
            private string m_strAppName = String.Empty;
            private string m_strAppInstallDirectory = String.Empty;
            private string m_strAppVersion = String.Empty;
            private string m_strAppInstallDate = String.Empty;
            private string m_strAppServicePack = String.Empty;
            private string m_strAppEdition = String.Empty;
            private string m_strContainingApps = String.Empty;
            private string m_strLicenseKey = String.Empty;
            private IDictionary<string, string> m_strAppServicePackLookupTable = null;
        }


        /// <summary> Data row property </summary>
        public StringBuilder dataRow {
            get { return m_dataRow; }
        }

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;

        /// <summary>Access Runtime Version</summary>
        private string m_strARTVersion;

        private IDictionary<string, OfficeAppInfo> m_appData = new Dictionary<string, OfficeAppInfo>();
        private ManagementScope m_cimvScope = null;
        private ManagementScope m_defaultScope = null;
        private ManagementClass m_wmiRegistry;

        /// <summary> License Keys </summary>
        private IDictionary<string, string> licenseData = new Dictionary<string, string>();

        private static IDictionary<string, string> s_strOfficeAppsExeLookupTable = new Dictionary<string, string>();
        private static IDictionary<string, string> s_strOfficeFeaturesLookupTable = new Dictionary<string, string>();
        private static IDictionary<string, IDictionary<string, string>> s_strOfficeAppsServicePackVersionLookupTable = new Dictionary<string, IDictionary<string, string>>();
        /// <summary>
        /// Static initializer to build up the application
        /// version table.
        /// </summary>
        static MSOfficeStaticScript() {
            // Build lookup table for office application and their binary names.
            s_strOfficeAppsExeLookupTable[@"Office"] = @"Mso.dll";
            s_strOfficeAppsExeLookupTable[@"Word"] = @"Winword.exe";
            s_strOfficeAppsExeLookupTable[@"Excel"] = @"Excel.exe";
            s_strOfficeAppsExeLookupTable[@"PowerPoint"] = @"POWERPNT.exe";
            s_strOfficeAppsExeLookupTable[@"Outlook"] = @"OUTLOOK.exe";
            s_strOfficeAppsExeLookupTable[@"Access"] = @"MSAccess.exe";
            s_strOfficeAppsExeLookupTable[@"Publisher"] = @"MSPUB.exe";
            s_strOfficeAppsExeLookupTable[@"FrontPage"] = @"FRONTPG.exe";
            s_strOfficeAppsExeLookupTable[@"OneNote"] = @"ONENOTE.exe";
            s_strOfficeAppsExeLookupTable[@"InfoPath"] = @"INFOPATH.exe";
            s_strOfficeAppsExeLookupTable[@"Groove"] = @"GROOVE.EXE";

            // Build lookup table for Office features.
            s_strOfficeFeaturesLookupTable[@"9"] = @"2000";
            s_strOfficeFeaturesLookupTable[@"10"] = @"XP";
            s_strOfficeFeaturesLookupTable[@"11"] = @"2003";
            s_strOfficeFeaturesLookupTable[@"12"] = @"2007";
            s_strOfficeFeaturesLookupTable[@"14"] = @"2010";
            s_strOfficeFeaturesLookupTable[@"15"] = @"2013";

            // Build lookup table for Office Service Pack version lookup table.
            Dictionary<string, string> MSOfficeServicePackVersionTable = new Dictionary<string, string>();
            MSOfficeServicePackVersionTable[@"9.0.3821"] = @"Service Release 1 (SR1)";
            MSOfficeServicePackVersionTable[@"9.0.4402"] = @"Service Pack 2 (SP2)";
            MSOfficeServicePackVersionTable[@"9.0.6926"] = @"Service Pack 3 (SP3)";
            MSOfficeServicePackVersionTable[@"10.3416.3501"] = @"Service Pack 1 (SP1)";
            MSOfficeServicePackVersionTable[@"10.4219.4219"] = @"Service Pack 2 (SP2)";
            MSOfficeServicePackVersionTable[@"10.0.6612"] = @"Service Pack 3 (SP3)";
            MSOfficeServicePackVersionTable[@"11.0.6361"] = @"Service Pack 1 (SP1)";
            MSOfficeServicePackVersionTable[@"11.0.6568"] = @"Service Pack 2 (SP2)";
            MSOfficeServicePackVersionTable[@"12.0.6213"] = @"Service Pack 1 (SP1)";

            Dictionary<string, string> MSWordServicePackVersionTable = new Dictionary<string, string>();
            MSWordServicePackVersionTable[@"9.0.3821"] = @"Service Release 1 (SR1)";
            MSWordServicePackVersionTable[@"9.0.4402"] = @"Service Pack 2 (SP2)";
            MSWordServicePackVersionTable[@"9.0.6926"] = @"Service Pack 3 (SP3)";
            MSWordServicePackVersionTable[@"10.3416.3501"] = @"Service Pack 1 (SP1)";
            MSWordServicePackVersionTable[@"10.4219.4219"] = @"Service Pack 2 (SP2)";
            MSWordServicePackVersionTable[@"10.0.6612"] = @"Service Pack 3 (SP3)";
            MSWordServicePackVersionTable[@"11.0.6359"] = @"Service Pack 1 (SP1)";
            MSWordServicePackVersionTable[@"11.0.6568"] = @"Service Pack 2 (SP2)";
            MSWordServicePackVersionTable[@"12.0.6211"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"Word"] = MSWordServicePackVersionTable;

            Dictionary<string, string> MSExcelServicePackVersionTable = new Dictionary<string, string>();
            MSExcelServicePackVersionTable[@"9.0.3821"] = @"Service Release 1 (SR1)";
            MSExcelServicePackVersionTable[@"9.0.4402"] = @"Service Pack 2 (SP2)";
            MSExcelServicePackVersionTable[@"9.0.6926"] = @"Service Pack 3 (SP3)";
            MSExcelServicePackVersionTable[@"10.3416.3501"] = @"Service Pack 1 (SP1)";
            MSExcelServicePackVersionTable[@"10.4219.4219"] = @"Service Pack 2 (SP2)";
            MSExcelServicePackVersionTable[@"10.0.6501"] = @"Service Pack 3 (SP3)";
            MSExcelServicePackVersionTable[@"11.0.6355"] = @"Service Pack 1 (SP1)";
            MSExcelServicePackVersionTable[@"11.0.6560"] = @"Service Pack 2 (SP2)";
            MSExcelServicePackVersionTable[@"12.0.6214"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"Excel"] = MSExcelServicePackVersionTable;

            Dictionary<string, string> MSPowerpointServicePackVersionTable = new Dictionary<string, string>();
            MSPowerpointServicePackVersionTable[@"9.0.3821"] = @"Service Release 1 (SR1)";
            MSPowerpointServicePackVersionTable[@"9.0.4527"] = @"Service Pack 2 (SP2)";
            MSPowerpointServicePackVersionTable[@"9.0.6620"] = @"Service Pack 3 (SP3)";
            MSPowerpointServicePackVersionTable[@"10.3506.3501"] = @"Service Pack 1 (SP1)";
            MSPowerpointServicePackVersionTable[@"10.4205.4219"] = @"Service Pack 2 (SP2)";
            MSPowerpointServicePackVersionTable[@"10.0.6501"] = @"Service Pack 3 (SP3)";
            MSPowerpointServicePackVersionTable[@"11.0.6361"] = @"Service Pack 1 (SP1)";
            MSPowerpointServicePackVersionTable[@"11.0.6564"] = @"Service Pack 2 (SP2)";
            MSPowerpointServicePackVersionTable[@"12.0.6211"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"PowerPoint"] = MSPowerpointServicePackVersionTable;

            Dictionary<string, string> MSOutlookServicePackVersionTable = new Dictionary<string, string>();
            MSOutlookServicePackVersionTable[@"9.0.3821"] = @"Service Release 1 (SR1)";
            MSOutlookServicePackVersionTable[@"9.0.4527"] = @"Service Pack 2 (SP2)";
            MSOutlookServicePackVersionTable[@"9.0.6620"] = @"Service Pack 3 (SP3)";
            MSOutlookServicePackVersionTable[@"10.3506.3501"] = @"Service Pack 1 (SP1)";
            MSOutlookServicePackVersionTable[@"10.4205.4219"] = @"Service Pack 2 (SP2)";
            MSOutlookServicePackVersionTable[@"10.0.6501"] = @"Service Pack 3 (SP3)";
            MSOutlookServicePackVersionTable[@"11.0.6353"] = @"Service Pack 1 (SP1)";
            MSOutlookServicePackVersionTable[@"11.0.6565"] = @"Service Pack 2 (SP2)";
            MSOutlookServicePackVersionTable[@"12.0.6212"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"Outlook"] = MSOutlookServicePackVersionTable;

            Dictionary<string, string> MSAccessServicePackVersionTable = new Dictionary<string, string>();
            MSAccessServicePackVersionTable[@"9.0.0.3821"] = @"Service Release 1 (SR1)";
            MSAccessServicePackVersionTable[@"9.0.0.4527"] = @"Service Pack 2 (SP2)";
            MSAccessServicePackVersionTable[@"9.0.0.6627"] = @"Service Pack 3 (SP3)";
            MSAccessServicePackVersionTable[@"10.3513.3501"] = @"Service Pack 1 (SP1)";
            MSAccessServicePackVersionTable[@"10.4219.4219"] = @"Service Pack 2 (SP2)";
            MSAccessServicePackVersionTable[@"10.0.6626"] = @"Service Pack 3 (SP3)";
            MSAccessServicePackVersionTable[@"11.0.6355"] = @"Service Pack 1 (SP1)";
            MSAccessServicePackVersionTable[@"11.0.6566"] = @"Service Pack 2 (SP2)";
            MSAccessServicePackVersionTable[@"12.0.6211"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"Access"] = MSAccessServicePackVersionTable;

            Dictionary<string, string> MSFrontPageServicePackVersionTable = new Dictionary<string, string>();
            MSFrontPageServicePackVersionTable[@"11.0.6356"] = @"Service Pack 1 (SP1)";
            MSFrontPageServicePackVersionTable[@"11.0.6552"] = @"Service Pack 2 (SP2)";
            MSFrontPageServicePackVersionTable[@"12.0.6213"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"FrontPage"] = MSFrontPageServicePackVersionTable;

            Dictionary<string, string> MSInfoPathServicePackVersionTable = new Dictionary<string, string>();
            MSInfoPathServicePackVersionTable[@"11.0.6357"] = @"Service Pack 1 (SP1)";
            MSInfoPathServicePackVersionTable[@"11.0.6565"] = @"Service Pack 2 (SP2)";
            MSInfoPathServicePackVersionTable[@"12.0.6214"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"InfoPath"] = MSInfoPathServicePackVersionTable;

            Dictionary<string, string> MSPublisherServicePackVersionTable = new Dictionary<string, string>();
            MSPublisherServicePackVersionTable[@"11.0.6255"] = @"Service Pack 1 (SP1)";
            MSPublisherServicePackVersionTable[@"11.0.6565"] = @"Service Pack 2 (SP2)";
            MSPublisherServicePackVersionTable[@"12.0.6211"] = @"Service Pack 1 (SP1)";
            s_strOfficeAppsServicePackVersionLookupTable[@"Publisher"] = MSPublisherServicePackVersionTable;
        }

        private static readonly string s_registryKeyOffice = @"SOFTWARE\Microsoft\Office\";
        private static readonly string s_registryKeyOffice64 = @"SOFTWARE\Wow6432Node\Microsoft\Office\";

        private static readonly string s_registryKeyUninstall = @"software\Microsoft\Windows\CurrentVersion\Uninstall\";
        private static readonly string s_registryKeyUninstall64 = @"software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\";

        private static readonly Regex s_officeSuiteRegex = new Regex(
            @"Microsoft Office .*(?<edition>Professional|Professional\sPlus|Basic|Home\sStudent|Academic|Standard|Enterprise|Premium|Developer|Small\sBusiness|Ultimate)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_frontPageRegex = new Regex(@"Microsoft[^h]* FrontPage.*",
                                                                   RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_accessRunTimeRegex = new Regex(@"Microsoft.*Access.*Runtime",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_excludedAppRegex = new Regex(@"(Visual Studio|Visual|Microsoft[^h]* FrontPage.*|Access|OneNote|Project|Visio|Viewer|Groove|Components)",
                                                                     RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_frontPageSimpleRegex = new Regex(@"frontpage", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_featureRegex = new Regex(@"(?<feature>[\d]+|XP)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex s_servicePackRegex = new Regex(@"(Word|Excel|PowerPoint|Outlook)", RegexOptions.Compiled);
        private static readonly Regex s_installDateRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
    }
}


