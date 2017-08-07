1. login Jenkins with "caceyli/Simple.0":
http://10.10.12.96:8080

2. Following vm must be used to stage applications : 
Host : vm396ub   (vm396ub.bdnacorp.com:10.10.12.96)
Login : appstaging – Simple.0 
Home directory (/home/appstaging) can be used to create the files required for staging. This will help us keep all the apps in a single place.

3. AWS:
Sign-in URL: https://bdna-aws-d2.signin.aws.amazon.com/console 
User name: caceyli
User password: 5{']|Qd3G7Xr

3. test the script with user (bdna/n1md@345) on 10.10.12.96.

4. cvs files in github d2:
agent/src/bdna-agent/config/signatureFileTable.csv
agent/src/bdna-agent/config/signatureProcessTable.csv
agent/mappingTables/APPIdCMDRFFTable_v2.csv

5. fp conversion:
https://bdnacorp.atlassian.net/browse/VO-327
https://bdnacorp.atlassian.net/browse/VO-342

6. cvs files:
G:\bdnagithub\learngit\unixj4fps

7. doc design
https://bdnacorp.atlassian.net/wiki/display/DP/Collection+Scripts+and+Agent+TDD
https://bdnacorp.atlassian.net/wiki/display/DP/Design

8. related info:
https://aws.amazon.com/ec2/systems-manager/
http://docs.aws.amazon.com/systems-manager/latest/userguide/create-ssm-doc.html
https://aws.amazon.com/ec2/systems-manager/run-command/
https://aws.amazon.com/s3/?sc_channel=PS&sc_campaign=acquisition_US&sc_publisher=google&sc_medium=s3_b&sc_content=s3_e&sc_detail=aws%20s3&sc_category=s3&sc_segment=192085379923&sc_matchtype=e&sc_country=US&s_kwcid=AL!4422!3!192085379923!e!!g!!aws%20s3&ef_id=WUIdegAAALFuMW9R:20170615053906:s
https://aws.amazon.com/athena/?sc_channel=PS&sc_campaign=acquisition_US&sc_publisher=google&sc_medium=athena_b&sc_content=athena_e&sc_detail=aws%20athena&sc_category=athena&sc_segment=192044121090&sc_matchtype=e&sc_country=us&s_kwcid=AL!4422!3!192044121090!e!!g!!aws%20athena&ef_id=WUIdegAAALFuMW9R:20170615053920:s
https://en.wikipedia.org/wiki/RPM_Package_Manager

9. https://github.com/bdna/D2/tree/Pioneer.0/Dev/agent/src/bdna-agent/config
10. https://github.com/bdna/D2/blob/Pioneer.0/Dev/agent/mappingTables/APPIdCMDRFFTable_v2.csv

Prashant list



Need agent field testing before PR can be approved.
Step for testing are the following:
 
0. create linux machine for your own testing...
 
1. get latest agent build from nas3
\\nas3\shared\product\nightly-builds\pioneer\1.0\qa\...
 
2. upload agent file to amazon instance under /var/lib/bdna
 
For example:
scp -i "n1md@345_californai.pem" <nas3-agent-build-files> ec2-user@ec2-54-241-168-95.us-west-1.compute.amazonaws.com:/var/lib/bdna
 
3. upload staged application to amazon VM from vm396ub
scp -i... similiar to above
 
 
4. encrypt the config csv using bdna-encrypt.exe
 
Windows version of bdna-encrypt.exe has been sent to your inbox.
Linux version of bdna-encrypt can be found under "util" folder under nas3
 
bdna-encrypt.exe –encrypt –inputFile=<test.csv> –outputFile=encrypted.txt
 
4.1. copy the encrypted csv to amazon VM under /var/lib/bdna/config
 
5 login to the target amazon VM
 
right mouse click on the amazon console, choose "connect"
 
For example:
ssh -i "n1md@345_californai.pem" ec2-user@ec2-54-241-168-95.us-west-1.compute.amazonaws.com
 
6. sudo as root
>sudo su
 
6. run the agent and do field testing, using this command
/var/lib/bdna/bdna-agent
 
7. actual scan result can be found under
/var/lib/amazon/ssm/[instance id]/inventory/custom
 
instance id can be different from different machine
 
/var/lib/amazon/ssm/i-05d111884e7e6410a/inventory/custom
 
8. you can also check agent log
/var/log/bdna/bdna-agent.log
