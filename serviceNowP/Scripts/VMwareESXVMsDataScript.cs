#region Copyright
/******************************************************************
*
*          Module: VMwareESXVMsDataScript
* Original Author: Rekha Rani
*   Creation Date: 2006/01/17
*
* Current Status
*       $Revision: 1.12 $
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

using System.Web.Services.Protocols;

using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using bdna.ScriptLib;
using bdna.Shared;

using VimApi; // the VirtualInfrastructureManagement Service Reference

namespace bdna.Scripts {
    public class VMwareESXVMsDataScript : ICollectionScriptRuntime {

        private string m_taskId;
        private long m_cleId;
        private long m_elementId;
        private long m_databaseTimestamp;
        private long m_localTimestamp;
        private IDictionary<string, string> m_attributes;
        private IDictionary<string, string> m_scriptParameters;
        private ITftpDispatcher m_tftpDispatcher = null;
        private IDictionary<string, object> m_connection;
        private Stopwatch m_executionTimer = null;

        /// <summary>Data row buffer.</summary>
        private StringBuilder m_dataRow = new StringBuilder();


        // VIM variablres
        private VimService vim_svc = null;
        private ServiceContent vim_svc_content = null;
        private ManagedObjectReference vim_svc_ref = null;

        public CollectionScriptResults ExecuteTask(long taskId, long cleId, long elementId, long databaseTimestamp, long localTimestamp, IDictionary<string, string> attributes, IDictionary<string, string> scriptParameters, IDictionary<string, object> connection, string tftpPath, string tftpPath_login, string tftpPath_password, ITftpDispatcher tftpDispatcher) {

            m_taskId = taskId.ToString();
            m_cleId = cleId;
            m_elementId = elementId;
            m_databaseTimestamp = databaseTimestamp;
            m_localTimestamp = localTimestamp;
            m_attributes = attributes;
            m_scriptParameters = scriptParameters;
            m_tftpDispatcher = tftpDispatcher;
            m_connection = connection;
            m_executionTimer = Stopwatch.StartNew();

            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script VMwareESXVMsDataScript.",
                                  m_taskId);

            try {

                if (resultCode == ResultCodes.RC_SUCCESS) {

                    UserSession userSessionObj = null;

                    if (m_connection.ContainsKey(@"VimServiceObj")) {
                        vim_svc = (VimService)m_connection[@"VimServiceObj"];
                    } else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error, 0, "Task Id {0}: Missing connection parameter VimServiceObj.", m_taskId);
                    }

                    if (m_connection.ContainsKey(@"VimServiceContentObj")) {
                        vim_svc_content = (ServiceContent)m_connection[@"VimServiceContentObj"];
                    } else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error, 0, "Task Id {0}: Missing connection parameter VimServiceContentObj.", m_taskId);
                    }


                    if (m_connection.ContainsKey(@"userSessionObj")) {
                        userSessionObj = (UserSession)m_connection[@"userSessionObj"];
                    } else {
                        resultCode = ResultCodes.RC_SCRIPT_PARAMETER_MISSING;
                        Lib.Logger.TraceEvent(TraceEventType.Error, 0, "Task Id {0}: Missing connection parameter userSessionObj.", m_taskId);
                    }

                    String url = null;

                    if(m_connection.ContainsKey(@"WebServiceUrl")) {
                        url = (String)m_connection[@"WebServiceUrl"];
                    }


                    if (userSessionObj != null) {

                        Lib.Logger.TraceEvent(TraceEventType.Verbose, 0, "Task Id {0}: Get VMs Info. Elapsed time {1} ms.", m_taskId, m_executionTimer.ElapsedMilliseconds);

                        //collect ESX VMs info:
                        StringBuilder ESXVMs_Info = getESXVMsData();

                        Lib.Logger.TraceEvent(TraceEventType.Verbose, 0, "Task Id {0}: VMs Info collected. Elapsed time {1} ms.", m_taskId, m_executionTimer.ElapsedMilliseconds);

                        this.BuildDataRow(@"EsxVMsData", ESXVMs_Info);

                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose, 0, "Task Id {0}: could not connect to esx web services at url {1} in (VMwareESXVMsDataScript).", m_taskId, url);
                        resultCode = ResultCodes.RC_LOGIN_FAILED;  //@TODO: change to some other value
                    }

                    // Disconnect from the service:
                    if (vim_svc != null) {
                        Disconnect();
                    }

                }

            } catch (SoapException se) {

                Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Caught SoapException in VMwareESXVMsDataScript.  Elapsed time {1}.\n <Exception detail:  [se: {2}]\n[Message: {3}]\n[Code: {4}]\n[Detail XML(OuterXml): {5}] Exception detail end>",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          se.ToString(),
                                          se.Message.ToString(),
                                          se.Code.ToString(),                                          
                                          se.Detail.OuterXml.ToString());

                resultCode = ResultCodes.RC_ESX_WEB_SERVICE_QUERY_FAILURE;

                if(se.Message.Contains("Login failed")) {
                    resultCode = ResultCodes.RC_LOGIN_FAILED;
                } else if(se.Message.Contains("Unsupported namespace")) {
                    resultCode = ResultCodes.RC_UNSUPPORTED_MESSAGE_TYPE;
                } else if (se.Code.ToString().Contains("ServerFaultCode")) {

                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: SoapException (ServerFaultCode) in VMwareESXVMsDataScript.  Elapsed time {1}.\n <More detail:  [InnerText: {2}]\n[InnerXml: {3}]  end>",
                                              m_taskId,
                                              m_executionTimer.Elapsed.ToString(),
                                              se.Detail.InnerText,
                                              se.Detail.InnerXml); 

                    if (se.Detail.InnerXml.ToString().Contains("InvalidPropertyFault") || se.Detail.OuterXml.ToString().Contains("InvalidPropertyFault")) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Caught SoapException in VMwareESXVMsDataScript due to an InvalidProperty.", 
                                              m_taskId);
                    }
                }

            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Caught exception in (VMwareESXVMsDataScript).  Elapsed time {1}.\n{2}Result code changed to RC_PROCESSING_EXECEPTION. <EXP => {3} <EXP =>",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          1,
                                          ex.ToString());

                    resultCode = ResultCodes.RC_ESX_WEB_SERVICE_QUERY_FAILURE;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Caught exception in - (VMwareESXVMsDataScript).  Elapsed time {1}.\n{2} <EXP => {3} <EXP =>",
                                          m_taskId,
                        m_executionTimer.Elapsed.ToString(),
                                          1,
                                          ex.ToString());
                    resultCode = ResultCodes.RC_ESX_WEB_SERVICE_QUERY_FAILURE;
                }
            } finally {
                if (vim_svc != null) {
                    vim_svc.Dispose();
                    vim_svc = null;
                    vim_svc_content = null;
                }
            }

            return new CollectionScriptResults
                (resultCode, 0, null, null, null, false, m_dataRow.ToString());

        }

        private void Disconnect() {
            // Disconnect from the service:
            if (vim_svc != null) {
                vim_svc.Logout(vim_svc_content.sessionManager);
                vim_svc.Dispose();
                vim_svc = null;
                vim_svc_content = null;
            }
        }


        private StringBuilder getESXVMsData() {

            // I name my tspecs so that they are self-explanatory.  'dc2vmf' stands for 'Datacenter to vm Folder'
            TraversalSpec dc2vmf = new TraversalSpec();

            dc2vmf.type = "Datacenter";

            dc2vmf.path = "vmFolder";

            dc2vmf.selectSet = new SelectionSpec[] { new SelectionSpec() };
            dc2vmf.selectSet[0].name = "traverseChild";

            TraversalSpec f2c = new TraversalSpec();
            f2c.type = "Folder";

            f2c.name = "traverseChild";

            f2c.path = "childEntity";

            f2c.selectSet = new SelectionSpec[] { new SelectionSpec(),
					                              dc2vmf};

            f2c.selectSet[0].name = f2c.name;

            // This is the Object Specification used in this search.
            ObjectSpec ospec = new ObjectSpec();

            // We're starting this search with the service instance's rootFolder.
            ospec.obj = vim_svc_content.rootFolder;

            // Add the top-level tspec (the Folder-2-childEntity) to the ospec.
            ospec.selectSet = new SelectionSpec[] { f2c };

            // This is the Property Specification use in this search.
            PropertySpec pspec = new PropertySpec();

            ////pspec.type = "HostSystem";
            pspec.type = "VirtualMachine";

            // Do not collect all properties about this object, only few selected properties.
            ///pspec.all = true;

            pspec.pathSet = new string[] { "name",
                                           "summary.config.guestFullName",
                                           "summary.config.guestId",
                                           "summary.config.memorySizeMB",
                                           "summary.config.numCpu",
                                           "summary.config.numVirtualDisks",
                                           "summary.config.uuid",
                                           "summary.config.vmPathName",
                                           "summary.runtime.powerState",  
                
                                           "guest.hostName",                                           
                                           "guest.ipAddress",
                                           "guest.toolsStatus",
                                           "guest.toolsVersion",
                                           "guest.net",
                                           "config.hardware.device"                           
                                         };


            // Build the PropertyFilterSpec and set its PropertySpecficiation (propSet) 
            // and ObjectSpecification (objectset) attributes to pspec and ospec respectively.
            PropertyFilterSpec pfspec = new PropertyFilterSpec();
            pfspec.propSet = new PropertySpec[] { pspec };
            pfspec.objectSet = new ObjectSpec[] { ospec };

            // Retrieve the property values from the VI3 SDk web service.
            ObjectContent[] occoll = vim_svc.RetrieveProperties(
                vim_svc_content.propertyCollector, new PropertyFilterSpec[] { pfspec });

            // go through results of the property retrieval if there were any.

            StringBuilder sb = new StringBuilder();

            if (occoll != null) {
                DynamicProperty pc = null;
                foreach (ObjectContent oc in occoll) {
                    DynamicProperty[] pcary = null;
                    pcary = oc.propSet;
                    sb.Append("<BDNA_ESX_VM>");
                    for (int i = 0; i < pcary.Length; i++) {
                        pc = pcary[i];

                        if (pc != null && pc.val.GetType().IsArray) {
                            if (pc.val.GetType() == typeof(VimApi.GuestNicInfo[])) {
                                /* commented macaddress from guest.net (available only if VMware Tools running on VM), instead use macaddress from config.hardware.device

                                GuestNicInfo[] guestNics = (GuestNicInfo[])pc.val;
                                foreach (GuestNicInfo nics in guestNics) {
                                    //sb.Append(oc.propSet[i].name).Append(".macAddress")  // OR
                                    sb.Append(pc.name).Append(".macAddress")
                                      .Append("<BDNA,1>")
                                      .Append(nics.macAddress)

                                      .Append("<BDNA,2>")

                                      .Append(pc.name).Append(".ipAddress")
                                      .Append("<BDNA,1>");
                                    //.Append(nics.ipAddress) // this gives System.String[]
                                    int c = 0;
                                    foreach (String ip in nics.ipAddress) {
                                        if (c > 0) {
                                            sb.Append(",").Append(ip);
                                        } else {
                                            sb.Append(ip);
                                            c++;
                                        }
                                    }
                                    sb.Append("<BDNA,>");
                                }
                                */
                            } else if (pc.val.GetType() == typeof(VimApi.VirtualDevice[])) {
                                VirtualDevice[] vd = (VirtualDevice[])pc.val;
                                foreach (VirtualDevice dev in vd) {
                                    if (dev.GetType().BaseType == typeof(VimApi.VirtualEthernetCard)) {
                                        sb.Append(pc.name).Append(".macAddress")
                                          .Append("<BDNA,1>")                                        
                                          .Append(((VirtualEthernetCard)dev).macAddress)

                                          .Append("<BDNA,2>")

                                          .Append(pc.name).Append(".addressType")
                                          .Append("<BDNA,1>")
                                          .Append(((VirtualEthernetCard)dev).addressType);
                                        sb.Append("<BDNA,>");                        
                                    }
                                }
                            }
                        } else {
                            sb.Append(oc.propSet[i].name)
                              .Append("<BDNA,1>")
                              .Append(oc.propSet[i].val)
                              .Append("<BDNA,>");
                        }

                    }
                }
            }

            return sb;
        }

        /// <summary>
        /// Generate a new data row for a collected attribute and add
        /// it to the aggregate data row buffer.
        /// </summary>
        /// 
        /// <param name="attributeName">Name of attribute collected.</param>
        /// <param name="collectedData">Collected data value.  Null is allowed.</param>
        private void BuildDataRow(string attributeName, StringBuilder collectedData) {
            if (!m_attributes.ContainsKey(attributeName)) {
                Lib.Logger.TraceEvent(TraceEventType.Error,
                                      0,
                                      "Task Id {0}: Attribute \"{1}\" missing from attributeSet.",
                                      m_taskId,
                                      attributeName);
            } else if (collectedData == null || collectedData.Length <= 0) {
                Lib.Logger.TraceEvent(TraceEventType.Verbose,
                                      0,
                                      "Task Id {0}: Script completed sucessfully with no data to return.",
                                      m_taskId);
            } else {

                m_dataRow.Append(m_elementId).Append(',')
                         .Append(m_attributes[attributeName]).Append(',')
                         .Append(m_scriptParameters[@"CollectorId"]).Append(',')
                         .Append(m_taskId).Append(',')
                         .Append(m_databaseTimestamp + m_executionTimer.ElapsedMilliseconds).Append(',')
                         .Append(attributeName).Append(',')
                         .Append(BdnaDelimiters.BEGIN_TAG).Append(collectedData).Append(BdnaDelimiters.END_TAG);
            }
        }

    }
}
