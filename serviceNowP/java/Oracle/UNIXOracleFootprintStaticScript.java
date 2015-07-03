package UNIXOracle;
import java.awt.List;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.regex.*;
import java.util.*;

public class UNIXOracleFootprintStaticScript {
	public static void main(String[] args) {
         
		String sigFile = "";
        //get the telnet connection (set by the connection script)
                // $host = $BDNA_Connection_Info{"HostObject"};
		        String host = "192.168.8.1";
        //get the signature files (set by fingerprint)
                 //sigFileString = $BDNA_Params{"root.types.footprint.OracleFootprint.UNIXOracleFootprint:signatureFiles"};
	             String sigFileString = "/bin/lsnrctl<BDNA,>/bin/sqlplus" ;
                 String[] sigFiles = sigFileString.split("<BDNA,>");
         // initialize the working variables
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
                 System.out.println("sigp:" + sigPatterns);
               //issue the find/grep command to locate directories with signature files
                  String cmd = "",one = "";
             //&echo("Doing cachedFind for directory root for pattern: $sigPatterns");
                 // String[]  output = cachedFind($host, 7*24*3600, $BDNA_Params{'root.$bdna.globalModuleConfig:filePatternList'}, "/", $sigPatterns, $BDNA_Params{'root.$bdna.globalModuleConfig.ModularCollectionOutOfSystemFind:ModularCollection::outOfSystemFindFilePath'}, 0);
                  String[]  output = {"/BA/app/oracle/product/11.2.0/bin/lsnrctl","/BA/app/oracle/product/11.2.0/bin/sqlplus"};
                  int oplength = output.length-1;
                  while (oplength>=0){
                    one = output[oplength];
                    one = one.replaceAll("\r|\n", "");
                    
                    //&echo("considering $one for a Oracle home directory...");
                    //records what home directory candidate contains what signature files
                    
                    
                    sigPath.put(one, 1);
                    
                    //# records what home directory candidate contains what signature files
                 //$sigPath{$one} = 1;
                                            //$_ = $one;

                    for (int i = 0; i < sigFiles.length; i++) {
                    	//System.out.println("one:" + one);
                    	//System.out.println("sigFiles[i]:" + sigFiles[i]);
                            Pattern homeRegex = Pattern.compile("(.*)" + sigFiles[i]);
                                Matcher matcher = homeRegex.matcher(one);
                                if (matcher.find()) {
                                	
                                    homeDir.put(matcher.group(1), 1);
                                }
                                //System.out.println("homeDir:" + homeDir.g);       
                                //System.out.println("sigFiles[i]:" + sigFiles[i]);
                    }
                 oplength--;
             }
             // extract additional Oracle home directories from /oratab
             // format is: ORACLE_SID:ORACLE_HOME:<N|Y>...

          cmd = "cat /etc/oratab /var/opt/oracle/oratab 2> /dev/null";
          //String[] output0 = &shellcmd($host, $cmd, "oratab");
          String[] output0 = {" Multiple entries with the same $ORACLE_SID are not allowed.","ora11g:/BA/app/oracle/product/11.2.0:Y"};
          oplength = output0.length-1;
          while (oplength>=0) {
              //$_ = shift(@output0);
              Pattern homeRegex = Pattern.compile("^([^#:]*):([^:]*):[NYny]");
                  Matcher matcher = homeRegex.matcher(output0[oplength]);
                  oplength--;
                  if (matcher.find()) {
                      //&echo("found Oracle home directory from /etc/oratab: $2, SID $1...");
                          homeDir.put(matcher.group(2),1);
                          System.out.println("homeDir:" + matcher.group(2));
                  }
           }


          ArrayList<String> resultDir = new ArrayList<String>();
          //String [] resultDir;
          String sigFileNotFound = "1";

          Iterator<?> iterDir = homeDir.entrySet().iterator();
          Iterator<?> iterSig = sigPath.entrySet().iterator();

          String key= "";
         
          
          
          while(iterDir.hasNext()){
              Map.Entry entryDir = (Map.Entry) iterDir.next();
              Object keyDir = entryDir.getKey();
              //Object val = entry.getValue();
              while(iterSig.hasNext()){
                  Map.Entry entrySig = (Map.Entry) iterSig.next();
                  Object keySig = entrySig.getKey();
                  for (int i = 0; i < sigFiles.length; i++) {
                	  
                      String sigFullPath = keyDir + sigFiles[i];
                      System.out.println("sigFullPath:" + sigFullPath);
                      System.out.println("keySig     :" + keySig);
                      //if (!keySig.equals(sigFullPath)) {
                        //  sigFileNotFound = "1";
                         //break;
                       //}

                       if (keySig.equals(sigFullPath) && sigPath.size() == sigFiles.length) {
                    	   sigFileNotFound = "0";
                    	   break;
                       }
                   }
                }
                if (!sigFileNotFound.equals("1")) {
                    resultDir.add((String) keyDir);
                    System.out.println("resultDir:" + keyDir);
                   
                 }
             }
          //#
          //# look for listen processes and guess Oracle homes from there
          //#
                  //String[] outputTns = &UNIXps(host, "tnslsnr");
                  String[] outputTns = {"/BA/app/oracle/product/11.2.0/bin/tnslsnr"};
                  oplength = outputTns.length-1;
                  Pattern dirRegex = Pattern.compile("(\\S*)/bin/tnslsnr");
                  while (oplength>=0) {
                      Matcher dirMat = dirRegex.matcher(outputTns[oplength].replaceAll("\r|\n", ""));
                      if (dirMat.find()) {
                          String dir = dirMat.group(1);
                          //&echo("Locating Oracle home from tnslsnr: $dir");
                          System.out.println("Locating Oracle home from tnslsnr: " + dir);
                          resultDir.add(dir);
                      }
                      oplength--;
                  }
       // construct the result, which is a list of pairs of (<home dir>, <version>)
          String resultString = "", hdir = "";

          //&echo("Oracle home dir list: @resultDir");
          for (int i=0;i<resultDir.size();i++) {
              //# svrmgrl exists before 9i, and will tell us a version number.
              //# But its version number doesn't always give the same major version
              //# number that people think of when they think Oracle database numbers,
              //# so we need to do some re-mapping.
              String dir = resultDir.get(i);
              cmd = "export ORACLE_HOME" + "\n" + "export LD_LIBRARY_PATH" + "\n" + "ORACLE_HOME=" + dir + "\n" + "LD_LIBRARY_PATH=" + dir + "/lib" + "\n" + dir + "/bin/svrmgrl -? < /dev/null";

              //String output1[] = &shellcmd($host, $cmd, "svrmgrl_version");
              
              String output1[] = {"test0","Release 11.1.0.1.0"};
              String version = "0",verString = "";
              Map<String,String> remapMajorVersion = new HashMap<String, String>();
              remapMajorVersion.put("2", "7");
              remapMajorVersion.put("3", "8");
              oplength = output1.length-1;
              


              for (int j=0;j<oplength; j++) {
                      Pattern verRegex = Pattern.compile("Release (\\d+)\\.(\\d+)\\.(\\d+)\\.(\\d+)\\.(\\d+)");
                      Matcher matcher = verRegex.matcher(output1[oplength]);
              if (matcher.find()) {
                      Iterator<?> iterVer = remapMajorVersion.entrySet().iterator();
                      while (iterVer.hasNext()) {
                    	  //System.out.println("verString:" + verString);
                              Map.Entry entryVer = (Map.Entry) iterVer.next();
                              Object keyVer = entryVer.getKey();
                              Object valVer = entryVer.getValue();
                              //System.out.println(keyVer + "=" + valVer);
                              //System.out.println("tur" + "=" + matcher.group(1));
                              if (valVer.equals(matcher.group(1))) {
                                     //&echo("Remapped svrmgrl-reported major version " .
                                    //"from '$1' to '$remapMajorVersion{$1}'.");
                              verString = valVer + matcher.group(2) + matcher.group(3);
                              System.out.println("verString:" + verString);
                              }
                      }

                      break;
              }
              }
              if (verString.equals("")) {
                  // OK, svrmgrl didn't work.  (Is this 9i? 9i doesn't provide svrmgrl.)
                  //# Try sqlplus -V (which doesn't work before 9i....)
                  cmd = "export ORACLE_HOME" + "\n" + "export LD_LIBRARY_PATH" + "\n" + "ORACLE_HOME=" + dir + "\n" + "LD_LIBRARY_PATH=" + dir + "/lib" + "\n" + dir + "/bin/sqlplus -V < /dev/null";
                  //String[] output2 = &shellcmd($host, $cmd, "sqlplus_version");
                  String[] output2 = {"SQL*Plus: Release 11.2.0.2.0 Production"};
                  //oplength = output2.length-1;
                  for (int j=0;j<output2.length; j++) {
                     Pattern verRegex0 = Pattern.compile("Release (\\d+\\.\\d+\\.\\d+)\\.(\\d+)\\.(\\d+)");
                     System.out.println("output2[j]");
                     Matcher matcher0 = verRegex0.matcher(output2[j]);
                     if (matcher0.find()) {
                          verString = matcher0.group(1);
                          System.out.println("verString:" + verString);
                          break;
                     }
                  }

              } 
              //  # try to cat XML_INV_LOC from $dir/inventory/ContentsXML/comps.xml.
              String invLoc = "";
              String cmdComp =  "export ORACLE_HOME" + "\n" + "export LD_LIBRARY_PATH" + "\n" + "ORACLE_HOME=" + dir + "\n" + "cat " + dir + "/inventory/ContentsXML/comps.xml' < /dev/null";
              //String[] outputComp = &shellcmd($host, $cmdComp, "context_LOC");
              String[] outputComp = {"xml","<COMP NAME=\"oracle.server\" VER=\"11.2.0.2.0\" BUILD_NUMBER=\"0\" REP_VER=\"0.0.0.0.0\" RELEASE=\"Production\" INV_LOC=\"Components/oracle.server/11.2.0.2.0/1/\" LANGS=\"en\" XML_INV_LOC=\"Components21/oracle.server/11.2.0.2.0/\""};

              for (int j=0;j<outputComp.length; j++) {
                  Pattern invLocRegex = Pattern.compile("\"oracle.server\"[^<]+XML_INV_LOC=\"([^\"]*)/\"");
                  Matcher matcher = invLocRegex.matcher(outputComp[j]);
                  if (matcher.find()) {
                      invLoc = matcher.group(1);
                      System.out.println("invLoc:" + invLoc);
                      break;
                  }
              }
              
           // # try to cat edition from $dir/inventory/ContentsXML/comps.xml.
              String cmdCon =  "export ORACLE_HOME" + "\n" + "export LD_LIBRARY_PATH" + "\n" + "ORACLE_HOME=" + dir + "\n" + "cat " + dir + "/inventory/ContentsXML/comps.xml < /dev/null";

              //String[] outputCon = &shellcmd($host, $cmdCon, "context_Edition");
              String[] outputCon = {"Oracle server","<INST_TYPE NAME=\"EE\" NAME_ID=\"EE\" DESC_ID=\"EE_DESC\"/>"};
              String edtionString = "";
              for (int j=0;j<outputCon.length; j++) {
                  Pattern ediRegex = Pattern.compile("\"(EE|SE)\"");
                  Matcher matcher = ediRegex.matcher(outputCon[j]);
                  if (matcher.find()) {
                          edtionString = matcher.group(1);
                          if (edtionString.equals("EE")) {
                          edtionString = "Enterprise";
                      }
                          if (edtionString.equals("SE")) {
                          edtionString = "Standard";
                      }
                          System.out.println("edtionString:" + edtionString);   
                          break;
                  }
              }

              //&echo("ERROR!  Unable to determine Oracle edition for XML_INV_LOC $invLoc.")
              if (edtionString.equals(""));
              //&echo("ERROR!  Unable to determine Oracle version for OracleHome $dir.")
              if (edtionString.equals(""));

            //  hdir = verString + $BDNA_Separator + dir + $BDNA_Separator + edtionString;

              //&echo("Oracle home dir: $hdir");

              //if(!resultString.equals("")) {
                //  resultString +=BDNA_Separator + hdir;
              //}
              //else {
              //    resultString = hdir;
              //}


          }
          

          
	}
}