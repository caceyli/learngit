#!/usr/bin/perl

# Author: Errol Neal
# Date: 2010-07-02
#
# Modified 2013-02-26
## Adjust for static directory so we don't have to reapply permissions after seq update

use strict;
use File::Basename;

my $nmap_binary;
my $nmap_args;
my $nmap_command;
my $nmap_dir;
my $nmap_partial_dir;
my $nmap_static_dir;
my $bdna_home = $ENV{BDNA_HOME};
my $nmap_static_dir_root = $ENV{HOME} . "/static_nmap";

if (scalar(@ARGV) == 0) {
print "No arguments supplied... Exiting!\n";
exit;
};

if ($ARGV[0] !~ m/(nmap[.]Linux[.]RHEL5[.]x86_64|nmap[.]Linux||nmap[.]Linux[.]RHEL5||nmap[.][Linux[.]RHEL4)/) {
print "This wrapper is designed to only work for BDNA Discover to call the nmap binary. Please use the full path to the real sudo command\n";
exit;
}

$nmap_binary = @ARGV[0];

$nmap_dir = dirname($nmap_binary);

if (join(" ", @ARGV) =~ m/^$nmap_binary\s(.+)$/) {
$nmap_args = $1;
};

if ($nmap_dir =~ m/$bdna_home\/nih\/(.+)$/) {
$nmap_partial_dir = $1;
};

$nmap_static_dir = "$nmap_static_dir_root/$nmap_partial_dir";

$nmap_binary = basename($nmap_binary);
$nmap_binary = "$nmap_static_dir/$nmap_binary";

$nmap_command = "$nmap_binary --privileged $nmap_args";
chdir($nmap_static_dir) or die "$!";
exec($nmap_command);
