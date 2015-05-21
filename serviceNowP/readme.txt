Because the first version of build.xml produced the following ERROR in refreshing Report, I created the second version of build.xml (that's the present version).

[Wed May 20 23:52:02 CST 2015] regression test
[Wed May 20 23:52:02 CST 2015] ERROR found:
[Wed May 20 23:52:02 CST 2015] ===========================================================
[Wed May 20 23:52:02 CST 2015] rule.RULE1.log:29027:[ERROR,RULE1 worker] 2015-05-20 21:28:28,407 bdna.rule: The Perl rule action failed to execute for rule com.bdna.modules.app.Microsoft.MicrosoftIISCollection!populateWebSite_weblogic_iisproxydll_Path_fromwebconfig, XML result: not well-formed (invalid token) at line 1, column 0, byte 0:

rs.log:2:{ERROR} [main] com.bdna.pl LogUtil 2015-05-20 19:26:16,997  Application framework internal error: com.bdna.bvl.view_not_found; oraIASinstalls

rs.log:24:{ERROR} [main] com.bdna.pl LogUtil 2015-05-20 19:26:33,618  Application framework internal error: com.bdna.bvl.view_not_found; XenBase

rs.log:46:{ERROR} [ThreadPoolWorker:Thread-11] com.bdna.pl LogUtil 2015-05-20 19:52:16,694  Application framework internal error: com.bdna.bvl.view_not_found; oraIASinstalls

rs.log:96:{ERROR} [ThreadPoolWorker:Thread-11] com.bdna.pl LogUtil 2015-05-20 19:52:35,649  Application framework internal error: com.bdna.bvl.view_not_found; XenBase

rs.log:654:{ERROR} [main] com.bdna.pl LogUtil 2015-05-20 23:43:37,337  Required report not available for query PUB_masterOSmap.
[Wed May 20 23:52:02 CST 2015] ===========================================================
[Wed May 20 23:52:02 CST 2015] export BDNA discovery data
Command: cd /home/bdna/install761/pso/CDB && sh DataExtract.sh islamabad_bdna_761_19 islamabad_bdna_761_19 BDNA
Schema Name: islamabad_bdna_761_19
Schema Password: islamabad_bdna_761_19
TNS_NAME: BDNA


