package Sybase;
import java.awt.List;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.regex.*;
import java.util.*;

public class UNIXSAPInstallationDynamicScript {
	public static void main(String[] args) {
		
// extract license host and database info from the config file
        // String dir = $BDNA_Params{"root.types.resource.software.installation.SAP.SAPInstallation.UNIXSAPInstallation:installDirectory"};
        // String sid = $BDNA_Params{"root.types.resource.software.installation.SAP.SAPInstallation.UNIXSAPInstallation:SID"};
		String dir = "/opt/app/Sybase/UNIXSAP";
		String sid = "vmhost152";
		String configFilePath = dir + "/SYS/profile/DEFAULT.PFL";
		String cmd_file = "cat $configFilePath";
		Map<String,String> BDNA_Results = new HashMap<String, String>(); // test
		
		//String[] lines = &shellcmd($BDNA_Connection_Info{"HostObject"},$cmd_file, "sapconf");
		String[] lines = {"rdisp/mshost = licenceServerHost152","dbms/type = databaseType1","dbtype = databaseType2","sapdbhost = databaseServerHost", "/dbhost = databaseServerHost", "/dbname = databaseSchema" };
		String line = "";
		for (int i=0; i<lines.length;i++) {
			line = lines[i];
	        Pattern licenseServerHostRegex = Pattern.compile("^[^#]*\\s*rdisp\\/mshost\\s*=\\s*(\\w+)(\\s*)$", Pattern.CASE_INSENSITIVE);    
	        Pattern databaseTypeRegex1 = Pattern.compile("^[^#]*\\s*dbms\\/type\\s*=\\s*(\\w+)(\\s*)$", Pattern.CASE_INSENSITIVE);
	        Pattern databaseTypeRegex2 = Pattern.compile("^[^#]*\\s*dbtype\\s*=\\s*(\\w+)(\\s*)$", Pattern.CASE_INSENSITIVE);    
	        Pattern databaseServerHostRegex1 = Pattern.compile("^[^#]*\\s*sapdbhost\\s*=\\s*(\\w+)(\\s*)$", Pattern.CASE_INSENSITIVE);
	        Pattern databaseServerHostRegex2 = Pattern.compile("^[^#]*\\/dbhost\\s*=\\s*(\\w+)(\\s*)$", Pattern.CASE_INSENSITIVE); 
	        Pattern databaseSchemaRegex1Regex = Pattern.compile("^[^#]*\\/dbname\\s*=\\s*(\\w+)(\\s*)$", Pattern.CASE_INSENSITIVE);  	        
	        
	        Matcher licenseServerHostM = licenseServerHostRegex.matcher(line);
	        Matcher databaseTypeM1 = databaseTypeRegex1.matcher(line);
	        Matcher databaseTypeM2 = databaseTypeRegex2.matcher(line);
	        Matcher databaseServerHostM1 = databaseServerHostRegex1.matcher(line);
	        Matcher databaseServerHostM2 = databaseServerHostRegex2.matcher(line);
	        Matcher databaseSchemaM = databaseSchemaRegex1Regex.matcher(line);
	        
            if (licenseServerHostM.find()) {
            	// BDNA_Results{"licenseServerHost"} = $1;
            	BDNA_Results.put("licenseServerHost", licenseServerHostM.group(1));
            	BDNA_Results.put("globalSID", sid + "@" + licenseServerHostM.group(1));
            } else if (databaseTypeM1.find()) {
            	BDNA_Results.put("databaseType", databaseTypeM1.group(1));            	            	
            } else if (databaseTypeM2.find()) {
            	BDNA_Results.put("databaseType", databaseTypeM2.group(1));            	
            } else if (databaseServerHostM1.find()) {
            	BDNA_Results.put("databaseServerHost", databaseServerHostM1.group(1));
            } else if (databaseServerHostM2.find()) {
            	BDNA_Results.put("databaseServerHost", databaseServerHostM2.group(1));
            } else if (databaseSchemaM.find()) {
            	BDNA_Results.put("databaseSchema", databaseSchemaM.group(1));
            }
           			
		}
		

//extract kernel version from saplicense
        String cmd_ver1 = "export LIBPATH=$dir/SYS/exe/run\n$dir/SYS/exe/run/saplicense -version";
        String line_ver1 = "";
        // my @lines = &shellcmd($BDNA_Connection_Info{"HostObject"},$cmd_ver1, "sapver");
		String[] lines_ver1 ={"kernel release ", "SAP release: ", "patch number "};
		for (int i=0; i< lines_ver1.length; i++) {
			line_ver1 = lines_ver1[i];
	        Pattern kernelVersionRegex1 = Pattern.compile("kernel release\\s*(\\w+)\\s*$", Pattern.CASE_INSENSITIVE);
	        Pattern kernelVersionRegex2 = Pattern.compile("SAP release:\\s*(\\w+)", Pattern.CASE_INSENSITIVE); 
	        Pattern patchNumberRegex = Pattern.compile("patch number\\s*(\\w+)\\s*$", Pattern.CASE_INSENSITIVE);  	        
	        
	        Matcher kernelVersionM1 = kernelVersionRegex1.matcher(line_ver1);
	        Matcher kernelVersionM2 = kernelVersionRegex2.matcher(line_ver1);
	        Matcher patchNumberM = patchNumberRegex.matcher(line_ver1);
			
	        if (kernelVersionM1.find()) {
	        	BDNA_Results.put("kernelVersion",kernelVersionM1.group(1));
	        } else if (kernelVersionM2.find()) {
	        	BDNA_Results.put("kernelVersion",kernelVersionM2.group(1));            	            	
            } else if (patchNumberM.find()) {
        	    BDNA_Results.put("patchNumber", patchNumberM.group(1));     
	     	}
		}
		
		if (BDNA_Results.get("kernelVersion")==null ||BDNA_Results.get("patchNumber")==null ) {
			String cmd_ver2 = "export LIBPATH=$dir/SYS/exe\n$dir/SYS/exe/run/saplicense -version";
	        String line_ver2 = "";
	        // my @lines = &shellcmd($BDNA_Connection_Info{"HostObject"},$cmd_ver1, "sapver");
			String[] lines_ver2 ={"kernel release ", "SAP release: ", "patch number 6 "};
			for (int i=0; i< lines_ver2.length; i++) {
				line_ver2 = lines_ver2[i];
		        Pattern kernelVersionRegex1 = Pattern.compile("kernel release\\s*(\\w+)\\s*$", Pattern.CASE_INSENSITIVE);
		        Pattern kernelVersionRegex2 = Pattern.compile("SAP release:\\s*(\\w+)", Pattern.CASE_INSENSITIVE); 
		        Pattern patchNumberRegex = Pattern.compile("patch number\\s*(\\w+)\\s*$", Pattern.CASE_INSENSITIVE);  	        
		        
		        Matcher kernelVersionM1 = kernelVersionRegex1.matcher(line_ver2);
		        Matcher kernelVersionM2 = kernelVersionRegex2.matcher(line_ver2);
		        Matcher patchNumberM = patchNumberRegex.matcher(line_ver2);
				
		        if (kernelVersionM1.find()) {
		        	BDNA_Results.put("kernelVersion",kernelVersionM1.group(1));
		        } else if (kernelVersionM2.find()) {
		        	BDNA_Results.put("kernelVersion",kernelVersionM2.group(1));            	            	
	            } else if (patchNumberM.find()) {
	        	    BDNA_Results.put("patchNumber", patchNumberM.group(1));     
		     	}
		     }
		}
		

		
// extract kernel and patch information for R3tran
		String cmd_R3V1 = "export LIBPATH=$dir/SYS/exe/run\n$dir/SYS/exe/run/R3trans -V";
		String line_R3V1 ="";
		String start = "0";
		String R3transPatches = "";
		//my @lines = &shellcmd($BDNA_Connection_Info{"HostObject"},$cmd_R3V1, "sapver");
		String[] lines_R3V1 = {""};
		for (int i=0; i<lines_R3V1.length; i++) {
			line_R3V1 = lines_R3V1[i];
			Pattern kernelVersionRegex1 = Pattern.compile("kernel release\\s*(\\w+)\\s*$", Pattern.CASE_INSENSITIVE);
			Pattern kernelVersionRegex2 = Pattern.compile("This is.*R3trans.*release\\s*(\\w+)", Pattern.CASE_INSENSITIVE);
			Pattern R3transPatchesRegex1 = Pattern.compile("R3trans patch information", Pattern.CASE_INSENSITIVE);
			Pattern R3transPatchesRegex2 = Pattern.compile("^\\(.*\\).*$", Pattern.CASE_INSENSITIVE);
			
			Matcher kernelVersionM1 = kernelVersionRegex1.matcher(line_R3V1);
			Matcher kernelVersionM2 = kernelVersionRegex2.matcher(line_R3V1);
			Matcher R3transPatchesM1 = R3transPatchesRegex1.matcher(line_R3V1);
			Matcher R3transPatchesM2 = R3transPatchesRegex2.matcher(line_R3V1);
			if (BDNA_Results.get("kernelVersion")==null && kernelVersionM1.find() ) {
				BDNA_Results.put("kernelVersion",kernelVersionM1.group(1));				
			}else if (BDNA_Results.get("kernelVersion")==null && kernelVersionM2.find()) {
				BDNA_Results.put("kernelVersion",kernelVersionM2.group(1));
			}else if (R3transPatchesM1.find()) {
				start="1";
			}else if (start=="1" && R3transPatchesM2.find()) {
				if (BDNA_Results.get("R3transPatches")==null) {
					R3transPatches = line_R3V1 + "<BDNA>";					
				}else {
					R3transPatches = BDNA_Results.get("R3transPatches") + line_R3V1 + "<BDNA>";
				}
				BDNA_Results.put("R3transPatches",R3transPatches);
			}else if (line_R3V1!= null && line_R3V1.equals("")) {
				start = "0";	
			}
				
		}
		
		if (BDNA_Results.get("R3transPatches")==null) {
			cmd_R3V1 = "export LIBPATH=$dir/SYS/exe\n$dir/SYS/exe/run/R3trans -V";
		    line_R3V1 ="";
			start = "0";
			R3transPatches = "";
			//my @lines = &shellcmd($BDNA_Connection_Info{"HostObject"},$cmd_R3V1, "sapver");
			String[] lines_R3V2 = {"kernel release 7", "This is R3trans release 8", "R3trans patch information", "(9.0.1)", ""};
			for (int i=0; i<lines_R3V2.length; i++) {
				line_R3V1 = lines_R3V2[i];
				Pattern kernelVersionRegex1 = Pattern.compile("kernel release\\s*(\\w+)\\s*$", Pattern.CASE_INSENSITIVE);
				Pattern kernelVersionRegex2 = Pattern.compile("This is.*R3trans.*release\\s*(\\w+)", Pattern.CASE_INSENSITIVE);
				Pattern R3transPatchesRegex1 = Pattern.compile("R3trans patch information", Pattern.CASE_INSENSITIVE);
				Pattern R3transPatchesRegex2 = Pattern.compile("^\\(.*\\).*$", Pattern.CASE_INSENSITIVE);
				
				Matcher kernelVersionM1 = kernelVersionRegex1.matcher(line_R3V1);
				Matcher kernelVersionM2 = kernelVersionRegex2.matcher(line_R3V1);
				Matcher R3transPatchesM1 = R3transPatchesRegex1.matcher(line_R3V1);
				Matcher R3transPatchesM2 = R3transPatchesRegex2.matcher(line_R3V1);
				if (BDNA_Results.get("kernelVersion")==null && kernelVersionM1.find() ) {
					BDNA_Results.put("kernelVersion",kernelVersionM1.group(1));				
				}else if (BDNA_Results.get("kernelVersion")==null && kernelVersionM2.find()) {
					BDNA_Results.put("kernelVersion",kernelVersionM2.group(1));
				}else if (R3transPatchesM1.find()) {
					start="1";
				}else if (start=="1" && R3transPatchesM2.find()) {
					if (BDNA_Results.get("R3transPatches")==null) {
						R3transPatches = line_R3V1 + "<BDNA>";					
					}else {
						R3transPatches = BDNA_Results.get("R3transPatches") + line_R3V1 + "<BDNA>";
					}
					BDNA_Results.put("R3transPatches",R3transPatches);
				}else if (line_R3V1!= null && line_R3V1.equals("")) {
					start = "0";	
				}
					
			}
		}

// my output for all collected info this script to help test this script 	(just for use by myself)	
		System.out.println("lines_R3V1.length:" + lines_R3V1.length);
		System.out.println("start:" + start);
		Iterator<?> iterResult = BDNA_Results.entrySet().iterator(); //test
        while(iterResult.hasNext()){ //test
            Map.Entry entryResult = (Map.Entry) iterResult.next(); //test
            Object keyResult = entryResult.getKey();  //test
            Object valueResult =  entryResult.getValue();  //test
            System.out.println(keyResult + ":" + valueResult);  //test
            
        }//test
        

// BDNA output at the end of the script  
        //if ($BDNA_Results{"licenseServerHost"} || $BDNA_Results{"databaseType"}  ||
        //    $BDNA_Results{"databaseServerHost"} || $BDNA_Results{"kernelVersion"} ||
        //    $BDNA_Results{"R3transPatches"} || $BDNA_Results{"TPPatches"}) {
        //    $BDNA_ResultCode = "com.bdna.cle.scripts.success";
        //} else {
        //    $BDNA_ResultCode = "com.bdna.cle.scripts.noData";    	
        //}
        //$BDNA_ErrorCode = 0;
        //$BDNA_MessageBundle = "MessagesBundle";
        
    	String BDNA_ResultCode = "";
        int BDNA_ErrorCode;
        String BDNA_MessageBundle ="";
        
        if (BDNA_Results.get("licenseServerHost")!=null || BDNA_Results.get("databaseType")!=null || BDNA_Results.get("databaseServerHost")!=null || BDNA_Results.get("licenseServerHost")!=null ||BDNA_Results.get("kernelVersion")!=null || BDNA_Results.get("patchNumber")!=null || BDNA_Results.get("R3transPatches")!=null || BDNA_Results.get("databaseServerHost")!=null) {
            BDNA_ResultCode = "com.bdna.cle.scripts.success";   
            } else {
                // $BDNA_ResultCode = "com.bdna.cle.scripts.noData";  
            	BDNA_ResultCode = "com.bdna.cle.scripts.noData"; 
            }        
        //$BDNA_ErrorCode = 0;
        //$BDNA_MessageBundle = "MessagesBundle";
        BDNA_ErrorCode = 0;    //test
        BDNA_MessageBundle = "MessagesBundle";  //test
         

	}	
}
