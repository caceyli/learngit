#!/usr/bin/perl
use strict;
use warnings;

open(GRADES, "/home/bdna/cacey/perl/optionlist") or die "Can't open grades: $!\n";

my @_licenseInfo =  <GRADES>;
my $_optionFind = 0;
my $_pcOptions="";
foreach my $_line (@_licenseInfo) {
if($_line =~/List of PowerCenter options are/){
 $_optionFind = 1;
 next;
}
if($_optionFind==1){
 if( $_line =~/Valid \[(.*)\]/){
 if($_pcOptions){
     $_pcOptions.="\n".$1;
 }
 else {
     $_pcOptions=$1;
 }
}
else {
 last;
}
}
}
if($_pcOptions){
print "PowerCenter Options are:"."\n".$_pcOptions."\n";
}
close(GRADES);

