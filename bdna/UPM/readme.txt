http://documents.software.dell.com/privilege-manager-for-unix/6.0/administrators-guide/

Managing Security Policy->Editing the Policy Interactively:

http://documents.software.dell.com/privilege-manager-for-unix/6.0/administrators-guide/managing-security-policy/editing-the-policy-interactively



[root@RH5-8156 examples_600_027]# /opt/quest/sbin/pmpolicy edit -p profileBasedPolicy.conf
  ** Validate options                                                    [ OK ]
** Edit Policy

  ** Check out working copy                                              [ OK ]
  ** Editing file:profileBasedPolicy.conf
  ** Perform syntax check                                                [ OK ]
  ** Verify files to commit                                              [ OK ]
Please enter the commit log message: updated by cacey.

  ** Commit change from working copy                                     [ OK ]
    ** Committed revision 35
  ** Finished editing policy

[root@RH5-8156 examples_600_027]# pmrun su -
********************************************************************
**      Quest Privilege Manager for Unix Version 6.0.0 (027)      **
********************************************************************
** You are required to authenticate as the UPM user :"root" before running this command
Password:
Request accepted by the "admin" profile on server:RH5-8156.RH5-8156

 All interactions with this command will be recorded in the file:
       /var/opt/quest/qpm4u/iolog//admin/root/su_20160722_1457_nekQWd

Executing "su" as user "root" ...
********************************************************************************
