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
    public class StopServiceSUScript : ICollectionScriptRuntime
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
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script StopServiceSUScript.",
                                  m_taskId);
            try
            {
                if (null == connection)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to StopServiceSUScript is null.",
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
                        string status = "Software usage service not installed";
                        if (IsServiceSUInstalled(cimvScope))
                        {
                            StopServiceSU(cimvScope);
                            status = "Software usage agent successfully stopped";
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
            catch (Exception ex)
            {
                Lib.LogException(m_taskId,
                                 executionTimer,
                                 "Unhandled exception in StopServiceSUScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                      0,
                      "Task Id {0}: Collection script StopServiceSUScript.  Elapsed time {1}.  Result code {2}.",
                      m_taskId,
                      executionTimer.Elapsed.ToString(),
                      resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
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

        private ResultCodes StopServiceSU(ManagementScope scope)
        {
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            try
            {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Stopping ServiceSU service.",
                                      m_taskId);
                using (ManagementObject mo = new ManagementObject("Win32_Service='" + s_serviceName + "'"))
                {
                    mo.Scope = scope;
                    mo.Get();
                    ManagementBaseObject outParams = mo.InvokeMethod("StopService", null, null);
                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Stopped ServiceSU with return value {1}",
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
                                      "Task Id {0}: StopService management exception:\n{1}\n{2}",
                                      m_taskId,
                                      mex.ToString(),
                                      sb.ToString());
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }
            catch (Exception ex)
            {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: StopService exception:\n{1}\n{2}",
                                      m_taskId,
                                      ex.ToString());
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }
            return resultCode;
        }

        #endregion

        /// <summary>Database assigned task Id.</summary>
        private string m_taskId;

        private static readonly string s_serviceName = "QP: Discovery Software Usage Agent";
        private static readonly string s_pathName = @"C:\Program Files\PSSOFT\QPDIscovery\Agent\QPSoftwareUsage.exe";
    }
}
