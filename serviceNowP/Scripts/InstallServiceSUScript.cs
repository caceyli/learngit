#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: David Chou
*   Creation Date: 2009/02/20
*
* Current Status
*       $Revision: 1.4 $
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

using bdna.ScriptLib;
using bdna.Shared;
using System.IO;

namespace bdna.Scripts
{
    public class InstallServiceSUScript : ICollectionScriptRuntime
    {
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
                ITftpDispatcher tftpDispatcher)
        {
            Stopwatch executionTimer = Stopwatch.StartNew();
            m_taskId = taskId.ToString();
            m_connection = connection;
            m_tftpPath = tftpPath;
            m_tftpPath_login = tftpPath_login;
            m_tftpPath_password = tftpPath_password;
            m_tftpDispatcher = tftpDispatcher;
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script InstallServiceSUScript.",
                                  m_taskId);
            try
            {
                if (null == connection)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to InstallServiceSUScript is null.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey(@"cimv2"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV2 namespace is not present in connection object.",
                                          m_taskId);
                }
                else if (!connection.ContainsKey(@"default"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          m_taskId);
                }
                else
                {
                    ManagementScope defaultScope = connection[@"default"] as ManagementScope;
                    ManagementScope cimvScope = connection[@"cimv2"] as ManagementScope;

                    if (!cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV2 namespace failed.",
                                              m_taskId);
                    }
                    else if (!defaultScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed.",
                                              m_taskId);
                    }
                    else
                    {
                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null))
                        {
                            string programFilesDir;
                            resultCode = Lib.GetRegistryStringValue(m_taskId, wmiRegistry, s_windowsRegistryPath, @"ProgramFilesDir", out programFilesDir);
                            if ((ResultCodes.RC_SUCCESS == resultCode) && !String.IsNullOrEmpty(programFilesDir))
                            {
                                m_programFilesDir = programFilesDir;
                            }
                            m_pathName = m_programFilesDir + s_relativeServicePath;
                            m_logPathName = m_programFilesDir + s_relativeLogPath;
                            m_executablePath = m_pathName + s_executableName;

                            string status = "Error installing software usage service";
                            if (!IsServiceSUInstalled(cimvScope))
                            {
                                resultCode = CopyFiles(cimvScope);
                                if (resultCode == ResultCodes.RC_SUCCESS)
                                {
                                    resultCode = CreateRegistryValues(wmiRegistry);
                                    if (resultCode == ResultCodes.RC_SUCCESS)
                                    {
                                        InstallServiceSU(cimvScope);
                                        status = "Successfully installed software usage service";
                                    }
                                }
                            }
                            else
                            {
                                status = "Software usage service already installed";
                            }
                            dataRow.Append(elementId)
                                .Append(',')
                                .Append(attributes[@"status"])
                                .Append(',')
                                .Append(scriptParameters[@"CollectorId"])
                                .Append(',')
                                .Append(taskId)
                                .Append(',')
                                .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                .Append(',')
                                .Append(@"status")
                                .Append(',')
                                .Append(BdnaDelimiters.BEGIN_TAG)
                                .Append(status)
                                .Append(BdnaDelimiters.END_TAG);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in InstallServiceSUScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                      0,
                      "Task Id {0}: Collection script InstallServiceSUScript.  Elapsed time {1}.  Result code {2}.",
                      m_taskId,
                      executionTimer.Elapsed.ToString(),
                      resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        private ResultCodes CopyFiles(ManagementScope cimvScope)
        {
            ResultCodes resultCode;
            string localPath = Directory.GetCurrentDirectory() + s_localServicePath;
            using (IRemoteProcess rp =
                RemoteProcess.SendFile(m_taskId, cimvScope, localPath + s_executableName, m_pathName + s_executableName,
                               m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher))
            {
                resultCode = rp.Launch();
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Sending of file {1} completed with result code {2}.",
                                      m_taskId,
                                      s_executableName,
                                      resultCode.ToString());
            }
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                return resultCode;
            }

            using (IRemoteProcess rp =
                RemoteProcess.SendFile(m_taskId, cimvScope, localPath + "ListPrint.xml", m_logPathName + "ListPrint.xml",
                               m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher))
            {
                resultCode = rp.Launch();
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Sending of file {1} completed with result code {2}.",
                                      m_taskId,
                                      "ListPrint.xml",
                                      resultCode.ToString());
            }
            return resultCode;
        }

        private ResultCodes CreateRegistryValues(ManagementClass wmiRegistry)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;

            resultCode = Lib.CreateRegistryKey(m_taskId, wmiRegistry, s_agentRegistryPath);
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                        0,
                        "Task Id {0}: Registry key creation failed with result code {1}.",
                        m_taskId,
                        resultCode);
                return resultCode;
            }

            resultCode = Lib.SetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath,
                @"SoftwareUsagePath", m_logPathName);
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                        0,
                        "Task Id {0}: Setting SoftwareUsagePath registry value failed with result code {1}.",
                        m_taskId,
                        resultCode);
                return resultCode;
            }

            resultCode = Lib.SetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath,
                @"LogPath", m_logPathName);
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                        0,
                        "Task Id {0}: Setting LogPath registry value failed with result code {1}.",
                        m_taskId,
                        resultCode);
                return resultCode;
            }

            resultCode = Lib.SetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath,
                @"Path", m_pathName);
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                        0,
                        "Task Id {0}: Setting Path registry value failed with result code {1}.",
                        m_taskId,
                        resultCode);
                return resultCode;
            }

            resultCode = Lib.SetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath,
                @"Period", @"1");
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                        0,
                        "Task Id {0}: Setting Period registry value failed with result code {1}.",
                        m_taskId,
                        resultCode);
                return resultCode;
            }

            return resultCode;
        }

        private bool IsServiceSUInstalled(ManagementScope scope)
        {
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                      0,
                      "Task Id {0}: Validating ServiceSU on host {1}.",
                      m_taskId,
                      scope.Path.Server);

            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            using (ManagementObject mo = new ManagementObject("Win32_Service='" + s_serviceName + "'"))
            {
                try
                {
                    mo.Scope = scope;
                    ManagementBaseObject outParams = mo.InvokeMethod("InterrogateService", null, null);
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: ServiceSU on host {1} is valid.",
                                          m_taskId,
                                          scope.Path.Server);
                    return true;
                }
                catch (ManagementException mex)
                {
                    if (mex.Message.ToLower().Trim() == "not found")
                    {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                              0,
                                              "Task Id {0}: ServiceSU on host {1} was not found.",
                                              m_taskId,
                                              scope.Path.Server);
                        return false;
                    }

                    StringBuilder sb = new StringBuilder();
                    foreach (PropertyData pd in mex.ErrorInformation.Properties)
                    {
                        if (null != pd && !pd.IsArray)
                        {
                            sb.Append(pd.Name).Append(@"=").Append(pd.Value).AppendLine();
                        }

                    }

                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to validate ServiceSU on host {1} resulted in an exception.\n{2}\n{3}",
                                          m_taskId,
                                          scope.Path.Server,
                                          mex.ToString(),
                                          sb.ToString());

                }
                catch (Exception ex)
                {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Attempt to validate ServiceSU on host {1} resulted in an exception.\n{2}",
                                          m_taskId,
                                          scope.Path.Server,
                                          ex.ToString());
                }
            }
            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: ServiceSU interrogation on host {1} had no result.",
                                  m_taskId,
                                  scope.Path.Server);
            return false;
        }

        private ResultCodes InstallServiceSU(ManagementScope scope)
        {
            ManagementClass mc = new ManagementClass("Win32_Service");
            mc.Scope = scope;

            ManagementBaseObject inParams = mc.GetMethodParameters("Create");
            inParams["Name"] = s_serviceName;
            inParams["DisplayName"] = s_serviceName;
            inParams["PathName"] = m_executablePath;
            inParams["ServiceType"] = 0x10;
            inParams["ErrorControl"] = 0x1;
            inParams["StartMode"] = "Manual";
            inParams["DesktopInteract"] = false;
            inParams["StartName"] = null;
            inParams["StartPassword"] = null;
            inParams["LoadOrderGroup"] = null;
            inParams["LoadOrderGroupDependencies"] = null;
            inParams["ServiceDependencies"] = null;

            ManagementBaseObject outParams = null;

            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Creating ServiceSU service.",
                                  m_taskId);

            try
            {
                outParams = mc.InvokeMethod("Create", inParams, null);
                resultCode = ResultCodes.RC_SUCCESS;
            }
            catch (Exception ex)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                      0,
                      "Task Id {0}: Exception when trying to create ServiceSU: {1}",
                      m_taskId,
                      ex.ToString());
            }

            Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                  0,
                                  "Task Id {0}: Created ServiceSU service with return value {1}",
                                  m_taskId,
                                  outParams["ReturnValue"].ToString());
            return resultCode;
        }

        #endregion

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;
        private string m_programFilesDir = @"C:\Program Files";
        private string m_pathName;
        private string m_logPathName;
        private string m_executablePath;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;
        private string m_tftpPath = string.Empty;
        private string m_tftpPath_login = string.Empty;
        private string m_tftpPath_password = string.Empty;

        /// <summary>Name of the service</summary>
        private static readonly string s_serviceName = "QP: Discovery Software Usage Agent";

        /// <summary>Local relative directory containing service files to be sent</summary>
        private static readonly string s_localServicePath = @"\metering\";

        /// <summary>Service path</summary>
        private static readonly string s_relativeServicePath = @"\PSSOFT\QPDIscovery\Agent\";

        /// <summary>Log path</summary>
        private static readonly string s_relativeLogPath = @"\PSSOFT\QPDIscovery\Agent\log\";

        /// <summary>Location of the service executable</summary>
        private static readonly string s_executableName = "QPSoftwareUsage.exe";

        /// <summary>Registry path for program files directory</summary>
        private static readonly string s_windowsRegistryPath = @"software\Microsoft\Windows\CurrentVersion";

        /// <summary>Registry path for QPD agent.</summary>
        private static readonly string s_agentRegistryPath = @"software\PSSOFT\QPDiscovery\AGENT";
    }
}
