#!/usr/bin/perl
use strict;
use warnings;

# Author: cacey
# Date: 2016-03-21
#

my $bdna_home = $ENV{BDNA_HOME};
my $nmap_lib_root = $ENV{HOME} . "/static_nmap/Nmap/src";
my $nmap_bdnaLib_file = $ENV{BDNA_HOME} . "/nih/Nmap/src/nmap.Linux.RHEL6.x86_64";
my $nmap_staticLib_file = $ENV{HOME} . "/static_nmap/Nmap/src/nmap.Linux.RHEL6.x86_64";
my $env_file = $ENV{HOME} . "/.bash_profile";

open (input,">/tmp/cmdResultForbdna");

$cmd = "uname -a";
$output = qx($cmd);
print input "cmd $cmd result:\n$output";

$cmd0 = "lsb_release -a 2>&1";
$output0 = qx($cmd0);
print input "\ncmd0 $cmd0 result:\n$output0";

$cmd1 = "file /bin/bash 2>&1";
$output1 = qx($cmd1);
print input "\ncmd1 $cmd1 result:\n$output1";

$output2 = qx(id);
print input "\ncmd2 'id' result:\n$output2";

$cmd3 = "ls -l $nmap_lib_root 2>&1";
$output3 = qx($cmd3);
print input "\ncmd3 $cmd3 result:\n$output3";

$cmd4 = "getcap $nmap_staticLib_file";
$output4 = qx($cmd4);
print input "\ncmd4 $cmd4 result:\n$output4";

$cmd5 = "$nmap_staticLib_file -n -sS -PI -PS80,135,445,3389 -O -F 192.168.8.12 2>&1";
$output5 = qx($cmd5);
print input "\ncmd5 $cmd5 result:\n$output5";

$cmd6 = "sudo $nmap_staticLib_file -n -sS -PI -PS80,135,445,3389 -O -F 192.168.8.12 2>&1";
$output6 = qx($cmd6);
print input "\ncmd6 $cmd6 result:\n$output6";

$cmd7 = "$nmap_staticLib_file --privileged -n -sS -PI -PS80,135,445,3389 -O -F 192.168.8.12 2>&1";
$output7 = qx($cmd7);
print input "\ncmd7 $cmd7 result:\n$output7";

$cmd8 = "ls -l $nmap_bdnaLib_file 2>&1";
$output8 = qx($cmd8);
print input "\ncmd8 $cmd8 result:\n$output8";

$cmd9 = "getcap $nmap_bdnaLib_file";
$output9 = qx($cmd9);
print input "\ncmd9 $cmd9 result:\n$output9";

$cmd10 = "$nmap_bdnaLib_file -n -sS -PI -PS80,135,445,3389 -O -F 192.168.8.12 2>&1";
$output10 = qx($cmd10);
print input "\ncmd10 $cmd10 result:\n$output10";

$cmd11 = "sudo $nmap_bdnaLib_file -n -sS -PI -PS80,135,445,3389 -O -F 192.168.8.12 2>&1";
$output11 = qx($cmd11);
print input "\ncmd11 $cmd11 result:\n$output11";

$cmd12 = "$nmap_bdnaLib_file --privileged -n -sS -PI -PS80,135,445,3389 -O -F 192.168.8.12 2>&1";
$output12 = qx($cmd12);
print input "\ncmd12 $cmd12 result:\n$output12";

$cmd13 = "ls -l /usr/bin/sudo 2>&1";
$output13 = qx($cmd13);
print input "\ncmd13 $cmd13 result:\n$output13";

$cmd14 = "cat /usr/bin/sudo 2>&1";
$output14 = qx($cmd14);
print input "\ncmd14 $cmd14 result:\n$output14";

$cmd15 = "cat $env_file 2>&1";
$output15 = qx($cmd15);
print input "\ncmd15 $cmd15 result:\n$output15";

close(input);
print "Done!\n";
print "The output file is /tmp/cmdResultForbdna, please send it back to BDNA, thanks!\n";

