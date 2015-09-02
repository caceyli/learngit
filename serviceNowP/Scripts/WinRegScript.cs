#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.26 $
*           $Date: 2014/07/16 23:02:43 $
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
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script to scavenge Windows level 2 registry data.
    /// </summary>
    public class WinRegScript : ICollectionScriptRuntime {

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
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script WinRegScript.",
                                  m_taskId);

            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to WinRegScript is null.",
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
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              m_taskId);
                    } else if (!defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed",
                                              m_taskId);
                    } else {
                        StringBuilder registryData = new StringBuilder();

                        //
                        // We have to massage the script parameter set, replace
                        // any keys with a colon by just the part after the colon.
                        // We should probably narrow this to just the registryKey 
                        // and registryRoot entries...
                        Dictionary<string, string> d = new Dictionary<string, string>();

                        foreach (KeyValuePair<string, string> kvp in scriptParameters) {
                            string[] sa = kvp.Key.Split(s_collectionParameterSetDelimiter,
                                                        StringSplitOptions.RemoveEmptyEntries);
                            Debug.Assert(sa.Length > 0);
                            d[sa[sa.Length - 1]] = kvp.Value;
                        }

                        scriptParameters = d;
                        string registryRoot = scriptParameters["registryRoot"];
                        string registryKey = scriptParameters["registryKey"];

                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null)) {
                            resultCode = GetRegistryData(wmiRegistry,
                                                         registryRoot,
                                                         registryKey,
                                                         registryData);
                        }

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"registryData"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"registryData")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG)
                                   .Append(registryData)
                                   .Append(BdnaDelimiters.END_TAG);
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Insufficient privilege to read registry.\nMessage: {1}",
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
                                      "Task Id {0}: Not enough privilege to access registry.\nMessage: {1}.",
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
                                 "Unhandled exception in WinRegScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script WinRegScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion

        private ResultCodes GetRegistryData(
                ManagementClass wmiRegistry,
                string registryRoot,
                string registryPaths,
                StringBuilder registryData) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            string keyPathsToScan = null;
            string regexPatternToUse = null;
            string keyEndPathToScan = string.Empty;

            int regexBeginTagPosition = registryPaths.IndexOf(s_regexPatternBeginTag);

            if (0 > regexBeginTagPosition) {
                keyPathsToScan = registryPaths;
            } else {
                keyPathsToScan = registryPaths.Substring(0, regexBeginTagPosition);
                int regexEndTagPosition = registryPaths.IndexOf(s_regexPatternEndTag);

                // @todo we should probably log a warning or return an error
                //       if the regex end tag is not found.
                int regexPatternLength = (0 > regexEndTagPosition)
                                       ? registryPaths.Length - regexBeginTagPosition - s_regexPatternBeginTag.Length
                                       : regexEndTagPosition - regexBeginTagPosition - s_regexPatternBeginTag.Length;
                regexPatternToUse = registryPaths.Substring(regexBeginTagPosition + s_regexPatternBeginTag.Length,
                                                            regexPatternLength);
                if (regexEndTagPosition + s_regexPatternEndTag.Length + 1 < registryPaths.Length) {
                    keyEndPathToScan = registryPaths.Substring(regexEndTagPosition + s_regexPatternEndTag.Length + 1);
                }
            }
            string[] requestedKeyPaths = keyPathsToScan.Split('|');
            IList<string> enhancedKeyPaths = new List<string>();
            foreach (string keyPath in requestedKeyPaths) {
                if (keyPath.StartsWith(s_registrySoftwarePath, StringComparison.CurrentCultureIgnoreCase)) {
                    enhancedKeyPaths.Add(keyPath);
                    enhancedKeyPaths.Add(s_registrySoftwarePath6432 + keyPath.Substring(s_registrySoftwarePath.Length));
                } else {
                    enhancedKeyPaths.Add(keyPath);
                }
            }

            foreach (string keyPath in enhancedKeyPaths) {
                //foreach (string keyPath in requestedKeyPaths) {
                string kp = keyPath.Trim();
                StringBuilder buffer = new StringBuilder();
                if (String.IsNullOrEmpty(regexPatternToUse)) {
                    string keyName = @"[" + registryRoot + @"\" + TranslateWow6432RegistryPath(kp) + @"]";
                    buffer.AppendLine().AppendLine(keyName);
                    resultCode = GetValues(wmiRegistry,
                                           registryRoot,
                                           kp,
                                           buffer);

                    if (ResultCodes.RC_SUCCESS == resultCode) {
                        resultCode = GetKeys(wmiRegistry,
                                             registryRoot,
                                             kp,
                                             buffer);
                    } 

                    // Ignore redundant key
                    if (kp.StartsWith(s_registrySoftwarePath6432)) {
                        string tempString = buffer.ToString().Replace("\r\n", "");
                        if (tempString != (keyName + "@=\"\"")) {
                            registryData.Append(buffer);
                        }
                    } else {
                        registryData.Append(buffer);
                    }
                } else {
                    resultCode = GetKeysWithRegularExpression(wmiRegistry,
                                                              registryRoot,
                                                              kp,
                                                              keyEndPathToScan,
                                                              regexPatternToUse,
                                                              registryData);
                }
                if (ResultCodes.RC_SUCCESS != resultCode) {
                    break;
                }
            }
            return resultCode;
        }

        private string TranslateWow6432RegistryPath(string registryPath) {
            if (registryPath.StartsWith(s_registrySoftwarePath6432)) {
                return s_registrySoftwarePath + registryPath.Substring(s_registrySoftwarePath6432.Length);
            }
            return registryPath;
        }

        private ResultCodes GetKeys(
                ManagementClass wmiRegistry,
                string registryRoot,
                string registryPath,
                StringBuilder registryData) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            RegistryTrees rt = RegistryTrees.HKEY_INVALID;

            try {
                rt = (RegistryTrees)Enum.Parse(typeof(RegistryTrees), registryRoot);
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Exception caught in GetKeys.\n{1}",
                                      m_taskId,
                                      ex.ToString());
                resultCode = ResultCodes.RC_LOCAL_SCRIPT_PROCESSING_ERROR;
            }

            if (ResultCodes.RC_SUCCESS == resultCode) {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);

                ManagementBaseObject outputParameters = null;

                resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.ENUM_KEY,
                                                      registryPath,
                                                      inputParameters,
                                                      out outputParameters);

                if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                    string[] subKeys = null;

                    using (outputParameters) {
                        subKeys = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                    }

                    if (null != subKeys && 0 < subKeys.Length) {
                        StringBuilder keyPath = new StringBuilder();

                        foreach (string subKey in subKeys) {
                            keyPath.Length = 0;
                            keyPath.Append(registryPath)
                                   .Append(@"\")
                                   .Append(subKey);
                            string kp = keyPath.ToString();

                            registryData.AppendLine()
                                        .Append('[')
                                        .Append(registryRoot)
                                        .Append(@"\")
                                        .Append(TranslateWow6432RegistryPath(kp))
                                        .AppendLine(@"]");

                            resultCode = GetValues(wmiRegistry,
                                                   registryRoot,
                                                   kp,
                                                   registryData);

                            if (ResultCodes.RC_SUCCESS == resultCode) {
                                resultCode = GetKeys(wmiRegistry,
                                                     registryRoot,
                                                     kp,
                                                     registryData);
                            }

                            if (ResultCodes.RC_SUCCESS != resultCode) {
                                break;
                            }
                        }
                    }
                }
            }
            return resultCode;
        }

        private ResultCodes GetKeysWithRegularExpression(
                ManagementClass wmiRegistry,
                string registryRoot,
                string registryPath,
                string registryEndPath,
                string regexPattern,
                StringBuilder registryData) {

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            RegistryTrees rt = RegistryTrees.HKEY_INVALID;

            try {
                rt = (RegistryTrees)Enum.Parse(typeof(RegistryTrees), registryRoot);
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Exception caught in GetKeysWithRegularExpression.\n{1}",
                                      m_taskId,
                                      ex.ToString());
                resultCode = ResultCodes.RC_LOCAL_SCRIPT_PROCESSING_ERROR;
            }

            if (ResultCodes.RC_SUCCESS == resultCode) {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);

                ManagementBaseObject outputParameters = null;

                resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.ENUM_KEY,
                                                      registryPath,
                                                      inputParameters,
                                                      out outputParameters);

                if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                    string[] subKeys = null;
                    using (outputParameters) {
                        subKeys = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                    }
                    if (null != subKeys && 0 < subKeys.Length) {
                        Regex rx = new Regex(regexPattern);
                        foreach (string subKey in subKeys) {
                            String keyPath = String.Empty;
                            if (!registryPath.EndsWith(@"\")) {
                                keyPath = registryPath + @"\" + subKey;
                            } else {
                                keyPath = registryPath + subKey;
                            }
                            if (rx.IsMatch(subKey)) {
                                if (string.IsNullOrEmpty(registryEndPath)) {
                                    registryData.AppendLine()
                                                .Append('[')
                                                .Append(registryRoot)
                                                .Append(@"\")
                                                .Append(TranslateWow6432RegistryPath(keyPath))
                                                .AppendLine(@"]");
                                    resultCode = GetValues(wmiRegistry,
                                                           registryRoot,
                                                           keyPath,
                                                           registryData);
                                } else {
                                    keyPath += @"\" + registryEndPath;
                                }

                                if (ResultCodes.RC_SUCCESS == resultCode) {
                                    resultCode = GetKeys(wmiRegistry,
                                                         registryRoot,
                                                         keyPath,
                                                         registryData);
                                }
                            }
                        

                            if (ResultCodes.RC_SUCCESS != resultCode) {
                                break;
                            }
                        }
                    }
                }
            }
            return resultCode;
        }

        private ResultCodes GetValues(
                ManagementClass wmiRegistry,
                string registryRoot,
                string registryPath,
                StringBuilder registryData) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            RegistryTrees rt = RegistryTrees.HKEY_INVALID;

            try {
                rt = (RegistryTrees)Enum.Parse(typeof(RegistryTrees), registryRoot);
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Exception caught in GetValues.\n{1}",
                                      m_taskId,
                                      ex.ToString());
                resultCode = ResultCodes.RC_LOCAL_SCRIPT_PROCESSING_ERROR;
            }

            if (ResultCodes.RC_SUCCESS == resultCode) {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_VALUES);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.ENUM_VALUES,
                                                      registryPath,
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
                        for (int i = 0;
                             valueTypes.Length > i;
                             ++i) {

                            if (String.IsNullOrEmpty(valueNames[i])) {
                                registryData.Append(@"@=");
                            } else {
                                registryData.Append('"')
                                            .Append(valueNames[i].Replace(@"\", @"\\"))
                                            .Append("\"=");
                            }

                            switch ((RegistryTypes)valueTypes[i]) {
                                case RegistryTypes.REG_SZ:
                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                    resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                                          wmiRegistry,
                                                                          RegistryMethodNames.GET_STRING_VALUE,
                                                                          registryPath,
                                                                          inputParameters,
                                                                          out outputParameters);
 
                                   if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        using (outputParameters) {
                                            string szValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                            registryData.Append('"').Append(szValue).Append('"');
                                        }
                                    }
                                    break;

                                case RegistryTypes.REG_EXPAND_SZ:
                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_EXPANDED_STRING_VALUE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);
                                    resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                                          wmiRegistry,
                                                                          RegistryMethodNames.GET_EXPANDED_STRING_VALUE,
                                                                          registryPath,
                                                                          inputParameters,
                                                                          out outputParameters);

                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        using (outputParameters) {
                                            string eszValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                            registryData.Append('"').Append(eszValue).Append('"');
                                        }
                                    }
                                    break;

                                case RegistryTypes.REG_BINARY:
                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_BINARY_VALUE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);
                                    resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                                          wmiRegistry,
                                                                          RegistryMethodNames.GET_BINARY_VALUE,
                                                                          registryPath,
                                                                          inputParameters,
                                                                          out outputParameters);
 
                                   if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        using (outputParameters) {
                                            byte[] bValue = outputParameters.GetPropertyValue(RegistryPropertyNames.U_VALUE) as byte[];
                                            registryData.Append(@"hex:");

                                           if (null != bValue && 0 < bValue.Length) {
                                                IEnumerator<byte> e = ((IList<byte>)bValue).GetEnumerator();
                                                e.MoveNext();
                                                registryData.Append(e.Current.ToString("x2"));
 
                                                while (e.MoveNext()) {
                                                    registryData.Append(',')
                                                                .Append(e.Current.ToString("x2"));
                                                }
                                            }
                                        }
                                    }
                                    break;

                                case RegistryTypes.REG_DWORD:
                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_DWORD_VALUE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                    resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                                          wmiRegistry,
                                                                          RegistryMethodNames.GET_DWORD_VALUE,
                                                                          registryPath,
                                                                          inputParameters,
                                                                          out outputParameters);

                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        using (outputParameters) {
                                            object dwValue = outputParameters.GetPropertyValue(RegistryPropertyNames.U_VALUE);
                                            if (null != dwValue) {
                                                registryData.Append(@"dword:")
                                                            .Append(((uint)dwValue).ToString("x8"));
                                            }
                                        }
                                    }
                                    break;

                                case RegistryTypes.REG_MULTI_SZ:
                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_MULTI_STRING_VALUE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                    resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                                          wmiRegistry,
                                                                          RegistryMethodNames.GET_MULTI_STRING_VALUE,
                                                                          registryPath,
                                                                          inputParameters,
                                                                          out outputParameters);

                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        using (outputParameters) {
                                            string[] mszValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string[];
                                            registryData.Append('"')
                                                        .Append((null != mszValue) ? String.Join(@",", mszValue) : String.Empty)
                                                        .Append('"');
                                        }
                                    }
                                    break;
                            }
                            registryData.AppendLine();
                        }
                    } else {
                        inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, rt);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, registryPath);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, String.Empty);

                        resultCode = Lib.InvokeRegistryMethod(m_taskId,
                                                              wmiRegistry,
                                                              RegistryMethodNames.GET_STRING_VALUE,
                                                              registryPath,
                                                              inputParameters,
                                                              out outputParameters);

                        if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                            using (outputParameters) {
                                string defaultKeyValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                registryData.Append("@=\"")
                                            .Append(defaultKeyValue)
                                            .AppendLine(@"""");
                            }
                        }
                    }
                }
            }
            return resultCode;
        }

        private string m_taskId;
        private static readonly string s_regexPatternBeginTag = @"<bdna_regex>";
        private static readonly string s_regexPatternEndTag = @"</bdna_regex>";
        private static readonly string s_registrySoftwarePath6432 = @"SOFTWARE\Wow6432Node\";
        private static readonly string s_registrySoftwarePath = @"SOFTWARE\";

        /// <summary>
        /// Delimiter used to strip bogus data from the beginning
        /// of some collection parameter set table entries.
        /// </summary>
        private static readonly char[] s_collectionParameterSetDelimiter = new char[] { ':' };
    }
}