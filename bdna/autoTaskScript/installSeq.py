#!/usr/bin/python

import time
import os

class installSeq:

    def __init__(self):
        self.bdnaHome = '/home/bdna/install771'
        self.scriptHome = '/home/bdna/myTask'
	self.loadseq = '/home/bdna/myTask/cmds'


    def executeCommand(self,cmds):
        for i in range(len(cmds)):
           print 'Command: ' + cmds[i]
           result = os.system(cmds[i])
           if result != 0:
               raise Exception('command failed: ' + cmds[i])


    def killBDNA(self):
        sleepTime = 5
        time.sleep(sleepTime)
        cmd1 = ['sudo killall -9 java perl sqlplus']
        time.sleep(sleepTime)
        try:
            self.executeCommand(cmd1)
        except Exception, e:
            return True

    def installPlatform(self):
    	cmd2 = ['sudo rm -rf /home/bdna/install771 /home/bdna/install770_4055/ && %s/inPlatform771' % (self.scriptHome)]
        try:
            self.executeCommand(cmd2)
        except Exception, e:
            raise Exception('install platform 7.7.0 GA failed')


    def startLoadSeq(self):
        cmd3 = ['sh %s/bin/configure.sh <%s' % (self.bdnaHome, self.loadseq)]
        try:
            self.executeCommand(cmd3)
        except Exception, e:
            raise Exception('load sequence failed')

if __name__ == '__main__':
    inst = installSeq()
    inst.killBDNA()
    inst.installPlatform()
    inst.startLoadSeq()

