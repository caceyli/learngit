--
-- Purpose: This script sets up Oracle tablespaces with medium-size deployment.  
--          This script needs to be run after extracting the BDNA Oracle10g tarball archive.
--
-- Usage: From SQL*Plus, run as system
--
-- Note: Requires minimum 152GB of free space


--
-- Increase TEMP tablespace size
--
ALTER DATABASE TEMPFILE '/u01/oradata/ora11g/temp01.dbf' AUTOEXTEND OFF;
ALTER DATABASE TEMPFILE '/u01/oradata/ora11g/temp01.dbf' RESIZE 8192M;
ALTER TABLESPACE "TEMP" ADD TEMPFILE '/u01/oradata/ora11g/temp02.dbf' SIZE 8192M;

--
-- Create BDNATEMP tablespace
--
CREATE SMALLFILE TABLESPACE "BDNATEMP" NOLOGGING DATAFILE '/u01/oradata/ora11g/BDNATEMP.dbf' SIZE 4096M EXTENT MANAGEMENT LOCAL UNIFORM SIZE 256K SEGMENT SPACE MANAGEMENT AUTO;
ALTER TABLESPACE "BDNATEMP" ADD DATAFILE '/u01/oradata/ora11g/BDNATEMP02.dbf' SIZE 4096M;


--
-- Increase UNDOTBS tablespace size
--
ALTER DATABASE DATAFILE '/u01/oradata/ora11g/undotbs01.dbf' AUTOEXTEND OFF;
ALTER DATABASE DATAFILE '/u01/oradata/ora11g/undotbs01.dbf' RESIZE 8192M;
ALTER TABLESPACE "UNDOTBS1" ADD DATAFILE '/u01/oradata/ora11g/undotbs02.dbf' SIZE 8192M;


--
-- Increase SYSTEM tablespace size
--
ALTER DATABASE DATAFILE '/u01/oradata/ora11g/system01.dbf' RESIZE 4096M;

--
-- Increase SYSAUX tablespace size
--
ALTER DATABASE DATAFILE '/u01/oradata/ora11g/sysaux01.dbf' RESIZE 4096M;


--
-- Create USERS tablespace
--
CREATE SMALLFILE TABLESPACE "USERS" LOGGING DATAFILE '/u01/oradata/ora11g/users01.dbf' SIZE 10240M EXTENT MANAGEMENT LOCAL UNIFORM SIZE 256K SEGMENT SPACE MANAGEMENT AUTO;
ALTER DATABASE DEFAULT TABLESPACE USERS;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users02.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users03.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users04.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users05.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users06.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users07.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users08.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users09.dbf' SIZE 10240M;
ALTER TABLESPACE "USERS" ADD DATAFILE '/u01/oradata/ora11g/users10.dbf' SIZE 10240M;

exit

