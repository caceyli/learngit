#!/bin/bash

foldersInModuleApp="Actuate Adobe Apache Citrix Veritas VMWare WyseTechnology"
filesInModuleApp="EMCSymAPI.xml UNIXOracleIAS.xml TelelogicDoors.xml TelelogicTau.xml Telelogic.xml"
filesInModuleAppIBM="Extend_UNIXWebSphereMQ_Server.xml Extend_WebSphereMQ_Server.xml Extend_WindowsWebSphereMQ_Server.xml Rational/ClearDDTS.xml Rational/PureCoverage.xml Rational/Purify.xml Rational/Quantify.xml Rational/Rose.xml"
foldersInPreModApp="Adobe Apache Ariba BMC Citrix JBoss TrendMicro"
filesInPreModApp="OracleIAS.xml Veritas_Install_History_UI.xml Veritas_License_UI.xml TrendMicro/OfficeScan_Client_UI.xml IBM/Extend_WebSphereMQ_Server.xml"

currentDir=`pwd`

cd $currentDir/modules/com/bdna/modules/app/
for i in $foldersInModuleApp;do
rm -rf $i;
echo "$i deleted."
done

for i in $filesInModuleApp;do
rm -rf $i;
echo "$i deleted."
done

cd $currentDir/modules/com/bdna/modules/app/IBM
for i in $filesInModuleAppIBM;do
rm -rf $i;
echo "$i deleted."
done

cd $currentDir/modules/com/bdna/presentationModules/app
for i in $foldersInPreModApp;do
rm -rf $i;
echo "$i deleted."
done

for i in $filesInPreModApp;do
rm -rf $i;
echo "$i deleted."
done

