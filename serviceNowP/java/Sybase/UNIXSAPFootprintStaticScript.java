package Sybase;
import java.awt.List;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.regex.*;
import java.util.*;

public class UNIXSAPFootprintStaticScript {
	public static void main(String[] args) {
		// get the telnet connection (set by the connection script)
		// my $host = $BDNA_Connection_Info{"HostObject"};
		String host = "192.168.8.152";
		
		// get the signature files (set by fingerprint)
		// $sigFileString = $BDNA_Params{"root.types.footprint.SAPFootprint.UNIXSAPFootprint:signatureFiles"};
		// @sigFiles = split(/$BDNA_Separator/, $sigFileString);
		String sigFileString = "/SYS/exe/run/R3trans<BDNA,>/SYS/exe/run/saplicense" ;
        String[] sigFiles = sigFileString.split("<BDNA,>");
        
        // initialize the working variables perl (%sigPath = (); %homeDir = ();)
        Map<String,Integer> sigPath = new HashMap<String, Integer>();
        Map<String,Integer> homeDir = new HashMap<String, Integer>();
        
        // formulate a list of regular expressions from the signature file list
        String sigPatterns = "";

        for (int i=0;i < sigFiles.length; i++) {
                if (!sigPatterns.equals("")) {
                        sigPatterns = sigPatterns + "|" + sigFiles[i] + "$";
                } else {
                        sigPatterns = sigFiles[i] + "$";
                }
                
        }
        System.out.println("sigp:" + sigPatterns); //for test 
        
        // issue the find/grep command to locate directories with signature files  (perl: my ($cmd, $one);)
        String cmd = "",one = "";
        // &echo("Doing cachedFind for directory root for pattern: $sigPatterns");
        System.out.println("Doing cachedFind for directory root for pattern:" + sigPatterns); 
        // String[]  output = cachedFind($host, 7*24*3600, $BDNA_Params{'root.$bdna.globalModuleConfig:filePatternList'}, "/", $sigPatterns, $BDNA_Params{'root.$bdna.globalModuleConfig.ModularCollectionOutOfSystemFind:ModularCollection::outOfSystemFindFilePath'});
        String[]  output = {"/opt/app/Sybase/UNIXSAP/SYS/exe/run/R3trans","/opt/app/Sybase/UNIXSAP/SYS/exe/run/saplicense"};
        
        int oplength = output.length-1;
        while (oplength>=0){
          one = output[oplength];
          one = one.replaceAll("\r|\n", "");
         
          // &echo("considering $one for a SAP home directory...");
          System.out.println("considering $one for a SAP home directory...");
          
          // what home directory candidate contains what signature files  (perl: $sigPath{$one} = 1;)
          sigPath.put(one, 1);
          
          // extract the home directory by removing the signature file suffix (perl: $_ = $one;)
          for (int i = 0; i < sigFiles.length; i++) {
                	  	  
              //System.out.println("one:" + one);
          	  //System.out.println("sigFiles[i]:" + sigFiles[i]);
              Pattern homeRegex = Pattern.compile("(.*)" + sigFiles[i]);
              Matcher matcher = homeRegex.matcher(one);
              if (matcher.find()) {
            	  homeDir.put(matcher.group(1), 1);                      	                          
               }
              System.out.println("sigFiles[" +i +"]:" + sigFiles[i]); //test
              System.out.println("homeDir:" + homeDir);       //test
              
          }
       oplength--;
          
        }
        
        ArrayList<String> resultDir = new ArrayList<String>();
        //String [] resultDir;
                        
        Iterator<?> iterDir = homeDir.entrySet().iterator();        
        
        while(iterDir.hasNext()){
        	String sigFileNotFound="0";
        	Map.Entry entryDir = (Map.Entry) iterDir.next();
            Object keyDir = entryDir.getKey();  
            System.out.println("keyDir     :" + keyDir);         
            // Object val = entry.getValue();
          
            for (int i = 0; i < sigFiles.length; i++) {                	              	  
                 String sigFullPath = keyDir + sigFiles[i];
                 if (sigPath.get(sigFullPath)==null)	{
                     sigFileNotFound = "1";
                     break;
                 }
            }
              
            if (!sigFileNotFound.equals("1")) {
                resultDir.add((String) keyDir);
                System.out.println("resultDir:" + keyDir);
               
            }
        }

        // construct the result, which is a list of pairs of (<home dir>, <SID>)
        // &echo("SAP home dir list: @resultDir");
        
        
        		
	}
}
