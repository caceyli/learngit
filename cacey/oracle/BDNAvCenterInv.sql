declare lmsinvTb int;
lmsvmTb int;
begin
    select count(*) into lmsinvTb from user_tables where table_name='LMS_VCENTERVMS';
    if lmsinvTb >0 then
       execute immediate 'drop table LMS_vCenterVMS';
    end if;

        select count(*) into lmsinvTb from user_tables where table_name='LMS_VCENTERINV';
    if lmsinvTb >0 then
       execute immediate 'drop table LMS_vCenterInv';
    end if;

end;
/

create table TMP_vCenterHost (VCenterServerName varchar(100),HostName varchar(100),VendorTMP varchar(100),ModelTMP varchar(100),FullNameTMP varchar(100),VersionTMP varchar(100),CpuModelTMP varchar(100),CpuMhzTMP varchar(100), CPUTMP varchar(100), CpuCoresTMP varchar(100),CpuThreadsTMP varchar(100),PowerStateTMP varchar(100));
create table TMP_HostDcCluster (HostNameTMP varchar(50), DatacenterTMP varchar(50),ClusterTMP varchar(50), IsStandAloneTMP varchar(50));  
create table TMP_HostStore (VCenterServerName varchar(100),HostNameTMP varchar(50), DatastoreTMP varchar(100));
create table TMP_HostStores (HostNameTMP varchar(50), DatastoresTMP varchar(100));
create table TMP_HostVM (VCenterServerName varchar(100),HostNameTMP varchar(50), VMName varchar(1024));
create table TMP_HostVMs (HostNameTMP varchar(100), VMsTMP varchar(2024));
CREATE table LMS_vCenterInv (VCenterServerName varchar(100), VCenterVersion varchar(100), HostName varchar(100), IsStandAlone varchar(100), Datacenter varchar(100), vCluster varchar(100), VMs varchar(1024), Vendor varchar(100), Model varchar(100), FullName varchar(100), Version varchar(100), CpuModel varchar(100),CpuMhz varchar(100),CPU varchar(100), CpuCores varchar(100), CpuThreads varchar(100),PowerState varchar(100),Datastores varchar(100));
create table LMS_vCenterVMS(VMName varchar(100), VMGuestOS varchar(100), VMGuestHostName varchar(100), VMIPAddress varchar(100), HostName varchar(100), VCenterServerName varchar(100));

Declare cursor vcinstance is select distinct owner from dba_tab_columns WHERE TABLE_NAME='VPX_PARAMETER';
username varchar (30);
VCenterServerName varchar (100);
VCenterServerVerID varchar(30);
formatedVERID varchar (30);
VPXV_HOSTS varchar(30);
VPXV_HOST_DATASTORE varchar(30);
VPXV_DATASTORE varchar(30);
VPXV_VMS varchar(30);
VPX_PARAMETER VARCHAR(30);
VPX_VERSION VARCHAR(30);
vpxv_entity VARCHAR(30);
vmHostName varchar(300);
i int;  
verIDLen int;

