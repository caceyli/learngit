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
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Runtime.InteropServices;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script for Microsoft Outlook Express and Windows Mail level 2 information.
    /// "Outlook Express" has been renamed to "Windows Mail" in Windows Vista operating system.
    /// </summary>
    public class MSOutlookExpressStaticScript : ICollectionScriptRuntime {

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
                                  "Task Id {0}: Collection script MSOutlookExpressStaticScript.",
                                  m_taskId);

            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to MSOutlookExpressStaticScript is null.",
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
                            resultCode = GetInstallationDirectory(wmiRegistry);
                        }
                        if (!String.IsNullOrEmpty(m_strInstallDirectory)) {
                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                resultCode = GetExeVersionAndInstallDate(cimvScope);

                                if (ResultCodes.RC_SUCCESS == resultCode) {
                                    dataRow.Append(elementId)
                                           .Append(',')
                                           .Append(attributes[@"installedOfficeDetails"])
                                           .Append(',')
                                           .Append(scriptParameters[@"CollectorId"])
                                           .Append(',')
                                           .Append(taskId)
                                           .Append(',')
                                           .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                           .Append(',')
                                           .Append(@"installedOfficeDetails")
                                           .Append(',')
                                           .Append(BdnaDelimiters.BEGIN_TAG)
                                           .Append("name=\"")
                                           .Append(m_strName)
                                           .Append('"')
                                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                                           .Append("version=\"")
                                           .Append(m_strVersion)
                                           .Append('"')
                                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                                           .Append("installDirectory=\"")
                                           .Append(m_strInstallDirectory)
                                           .Append('"')
                                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                                           .Append("installDate=\"")
                                           .Append(m_strInstallDate)
                                           .Append('"')
                                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                                           .Append("lastAccessedDate=\"")
                                           .Append(m_strLastAccessedDate)
                                           .Append('"')
                                           .Append(BdnaDelimiters.END_TAG);
                                }
                                // Registry entries may exist for Windows Mail even if it
                                // is not installed--see bug #16177. The registry can have
                                // a valid InstallRoot but no WINMAIL.EXE file is in the
                                // directory. We still should return RC_SUCCESS.
                                resultCode = ResultCodes.RC_SUCCESS;
                            }
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to access outlook express file property.\nMessage: {1}",
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
                                 "Unhandled exception in MSOutlookExpressStaticScript",
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
                                  "Task Id {0}: Collection script MSOutlookExpressStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  result.ResultCode.ToString());
            return result;
        }
        #endregion

        /// <summary>
        /// Get install location information from the remote registry.
        /// </summary>
        /// <param name="wmiRegistry">Remote registry connection.</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes GetInstallationDirectory(ManagementClass wmiRegistry) {
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
            resultCode = getRegistryStringValue(wmiRegistry, s_wmRegistryPath, @"InstallRoot", out m_strInstallDirectory);
            if (!String.IsNullOrEmpty(m_strInstallDirectory)) {
                m_bIsWindowsMailInstalled = true;
                m_strName = @"Microsoft Windows Mail";
                resultCode = getRegistryStringValue(wmiRegistry, s_wmRegistryPath, @"MediaVer", out m_strVersion);
            }
            else {
                resultCode = getRegistryStringValue(wmiRegistry, s_oeRegistryPath, @"InstallRoot", out m_strInstallDirectory);
            }
            return resultCode;
        }

        /// <summary>
        /// Get version and installation date information from WMI.
        /// </summary>
        /// <param name="cimvScope">WMI connection.</param>
        /// <returns>Operation result code.</returns>
        private ResultCodes GetExeVersionAndInstallDate(ManagementScope cimvScope) {                
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_ACCESS_FILE_PROPERTY;
            if (String.IsNullOrEmpty(m_strInstallDirectory)) {
                return resultCode;
            }

            Dictionary<string, string> fileProp = null;
            String exeFullPath = m_strInstallDirectory + (m_bIsWindowsMailInstalled? @"\WINMAIL.EXE" : @"\MSIMN.EXE");
            resultCode = Lib.RetrieveFileProperties(m_taskId, cimvScope, exeFullPath, out fileProp);
            if (fileProp != null) {
                if (fileProp.ContainsKey("Version")) {
                    m_strVersion = fileProp["Version"];
                }
                if (fileProp.ContainsKey("LastAccessed")) {
                    m_strLastAccessedDate = fileProp["LastAccessed"];
                    Debug.Assert(8 <= m_strLastAccessedDate.Length);
                    m_strLastAccessedDate = String.Format("{0}/{1}/{2}",
                                                          m_strLastAccessedDate.Substring(0, 4),
                                                          m_strLastAccessedDate.Substring(4, 2),
                                                          m_strLastAccessedDate.Substring(6, 2));
                }
                if (fileProp.ContainsKey("InstallDate")) {
                    m_strInstallDate = fileProp["InstallDate"];
                    if (!String.IsNullOrEmpty(m_strInstallDate)) {
                        Debug.Assert(8 <= m_strInstallDate.Length);
                        m_strInstallDate = String.Format("{0}/{1}/{2}",
                                                        m_strInstallDate.Substring(0, 4),
                                                        m_strInstallDate.Substring(4, 2),
                                                        m_strInstallDate.Substring(6, 2));
                    }
                }
            }
            return resultCode;
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

        /// <summary>Registry path for Outlook Express.</summary>
        private static readonly string s_oeRegistryPath = @"software\Microsoft\Outlook Express";

        /// <summary>Registry path for Windows Mail</summary>
        private static readonly string s_wmRegistryPath = @"software\Microsoft\Windows Mail";

        /// <summary>Outlook Express installtion directory.</summary>
        private string m_strInstallDirectory = String.Empty;

        /// <summary>Product Name</summary>
        private string m_strName = @"Microsoft Outlook Express";

        /// <summary>Outlook Express version.</summary>
        private string m_strVersion = String.Empty;

        /// <summary>Outlook Express installation date.</summary>
        private string m_strInstallDate = String.Empty;

        /// <summary>Last Access Date</summary>
        private string m_strLastAccessedDate = String.Empty;

        /// <summary> Boolean flag to indicate whether it is WindowsMail or OutlookExpress </summary>
        private bool m_bIsWindowsMailInstalled = false;

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;
    }
}
