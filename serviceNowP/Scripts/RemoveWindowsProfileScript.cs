#region Copyright
/******************************************************************
*
*          Module: Windows Collection Scripts
* Original Author: Parin Kenia
*   Creation Date: 2007/08/27
*
* Current Status
*       $Revision: 1.4 $
*           $Date: 2014/07/16 23:02:42 $
*         $Author: ameau $
*
*******************************************************************
*
* Copyright (c) 2001-2008 BDNA Corporation.
* All Rights Reserved
*
* ******bDNA CONFIDENTIAL******
*
* The following code was developed and is owned by bDNA Corporation
* This code is confidential and may contain
* trade secrets. The code must not be distributed to any party
* outside of bDNA Corporation Inc. without written
* permission from bDNA.  The code may be covered by patents,
* patents pending, or patents applied for in the US or elsewhere.
*
******************************************************************/
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Management;
using System.Text;
using System.Threading;

using bdna.ScriptLib;
using bdna.Shared;

namespace bdna.Scripts
{

    /// <summary>
    /// Collection task for Windows remote profile cleanup.  Does a blind
    /// delete of the remote directory corresponding to the Windows profile
    /// for the user specified in the connection object.
    /// </summary>
    public class RemoveWindowsProfileScript : ICollectionScriptRuntime
    {

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
                ITftpDispatcher tftpDispatcher)
        {
            Stopwatch executionTimer = Stopwatch.StartNew();
            string taskIdString = taskId.ToString();
            StringBuilder dataRow = new StringBuilder();
            ResultCodes resultCode = ResultCodes.RC_SUCCESS;

            Lib.Logger.TraceEvent(TraceEventType.Start,
                                  0,
                                  "Task Id {0}: Collection script RemoveWindowsProfileScript.",
                                  taskIdString);

            try
            {

                if (null == connection)
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Connection object passed to RemoveWindowsProfileScript is null.",
                                          taskIdString);
                }
                else if (!connection.ContainsKey(@"cimv2"))
                {
                    resultCode = ResultCodes.RC_NULL_CONNECTION_OBJECT;
                    Lib.Logger.TraceEvent(TraceEventType.Error,
                                          0,
                                          "Task Id {0}: Management scope for CIMV namespace is not present in connection object.",
                                          taskIdString);
                }
                else
                {
                    ManagementScope cimvScope = connection[@"cimv2"] as ManagementScope;

                    if (!cimvScope.IsConnected)
                    {
                        resultCode = ResultCodes.RC_WMI_CONNECTION_FAILED;
                        Lib.Logger.TraceEvent(TraceEventType.Error,
                                              0,
                                              "Task Id {0}: Connection to CIMV namespace failed",
                                              taskIdString);
                    }
                    else
                    {
                        string userName = connection[@"userName"] as string;
                        Debug.Assert(null != userName);

                        int domainSeparatorPosition = userName.IndexOf('\\');

                        if (-1 != domainSeparatorPosition)
                        {
                            userName = userName.Substring(domainSeparatorPosition + 1);
                        }

                        string profileDirectory = @"c:\Documents and Settings\" + userName;
                        uint wmiMethodResultCode = 0;
                        resultCode = Lib.DeleteDirectory(taskIdString, profileDirectory, cimvScope, out wmiMethodResultCode);
                    }
                }
            }
            catch (Exception ex)
            {
                Lib.LogException(taskIdString,
                                 executionTimer,
                                 "Unhandled exception in RemoveWindowsProfileScript",
                                 ex);
                resultCode = ResultCodes.RC_PROCESSING_EXCEPTION;
            }

            Lib.Logger.TraceEvent(TraceEventType.Stop,
                                  0,
                                  "Task Id {0}: Collection script RemoveWindowsProfileScript.  Elapsed time {1}.  Result code {2}.",
                                  taskIdString,
                                  executionTimer.Elapsed.ToString(),
                                  resultCode.ToString());
            return new CollectionScriptResults(resultCode, 0, null, null, null, false, dataRow.ToString());
        }

        #endregion ICollectionScriptRuntime

    }

}
