#region Copyright
/******************************************************************
*
*          Module: FSecure AntiVirus Details Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/10/17
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
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script to gather virus definition version for F-Secure Anti Virus.
    /// </summary>
    public class FSecureAVDetailsScript : ICollectionScriptRuntime {
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
            StringBuilder collectedData = new StringBuilder();

            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script FSecureAVDetailsScript.",
                                  m_taskId);
            try {
                if (null == scriptParameters) {
                    resultCode = ResultCodes.RC_NULL_PARAMETER_SET;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Parameter set object passed to FSecureAVDetailsScript is null.",
                                          m_taskId);
                } else if (!scriptParameters.ContainsKey(@"installDirectory") ||
                    string.IsNullOrEmpty(scriptParameters[@"installDirectory"])) {
                    resultCode = ResultCodes.RC_NULL_PARAMETER_SET_NAME;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Parameter installDirectory object passed to FSecureAVDetailsScript is null.",
                                          m_taskId);
                } else if (null == attributes) {
                    resultCode = ResultCodes.RC_NULL_ATTRIBUTE_SET;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attribute object passed to FSecureAVDetailsScript is null.",
                                          m_taskId);
                } else if (!attributes.ContainsKey(@"avDefDetails")) {
                    resultCode = ResultCodes.RC_NULL_ATTRIBUTE_SET;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attribute object passed to FSecureAVDetailsScript is null.",
                                          m_taskId);

                } else if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to FSecureAVDetailsScript is null.",
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
                        // Get AV Definition directories
                        ManagementClass wmiRegistry = new ManagementClass(m_defaultScope, new ManagementPath(@"StdRegProv"), null);
                        IList<string> repositoryDirectories = null;
                        using (wmiRegistry) {
                            repositoryDirectories = GetVirusDefinitionPath(wmiRegistry, scriptParameters[@"installDirectory"]);
                            //resultCode = GetInstalledDirectoriesAndVersion();
                            //if (resultCode == ResultCodes.RC_SUCCESS) {
                            //    resultCode = GetBaseInstallationDetails();
                            //}
                        }
                        foreach (string avDefPath in repositoryDirectories) {
                            IList<string> AV_database = Lib.RetrieveSubDirectories(m_taskId, m_cimvScope, avDefPath.ToString());
                            foreach (string avRepository in AV_database) {
                                string avRepositoryPath = avDefPath + @"\" + avRepository;
                                string virusDefinitionInfo = GetAntiVirusRepositoryDetails(avRepositoryPath);
                                if (!string.IsNullOrEmpty(virusDefinitionInfo)) {
                                    if (collectedData.Length > 0) {
                                        collectedData.Append(BdnaDelimiters.DELIMITER1_TAG);
                                    }
                                    collectedData.Append(virusDefinitionInfo);
                                }
                            }
                        }

                        if (collectedData.Length <= 0) {
                            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                                  0,
                                                  "Task Id {0}: Script completed sucessfully with no data to return.",
                                                  m_taskId);
                        } else {
                            dataRow.Append(elementId).Append(',')
                                     .Append(attributes[@"avDefDetails"]).Append(',')
                                     .Append(scriptParameters[@"CollectorId"]).Append(',')
                                     .Append(taskId).Append(',')
                                     .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds).Append(',')
                                     .Append(@"avDefDetails").Append(',')
                                     .Append(BdnaDelimiters.BEGIN_TAG)
                                     .Append(collectedData.ToString())
                                     .Append(BdnaDelimiters.END_TAG);
                            resultCode = ResultCodes.RC_SUCCESS;
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access av file property.\nMessage: {1}",
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
                                 "Unhandled exception in FSecureAVDetailsScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script FSecureAVDetailsScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion

        /// <summary>
        /// Get path to virus definition path.
        /// </summary>
        /// <param name="wmiRegistry">WMI registry</param>
        /// <param name="installDir">Install Directory of F-Secure Anti-Virus.</param>
        /// <returns>Paths to virus definition</returns>
        private IList<string> GetVirusDefinitionPath(ManagementClass wmiRegistry, string installDir) {
            List<string> virusDefDatabase = new List<string>();
            try {
                if (!string.IsNullOrEmpty(installDir) && installDir.Contains(@"\")) {
                    int lastIndex = installDir.LastIndexOf(@"\");
                    int length = installDir.Length - lastIndex;

                    string avDatabasePath = string.Empty;
                    if (length > 0 && lastIndex > 0) {
                        avDatabasePath = installDir.Substring(0, lastIndex) + @"\FSAUA\Content";
                    }

                    if (Lib.ValidateDirectory(m_taskId, avDatabasePath, m_cimvScope)) {
                        virusDefDatabase.Add(avDatabasePath);
                    }
                }

                if (virusDefDatabase.Count <= 0) {
                    // If directory path is invalid, search for data directory from automatic update agent.
                    string[] strAddRemoveKeys = null;
                    ResultCodes resultCode =
                        this.GetImmediateRegistrySubKeys(wmiRegistry, s_registryKeyUninstall, out strAddRemoveKeys);
                    if (resultCode == ResultCodes.RC_SUCCESS && strAddRemoveKeys != null) {
                        foreach (string strSubKeyLabel in strAddRemoveKeys) {
                            string displayName = String.Empty;
                            string strUninstallKeyOneApp = s_registryKeyUninstall + strSubKeyLabel;
                            resultCode = this.GetRegistryStringValue
                                (wmiRegistry, strUninstallKeyOneApp, @"DisplayName", out displayName);
                            if (string.IsNullOrEmpty(displayName)) {
                                displayName = strSubKeyLabel;
                            }

                            if (String.IsNullOrEmpty(displayName)) continue;
                            Match matchFSecureAutoUpdateAgent = s_FSecureAutoUpdateAgentRegex.Match(displayName);
                            if (!matchFSecureAutoUpdateAgent.Success) continue;


                            // read uninstall string and install Directory
                            string strAgentDir = string.Empty;
                            resultCode = this.GetRegistryStringValue
                                (wmiRegistry, strUninstallKeyOneApp, @"InstallLocation", out strAgentDir);
                            if (resultCode != ResultCodes.RC_SUCCESS || string.IsNullOrEmpty(strAgentDir)) {
                                string uninstallString = string.Empty;
                                resultCode = GetRegistryStringValue
                                    (wmiRegistry, strUninstallKeyOneApp, @"UninstallString", out uninstallString);
                                if (resultCode == ResultCodes.RC_SUCCESS && !string.IsNullOrEmpty(uninstallString)) {
                                    if (s_FSecureAgentUninstallRegex.IsMatch(uninstallString)) {
                                        Match matchUninstallString = s_FSecureAgentUninstallRegex.Match(uninstallString);
                                        strAgentDir = matchUninstallString.Groups[@"installDir"].ToString();
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(strAgentDir)) {
                                string strAgentDataDir = string.Empty;
                                if (!strAgentDir.EndsWith(@"\")) {
                                    strAgentDataDir = strAgentDir + @"\FSAUA\Content";
                                } else {
                                    strAgentDataDir = strAgentDir + @"FSAUA\Content";
                                }
                                if (Lib.ValidateDirectory(m_taskId, strAgentDataDir, m_cimvScope)) {
                                    if (!virusDefDatabase.Contains(strAgentDataDir)) {
                                        virusDefDatabase.Add(strAgentDataDir);
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for sub-folder.\n{1}",
                                      m_taskId,
                                      ex.ToString());
            }
            return virusDefDatabase;
        }

        /// <summary>
        /// Retrieve Virus Definition Information
        /// </summary>
        /// <param name="repositoryPath">Path to repository</param>
        /// <returns>Info String.</returns>
        private string GetAntiVirusRepositoryDetails(string repositoryPath) {
            StringBuilder virusDefinitionInfo = new StringBuilder();
            try {
                IList<string> antiVirusFileSet =
                    Lib.RetrieveSubDirectories(m_taskId, m_cimvScope, repositoryPath.ToString());
                if (antiVirusFileSet != null && antiVirusFileSet.Count == 1) {
                    string fileSetVersion = antiVirusFileSet[0];
                    if (!string.IsNullOrEmpty(fileSetVersion)) {
                        String fileSetPath = repositoryPath + @"\" + fileSetVersion;
                        IList<string> avFiles = Lib.RetrieveFileListings(m_taskId, m_cimvScope, fileSetPath);
                        if (avFiles != null && avFiles.Count > 0) {
                            foreach (string avFile in avFiles) {
                                if (s_FSecureAVMetaData.Keys.Contains(avFile.ToUpper())) {
                                    virusDefinitionInfo.Append(@"Virus_Def_FileSet_Type=""")
                                                       .Append(s_FSecureAVMetaData[avFile.ToUpper()]).Append('"')
                                                       .Append(BdnaDelimiters.DELIMITER2_TAG)
                                                       .Append(@"Virus_Def_FileSet_Ver=""")
                                                       .Append(fileSetVersion).Append('"');

                                    // Get Virus Definition Date
                                    String avFilePath = fileSetPath + @"\" + avFile;
                                    Dictionary<string, string> fileProperties = new Dictionary<string, string>();
                                    Lib.RetrieveFileProperties(m_taskId, m_cimvScope, avFilePath, out fileProperties);
                                    if (fileProperties != null && fileProperties.ContainsKey(@"LastModified")) {
                                        string strLastModifiedDate = String.Empty;
                                        try {
                                            strLastModifiedDate = fileProperties[@"LastModified"];
                                            DateTime lastModifiedDate;
                                            if (DateTime.TryParse(strLastModifiedDate, out lastModifiedDate)) {
                                                lastModifiedDate = DateTime.Parse(strLastModifiedDate);
                                                strLastModifiedDate = lastModifiedDate.ToShortDateString();
                                            } else {
                                                if (s_dateRegex.IsMatch(strLastModifiedDate)) {
                                                    Match match = s_dateRegex.Match(strLastModifiedDate);
                                                    strLastModifiedDate = match.Groups[1].ToString();
                                                }
                                            }

                                            virusDefinitionInfo.Append(BdnaDelimiters.DELIMITER2_TAG)
                                                               .Append(@"Virus_Def_File_Date=""")
                                                               .Append(strLastModifiedDate).Append('"');
                                        } catch (Exception ex) {
                                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                                  0,
                                                                  "Task Id {0}: Last Modified Date format exception: {1}",
                                                                  m_taskId,
                                                                  ex.ToString());
                                        }
                                    }
                                    continue;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Unexpected error while searching for sub-folder.\n{1}",
                                      m_taskId,
                                      ex.ToString());
            }
            return virusDefinitionInfo.ToString();
        }

        /// <summary>
        /// Get all Immediate subKey of one registry branch (non-recursive)
        /// </summary>
        /// <param name="strKeyPath">Path</param>
        /// <returns>Array of SubKeys</returns>
        private ResultCodes GetImmediateRegistrySubKeys(ManagementClass wmiRegistry, string strKeyPath, out string[] strSubKeys) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            strSubKeys = null;
            using (ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY)) {
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, strKeyPath);

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(m_taskId, wmiRegistry, RegistryMethodNames.ENUM_KEY, strKeyPath, inputParameters, out outputParameters);

                if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                    using (outputParameters) {
                        strSubKeys = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                    }
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Get one registry string value
        /// </summary>
        /// <param name="strKeyPath">Key Path</param>
        /// <param name="strKeyName">Key Name</param>
        /// <returns>Value</returns>
        private ResultCodes GetRegistryStringValue
                (ManagementClass wmiRegistry, string strKeyPath, string strKeyName, out string strKeyValue) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            strKeyValue = String.Empty;
            using (ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE)) {
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, strKeyPath);
                inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, strKeyName);
                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(m_taskId, wmiRegistry, RegistryMethodNames.GET_STRING_VALUE, strKeyPath,
                                                      inputParameters, out outputParameters);
                if (resultCode == ResultCodes.RC_SUCCESS && null != outputParameters) {
                    using (outputParameters) {
                        strKeyValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                    }
                }
            }
            return resultCode;
        }

        /// <summary> Data row property </summary>
        public StringBuilder dataRow {
            get { return m_dataRow; }
        }

        // FSecure Anti-Virus metadata definition repository
        static FSecureAVDetailsScript() {
            if (s_FSecureAVMetaData != null) {
                s_FSecureAVMetaData.Add(@"FS@AVPE.INI", @"AVPE");
                s_FSecureAVMetaData.Add(@"FS@AV.INI", @"AV");
                s_FSecureAVMetaData.Add(@"FS@LIBRA.INI", @"Libra");
                s_FSecureAVMetaData.Add(@"FS@ORION.INI", @"Orion");
            }
        }
        /// <summary>Add/Remove registry </summary>
        private static readonly string s_registryKeyUninstall = @"software\Microsoft\Windows\CurrentVersion\Uninstall\";

        /// <summary>F-Secure AV Definition registry </summary>
        private static IDictionary<string, string> s_FSecureAVMetaData = new Dictionary<string, string>();

        /// <summary>Date Format</summary>
        private static readonly Regex s_dateRegex = new Regex(@"^(\d\d\d\d\d\d\d\d).*", RegexOptions.Compiled);

        private static readonly Regex s_FSecureAutoUpdateAgentRegex =
            new Regex(@"F-Secure(.*) Automatic Update Agent", RegexOptions.Compiled);

        private static readonly Regex s_FSecureAgentUninstallRegex =
            new Regex(@"""?(?<installDir>.*?)[^\\]+.exe""", RegexOptions.Compiled);

        /// <summary> CIMV2 Management Scope </summary>
        private ManagementScope m_cimvScope = null;

        /// <summary> CIMV2 Management Scope </summary>
        private ManagementScope m_defaultScope = null;

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;

    }
}