begin
open vcinstance;
LOOP
    fetch vcinstance into username;
    exit when vcinstance%notfound;
    dbms_output.put_line('wellcom');
    VPXV_HOSTS := username||'.'||'VPXV_HOSTS';
    VPXV_HOST_DATASTORE := username||'.'||'VPXV_HOST_DATASTORE';
    VPXV_DATASTORE := username||'.'||'VPXV_DATASTORE';
    VPXV_VMS := username||'.'||'VPXV_VMS';
    VPX_PARAMETER := username||'.'||'VPX_PARAMETER';
    VPX_VERSION := username||'.'||'VPX_VERSION';
    vpxv_entity := username||'.'||'vpxv_entity';


    execute immediate 'select VALUE  FROM '||VPX_PARAMETER||' where NAME = '''||'VirtualCenter.InstanceName'||'''' into VCenterServerName;
    execute immediate 'SELECT VER_ID FROM '||VPX_VERSION||'' into VCenterServerVerID;
    
    dbms_output.put_line(VCenterServerVerID);
    verIDLen := length(VCenterServerVerID);
    i :=1;
    formatedVERID := substr(VCenterServerVerID,i,1);

    Loop
        i :=i+1;
        EXIT WHEN i>verIDLen;
        formatedVERID := formatedVERID||'.'||SUBStr(VCenterServerVerID,i,1);
    end Loop;

    execute immediate 'insert into TMP_HostVM
    select 
    '''||VCenterServerName||''' as VCenterServerName,
    VPH.NAME AS HOST_NAME,
    VPM.NAME as VMName
    FROM '||VPXV_HOSTS||' VPH
    INNER JOIN '||VPXV_VMS||' VPM 
    ON VPH.HOSTID= VPM.HOSTID';  
    
    execute immediate 'insert into TMP_HostStore
    SELECT
    '''||VCenterServerName||''' as VCenterServerName,
    VPH.NAME AS HOST_NAME
    , VDS.NAME AS DS_NAME
    FROM
    '||VPXV_HOST_DATASTORE||' VHD INNER JOIN '||VPXV_DATASTORE||' VDS ON VDS.ID = VHD.DS_ID
    INNER JOIN '||VPXV_HOSTS||' VPH ON VPH.HOSTID= VHD.HOST_ID
     ';
     
    execute immediate 'insert into TMP_vCenterHost
    select
    '''||VCenterServerName||''' as VCenterServerName
    , NAME AS HostNameTMP
    , HOST_VENDOR AS VendorTMP
    , HOST_MODEL AS ModelTMP
    , PRODUCT_NAME AS FullNameTMP
    , PRODUCT_VERSION AS VersionTMP
    , CPU_MODEL  AS CpuModelTMP
    , cast(cast(CPU_HZ as numeric)/1000/1000 as numeric) AS CpuMhzTMP
    , CPU_COUNT  AS CPUTMP
    , CPU_CORE_COUNT  AS CpuCoresTMP
    , CPU_THREAD_COUNT AS CpuThreadsTMP
    , POWER_STATE AS PowerStateTMP
    from '||VPXV_HOSTS||' ';
   
    declare cursor hostSt is select distinct HostNameTMP from TMP_HostStore;
    stores varchar(1024);
    stHostName varchar2(1024);
    stCount int;
    Begin 
    open hostSt;
    Loop
        fetch  hostSt into stHostName;
        EXIT WHEN hostSt%notfound;
            declare cursor STORE is select distinct DatastoreTMP from TMP_HostStore where HostNameTMP= stHostName;
            stores varchar(1024);
            stcount NUMBER(20);
            stname varchar(300);
            begin
            stcount:=0;
            open STORE;
            loop
                fetch STORE INTO stname;
                exit when STORE%notfound;
                stcount:=stcount+1;
                if stcount=1 then
                stores :=stname;
                else 
                stores := stores ||';' || stname;
                end if;
            end loop;
            close STORE;    
            insert into TMP_HostStores values(stHostName,stores);
            end;  
    end loop;
    close hostSt;
    end;
    
   Declare cursor hostVM is select distinct HostName from TMP_vCenterHost;
    VMs varchar(300);  
    verIDLen int;    
    vmnumber int;

    Begin 

    open hostVM;
    Loop
        VMs:='';
        fetch  hostVM into vmHostName;
        EXIT WHEN hostVM%notfound;
        select count(*) into vmnumber from TMP_HostVM where HOSTNAMETMP= vmHostName;
      
        if vmnumber=0 then
            insert into TMP_HostVMs values(vmHostName,'NULL');
        else
            declare cursor VM is select distinct VMName from TMP_HostVM where HOSTNAMETMP= vmHostName ;
            vms varchar(1024);
            vmcount NUMBER(20);
            vmName varchar(300);
            begin
            vmcount:=0;
            open VM;
            loop
                fetch VM INTO vmName;
                exit when VM%notfound;
                vmcount:=vmcount+1;
                if vmcount=1 then
                    vms :=vmName;
                else 
                    vms := vms ||';' || vmName;
                end if;
            end loop;
            close VM;
            insert into TMP_HostVMs values(vmHostName,VMs);    
            end;
        end if;    
    end loop;
    close hostVM;
    end;
    
  
    declare cursor hostDcCluster is select distinct HOSTNAME from TMP_vCenterHost;
    parentid varchar(100);
    parentype varchar(100);
    dcHostName varchar(100);
    dcName varchar(100);
    isStandlone varchar (8);
    clusterName  varchar(100);
    Begin 

    open hostDcCluster;
    Loop
        isStandlone := 'TRUE';
	clusterName := 'NULL';
        fetch  hostDcCluster into dcHostName;
        EXIT WHEN hostDcCluster%notfound;
        execute immediate 'select PARENT_ID  FROM '||vpxv_entity||' where name = '''||dcHostName||''' AND ENTITY_TYPE = '''||'HOST'||'''' into parentid;
	execute immediate 'select distinct PARENT_ENTITY_TYPE from '||vpxv_entity||' WHERE PARENT_ID = '''||parentid||'''' into parentype;
        dbms_output.put_line(parentype);
        if (parentype = 'CLUSTER_COMPUTE_RESOURCE') then
            isStandlone := 'FALSE';           
            execute immediate 'select name from '||vpxv_entity||' where id = '''||parentid||'''' into clusterName;
        end if;
        loop  
            EXIT WHEN parentype = 'DATACENTER';
            
            execute immediate 'select PARENT_ID from '||vpxv_entity||' where id = '''||parentid||'''' into parentid;
            execute immediate 'select distinct PARENT_ENTITY_TYPE from '||vpxv_entity||' WHERE PARENT_ID = '''||parentid||'''' into parentype;
            dbms_output.put_line(parentype);
            if (parentype = 'CLUSTER_COMPUTE_RESOURCE') then
                isStandlone := 'FALSE';
            
            execute immediate 'select name from '||vpxv_entity||' where id = '''||parentid||'''' into clusterName;
        end if;
        
    end loop;
   
    execute immediate 'select name from '||vpxv_entity||' where id = '''||parentid||'''' into dcName;

    -- SELECT NAME into dcName FROM vpxv_entity WHERE ID IN(select DATACENTER_ID FROM vpxv_hosts WHERE NAME=dcHostName);
    insert into TMP_HostDcCluster values(dcHostName,dcName,clusterName,isStandlone);
    end loop;
    close hostDcCluster;
    END;
    
    execute immediate 'INSERT INTO LMS_vCenterInv
    select 
    '''||VCenterServerName||''' as VCenterServerName
    , '''||formatedVERID||''' as VCenterVersion
    , aa.HostName as HostName
    , bb.IsStandAloneTMP as IsStandAlone
    , bb.DatacenterTMP as Datacenter
    , bb.ClusterTMP as vCluster
    , dd.VMsTMP as VMs
    , aa.VendorTMP as Vendor
    , aa.ModelTMP as Model
    , aa.FullNameTMP as FullName
    , aa.VersionTMP as Version
    , aa.CpuModelTMP as CpuModel
    , aa.CpuMhzTMP as CpuMhz
    , aa.CPUTMP as CPU
    , aa.CpuCoresTMP as CpuCores
    , aa.CpuThreadsTMP as CpuThreads
    , aa.PowerStateTMP as PowerState
    , cc.DatastoresTMP as Datastores
    from TMP_vCenterHost aa inner join TMP_HostDcCluster bb on aa.HostName=bb.HostNameTMP inner join TMP_HostStores cc on aa.HostName=cc.HostNameTMP inner join TMP_HostVMs dd on aa.HostName=dd.HostNameTMP';
  
    execute immediate 'insert into LMS_vCenterVMS 
    select 
    vm.name as VMName,
    vm.GUEST_OS AS VMGuestOS,
    vm.DNS_NAME AS VMGuestHostName,
    vm.IP_ADDRESS AS VMIPAddress,
    tv.HOSTNAMETMP as HostName,
    '''||VCenterServerName||''' as VCenterServerName
    from '||VPXV_VMS||' vm inner join TMP_HostVM tv on vm.name=tv.VMNAME';
    
    execute immediate 'truncate table TMP_vCenterHost';
    execute immediate 'truncate table TMP_HostDcCluster';
    execute immediate 'truncate table TMP_HostStore';
    execute immediate 'truncate table TMP_HostStores';
    execute immediate 'truncate table TMP_HostVM';
    execute immediate 'truncate table TMP_HOSTVMS';
    
  END LOOP;
    execute immediate 'DROP table TMP_vCenterHost';
    execute immediate 'DROP table TMP_HostDcCluster';
    execute immediate 'DROP table TMP_HostStore';
    execute immediate 'DROP table TMP_HostStores';
    execute immediate 'DROP table TMP_HostVM';
    execute immediate 'DROP table TMP_HOSTVMS';
  END;

/

commit;
exit;


