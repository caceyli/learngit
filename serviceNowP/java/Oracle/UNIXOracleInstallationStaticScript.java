package UNIXOracle;
import java.awt.List;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.regex.*;
import java.util.*;
public class UNIXOracleInstallationStaticScript {
	public static void main(String[] args) {
		
		//String host = $BDNA_Connection_Info{"HostObject"};
        //String home = $BDNA_Params{"root.types.resource.software.installation.OracleInstallation.UNIXOracleInstallation:OracleHome"};
        String hosttype = "",dev_name= "", inode_num= "", df_cmd = "";
        String host = "192.168.9.102";
		String home = " /u01/app/oracle/product/11.2.0";
          //   #
          //   # Use different df command for different OS type
          //   #
             //String[] hosttypeArray = &shellcmd($host, 'uname', 'uname');
              
             String[] hosttypeArray = {"Linux"};
             if (hosttypeArray.length>0) {
                 hosttype = hosttypeArray[0];
                 hosttype = hosttype.replaceAll("\r|\n", "");
             }
             Pattern htRegex = Pattern.compile("SunOS|AIX|Linux|OSF1|IRIX|IRIX64");

             Matcher htMat = htRegex.matcher(hosttype);

             //&echo('<'. $hosttype .'>');
             if (htMat.find()) {
                 //&echo("hosttype is $hosttype");
                 df_cmd = "df -k " + home + " | awk '{print $1}'";
             }
             else if (hosttype.contains("HP-UX")) {
                 df_cmd = "bdf " + home + " | awk '{print $1}'";
             }
             else {
                // &echo("HOSTTYPE is not known supported type.");
            	 System.out.println("HOSTTYPE is not known supported type.");
             }
           //#
             //#retrieve file system name from df command.
             //#
             if (!df_cmd.equals("")) {
                 //String[] output0 = &shellcmd($host, $df_cmd, 'df_k');
            	 String[] output0 = {"",""};
                 //shift(@output) if ((scalar @output >= 0) && ($output[0] =~ m/^Filesystem/));

                 Pattern opRegex = Pattern.compile("Filesystem");
                 Pattern tempRegex = Pattern.compile("\\(|\\)");
                 Pattern tempRegex0 = Pattern.compile("^\\S+:/\\S*$");
                 Pattern tempRegex1 = Pattern.compile("^/\\S*");

                 Matcher opMatcher = opRegex.matcher(output0[0]);

                 if (opMatcher.find()) {
                         if (output0.length > 0){
                             String temp = output0[0];
                             temp = temp.replaceAll("\r|\n", "");
                             temp = temp.replaceAll("^\\s+", "");//$temp =~ s/^\s+//; $temp =~ s/\s+$//;

                             Matcher tempMat = tempRegex.matcher(temp);
                             Matcher tempMat0 = tempRegex.matcher(temp);
                             Matcher tempMat1 = tempRegex.matcher(temp);
                             if (!tempMat.find()) {
                                 //# ensure parsed value is file path or some network file path.
                                 if (tempMat0.find() || tempMat1.find()) {
                                     dev_name = temp;
                                 }
                             }
                             dev_name = dev_name.replaceAll("[/\\.\\:]", "_");
                         }
                 }
             }
             
           //#
             //#retrieve inode number
             //#
             String cmd = "echo `ls -Lid " + home + "`" + " __BDNA_RESULT__";
             //String[] output1 = &shellcmd($host, $cmd, "inode");
             String[] output1 = {"dd"};
             int oplength = output1.length-1;
             Pattern lineRegex = Pattern.compile("(\\d+) "+ home + " __BDNA_RESULT__");

             while (oplength>=0) {
                 String line = output1[oplength];
                 oplength--;


                 Matcher lineMatcher = lineRegex.matcher(output1[oplength]);
                 if (lineMatcher.find()) {
                     if (!dev_name.equals("")) {
                         String uniqueIdentifier = lineMatcher.group(1) + dev_name;
                     } else {
                         String uniqueIdentifier = lineMatcher.group(1);
                     }
                 }
             }


	}
}
