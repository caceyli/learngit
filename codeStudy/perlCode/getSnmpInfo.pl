#!/usr/bin/perl
print "start to collect storage information!\n";
print "This may take several minitues......\n";
my $ip;
my $community;
my $port;
my $BDNA_HOME;
if ($ARGV[0] ne "") {
   $ip = $ARGV[0];
} else {
    print "Script exit without running! missing storage host ip\n";
    exit;
}
if ($ARGV[0] ne "") {
   if ($ARGV[1] ne "") {
      $BDNA_HOME = $ARGV[1];
   }
   else {
      print "Script exit without running! Please use like this: ./fetch_infor.pl \$BDNA_HOME\n";
      exit;
    }
}
if ($ARGV[2] ne "") {
   $community = $ARGV[2];
}else {
   $community = "public";
}
if ($ARGV[0] ne "" && $ARGV[1] ne "" && $ARGV[2] ne "") {
   if ($ARGV[3] ne "") {
       $port = $ARGV[3];
   }else {
       print "Script exit without running! missing snmp Port\n";
       exit;
   }
}else {
   $port = "161";

}
`mkdir "$BDNA_HOME/nih/SNMP/MIBs/Oracle"` unless (-d "$BDNA_HOME/nih/SNMP/MIBs/Oracle");
`cp * $BDNA_HOME/nih/SNMP/MIBs/Oracle`;
`export PATH=$PATH:/bin:/sbin:/usr/bin:/usr/sbin:/usr/local/bin:/usr/local/sbin`;
`rm -rf ~/markInfor_SunStoDev.tar_$ip` unless (-e "~/markInfor_SunStoDev.tar_$ip");
`ping $ip -c 6 > ~/markInfo_$ip 2> ~/markInfo_err_$ip`;
#`chkconfig --list | grep snmp >> ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;
`echo "\n\n====================Below is collected information for oid sunAkMIB in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "====================Below is error information for oid sunAkMIB in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;

`snmpwalk -m SUN-AK-MIB -M $BDNA_HOME/nih/SNMP/MIBs/Oracle -M +/usr/share/snmp/mibs -v 2c -Oq -c $community $ip:$port sunAkMIB >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid sunAkInfo in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "====================Below is error information for oid sunAkInfo in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;

`snmpwalk -m SUN-AK-MIB -M $BDNA_HOME/nih/SNMP/MIBs/Oracle -M +/usr/share/snmp/mibs -v 2c -Oq -c $community $ip:$port sunAkInfo >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid sunAkInfoType in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "====================Below is error information for oid sunAkInfoType in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;

`snmpwalk -m SUN-AK-MIB -M $BDNA_HOME/nih/SNMP/MIBs/Oracle -M +/usr/share/snmp/mibs -v 2c -Oq -c $community $ip:$port sunAkInfoType >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid "products" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "====================Below is error information for oid "products" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;

`snmpwalk -m SUN-SNMP -M $BDNA_HOME/nih/SNMP/MIBs/Oracle -M +/usr/share/snmp/mibs -v 2c -Oq -c $community $ip:$port products >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid "1.3.6.1.4.1.42.2" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "\n\n====================Below is error information for oid "1.3.6.1.4.1.42.2" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;
`snmpwalk -v 2c -Oq -c $community $ip:$port 1.3.6.1.4.1.42.2 >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid "sunStorageMIB" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "====================Below is error information for oid "sunStorageMIB" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;

`snmpwalk -m SUN-STORAGE-MIB -M $BDNA_HOME/nih/SNMP/MIBs/Oracle -M +/usr/share/snmp/mibs -v 2c -Oq -c $community $ip:$port sunStorageMIB  >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid "1.3.6.1.4.1.42.2.2.6.2" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "\n\n====================Below is error information for oid "1.3.6.1.4.1.42.2.2.6.2" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;
`snmpwalk -v 2c -Oq -c $community $ip:$port 1.3.6.1.4.1.42.2.2.6.2 >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information for oid "1.3.6.1.2.1.1" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_$ip`;
`echo "\n\n====================Below is error information for oid "1.3.6.1.2.1.1" in machine "$ip",port "$port",community "$community"====================\n" >> ~/markInfo_err_$ip`;
`snmpwalk -v 2c -Oq -c $community $ip:$port 1.3.6.1.2.1.1 >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`echo "\n\n====================Below is collected information snmpwalk -v 2c -c "$community" "$ip"====================\n" >> ~/markInfo_$ip`;
`echo "\n\n====================Below is error information for snmpwalk -v 2c -c "$community" "$ip"====================\n" >> ~/markInfo_err_$ip`;
`snmpwalk -v 2c -c $community $ip >>  ~/markInfo_$ip 2>> ~/markInfo_err_$ip`;

`cd ~; tar -cf markInfor_SunStoDev.tar_$ip markInfo_$ip markInfo_$ip  markInfo_err_$ip  markInfo_err_$ip`;
`rm -rf ~/markInfo_$ip ~/markInfo_$ip ~/markInfo_err_$ip  ~/markInfo_err_$ip`;
print "collect information finish!\n";
print "Result is copied to ~/markInfor_SunStoDev.tar_$ip\n";

