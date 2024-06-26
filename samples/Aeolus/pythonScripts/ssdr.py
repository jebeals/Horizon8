import sys
import clr
import System.Collections.Generic
import System
clr.AddReference('System.Core')
clr.AddReference('IronPython')
clr.AddReference('System.Xml')
clr.AddReferenceByName('Utilities')
clr.AddReferenceByName('HSFUniverse')
clr.AddReferenceByName('UserModel')
clr.AddReferenceByName('MissionElements')
clr.AddReferenceByName('HSFSystem')

import System.Xml
import HSFSystem
import MissionElements
import Utilities
import HSFUniverse
import UserModel
from HSFSystem import *
from System.Xml import XmlNode
from Utilities import *
from HSFUniverse import *
from UserModel import *
from MissionElements import *
from System import Func, Delegate
from System.Collections.Generic import *
from IronPython.Compiler import CallTarget0

class ssdr(HSFSystem.Subsystem):

    def CanPerform(self, event, universe):
        #print(self.Task.Type)
        if (self.Task.Type == "imaging"):
            ts = event.GetTaskStart(self.Asset)
            te = event.GetTaskEnd(self.Asset)

            oldbufferratio = self.NewState.GetLastValue(self.databufferfillratio_key)[1]
            newdataratein = HSFProfile[System.Double]()
            newdataratein = self.DependencyCollector(event) / self.bufferSize
            exceeded = False
            newdataratio = HSFProfile[System.Double]()
            newdataratio = newdataratein.upperLimitIntegrateToProf(ts, te, 1, 1, exceeded, 0, oldbufferratio)
            if (exceeded == False):
                self.NewState.AddValues(self.databufferfillratio_key, newdataratio[0])
                #print("SSDR CanPreform, Imaging Task, Dataratio")
                #print(newdataratio[0])
                return True
            
            return False
        if (self.Task.Type == "comm"):
            ts = event.GetTaskStart(self.Asset)
            event.SetTaskEnd(self.Asset, ts + 60.0)
            te = event.GetTaskEnd(self.Asset)
            data = self.bufferSize * self.NewState.GetLastValue(self.databufferfillratio_key)[1]
            if( data / 2 > 50):
                dataqueout = data/2
            else:
                dataqueout = data
            if (data - dataqueout < 0):
                dataqueout = data
            if (dataqueout > 0):
                self.NewState.AddValue(self.databufferfillratio_key, te, (data - dataqueout) / self.bufferSize)

            return True
        
        return True

    def CanExtend(self, event, universe, extendTo):
        return super(ssdr, self).CanExtend(event, universe, extendTo)

    def Power_asset1_from_SSDR_asset1(self, event):
        prof1 = HSFProfile[System.Double]()
        prof1[event.GetEventStart(self.Asset)] = 15
        return prof1

    def Comm_asset1_from_SSDR_asset1(self, event):
        datarate = 5000 * (event.State.GetValueAtTime(self.databufferfillratio_key, event.GetTaskStart(self.Asset)).Item2 - event.State.GetValueAtTime(self.databufferfillratio_key, event.GetTaskEnd(self.Asset)).Item2) / (event.GetTaskEnd(self.Asset) - event.GetTaskStart(self.Asset))
        prof1 = HSFProfile[System.Double]()
        if (datarate != 0):
            prof1[event.GetTaskStart(self.Asset)] = datarate
            prof1[event.GetTaskEnd(self.Asset)] = 0
        return prof1

    def DepFinder(self, depFnName):  # Search for method from string input
        fnc = getattr(self, depFnName)
        dep = Dictionary[str, Delegate]()
        depFnToAdd = Func[Event, Utilities.HSFProfile[System.Double]](fnc)
        dep.Add(depFnName, depFnToAdd)
        return dep

    def GetDependencyCollector(self):
        return Func[Event,  Utilities.HSFProfile[System.Double]](self.DependencyCollector)

    def DependencyCollector(self, currentEvent):
        return super(ssdr, self).DependencyCollector(currentEvent)

#    def GetDependencyDictionary(self):
#        dep = Dictionary[str, Delegate]()
#        depFunc1 = Func[Event,  Utilities.HSFProfile[System.Double]](self.POWERSUB_PowerProfile_SSDRSUB)
#        depFunc2 = Func[Event,  Utilities.HSFProfile[System.Double]](self.COMMSUB_DataRateProfile_SSDRSUB)
#        dep.Add("CommfromSSDR" + "." + self.Asset.Name, depFunc2)
#        depFunc3 = Func[Event,  System.Double](self.EVAL_DataRateProfile_SSDRSUB)
#        dep.Add("EvalfromSSDR" + "." + self.Asset.Name, depFunc3)
#        return dep

#    def EVAL_DataRateProfile_SSDRSUB(self, event):
#        return (event.State.GetValueAtTime(DATABUFFERRATIO_KEY, event.GetTaskEnd(self.Asset)).Value - event.State.GetValueAtTime(DATABUFFERRATIO_KEY, event.GetTaskEnd(self.Asset)).Value) * 50
