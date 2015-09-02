#region Copyright
/******************************************************************
*
* Module: VMwareESXHostDataScript
* Original Author: Rekha Rani
*   Creation Date: 2008/07/10
*
* Current Status
*       $Revision: 1.10 $
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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics; // for TraceEvent call


using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using System.Web.Services.Protocols;

//VimApi for the VirtualInfrastructureManagement Service Reference
using VimApi;

using bdna.ScriptLib;
using bdna.Shared;


namespace bdna.Scripts {
    public class ESXHostInfo : ICollectionScriptRuntime {

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
        static VimService vim_svc;
        static ServiceContent vim_svc_content;
        protected ManagedObjectReference vim_svc_ref = null;

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
                                  "Task Id {0}: Collection script ESXHostInfo.",
                                  m_taskId);

            StringBuilder ESX_Info = new StringBuilder();
            Boolean success = false;

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

                    if (m_connection.ContainsKey(@"WebServiceUrl")) {
                        url = (String)m_connection[@"WebServiceUrl"];
                    }

                    if (userSessionObj != null) {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose, 0, "Task Id {0}: Going to get ESX Info. Elapsed time {1} ms.", m_taskId, m_executionTimer.ElapsedMilliseconds);

                        //collect ESX Host's basic info:
                        //StringBuilder HostBasicInfo = getEsxHostBasicInfo();
                        //ESX_Info.Append(HostBasicInfo);

                        ESX_Info.Append(getEsxHostBasicInfo());
                                                
                        //collect ESX host info:
                        StringBuilder ESX_MoreInfo = getEsxHostInfo();
                        ESX_Info.Append(ESX_MoreInfo);

                        //collect ESX license information:
                        StringBuilder ESX_License = getEsxLicenseInfo();
                        ESX_Info.Append(ESX_License);

                        //collect ESX datastore information:
                        StringBuilder ESX_Datastore = getEsxDatastoreInfo();
                        ESX_Info.Append(ESX_Datastore);

                        Lib.Logger.TraceEvent(TraceEventType.Verbose, 0, "Task Id {0}: Collected ESX Info. Elapsed time {1} ms.", m_taskId, m_executionTimer.ElapsedMilliseconds);

                        success = true;

                        this.BuildDataRow(@"EsxHostData", ESX_Info);


                    } else {
                        Lib.Logger.TraceEvent(TraceEventType.Verbose, 0, "Task Id {0}: could not connect to esx web services at url {1}.", m_taskId, url);
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
                                          "Task Id {0}: Caught SoapException in VMwareESXHostDataScript.  Elapsed time {1}.\n <Exception detail:  [se: {2}]\n[Message: {3}]\n[Code: {4}]\n[Detail XML(OuterXml): {5}] Exception detail end>",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          se.ToString(),
                                          se.Message.ToString(),
                                          se.Code.ToString(),                                          
                                          se.Detail.OuterXml.ToString());

                resultCode = ResultCodes.RC_ESX_WEB_SERVICE_QUERY_FAILURE;

                if (se.Message.Contains("Login failed")) {
                    resultCode = ResultCodes.RC_LOGIN_FAILED;
                } else if (se.Message.Contains("Unsupported namespace")) {
                    resultCode = ResultCodes.RC_UNSUPPORTED_MESSAGE_TYPE;
                } else if (se.Code.ToString().Contains("ServerFaultCode")) {

                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: SoapException (ServerFaultCode) in VMwareESXHostDataScript.  Elapsed time {1}.\n <More detail:  [InnerText: {2}]\n[InnerXml: {3}]  end>",
                                              m_taskId,
                                              m_executionTimer.Elapsed.ToString(),
                                              se.Detail.InnerText,
                                              se.Detail.InnerXml);

                    if (se.Detail.InnerXml.ToString().Contains("InvalidPropertyFault") || se.Detail.OuterXml.ToString().Contains("InvalidPropertyFault")) {
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Caught SoapException in VMwareESXHostDataScript due to an InvalidProperty.", 
                                              m_taskId);
                    }
                }

            } catch (Exception ex) {
                if (ResultCodes.RC_SUCCESS == resultCode) {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Caught exception in VMwareESXHostDataScript.  Elapsed time {1}.\n{2}Result code changed to RC_PROCESSING_EXECEPTION. <EXP => {3} <EXP =>",
                                          m_taskId,
                                          m_executionTimer.Elapsed.ToString(),
                                          1,
                                          ex.ToString());

                    resultCode = ResultCodes.RC_ESX_WEB_SERVICE_QUERY_FAILURE;
                } else {
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Caught exception in - VMwareESXHostDataScript.  Elapsed time {1}.\n{2} <EXP => {3} <EXP =>",
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
            
            //if all data not collected then send back whatever has been collected.
            if(!success) {
                this.BuildDataRow(@"EsxHostData", ESX_Info);
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


        private StringBuilder getEsxHostBasicInfo() {
            String fullName = vim_svc_content.about.fullName;
            String version = vim_svc_content.about.version;

            ///Property = oc.propSet[i].name and PropertyValue = oc.propSet[i].val
            /// [Note: Property and their value are seperated by <BDNA,1> delimiter ]

            //String ret = "config.product.fullName" + "<BDNA,1>" + fullName + "<BDNA,>" + "config.product.version" + "<BDNA,1>" + version + "<BDNA,>";

            StringBuilder ret = new StringBuilder();

            ret.Append("config.product.fullName")
               .Append("<BDNA,1>")
               .Append(fullName)
               .Append("<BDNA,>")
               .Append("config.product.version")
               .Append("<BDNA,1>")
               .Append(version)
               .Append("<BDNA,>");

            Lib.Logger.TraceEvent(TraceEventType.Verbose, 0,
                                  "Task Id {0}: ESX fullName={2}, version={3}. Elapsed time {1} ms.", 
                                  m_taskId, 
                                  m_executionTimer.ElapsedMilliseconds,
                                  fullName,
                                  version);          

            return ret;                        
        }

        private StringBuilder getEsxHostInfo() {

            // I name my tspecs so that they are self-explanatory.  'dc2hf' stands for 'Datacenter to Host Folder'
            TraversalSpec dc2hf = new TraversalSpec();

            dc2hf.type = "Datacenter";

            dc2hf.path = "hostFolder";

            dc2hf.selectSet = new SelectionSpec[] { new SelectionSpec() };
            dc2hf.selectSet[0].name = "traverseChild";

            TraversalSpec cr2host = new TraversalSpec();
            cr2host.type = "ComputeResource";
            cr2host.path = "host";

            TraversalSpec f2c = new TraversalSpec();
            f2c.type = "Folder";

            f2c.name = "traverseChild";

            f2c.path = "childEntity";

            f2c.selectSet = new SelectionSpec[] { new SelectionSpec(), dc2hf, cr2host };

            f2c.selectSet[0].name = f2c.name;

            // This is the Object Specification used in this search.
            ObjectSpec ospec = new ObjectSpec();

            // We're starting this search with the service instance's rootFolder.
            ospec.obj = vim_svc_content.rootFolder;

            // Add the top-level tspec (the Folder-2-childEntity) to the ospec.
            ospec.selectSet = new SelectionSpec[] { f2c };

            // This is the Property Specification use in this search.
            PropertySpec pspec = new PropertySpec();

            pspec.type = "HostSystem";

            // Do not collect all properties about this object.
            ///pspec.all = true;

            // Collect only the name property from this object.
            ///pspec.pathSet = new string[] { "name", "hardware.memorySize" };
            // above only gives memorysize
            // pspec.pathSet = new string[] { "name"};

            pspec.pathSet = new string[] { "name", 
                                           "hardware.memorySize", 
                                           "hardware.systemInfo.model", 
                                           "hardware.systemInfo.uuid",
                                           "hardware.systemInfo.vendor",
                                           //"hardware.biosInfo.biosVersion",  //Since VI API 2.5
                                           //"hardware.biosInfo.releaseDate",  //Since VI API 2.5
                                           "hardware.cpuPkg",

                                           "summary.hardware.memorySize",
                                           "summary.hardware.model",                                           
                                           "summary.hardware.uuid",
                                           "summary.hardware.vendor",
                                           "summary.hardware.cpuMhz",
                                           "summary.hardware.cpuModel",
                                           "summary.hardware.numCpuCores",
                                           "summary.hardware.numCpuPkgs",
                                           "summary.hardware.numCpuThreads",

                                           "config.storageDevice.scsiLun",
                                           "config.network.pnic",                
                                           
                                           //"config.product.fullName",
                                           //"config.product.version",
                                           "config.service.service[\"vmware-vpxa\"].running",
                                           "runtime.bootTime"                                         

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
                    for (int i = 0; i < pcary.Length; i++) {
                        pc = pcary[i];

                        if (pc != null && pc.val.GetType().IsArray) {

                            if (pc.val.GetType() == typeof(VimApi.PhysicalNic[])) {
                                // PhysicalNic:
                                //PhysicalNic[] host_phyNic_info = (PhysicalNic[])oc.propSet[0].val;
                                PhysicalNic[] host_phyNic_info = (PhysicalNic[])pc.val;
                                foreach (PhysicalNic host_phyNic in host_phyNic_info) {

                                    /* VI API 2.0 */

                                    sb.Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".device")
                                      .Append("<BDNA,1>")
                                      .Append(host_phyNic.device)

                                      .Append("<BDNA,>");

                                    /* VI API 2.5  */
                                    /*
                                    sb.Append(oc.propSet[i].name).Append(".mac")
                                      .Append("<BDNA,1>")
                                      .Append(host_phyNic.mac)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".device")
                                      .Append("<BDNA,1>")
                                      .Append(host_phyNic.device)

                                      .Append("<BDNA,>");
                                    */
                                }
                            }

                            if (pc.val.GetType() == typeof(VimApi.ScsiLun[])) {
                                //ScsiLun:
                                //ScsiLun[] host_storage_info = (ScsiLun[])oc.propSet[1].val;
                                ScsiLun[] host_storage_info = (ScsiLun[])pc.val;
                                foreach (ScsiLun host_stroage in host_storage_info) {

                                    if (host_stroage.deviceType.Equals("disk")) {
                                        sb.Append(oc.propSet[i].name).Append(".HostScsiDisk.capacity.block")
                                          .Append("<BDNA,1>")
                                          .Append(((HostScsiDisk)host_stroage).capacity.block)

                                          .Append("<BDNA,2>")
                                          .Append(oc.propSet[i].name).Append(".HostScsiDisk.capacity.blockSize")
                                          .Append("<BDNA,1>")
                                          .Append(((HostScsiDisk)host_stroage).capacity.blockSize)

                                          .Append("<BDNA,2>");
                                    }

                                    sb.Append(oc.propSet[i].name).Append(".canonicalName")
                                      .Append("<BDNA,1>")
                                      .Append(host_stroage.canonicalName)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".deviceName")
                                      .Append("<BDNA,1>")
                                      .Append(host_stroage.deviceName)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".deviceType")
                                      .Append("<BDNA,1>")
                                      .Append(host_stroage.deviceType)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".model")
                                      .Append("<BDNA,1>")
                                      .Append(host_stroage.model)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".serialNumber")
                                      .Append("<BDNA,1>")
                                      .Append(host_stroage.serialNumber)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".vendor")
                                      .Append("<BDNA,1>")
                                      .Append(host_stroage.vendor)

                                      .Append("<BDNA,>");

                                }
                            }

                            if (pc.val.GetType() == typeof(VimApi.HostCpuPackage[])) {
                                HostCpuPackage[] host_cpuPkg_info = (HostCpuPackage[])pc.val;
                                foreach (HostCpuPackage host_cpuPkg in host_cpuPkg_info) {
                                    sb.Append(oc.propSet[i].name).Append(".description")
                                      .Append("<BDNA,1>")
                                      .Append(host_cpuPkg.description)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".hz")
                                      .Append("<BDNA,1>")
                                      .Append(host_cpuPkg.hz)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".index")
                                      .Append("<BDNA,1>")
                                      .Append(host_cpuPkg.index)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".threadId_NumberOfThread")
                                      .Append("<BDNA,1>")
                                      .Append(host_cpuPkg.threadId.Length)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".vendor")
                                      .Append("<BDNA,1>")
                                      .Append(host_cpuPkg.vendor)

                                      .Append("<BDNA,>");
                                }
                            }
                        } else {

                            ///Property = oc.propSet[i].name and PropertyValue = oc.propSet[i].val
                            /// [Note: Property and their value are seperated by <BDNA,1> delimiter ]
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


        private StringBuilder getEsxLicenseInfo() {
            // No need for a traversal, since in this case starting object = target object                      

            // This is the Object Specification used in this search.
            ObjectSpec ospec = new ObjectSpec();

            // We're starting this search with the service instance's licenseManager.
            ospec.obj = vim_svc_content.licenseManager;
            ospec.skip = false; // checks that starting object matches the type specified in the PropertySpec

            /* this is working just for edition:
            // This is the Property Specification use in this search.
            PropertySpec pspec = new PropertySpec();

            pspec.type = "LicenseManager";

            pspec.pathSet = new string[] { "licensedEdition" };
            */

            // this is new for obj ref also: ************* START
            PropertySpec[] pspec = new PropertySpec[] { new PropertySpec(), new PropertySpec() };

            pspec[0].type = "LicenseManager";

            /* For VI API 2.0 */
            pspec[0].pathSet = new string[] { "featureInfo" };

            /* for VI API 2.5 */
            /*
            pspec[0].pathSet = new string[] { "licensedEdition", "featureInfo" };
            */

            pspec[1].type = "LicenseManager";
            pspec[1].all = false;

            // this is new for obj ref also: ************* END

            // Build the PropertyFilterSpec and set its PropertySpecficiation (propSet) 
            // and ObjectSpecification (objectset) attributes to pspec and ospec respectively.
            PropertyFilterSpec pfspec = new PropertyFilterSpec();
            pfspec.propSet = pspec; //new PropertySpec[] { pspec };
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
                    for (int i = 0; i < pcary.Length; i++) {
                        pc = pcary[i];

                        if (pc != null && pc.val.GetType().IsArray) {

                            if (pc.val.GetType() == typeof(VimApi.LicenseFeatureInfo[])) {
                                LicenseFeatureInfo[] host_license_info = (LicenseFeatureInfo[])pc.val;
                                foreach (LicenseFeatureInfo host_license in host_license_info) {
                                    sb.Append(oc.propSet[i].name).Append(".costUnit")
                                      .Append("<BDNA,1>")
                                      .Append(host_license.costUnit)

                                    /* commented for VI API 2.5 to VI API 2.0 */
                                        /*
                                          .Append("<BDNA,2>")
                                          .Append(oc.propSet[i].name).Append(".edition")
                                          .Append("<BDNA,1>")
                                          .Append(host_license.edition)

                                          .Append("<BDNA,2>")
                                          .Append(oc.propSet[i].name).Append(".expiresOn")
                                          .Append("<BDNA,1>")
                                          .Append(host_license.expiresOn)

                                          .Append("<BDNA,2>")
                                          .Append(oc.propSet[i].name).Append(".featureDescription")
                                          .Append("<BDNA,1>")
                                          .Append(host_license.featureDescription)
                                        */

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".featureName")
                                      .Append("<BDNA,1>")
                                      .Append(host_license.featureName)

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".key")
                                      .Append("<BDNA,1>")
                                      .Append(host_license.key)

                                    /* commented for VI API 2.5 to VI API 2.0 */
                                        /*   
                                          .Append("<BDNA,2>")
                                          .Append(oc.propSet[i].name).Append(".sourceRestriction")
                                          .Append("<BDNA,1>")
                                          .Append(host_license.sourceRestriction)
                                        */

                                      .Append("<BDNA,2>")
                                      .Append(oc.propSet[i].name).Append(".state")
                                      .Append("<BDNA,1>")
                                      .Append(host_license.state)

                                      .Append("<BDNA,>");
                                }

                            }

                        } else {

                            ///Property = oc.propSet[i].name and PropertyValue = oc.propSet[i].val
                            /// [Note: Property and their value are seperated by <BDNA,1> delimiter ]
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


        private StringBuilder getEsxDatastoreInfo() {
            // I name my tspecs so that they are self-explanatory.  'dc2hf' stands for 'Datacenter to Host Folder'
            TraversalSpec dc2hf = new TraversalSpec();

            dc2hf.type = "Datacenter";

            dc2hf.path = "hostFolder";

            dc2hf.selectSet = new SelectionSpec[] { new SelectionSpec() };
            dc2hf.selectSet[0].name = "traverseChild";

            TraversalSpec cr2host = new TraversalSpec();
            cr2host.type = "ComputeResource";
            cr2host.path = "host";

            TraversalSpec f2c = new TraversalSpec();
            f2c.type = "Folder";

            f2c.name = "traverseChild";

            f2c.path = "childEntity";

            f2c.selectSet = new SelectionSpec[] { new SelectionSpec(), dc2hf, cr2host };

            f2c.selectSet[0].name = f2c.name;

            // This is the Object Specification used in this search.
            ObjectSpec ospec = new ObjectSpec();

            // We're starting this search with the service instance's rootFolder.
            ospec.obj = vim_svc_content.rootFolder;

            // Add the top-level tspec (the Folder-2-childEntity) to the ospec.
            ospec.selectSet = new SelectionSpec[] { f2c };

            // This is the Property Specification use in this search.
            PropertySpec pspec = new PropertySpec();

            pspec.type = "HostSystem";

            // Do not collect all properties about this object.
            //pspec.all = true;

            pspec.pathSet = new string[] { "datastore"                                       
                                         };

            // Build the PropertyFilterSpec and set its PropertySpecficiation (propSet) 
            // and ObjectSpecification (objectset) attributes to pspec and ospec respectively.
            PropertyFilterSpec pfspec = new PropertyFilterSpec();
            pfspec.propSet = new PropertySpec[] { pspec };
            pfspec.objectSet = new ObjectSpec[] { ospec };

            // Retrieve the property values from the VI3 SDk web service.
            ObjectContent[] occoll = vim_svc.RetrieveProperties(
                vim_svc_content.propertyCollector, new PropertyFilterSpec[] { pfspec });

            // Print out the results of the property retrieval if there were any.

            StringBuilder sb = new StringBuilder();

            if (occoll != null) {
                DynamicProperty pc = null;
                foreach (ObjectContent oc in occoll) {
                    DynamicProperty[] pcary = null;
                    pcary = oc.propSet;
                    for (int i = 0; i < pcary.Length; i++) {
                        pc = pcary[i];

                        if (pc.val.GetType() == typeof(VimApi.ManagedObjectReference[])) {
                            ManagedObjectReference[] dsList = (ManagedObjectReference[])pc.val;
                            ManagedObjectReference dsRef = null;
                            for (int j = 0; j < dsList.Length; j++) {
                                dsRef = dsList[j];
                                Object[] dsProps = getProperties(dsRef, new String[] { "summary.capacity", 
                                                                                           "summary.freeSpace",
                                                                                           "summary.name", 
                                                                                           "summary.type",
                                                                                           "summary.url"
                                                                                         });

                                /* Put sb append under if logic so that it would not give any error if due to any reason all mentioned properties not get collected */
                                for (int c = 0; c < dsProps.Length; c++) {
                                    if (c == 0) {
                                        sb.Append("Datastore.capacity")
                                          .Append("<BDNA,1>")
                                          .Append(Convert.ToString(dsProps[0]))
                                          .Append("<BDNA,2>");
                                    } else if (c == 1) {
                                        sb.Append("Datastore.freeSpace")
                                          .Append("<BDNA,1>")
                                          .Append(Convert.ToString(dsProps[1]))
                                          .Append("<BDNA,2>");
                                    } else if (c == 2) {
                                        sb.Append("Datastore.name")
                                          .Append("<BDNA,1>")
                                          .Append(((String)dsProps[2]))
                                          .Append("<BDNA,2>");
                                    } else if (c == 3) {
                                        sb.Append("Datastore.type")
                                          .Append("<BDNA,1>")
                                          .Append(((String)dsProps[3]))
                                          .Append("<BDNA,2>");
                                    } else if (c == 4) {
                                        sb.Append("Datastore.url")
                                        .Append("<BDNA,1>")
                                        .Append(((String)dsProps[4]));
                                    }

                                }
                                sb.Append("<BDNA,>");
                            }
                        }
                    }
                }
            }

            return sb;
        }

        /*
         * getProperties --
         * 
         * Retrieves the specified set of properties for the given managed object
         * reference into an array of result objects (returned in the same oder
         * as the property list).
       */
        public static Object[] getProperties(ManagedObjectReference moRef, String[] properties) {
            // PropertySpec specifies what properties to
            // retrieve and from type of Managed Object
            PropertySpec pSpec = new PropertySpec();
            pSpec.type = moRef.type;
            pSpec.pathSet = properties;

            // ObjectSpec specifies the starting object and
            // any TraversalSpecs used to specify other objects 
            // for consideration
            ObjectSpec oSpec = new ObjectSpec();
            oSpec.obj = moRef;

            // PropertyFilterSpec is used to hold the ObjectSpec and 
            // PropertySpec for the call
            PropertyFilterSpec pfSpec = new PropertyFilterSpec();
            pfSpec.propSet = new PropertySpec[] { pSpec };
            pfSpec.objectSet = new ObjectSpec[] { oSpec };

            // retrieveProperties() returns the properties
            // selected from the PropertyFilterSpec

            //VimService service1 = new VimService();
            ManagedObjectReference _svcRef1 = new ManagedObjectReference();
            _svcRef1.type = "ServiceInstance";
            _svcRef1.Value = "ServiceInstance";

            ServiceContent sic1 = vim_svc.RetrieveServiceContent(_svcRef1);
            ObjectContent[] ocs = new ObjectContent[20];
            ocs = vim_svc.RetrieveProperties(sic1.propertyCollector, new PropertyFilterSpec[] { pfSpec });

            // Return value, one object for each property specified
            Object[] ret = new Object[properties.Length];

            if (ocs != null) {
                for (int i = 0; i < ocs.Length; ++i) {
                    ObjectContent oc = ocs[i];
                    DynamicProperty[] dps = oc.propSet;
                    if (dps != null) {
                        for (int j = 0; j < dps.Length; ++j) {
                            DynamicProperty dp = dps[j];
                            // find property path index
                            for (int p = 0; p < ret.Length; ++p) {
                                if (properties[p].Equals(dp.name)) {
                                    ret[p] = dp.val;
                                }
                            }
                        }
                    }
                }
            }
            return ret;
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
