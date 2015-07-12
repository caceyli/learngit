#!/usr/bin/perl

# Provide a transform script to transform some C# scripts for Click-to-scan usage. 
# For example, transform WinStaticScript.cs to WinCTS.cs. 
# This script will be run at build time to generate C# scripts for Click-to-scan.

# The mapping of source files and target files.
my %filesToTransform = ("WinStaticScript.cs" => "../WinCTS/WinCTSProj/CTSWinStaticScript.cs",
                        "WinAppStaticScript.cs" => "../WinCTS/WinCTSProj/CTSWinAppStaticScript.cs",
                        "InternetExplorerStaticScript.cs" => "../WinCTS/WinCTSProj/CTSInternetExplorerStaticScript.cs",
                        "MSOutlookExpressStaticScript.cs" => "../WinCTS/WinCTSProj/CTSMSOutlookExpressStaticScript.cs",
                        "MSOfficeStaticScript.cs" => "../WinCTS/WinCTSProj/CTSMSOfficeStaticScript.cs", 
                        "ODBCDriversCollectionScript.cs" => "../WinCTS/WinCTSProj/CTSODBCDriversCollectionScript.cs"); 

# The transformation rules in replacement expression;
my @transformRules = ('s/\bnew\s+ManagementClass\b/new CTSManagementClass/g',
                      's/\bManagementClass\b/ICTSManagementClass/g',
                      's/\bManagementScope\b/ICTSManagementScope/g',
                      's/\bnew ManagementObject\b/new CTSManagementObject/g',
                      's/\bManagementObject\b/ICTSManagementObject/g',
                      's/\bManagementBaseObject\b/CTSManagementBaseObject/g',
                      's/\bManagementObjectSearcher\b/CTSManagementObjectSearcher/g',
                      's/\bManagementObjectCollection\b/ICTSManagementObjectCollection/g',
                      's/\bPropertyData\b/ICTSPropertyData/g',
                      's/\bPropertyDataCollection\b/ICTSPropertyDataCollection/g',
                      's/\bnew\s+EnumerationOptions\b/new CTSEnumerationOptions/g',
                      's/\bEnumerationOptions\b/ICTSEnumerationOptions/g',
                      's/\bnamespace\s+bdna\.Scripts\b/namespace WinCTSProj/',
                      's/\bLib.GetRegistryDWord\b/CTSTransformHelper.GetRegistryDWord/g',
                      's/\bLib.GetRegistryExpandedStringValue\b/CTSTransformHelper.GetRegistryExpandedStringValue/g',
                      's/\bLib.GetRegistryImmediateSubKeys\b/CTSTransformHelper.GetRegistryImmediateSubKeys/g',
                      's/\bLib.GetRegistryStringArrayValue\b/CTSTransformHelper.GetRegistryStringArrayValue/g',
                      's/\bLib.GetRegistryStringValue\b/CTSTransformHelper.GetRegistryStringValue/g',
                      's/\bLib.GetRegistrySubkeyName\b/CTSTransformHelper.GetRegistrySubkeyName/g',
                      's/\bLib.GetRegistryBinaryValue\b/CTSTransformHelper.GetRegistryBinaryValue/g',
                      's/\bLib.GetRegistryDWordStringValue\b/CTSTransformHelper.GetRegistryDWordStringValue/g',
                      's/\bLib.ValidateFile\b/CTSTransformHelper.ValidateFile/g',
                      's/\bLib.ValidateDirectory\b/CTSTransformHelper.ValidateDirectory/g',
                      's/\bLib.RetrieveFileListings\b/CTSTransformHelper.RetrieveFileListings/g',
                      's/\bLib.RetrieveFileProperties\b/CTSTransformHelper.RetrieveFileProperties/g',
                      's/\bLib.RetrieveSubDirectories\b/CTSTransformHelper.RetrieveSubDirectories/g',
                      's/\bLib.ExecuteWqlSelectQuery\b/CTSTransformHelper.ExecuteWqlSelectQuery/g',
                      's/\bLib.ExecuteWqlQuery\b/CTSTransformHelper.ExecuteWqlQuery/g',
                      's/\bLib.InvokeRegistryMethod\b/CTSTransformHelper.InvokeRegistryMethod/g',
                      's/\bLib.GetRegistryImmediateKeyValues\b/CTSTransformHelper.GetRegistryImmediateKeyValues/g',
                      's/software\\\\Microsoft\\\\IE Setup\\\\Setup/SOFTWARE\\\\Microsoft\\\\IE Setup\\\\Setup/g',
                      's/software\\\\Microsoft\\\\IE4\\\\Setup/SOFTWARE\\\\Microsoft\\\\IE4\\\\Setup/g',
                      's/software\\\\Microsoft\\\\Internet Explorer/SOFTWARE\\\\Microsoft\\\\Internet Explorer/g',
                      's/IEXPLORE\.EXE/iexplore\.exe/g',
                      's/software\\\\Microsoft\\\\Outlook Express/SOFTWARE\\\\Microsoft\\\\Outlook Express/g',
                      's/software\\\\Microsoft\\\\Windows Mail/SOFTWARE\\\\Microsoft\\\\Windows Mail/g',
                      's/\bLib.ExtractLicenseKeyFromMSDigitalProductID\b/CTSTransformHelper.ExtractLicenseKeyFromMSDigitalProductID/g',
                      's/\bMSOfficeStaticScript\b/CTSMSOfficeStaticScript/g',
                      's/software\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall/SOFTWARE\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall/g',
                      's/software\\\\Wow6432Node\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall/SOFTWARE\\\\Wow6432Node\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Uninstall/g',
                      's/\bLib.DeleteDirectory\b/\/\/Lib.DeleteDirectory/');

# Process each file transformation
foreach my $sourceFile (keys (%filesToTransform)) {
    my $targetFile = $filesToTransform{$sourceFile};
    
    # Get source class name from source file.
    $sourceFile =~ /\b(\w+)\.cs/;
    my $sourceClass = $1;
    
    # Get target class name from target file.
    $targetFile =~ /\b(\w+)\.cs/;
    my $targetClass = $1;
    
    # Add a rule to change class name and constructor into @transformRules
    push(@transformRules, 's/public\s+class\s+'.$sourceClass.'\b/public class '.$targetClass.'/');
    push(@transformRules, 's/\bstatic '.$sourceClass.'\b()/static '.$targetClass.'/');
    
    # Do file transformation
    &transformFile($sourceFile, $targetFile);
    
    # Remove the added class name and constructor transformation rule for this file
    pop(@transformRules);
    pop(@transformRules);
}

#*************************Definition of functions *******************************

# Transform a file
# @param sourceFile, the path of source file
# @param targetFile, the path of target file
sub transformFile {
    my ($sourceFile, $targetFile) = @_;
    open (INFILE, $sourceFile) || die ("Could not open file $sourceFile for read: $!");
    open (OUTFILE, "> $targetFile") || die ("Could not open file $targetFile for write: $!");
    
    print("Transforming $sourceFile to $targetFile ...\n");
    
    my $line = <INFILE>;
    
    while ($line) {
        # Transform the line
        my $transLine = &transFormLine($line);
        
        print OUTFILE $transLine;
            
        $line = <INFILE>;
    }
    
    close(INFILE);
    close(OUTFILE);
}

# Transform a line
# @param line, the line to transform
sub transFormLine {
    my ($line) = @_;
    
    # Apply each transformation rule on $line
    foreach my $replaceExp (@transformRules) {
        my $exp = '$line =~ '.$replaceExp;
        eval($exp);
    }

    return $line;
}
