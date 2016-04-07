package caceyJava;
import java.io.FileInputStream;
import java.util.Properties;
import java.util.Iterator;
import java.io.IOException;

public class getFileContent {
	
	public static void main(String[] args) {
		
	
	 String file = "C:/javaFile/version.txt";
	 try {
	 FileInputStream fs = new FileInputStream(file);
     Properties props = new Properties();
     props.load(fs);
     String version = null;
     String contentVersion = "";
     String contentBuildNumber = "";
     int m_contentVersion;

     for (Iterator iter = props.entrySet().iterator(); iter.hasNext();) {
         java.util.Map.Entry entry = (java.util.Map.Entry)iter.next();

         String key = (String)entry.getKey();
         if (key.equals("bdna.version")) {
             version = (String)entry.getValue();
         } else if (key.equals("bdna.content.releaseVersion")) {
             contentVersion = (String)entry.getValue();
         } else if (key.equals("bdna.content.build.number")) {
             contentBuildNumber = (String)entry.getValue();
         }
         
     }

    fs.close();
    System.out.println(contentBuildNumber);
    System.out.println(contentVersion);
    
    try {
        m_contentVersion = Integer.parseInt(contentVersion);
    } catch (NumberFormatException nfe) {
        m_contentVersion = 0;
    }
    
    System.out.println(m_contentVersion);
    
    }
	catch (IOException  e) {
		 System.out.println("file not existed");

    }
    }

}