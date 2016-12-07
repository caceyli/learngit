BDNA-1461: [JPMC] Deep fingerprint(s) for PowerCenter to identify the following options

http://www.gerardnico.com/doc/powercenter/PC_861_LicenseNotice.pdf
http://gerardnico.com/doc/powercenter/PC_861_AdministratorGuide.pdf  (page 271)

C:\Informatica\9.5.1\isp\bin>infacmd.bat ShowLicense -dn Domain_win03-8144 -un administrator -pd bdna -ln 951_License_win03-8144_48541
Edition:           Informatica Standard
Software Version:  9.0.1
Distributed by:    INFORMATICA
Issued on:         2011-Apr-20
Validity period:   Non-Expiry
Serial number:     48541
Deployment level:  Production

List of supported platforms are:
   [All operating systems] is authorized for [14] logical CPUs
Number of authorized repository instances: 255
Number of authorized CAL usage count: 0

List of PowerCenter options are:
   Valid [Data Analyzer]
   Valid [Mapping Generation]
   Valid [OS Profiles]
   Valid [Team Based Development]
List of connections are:
   Valid [DB2]
   Valid [Informix]
   Valid [Microsoft SQL Server]
   Valid [ODBC]
   Valid [Oracle]
   Valid [Teradata]
   Valid [PowerExchange for IBM MQ Series]
   Valid [PowerExchange for Oracle E-Business Suite]
   Valid [PowerExchange for PeopleSoft]
   Valid [PowerExchange for SAP NetWeaver - BW]
   Valid [PowerExchange for SAP NetWeaver - BW (Real-Time)]
   Valid [PowerExchange for SAP NetWeaver - mySAP]
   Valid [PowerExchange for SAP NetWeaver - mySAP (Real-Time)]
   Valid [PowerExchange for Siebel]


C:\Informatica\9.5.1\isp\bin>infacmd.bat listLicenses -dn Domain_win03-8144 -un administrator -pd bdna
951_License_win03-8144_48541 (48541)
command ran successfully.


C:\Informatica\9.5.1\isp\config\nodemeta.xml

<?xml version="1.0" encoding="UTF-8"?>
<imx:IMX xmlns:imx="http://com.informatica.imx" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" serializationSpecVersion="5.0" crcEnabled="0" xmlns:domainservice="http://com.informatica.isp.metadata.domainservice/2" versiondomainservice="2.6.1" xmlns:common="http://com.informatica.isp.metadata.common/2" versioncommon="2.2.0" xsi:schemaLocation="http://com.informatica.imx IMX.xsd http://com.informatica.isp.metadata.domainservice/2 com.informatica.isp.metadata.domainservice.xsd http://com.informatica.isp.metadata.common/2 com.informatica.isp.metadata.common.xsd">
<domainservice:GatewayNodeConfig imx:id="U:JqRSISeHEeanMMQjISnJ4Q" adminconsolePort="6008" adminconsoleShutdownPort="6009" domainName="Domain_win03-8144" nodeName="node01_win03-8144" dbConnectivity="ID_1">
<address imx:id="ID_2" xsi:type="common:NodeAddress" host="win03-8144" httpPort="6005" port="6006"/>
<httpsInfo imx:id="ID_3" xsi:type="domainservice:HttpsInfo" encryptedKeystorePass="d89Efzmt1m9J5wJGOOCGgA%3D%3D" httpsPort="8443" keystoreFile="C:%5CInformatica%5C9.5.1%5Ctomcat%5Cconf%5CDefault.keystore"/>
<portals>
<NodeRef imx:id="ID_4" xsi:type="common:NodeRef" address="ID_2" nodeName="node01_win03-8144"/>
</portals>
<configSettings>
<OptionGroup imx:id="U:JqRSJCeHEeanMMQjISnJ4Q" xsi:type="common:OptionGroup" name="EMRSConfigSettings"/>
</configSettings>
</domainservice:GatewayNodeConfig>
<domainservice:DBConnectivity imx:id="ID_1" dbConnectString="jdbc:informatica:oracle:%2F%2F192.168.8.144:1521%3BServiceName%3Dorcl%3BMaxPooledStatements%3D20%3BCatalogOptions%3D0%3BBatchPerformanceWorkaround%3Dtrue" dbEncryptedPassword="D%2B%2B1c2bQMXkKWBTtWmnGtkb%2Bp6ITrBt%2BFi0GwaE7BHg%3D" dbType="ORACLE" dbUsername="DomainManager"/>
</imx:IMX>
