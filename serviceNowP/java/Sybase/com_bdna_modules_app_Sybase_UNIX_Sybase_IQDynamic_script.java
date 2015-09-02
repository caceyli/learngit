package Sybase;
import java.awt.List;
import java.util.ArrayList;
import java.util.Iterator;
import java.util.regex.*;
import java.util.*;


public class com_bdna_modules_app_Sybase_UNIX_Sybase_IQDynamic_script {
	public static void main(String[] args) {
		//my $host = $BDNA_Connection_Info{"HostObject"};
        String host = "192.168.8.152";
        //my $_UNIX_Sybase_IQ_installDirectory = $BDNA_Params{"root.types.resource.software.application.Sybase.UNIX_Sybase_IQ:installDirectory"};
        String _UNIX_Sybase_IQ_installDirectory = "/opt/app/Sybase/UNIX_Sybase_IQ";
        String _UNIX_Sybase_IQ_isRunning = "";
        //my @_runningProcess = &_j4_findProcess($host, [("iqsrv")]);
        //my @_runningProcess2 = &_j4_findProcess($host, [("java")]);
        String[] _runningProcess ={"/opt/app/Sybase/UNIX_Sybase_IQ/bin/iqsrv"};
        String[] _runningProcess2 = {"iq.agent=/opt/app/Sybase/UNIX_Sybase_IQ/java/IQAgent2.jar"};
        String _isRunning = "False";
        String _isRunningDir = "";
       
        for (int i=0; i<_runningProcess.length; i++) {
        	Pattern _exProcess = Pattern.compile("(.*)bin/iqsrv|(.*)bin64/iqsrv");
        	System.out.println(_runningProcess[i]);
        	Matcher matcher0 = _exProcess.matcher(_runningProcess[i]);
        	if (matcher0.find()) {
        		_isRunningDir = matcher0.group(1);
                System.out.println("RunningDir:" + _isRunningDir);
                if (_isRunningDir.contains(_UNIX_Sybase_IQ_installDirectory)) {
                	_isRunning = "True";
                	System.out.println("isRunning:" + _isRunning);
                }
                break;
           }
        	
        }
        
        for (int i=0; i<_runningProcess2.length; i++) {
        	Pattern _exProcess2 = Pattern.compile("iq.agent=(.*)/java/IQAgent\\d*.jar");
        	System.out.println(_runningProcess2[i]);
        	Matcher matcher0 = _exProcess2.matcher(_runningProcess2[i]);
        	if (matcher0.find()) {
        		_isRunningDir = matcher0.group(1);
                System.out.println("RunningDir:" + _isRunningDir);
                if (_isRunningDir.contains(_UNIX_Sybase_IQ_installDirectory)) {
                	_isRunning = "True";
                	System.out.println("isRunning:" + _isRunning);
                }
                break;
           }
        	
        }
        	
        _UNIX_Sybase_IQ_isRunning = _isRunning;
        System.out.println("UNIX Sybase IQ isRunning:" + _UNIX_Sybase_IQ_isRunning);
        //$BDNA_Results{"isRunning"} = $_UNIX_Sybase_IQ_isRunning;
	}
        
}
