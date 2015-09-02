#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Alexander Meau
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.24 $
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
using System.Runtime.InteropServices;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script for Internet Explorer level 2 information.
    /// </summary>
    public class InternetExplorerStaticScript : ICollectionScriptRuntime {

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
                                  "Task Id {0}: Collection script InternetExplorerStaticScript.",
                                  m_taskId);

            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to InternetExplorerStaticScript is null.",
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
                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, 
                                                                                 new ManagementPath(@"StdRegProv"), 
                                                                                 null)) {
                            Debug.Assert(null != wmiRegistry);
                            resultCode = GetIEInstalledDirectory(wmiRegistry);
                        }

                        if (!String.IsNullOrEmpty(m_ieInstallDirectory)) {
                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                resultCode = GetIEVersionAndInstallDate(cimvScope);
                            }

                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                GetServicePack();
                                dataRow.Append(elementId)
                                       .Append(',')
                                       .Append(attributes[@"IEDetails"])
                                       .Append(',')
                                       .Append(scriptParameters[@"CollectorId"])
                                       .Append(',')
                                       .Append(taskId)
                                       .Append(',')
                                       .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                       .Append(',')
                                       .Append(@"IEDetails")
                                       .Append(',')
                                       .Append(BdnaDelimiters.BEGIN_TAG)
                                       .Append(@"name=""Internet Explorer")
                                       .Append(m_ieFullProductName)
                                       .Append('"')
                                       .Append(BdnaDelimiters.DELIMITER2_TAG)
                                       .Append("servicePack=\"")
                                       .Append(m_ieServicePack)
                                       .Append('"')
                                       .Append(BdnaDelimiters.DELIMITER2_TAG)
                                       .Append("version=\"")
                                       .Append(m_ieVersion)
                                       .Append('"')
                                       .Append(BdnaDelimiters.DELIMITER2_TAG)
                                       .Append("installDirectory=\"")
                                       .Append(m_ieInstallDirectory)
                                       .Append('"')
                                       .Append(BdnaDelimiters.DELIMITER2_TAG)
                                       .Append("installDate=\"")
                                       .Append(m_ieInstallDate)
                                       .Append('"')
                                       .Append(BdnaDelimiters.END_TAG);
                            }
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access file property iexplore.exe.\nMessage: {1}",
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
                                 "Unhandled exception in InternetExplorerStaticScript",
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
                                  "Task Id {0}: Collection script InternetExplorerStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }

        #endregion ICollectionScriptRuntime Members

        /// <summary>
        /// Query the remote registry to get the install directory for Internet Explorer 6 or below
        /// </summary>
        /// <param name="wmiRegistry">Remote registry connection.</param>
        /// <returns>Operation resutl code.</returns>
        private ResultCodes GetIEInstalledDirectory(ManagementClass wmiRegistry) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
            resultCode = getRegistryStringValue(wmiRegistry, s_ie7Path, @"Version", out m_ieVersion);
            if (!string.IsNullOrEmpty(m_ieVersion)) {
                if (m_ieVersion.StartsWith("7") || m_ieVersion.StartsWith("8") || m_ieVersion.StartsWith("9")) {
                    m_ieInstallDirectory = s_ie7Directory;
                }
            }
            if (String.IsNullOrEmpty(m_ieInstallDirectory)) {
                resultCode = getRegistryStringValue(wmiRegistry, s_ieSetupPath1, @"Path", out m_ieInstallDirectory);
                if (String.IsNullOrEmpty(m_ieInstallDirectory)) {
                    resultCode = getRegistryStringValue(wmiRegistry, s_ieSetupPath2, @"Path", out m_ieInstallDirectory);
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Use WMI to get version and installation information for Internet explorer 6 or below
        /// </summary>
        /// 
        /// <param name="cimvScope">WMI connection.</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes GetIEVersionAndInstallDate(ManagementScope cimvScope) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
            if (String.IsNullOrEmpty(m_ieInstallDirectory)) {
                return resultCode;
            }

            Dictionary<string, string> fileProp = null;
            resultCode = Lib.RetrieveFileProperties(m_taskId, cimvScope, m_ieInstallDirectory + @"\IEXPLORE.EXE", out fileProp);
            if (fileProp != null) {
                if (fileProp.ContainsKey("Version")) {
                    m_ieVersion = fileProp["Version"];
                }
                if (fileProp.ContainsKey("InstallDate")) {
                    m_ieInstallDate = fileProp["InstallDate"];
                    if (!String.IsNullOrEmpty(m_ieInstallDate)) {
                        Debug.Assert(8 <= m_ieInstallDate.Length);
                        m_ieInstallDate = String.Format("{0}/{1}/{2}",
                                                        m_ieInstallDate.Substring(0, 4),
                                                        m_ieInstallDate.Substring(4, 2),
                                                        m_ieInstallDate.Substring(6, 2));
                    }
                }
            }
            return resultCode;
        }

        /// <summary>
        /// Helper method to grep through the version string to
        /// see if we can get the service pack information.
        /// </summary>
        private void GetServicePack() {
            string versionNumber = null;
            MatchCollection mc = s_versionRegex.Matches(m_ieVersion);
            if (null != mc && 0 < mc.Count) {
                versionNumber = mc[0].Groups[1].Value.ToString();

                if (!String.IsNullOrEmpty(versionNumber)) {

                    for (int i = 0; s_versionMap.Length > i; i++) {
                        if (0 <= s_versionMap[i].m_version.IndexOf(versionNumber)) {
                            m_ieFullProductName = s_versionMap[i].m_fullName;
                            m_ieServicePack = s_versionMap[i].m_release;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve String value from registry path.
        /// </summary>
        /// <param name="wmiRegistry">Management Object</param>
        /// <param name="keyPath">Registry Path</param>
        /// <param name="keyName">Key Name</param>
        /// <param name="stringValue">Key Value</param>
        /// <returns></returns>
        private ResultCodes getRegistryStringValue(
                ManagementClass wmiRegistry,
                string keyPath,
                string keyName,
                out string stringValue) {
            ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_MULTI_STRING_VALUE);
            inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
            inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, keyPath);
            inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, keyName);

            ManagementBaseObject outputParameters = null;
            stringValue = null;
            ResultCodes resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                  wmiRegistry,
                                                  RegistryMethodNames.GET_STRING_VALUE,
                                                  keyPath,
                                                  inputParameters,
                                                  out outputParameters);

            if (resultCode == ResultCodes.RC_SUCCESS && null != outputParameters) {

                using (outputParameters) {
                    stringValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                }

            }
            return resultCode;
        }

        /// <summary>Database assigned task Id.</summary>
        private string  m_taskId;

        /// <summary>
        /// Version map table entry.  Associate a version code
        /// with description and service pack information.
        /// </summary>
        private struct VersionMapEntry {

            public VersionMapEntry(
                    string          version,
                    string          fullName,
                    string          release) {
                m_version = version;
                m_fullName = fullName;
                m_release = release;
            }

            /// <summary>Version string.</summary>
            public readonly string  m_version;

            /// <summary>Version description.</summary>
            public readonly string  m_fullName;

            /// <summary>Release description.</summary>
            public readonly string  m_release;
        }

        /// <summary>Version mapping table.</summary>
        private static readonly VersionMapEntry[]   s_versionMap = {
            new VersionMapEntry(@"4.40.308"       , @" 1.0 /(Plus! for Windows 95/NT)"                  , String.Empty),
            new VersionMapEntry(@"4.40.520"       , @" 2.0"                                             , String.Empty),
            new VersionMapEntry(@"4.70.1155"      , @" 3.0"                                             , String.Empty),
            new VersionMapEntry(@"4.70.1158"      , @" 3.0 /(Windows 95 OSR2/NT)"                       , String.Empty),
            new VersionMapEntry(@"4.70.1215"      , @" 3.01"                                            , String.Empty),
            new VersionMapEntry(@"4.70.1300"      , @" 3.02 and 3.02a"                                  , String.Empty),
            new VersionMapEntry(@"4.71.544"       , @" 4.0 Platform Preview 1.0 (PP1)"                  , @"Platform Preview 1 (PP1)"),
            new VersionMapEntry(@"4.71.1008.3"    , @" 4.0 Platform Preview 2.0 (PP2)"                  , @"Platform Preview 2 (PP2)"),
            new VersionMapEntry(@"4.71.1712.5"    , @" 4.0"                                             , String.Empty),
            new VersionMapEntry(@"4.71.1712.6"    , @" 4.0"                                             , String.Empty),
            new VersionMapEntry(@"4.72.2106.7"    , @" 4.01"                                            , String.Empty),
            new VersionMapEntry(@"4.72.2106.8"    , @" 4.01"                                            , String.Empty),
            new VersionMapEntry(@"4.72.3110.3"    , @" 4.01 Service Pack 1 /(Windows 98)"               , @"Service Pack 1 (SP1)"),
            new VersionMapEntry(@"4.72.3110.8"    , @" 4.01 Service Pack 1 /(Windows 98/NT)"            , @"Service Pack 1 (SP1)"),
            new VersionMapEntry(@"4.72.3612.1707" , @" 4.01 Service Pack 2"                             , @"Service Pack 2 (SP2)"),
            new VersionMapEntry(@"4.72.3612.1713" , @" 4.01 Service Pack 2"                             , @"Service Pack 2 (SP2)"),
            new VersionMapEntry(@"5.00.0518.5"    , @" 5 Developer Preview (Beta 1)"                    , String.Empty),
            new VersionMapEntry(@"5.0.0518.5"     , @" 5 Developer Preview (Beta 1)"                    , String.Empty),
            new VersionMapEntry(@"5.00.0518.10"   , @" 5 Developer Preview (Beta 1)"                    , String.Empty),
            new VersionMapEntry(@"5.0.0518.10"    , @" 5 Developer Preview (Beta 1)"                    , String.Empty),
            new VersionMapEntry(@"5.00.0910.1308" , @" 5 Beta (Beta 2)"                                 , String.Empty),
            new VersionMapEntry(@"5.0.0910.1308"  , @" 5 Beta (Beta 2)"                                 , String.Empty),
            new VersionMapEntry(@"5.00.0910.1309" , @" 5 Beta (Beta 2)"                                 , String.Empty),
            new VersionMapEntry(@"5.0.0910.1309"  , @" 5 Beta (Beta 2)"                                 , String.Empty),
            new VersionMapEntry(@"5.00.2014.213"  , @" 5"                                               , String.Empty),
            new VersionMapEntry(@"5.0.2014.213"   , @" 5"                                               , String.Empty),
            new VersionMapEntry(@"5.00.2014.0216" , @" 5"                                               , String.Empty),
            new VersionMapEntry(@"5.0.2014.0216"  , @" 5"                                               , String.Empty),
            new VersionMapEntry(@"5.00.2314.1000" , @" 5 (Office 2000)"                                 , String.Empty),
            new VersionMapEntry(@"5.0.2314.1000"  , @" 5 (Office 2000)"                                 , String.Empty),
            new VersionMapEntry(@"5.00.2314.1003" , @" 5 (Office 2000)"                                 , String.Empty),
            new VersionMapEntry(@"5.0.2314.1003"  , @" 5 (Office 2000)"                                 , String.Empty),
            new VersionMapEntry(@"5.00.2614.3500" , @" 5 (Windows 98 Second Edition/NT)"                , String.Empty),
            new VersionMapEntry(@"5.0.2614.3500"  , @" 5 (Windows 98 Second Edition/NT)"                , String.Empty),
            new VersionMapEntry(@"5.00.2516.1900" , @" 5.01 (Windows 2000 Beta 3, build 5.00.2031)"     , String.Empty),
            new VersionMapEntry(@"5.0.2516.1900"  , @" 5.01 (Windows 2000 Beta 3, build 5.00.2031)"     , String.Empty),
            new VersionMapEntry(@"5.00.2919.800"  , @" 5.01 (Windows 2000 RC1, build 5.00.2072)"        , @"Release Candidate 1 (RC1)"),
            new VersionMapEntry(@"5.0.2919.800"   , @" 5.01 (Windows 2000 RC1, build 5.00.2072)"        , @"Release Candidate 1 (RC1)"),
            new VersionMapEntry(@"5.00.2919.3800" , @" 5.01 (Windows 2000 RC2, build 5.00.2128)"        , @"Release Candidate 2 (RC2)"),
            new VersionMapEntry(@"5.0.2919.3800"  , @" 5.01 (Windows 2000 RC2, build 5.00.2128)"        , @"Release Candidate 2 (RC2)"),
            new VersionMapEntry(@"5.00.2919.6307" , @" 5.01 (Office 2000 SR-1)"                         , @"Service Release 2 (SR2)"),
            new VersionMapEntry(@"5.0.2919.6307"  , @" 5.01 (Office 2000 SR-1)"                         , @"Service Release 2 (SR2)"),
            new VersionMapEntry(@"5.00.2920.0000" , @" 5.01 (Windows 2000, build 5.00.2195)"            , String.Empty),
            new VersionMapEntry(@"5.0.2920.0000"  , @" 5.01 (Windows 2000, build 5.00.2195)"            , String.Empty),
            new VersionMapEntry(@"5.00.3103.1000" , @" 5.01 SP1 (Windows 2000 SP1)"                     , @"Service Pack 1 (SR1)"),
            new VersionMapEntry(@"5.0.3103.1000"  , @" 5.01 SP1 (Windows 2000 SP1)"                     , @"Service Pack 1 (SR1)"),
            new VersionMapEntry(@"5.00.3105.0106" , @" 5.01 SP1 (Windows 95/98 and Windows NT 4.0)"     , @"Service Pack 1 (SR1)"),
            new VersionMapEntry(@"5.0.3105.0106"  , @" 5.01 SP1 (Windows 95/98 and Windows NT 4.0)"     , @"Service Pack 1 (SR1)"),
            new VersionMapEntry(@"5.00.3314.2100" , @" 5.01 SP2 (Windows 95/98 and Windows NT 4.0)"     , @"Service Pack 2 (SR2)"),
            new VersionMapEntry(@"5.0.3314.2100"  , @" 5.01 SP2 (Windows 95/98 and Windows NT 4.0)"     , @"Service Pack 2 (SR2)"),
            new VersionMapEntry(@"5.00.3314.2101" , @" 5.01 SP2 (Windows 95/98 and Windows NT 4.0)"     , @"Service Pack 2 (SR2)"),
            new VersionMapEntry(@"5.0.3314.2101"  , @" 5.01 SP2 (Windows 95/98 and Windows NT 4.0)"     , @"Service Pack 2 (SR2)"),
            new VersionMapEntry(@"5.00.3315.2879" , @" 5.01 SP2 (Windows 2000 SP2)"                     , @"Service Pack 2 (SP2)"),
            new VersionMapEntry(@"5.0.3315.2879"  , @" 5.01 SP2 (Windows 2000 SP2)"                     , @"Service Pack 2 (SP2)"),
            new VersionMapEntry(@"5.00.3315.1000" , @" 5.01 SP2 /(Windows 2000 SP2)"                    , @"Service Pack 2 (SP2)"),
            new VersionMapEntry(@"5.0.3315.1000"  , @" 5.01 SP2 /(Windows 2000 SP2)"                    , @"Service Pack 2 (SP2)"),
            new VersionMapEntry(@"5.00.3502.5400" , @" 5.01 SP3 (Windows 2000 SP3 only)"                , @"Service Pack 3 (SP3)"),
            new VersionMapEntry(@"5.00.3502.1000" , @" 5.01 SP3 (Windows 2000 SP3 only)"                , @"Service Pack 3 (SP3)"),
            new VersionMapEntry(@"5.0.3502.1000"  , @" 5.01 SP3 (Windows 2000 SP3 only)"                , @"Service Pack 3 (SP3)"),
            new VersionMapEntry(@"5.00.3700.1000" , @" 5.01 SP4 (Windows 2000 SP4 only)"                , @"Service Pack 4 (SP4)"),
            new VersionMapEntry(@"5.0.3700.1000"  , @" 5.01 SP4 (Windows 2000 SP4 only)"                , @"Service Pack 4 (SP4)"),
            new VersionMapEntry(@"5.00.3700.6668" , @" 5.01 SP4 (Windows 2000 SP4 only)"                , @"Service Pack 4 (SP4)"),
            new VersionMapEntry(@"5.0.3700.6668"  , @" 5.01 SP4 (Windows 2000 SP4 only)"                , @"Service Pack 4 (SP4)"),
            new VersionMapEntry(@"5.50.3825.1300" , @" 5.5 Developer Preview (Beta)"                    , String.Empty),
            new VersionMapEntry(@"5.50.4030.2400" , @" 5.5 & Internet Tools Beta"                       , String.Empty),
            new VersionMapEntry(@"5.50.4134.0100" , @" 5.5 for Windows Me (4.90.3000)"                  , String.Empty),
            new VersionMapEntry(@"5.50.4134.0600" , @" 5.5"                                             , String.Empty),
            new VersionMapEntry(@"5.50.4308.2900" , @" 5.5 Advanced Security Privacy Beta"              , String.Empty),
            new VersionMapEntry(@"5.50.4522.1800" , @" 5.5 Service Pack 1"                              , String.Empty),
            new VersionMapEntry(@"5.50.4807.2300" , @" 5.5 Service Pack 2"                              , String.Empty),
            new VersionMapEntry(@"6.00.2462.0000" , @" 6 Public Preview (Beta)"                         , String.Empty),
            new VersionMapEntry(@"6.0.2462.0000"  , @" 6 Public Preview (Beta)"                         , String.Empty),
            new VersionMapEntry(@"6.00.2479.0006" , @" 6 Public Preview (Beta) Refresh"                 , String.Empty),
            new VersionMapEntry(@"6.0.2479.0006"  , @" 6 Public Preview (Beta) Refresh"                 , String.Empty),
            new VersionMapEntry(@"6.00.2600.0000" , @" 6 (Windows XP)"                                  , String.Empty),
            new VersionMapEntry(@"6.0.2600.0000"  , @" 6 (Windows XP)"                                  , String.Empty),
            new VersionMapEntry(@"6.00.2800.1106" , @" 6 Service Pack 1 (Windows XP SP1)"               , @"Service Pack 1 (SP1)"),
            new VersionMapEntry(@"6.0.2800.1106"  , @" 6 Service Pack 1 (Windows XP SP1)"               , @"Service Pack 1 (SP1)"),
            new VersionMapEntry(@"6.00.2800.1278" , @" 6 Update v.01 Developer Preview (SP1b Beta)"     , @"Service Pack 1b Beta (SP1b)"),
            new VersionMapEntry(@"6.0.2800.1278"  , @" 6 Update v.01 Developer Preview (SP1b Beta)"     , @"Service Pack 1b Beta (SP1b)"),
            new VersionMapEntry(@"6.00.2800.1314" , @" 6 Update v.04 Developer Preview (SP1b Beta)"     , @"Service Pack 1b Beta (SP1b)"),
            new VersionMapEntry(@"6.0.2800.1314"  , @" 6 Update v.04 Developer Preview (SP1b Beta)"     , @"Service Pack 1b Beta (SP1b)"),
            new VersionMapEntry(@"6.00.2900.2180" , @" 6 for Windows XP SP2"                            , String.Empty),
            new VersionMapEntry(@"6.0.2900.2180"  , @" 6 for Windows XP SP2"                            , String.Empty),
            new VersionMapEntry(@"6.00.3663.0000" , @" 6 for Microsoft Windows Server 2003 RC1"         , @"Release Candidate 1 (RC1)"),
            new VersionMapEntry(@"6.0.3663.0000"  , @" 6 for Microsoft Windows Server 2003 RC1"         , @"Release Candidate 1 (RC1)"),
            new VersionMapEntry(@"6.00.3718.0000" , @" 6 for Windows Server 2003 RC2"                   , @"Release Candidate 2 (RC2)"),
            new VersionMapEntry(@"6.0.3718.0000"  , @" 6 for Windows Server 2003 RC2"                   , @"Release Candidate 2 (RC2)"),
            new VersionMapEntry(@"6.00.3790.0000" , @" 6 for Windows Server 2003 (released)"            , String.Empty),
            new VersionMapEntry(@"6.0.3790.0000"  , @" 6 for Windows Server 2003 (released)"            , String.Empty),
            new VersionMapEntry(@"6.00.3790.1830" , @" 6 for Windows Server 2003 SP1 and Windows XP x64", @"Service Pack 1 (SP1)"),
            new VersionMapEntry(@"6.0.3790.1830"  , @" 6 for Windows Server 2003 SP1 and Windows XP x64", @"Service Pack 1 (SP1)"),
            new VersionMapEntry(@"7.00.5730.1100" , @" 7 for Windows XP and Windows Server 2003"        , String.Empty),
            new VersionMapEntry(@"7.0.5730.1100"  , @" 7 for Windows XP and Windows Server 2003"        , String.Empty),
            new VersionMapEntry(@"7.00.6000.16386", @" 7 for Windows Vista"                             , String.Empty),
            new VersionMapEntry(@"7.0.6000.16386" , @" 7 for Windows Vista"                             , String.Empty)
        };

        /// <summary>Registry path for IE setup information.</summary>
        private static readonly string  s_ieSetupPath1 = @"software\Microsoft\IE Setup\Setup";

        /// <summary>Registry path for IE4 setup information.</summary>
        private static readonly string  s_ieSetupPath2 = @"software\Microsoft\IE4\Setup";

        /// <summary> Registry path for IE 7 </summary>
        private static readonly string  s_ie7Path = @"software\Microsoft\Internet Explorer";

        /// <summary>Installation Directory for IE 7</summary>
        private static readonly string s_ie7Directory = @"C:\Program Files\Internet Explorer";

        /// <summary>Installed directory location.</summary>
        private string m_ieInstallDirectory = String.Empty;

        /// <summary>Version information.</summary>
        private string m_ieVersion = String.Empty;

        /// <summary>Installed date.</summary>
        private string m_ieInstallDate = String.Empty;

        /// <summary>Full product name.</summary>
        private string m_ieFullProductName = String.Empty;

        /// <summary>Service pack information.</summary>
        private string m_ieServicePack = String.Empty;

        private static readonly Regex s_versionRegex =
            new Regex(@"([.\d]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
