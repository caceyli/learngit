#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Mike Frost
*   Creation Date: 2006/01/17
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
using bdna.ScriptLib;
using bdna.Shared;
using System.Text.RegularExpressions;

namespace bdna.Scripts {

    /// <summary>
    /// Broad spectrum collection script for grabbing the bulk of
    /// level two data for Windows.
    /// </summary>
    public class HyperVServerStaticScript : ICollectionScriptRuntime {

        #region ICollectionScriptRuntime
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
            string taskIdString = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            bool cleanupProfile = false;
            string profileDirectory = null;
            ManagementScope cimvScope = null;
            ManagementScope defaultScope = null;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script HyperVServerStaticScript.",
                                  taskIdString);

            try {
                if (null == connection) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to HyperVServerStaticScript is null.",
                                          taskIdString);

                } else if (!connection.ContainsKey(@"cimv2")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          taskIdString);
                } else if (!connection.ContainsKey(@"default")) {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for Default namespace is not present in connection object.",
                                          taskIdString);
                } else {
                    cimvScope = connection[@"cimv2"] as ManagementScope;
                    defaultScope = connection[@"default"] as ManagementScope;

                    if (!cimvScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              taskIdString);
                    } else if (!defaultScope.IsConnected) {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to Default namespace failed",
                                              taskIdString);
                    } else {
                        if (Lib.EnableProfileCleanup) {
                            string userName = connection[@"userName"] as string;
                            Debug.Assert(null != userName);
                            int domainSeparatorPosition = userName.IndexOf('\\');
                            if (-1 != domainSeparatorPosition) {
                                userName = userName.Substring(domainSeparatorPosition + 1);
                            }
                            profileDirectory = @"c:\Documents and Settings\" + userName;
                            cleanupProfile = !Lib.ValidateDirectory(taskIdString, profileDirectory, cimvScope);
                        }
                        IDictionary<string, string> queryResults = new Dictionary<string, string>();

                        //
                        // Loop through the WMI query table and perform
                        // each query.
                        foreach (CimvQueryTableEntry cqte in s_cimvQueryTable) {
                            //
                            // we currently ignore the result code from the wmi
                            // queries and simply return whatever data we managed
                            // to get.
                            /*resultCode = */
                            cqte.ExecuteQuery(taskIdString, cimvScope, queryResults);
                        }

                        using (ManagementClass wmiRegistry = new ManagementClass(defaultScope, new ManagementPath(@"StdRegProv"), null)) {
                            Debug.Assert(null != wmiRegistry);
                            //
                            // Get Installed Software.
                            //GetInstalledHotfix(taskIdString, cimvScope, wmiRegistry, queryResults);

                            //
                            // Loop through the invocation list for all of
                            // the registry queries and call each one.
                            foreach (RegistryQuery rq in s_registryDelegates.GetInvocationList()) {
                                //
                                // we currently ignore the result code from the registry
                                // queries and simply return whatever data we managed
                                // to get.
                                /*resultCode = */rq(taskIdString, wmiRegistry, queryResults);
                            }
                            GetWindowsMonitors(taskIdString, cimvScope, wmiRegistry, queryResults);
                            GetUserDesktopSettings(taskIdString, cimvScope, queryResults);
                        }

                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            //
                            // Build up the data row prefix
                            dataRow.Append(elementId)
                                   .Append(',')
                                   .Append(attributes[@"systemData"])
                                   .Append(',')
                                   .Append(scriptParameters[@"CollectorId"])
                                   .Append(',')
                                   .Append(taskId)
                                   .Append(',')
                                   .Append(databaseTimestamp + executionTimer.ElapsedMilliseconds)
                                   .Append(',')
                                   .Append(@"systemData")
                                   .Append(',')
                                   .Append(BdnaDelimiters.BEGIN_TAG);

                            //
                            // Build up the data row from all of the collected
                            // data row entries.
                            if (0 < queryResults.Count) {
                                IEnumerator<KeyValuePair<string, string>> e = queryResults.GetEnumerator();
                                e.MoveNext();
                                KeyValuePair<string, string> kvp = e.Current;
                                dataRow.Append(kvp.Key)
                                       .Append('=')
                                       .Append(kvp.Value);

                                while (e.MoveNext()) {
                                    kvp = e.Current;
                                    dataRow.Append(BdnaDelimiters.DELIMITER_TAG)
                                           .Append(kvp.Key)
                                           .Append('=')
                                           .Append(kvp.Value);
                                }
                            }

                            //
                            // Add the end tag and it's done.
                            dataRow.Append(BdnaDelimiters.END_TAG);
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.LogException(taskIdString,
                                 executionTimer,
                                 "Unhandled exception in HyperVServerStaticScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            if (cleanupProfile && null != profileDirectory && null != cimvScope) {
                uint wmiMethodResultCode = 0;
                Lib.DeleteDirectory(taskIdString, profileDirectory, cimvScope, out wmiMethodResultCode);
            }


            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script HyperVServerStaticScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());

            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString().Replace("\n","").Replace("\r",""));
        }
        #endregion ICollectionScriptRuntime

        /// <summary>
        /// Static initializer to build up the map of 
        /// registry printer value names to data row item
        /// names.
        /// </summary>
        static HyperVServerStaticScript() {
            s_printerKeyNameToRowNameMap[@"Name"] = @"name";
            s_printerKeyNameToRowNameMap[@"Port"] = @"port";
            s_printerKeyNameToRowNameMap[@"Share Name"] = @"sharedName";
            s_printerKeyNameToRowNameMap[@"Status"] = @"statusBits";
            s_printerKeyNameToRowNameMap[@"Attributes"] = @"attributeBits";
            s_printerKeyNameToRowNameMap[@"Printer Driver"] = @"driver";
        }

        /// <summary>
        /// Registry query to get Windows make and model info.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetWindowsProductType(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            try {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, s_registryKeyProductOptions);
                inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, @"productType");

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.GET_STRING_VALUE,
                                                      s_registryKeyProductOptions,
                                                      inputParameters,
                                                      out outputParameters);

                if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                    using (outputParameters) {
                        string s = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                        if (!String.IsNullOrEmpty(s)) {
                            if (s.Equals(@"WinNT")) {
                                s = @"Workstation";
                            } else if (s.Equals(@"ServerNT")) {
                                s = @"Standalone Server";
                            } else if (s.Equals(@"LanmanNT")) {
                                s = @"Domain Controller";
                            }
                            queryResults[@"operatingSystem.productType"] = s;
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetWindowsProductType failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsProductType failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Handler for the Win32_DesktopMonitor query.  
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void OsMonitorResultHandler(
                string                                           taskID,
                ManagementClass                                  wmiRegistry,
                ManagementObjectCollection                       queryResults,
                string[]                                         propertyMap,
                IDictionary<string, IDictionary<string, string>> monitorProperties) {

            StringBuilder builder = new StringBuilder();
            try {
                foreach (ManagementObject mo in queryResults) {
                    IDictionary<string, string> monitorDetails = new Dictionary<string, string>();

                    monitorDetails[@"ModelName"] = mo.Properties[@"Caption"].Value as string;
                    //monitorDetails[@"Description"] = mo.Properties[@"Description"].Value as string;
                    //monitorDetails[@"DeviceID"] = mo.Properties[@"DeviceID"].Value as string;
                    monitorDetails[@"ManufacturerID"] = mo.Properties[@"MonitorManufacturer"].Value as string;
                    monitorDetails[@"MonitorType"] = mo.Properties[@"MonitorType"].Value as string;
                    monitorDetails[@"PixelsPerXInch"] = String.Format("{0}", mo.Properties[@"PixelsPerXLogicalInch"].Value);
                    monitorDetails[@"PixelsPerYInch"] = String.Format("{0}", mo.Properties[@"PixelsPerYLogicalInch"].Value);
                    monitorDetails[@"ScreenWidth"] = String.Format("{0}", mo.Properties[@"ScreenWidth"].Value);
                    monitorDetails[@"ScreenHeight"] = String.Format("{0}", mo.Properties[@"ScreenHeight"].Value);
                    
                    int status = 0;
                    try {
                        string av = String.Format("{0}", mo.Properties[@"Availability"].Value);

                        if (mo.Properties[@"Availability"].Value != null) {
                            if (!int.TryParse(av, out status)) {
                                throw (new Exception("Number parsing exception: monitor availability string"));
                            }
                        }
                    } catch (Exception ex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Collection script HyperVServerStaticScript. Unexpected exception. {0}",
                                              ex.Message);
                    } finally {
                        switch (status) {
                            case 1: monitorDetails[@"Availability"] = @"Other"; break;
                            case 2: monitorDetails[@"Availability"] = @"Unknown"; break;
                            case 3: monitorDetails[@"Availability"] = @"Running or Full Power"; break;
                            case 4: monitorDetails[@"Availability"] = @"Warning"; break;
                            case 5: monitorDetails[@"Availability"] = @"In Test"; break;
                            case 6: monitorDetails[@"Availability"] = @"Not Applicable"; break;
                            case 7: monitorDetails[@"Availability"] = @"Power Off"; break;
                            case 8: monitorDetails[@"Availability"] = @"Off Line"; break;
                            case 9: monitorDetails[@"Availability"] = @"Off Duty"; break;
                            case 10: monitorDetails[@"Availability"] = @"Degraded"; break;
                            case 11: monitorDetails[@"Availability"] = @"Not Installed"; break;
                            case 12: monitorDetails[@"Availability"] = @"Install Error"; break;
                            case 13: monitorDetails[@"Availability"] = @"Power Save - Unknown"; break;
                            case 14: monitorDetails[@"Availability"] = @"Power Save - Low Power Mode"; break;
                            case 15: monitorDetails[@"Availability"] = @"Power Save - Standby"; break;
                            case 16: monitorDetails[@"Availability"] = @"Power Cycle"; break;
                            case 17: monitorDetails[@"Availability"] = @"Power Save - Warning"; break;
                            default: monitorDetails[@"Availability"] = @"Unknown Status"; break;
                        }
                    }
                    
                    int powerMgtCapabilities = 0;
                    try {
                        if (mo.Properties[@"PowerManagementCapabilities"].Value != null || !"".Equals(mo.Properties[@"PowerManagementCapabilities"].Value.ToString())) {
                            if (!int.TryParse(String.Format(@"{0}", mo.Properties[@"PowerManagementCapabilities"].Value), out powerMgtCapabilities)) {
                                throw (new Exception("Number parsing exception: monitor PowerManagementCapabilities string"));
                            }
                        }
                    } catch (Exception ex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Collection script WindowsStaticScript. Unexpected exception. {0}",
                                              ex.Message);

                    } finally {
                        switch (powerMgtCapabilities) {
                            case 0: monitorDetails[@"PowerSavingMgmt"] = @"Unknown"; break;
                            case 1: monitorDetails[@"PowerSavingMgmt"] = @"Not Supported"; break;
                            case 2: monitorDetails[@"PowerSavingMgmt"] = @"Disabled"; break;
                            case 3: monitorDetails[@"PowerSavingMgmt"] = @"Enabled"; break;
                            case 4: monitorDetails[@"PowerSavingMgmt"] = @"Power Saving Modes Entered Automatically"; break;
                            case 5: monitorDetails[@"PowerSavingMgmt"] = @"Power State Settable"; break;
                            case 6: monitorDetails[@"PowerSavingMgmt"] = @"Power Cycling Supported"; break;
                            case 7: monitorDetails[@"PowerSavingMgmt"] = @"Timed Power-On Supported"; break;
                            default: monitorDetails[@"PowerSavingMgmt"] = @"Unknown"; break;
                        }
                    }
                    //
                    // Real physical monitor should have PnP device ID.
                    monitorDetails[@"PNPDeviceID"] = mo.Properties[@"PNPDeviceID"].Value as string;
                    if (!string.IsNullOrEmpty(monitorDetails[@"PNPDeviceID"])) {
                        string monitorID = monitorDetails[@"PNPDeviceID"];
                        int index = monitorID.LastIndexOf(@"\");
                        if (index > 0) {
                            monitorID = monitorID.Substring(0, index);
                        }
                        if (!monitorProperties.ContainsKey(monitorID)) {
                            if (ValidateMonitorHardwareId(taskID, 
                                                          wmiRegistry, 
                                                          monitorDetails[@"PNPDeviceID"])) {
                                if (monitorID != "DISPLAY\\NEC61BE") {
                                    monitorProperties.Add(monitorID, monitorDetails);
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Collection script WindowsStaticScript. Unexpected exception. {0}",
                                      ex.Message);
            }
        }

        /// <summary>
        /// Validate registry path exists.
        /// </summary>
        /// <param name="taskId">Task ID</param>
        /// <param name="wmiRegistry">WMI Registry</param>
        /// <param name="registryKey">Key path</param>
        /// <returns>True if registry path is valid; false otherwise.</returns>
        private static bool ValidateMonitorHardwareId(string taskID,
                                                     ManagementClass wmiRegistry,
                                                     string hardwareIdKeyPath) {
            try {
                string[] keys = null;
                if (ResultCodes.RC_SUCCESS == Lib.GetRegistrySubkeyName(taskID,
                                                                        wmiRegistry,
                                                                        s_registryKeyMonitors + @"\" + hardwareIdKeyPath,
                                                                        out keys)) {
                    if (keys != null) {
                        foreach (string key in keys) {
                            if (key == @"Device Parameters") {
                                return true;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Exception received validating monitor hardware ID: {0} with exception: {1}.",
                                      hardwareIdKeyPath,
                                      ex.Message);
            }
            return false;
        }
                                     
        /// <summary>
        /// Provided Device ID key path, it will parse and return corresponding monitor information
        /// </summary>
        /// <param name="taskId">Task ID</param>
        /// <param name="wmiRegistry">Registry</param>
        /// <param name="hardwareIdKeyPath">Key Path</param>
        /// <param name="monitorDetails">Dictionary of monitor details</param>
        /// <returns></returns> = 
        private static ResultCodes GetWindowsMonitorEDIDInfo(string taskId,
                                                             ManagementClass wmiRegistry,
                                                             string hardwareIdKeyPath,
                                                             IDictionary<string, string> monitorDetails) {
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            Stopwatch sw = Stopwatch.StartNew();
            if (monitorDetails == null) {
                monitorDetails = new Dictionary<string, string>();
            }
            
            try {
                byte[] EDID = null;
                resultCode = Lib.GetRegistryBinaryValue(taskId,
                                                        wmiRegistry,
                                                        s_registryKeyMonitors + @"\" + hardwareIdKeyPath + @"\Device Parameters",
                                                        @"EDID",
                                                        out EDID);
                if (resultCode == ResultCodes.RC_SUCCESS && EDID != null) {
                    if (!monitorDetails.ContainsKey(@"PNPDeviceID")) {
                        monitorDetails.Add(@"PNPDeviceID", hardwareIdKeyPath);
                    }

                    // EDID Version
                    string EDID_version = String.Format("{0:X}.{1:X}", EDID[18], EDID[19]);
                    monitorDetails[@"EDID_Version"] = EDID_version;
                    if (EDID[18] >= (byte)2) return resultCode;  // Cannot parse EDID version 2.0 or above

                    // Manufacturer ID
                    byte char1 = 0, char2 = 0, char3 = 0;
                    byte byte1 = EDID[8];
                    byte byte2 = EDID[9];
                    if ((byte1 & 64) > 0) {
                        char1 += 16;
                    }
                    if ((byte1 & 32) > 0) {
                        char1 += 8;
                    }

                    if ((byte1 & 16) > 0) {
                        char1 += 4;
                    }
                    if ((byte1 & 8) > 0) {
                        char1 += 2;
                    }
                    if ((byte1 & 4) > 0) {
                        char1 += 1;
                    }

                    if ((byte1 & 2) > 0) {
                        char2 += 16;
                    }
                    if ((byte1 & 1) > 0) {
                        char2 += 8;
                    }

                    if ((byte2 & 128) > 0) {
                        char2 += 4;
                    }
                    if ((byte2 & 64) > 0) {
                        char2 += 2;
                    }
                    if ((byte2 & 32) > 0) {
                        char2 += 1;
                    }

                    char3 += (byte)(byte2 & (byte)16);
                    char3 += (byte)(byte2 & (byte)8);
                    char3 += (byte)(byte2 & (byte)4);
                    char3 += (byte)(byte2 & (byte)2);
                    char3 += (byte)(byte2 & (byte)1);

                    string manufacturerId = String.Format("{0}{1}{2}",
                                                           (char)(char1 + 64), (char)(char2 + 64), (char)(char3 + 64));
                    if (!string.IsNullOrEmpty(manufacturerId)) {
                        switch (manufacturerId) {
                            case "SAM": monitorDetails[@"ManufacturerID"] = "Samsung"; break;
                            case "DEL": monitorDetails[@"ManufacturerID"] = "Dell"; break;
                            case "VSC": monitorDetails[@"ManufacturerID"] = "Viewsonic"; break;
                            case "ACR": monitorDetails[@"ManufacturerID"] = "Acer"; break;
                            case "PGS": monitorDetails[@"ManufacturerID"] = "Princeton"; break;
                            default:
                                if (manufacturerId.EndsWith(@"_") && manufacturerId.Length > 1) {
                                    monitorDetails[@"ManufacturerID"] = manufacturerId.Substring(0, manufacturerId.Length - 1);
                                } else {
                                    monitorDetails[@"ManufacturerID"] = manufacturerId;
                                } 
                                break;
                        }
                    }

                    if (!monitorDetails.ContainsKey(@"ManufacturerID")) {
                        string manufacturerID = String.Format("0x{0:X}{1:X}", EDID[9], EDID[8]);
                        try {
                            int iManufacturerID = Convert.ToInt32(manufacturerID, 16);
                            manufacturerID = iManufacturerID.ToString() + @" (" + manufacturerID + @")";
                        } catch (Exception ex) {
                            Lib.LogException(taskId,
                                             sw,
                                             "GetWindowsMonitorEDIDInfo Method, manufacturer year number parsing exception",
                                             ex);
                        }
                        monitorDetails[@"ManufacturerID"] = manufacturerID;
                    }

                    //manufacturer year
                    try {
                        string year = String.Format("{0}", EDID[17]);
                        int iyear = Convert.ToInt32(year, 10) + 1990;
                        if (iyear <= DateTime.Today.Year) {
                            monitorDetails[@"ManufactureDate"] = iyear.ToString();
                        }
                    } catch (Exception ex) {
                        Lib.LogException(taskId,
                                         sw,
                                         "getwindowsmonitoredidinfo method, manufacturer year number parsing exception",
                                         ex);
                    }                    

                    // Product ID
                    string productID = String.Format("0x{0:X}{1:X}", EDID[11], EDID[10]);
                    try {
                        int iProductID = Convert.ToInt32(productID, 16);
                        productID = iProductID.ToString() + @" (" + productID + @")";
                    } catch (Exception ex) {
                        Lib.LogException(taskId,
                                         sw,
                                         "GetWindowsMonitorEDIDInfo Method, product ID number parsing exception",
                                         ex);
                    }
                    if (productID != "0 (0x00)") {
                        monitorDetails[@"ProductID"] = productID;
                    }

                    // Serial number
                    string serialNum = String.Format("0x{0:X}{1:X}{2:X}{3:X}", EDID[15], EDID[14], EDID[13], EDID[12]);
                    try {
                        int iSerialNum = Convert.ToInt32(serialNum, 16);
                        serialNum = iSerialNum.ToString() + @" (" + serialNum + @")";
                    } catch (Exception ex) {
                        Lib.LogException(taskId,
                                         sw,
                                         "GetWindowsMonitorEDIDInfo Method, serial number parsing exception",
                                         ex);
                    }
                    if (serialNum != @"0 (0x0000)") {
                        monitorDetails[@"HardwareNumber"] = serialNum;
                    }

                    // Monitor Model name and Serial Number
                    // Each descriptor is 18 bytes  (4 bytes header + 14 bytes value) 
                    String descriptorHeaderFormatString = @"{0}{1}{2}{3}";
                    String descriptorValueFormatString = @"{0}{1}{2}{3}{4}{5}{6}{7}{8}{9}" +
                                                    @"{10}{11}{12}{13}";
                    for (int i = 54; i <= 108; i += 18) {
                        string descriptorHeader = String.Format(descriptorHeaderFormatString,
                                                  (char)EDID[i], (char)EDID[i + 1],
                                                  (char)EDID[i + 2], (char)EDID[i + 3]);

                        string descriptorValue = String.Format(descriptorValueFormatString,
                                                 (char)EDID[i + 4], (char)EDID[i + 5], (char)EDID[i + 6],
                                                 (char)EDID[i + 7], (char)EDID[i + 8], (char)EDID[i + 9],
                                                 (char)EDID[i + 10], (char)EDID[i + 11], (char)EDID[i + 12],
                                                 (char)EDID[i + 13], (char)EDID[i + 14], (char)EDID[i + 15],
                                                 (char)EDID[i + 16], (char)EDID[i + 17]).Trim();
                        if (descriptorValue[0] == (char)(0x00)) {
                            descriptorValue = descriptorValue.Substring(1);
                        }
                        if (descriptorHeader == String.Format(descriptorHeaderFormatString,
                                                 (char)(0x00), (char)(0x00), (char)(0x00), (char)(0xFF))) {
                            string serialNumeric = String.Format(" (0x{0:X}{1:X}{2:X}{3:X})", EDID[15], EDID[14], EDID[13], EDID[12]);
                            if (descriptorValue != @"0") {
                                monitorDetails["HardwareNumber"] = descriptorValue + serialNumeric;
                            }
                        }

                        if (descriptorHeader == String.Format(descriptorHeaderFormatString,
                                                 (char)(0x00), (char)(0x00), (char)(0x00), (char)(0xFC))) {
                            if (!string.IsNullOrEmpty(monitorDetails[@"ManufacturerID"])) {
                                if (!descriptorValue.ToUpper().StartsWith(monitorDetails[@"ManufacturerID"].ToUpper())) {
                                    monitorDetails[@"ModelName"] = monitorDetails[@"ManufacturerID"] + @" " + descriptorValue;
                                } else {
                                    monitorDetails[@"ModelName"] = descriptorValue;
                                }
                            } else {
                                monitorDetails[@"ModelName"] = descriptorValue;
                            }
                        }
                    }

                    if (!monitorDetails.ContainsKey(@"ModelName") && hardwareIdKeyPath.StartsWith(@"DISPLAY\")) {
                        string name = hardwareIdKeyPath.Substring(8);
                        int index = name.IndexOf(@"\");
                        if (index > 0) {
                            monitorDetails[@"ModelName"] = name.Substring(0, index);
                        }
                    }

                    // Eight bits has input signal
                    monitorDetails[@"InputSignal"] = ((EDID[20] & (byte)128) == 128) ? @"Digial" : @"Analog";

                    // Gamma Value
                    try {
                        string displayGamma = String.Format("{0}", EDID[23]);                            
                        double iGamma = Convert.ToDouble(displayGamma)/100.0 + 1.0;
                        monitorDetails[@"GammaValue"] = String.Format("{0}", iGamma);
                    } catch (Exception ex) {
                        Lib.LogException(taskId,
                                         sw,
                                         "GetWindowsMonitorEDIDInfo Method, Gamma value parsing exception",
                                         ex);
                    }

                    //
                    //  Set Power Management Default
                    if (!monitorDetails.ContainsKey(@"PowerSavingMgmt")) {
                        monitorDetails[@"PowerSavingMgmt"] = @"Unknown";
                    }
                    byte iFeature = EDID[24];
                    StringBuilder powerFeature = new StringBuilder();
                    if ((iFeature & (byte)128) == 128) {
                        powerFeature.Append(@"Standby");
                    }
                    if ((iFeature & (byte)64) == 65) {
                        if (powerFeature.Length > 0 && !powerFeature.ToString().EndsWith(", ")) {
                            powerFeature.Append(@", ");
                        }
                        powerFeature.Append(@"Suspend");
                    }
                    if ((iFeature & (byte)64) == 65) {
                        if (powerFeature.Length > 0 && !powerFeature.ToString().EndsWith(", ")) {
                            powerFeature.Append(@", ");
                        }
                        powerFeature.Append(@"Active-off/low power");
                    }

                    if (monitorDetails[@"PowerSavingMgmt"] == @"Unknown") {
                        if (powerFeature.Length > 0) {
                            monitorDetails[@"PowerSavingMgmt"] = powerFeature.ToString();
                        }
                    }

                    if (!monitorDetails.ContainsKey(@"Availability")) {
                        monitorDetails[@"Availability"] = @"Unknown Status"; 
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetWindowsMonitorEDIDInfo failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsMonitorEDIDInfo failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Registry query to get Windows Monitor information
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetWindowsMonitors(string taskId,
                                                      ManagementScope scope,
                                                      ManagementClass wmiRegistry,
                                                      IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;

            /// <summary> Monitor properties </summary>
            IDictionary<string, IDictionary<string, string>> monitorProperties = 
                new Dictionary<string, IDictionary<string, string>>();

            string[] propertyMap = new string[] {@"Caption",
                                                 @"MonitorManufacturer",
                                                 @"MonitorType",
                                                 @"ScreenWidth",
                                                 @"ScreenHeight",
                                                 @"PixelsPerXLogicalInch",
                                                 @"PixelsPerYLogicalInch",
                                                 @"PNPDeviceID",
                                                 @"PowerManagementCapabilities",
                                                 @"PowerManagementSupported",
                                                 @"Availability"};

            ManagementObjectCollection moc = null;
            ManagementObjectSearcher mos = new ManagementObjectSearcher(scope,
                                                                        new SelectQuery(@"Win32_DesktopMonitor",
                                                                                        null,
                                                                                        propertyMap));
            EnumerationOptions enumOption = new EnumerationOptions(null, Lib.WmiMethodTimeout, Lib.WmiBlockSize, true, true, false, false, false, false, false);
            using (mos) {
                resultCode = Lib.ExecuteWqlQuery(taskId, mos, enumOption, out moc);
            }

            //
            // Retry on any failure except query timeout.
            if (ResultCodes.RC_SUCCESS != resultCode && ResultCodes.RC_WMI_QUERY_TIMEOUT != resultCode) {
                string originalQuery = mos.Query.QueryString;
                mos = new ManagementObjectSearcher(scope,
                                                   new SelectQuery(@"Win32_DesktopMonitor"));

                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Retrying failed query {1} as\n{2}",
                                      taskId,
                                      originalQuery,
                                      mos.Query.QueryString);

                using (mos) {
                    resultCode = Lib.ExecuteWqlQuery(taskId, mos, enumOption, out moc);
                }
            }
            if (null != moc) {
                using (moc) {
                    if (ResultCodes.RC_SUCCESS == resultCode) {
                        OsMonitorResultHandler(taskId, wmiRegistry, moc, propertyMap, monitorProperties);
                    }
                }
            }
          
            foreach (KeyValuePair<string, IDictionary<string, string>> kvp in monitorProperties) {
                string hardwareIdKeyPath = null;
                IDictionary<string, string> monitorDetails = kvp.Value;
                if (monitorDetails != null && monitorDetails.ContainsKey(@"PNPDeviceID")) {
                    hardwareIdKeyPath = monitorDetails[@"PNPDeviceID"];
                    GetWindowsMonitorEDIDInfo(taskId, wmiRegistry, hardwareIdKeyPath, monitorDetails);
                }                                
            }
            try {
                string[] vesaMonitorIDs = null;
                resultCode = Lib.GetRegistrySubkeyName(taskId,
                                                       wmiRegistry,
                                                       s_registryKeyMonitors + @"\DISPLAY",
                                                       out vesaMonitorIDs);
                if (resultCode == ResultCodes.RC_SUCCESS && vesaMonitorIDs != null) {

                    //
                    // Loop thru each possible monitor product IDs
                    foreach (string vesaMonitorID in vesaMonitorIDs) {
                        string vesaMonitorIdPath = s_registryKeyMonitors + @"\DISPLAY\" + vesaMonitorID;
                        string[] pnpIDs = null;
                        resultCode = Lib.GetRegistrySubkeyName(taskId,
                                                               wmiRegistry,
                                                               vesaMonitorIdPath,
                                                               out pnpIDs);
                        if (resultCode == ResultCodes.RC_SUCCESS && pnpIDs != null) {

                            //
                            // Loop thru each possible monitor Microsoft IDs
                            foreach (string pnpID in pnpIDs) {
                                string hardwareIdKeyPath = vesaMonitorIdPath + @"\" + pnpID;
                                string[] hardwareIdSubKeyPaths = null;
                                resultCode = Lib.GetRegistrySubkeyName(taskId,
                                                                       wmiRegistry,
                                                                       hardwareIdKeyPath,
                                                                       out hardwareIdSubKeyPaths);
                                if (resultCode == ResultCodes.RC_SUCCESS && hardwareIdSubKeyPaths != null) {
                                    IDictionary<string, string> monitorDetails = new Dictionary<string, string>();

                                    //
                                    // Loop thru each possible monitor record
                                    foreach (string subKey in hardwareIdSubKeyPaths) {
                                        if (subKey == "Control") {
                                            string monitorName = null, mfg = null;
                                            try {
                                                resultCode = Lib.GetRegistryStringValue(taskId,
                                                                                        wmiRegistry,
                                                                                        hardwareIdKeyPath,
                                                                                        @"DeviceDesc",
                                                                                        out monitorName);

                                                resultCode = Lib.GetRegistryStringValue(taskId,
                                                                                        wmiRegistry,
                                                                                        hardwareIdKeyPath,
                                                                                        @"Mfg",
                                                                                        out mfg);


                                                hardwareIdKeyPath = hardwareIdKeyPath.Substring(s_registryKeyMonitors.Length + 1);
                                                GetWindowsMonitorEDIDInfo(taskId, wmiRegistry, hardwareIdKeyPath, monitorDetails);
                                                if (!string.IsNullOrEmpty(monitorName)) {
                                                    if (!monitorName.Contains(@"Standard") && 
                                                        !monitorName.Contains(@"Default") && 
                                                        !monitorName.Contains(@";")) {
                                                        monitorDetails[@"ModelName"] = monitorName;
                                                    }
                                                }

                                                string monitorID = hardwareIdKeyPath;
                                                int index = monitorID.LastIndexOf(@"\");
                                                if (index > 0) {
                                                    monitorID = monitorID.Substring(0, index);
                                                }
                                                if (!monitorProperties.ContainsKey(monitorID) && 
                                                     monitorDetails.ContainsKey(@"PNPDeviceID")) {
                                                    if (monitorID != "DISPLAY\\NEC61BE") {
                                                        monitorProperties.Add(monitorID, monitorDetails);
                                                    }
                                                }
                                            } catch (ManagementException mex) {
                                                Lib.LogException(taskId,
                                                                 sw,
                                                                 "Filter out out-dated monitor registry record: 0" +
                                                                 monitorName + ", ID=" + hardwareIdKeyPath,
                                                                 mex);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }


            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetWindowsMonitors failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsMonitors failed",
                                 ex);
            } finally {
                if (monitorProperties != null && monitorProperties.Count > 0) {
                    StringBuilder builder = new StringBuilder();
                    foreach (string id in monitorProperties.Keys) {
                        IDictionary<string, string> monitorDetails = monitorProperties[id];
                        if (monitorDetails != null && monitorDetails.Count > 0) {
                            if (builder.Length > 0) {
                                builder.Append(BdnaDelimiters.DELIMITER1_TAG);
                            }
                            StringBuilder sb = new StringBuilder();
                            foreach (KeyValuePair<string, string> kvp in monitorDetails) {
                                if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value)) {
                                    if (kvp.Key != @"PNPDeviceID") {
                                        if (sb.Length > 0) {
                                            sb.Append(BdnaDelimiters.DELIMITER2_TAG);
                                        }
                                        sb.Append(kvp.Key).Append("=").Append('"').Append(kvp.Value).Append('"');                                        
                                    }
                                }
                            }
                            builder.Append(sb);
                        }
                    }
                    queryResults[@"operatingSystem.monitors"] = builder.ToString();
                }
            }
            return resultCode;
        }


        /// <summary>
        /// Registry query to get Windows last logon information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetWindowsLastLogon(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            try {
                //
                // Collecting last logon name from registry
                string lastLogonName = null, domainName = null, username = null;
                resultCode = Lib.GetRegistryStringValue(taskId, wmiRegistry, s_registryKeyWinlogon, @"DefaultUserName", out username);
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    resultCode = Lib.GetRegistryStringValue(taskId, wmiRegistry, s_registryKeyWinlogon, @"DefaultDomainName", out domainName);
                }
                if (!string.IsNullOrEmpty(domainName) && !string.IsNullOrEmpty(username)) {
                    lastLogonName = domainName + @"\" + username;
                }
                
                //
                // Vista has different branch collecting last logon data
                if (string.IsNullOrEmpty(lastLogonName)) {
                    resultCode = Lib.GetRegistryStringValue(taskId, wmiRegistry, s_registryKeyWinlogonVista, @"LastLoggedOnSAMUser", out lastLogonName);
                }
                if (string.IsNullOrEmpty(lastLogonName)) {
                    resultCode = Lib.GetRegistryStringValue(taskId, wmiRegistry, s_registryKeyWinlogonVista, @"LastLoggedOnUser", out lastLogonName);
                }

                if (string.IsNullOrEmpty(lastLogonName)) {
                    queryResults[@"operatingSystem.lastLogon"] = @"Last Logon user name not found.";
                } else {
                    queryResults[@"operatingSystem.lastLogon"] = lastLogonName;
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetWindowsLastLogon failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsLastLogon failed",
                                 ex);
            }
            return resultCode;
        }


        /// <summary>
        /// Registry query to get Windows build information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetBuildNumAndGUIDFromRegistry(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            try {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, s_registryKeyCurrentVersion);
                inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, @"CurrentBuildNumber");

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.GET_STRING_VALUE,
                                                      s_registryKeyCurrentVersion,
                                                      inputParameters,
                                                      out outputParameters);

                if (resultCode == ResultCodes.RC_SUCCESS && null != outputParameters) {
                    using (outputParameters) {
                        string s = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                        if (!String.IsNullOrEmpty(s)) {
                            queryResults[@"operatingSystem.buildNumber"] = s;
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetBuildNumAndGUIDFromRegistry failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetBuildNumAndGUIDFromRegistry failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Registry query to get Windows installed hotfix information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetInstalledHotfix(string taskId,
                                                        ManagementScope scope,
                                                        ManagementClass wmiRegistry,
                                                        IDictionary<string, string> queryResults) {

            Stopwatch sw = Stopwatch.StartNew();
            StringBuilder sb = new StringBuilder(), hotfixBuilder = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            try {
                IDictionary<string, StringBuilder> buf = new Dictionary<string, StringBuilder>();
                IDictionary<string, string> hotFixes = new Dictionary<string, string>();
                resultCode = GetUninstallRegistry(taskId,
                                                  wmiRegistry,
                                                  s_registryKeyUninstall,
                                                  buf,
                                                  hotFixes);
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    resultCode = GetUninstallRegistry(taskId,
                                                      wmiRegistry,
                                                      s_registryKeyUninstall64,
                                                      buf,
                                                      hotFixes);
                }

                    if (queryResults.ContainsKey(@"operatingSystem.idString")) {
                    //if (!s_2000Regex.IsMatch(queryResults[@"operatingSystem.idString"])) {
                    if (s_vistaRegex.IsMatch(queryResults[@"operatingSystem.idString"]) ||
                        s_2008Regex.IsMatch(queryResults[@"operatingSystem.idString"])) {
                        s_hotFixQuery.ExecuteQuery(taskId, scope, hotFixes);
                    }
                }
                
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    foreach (KeyValuePair<string, StringBuilder> kvp in buf) {
                        if (kvp.Value != null) {
                            sb.Append(kvp.Value);
                        }
                    }

                    foreach (KeyValuePair<string, string> kvp in hotFixes) {
                        // For 5.0, get hotfix ID only.
                        if (hotfixBuilder.Length > 0) {
                            hotfixBuilder.Append(BdnaDelimiters.DELIMITER2_TAG);
                        }
                        hotfixBuilder.Append(kvp.Value);
                        //if (!string.IsNullOrEmpty(kvp.Value)) {
                        //    if (hotfixBuilder.Length > 0) {
                        //        hotfixBuilder.Append(BdnaDelimiters.DELIMITER1_TAG);
                        //    }
                        //    hotfixBuilder.Append(kvp.Value);
                        //}
                    }
                    if (hotfixBuilder.Length > 0) {
                        queryResults[@"operatingSystem.patchesInstalled"] = hotfixBuilder.ToString();
                    } else {
                        queryResults[@"operatingSystem.patchesInstalled"] = @"No Hotfix found.";
                    }
                }
            } catch (ManagementException me) {
                resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetInstalledSoftware failed",
                                           me);
            } catch (Exception ex) {
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetInstalledHotfix failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Get uninstall registry from given registry path.
        /// </summary>
        /// <param name="taskId">taskId</param>
        /// <param name="wmiRegistry">WMI Registry</param>
        /// <param name="uninstallRegistryPath">Uninstall registry path.</param>
        /// <param name="registryBuffer">Buffer</param>
        /// <returns></returns>
        private static ResultCodes GetUninstallRegistry(
                string taskId,
                ManagementClass wmiRegistry,
                string uninstallRegistryPath,
                IDictionary<string, StringBuilder> registryBuffer,
                IDictionary<string, string> hotFixBuffer) {

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;
            ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
            inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
            inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, uninstallRegistryPath);

            ManagementBaseObject outputParameters = null;
            resultCode = Lib.InvokeRegistryMethod(taskId,
                                                  wmiRegistry,
                                                  RegistryMethodNames.ENUM_KEY,
                                                  s_registryKeyUninstall,
                                                  inputParameters,
                                                  out outputParameters);

            if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                string[] subKeys = null;
                using (outputParameters) {
                    subKeys = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                }

                if (null != subKeys && 0 < subKeys.Length) {
                    foreach (string subKey in subKeys) {
                        StringBuilder sb = new StringBuilder();
                        string displayNameString = null, subKeyString = subKey, installDate = null;
                        string subkeyPath = uninstallRegistryPath + @"\" + subKey;
                        sb.Append(BdnaDelimiters.DELIMITER1_TAG)
                          .Append(@"SubKeyLabel")
                          .Append(BdnaDelimiters.DELIMITER2_TAG);
                        sb.Append(subKey);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                        inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);

                        outputParameters = null;
                        resultCode = Lib.InvokeRegistryMethod(taskId,
                                                              wmiRegistry,
                                                              RegistryMethodNames.ENUM_VALUES,
                                                              subkeyPath,
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

                                for (int i = 0; valueNames.Length > i; ++i) {
                                    string valueNamesRegistryMethod;
                                    switch ((RegistryTypes)valueTypes[i]) {
                                        case RegistryTypes.REG_SZ:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_STRING_VALUE;
                                            break;
                                        case RegistryTypes.REG_EXPAND_SZ:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_EXPANDED_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_EXPANDED_STRING_VALUE;
                                            break;
                                        case RegistryTypes.REG_BINARY:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_BINARY_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_BINARY_VALUE;
                                            break;
                                        case RegistryTypes.REG_DWORD:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_DWORD_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_DWORD_VALUE;
                                            break;
                                        case RegistryTypes.REG_MULTI_SZ:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_MULTI_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_MULTI_STRING_VALUE;
                                            break;
                                        default:
                                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                                            valueNamesRegistryMethod = RegistryMethodNames.GET_STRING_VALUE;
                                            break;
                                    }
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);
                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                    outputParameters = null;
                                    resultCode = Lib.InvokeRegistryMethod(taskId,
                                                                          wmiRegistry,
                                                                          valueNamesRegistryMethod,
                                                                          subkeyPath,
                                                                          inputParameters,
                                                                          out outputParameters);

                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                        string installedItemValue = String.Empty;
                                        using (outputParameters) {
                                            switch ((RegistryTypes)valueTypes[i]) {
                                                case RegistryTypes.REG_SZ:
                                                case RegistryTypes.REG_EXPAND_SZ:
                                                case RegistryTypes.REG_MULTI_SZ:
                                                    installedItemValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                                    break;
                                                case RegistryTypes.REG_BINARY:
                                                case RegistryTypes.REG_DWORD:
                                                    object dwBinValue = outputParameters.GetPropertyValue(RegistryPropertyNames.U_VALUE);
                                                    if (null != dwBinValue) {
                                                        installedItemValue = dwBinValue.ToString();
                                                    }
                                                    break;
                                                default:
                                                    installedItemValue = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                                    break;
                                            }
                                        }

                                        if (!String.IsNullOrEmpty(installedItemValue)) {
                                            sb.Append(BdnaDelimiters.DELIMITER2_TAG)
                                              .Append(valueNames[i])
                                              .Append(BdnaDelimiters.DELIMITER2_TAG)
                                              .Append(installedItemValue);
                                            if (valueNames[i] == @"DisplayName") {
                                                displayNameString = installedItemValue;
                                            } else if (valueNames[i] == @"InstallDate") {
                                                installDate = installedItemValue;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        // Merge result from 64 bits and 32 bits.
                        if (registryBuffer.ContainsKey(subKey)) {
                            if (registryBuffer[subKey].Length < sb.Length) {
                                registryBuffer[subKey] = sb;
                            }
                        } else {
                            registryBuffer.Add(subKey, sb);
                        }


                        // Get Hotfix 
                        string hotFixId = null;
                        if (string.IsNullOrEmpty(displayNameString)) {
                            displayNameString = subKeyString;
                        }
                        if (!string.IsNullOrEmpty(displayNameString)) {
                            if (s_hotFixRegex.IsMatch(displayNameString)) {
                                MatchCollection mc = s_hotFixRegex.Matches(displayNameString);
                                if (0 < mc.Count) {
                                    hotFixId = mc[0].Groups[1].Value;
                                    StringBuilder tempSB = new StringBuilder();
                                    tempSB.Append(@"Desc=""").Append(displayNameString).Append('"')
                                          .Append(BdnaDelimiters.DELIMITER1_TAG);
                                    tempSB.Append(@"HotFixID=""").Append(hotFixId).Append('"');
                                    if (!string.IsNullOrEmpty(installDate)) {
                                        tempSB.Append(BdnaDelimiters.DELIMITER1_TAG)
                                              .Append(@"InstallDate=""").Append(installDate).Append('"');
                                    }

                                    if (!hotFixBuffer.ContainsKey(hotFixId) && tempSB.Length > 0) {
                                        hotFixBuffer.Add(hotFixId, tempSB.ToString());
                                    } 
                                }
                            }
                        }
                    }   
                }
            }
            return resultCode;
        }

/**** Bug 17320
        /// <summary>
        /// Registry query to retrieve Power Settings information
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetPowerSettings(string taskId,
                                                    ManagementClass wmiRegistry,
                                                    IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            try {
                string osName = "XP";

                Dictionary<string, string> processorDic = new Dictionary<string, string>();
                processorDic.Add("00", "NONE");
                processorDic.Add("01", "CONSTANT");
                processorDic.Add("02", "DEGRADE");
                processorDic.Add("03", "ADAPTIVE");

                //Get OS Name
                resultCode = Lib.GetRegistryStringValue(taskId, 
                                                        wmiRegistry, 
                                                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", @"ProductName", 
                                                        out osName);
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    if (osName.Contains("2000")) {
                        queryResults[@"operatingSystem.powerSettingsDetails"] = GetWindows2000PowerSettings(taskId,
                                                                                           wmiRegistry,
                                                                                           processorDic);
                    } else if (osName.Contains("XP") || osName.Contains("2003")) {
                        queryResults[@"operatingSystem.powerSettingsDetails"] = GetWindowsXPPowerSettings(taskId,
                                                                                           wmiRegistry,
                                                                                           processorDic);
                    } else if (osName.Contains("Vista") || osName.Contains("2008")) {
                        queryResults[@"operatingSystem.powerSettingsDetails"] = GetWindows6PowerSettings(taskId, 
                                                                                         wmiRegistry, 
                                                                                         processorDic);
                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Error collect OS platform"
                                              + "@SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion:ProductName: {1}.",
                                              osName);
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetUserPowerSettings failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetUserPowerSettings failed",
                                 ex);
            }
            return resultCode;
        }

        // Get Windows XP Power Settings
        private static string GetWindowsXPPowerSettings(string taskId,
                                                        ManagementClass wmiRegistry,
                                                        Dictionary<string, string> processorDic) {
            Stopwatch sw = Stopwatch.StartNew();
            StringBuilder powerInfoBuffer = null;
            string currentPowerPolicy = "";
            try {
                Dictionary<string, string> resultDic = new Dictionary<string, string>();
                string[] valueNames = null;
                ResultCodes resultCode = Lib.GetRegistrySubkeyName(taskId,
                                                                   wmiRegistry,
                                                                   RegistryTrees.HKEY_USERS,
                                                                    @"",
                                                                    out valueNames);

                foreach (string name in valueNames) {
                    powerInfoBuffer = new StringBuilder();
                    if (!name.EndsWith("Classes")) {
                        string lastUserID = string.Empty;
                        resultCode = Lib.GetRegistryStringValue(taskId,
                                                                wmiRegistry,
                                                                RegistryTrees.HKEY_USERS,
                                                                name + @"\Identities",
                                                                @"Last User ID",
                                                                out lastUserID);
                        if (resultCode != ResultCodes.RC_SUCCESS) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Error collecting last user id: {1}",
                                                  taskId, 
                                                  lastUserID);
                            break;;
                        }
                        
                        // Retrieve Power Policy
                        resultCode = Lib.GetRegistryStringValue(taskId,
                                                                wmiRegistry,
                                                                RegistryTrees.HKEY_USERS,
                                                                name + @"\Control Panel\PowerCfg",
                                                                "CurrentPowerPolicy",
                                                                out currentPowerPolicy);
                        if (resultCode != ResultCodes.RC_SUCCESS) {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Error collecting binary power settings "
                                                  + "HKCU\\CControl Panel\\PowerCfg\\PowerPolicies\\{1}: {2}",
                                                  taskId,
                                                  currentPowerPolicy);
                            break;
                        }

                        // Collect Power Policies binary value.
                        byte[] powerSettingsArray = null;
                        resultCode = Lib.GetRegistryBinaryValue(taskId,
                                                                wmiRegistry,
                                                                RegistryTrees.HKEY_USERS,
                                                                name + @"\Control Panel\PowerCfg\PowerPolicies\" + currentPowerPolicy,
                                                                @"Policies",
                                                                out powerSettingsArray);
                        if (resultCode == ResultCodes.RC_SUCCESS) {
                            string value = "";
                            for (int i = 0; i < powerSettingsArray.Length; i++) {
                                value += String.Format("{0:X2}", powerSettingsArray[i]);
                                if ((i + 1) % 8 == 0) {
                                    int rowCount = (i + 1) / 8;
                                    if (rowCount == 4) {
                                        powerInfoBuffer.Append("Standby_AC=")
                                                       .Append(ToValue(ReverseString(value.Substring(8, 8))));
                                    } else if (rowCount == 5) {
                                        string standbyDC = ToValue(ReverseString(value.Substring(0, 8)));
                                        powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Standby_DC=")
                                                       .Append(standbyDC);
                                        string processorThrottleAC = "\"" + processorDic[ReverseString(value.Substring(12, 2))] + "\"";
                                        powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Processor_Throttle_AC=")
                                                       .Append(processorThrottleAC);
                                        string processorThrottleDC = "\"" + processorDic[ReverseString(value.Substring(14, 2))] + "\"";
                                        powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Processor_Throttle_DC=")
                                                       .Append(processorThrottleDC);
                                    } else if (rowCount == 8) {
                                        powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Monitor_AC=")
                                                       .Append(ToValue(ReverseString(value.Substring(0, 8))))
                                                       .Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Monitor_DC=")
                                                       .Append(ToValue(ReverseString(value.Substring(8, 8))));
                                    } else if (rowCount == 9) {
                                        powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Disk_AC=")
                                                       .Append(ToValue(ReverseString(value.Substring(0, 8))))
                                                       .Append(BdnaDelimiters.DELIMITER1_TAG)
                                                       .Append("Disk_DC=")
                                                       .Append(ToValue(ReverseString(value.Substring(8, 8))));
                                    }
                                    value = "";
                                }
                            }
                            if (!resultDic.ContainsKey(lastUserID)) {
                                resultDic.Add(lastUserID, powerInfoBuffer.ToString());
                            }
                        } else {
                            Lib.Logger.TraceEvent(TraceEventType.Error,
                                                  0,
                                                  "Task Id {0}: Error collecting binary power settings "
                                                  + "HKCU\\CControl Panel\\PowerCfg\\PowerPolicies\\{1}: {2}",
                                                  taskId,
                                                  currentPowerPolicy);
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
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsXPowerSettings failed",
                                 ex);
            }
            return string.Empty;            
        }
*/

        /**
         * Helper method to collect power setting from 2000.
         */ 
/*
        private static string GetWindows2000PowerSettings(string taskId,
                                                          ManagementClass wmiRegistry,
                                                          Dictionary<string, string> processorDic) {
            Stopwatch sw = Stopwatch.StartNew();
            StringBuilder powerInfoBuffer = new StringBuilder();
            string currentPowerPolicy = "";
            try {
                ResultCodes resultCode = Lib.GetRegistryStringValue(taskId,
                                                                    wmiRegistry,
                                                                    RegistryTrees.HKEY_CURRENT_USER,
                                                                    @"Control Panel\PowerCfg",
                                                                    @"CurrentPowerPolicy",
                                                                    out currentPowerPolicy);

                if (resultCode != ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting current power policy HKCU\\Control Panel\\PowerCfg: {1}",
                                          taskId, 
                                          currentPowerPolicy);
                    return String.Empty;
                }
                
                // Collect Power Policies binary value.
                byte[] powerSettingsArray = null;
                resultCode = Lib.GetRegistryBinaryValue(taskId,
                                                        wmiRegistry,
                                                        RegistryTrees.HKEY_CURRENT_USER,
                                                        @"Control Panel\PowerCfg\PowerPolicies\" + currentPowerPolicy,
                                                        "Policies",
                                                        out powerSettingsArray);
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    string value = null;
                    for (int i = 0; i < powerSettingsArray.Length; i++) {
                        value += String.Format("{0:X2}", powerSettingsArray[i]);
                        if ((i + 1) % 8 == 0) {
                            int rowCount = (i + 1) / 8;
                            if (rowCount == 4) {
                                powerInfoBuffer.Append("Standby_AC=")
                                               .Append(ToValue(ReverseString(value.Substring(8, 8))));
                            } else if (rowCount == 5) {
                                string standbyDC = ToValue(ReverseString(value.Substring(0, 8)));
                                powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Standby_DC=")
                                               .Append(standbyDC);
                                string processor_throttleAC = "\"" + processorDic[ReverseString(value.Substring(12, 2))] + "\"";
                                powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Processor_Throttle_AC=")
                                               .Append(processor_throttleAC);
                                string processor_throttleDC = "\"" + processorDic[ReverseString(value.Substring(14, 2))] + "\"";
                                powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Processor_Throttle_DC=")
                                               .Append(processor_throttleDC);
                            } else if (rowCount == 8) {
                                string monitorAC = ToValue(ReverseString(value.Substring(0, 8)));
                                string monitorDC = ToValue(ReverseString(value.Substring(8, 8)));
                                powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Monitor_AC=")
                                               .Append(monitorAC)
                                               .Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Monitor_DC=")
                                               .Append(monitorDC);
                            } else if (rowCount == 9) {
                                powerInfoBuffer.Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Disk_AC=")
                                               .Append(ToValue(ReverseString(value.Substring(0, 8))))
                                               .Append(BdnaDelimiters.DELIMITER1_TAG)
                                               .Append("Disk_DC=")
                                               .Append(ToValue(ReverseString(value.Substring(8, 8))));
                            }
                            value = "";
                        }
                    }
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting binary power settings "
                                          + "HKCU\\CControl Panel\\PowerCfg\\PowerPolicies\\{1}: {2}",
                                          taskId,
                                          currentPowerPolicy);
                }
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindows2000PowerSetting failed",
                                 ex);
            }
            return powerInfoBuffer.ToString();
        }

        // power setting helperr method.
        private static string ReverseString(string value) {
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
*/
        /**
         * Power Setting collection for Vista/2008
         */
/*
        private static string GetWindows6PowerSettings(string taskId,
                                                       ManagementClass wmiRegistry,
                                                       Dictionary<string, string> processorDic) {
            StringBuilder powerInfoBuffer = new StringBuilder();
            Stopwatch sw = Stopwatch.StartNew();
            try {
                // Collect Active Power Scheme
                string activePowerScheme = "";
                ResultCodes resultCode = Lib.GetRegistryStringValue(taskId,
                                                                    wmiRegistry,
                                                                    @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes",
                                                                    "ActivePowerScheme",
                                                                    out activePowerScheme);

                if (resultCode != ResultCodes.RC_SUCCESS) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting active power scheme " +
                                          "SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes: {1}",
                                          taskId,
                                          activePowerScheme);
                    return string.Empty;
                }

                if ("a1841308-3541-4fab-bc81-f71556f20b4a".Equals(activePowerScheme)) {
                    powerInfoBuffer.Append("Processor_Throttle_AC=\"Power saver\"<BDNA,1>")
                                   .Append("Processor_Throttle_DC=\"Power saver\"<BDNA,1>");
                } else if ("381b4222-f694-41f0-9685-ff5bb260df2e".Equals(activePowerScheme)) {
                    powerInfoBuffer.Append("Processor_Throttle_AC=\"Balanced\"<BDNA,1>")
                                   .Append("Processor_Throttle_DC=\"Balanced\"<BDNA,1>");
                } else if ("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c".Equals(activePowerScheme)) {
                    powerInfoBuffer.Append("Processor_Throttle_AC=\"High performance\"<BDNA,1>")
                                   .Append("Processor_Throttle_DC=\"High performance\"<BDNA,1>");
                }

                // Collect Monitor AC Setting
                string regPath = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme +
                                 @"\7516b95f-f776-4464-8c53-06167f40cc99\3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";

                string monitorAC = string.Empty;
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "ACSettingIndex",
                                                  out monitorAC);
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("Monitor_AC=")
                                   .Append(IntToValue(string.Format("{0:X2}", monitorAC)));                    
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting AC Monitor Setting {1}",
                                          taskId,
                                          monitorAC);
                }

                // Collect Monitor DC Setting
                string monitorDC = string.Empty;
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "DCSettingIndex",
                                                  out monitorDC);

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Monitor_DC="+IntToValue(string.Format("{0:X2}", monitorDC)));

                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Monitor DC {1}",
                                          taskId,
                                          monitorDC);
                }

                string discAC = string.Empty;
                regPath = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme + 
                          @"\0012ee47-9041-4b5d-9b77-535fba8b1442\6738e2c4-e8a5-4a42-b16a-e040e769756e";

                // retrieve Disc AC setting
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "ACSettingIndex",
                                                  out discAC);

                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Disk_AC="+IntToValue(string.Format("{0:X2}", discAC)));
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Disc AC Settings{1}",
                                          taskId,
                                          discAC);
                }

                // Collect Disc DC Setting
                string discDC = string.Empty;
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "DCSettingIndex",
                                                  out discDC);
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Disk_DC="+IntToValue(string.Format("{0:X2}", discDC)));
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Disc DC Settings: {1}",
                                          taskId,
                                          discDC);
                }

                // Collect Standby AC setting;
                string standbyAC = string.Empty;
                regPath = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme + 
                          @"\238C9FA8-0AAD-41ED-83F4-97BE242C8F20\29f6c1db-86da-48c5-9fdb-f2b67b1f44da";
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "ACSettingIndex",
                                                  out standbyAC);
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Standby_AC="+string.Format("{0:X2}", standbyAC));
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Standby AC Settings: {1}",
                                          taskId,
                                          standbyAC);
                }

                // Collect Standby DC Setting
                string standbyDC = string.Empty;
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "DCSettingIndex",
                                                  out standbyDC);
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Standby_DC=" +IntToValue(string.Format("{0:X2}", standbyAC)));
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Standby AC Settings: {1}",
                                          taskId,
                                          standbyDC);
                }
                
                // Collect hibernate AC Setting
                string hibernateAC = string.Empty;
                regPath = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\" + activePowerScheme +
                          @"\238C9FA8-0AAD-41ED-83F4-97BE242C8F20\9d7815a6-7ee4-497e-8888-515a05f02364";
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "ACSettingIndex",
                                                  out hibernateAC);
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Hibernate_AC="+IntToValue(string.Format("{0:X2}", hibernateAC)));
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Hibernate AC Settings: {1}",
                                          taskId,
                                          hibernateAC);
                }

                // Collect Hibernate DC Setting
                string hibernateDC = string.Empty;
                resultCode = Lib.GetRegistryDWord(taskId,
                                                  wmiRegistry,
                                                  regPath,
                                                  "DCSettingIndex",
                                                  out hibernateDC);
                if (resultCode == ResultCodes.RC_SUCCESS) {
                    powerInfoBuffer.Append("<BDNA,1>Hibernate_DC="+IntToValue(string.Format("{0:X2}", hibernateDC)));
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Error collecting Hibernate DC Settings: {1}",
                                          taskId,
                                          hibernateDC);
                }
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindows6PowerSettings failed",
                                 ex);
            }
            return powerInfoBuffer.ToString();
        }


        private static int ToInt32(string hexString) {
            return Convert.ToInt32(hexString, 16);
        }

        private static string ToValue(string hexString) {
            int intValue = Convert.ToInt32(hexString, 16) / 60;
            if (intValue == 0) {
                return "\"Never\"";
            } else {
                return "\"After " + intValue.ToString() + " mins\"";
            }
        }

        private static string IntToValue(string intString) {
            int intValue = Convert.ToInt32(intString) / 60;
            if (intValue == 0) {
                return "\"Never\"";
            } else {
                return "\"After " + intValue.ToString() + " mins\"";
            }
        }
*/

        /// <summary>
        /// Registry query to get Windows local printer information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetCPUModelViaRegistry(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            StringBuilder dataRowEntry = new StringBuilder();
            StringBuilder printerLine = new StringBuilder();

            try {
                string[] cpuIDs = null;
                string[] cpuLabels = null;
                resultCode = Lib.GetRegistryImmediateSubKeys(taskId, wmiRegistry, s_registryKeyCPUs, out cpuIDs);
                if (cpuIDs != null && cpuIDs.Length > 0) {
                    cpuLabels = new String[cpuIDs.Length];
                    for (int i = 0; i < cpuIDs.Length; i++) {
                        resultCode = Lib.GetRegistryStringValue(taskId, 
                                                                wmiRegistry, 
                                                                s_registryKeyCPUs + @"\" + cpuIDs[i], 
                                                                @"ProcessorNameString",
                                                                out cpuLabels[i]);
                    }
                    if ((cpuIDs.Length == 1) && (cpuLabels != null)) {
                        if (queryResults.ContainsKey(@"cpu.model") && !string.IsNullOrEmpty(cpuLabels[0])) {
                            queryResults[@"cpu.model"] = cpuLabels[0];
                        }
                    } else {
                        for (int cpuID = 0; cpuID < cpuLabels.Length; cpuID++) {
                            string index = @"cpu.model";
                            if (cpuID == 0) {
                                if (queryResults.ContainsKey(index) && !string.IsNullOrEmpty(cpuLabels[cpuID])) {
                                    queryResults[index] = cpuLabels[cpuID];
                                } else if (queryResults.ContainsKey(index + cpuID) && !string.IsNullOrEmpty(cpuLabels[cpuID])) {
                                    queryResults[@"cpu.model" + cpuID] = cpuLabels[cpuID];
                                }
                            } else {
                                if (queryResults.ContainsKey(index + cpuID) && !string.IsNullOrEmpty(cpuLabels[cpuID])) {
                                    queryResults[@"cpu.model" + cpuID] = cpuLabels[cpuID];
                                }
                            }
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetCPUModelViaRegistry failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetCPUModelViaRegistry failed",
                                 ex);
            }
            return resultCode;
        }   



        /// <summary>
        /// Registry query to get Windows local printer information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetPrintersViaRegistry(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            StringBuilder dataRowEntry = new StringBuilder();
            StringBuilder printerLine = new StringBuilder();

            try {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_KEY);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, s_registryKeyPrinters);

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.ENUM_KEY,
                                                      s_registryKeyPrinters,
                                                      inputParameters,
                                                      out outputParameters);

                if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                    string[] subKeys = null;
                    using (outputParameters) {
                        subKeys = outputParameters.GetPropertyValue(RegistryPropertyNames.NAMES) as string[];
                    }

                    //
                    // Process list of installed printers, if any.
                    if (null != subKeys && 0 < subKeys.Length) {
                        StringBuilder keyPath = new StringBuilder();

                        //
                        // Loop through list of installed printers
                        foreach (string printer in subKeys) {
                            printerLine.Length = 0;
                            string subkeyPath = s_registryKeyPrinters + @"\" + printer;
                            inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.ENUM_VALUES);
                            inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                            inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);

                            outputParameters = null;
                            resultCode = Lib.InvokeRegistryMethod(taskId,
                                                                  wmiRegistry,
                                                                  RegistryMethodNames.ENUM_VALUES,
                                                                  subkeyPath,
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

                                    //
                                    // Loop through the set of keys we want and see if they
                                    // exist in the values we got from the registry.
                                    foreach (KeyValuePair<string, string> kvp in s_printerKeyNameToRowNameMap) {

                                        //
                                        // Search the value list 
                                        for (int i = 0;
                                             valueNames.Length > i;
                                             ++i) {

                                            //
                                            // Get the registry data if this registry value name is
                                            // one we're interested in.
                                            if (valueNames[i].Equals(kvp.Key)) {
                                                string valueData = null;

                                                //
                                                // Grab string value.
                                                if (RegistryTypes.REG_SZ == (RegistryTypes)valueTypes[i]) {
                                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);
                                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                                    resultCode = Lib.InvokeRegistryMethod(taskId,
                                                                                          wmiRegistry,
                                                                                          RegistryMethodNames.GET_STRING_VALUE,
                                                                                          subkeyPath,
                                                                                          inputParameters,
                                                                                          out outputParameters);

                                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                                        using (outputParameters) {
                                                            valueData = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                                                        }
                                                    }

                                                    //
                                                    // Grab dword value.
                                                } else if (RegistryTypes.REG_DWORD == (RegistryTypes)valueTypes[i]) {
                                                    inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_DWORD_VALUE);
                                                    inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                                                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, subkeyPath);
                                                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, valueNames[i]);

                                                    resultCode = Lib.InvokeRegistryMethod(taskId,
                                                                                          wmiRegistry,
                                                                                          RegistryMethodNames.GET_DWORD_VALUE,
                                                                                          subkeyPath,
                                                                                          inputParameters,
                                                                                          out outputParameters);

                                                    if (ResultCodes.RC_SUCCESS == resultCode && null != outputParameters) {
                                                        using (outputParameters) {
                                                            object dwValue = outputParameters.GetPropertyValue(RegistryPropertyNames.U_VALUE);
                                                            if (null != dwValue) {
                                                                valueData = dwValue.ToString();
                                                            }
                                                        }
                                                    }
                                                }

                                                //
                                                // Update the data row buffer if we got something.
                                                if (null != valueData) {
                                                    if (0 != printerLine.Length) {
                                                        printerLine.Append(BdnaDelimiters.DELIMITER2_TAG);
                                                    }

                                                    printerLine.Append(kvp.Value)
                                                               .Append('=')
                                                               .Append(valueData);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (0 != printerLine.Length) {
                                if (0 != dataRowEntry.Length) {
                                    dataRowEntry.Append(BdnaDelimiters.DELIMITER1_TAG);
                                }
                                dataRowEntry.Append(printerLine);
                            }
                        }
                    }
                }

                if (0 != dataRowEntry.Length) {
                    queryResults[@"printerViaRegistry"] = dataRowEntry.ToString();
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetPrintersViaRegistry failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetPrintersViaRegistry failed",
                                 ex);
            }
            return resultCode;
        }   

        /// <summary>
        /// Registry query to get Windows License Key information.
        /// </summary>
        /// 
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        /// 
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetWindowsLicenseKey(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;
            StringBuilder dataRowEntry = new StringBuilder();
            StringBuilder printerLine = new StringBuilder();
            try {
                byte[] digitalProductID = null;
                using (ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_BINARY_VALUE)) {
                    inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, s_registryKeyCurrentVersion);
                    inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, @"DigitalProductId");
                    ManagementBaseObject outputParameters = null;
                    resultCode = Lib.InvokeRegistryMethod(taskId, wmiRegistry, RegistryMethodNames.GET_BINARY_VALUE,
                                                          s_registryKeyCurrentVersion, inputParameters, out outputParameters);
                    if (resultCode == ResultCodes.RC_SUCCESS && null != outputParameters) {
                        using (outputParameters) {
                            digitalProductID = outputParameters.GetPropertyValue(RegistryPropertyNames.U_VALUE) as byte[];
                            if (digitalProductID != null) {
                                string licenseKey = Lib.ExtractLicenseKeyFromMSDigitalProductID(taskId, digitalProductID);
                                if (!string.IsNullOrEmpty(licenseKey)) {
                                    queryResults[@"operatingSystem.licenseKey"] = licenseKey;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsLicenseKey failed",
                                 ex);
            }
            return resultCode;
        }


        /// <summary>
        /// Helper method to build up list name value pairs by
        /// merging value collected via WMI with names expected in
        /// the resulting data row.
        /// </summary>
        /// 
        /// <param name="propertyDataCollection">Collection of properties from a ManagementObject returned
        ///     by a WMI query.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names. Essentially
        ///     the key value from this map is a property name in the 
        ///     propertyDataCollection and the value in this map is
        ///     name required in the resulting data row.</param>
        /// <param name="processedResults">Collection of resulting name value pairs.</param>
        /// <param name="addSequenceNumber">Set to true to add a sequence number "(9)" to the end
        ///     of each data row item name.</param>
        private static void ProcessPropertyData(
                PropertyDataCollection propertyDataCollection,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults,
                bool addSequenceNumber,
                int sequenceNumber) {

            //
            // Step through each property returned by WMI.
            foreach (PropertyData pd in propertyDataCollection) {
                //
                // Find the data row name (our name) for this
                // property by using the map of WMI names to 
                // data row names.
                string dataRowItemName = propertyMap[pd.Name];
                if (null != dataRowItemName && null != pd.Value) {
                    string processedResultKey = (addSequenceNumber)
                        ? dataRowItemName + sequenceNumber
                        : dataRowItemName;
                    string processedResultValue = null;
                    if (pd.IsArray && CimType.String == pd.Type) {
                        string[] strArray = new string[((IList<object>)pd.Value).Count];
                        for (int i = 0; i < ((IList<object>)pd.Value).Count; ++i) {
                            strArray[i] = ((IList<object>)pd.Value)[i].ToString();
                        }

                        processedResultValue = String.Join(",", strArray);
                    } else {
                        processedResultValue = pd.Value.ToString();
                    }
                    processedResults[processedResultKey] = processedResultValue;
                }
            }
        }

        /// <summary>
        /// The default handle for WMI query results just generates
        /// the name value pairs for the data row with no additional
        /// manipulation.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void DefaultResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults) {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    addSequenceNumber,
                                    count);
                count++;
            }
        }

        /// <summary>
        /// The handle for WMI query results generates
        /// the name value pairs for the data row for ComputerSystem
        /// and stores name of Computer in global variable
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void ComputerSystemResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults)
        {
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults)
            {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    addSequenceNumber,
                                    count);
                computerName = mo.Properties[@"Name"].Value.ToString();
                count++;
            }
        }

        private static void HotfixResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults) {
                //
                // Step through each property returned by WMI.
                StringBuilder sb = new StringBuilder();
                string hotfixID = null;
                foreach (PropertyData pd in mo.Properties) {
                    string propertyName = pd.Name;
                    if (null != propertyName && null != pd.Value) {
                        string propertyValue = pd.Value.ToString();
                        if (pd.IsArray && CimType.String == pd.Type) {
                            propertyValue = String.Join(",", pd.Value as string[]);
                        } else if (propertyName == @"HotFixID") {
                            hotfixID = propertyValue;
                        } else if (propertyName == @"InstalledOn") {
                            try {
                                propertyName = @"InstallDate";
                                Int64 iValue = Int64.Parse(pd.Value.ToString(),
                                                           System.Globalization.NumberStyles.AllowHexSpecifier);
                                DateTime dValue = DateTime.FromFileTimeUtc(iValue);
                                propertyValue = dValue.ToShortDateString();
                            } catch (Exception ex) {
                                Lib.Logger.TraceEvent(TraceEventType.Warning,
                                                      0,
                                                      "Collection script WindowsStaticScript. Hotfix ID {0} dateTime value {1} not valid. {2}",
                                                      hotfixID,
                                                      pd.Value.ToString(),
                                                      ex.Message);
                            }
                        } else if (propertyName == @"Description") {
                            propertyName = @"Desc";
                            if (string.IsNullOrEmpty(propertyValue)) {
                                propertyValue = @"Update";
                            }
                        }

                        if (!string.IsNullOrEmpty(propertyValue)) {
                            if (sb.Length > 0) {
                                sb.Append(BdnaDelimiters.DELIMITER1_TAG);
                            }
                            sb.Append(propertyName + @"=""" + propertyValue + @"""");
                        }
                    }
                }
                if (!string.IsNullOrEmpty(hotfixID)) {
                    if (!processedResults.ContainsKey(hotfixID)) {
                        processedResults[hotfixID] = hotfixID;
                    }
                }

                if (!string.IsNullOrEmpty(hotfixID) && sb.Length > 0 ) {
                    if (!processedResults.ContainsKey(hotfixID)) {
                        processedResults[hotfixID] = sb.ToString();
                    } else {
                        // replace hotfix data with one with more information.
                        if (processedResults[hotfixID].ToString().Length < sb.ToString().Length) {
                            processedResults[hotfixID] = sb.ToString();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handler for the Win32_OperatingSystem query.  Uses query
        /// results to compute OS related data row items.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void OsInformationResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            StringBuilder sb = new StringBuilder();
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;

            foreach (ManagementObject mo in queryResults) {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    addSequenceNumber,
                                    count);
                ++count;
                sb.Length = 0;
                String installDateStr = string.Empty;
                try {
                    //20050805 164648.000000-420
                    installDateStr = mo.Properties[@"InstallDate"].Value.ToString();
                    if (!string.IsNullOrEmpty(installDateStr)) {
                        if (s_dateTimeRegex.IsMatch(installDateStr)) {
                            MatchCollection mc = s_dateTimeRegex.Matches(installDateStr);
                            if (0 < mc.Count) {
                                installDateStr = mc[0].Groups[1].Value + @"/" +
                                                 mc[0].Groups[2].Value + @"/" +
                                                 mc[0].Groups[3].Value + @" " +
                                                 mc[0].Groups[4].Value + @":" +
                                                 mc[0].Groups[5].Value;
                            }
                        } 
                        processedResults[@"operatingSystem.installDate"] = installDateStr;
                    }
                } catch (Exception ex) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Collection script WindowsStaticScript. Unexpected exception. {0}",
                                          ex.Message);
                }

                try {
                    String osLanguage = (new StringBuilder()).Append(mo.Properties[@"OSLanguage"].Value).ToString();
                    switch (osLanguage) {
                        case "9": osLanguage = "English"; break;
                        case "1025": osLanguage = "Arabic - Saudi Arabia"; break;
                        case "1026": osLanguage = "Bulgarian"; break;
                        case "1027": osLanguage = "Catalan"; break;
                        case "1028": osLanguage = "Chinese (Traditional) - Taiwan"; break;
                        case "1029": osLanguage = "Czech"; break;
                        case "1030": osLanguage = "Danish"; break;
                        case "1031": osLanguage = "German - Germany"; break;
                        case "1032": osLanguage = "Greek"; break;
                        case "1033": osLanguage = "English - United States"; break;
                        case "1034": osLanguage = "Spanish - Traditional Sort"; break;
                        case "1035": osLanguage = "Finnish"; break;
                        case "1036": osLanguage = "French - France"; break;
                        case "1037": osLanguage = "Hebrew"; break;
                        case "1038": osLanguage = "Hungarian"; break;
                        case "1039": osLanguage = "Icelandic"; break;
                        case "1040": osLanguage = "Italian - Italy"; break;
                        case "1041": osLanguage = "Japanese"; break;
                        case "1042": osLanguage = "Korean"; break;
                        case "1043": osLanguage = "Dutch - Netherlands"; break;
                        case "1044": osLanguage = "Norwegian - Bokmal"; break;
                        case "1045": osLanguage = "Polish"; break;
                        case "1046": osLanguage = "Portuguese - Brazil"; break;
                        case "1047": osLanguage = "Rhaeto-Romanic"; break;
                        case "1048": osLanguage = "Romanian"; break;
                        case "1049": osLanguage = "Russian"; break;
                        case "1050": osLanguage = "Croatian"; break;
                        case "1051": osLanguage = "Slovak"; break;
                        case "1052": osLanguage = "Albanian"; break;
                        case "1053": osLanguage = "Swedish"; break;
                        case "1054": osLanguage = "Thai"; break;
                        case "1055": osLanguage = "Turkish"; break;
                        case "1056": osLanguage = "Urdu"; break;
                        case "1057": osLanguage = "Indonesian"; break;
                        case "1058": osLanguage = "Ukrainian"; break;
                        case "1059": osLanguage = "Belarusian"; break;
                        case "1060": osLanguage = "Slovenian"; break;
                        case "1061": osLanguage = "Estonian"; break;
                        case "1062": osLanguage = "Latvian"; break;
                        case "1063": osLanguage = "Lithuanian"; break;
                        case "1065": osLanguage = "Persian"; break;
                        case "1066": osLanguage = "Vietnamese"; break;
                        case "1069": osLanguage = "Basque"; break;
                        case "1070": osLanguage = "Serbian"; break;
                        case "1071": osLanguage = "Macedonian (F.Y.R.O. Macedonia)"; break;
                        case "1072": osLanguage = "Sutu"; break;
                        case "1073": osLanguage = "Tsonga"; break;
                        case "1074": osLanguage = "Tswana"; break;
                        case "1076": osLanguage = "Xhosa"; break;
                        case "1077": osLanguage = "Zulu"; break;
                        case "1078": osLanguage = "Afrikaans"; break;
                        case "1080": osLanguage = "Faeroese"; break;
                        case "1081": osLanguage = "Hindi"; break;
                        case "1082": osLanguage = "Maltese"; break;
                        case "1084": osLanguage = "Scottish Gaelic"; break;
                        case "1085": osLanguage = "Yiddish"; break;
                        case "1086": osLanguage = "Malay - Malaysia"; break;
                        case "2049": osLanguage = "Arabic - Iraq"; break;
                        case "2052": osLanguage = "Chinese (Simplified) - PRC"; break;
                        case "2055": osLanguage = "German - Switzerland"; break;
                        case "2057": osLanguage = "English - United Kingdom"; break;
                        case "2058": osLanguage = "Spanish - Mexico"; break;
                        case "2060": osLanguage = "French - Belgium"; break;
                        case "2064": osLanguage = "Italian - Switzerland"; break;
                        case "2067": osLanguage = "Dutch - Belgium"; break;
                        case "2068": osLanguage = "Norwegian - Nynorsk"; break;
                        case "2070": osLanguage = "Portuguese - Portugal"; break;
                        case "2072": osLanguage = "Romanian - Moldova"; break;
                        case "2073": osLanguage = "Russian - Moldova"; break;
                        case "2074": osLanguage = "Serbian - Latin"; break;
                        case "2077": osLanguage = "Swedish - Finland"; break;
                        case "3073": osLanguage = "Arabic - Egypt"; break;
                        case "3076": osLanguage = "Chinese (Traditional) - Hong Kong SAR"; break;
                        case "3079": osLanguage = "German - Austria"; break;
                        case "3081": osLanguage = "English - Australia"; break;
                        case "3082": osLanguage = "Spanish - International Sort"; break;
                        case "3084": osLanguage = "French - Canada"; break;
                        case "3098": osLanguage = "Serbian - Cyrillic"; break;
                        case "4097": osLanguage = "Arabic - Libya"; break;
                        case "4100": osLanguage = "Chinese (Simplified) - Singapore"; break;
                        case "4103": osLanguage = "German - Luxembourg"; break;
                        case "4105": osLanguage = "English - Canada"; break;
                        case "4106": osLanguage = "Spanish - Guatemala"; break;
                        case "4108": osLanguage = "French - Switzerland"; break;
                        case "5121": osLanguage = "Arabic - Algeria"; break;
                        case "5127": osLanguage = "German - Liechtenstein"; break;
                        case "5129": osLanguage = "English - New Zealand"; break;
                        case "5130": osLanguage = "Spanish - Costa Rica"; break;
                        case "5132": osLanguage = "French - Luxembourg"; break;
                        case "6145": osLanguage = "Arabic - Morocco"; break;
                        case "6153": osLanguage = "English - Ireland"; break;
                        case "6154": osLanguage = "Spanish - Panama"; break;
                        case "7169": osLanguage = "Arabic - Tunisia"; break;
                        case "7177": osLanguage = "English - South Africa"; break;
                        case "7178": osLanguage = "Spanish - Dominican Republic"; break;
                        case "8193": osLanguage = "Arabic - Oman"; break;
                        case "8201": osLanguage = "English - Jamaica"; break;
                        case "8202": osLanguage = "Spanish - Venezuela"; break;
                        case "9217": osLanguage = "Arabic - Yemen"; break;
                        case "9226": osLanguage = "Spanish - Colombia"; break;
                        case "10241": osLanguage = "Arabic - Syria"; break;
                        case "10249": osLanguage = "English - Belize"; break;
                        case "10250": osLanguage = "Spanish - Peru"; break;
                        case "11265": osLanguage = "Arabic - Jordan"; break;
                        case "11273": osLanguage = "English - Trinidad"; break;
                        case "11274": osLanguage = "Spanish - Argentina"; break;
                        case "12289": osLanguage = "Arabic - Lebanon"; break;
                        case "12298": osLanguage = "Spanish - Ecuador"; break;
                        case "13313": osLanguage = "Arabic - Kuwait"; break;
                        case "13322": osLanguage = "Spanish - Chile"; break;
                        case "14337": osLanguage = "Arabic - U.A.E."; break;
                        case "14346": osLanguage = "Spanish - Uruguay"; break;
                        case "15361": osLanguage = "Arabic - Bahrain"; break;
                        case "15370": osLanguage = "Spanish - Paraguay"; break;
                        case "16385": osLanguage = "Arabic - Qatar"; break;
                        case "16394": osLanguage = "Spanish - Bolivia"; break;
                        case "17418": osLanguage = "Spanish - El Salvador"; break;
                        case "18442": osLanguage = "Spanish - Honduras"; break;
                        case "19466": osLanguage = "Spanish - Nicaragua"; break;
                        case "20490": osLanguage = "Spanish - Puerto Rico"; break;
                    }
                    processedResults[@"operatingSystem.osLanguage"] = osLanguage;
                } catch (Exception ex) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Collection script WindowsStaticScript. Unexpected OSLanguage value. {0}",
                                          ex.Message);
                }


                processedResults[@"operatingSystem.idString"] = sb.Append(mo.Properties[@"Caption"].Value)
                                                                  .Append(@"(")
                                                                  .Append(mo.Properties[@"BuildNumber"].Value)
                                                                  .Append(@")")
                                                                  .ToString();

                try {
                    string captionValue = mo.Properties[@"Caption"].Value.ToString();
                    if ((!string.IsNullOrEmpty(captionValue)) && (captionValue.Contains("x64 Edition"))) {
                        processedResults[@"operatingSystem.osArchitecture"] = @"64-bit";
                    }
                } catch (Exception ex) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Collection script WindowsStaticScript. Unexpected OS Caption value. {0}",
                                          ex.Message);
                }

                try {
                    string serviceRelease = (new StringBuilder()).Append(mo.Properties[@"OtherTypeDescription"].Value).ToString();
                    if (!string.IsNullOrEmpty(serviceRelease)) {
                        processedResults[@"operatingSystem.serviceRelease"] = serviceRelease;
                    }
                        
                } catch (Exception ex) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Collection script WindowsStaticScript. Unexpected Service Release value. {0}",
                                          ex.Message);
                }

                object totalSwapSpaceSize = mo.Properties[@"TotalSwapSpaceSize"].Value;
                processedResults[@"operatingSystem.TotalSwapSpaceSize"]
                    = (null != totalSwapSpaceSize)
                        ? totalSwapSpaceSize.ToString()
                        : String.Empty;
            }
            DateTime bootTime;
            if (processedResults.ContainsKey(@"operatingSystem.lastBootUpTime")) {
                string lastBootUpTime = processedResults[@"operatingSystem.lastBootUpTime"];
                string formattedlastBootUpTime = lastBootUpTime.Substring(0, 4) + '/' +
                                                    lastBootUpTime.Substring(4, 2) + '/' +
                                                    lastBootUpTime.Substring(6, 2) + ' ' +
                                                    lastBootUpTime.Substring(8, 2) + ':' +
                                                    lastBootUpTime.Substring(10, 2) + ':' +
                                                    lastBootUpTime.Substring(12, 2);

                if (DateTime.TryParse(formattedlastBootUpTime, out bootTime)) {
                    int diffInSecs = (int)((DateTime.Now.Ticks - bootTime.Ticks) / 10000000);
                    processedResults["operatingSystem.uptime1"] = diffInSecs.ToString();
                }
            }
        }

        /// <summary>
        /// Registry query to get Windows build information.
        /// </summary>
        ///
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        ///
        /// <returns>Operation result code (one of the RC_ constants).</returns>

        private static ResultCodes GetWindowsProductID(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            try {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, s_registryKeyCurrentVersion);
                inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, @"ProductId");

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.GET_STRING_VALUE,
                                                      s_registryKeyCurrentVersion,
                                                      inputParameters,
                                                      out outputParameters);

                if (resultCode == ResultCodes.RC_SUCCESS && null != outputParameters) {
                    using (outputParameters) {
                        string s = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                        if (!String.IsNullOrEmpty(s)) {
                            queryResults[@"operatingSystem.appProductID"] = s;
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetWindowsProductID failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsProductID failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Registry query to get Windows release version
        /// </summary>
        ///
        /// <param name="taskId">Database assigned task Id.</param>
        /// <param name="wmiRegistry">WMI registry provider.</param>
        /// <param name="queryResults">Dictionary for formatted data row entry.</param>
        ///
        /// <returns>Operation result code (one of the RC_ constants).</returns>
        private static ResultCodes GetWindowsProductReleaseVersion(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_READ_REGISTRY;

            try {
                ManagementBaseObject inputParameters = wmiRegistry.GetMethodParameters(RegistryMethodNames.GET_STRING_VALUE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.DEF_KEY, RegistryTrees.HKEY_LOCAL_MACHINE);
                inputParameters.SetPropertyValue(RegistryPropertyNames.SUB_KEY_NAME, s_registryKeyCurrentVersion);
                inputParameters.SetPropertyValue(RegistryPropertyNames.VALUE_NAME, @"ProductName");

                ManagementBaseObject outputParameters = null;
                resultCode = Lib.InvokeRegistryMethod(taskId,
                                                      wmiRegistry,
                                                      RegistryMethodNames.GET_STRING_VALUE,
                                                      s_registryKeyCurrentVersion,
                                                      inputParameters,
                                                      out outputParameters);

                if (resultCode == ResultCodes.RC_SUCCESS && null != outputParameters) {
                    using (outputParameters) {
                        String productName = outputParameters.GetPropertyValue(RegistryPropertyNames.S_VALUE) as string;
                        if (!String.IsNullOrEmpty(productName)) {
                            if (s_serviceReleaseRegex.IsMatch(productName)) {
                                MatchCollection mc = s_serviceReleaseRegex.Matches(productName);
                                String releaseVersionFromRegistry = mc[0].Groups[1].Value;
                                if (!string.IsNullOrEmpty(releaseVersionFromRegistry)) {
                                    if (!queryResults.ContainsKey(@"operatingSystem.serviceRelease")) {
                                        queryResults[@"operatingSystem.serviceRelease"] = mc[0].Groups[1].Value;
                                    } else {
                                        // Use WMI value as default.
                                        Lib.Logger.TraceEvent(TraceEventType.Information,
                                              0,
                                              "Collection script WindowsStaticScript. WMI Service Release Version: {0}, Registry {1}",
                                              queryResults[@"operatingSystem.serviceRelease"], releaseVersionFromRegistry);
                                    }
                                    
                                }
                            }
                        }
                    }
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetWindowsProductID failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetWindowsProductID failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Handler for the Win32_LogicalShareSecuritySetting query.  Invokes Method
        /// GetSecurityDescriptor to collect trustee name.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void ShareSecurityResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            StringBuilder builder = new StringBuilder();
            try {
                foreach (ManagementObject mo in queryResults) {
                    if (builder.Length > 0) {
                        builder.Append(BdnaDelimiters.DELIMITER1_TAG);
                    }

                    string shareName = (mo.Properties[@"Name"].Value).ToString();
                    builder.Append(@"ShareName=")
                           .Append(shareName)
                           .Append(BdnaDelimiters.DELIMITER2_TAG);

                    InvokeMethodOptions options = new InvokeMethodOptions();
                    ManagementBaseObject outParamsMthd = mo.InvokeMethod("GetSecurityDescriptor", null, options);
                    ManagementBaseObject descriptor = outParamsMthd["Descriptor"] as ManagementBaseObject;
                    ManagementBaseObject[] dacl = descriptor["DACL"] as ManagementBaseObject[];

                    int count = 0;
                    foreach (ManagementBaseObject ace in dacl) {
                        ManagementBaseObject trustee = ace["Trustee"] as ManagementBaseObject;
                        if (count != 0) {
                            builder.Append(",");
                        } else {
                            builder.Append(@"Trustees=");
                        }
                        string trusteeName = (string)trustee["Name"];
                        builder.Append(trusteeName);
                        ++count;
                    }
                }
                processedResults["operatingSystem.shareInfo"] = builder.ToString();
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Collection script WindowsStaticScript. Unexpected exception. {0}",
                                      ex.Message);
            }
        }

        /// <summary>
        /// Collect User Account Information 
        /// </summary>
        /// <param name="taskId">Task Id for logging.</param>
        /// <param name="scope">WMI connection to target machine.</param>
        /// <returns>Operation result code.</returns>
        private static ResultCodes GetUserDesktopSettings(
                string taskId,
                ManagementScope scope,
                IDictionary<string, string> queryResults) {
            Stopwatch sw = Stopwatch.StartNew();
            ResultCodes resultCode = ResultCodes.RC_INSUFFICIENT_PRIVILEGE_TO_RUN_WMI_QUERY;
            StringBuilder dataRowEntry = new StringBuilder();
            StringBuilder profileLine = new StringBuilder();
            string userName = null;

            try {
                ManagementObjectSearcher mos = new ManagementObjectSearcher(
                    @"Select * from Win32_UserAccount where Domain=""" + computerName + @"""");
                mos.Scope = scope;
                ManagementObjectCollection moc = null;
                resultCode = Lib.ExecuteWqlQuery(taskId, mos, out moc);

                if (ResultCodes.RC_SUCCESS == resultCode && null != moc) {
                    using (moc) {
                        foreach (ManagementObject mo in moc) {
                            profileLine.Length = 0;
                            if (0 != dataRowEntry.Length) {
                                dataRowEntry.Append(BdnaDelimiters.DELIMITER1_TAG);
                            }
                            if ((mo.Properties[@"Name"].Value) != null) {
                                userName = (mo.Properties[@"Name"].Value).ToString();
                                profileLine.Append("Name=").Append(userName).Append(BdnaDelimiters.DELIMITER2_TAG);
                            }
                            if ((mo.Properties[@"FullName"].Value) != null) {
                                string fullName = (mo.Properties[@"FullName"].Value).ToString();
                                profileLine.Append("FullName=").Append(fullName).Append(BdnaDelimiters.DELIMITER2_TAG);
                            }
                            if ((mo.Properties[@"Disabled"].Value) != null) {
                                string disabled = (mo.Properties[@"Disabled"].Value).ToString();
                                profileLine.Append("Disabled=").Append(disabled).Append(BdnaDelimiters.DELIMITER2_TAG);
                            }
                            if ((mo.Properties[@"PasswordChangeable"].Value) != null) {
                                string passwordChangeable = (mo.Properties[@"PasswordChangeable"].Value).ToString();
                                profileLine.Append("PasswordChangeable=").Append(passwordChangeable).Append(BdnaDelimiters.DELIMITER2_TAG);
                            }
                            if ((mo.Properties[@"PasswordExpires"].Value) != null) {
                                string passwordExpires = (mo.Properties[@"PasswordExpires"].Value).ToString();
                                profileLine.Append("PasswordExpires=").Append(passwordExpires).Append(BdnaDelimiters.DELIMITER2_TAG);
                            }
                            if ((mo.Properties[@"PasswordRequired"].Value) != null) {
                                string passwordRequired = (mo.Properties[@"PasswordRequired"].Value).ToString();
                                profileLine.Append("PasswordRequired=").Append(passwordRequired).Append(BdnaDelimiters.DELIMITER2_TAG);
                            }

                            ManagementObjectSearcher mos1 = new ManagementObjectSearcher(
                                    @"Select * from Win32_Desktop where Name like ""%" + userName + @"%""");
                            mos1.Scope = scope;
                            ManagementObjectCollection moc1 = null;
                            ResultCodes rc = Lib.ExecuteWqlQuery(taskId, mos1, out moc1);

                            if (ResultCodes.RC_SUCCESS == rc && null != moc1) {
                                using (moc1) {
                                    foreach (ManagementObject mo1 in moc1) {
                                        if ((mo1.Properties[@"ScreenSaverExecutable"].Value) != null) {
                                            string screenSaverExecutable = (mo1.Properties[@"ScreenSaverExecutable"].Value).ToString();
                                            profileLine.Append("ScreenSaverExecutable=").Append(screenSaverExecutable).Append(BdnaDelimiters.DELIMITER2_TAG);
                                        }
                                        if (mo1.Properties[@"ScreenSaverSecure"].Value != null) {
                                            string screenSaverSecure = (mo1.Properties[@"ScreenSaverSecure"].Value).ToString();
                                            profileLine.Append("ScreenSaverSecure=").Append(screenSaverSecure).Append(BdnaDelimiters.DELIMITER2_TAG);
                                        }
                                        if ((mo1.Properties[@"ScreenSaverTimeout"].Value) != null) {
                                            string screenSaverTimeout = (mo1.Properties[@"ScreenSaverTimeout"].Value).ToString();
                                            profileLine.Append("ScreenSaverTimeout=").Append(screenSaverTimeout).Append(BdnaDelimiters.DELIMITER2_TAG);
                                        }
                                        if ((mo1.Properties[@"ScreenSaverActive"].Value) != null) {
                                            string screenSaverActive = (mo1.Properties[@"ScreenSaverActive"].Value).ToString();
                                            profileLine.Append("ScreenSaverActive=").Append(screenSaverActive).Append(BdnaDelimiters.DELIMITER2_TAG);
                                        }
                                    }
                                }
                            } else {
                                resultCode = rc;
                            }
                            if (0 != profileLine.Length) {
                                dataRowEntry.Append(profileLine);
                            }
                        }
                    }
                }
                if (0 != dataRowEntry.Length) {
                    queryResults["operatingSystem.userInfo"] = dataRowEntry.ToString();
                }
            } catch (ManagementException me) {
                Lib.LogManagementException(taskId,
                                           sw,
                                           "Script method GetUserDesktopSettings failed",
                                           me);
            } catch (Exception ex) {
                Lib.LogException(taskId,
                                 sw,
                                 "Script method GetUserDesktopSettings failed",
                                 ex);
            }
            return resultCode;
        }

        /// <summary>
        /// Handler for the Win32_PerfRawData_PerfOS_System query to calculate system uptime
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void BootUpTimeResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            UInt64 intPerfTimeStamp = 0;
            UInt64 intPerfTimeFreq = 0;
            UInt64 intelapsedTimeCounter = 0;

            foreach (ManagementObject mo in queryResults) {
                foreach (PropertyData pd in mo.Properties) {
                    if (pd.Name.Equals(@"Timestamp_Object")) {
                        intPerfTimeStamp = Convert.ToUInt64(pd.Value);
                    } else if (pd.Name.Equals(@"Frequency_Object")) {
                        intPerfTimeFreq = Convert.ToUInt64(pd.Value);
                    } else if (pd.Name.Equals(@"SystemUpTime")) {
                        intelapsedTimeCounter = Convert.ToUInt64(pd.Value);
                    }
                }
            }

            if ((intPerfTimeStamp > intelapsedTimeCounter) && (intPerfTimeFreq > 0)) {

                UInt64 uptimeInSecs = (intPerfTimeStamp - intelapsedTimeCounter) / intPerfTimeFreq;
                if (0 != uptimeInSecs) {
                    processedResults["operatingSystem.uptime2"] = uptimeInSecs.ToString();
                }
            }
        }

        /// <summary>
        /// Build Process children Table.
        /// </summary>
        /// <param name="queryResults">WMI collection result.</param>
        /// <returns>Children process table.</returns>
        private static IDictionary<string, IList<string>>
                BuildProcessChildrenTable(ManagementObjectCollection queryResults) {

            /// <summary> Listing of process and its parent process </summary>
            IDictionary<string, string> pTable = new Dictionary<string, string>();

            /// <summary> Listing of process and its children process </summary>
            IDictionary<string, IList<string>> cTable = new Dictionary<string, IList<string>>();

            // Initialize parent and children process table from query result.
            foreach (ManagementObject mo in queryResults) {
                string procId = (Convert.ToUInt32(mo.Properties[@"ProcessId"].Value)).ToString();
                string parentProcId = (Convert.ToUInt32(mo.Properties[@"ParentProcessId"].Value)).ToString();
                if (procId != parentProcId) {
                    pTable.Add(procId, parentProcId);
                }
                cTable.Add(procId, null);
            }

            // Build relationship table.
            foreach (string pid in pTable.Keys) {
                if (cTable[pid] == null) {
                    cTable[pid] = FindChildrenProcesses(pTable, cTable, pid, new List<string>());
                }
            }
            return cTable;
        }


        /// <summary>
        /// Find Children process
        /// </summary>
        /// <param name="pTable">Parent Process Table</param>
        /// <param name="cTable">Children Process Table</param>
        /// <param name="pid">Process ID</param>
        /// <returns></returns>
        private static IList<string> FindChildrenProcesses(
            IDictionary<string, string> pTable,
            IDictionary<string, IList<string>> cTable,
            string pid,
            IList<string> tempList) {

            IList<string> retList = new List<string>();
            if (cTable.ContainsKey(pid) && cTable[pid] != null) {
                return cTable[pid];
            } else {
                foreach (KeyValuePair<string, string> entry in pTable) {
                    string id = entry.Key;
                    string ppid = entry.Value;
                    if (ppid == pid) {
                        retList.Add(id);
                        IList<string> subChildrenList = null;
                        if (!tempList.Contains(id) && !retList.Contains(id)) {
                            subChildrenList = FindChildrenProcesses(pTable, cTable, id, retList);
                            cTable[id] = subChildrenList;
                        }
                        if (subChildrenList != null) {
                            foreach (string newId in subChildrenList) {
                                if (!retList.Contains(newId)) {
                                    retList.Add(newId);
                                }
                            }
                        }
                    }
                }
                cTable[pid] = retList;
            }
            return retList;
        }

        /// <summary>
        /// Handler for the Win32_Process query.  Build up a custom list
        /// of processes for a data row entry.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void OsProcessResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processResults) {

            StringBuilder builder = new StringBuilder();
            int numProcesses = queryResults.Count;
            IDictionary<string, IList<string>> childrenProcessTable = BuildProcessChildrenTable(queryResults);
            try {
                foreach (ManagementObject mo in queryResults) {
                    if (builder.Length > 0) {
                        builder.Append(BdnaDelimiters.DELIMITER1_TAG);
                    }

                    // Process Id and Parent Process Id
                    string pid = (Convert.ToUInt32(mo.Properties[@"ProcessId"].Value)).ToString();
                    builder.Append(@"ProcessId=")
                           .Append('"').Append(pid).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"ParentProcessId=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"ParentProcessId"].Value)).ToString()).Append('"');

                    // Name of process and Command Line
                    string[] owner = new string[2];
                    try {
                        mo.InvokeMethod(@"GetOwner", (object[])owner);
                    } catch (Exception mex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Collection script WindowsStaticScript. Owner retrieval exception. {0} for PID {1}",
                                              mex.Message,
                                              pid);
                    }
                    if (!string.IsNullOrEmpty(owner[0]) && !string.IsNullOrEmpty(owner[1])) {
                        if (owner[1].ToUpper() != "NT AUTHORITY") {
                            builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                                .Append(@"Owner=")
                                .Append('"').Append(owner[1]).Append(@"\").Append(owner[0]).Append('"');
                        } else {
                            builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                                .Append(@"Owner=")
                                .Append('"').Append(owner[0]).Append('"');
                        }
                    } else {
                        builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                            .Append(@"Owner=""""");
                    }

                    // Process Name.
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"Name=")
                           .Append('"').Append(mo.Properties[@"Name"].Value).Append('"');

                    // Command Line Retrieval.
                    // Windows2000 will throw management exception if permission not met.
                    string cmdLine = String.Empty;
                    try {
                        cmdLine = mo.Properties[@"CommandLine"].Value.ToString();
                    } catch (Exception mex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Collection script WindowsStaticScript. Command Line retrieval exception. {0} for processID {1}",
                                              mex.Message,
                                              pid);
                    }
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"CommandLine=")
                           .Append('"').Append(cmdLine).Append('"');

                    // Open Handle Count and Thread Count
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"OpenHandleCount=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"HandleCount"].Value)).ToString()).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"ThreadCount=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"ThreadCount"].Value)).ToString()).Append('"');

                    // CPU Usage Info
                    ulong kernelModeTime = 0, userModeTime = 0;
                    string kernelModeTimeString = @"", userModeTimeString = @"";
                    try {
                        UInt64.TryParse(mo.Properties[@"KernelModeTime"].Value.ToString(), out kernelModeTime);
                        UInt64.TryParse(mo.Properties[@"UserModeTime"].Value.ToString(), out userModeTime);

                        // kernelModeTime is in unit of 100 ns, need to divide by 1000000 to be in seconds
                        kernelModeTime /= 10000;
                        userModeTime /= 10000;
                        if (kernelModeTime.ToString().IndexOf('.') > 0) {
                            kernelModeTimeString = kernelModeTime.ToString().Substring(0, kernelModeTime.ToString().IndexOf('.'));
                        } else {
                            kernelModeTimeString = kernelModeTime.ToString();
                        }

                        if (userModeTime.ToString().IndexOf('.') > 0) {
                            userModeTimeString = userModeTime.ToString().Substring(0, userModeTime.ToString().IndexOf('.'));
                        } else {
                            userModeTimeString = userModeTime.ToString();
                        }
                    } catch (Exception ex) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Collection script WindowsStaticScript. Process CPU Usage number format exception. {0}",
                                              ex.Message);
                    }

                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"CPU_KernelModeTime=")
                           .Append('"').Append(kernelModeTimeString).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"CPU_UserModeTime=")
                           .Append('"').Append(userModeTimeString).Append('"');

                    // Virtual Space Info
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"CurVirtualSpaceMB=")
                           .Append('"').Append((Convert.ToUInt64(mo.Properties[@"VirtualSize"].Value)/1000).ToString()).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"PeakVirtualSpaceMB=")
                           .Append('"').Append((Convert.ToUInt64(mo.Properties[@"PeakVirtualSize"].Value)/1000).ToString()).Append('"');

                    // Physical Memory Info
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"CurPhysicalMemory=")
                           .Append('"').Append((Convert.ToUInt64(mo.Properties[@"WorkingSetSize"].Value)).ToString()).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"PeakPhysicalMemory=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"PeakWorkingSetSize"].Value)).ToString()).Append('"');

                    // Page File Info
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"CurPageFileSize=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"PageFileUsage"].Value)).ToString()).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"PeakPageFileSize=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"PeakPageFileUsage"].Value)).ToString()).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"PageFaults=")
                           .Append('"').Append((Convert.ToUInt32(mo.Properties[@"PageFaults"].Value)).ToString()).Append('"');

                    // Children process Info
                    int childrenCount = 0;
                    StringBuilder buf = new StringBuilder();
                    if (childrenProcessTable.ContainsKey(pid) && childrenProcessTable[pid] != null) {
                        if (childrenProcessTable[pid].Count > 0) {
                            childrenCount = childrenProcessTable[pid].Count;

                            foreach (string childPID in childrenProcessTable[pid]) {
                                if (buf.Length > 0) {
                                    buf.Append(@",");
                                }
                                buf.Append(childPID);
                            }
                        }
                    }
                    builder.Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"Dep_Pid_List=")
                           .Append('"').Append(buf.ToString()).Append('"')
                           .Append(BdnaDelimiters.DELIMITER2_TAG)
                           .Append(@"numDependent=")
                           .Append('"').Append(childrenCount).Append('"');
                }
                processResults[@"operatingSystem.winProcesses"] = builder.ToString();
                processResults[@"operatingSystem.numProcesses"] = numProcesses.ToString();
            } catch (Exception ex) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Collection script WindowsStaticScript. Unexpected exception. {0}",
                                      ex.Message);

                processResults[@"operatingSystem.winProcesses"] = String.Empty;
                processResults[@"operatingSystem.numProcesses"] = String.Empty;
            }
        }

        /// <summary>
        /// Handler for the Win32_OperatingSystem query.  Perform some
        /// additional computations for cpu and memory statistics.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void HardwareMemoryResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {

            UInt64 totalVisibleMemory = 0;
            UInt64 freePhysicalMemory = 0;
            foreach (ManagementObject mo in queryResults) {
                foreach (PropertyData pd in mo.Properties) {
                    if (pd.Name.Equals(@"TotalVisibleMemorySize")) {
                        totalVisibleMemory = Convert.ToUInt64(pd.Value) * 1024L;
                    } else if (pd.Name.Equals(@"FreePhysicalMemory")) {
                        freePhysicalMemory = Convert.ToUInt64(pd.Value) * 1024L;
                    }
                }
            }

            processedResults[@"hardware.totalMemory"] = totalVisibleMemory.ToString();
            processedResults[@"hardware.availableMemory"] = freePhysicalMemory.ToString();
            processedResults[@"hardware.memoryUtilPercent"] = (0 != totalVisibleMemory)
                ? Convert.ToInt32((Convert.ToDecimal(freePhysicalMemory) / Convert.ToDecimal(totalVisibleMemory) * 100m)).ToString()
                : String.Empty;
        }

        /// <summary>
        /// Handler for the Win32_Printer query.  Pretty much the
        /// same as the default handler except we force the sequence
        /// number to always be generated.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void PrinterDiscoveryResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            int count = 0;
            foreach (ManagementObject mo in queryResults) {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    true,
                                    count);
                count++;
            }
        }

        /// <summary>
        /// Handler for the Win32_UserAccount query.  Mostly the same
        /// as default handling except we add a summary entry to the
        /// data row.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void UserAccountResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            bool addSequenceNumber = 1 < queryResults.Count;
            int count = 0;
            foreach (ManagementObject mo in queryResults) {
                ProcessPropertyData(mo.Properties,
                                    propertyMap,
                                    processedResults,
                                    addSequenceNumber,
                                    count);
                count++;
            }
            processedResults[@"operatingSystem.numUsers"] = queryResults.Count.ToString();
        }

        /// <summary>
        /// Handler for the Win32_Service query.  Completely custom
        /// data row with sequenced/correlated data for each service
        /// running on the remote host.
        /// </summary>
        /// 
        /// <param name="queryResults">Collection of WMI query results.</param>
        /// <param name="propertyMap">Map of WMI property names to data row entry names (the
        ///     name part of a datarow name/value pair).</param>
        /// <param name="processedResults">Target collection of processed results.</param>
        private static void NtServicesResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults) {
            StringBuilder sb = new StringBuilder();
            int sequenceNumber = 0;
            foreach (ManagementObject mo in queryResults) {
                ++sequenceNumber;
                sb.Append(BdnaDelimiters.DELIMITER1_TAG)
                  .AppendFormat(@"serviceName({0})=""{1}""", sequenceNumber, mo.Properties[@"Name"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"displayName({0})=""{1}""", sequenceNumber, mo.Properties[@"DisplayName"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"description({0})=""{1}""", sequenceNumber, mo.Properties[@"Description"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"pathName({0})=""{1}""", sequenceNumber, mo.Properties[@"PathName"].Value);

                if (null != mo.Properties[@"InstallDate"]) {
                    string installDate = mo.Properties[@"InstallDate"].Value as string;
                    if (!String.IsNullOrEmpty(installDate)) {
                        sb.Append(BdnaDelimiters.DELIMITER2_TAG)
                          .AppendFormat(@"InstallDate({0})=""{1}""", sequenceNumber, installDate);
                    }
                }
                sb.Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"serviceType({0})=""{1}""", sequenceNumber, mo.Properties[@"ServiceType"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"is_Desktop_Interact({0})=""{1}""", sequenceNumber, mo.Properties[@"DesktopInteract"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"startMode({0})=""{1}""", sequenceNumber, mo.Properties[@"StartMode"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"logOnUser({0})=""{1}""", sequenceNumber, mo.Properties[@"StartName"].Value)
                  .Append(BdnaDelimiters.DELIMITER2_TAG)
                  .AppendFormat(@"state({0})=""{1}""", sequenceNumber, mo.Properties[@"State"].Value);
            }
            processedResults[@"operatingSystem.services"] = sb.ToString();
        }

        /// <summary>
        /// This is the signature for all WMI query result handlers.
        /// </summary>
        private delegate void CimvQueryResultHandler(
                ManagementObjectCollection queryResults,
                NameValueCollection propertyMap,
                IDictionary<string, string> processedResults);

        /// <summary>
        /// This is the signature for all of the registry query
        /// methods.
        /// </summary>
        private delegate ResultCodes RegistryQuery(
                string taskId,
                ManagementClass wmiRegistry,
                IDictionary<string, string> processedResults);

        /// <summary>
        /// This class is the table entry for the CIMV query table.
        /// It essentially binds a class to query with the properties
        /// of interest and a handler to process the query results.
        /// </summary>
        private class CimvQueryTableEntry {
            /// <summary>
            /// Construct a table entry with default query
            /// enumeration options.
            /// </summary>
            /// 
            /// <param name="className">WMI class name to query.</param>
            /// <param name="classProperties">String array of WMI properties to query the WMI class for.</param>
            /// <param name="dataRowItemNames">List of matching data row item names.  These are the names
            ///     of the name/value pairs in the resulting data row.  The
            ///     order of these names MUST match the order of property
            ///     names in classProperties.</param>
            /// <param name="resultHandler">Delegate to process the query results.</param>
            /// <param name="enumerationOptions">Query enumeration options.</param>
            public CimvQueryTableEntry(
                    string className,
                    string[] classProperties,
                    string[] dataRowItemNames,
                    CimvQueryResultHandler resultHandler)
                : this(className,
                           classProperties,
                           dataRowItemNames,
                           resultHandler,
                           new EnumerationOptions(null, Lib.WmiMethodTimeout, Lib.WmiBlockSize, true, true, false, false, false, false, false)) {
            }

            /// <summary>
            /// Full constructor with all values specified.
            /// </summary>
            /// 
            /// <param name="className">WMI class name to query.</param>
            /// <param name="classProperties">String array of WMI properties to query the WMI class for.</param>
            /// <param name="dataRowItemNames">List of matching data row item names.  These are the names
            ///     of the name/value pairs in the resulting data row.  The
            ///     order of these names MUST match the order of property
            ///     names in classProperties.</param>
            /// <param name="resultHandler">Delegate to process the query results.</param>
            /// <param name="enumerationOptions">Query enumeration options.</param>
            public CimvQueryTableEntry(
                    string className,
                    string[] classProperties,
                    string[] dataRowItemNames,

                    CimvQueryResultHandler resultHandler,
                    EnumerationOptions enumerationOptions) {

                m_className = className;
                m_resultHandler = resultHandler;
                m_enumerationOptions = enumerationOptions;
                int mapSize = (null == classProperties) ? 0 : classProperties.Length;
                if (0 == mapSize) {
                    m_propertyMap = new NameValueCollection(1);
                    m_propertyMap[@"*"] = null;
                } else {
                    m_propertyMap = new NameValueCollection(mapSize);
                    for (int i = 0;
                         classProperties.Length > i;
                         ++i) {
                        m_propertyMap[classProperties[i]] = (null == dataRowItemNames || dataRowItemNames.Length <= i)
                            ? null
                            : dataRowItemNames[i];
                    }
                }
            }

            /// <summary>
            /// Execute the WMI query for this table entry.
            /// </summary>
            /// 
            /// <param name="taskId">Database assigned task Id.</param>
            /// <param name="scope">WMI connection to use.</param>
            /// <param name="results">Target collection to populate with results.</param>
            /// 
            /// <returns>Operation result code.</returns>
            public ResultCodes ExecuteQuery(
                    string taskId,
                    ManagementScope scope,
                    IDictionary<string, string> results) {

                ResultCodes resultCode = ResultCodes.RC_SUCCESS;
                ManagementObjectCollection moc = null;
                ManagementObjectSearcher mos = null;

                if (m_className != @"Win32_Process") {
                    mos = new ManagementObjectSearcher(scope,
                                                        new SelectQuery(m_className,
                                                                        null,
                                                                        m_propertyMap.AllKeys));
                } else {
                    mos = new ManagementObjectSearcher(scope,
                                                        new SelectQuery("select * from " + m_className));
                }

                using (mos) {
                    resultCode = Lib.ExecuteWqlQuery(taskId, mos, m_enumerationOptions, out moc);
                }

                //
                // Retry on any failure except query timeout.
                if (ResultCodes.RC_SUCCESS != resultCode && ResultCodes.RC_WMI_QUERY_TIMEOUT != resultCode) {
                    string originalQuery = mos.Query.QueryString;
                    mos = new ManagementObjectSearcher(scope,
                                                       new SelectQuery(m_className));

                    Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                          0,
                                          "Task Id {0}: Retrying failed query {1} as\n{2}",
                                          taskId,
                                          originalQuery,
                                          mos.Query.QueryString);

                    using (mos) {
                        resultCode = Lib.ExecuteWqlQuery(taskId, mos, m_enumerationOptions, out moc);
                    }
                }

                if (null != moc) {
                    using (moc) {
                        if (ResultCodes.RC_SUCCESS == resultCode) {
                            m_resultHandler(moc, m_propertyMap, results);
                        }
                    }
                }

                // @todo for now, always return success.  We've
                // observed cases where an individual query will
                // fail, but all the others will work.  Gather
                // and return as much data as possible.  We
                // should create a warning result code.
                return ResultCodes.RC_SUCCESS;
            }

            /// <summary>WMI class name to query.</summary>
            private string m_className;

            /// <summary>
            /// Delegate to call to process the records returned by our
            /// WQL query.
            /// </summary>
            private CimvQueryResultHandler m_resultHandler;

            /// <summary>
            /// Maps the properties used WQL query to the item names used
            /// in generating the data row.  This collection caches the
            /// array of property names, which is perfect for our needs.
            /// </summary>
            private NameValueCollection m_propertyMap;

            private EnumerationOptions m_enumerationOptions;
        }

        /// <summary>Simple handler to match up query results to data row entries.</summary>
        private static CimvQueryResultHandler s_defaultResultHandler = new CimvQueryResultHandler(DefaultResultHandler);

        /// <summary>Handler for the Win32_ComputerSystem query.</summary>
        private static CimvQueryResultHandler s_computerSystemResultHandler = new CimvQueryResultHandler(ComputerSystemResultHandler);

        /// <summary>
        /// Global Variable to store name of computer
        /// </summary>
        private static string computerName = null;

        /// <summary>Handler for the Win32_OperatingSystem query.</summary>
        private static CimvQueryResultHandler s_osInformationResultHandler = new CimvQueryResultHandler(OsInformationResultHandler);

        /// <summary>Handler for the Win32_PerfRawData_PerfOS_System query.</summary>
        private static CimvQueryResultHandler s_bootUpTimeResultHandler = new CimvQueryResultHandler(BootUpTimeResultHandler);

        /// <summary>Handler for the Win32_Process query.</summary>
        private static CimvQueryResultHandler s_osProcessResultHandler = new CimvQueryResultHandler(OsProcessResultHandler);

        /// <summary>Handler for the Win32_OperatingSystem query.</summary>
        private static CimvQueryResultHandler s_hardwareMemoryResultHandler = new CimvQueryResultHandler(HardwareMemoryResultHandler);

        /// <summary>Handler for the Win32_LogicalShareSecuritySetting query.</summary>
        private static CimvQueryResultHandler s_shareSecurityResultHandler = new CimvQueryResultHandler(ShareSecurityResultHandler);

        /// <summary>Handler for the Win32_Printer query.</summary>
        private static CimvQueryResultHandler s_printerDiscoveryResultHandler = new CimvQueryResultHandler(PrinterDiscoveryResultHandler);

        /// <summary>Handler for the Win32_UserAccount query.</summary>
        private static CimvQueryResultHandler s_userAccountResultHandler = new CimvQueryResultHandler(UserAccountResultHandler);

        /// <summary>Handler for the Win32_Service query.</summary>
        private static CimvQueryResultHandler s_ntServicesResultHandler = new CimvQueryResultHandler(NtServicesResultHandler);

        /// <summary>Handler for the Windows Hotfix query.</summary>
        private static CimvQueryResultHandler s_hotfixResultHandler = new CimvQueryResultHandler(HotfixResultHandler);

        /// <summary>
        /// WMI query table.  This table contains an entry for 
        /// each WMI query we want to make.  Each entry specifies
        /// which WMI class to query and what properties from the
        /// class we are interested in.  The properties we're
        /// interested in are bound to the associated data row
        /// item names and a specific handler to process the query
        /// results into a data row entry.
        /// </summary>
        private static CimvQueryTableEntry[] s_cimvQueryTable = {
            new CimvQueryTableEntry(@"Win32_OperatingSystem",
                                    new string[] {@"LastBootUpTime",
                                                  @"Version",
                                                  @"CSDVersion",                                                  
                                                  @"Caption",
                                                  @"BuildNumber",
                                                  @"TotalSwapSpaceSize",
                                                  @"InstallDate",
                                                  @"Description",
                                                  @"OtherTypeDescription",
                                                  @"OSLanguage"},
                                    new string[] {@"operatingSystem.lastBootUpTime",
                                                  @"operatingSystem.version",
                                                  @"operatingSystem.patchLevel",
                                                  @"operatingSystem.installDate",
                                                  @"operatingSystem.osLanguage"},
                                    s_osInformationResultHandler),

            new CimvQueryTableEntry(@"Win32_OperatingSystem",
                                    new string[] {@"OSArchitecture"},
                                    new string[] {@"operatingSystem.osArchitecture"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_PerfRawData_PerfOS_System",
                                   new string[] {@"Frequency_Object",
                                                 @"SystemUpTime",
                                                 @"Timestamp_Object"},
                                   null,
                                   s_bootUpTimeResultHandler),

            new CimvQueryTableEntry(@"Win32_ComputerSystem",
                                    new string[] {@"Domain",
                                                  @"Name",
                                                  @"Domain",
                                                  @"Model",
                                                  @"Manufacturer",
                                                  @"DomainRole"},

                                    new string[] {@"operatingSystem.domain",
                                                  @"host.hostName",
                                                  @"host.domain",
                                                  @"hardware.model",
                                                  @"hardware.manufacturer",
                                                  @"host.dwmembership"},
                                    s_computerSystemResultHandler),

            new CimvQueryTableEntry(@"Win32_Process",
                                    new string[] {@"Name",
                                                  @"ProcessId" ,
                                                  @"CommandLine",
                                                  @"HandleCount",
                                                  @"KernelModeTime",
                                                  @"UserModeTime",
                                                  @"WorkingSetSize",
                                                  @"VirtualSize",
                                                  @"PageFileUsage",
                                                  @"PeakWorkingSetSize",
                                                  @"PeakPageFileUsage",
                                                  @"PeakVirtualSize",
                                                  @"ParentProcessId",
                                                  @"PageFaults",
                                                  @"ThreadCount"},
                                    null,
                                    s_osProcessResultHandler),

            new CimvQueryTableEntry(@"Win32_BIOS",
                                    new string[] {@"SerialNumber",
                                                  @"InstallDate",
                                                  @"ReleaseDate",
                                                  @"BIOSVersion",
                                                  @"Version"},

                                    new string[] {@"hardware.serialNumber",
                                                  @"bios.InstallDate",
                                                  @"bios.ReleaseDate",
                                                  @"bios.BiosVersion",
                                                  @"bios.Version"},

                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_BaseBoard",
                                    new string[] {@"SerialNumber",
                                                  @"Tag",
                                                  @"OtherIdentifyingInfo",
                                                  @"Product",
                                                  @"PartNumber"},

                                    new string[] {@"hardware.baseboardSerialNumber",
                                                  @"hardware.baseboardTag",
                                                  @"hardware.baseboardOtherInfo",
                                                  @"hardware.baseboardPartNumber",
                                                  @"hardware.physicalElementPartNumber"},

                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_OperatingSystem",
                                    new string[] {@"TotalVisibleMemorySize",
                                                  @"FreePhysicalMemory"},
                                    null,
                                    s_hardwareMemoryResultHandler),

            new CimvQueryTableEntry(@"Win32_ComputerSystemProduct",
                                    new string[] {@"IdentifyingNumber",
                                                  @"UUID"},
                                    new string[] {@"hardware.identifyingNumber",
                                                  @"hardware.UUID"},
                                    s_defaultResultHandler),


            new CimvQueryTableEntry(@"Win32_SystemEnclosure",
                                    new string[] {@"SerialNumber",
                                                  @"SMBIOSAssetTag"},
                                    new string[] {@"hardware.systemEnclosureSerialNumber",
                                                  @"hardware.SMBIOSAssetTag"},
                                    s_defaultResultHandler),

//             new CimvQueryTableEntry(@"Win32_ComputerSystem",
//                                     new string[] {@"NumberOfProcessors"},
//                                     new string[] {@"hardware.numCPUs"},
//                                     s_defaultResultHandler),

/* Bug 11433 - Commenting out attributes LoadPercentage, NumberOfCores, NumberOfLogicalProcessors
   as these were causing entries to be made in event logs of target machines.
   We are not doing anything with these attributes at this point, so commenting out makes sense.
   We will need to revisit the Cores/thread entries with Windows Vista */

            new CimvQueryTableEntry(@"Win32_Processor",
                                    new string[] {@"ProcessorId",
                                                  @"DeviceID",
                                                  @"Manufacturer",
                                                  @"MaxClockSpeed",
                                                  @"Name",
                                                  //@"LoadPercentage",
                                                  //@"LoadPercentage",
                                                  @"SocketDesignation",
                                                  @"DataWidth",
                                                  @"Architecture",
                                                  @"CurrentClockSpeed"},
                                                  //@"NumberOfCores",
                                                  //@"NumberOfLogicalProcessors"},
                                    new string[] {@"cpu.processorId",
                                                  @"cpu.id",
                                                  @"cpu.manufacturer",
                                                  @"cpu.speed",
                                                  @"cpu.model",
                                                  //@"cpu.percentUsed",
                                                  //@"hardware.CPUUtilPercent",
                                                  @"cpu.socket",
                                                  @"cpu.bits",
                                                  @"cpu.isa",
                                                  @"cpu.currSpeed"},
                                                  //@"cpu.cores",
                                                  //@"cpu.threads"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_OperatingSystem",
                                    new string[] {@"BootDevice"},
                                    new string[] {@"hardDrive.bootPartition"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_DiskDrive",
                                    new string[] {@"Model",
                                                  @"Manufacturer",
                                                  @"InterfaceType",
                                                  @"Size",
                                                  @"Caption"},

                                    new string[] {@"hardDrive.model",
                                                  @"hardDrive.manufacturer",
                                                  @"hardDrive.type",
                                                  @"hardDrive.capacity",
                                                  @"hardDrive.idString"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_LogicalDisk",
                                    new string[] {@"DeviceID",
                                                  @"FileSystem",
                                                  @"QuotasDisabled",
                                                  @"FreeSpace",
                                                  @"ProviderName",
                                                  @"Size",
                                                  @"VolumeSerialNumber"},

                                    new string[] {@"fileSystem.mountPoint",
                                                  @"fileSystem.type",
                                                  @"fileSystem.quotasDisabled",
                                                  @"fileSystem.availableCapacity",
                                                  @"fileSystem.networkPath",
                                                  @"fileSystem.capacity",
                                                  @"fileSystem.volumeSN"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_LogicalShareSecuritySetting",
                                    new string[] {@"Name"},
                                    null,
                                    s_shareSecurityResultHandler),

            new CimvQueryTableEntry(@"Win32_CDROMDrive",
                                    new string[] {@"Drive",
                                                  @"Manufacturer",
                                                  @"Caption"},

                                    new string[] {@"CDROM.drive",
                                                  @"CDROM.manufacturer",
                                                  @"CDROM.idString"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_POTSModem",
                                    new string[] {@"StatusInfo",
                                                  @"Description",
                                                  @"Model",
                                                  @"DeviceType",
                                                  @"MaxBaudRateToPhone"},

                                    new string[] {@"modem.isActive",
                                                  @"modem.description",
                                                  @"modem.model",
                                                  @"modem.type",
                                                  @"modem.capacity"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_PCMCIAController",
                                    new string[] {@"Description",
                                                  @"DeviceID",
                                                  @"Manufacturer",
                                                  @"StatusInfo",
                                                  @"Name"},

                                    new string[] {@"PCMCIACard.description",
                                                  @"PCMCIACard.ID",
                                                  @"PCMCIACard.manufacturer",
                                                  @"PCMCIACard.statusInfo",
                                                  @"PCMCIACard.name"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_NetworkAdapter",
                                    new string[] {@"PNPDeviceID",
                                                  @"ProductName",
                                                  @"Speed",
                                                  @"Manufacturer",
                                                  @"MACAddress"},

                                    new string[] {@"NIC.PNPDeviceID",
                                                  @"NIC.name",
                                                  @"NIC.speed",
                                                  @"NIC.manufacturer",
                                                  @"NIC.MACAddress"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_NetworkAdapterConfiguration",
                                    new string[] {@"DNSHostName",
                                                  @"DNSServerSearchOrder",
                                                  @"IPAddress",
                                                  @"IPXMediaType",
                                                  @"DefaultIPGateway",
                                                  @"IPSubnet",
                                                  @"IPUseZeroBroadcast",
                                                  @"DHCPServer",
                                                  @"DHCPEnabled",
                                                  @"DHCPLeaseExpires",
                                                  @"WINSPrimaryServer",
                                                  @"WINSSecondaryServer"},

                                    new string[] {@"NIC.DNSServers",
                                                  @"NIC.DNSServerSearchOrder",
                                                  @"NIC.IPAddress",
                                                  @"NIC.mediaType",
                                                  @"NIC.defaultRoute",
                                                  @"NIC.networkMask",
                                                  @"NIC.broadcastAddress",
                                                  @"NIC.dhcpServer",
                                                  @"NIC.dhcpEnabled",
                                                  @"NIC.dhcpLeaseExpires",
                                                  @"NIC.WINSPrimaryServer",
                                                  @"NIC.WINSSecondaryServer"},
                                    s_defaultResultHandler),

            new CimvQueryTableEntry(@"Win32_VideoController",
                                    new string[] {@"Name",
                                                  @"Description",
                                                  @"AdapterCompatibility",
                                                  @"AdapterRAM"},

                                    new string[] {@"video.Name",
                                                  @"video.Description",
                                                  @"video.AdapterCompatibility",
                                                  @"video.AdapterRAM"},
                                    s_defaultResultHandler),
                                                

//             new CimvQueryTableEntry(@"Win32_Printer",
//                                     new string[] {@"Name",
//                                                   @"PortName",
//                                                   @"ShareName",
//                                                   @"ServerName",
//                                                   @"Local",
//                                                   @"WorkOffline",
//                                                   @"DriverName"},
//                                     new string[] {@"printer.name",
//                                                   @"printer.port",
//                                                   @"printer.sharedName",
//                                                   @"printer.serverName",
//                                                   @"printer.local",
//                                                   @"printer.WorkOffline",
//                                                   @"printer.driverName"},
//                                     s_printerDiscoveryResultHandler,
//                                     new EnumerationOptions(null,
//                                                            TimeSpan.MaxValue,
//                                                            1,
//                                                            false,
//                                                            true,
//                                                            false,
//                                                            false,
//                                                            false,
//                                                            false,
//                                                            false)),
//
//             new CimvQueryTableEntry(@"Win32_Group",
//                                     new string[] {@"Name"},
//                                     new string[] {@"operatingSystem.groups"},
//                                     s_defaultResultHandler),
//
//             new CimvQueryTableEntry(@"Win32_UserAccount",
//                                     new string[] {@"Caption"},
//                                     new string[] {@"operatingSystem.users"},
//                                     s_userAccountResultHandler),

            new CimvQueryTableEntry(@"Win32_Service",
                                    new string[] {@"Name",
                                                  @"DisplayName",
                                                  @"Description",
                                                  @"PathName",
                                                  @"InstallDate",
                                                  @"ServiceType",
                                                  @"DesktopInteract",
                                                  @"StartMode",
                                                  @"StartName",
                                                  @"State"},
                                    null,
                                    s_ntServicesResultHandler)
        };

        private static CimvQueryTableEntry s_hotFixQuery = new CimvQueryTableEntry(@"Win32_QuickFixEngineering",
                                                                                   new string[] {@"HotFixID",
                                                                                                 @"Description",
                                                                                                 @"InstalledOn"},
                                                                                   null,
                                                                                   s_hotfixResultHandler);

        /// <summary>
        /// This delegate contains instances of all of the methods
        /// we want to call to obtain information from the registry.
        /// This is just a convenient way for the main entry point
        /// to call of the registry access methods without really
        /// knowing how many there are.  If the list every gets
        /// too big we will have the option of invoking the delegate
        /// asynchronously which would allow the registry calls to
        /// run concurrently (a little extra synchronization code
        /// will be needed to do this).
        /// </summary>
        private static RegistryQuery s_registryDelegates = new RegistryQuery(GetBuildNumAndGUIDFromRegistry)
                                                         + new RegistryQuery(GetWindowsProductType)
                                                         + new RegistryQuery(GetWindowsLastLogon)
                                                         + new RegistryQuery(GetWindowsProductID)
                                                         + new RegistryQuery(GetWindowsProductReleaseVersion)
                                                         + new RegistryQuery(GetWindowsLicenseKey)
                                                         + new RegistryQuery(GetPrintersViaRegistry)
                                                         + new RegistryQuery(GetCPUModelViaRegistry);
                                                         // Bug 17320
                                                         // + new RegistryQuery(GetPowerSettings);                                                                    

        /// <summary>
        /// List of registry value names we're interested in for
        /// installed software.
        /// </summary>
        private static readonly string[] s_installedSoftwareDataRowItemNames = {@"DisplayName",
                                                                                @"DisplayVersion",
                                                                                @"InstallDate",
                                                                                @"InstallLocation",
                                                                                @"Publisher"};
        /// <summary>
        /// Map of registry value names to data row item names
        /// for printer registry collection.  Populated
        /// in the static initializer for this class.
        /// </summary>
        private static IDictionary<string, string> s_printerKeyNameToRowNameMap = new Dictionary<string, string>();

        /// <summary>Registry path for product options.</summary>
        private static readonly string s_registryKeyProductOptions = @"SYSTEM\CurrentControlSet\Control\ProductOptions";

        /// <summary>Registry path for Windows logon data.</summary>
        private static readonly string s_registryKeyWinlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        private static readonly string s_registryKeyWinlogonVista = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI";

        /// <summary>Registry path for Windows Product Name.</summary>
        private static readonly string s_registryWinProdName = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";


        /// <summary>Registry path for Windows make &amp; model info.</summary>
        private static readonly string s_registryKeyCurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        /// <summary>Registry path for installed software information.</summary>
        private static readonly string s_registryKeyUninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private static readonly string s_registryKeyUninstall64 = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        /// <summary>Registry path for installed printer information.</summary>
        private static readonly string s_registryKeyPrinters = @"SYSTEM\CurrentControlSet\Control\Print\Printers";

        /// <summary>Registry path for monitor EDID information.</summary>
        private static readonly string s_registryKeyMonitors = @"SYSTEM\CurrentControlSet\Enum";

        /// <summary>Registry path for CPU data information.</summary>
        private static readonly string s_registryKeyCPUs = @"HARDWARE\DESCRIPTION\System\CentralProcessor";


        private static readonly Regex s_hotFixRegex = new Regex(@".*(Q\d+|KB\d+)", RegexOptions.Compiled);
        private static readonly Regex s_dateTimeRegex = new Regex(@"(\d\d\d\d)(\d\d)(\d\d)(\d\d)(\d\d).*");
        private static readonly Regex s_2000Regex = new Regex(@"(2000)", RegexOptions.Compiled);
        private static readonly Regex s_vistaRegex = new Regex(@"([v|V]ista)", RegexOptions.Compiled);
        private static readonly Regex s_2008Regex = new Regex(@"(2008)", RegexOptions.Compiled);
        private static readonly Regex s_serviceReleaseRegex = new Regex(@"\s*(SR\s*\d+|R\s*\d+)", RegexOptions.Compiled);        
    }
}
