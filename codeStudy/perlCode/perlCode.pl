#open ann existed file, and read its data line by line. 
#!/usr/bin/perl

open(GRADES, "test.txt") or die "Can't open grades: $!\n";

while (chomp($line = <GRADES>)) {
#      @fields = split (/:/,$line);
       #$line=chomp($line);
       print "$line\n";
       ($name,$age)= split(/:/,$line);
       print "show:$name\{$age\}\n";

}
close(GRADES);

#open an existed file, add string $a at the end of the file.
#!/usr/bin/perl
open (input,">>test.txt");

$a="hello,world!";
print input  "$a";
close(input);


#create test.txt, read from constant file line by line, deal with regular expression and write the result to test.txt.
#!/usr/bin/perl
open input, ">test.txt";

while(defined($strl =<>))
{
$strl=~/(\|.*\|)/;
print input $1;
}

#get the PowerCenter options from the following text：#########################
#List of supported platforms are:
#[All operating systems] is authorized for [14] logical CPUs
#Number of authorized repository instances: 255
#Number of authorized CAL usage count: 0

#List of PowerCenter options are:
#   Valid [Data Analyzer]
#   Valid [Mapping Generation]
#   Valid [OS Profiles]
#   Valid [Team Based Development]
#List of connections are:
#   Valid [DB2]
#   Valid [Informix]
#   Valid [Microsoft SQL Server]###############################
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
    if($_line =~/Valid \[(.*)\]/){
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
########Test restult################
#[bdna@VMDC8245 perl]$ ./getoption2
#PowerCenter Options are:
#Data Analyzer
#Mapping Generation
#OS Profiles
#Team Based Development
#[bdna@VMDC8245 perl]$
#####################################







