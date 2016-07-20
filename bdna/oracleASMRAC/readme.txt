CLUSTERWARE + ASM + RAC introduce
http://docs.oracle.com/database/121/TDPRC/intro_tdprc.htm#TDPRC116

GRID (CLUSTERWARE + ASM) + RAC 安装
http://blog.chinaunix.net/xmlrpc.php?r=blog/article&id=4681351&uid=29655480

管理oracle 11g RAC 常用命令
http://www.cnblogs.com/top5/archive/2012/11/15/2771312.html

sqlplus remote connection with service name:
sqlplus system/bdnacn@//192.168.11.101:1522/ora12c
sqlplus system/bdnacn@192.168.11.101:1522/ora12c

SQL> select instance_name,status from v$instance;

Customer environment, probably this situation:
http://www.dba-oracle.com/security/removing_permissions.htm
chmod o-x $ORACLE_HOME/bin/oracle

CHINA RAC environment： (oracle db and grid installation are separately installed by oracle user and grid user.)
*******************
node1 
eth0   192.168.11.101  (host ip)
eth0:1 192.168.11.103  (vip for node1)

node2 
eth0   192.168.11.102  (host ip)
eth0:1 192.168.11.104  (vip1 for node2)
eth0:2 192.168.11.105  （vip2)

dbname/service_name: ora12c
sid: node1-ora12c1, node2-ora12c2
port: 1522
********************

Standlone environment
*********************
host ip: 192.168.9.105  
sid: orcl01
port: 1521
*********************

For both environment
*********************
Level 2 credential: vzpbdna/bdnacn
Level 3 credential: bdnadisc/bdnacn
*********************


Eerrol's RAC environment: (both oracle db and grid installation are installed by oracle user.)
*******************************
rac-node1:
eth0    192.168.100.240 (host ip)
eth0:2  192.168.100.245 (vip)
eth0:3  192.168.100.246 (vip)
eth0:5  192.168.100.242 (vip for rac-node1)

rac-node2:
eth0    192.168.100.241 (host ip)
eth0:1  192.168.100.243 (vip for rac-node2)
eth0:2  192.168.100.244 (vip)

dbname/service_name: BDNA
sid: rac-node1 BDNA1, rac-node2 BDNA2
port: 2500
********************************

For this environment
*********************
Level 2 credential: vzpbdna/bdna
Level 3 credential: BDNADISC/bdnacn
*********************
