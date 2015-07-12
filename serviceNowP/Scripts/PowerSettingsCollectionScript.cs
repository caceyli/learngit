#region Copyright
/******************************************************************
*
*          Module: Windows Power Settings Collection Scripts
* Original Author: Hansen
*   Creation Date: 2006/09/09
*
* Current Status
*       $Revision: 1.11 $
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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Management;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts {

    /// <summary>
    /// Collection script to gather Windows Power Settings.
    /// </summary>
    public class PowerSettingsCollectionScript : ICollectionScriptRuntime {
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
            string m_collectedData = "";
            try {
                ManagementScope cimvScope = null;
                ManagementScope defaultScope = null;

                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to PowerSettingsCollectionScript is null.",
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
                        m_wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null);
                        using (m_wmiRegistry) {
                            m_collectedData = GetPowerInfo(m_wmiRegistry);
                        }

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"powerSettingsDetails"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"powerSettingsDetails")
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
                                 "Unhandled exception in PowerSettingsCollectionScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script PowerSettingsCollectionScript.  Elapsed time {1}.  Result code {2}.",
                                  m_taskId,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }
        #endregion

        private string m_taskId;
        private ManagementClass m_wmiRegistry = null;

        private const uint HKEY_CLASSES_ROOT = 0x80000000;
        private const uint HKEY_CURRENT_USER = 0x80000001;
        private const uint HKEY_LOCAL_MACHINE = 0x80000002;
        private const uint HKEY_USERS = 0x80000003;
        private const uint HKEY_CURRENT_CONFIG = 0x80000005;
        private const uint HKEY_DYN_DATA = 0x80000006;

        private Dictionary<string, string> ProcessorDic = new Dictionary<string, string>();

        /*
         *  It is entrance to get power settings.
         *  <param name="host">You assigned a destination host.</param>
         *  <param name="user">Username in a domain.</param>
         *  <param name="passowrd">Password of the user.</param>
         * 
         * */
        private string GetPowerInfo(ManagementClass _wmiRegistry) {
            string powerInfo = "";
            string osName = "XP";
            try {
                InitProcessorDic();
                //Get OS Name
                ManagementClass wmiRegistry = _wmiRegistry;
                ManagementBaseObject inParams = wmiRegistry.GetMethodParameters("GetStringValue");

                inParams["hDefKey"] = HKEY_LOCAL_MACHINE;
                inParams["sSubKeyName"] = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
                inParams["sValueName"] = "ProductName";

                ManagementBaseObject outParams0 = wmiRegistry.InvokeMethod("GetStringValue", inParams, null);
                if (Convert.ToUInt32(outParams0["ReturnValue"]) == 0) {
                    osName = outParams0["sValue"].ToString();
                }

                if (osName.Contains("2000")) {
                    powerInfo = GetWindows2000Power(wmiRegistry);
                } else if (osName.Contains("XP") || osName.Contains("2003")) {
                    powerInfo = GetWindowsXPPower(wmiRegistry);
                } else if (osName.Contains("Vista") || osName.Contains("2008")) {
                    powerInfo = GetWindows6Power(wmiRegistry);
                }
                return powerInfo;
            } catch (Exception e) {
                //MessageBox.Show(e.ToString());
                return powerInfo;
            }
        }

        private void InitProcessorDic() {
            ProcessorDic.Clear();
            ProcessorDic.Add("00", "NONE");
            ProcessorDic.Add("01", "CONSTANT");
            ProcessorDic.Add("02", "DEGRADE");
            ProcessorDic.Add("03", "ADAPTIVE");
        }

        private string getProcessorThrottle(string key) {
            if (this.ProcessorDic.ContainsKey(key)) {
                return ProcessorDic[key];
            } else {
                return "Not Supported";
            }

        }

        private string ReverseString(string value) {
            string newValue = "";
            if (value != null) {
                char[] array = value.ToCharArray();
                int len = array.Length;
                for (int i = 0; i < len; i++) {
                    if (i % 2 != 0) {
                        char tempc = array[i - 1];
                        array[i - 1] = array[i];
                        array[i] = tempc;
                    }
                }
                for (int i = len - 1; i >= 0; i--) {
                    newValue = newValue + array[i];
                }
            }
            return newValue;
        }

        private int ToInt32(string hexString) {
            return Convert.ToInt32(hexString, 16);
        }

        private string ToValue(string hexString) {
            int intValue = Convert.ToInt32(hexString, 16) / 60;
            if (intValue == 0) {
                return "\"Never\"";
            } else {
                return "\"After " + intValue.ToString() + " mins\"";
            }
        }

        private string IntToValue(string intString) {
            int intValue = Convert.ToInt32(intString) / 60;
            if (intValue == 0) {
                return "\"Never\"";
            } else {
                return "\"After " + intValue.ToString() + " mins\"";
            }
        }

        private string GetWindows2000Power(ManagementClass wmiRegistry) {
            string powerInfo = "";
            string policy = "0";
            try {
                ManagementBaseObject inParams = wmiRegistry.GetMethodParameters("GetStringValue");

                inParams["hDefKey"] = HKEY_CURRENT_USER;
                inParams["sSubKeyName"] = @"Control Panel\PowerCfg";
                inParams["sValueName"] = "CurrentPowerPolicy";

                ManagementBaseObject outParams = wmiRegistry.InvokeMethod("GetStringValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    policy = outParams["sValue"].ToString();
                    inParams["hDefKey"] = HKEY_CURRENT_USER;
                    inParams["sSubKeyName"] = @"Control Panel\PowerCfg\PowerPolicies\" + policy;
                    inParams["sValueName"] = "Policies";

                    outParams = wmiRegistry.InvokeMethod("GetBinaryValue", inParams, null);
                    if (Convert.ToUInt32(outParams["ReturnValue"]) >= 0) {
                        byte[] array = outParams["uValue"] as byte[];
                        string value = "";
                        for (int i = 0; i < array.Length; i++) {
                            value = value + String.Format("{0:X2}", array[i]);
                            int valueLen = value.Length;
                            if ((i + 1) % 8 == 0) {
                                int rowCount = (i + 1) / 8;
                                if (rowCount == 4) {
                                    powerInfo = powerInfo + "" + "Standby_AC=" + ToValue(ReverseString(value.Substring(8, 8)));
                                } else if (rowCount == 5) {
                                    powerInfo = powerInfo + "<BDNA,1>" + "Standby_DC=" + ToValue(ReverseString(value.Substring(0, 8)));
                                    if (valueLen == 16) {
                                        powerInfo = powerInfo + "<BDNA,1>" + "Processor_Throttle_AC=" + getProcessorThrottle(ReverseString(value.Substring(12, 2)));
                                        powerInfo = powerInfo + "<BDNA,1>" + "Processor_Throttle_DC=" + getProcessorThrottle(ReverseString(value.Substring(14, 2)));
                                    } else {
                                        powerInfo = powerInfo + "<BDNA,1>" + "Processor_Throttle_AC=Invalid Data:" + value;

                                    }
                                } else if (rowCount == 8) {
                                    if (valueLen == 16) {
                                        powerInfo = powerInfo + "<BDNA,1>" + "Monitor_AC=" + ToValue(ReverseString(value.Substring(0, 8)));
                                        powerInfo = powerInfo + "<BDNA,1>" + "Monitor_DC=" + ToValue(ReverseString(value.Substring(8, 8)));
                                    } else {
                                        powerInfo = powerInfo + "<BDNA,1>" + "Monitor_AC=Invalid Data:" + value;

                                    }
                                } else if (rowCount == 9) {
                                    if (valueLen == 16) {
                                        powerInfo = powerInfo + "<BDNA,1>" + "Disk_AC=" + ToValue(ReverseString(value.Substring(0, 8)));
                                        powerInfo = powerInfo + "<BDNA,1>" + "Disk_DC=" + ToValue(ReverseString(value.Substring(8, 8)));
                                    } else {
                                        powerInfo = powerInfo + "<BDNA,1>" + "Disk_AC=Invalid Data:" + value;

                                    }
                                }
                                value = "";
                            }
                        }
                    }
                } else {
                    powerInfo = powerInfo + "\r\n" + "Error retrieving value :" + outParams["ReturnValue"].ToString();
                }
                return powerInfo;
            } catch (Exception e) {
                powerInfo = powerInfo + "  " + e.ToString();
                return powerInfo;
            } finally {

            }
        }

        private string GetWindowsXPPower(ManagementClass wmiRegistry) {
            string powerInfo = "";
            Dictionary<string, string> resultDic = new Dictionary<string, string>();
            try {
                ManagementBaseObject inParams = wmiRegistry.GetMethodParameters("GetStringValue");
                inParams["hDefKey"] = HKEY_USERS;
                inParams["sSubKeyName"] = @"";

                ManagementBaseObject outParams = wmiRegistry.InvokeMethod("EnumKey", inParams, null);
                string[] valueNames = (string[])outParams["sNames"];
                foreach (string name in valueNames) {

                    if (!name.EndsWith("Classes")) {
                        powerInfo = "";
                        inParams["hDefKey"] = HKEY_USERS;
                        inParams["sSubKeyName"] = name + @"\Identities";
                        inParams["sValueName"] = "Last User ID";
                        outParams = wmiRegistry.InvokeMethod("GetStringValue", inParams, null);
                        if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                            string lastUserID = outParams["sValue"].ToString();
                            ///////////////////////Get Power Policy/////////////////////////////
                            inParams["hDefKey"] = HKEY_USERS;
                            inParams["sSubKeyName"] = name + @"\Control Panel\PowerCfg";
                            inParams["sValueName"] = "CurrentPowerPolicy";

                            outParams = wmiRegistry.InvokeMethod("GetStringValue", inParams, null);
                            string policy = outParams["sValue"].ToString();
                            //MessageBox.Show(lastUserID + "==" + policy);
                            ///////////////////////Get Power Value(Binary)/////////////////////////////
                            inParams["hDefKey"] = HKEY_USERS;
                            inParams["sSubKeyName"] = name + @"\Control Panel\PowerCfg\PowerPolicies\" + policy;
                            inParams["sValueName"] = "Policies";

                            outParams = wmiRegistry.InvokeMethod("GetBinaryValue", inParams, null);
                            if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                                byte[] array = outParams["uValue"] as byte[];
                                string value = "";
                                for (int i = 0; i < array.Length; i++) {
                                    value = value + String.Format("{0:X2}", array[i]);
                                    if ((i + 1) % 8 == 0) {
                                        int rowCount = (i + 1) / 8;
                                        if (rowCount == 4) {
                                            powerInfo = powerInfo + "" + "Standby_AC=" + ToValue(ReverseString(value.Substring(8, 8)));
                                        } else if (rowCount == 5) {
                                            powerInfo = powerInfo + "<BDNA,1>" + "Standby_DC=" + ToValue(ReverseString(value.Substring(0, 8)));
                                            powerInfo = powerInfo + "<BDNA,1>" + "Processor_Throttle_AC=" + getProcessorThrottle(ReverseString(value.Substring(12, 2)));
                                            powerInfo = powerInfo + "<BDNA,1>" + "Processor_Throttle_DC=" + getProcessorThrottle(ReverseString(value.Substring(14, 2)));
                                        } else if (rowCount == 8) {
                                            powerInfo = powerInfo + "<BDNA,1>" + "Monitor_AC=" + ToValue(ReverseString(value.Substring(0, 8)));
                                            powerInfo = powerInfo + "<BDNA,1>" + "Monitor_DC=" + ToValue(ReverseString(value.Substring(8, 8)));
                                        } else if (rowCount == 9) {
                                            powerInfo = powerInfo + "<BDNA,1>" + "Disk_AC=" + ToValue(ReverseString(value.Substring(0, 8)));
                                            powerInfo = powerInfo + "<BDNA,1>" + "Disk_DC=" + ToValue(ReverseString(value.Substring(8, 8)));
                                        }
                                        value = "";
                                    }
                                }
                            }
                            if (!resultDic.ContainsKey(lastUserID)) {
                                resultDic.Add(lastUserID, powerInfo);
                            }
                        }
                    }
                }
                int resultCount = resultDic.Count;
                foreach (string key in resultDic.Keys) {
                    if ("{00000000-0000-0000-0000-000000000000}".Equals(key) && resultCount == 1) {
                        return resultDic["{00000000-0000-0000-0000-000000000000}"];
                    } else if ("{00000000-0000-0000-0000-000000000000}".Equals(key) && resultCount > 1) {
                        continue;
                    } else {
                        return resultDic[key];
                    }
                }
                return powerInfo;
            } catch (Exception e) {
                return powerInfo;
            } finally {

            }
        }

        private string GetWindows6Power(ManagementClass wmiRegistry) {
            string powerInfo = "";
            string activePowerScheme = "";

            try {
                ManagementBaseObject inParams = wmiRegistry.GetMethodParameters("GetStringValue");
                inParams["hDefKey"] = HKEY_LOCAL_MACHINE;
                inParams["sSubKeyName"] = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
                inParams["sValueName"] = "ActivePowerScheme";

                ManagementBaseObject outParams = wmiRegistry.InvokeMethod("GetStringValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    activePowerScheme = outParams["sValue"].ToString();
                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }

                if ("a1841308-3541-4fab-bc81-f71556f20b4a".Equals(activePowerScheme)) {
                    powerInfo = "Processor_Throttle_AC=\"Power saver\"<BDNA,1>Processor_Throttle_DC=\"Power saver\"<BDNA,1>";
                } else if ("381b4222-f694-41f0-9685-ff5bb260df2e".Equals(activePowerScheme)) {
                    powerInfo = "Processor_Throttle_AC=\"Balanced\"<BDNA,1>Processor_Throttle_DC=\"Balanced\"<BDNA,1>";
                } else if ("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c".Equals(activePowerScheme)) {
                    powerInfo = "Processor_Throttle_AC=\"High performance\"<BDNA,1>Processor_Throttle_DC=\"High performance\"<BDNA,1>";
                }
                inParams["hDefKey"] = HKEY_LOCAL_MACHINE;
                inParams["sSubKeyName"] = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme + @"\7516b95f-f776-4464-8c53-06167f40cc99\3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";
                inParams["sValueName"] = "ACSettingIndex";

                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "Monitor_AC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }
                inParams["sValueName"] = "DCSettingIndex";
                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Monitor_DC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }

                inParams["hDefKey"] = HKEY_LOCAL_MACHINE;
                inParams["sSubKeyName"] = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme + @"\0012ee47-9041-4b5d-9b77-535fba8b1442\6738e2c4-e8a5-4a42-b16a-e040e769756e";
                inParams["sValueName"] = "ACSettingIndex";

                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Disk_AC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }
                inParams["sValueName"] = "DCSettingIndex";
                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Disk_DC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }

                inParams["hDefKey"] = HKEY_LOCAL_MACHINE;
                inParams["sSubKeyName"] = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme + @"\238C9FA8-0AAD-41ED-83F4-97BE242C8F20\29f6c1db-86da-48c5-9fdb-f2b67b1f44da";
                inParams["sValueName"] = "ACSettingIndex";

                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Standby_AC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }
                inParams["sValueName"] = "DCSettingIndex";

                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Standby_DC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }

                inParams["hDefKey"] = HKEY_LOCAL_MACHINE;
                inParams["sSubKeyName"] = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme + @"\238C9FA8-0AAD-41ED-83F4-97BE242C8F20\9d7815a6-7ee4-497e-8888-515a05f02364";
                inParams["sValueName"] = "ACSettingIndex";

                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Hibernate_AC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }
                inParams["sValueName"] = "DCSettingIndex";
                outParams = wmiRegistry.InvokeMethod("GetDWORDValue", inParams, null);
                if (Convert.ToUInt32(outParams["ReturnValue"]) == 0) {
                    powerInfo = powerInfo + "" + "<BDNA,1>Hibernate_DC=" + IntToValue(string.Format("{0:X2}", outParams["uValue"].ToString()));

                } else {
                    Console.WriteLine("Error retrieving value : " + outParams["ReturnValue"].ToString());
                }

                return powerInfo;
            } catch (Exception e) {
                //MessageBox.Show(e.ToString());
            }

            return powerInfo;
        }

    }
}
