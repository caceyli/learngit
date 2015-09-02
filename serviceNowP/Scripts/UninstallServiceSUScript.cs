#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: David Chou
*   Creation Date: 2009/02/20
*
* Current Status
*       $Revision: 1.2 $
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

namespace bdna.Scripts
{
    public class UninstallServiceSUScript : ICollectionScriptRuntime
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
            m_tftpDispatcher = tftpDispatcher;
            m_tftpPath = tftpPath;
            m_tftpPath_login = tftpPath_login;
            m_tftpPath_password = tftpPath_password;
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script UninstallServiceSUScript.",
                                  m_taskId);
            try
            {
                if (null == connection)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to UninstallServiceSUScript is null.",
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
                            try
                            {
                                resultCode = DeleteRegistryValues(wmiRegistry);
                            }
                            catch (Exception ex)
                            {
                                // We may catch an exception if the QPD registry keys or values
                                // do not exist. Carry on in this case.
                            }
                            if (resultCode == ResultCodes.RC_SUCCESS)
                            {
                                string status = "Software usage service was not found";
                                if (IsServiceSUInstalled(cimvScope))
                                {
                                    resultCode = UninstallServiceSU(cimvScope);
                                    status = "Software usage agent successfully uninstalled";
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
                            if (resultCode == ResultCodes.RC_SUCCESS && !String.IsNullOrEmpty(m_pathName))
                            {
                                DeleteFiles(cimvScope);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in UninstallServiceSUScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                      0,
                      "Task Id {0}: Collection script UninstallServiceSUScript.  Elapsed time {1}.  Result code {2}.",
                      m_taskId,
                      executionTimer.Elapsed.ToString(),
                      resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion

        private ResultCodes DeleteRegistryValues(ManagementClass wmiRegistry)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;

            // We first read the software usage path for deleting the files later
            Lib.GetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"SoftwareUsagePath", out m_serviceSUPath);
            Lib.GetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"LogPath", out m_logPath);
            Lib.GetRegistryStringValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"Path", out m_pathName);

            resultCode = Lib.DeleteRegistryValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"LogPath");
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Information,
                    0,
                    "Task Id {0}: Deleting LogPath registry value failed with result code {1}.",
                    m_taskId,
                    resultCode);
            }
            resultCode = Lib.DeleteRegistryValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"Path");
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Information,
                    0,
                    "Task Id {0}: Deleting Path registry value failed with result code {1}.",
                    m_taskId,
                    resultCode);
            }
            resultCode = Lib.DeleteRegistryValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"Period");
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Information,
                    0,
                    "Task Id {0}: Deleting Period registry value failed with result code {1}.",
                    m_taskId,
                    resultCode);
            }
            resultCode = Lib.DeleteRegistryValue(m_taskId, wmiRegistry, s_agentRegistryPath, @"SoftwareUsagePath");
            if (resultCode != ResultCodes.RC_SUCCESS)
            {
                Lib.Logger.TraceEvent(TraceEventType.Information,
                    0,
                    "Task Id {0}: Deleting SoftwareUsagePath registry value failed with result code {1}.",
                    m_taskId,
                    resultCode);
            }
            return resultCode;
        }

        private bool IsServiceSUInstalled(ManagementScope scope)
        {

            //ManagementClass mcServices = new ManagementClass("Win32_Service");
            //foreach (ManagementObject moService in mcServices.GetInstances())
            //{
            //    string name = moService.GetPropertyValue("Name").ToString();
            //    Console.WriteLine("I see service " + name +
            //        " with caption " + moService.GetPropertyValue("Caption").ToString());
            //    if (name == s_serviceName)
            //    {
            //        int result = Convert.ToInt32(moService.InvokeMethod("Delete", null));
            //        Console.WriteLine("Deleted " + name + " with result " + result.ToString());
            //    }
            //}

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
            return false;
        }

        private ResultCodes UninstallServiceSU(ManagementScope scope)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            try
            {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Deleting ServiceSU service.",
                                      m_taskId);
                using (ManagementObject mo = new ManagementObject("Win32_Service='" + s_serviceName + "'"))
                {
                    mo.Scope = scope;
                    mo.Get();
                    ManagementBaseObject outParams = mo.InvokeMethod("Delete", null, null);
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Deleted ServiceSU with return value {1}",
                                          m_taskId,
                                          outParams["ReturnValue"].ToString());
                    resultCode = ResultCodes.RC_SUCCESS;
                }
            }
            catch (ManagementException mex)
            {
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
                                      "Task Id {0}: Uninstall management exception:\n{1}\n{2}",
                                      m_taskId,
                                      mex.ToString(),
                                      sb.ToString());
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }
            catch (Exception ex)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Uninstall exception:\n{1}\n{2}",
                                      m_taskId,
                                      ex.ToString());
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }
            return resultCode;
        }

        private ResultCodes DeleteFiles(ManagementScope cimvScope)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            if (String.IsNullOrEmpty(m_serviceSUPath))
            {
                return resultCode;
            }
            resultCode = DeleteFile(cimvScope, m_pathName + s_executableName);
            resultCode = DeleteFile(cimvScope, m_serviceSUPath + "ListPrint.xml");
            return resultCode;
        }

        private ResultCodes DeleteFile(ManagementScope cimvScope, string fileName)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            using (IRemoteProcess rp =
                    RemoteProcess.NewRemoteProcess(m_taskId, cimvScope,
                                                   @"cmd /Q /C del """ + fileName + '"', null,
                                                   StdioRedirection.STDOUT, m_connection, m_tftpPath, m_tftpPath_login, m_tftpPath_password, m_tftpDispatcher))
            {
                resultCode = rp.Launch();
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Deletion of file {1} completed with result code {2}.",
                                      m_taskId,
                                      fileName,
                                      resultCode.ToString());
            }
            return resultCode;
        }

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;

        /// <summary>Map of connection parameters.</summary>
        private IDictionary<string, object> m_connection;

        /// <summary>TFTP Listener</summary>
        private ITftpDispatcher m_tftpDispatcher = null;
        private string m_tftpPath = string.Empty;
        private string m_tftpPath_login = string.Empty;
        private string m_tftpPath_password = string.Empty;

        /// <summary>Path of the executable.</summary>
        private string m_serviceSUPath;
        private string m_logPath;
        private string m_pathName;

        private static readonly string s_serviceName = "QP: Discovery Software Usage Agent";

        /// <summary>Location of the service executable</summary>
        private static readonly string s_executableName = "QPSoftwareUsage.exe";

        /// <summary>Registry path for QPD agent.</summary>
        private static readonly string s_agentRegistryPath = @"software\PSSOFT\QPDiscovery\AGENT";

    }
}
