#!/usr/bin/python
import os

class scanBDNA:
    def __init__(self):
        self.bdnaHome = '/home/bdna/install771'
        self.scriptHome = '/home/bdna/myTask'


    def executeCommand(self,cmds):
        for i in range(len(cmds)):
            print 'Command: ' + cmds[i]
            result = os.system(cmds[i])
            if result != 0:
                raise Exception('command failed: ' + cmds[i])

    def importLMSkey(self):
        cmds = ['sh %s/conf/bcp_store/EnterpriseSequence/bin/install_oracle_lms_license.sh -k db252388113cb95e9741cb55ca57c0190804fe36267525194f345aa590798f152d9908e18831447e' % (self.bdnaHome)]
        try:
            self.executeCommand (cmds)
        except Exception, e:
            raise Exception('import lmskey failed\n',str(e))


    def importNetwork(self):
        if not os.path.isdir(self.scriptHome):
            raise Exception('folder not exist: %s' % (self.scriptHome))
        cmds = ['cd %s/bin ; sh runjava.sh com.bdna.pl.util.BulkLoader -i:4 -f:%s/01_bdna_network_level1.txt' % (self.bdnaHome, self.scriptHome),
                'cd %s/bin ; sh runjava.sh com.bdna.pl.util.BulkLoader -i:3 -f:%s/05_bdna_bulkload_projects_containers.txt' % (self.bdnaHome, self.scriptHome)]
        try:
            self.executeCommand (cmds)
        except Exception, e:
            raise Exception('import networks failed\n',str(e))
    
    def importCredential(self, cred):
        if not os.path.isdir(self.scriptHome):
            raise Exception('folder not exist: %s\n%s' % (self.scriptHome, str(e)))
        cmds = ['cd %s/bin ; sh runjava.sh com.bdna.agenda.BulkCommon BulkCred4 %s/06_bdna_bulkload_credentials_%s.txt' % (self.bdnaHome, self.scriptHome, cred)]
        try:
            self.executeCommand (cmds)
        except Exception, e:
            raise Exception('import credential failed: %s\n%s' % (cred, str(e)))
    
    def importTask(self, task):
        cmds = ['cd %s/bin ; sh runjava.sh com.bdna.agenda.BulkCommon BulkTask4 %s/07_bdna_bulkload_scantasks_%s.txt' % (self.bdnaHome, self.scriptHome, task)]
        try:
            self.executeCommand (cmds)
        except Exception, e:
            raise Exception('import task failed: %s\n%s' % (task, str(e)))

    def startScan(self):
        # if sequence is null, script scan task is empty, nothing to do. for manually scripting use.
        # but if sequence is not null, script will include at least level1, snmp, windows and linux in scan tasks.
        if 1<2:
            self.importTask ("level1")
            self.importCredential ("windows")
            self.importTask ("windows")
            self.importCredential ("unix")
            self.importTask ("unix")
            self.importCredential ("unixoracle")
            self.importCredential ("winoracle")
            self.importTask ("unixoracle")
            self.importTask ("winoracle")
     
if __name__ == '__main__':
    test = scanBDNA()
    test.importLMSkey()
    test.importNetwork()
    test.startScan()

