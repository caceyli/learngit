package UNIXOracle;
import java.awt.List;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.regex.*;
import java.util.*;
public class UNIXOracleInstallationDynamicScript {
	public static void main(String[] args) {
		String host = "", resultString = "", home= "", cmd = "",cmd0 = "";
	     

		//# get the telnet connection (set by the connection script)
		//$host = $BDNA_Connection_Info{"HostObject"};
		//$home = $BDNA_Params{"root.types.resource.software.installation.OracleInstallation.UNIXOracleInstallation:OracleHome"};

		// #
		//# Validate instances by trying a connection with a dummy user
		//#

         ArrayList<String> validated_sids = new ArrayList<String>();
         ArrayList<String> invalidated_sids = new ArrayList<String>();
		             

		             
         String validateFlag = "0";
		             
         Pattern lineRegex = Pattern.compile("ORA-(01017|04031|28000):");

         //if ($BDNA_Params{"root.types.footprint.OracleFootprint.UNIXOracleFootprint:runningInstances"} ne '<BDNA,>') {
		     // String[] sids = split(/$BDNA_Separator/, $BDNA_Params{"root.types.footprint.OracleFootprint.UNIXOracleFootprint:runningInstances"});        
                String[] sids = {"ora11g","oradv"};
                for (int i=0;i<sids.length;i++) {
		                     validateFlag = "0";
		                     String sid = sids[i];
		                     sid = sid.replaceAll("\\s+", "");
		                    // &echo("Considering Oracle instance $sid");
		                     System.out.println("Considering Oracle instance " + sid);
		                     cmd0 = "PATH="+ home + "/bin:/sbin:/bin:/usr/sbin:/usr/bin:/usr/local/sbin:/usr/local/bin" + "\n" + "export ORAENV_ASK" + "\n" + "export ORACLE_SID" + "\n" + "ORAENV_ASK=NO" + "\n" +  "ORACLE_SID=" + sid + "\n" + ". " + home + "/bin/oraenv";
	 
		                    // &echo("Trying to validate $sid for $home using oraenv.");
		                     cmd = cmd0 + "\n" + home + "bin/sqlplus BDNA_VALIDATE/BDNA_WRONG_PASSWORD < /dev/null";

		                     //String[] output = &shellcmd($host, $cmd, "validate_instance");
		                     String[] output = {"validate_instance","ORA-01017: invalid username/password;"};

		                     for(int j = 0;j<output.length;j++) {
		                        // # Example messages:
		                         //# ORA-01017: invalid username/password; logon denied
		                         //# ORA-04031: unable to allocate 16108 bytes of shared memory...
		                         //# ORA-28000: the account is locked....
		                    	 
		                    	 Matcher lineMat = lineRegex.matcher(output[j]);
		                         if (lineMat.find())
		                         {
		                             //&echo("Validated Oracle instance $sid for $home using oraenv.");
		                             //push(@validated_sids, $sid);
		                        	 validated_sids.add(sid);
		                        	 validateFlag = "1";
		                        	 System.out.println("sid=" + sid + ",flag=" + validateFlag);
		                         } 
		                     }
		                   //#for BUG27992---set TWO_TASK
		                     if (validateFlag.equals("0")) {
		                       //  String cmdSet = join("\n",
		                            //     "PATH=$home/bin:/sbin:/bin:/usr/sbin:/usr/bin:/usr/local/sbin:/usr/local/bin",
		                             //    "export ORAENV_ASK",
		                             //   "export ORACLE_SID",
		                               //  "export TWO_TASK",
		                               //  "ORAENV_ASK=NO",
		                              //   "ORACLE_SID=$sid",
		                               //  "TWO_TASK=$sid",
		                               //  ". $home/bin/oraenv");
		                    	 String cmdSet = "PATH=" + home + "/bin:/sbin:/bin:/usr/sbin:/usr/bin:/usr/local/sbin:/usr/local/bin" + "\n" + "export ORAENV_ASK" + "\n" + "export ORACLE_SID" + "\n" + "export TWO_TASK" + "\n" + "ORAENV_ASK=NO" + "\n" + "ORACLE_SID=$sid" + "\n" + "TWO_TASK=$sid" + "\n" + ". " + home + "/bin/oraenv";
		                         //&echo("After Set TWO_TASK,Trying to validate $sid for $home using oraenv.");
		                    	 
		                    	 String cmdExecute = cmdSet + "\n" + home + "/bin/sqlplus BDNA_VALIDATE/BDNA_WRONG_PASSWORD < /dev/null";
		                         //$cmdExecute = join("\n",
		                           //          $cmdSet,
		                             //        "'$home/bin/sqlplus' BDNA_VALIDATE/BDNA_WRONG_PASSWORD < /dev/null");

		                         //String[] outputExecute = &shellcmd($host, $cmdExecute, "validate_instance");
		                         String[] outputExecute = {"validate_instance","ORA-04031: invalid username/password;"};
	                                 for (int j =0; j < outputExecute.length; j++) {
	                            	 
	                            	 Matcher lineMat = lineRegex.matcher(outputExecute[j]);
	                            	 
	                            	 // # Example messages:
		                             //# ORA-01017: invalid username/password; logon denied
		                             //# ORA-04031: unable to allocate 16108 bytes of shared memory...
		                             //# ORA-28000: the account is locked....
	                            	 if (lineMat.find())
		                             {
		                                 //&echo("After Set TWO_TASK,Validated Oracle instance $sid for $home using oraenv.");
	                            		 validated_sids.add(sid);
		                                 //push(@validated_sids, $sid);
		                                 validateFlag = "1";
		                                 System.out.println("sidexe=" + sid + ",flag=" + validateFlag);
		                             }
	                            	 
	                            
	                             }
		                         
		                     }
		                     if (validateFlag.equals("0")) {
		                         cmd = cmd0 + "\n" + home + "/bin/sqlplus BDNA_VALIDATE/BDNA_WRONG_PASSWORD\\@localhost/" + sid + "< /dev/null";
		                         //&echo("Trying to validate $sid for $home using oraenv and localhost...");                  
		                         //$cmd = join("\n",
		                           //      $cmd0,
		                             //    "'$home/bin/sqlplus' BDNA_VALIDATE/BDNA_WRONG_PASSWORD\@localhost/$sid < /dev/null");

		                         //String[] output0 = &shellcmd($host, $cmd, "validate_instance");
		                         String[] output0 = {"validate_instance","ORA-04031: invalid username/password;"};
	                                 for (int j =0; j < output0.length; j++) {
	                            	// # Example messages:
		                             //# ORA-01017: invalid username/password; logon denied
		                             //# ORA-04031: unable to allocate 16108 bytes of shared memory...
		                             //# ORA-28000: the account is locked....
	                            	 Matcher lineMat = lineRegex.matcher(output0[j]);
		                             if (lineMat.find())
		                             {
		                                 //&echo("Validated Oracle instance $sid for $home using oraenv. and localhost...");
		                                 //push(@validated_sids, $sid);
		                            	 validated_sids.add(sid);
		                                 validateFlag = "1";
		                                 System.out.println("sid0=" + sid + ",flag=" + validateFlag);
		                             }
	                             }

		                         if (validateFlag.equals("0")) {
		                             //push(@invalidated_sids, $sid);
		                        	 invalidated_sids.add(sid);
		                        	 System.out.println("iv_sid=" + sid + ",flag=" + validateFlag);
		                        	 
		                         }
		                     }
		                }

		                 //# Now try validating the remaining Oracle instance using earlier approach
		                 
		                 
		                 for (int i=0;i<invalidated_sids.size();i++) {
		                     validateFlag = "0";
		                     String sid = invalidated_sids.get(i);
		                     sid = sid.replaceAll("\\s+","");
		                     
		                     //&echo("Considering Oracle instance $sid");
		                     cmd0 = "export ORACLE_HOME" + "\n" + "export ORACLE_SID" + "\n" + "export LD_LIBRARY_PATH" + "\n" + "ORACLE_HOME="  + home + "\n" + "ORACLE_SID=" + sid + "\n" + "LD_LIBRARY_PATH=" + home + "/lib";
		                     
		                     //$cmd0 = join("\n",
		                                // "export ORACLE_HOME",
		                                 //"export ORACLE_SID",
		                                 //"export LD_LIBRARY_PATH",
		                                 //"ORACLE_HOME='$home'",
		                                 //"ORACLE_SID='$sid'",
		                                 //"LD_LIBRARY_PATH='$home/lib'");

		                     //&echo("Trying to validate $sid for $home using oracle home.");
		                     cmd = cmd0 + "\n" + home + "/bin/sqlplus BDNA_VALIDATE/BDNA_WRONG_PASSWORD < /dev/null";
		                     //$cmd = join("\n",
		                       //          $cmd0,
		                         //        "'$home/bin/sqlplus' BDNA_VALIDATE/BDNA_WRONG_PASSWORD < /dev/null");

		                     //String[] output1 = &shellcmd($host, $cmd, "validate_instance");\
		                     String[] output1 = {"validate_instance","ORA-04031: invalid username/password;"};
		                     
		                     for (int j=0;j < output1.length; j++) {
		                    	 //# Example messages:
		                         //# ORA-01017: invalid username/password; logon denied
		                         //# ORA-04031: unable to allocate 16108 bytes of shared memory...
		                         //# ORA-28000: the account is locked....
		                         Matcher lineMat = lineRegex.matcher(output1[j]);
		                         if (lineMat.find()) {
		                        	// &echo("Validated Oracle instance $sid for $home.");
		                             //push(@validated_sids, $sid);
		                        	 validated_sids.add(sid);
		                             validateFlag = "1";
		                        	 
		                         }
		                     }
		                     

		                     if (validateFlag.equals("0")) {
		                         //cmd = "";
		                         //&echo("Trying to validate $sid for $home using oracle home and localhost...");
		                    	 cmd = cmd0 + "\n" + home + "/bin/sqlplus BDNA_VALIDATE/BDNA_WRONG_PASSWORD@localhost/" + sid + "< /dev/null";
		                         //$cmd = join("\n",
		                           //      $cmd0,
		                             //    "'$home/bin/sqlplus' BDNA_VALIDATE/BDNA_WRONG_PASSWORD\@localhost/$sid < /dev/null");

		                         //String output2 = &shellcmd($host, $cmd, "validate_instance");
		                    	 String[] output2 = {"validate_instance","ORA-04031: invalid username/password;"};
		                         for (int j = 0; j < output2.length; j++) {
		                        	 
		                        	 //# Example messages:
		                             //# ORA-01017: invalid username/password; logon denied
		                             //# ORA-04031: unable to allocate 16108 bytes of shared memory...
		                             //# ORA-28000: the account is locked....
		                        	 
		                        	 Matcher lineMat = lineRegex.matcher(output2[j]);
		                        	 if (lineMat.find()) {
		                        		// &echo("Validated Oracle instance $sid for $home using localhost...");
		                        		 validated_sids.add(sid);
		                        		 validateFlag = "1";
		                        		 System.out.println("v_sid=" + sid + ",flag=" + validateFlag);
		                        	 }
		                        	 
		                         }

		                     }
		                 }
		                 

		                 ArrayList<String> re_validated_sids = new ArrayList<String>();
		                 
		                 String dbs_files_cmd = "cd " + home + "/dbs" + "\n" + "ls -l | awk {print \\$9}";
		                 //my $dbs_files_cmd = join("\n",
		                   //                                      "cd $home/dbs",
		                     //                                    "ls -l | awk '{print \$9}'");
		                  //&echo ("Command to be executed is <$dbs_files_cmd>.");
		                 //String[] dbs_files_output = &shellcmd($host, $dbs_files_cmd);
		                 String[] dbs_files_output = {"lkORA11G","orapwora11g"};
		                 //chomp(@dbs_files_output);
		                 
		                 //&echo("Belowing is Re-validate instances for ORACLE_HOME ".$home);
		                 for (int i = 0;i <validated_sids.size();i++) {
		                	 String rv_sid = validated_sids.get(i);
		                	 
		                	 for (int j = 0; j < dbs_files_output.length; j++) {
			                	 String dbs_line = dbs_files_output[j].replaceAll("\r|\n", "");
			                	 Pattern dbsRegex0 = Pattern.compile("^lk(?i)" + rv_sid);
			                	 Pattern dbsRegex1 = Pattern.compile("^hc(?i)_" + rv_sid + "\\.dat");
			                	 Pattern dbsRegex2 = Pattern.compile("^spfile(?i)" + rv_sid + "\\.ora");
			                	 
			                	 Matcher dbsMat0 = dbsRegex0.matcher(dbs_line);
			                	 Matcher dbsMat1 = dbsRegex1.matcher(dbs_line);
			                	 Matcher dbsMat2 = dbsRegex2.matcher(dbs_line);
			                	 System.out.println("testabc:" + rv_sid);
			                	 
			                	 if (dbsMat0.find()) {
			                		 re_validated_sids.add(validated_sids.get(i));
			                		 System.out.println("rv_sid=" + rv_sid + "yeslk");
		                             //&echo("Instances $validated_sid belong to ORACLE_HOME $home by file: lk<oracle_sid>");
		                             break;
			                		 
			                	 } else if (dbsMat1.find()) {
			                		 re_validated_sids.add(validated_sids.get(i));
			                		 System.out.println("rv_sid=" + rv_sid + "yeshc");
			                		 //&echo("Instances $validated_sid belong to ORACLE_HOME $home by file: hc_<oracle_sid>.data");
			                		 break;
			                	 } else if (dbsMat2.find()) {
			                		 re_validated_sids.add(validated_sids.get(i));
			                		 System.out.println("rv_sid=" + rv_sid + "yesspfile");
			                		 //&echo("Instances $validated_sid belong to ORACLE_HOME $home by file :spfile<oracle_sid>.ora");
			                		 break;
			                		 
			                	 }
			                	 
			                 }
		                 }
		                 

		    // # Set a default value if there are no validated instances.  This marker
		     //# will be used for de-dupping.
//		                 $BDNA_Results{"validatedInstances"} = join($BDNA_Separator, @re_validated_sids) || $BDNA_Separator;
              //      } 
		             //else {
//		                 $BDNA_Results{"validatedInstances"} = '<BDNA,>';
//		             }

		     //#
		     //# Find all the init files under the oracle home directory
		     // #

		            // &echo("Doing cachedFind for directory $home for pattern: init.*ora");
		             //String[] output3 = cachedFind($host, 7*24*3600, $BDNA_Params{'root.$bdna.globalModuleConfig:filePatternList'}, $home, 'init.*ora', $BDNA_Params{'root.$bdna.globalModuleConfig.ModularCollectionOutOfSystemFind:ModularCollection::outOfSystemFindFilePath'}, 0);
	                 String[] output3  = {"sigfiles","/u01/app/oracle/product/"};
		             resultString = "";
		             int oplength = output3.length-1;
		             while (oplength >= 0) {
		                 String lineOut = output3[oplength].replaceAll("\r|\n", "");
		                 oplength--;
		                 Pattern opRegex = Pattern.compile("^/");
		                 Matcher opMat = opRegex.matcher(lineOut);
		                 
		                 if (opMat.find()) {
		                     // $file = $_;
		                	 String file = lineOut;
		                	 System.out.println("file:" + file);
		                     //String dbname = &getTargetProperty($host, $file, "db_name");
		                	 String dbname = "ora11g";
		                     if (!dbname.equals("")) {
		                         //&echo("found Oracle init file: $file, db_name = $dbname");
		                         if (!resultString.equals("")) {
		                             //resultString += $BDNA_Separator + file;
		                        	 resultString += "<BDNA,>" + file;
		                         }
		                         else {
		                             resultString = file;
		                         }
		                         System.out.println("resultString1:" + resultString);
		                     }
		                 }
		             }
		//             $BDNA_Results{"OracleInitFiles"} = $resultString;

		     //#
		     //# use lsnrctl to figure out the listener/service pairs
		     //#
		            // $cmd = join("\n",
		              //           "export ORACLE_HOME",
		                //         "export LD_LIBRARY_PATH",
		                  //       "ORACLE_HOME='$home'",
		                     //    "LD_LIBRARY_PATH='$home/lib'",
		                       //  "'$home/bin/lsnrctl' status");
		             
		             cmd = "export ORACLE_HOME" + "\n" + "export LD_LIBRARY_PATH" + "\n" + "ORACLE_HOME=" + home + "\n" + "LD_LIBRARY_PATH=" + home + "lib" + "\n" + home + "/bin/lsnrctl status";
		          
		             //String output4 = &shellcmd($host, $cmd, "lsnrctl_status");
		             String output4[] = {"Connecting to (DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=192.168.9.102)(PORT=1521)))","Alias                     LISTENER","Listener Parameter File   /u01/app/oracle/product/11.2.0/network/admin/listener.ora","Listening Endpoints Summary...","(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=192.168.9.102)(PORT=1521)))","Instance \"ora11g\", status UNKNOWN, has 1 handler(s) for this service...","Instance \"ora11g\", status READY, has 1 handler(s) for this service..."};

		             String addr = "",listener = "", service = "";
		             resultString = "";
		             String matched = "";
		            // oplength = output4.length-1;
		             
		             Pattern addrRegex = Pattern.compile("Connecting to (.*)$");
	            	 Pattern lisRegex0 = Pattern.compile("Listener Parameter File\\s*" + (home + ".*"));
	            	 Pattern lisRegex1 = Pattern.compile("Alias(\\W*)(\\w*)$");
	            	 Pattern serRegex0 = Pattern.compile("(\\w*)(\\W*)has(.*)service handler");
	            	 Pattern serRegex1 = Pattern.compile("Instance \"(.*)\", status .*, has .* handler");
	            	 
	            	 for (int i = 0;i < output4.length;i++) {
		            	 String lineOp = output4[i];
		            	 
		            	 Matcher addrMat = addrRegex.matcher(lineOp);
		            	 Matcher lisMat0 = lisRegex0.matcher(lineOp);
		            	 Matcher lisMat1 = lisRegex1.matcher(lineOp);
		            	 Matcher serMat0 = serRegex0.matcher(lineOp);
		            	 Matcher serMat1 = serRegex1.matcher(lineOp);
		                 if (addrMat.find()) {
		                    // &echo($1);
		                     addr = addrMat.group(1);
		                     System.out.println("addr:" + addr);
		                 } else if (lisMat0.find()) {
		                     //# the listner parameter file must match the installation directory
		                     //&echo("listener $1 matches $home");
		                     matched = "TRUE";
		                     System.out.println("matched: " + matched);
		                 } else if (lisMat1.find()) {
		                     //&echo($2);
		                     listener = lisMat1.group(2);
		                     System.out.println("listener: " + listener);
		                 } else if (matched.equals("TRUE") && serMat0.find()) {
		                     // # Oracle 8i output (no clear distinction between instances and services)
		                     service = serMat0.group(1);
		                     //echo("listener: $listener, service: $service");
		                     //String pair = listener + $BDNA_Separator + service;
		                     String pair = listener + "<BDNA,>" + service;
		                     System.out.println("pair: " + pair);
		                     if(!resultString.equals("")) {
		                         //resultString += $BDNA_Separator + pair;
		                    	 resultString += "<BDNA,>" + pair;
		                     }
		                     else {
		                         resultString = pair;
		                     }
		                     System.out.println("resultString2" + resultString);
		                 } else if (matched.equals("TRUE") && serMat1.find()) {
		                     //# Oracle 9i output (services have their own entries, to be consistent with 8i, we get the instances instead of the services)
		                     service = serMat1.group(1);
		                     //&echo("listener: $listener, service: $service");
		                     //String pair = listener + $BDNA_Separator + service;
		                     String pair = listener + "<BDNA,>" + service;
		                     System.out.println("listener:" + listener + ", service: " + service);
		                     if(!resultString.equals("")) {
		                        // resultString += $BDNA_Separator + pair;
		                    	 resultString += "<BDNA,>"  + pair;
		                     }
		                     else {
		                         resultString = pair;
		                     }
		                     System.out.println("resultString3:" + resultString);
		                     
		                 }
		                 
	               }
		             
		             
		         //  $BDNA_Results{"listenerServices"} = $resultString;

		           cmd = "cat " + home + "/network/admin/listener.ora";
		           //String[] output5 = &shellcmd($host, $cmd, "listener.ora");
		           String[] output5 = {"LISTENER =","SID_LIST","ADR_BASE_LISTENER = /u01/app/oracle"};
		           oplength = output5.length-1;
		          
		           Pattern lisNameRegex = Pattern.compile("^(\\w+)\\s+=");
		           
		           String listenerNames = "";
		           while (oplength >= 0) {
		               String oplin = output5[oplength];
		               oplength--;
		               Matcher oplinMat = lisNameRegex.matcher(oplin);
		               if (oplinMat.find()) {
		                   if(!oplin.contains("ADR_BASE") && !oplin.contains("SID_LIST")) {
		                       if(!listenerNames.equals("")) {
		                           //listenerNames += $BDNA_Separator + oplinMat.group(1);
		                    	   listenerNames += "<BDNA,>" + oplinMat.group(1);
		                       }
		                       else {
		                           listenerNames = oplinMat.group(1);
		                       }
		                    }
		                   
		               }
		           }
		           System.out.println("listenerNames:" + listenerNames);


	             
	}


}
