﻿// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Utilities;
using MissionElements;
using UserModel;
using Task = MissionElements.Task; // error CS0104: 'Task' is an ambiguous reference between 'MissionElements.Task' and 'System.Threading.Tasks.Task'

namespace HSFScheduler
{
    public class SystemSchedule
    {
        #region Attributes
        public StateHistory AllStates; //pop never gets used so just use list
        public double ScheduleValue;
        #endregion

        #region Constructors
        public SystemSchedule(SystemState initialstates) 
        {
            ScheduleValue = 0;
            AllStates = new StateHistory(initialstates);
        }

        public SystemSchedule(StateHistory allStates)
        {
            AllStates = new StateHistory(allStates);
        }

        public SystemSchedule(SystemSchedule oldSchedule, Event emptyEvent)
        {
            AllStates = new StateHistory(oldSchedule.AllStates);
            AllStates.Events.Push(emptyEvent);
        }

        public SystemSchedule(StateHistory oldStates, Stack<Access> newAccessList, double newEventStartTime)
        {
            Dictionary<Asset, Task> tasks = new Dictionary<Asset, Task>();
            Dictionary<Asset, double> taskStarts = new Dictionary<Asset, double>();
            Dictionary<Asset, double> taskEnds = new Dictionary<Asset, double>();
            Dictionary<Asset, double> eventStarts = new Dictionary<Asset, double>();
            Dictionary<Asset, double> eventEnds = new Dictionary<Asset, double>();

            foreach (var access in newAccessList)
            {
                if (access.Task != null)
                {
                    //  Access Starts before Event Start && Event Start is before the Access End
                    if (access.AccessStart <= newEventStartTime && newEventStartTime <= access.AccessEnd)
                        taskStarts.Add(access.Asset, newEventStartTime);
                    //  Access starts after Event Start && Access Starts before Step Size
                    else if (access.AccessStart >= newEventStartTime && access.AccessStart <= newEventStartTime + SimParameters.SimStepSeconds)
                        taskStarts.Add(access.Asset, access.AccessStart);
                    //  Set Task Start to Event Start Time
                    else
                    {
                        //Console.WriteLine("Event Start: " + newEventStartTime + " AccesStart: " + access.AccessStart + " AccessEnd: " + access.AccessEnd);
                        taskStarts.Add(access.Asset, newEventStartTime);
                    }
                    tasks.Add(access.Asset, access.Task);

                    //  Access Ends after the Simulation End Time - Set Task End time to Sim End Time
                    if (access.AccessEnd > SimParameters.SimEndSeconds)
                        taskEnds.Add(access.Asset, SimParameters.SimEndSeconds);
                    // Set, set Task End time to Access End Time
                    else
                        taskEnds.Add(access.Asset, access.AccessEnd);

                    eventStarts.Add(access.Asset, newEventStartTime);
                    
                    //  If Event Start + Step Size > Sim End Time - Set Event End time to Sim End Time
                    if (newEventStartTime + SimParameters.SimStepSeconds > SimParameters.SimEndSeconds)
                        eventEnds.Add(access.Asset, SimParameters.SimStepSeconds);
                    //  Else, set Event End time to Event Start Time + Sim Step
                    else
                        eventEnds.Add(access.Asset, newEventStartTime + SimParameters.SimStepSeconds);
                }
                else
                {
                    taskStarts.Add(access.Asset, newEventStartTime);
                    taskEnds.Add(access.Asset, newEventStartTime);
                    tasks.Add(access.Asset, null);
                    eventStarts.Add(access.Asset, newEventStartTime);
                    eventEnds.Add(access.Asset, newEventStartTime + SimParameters.SimStepSeconds);
                }

            }
            Event eventToAdd = new Event(tasks, new SystemState(oldStates.GetLastState())); //all references
            eventToAdd.SetEventEnd(eventEnds);
            eventToAdd.SetTaskEnd(taskEnds);
            eventToAdd.SetEventStart(eventStarts);
            eventToAdd.SetTaskStart(taskStarts);
            AllStates = new StateHistory(oldStates, eventToAdd);
        }
        #endregion
        
        /// <summary>
        /// Determine if a task can be added to a schedule at the new start time
        /// </summary>
        /// <param name="newAccessList"></param>
        /// <param name="newTaskStartTime"></param>
        /// <returns></returns>
        public bool CanAddTasks(Stack<Access> newAccessList, double currentTime)
        {
            int count = 0;

	        foreach(var access in newAccessList)
            {
                if (!AllStates.isEmpty(access.Asset))
                {
                    if (AllStates.GetLastEvent().GetEventEnd(access.Asset) > currentTime)
                        return false;
                }

		        if(access.Task != null)
                {
				    count += AllStates.timesCompletedTask(access.Task);
			        if(count >= access.Task.MaxTimesToPerform)
				        return false;
		        }
	        }
	        return true;
        }

        #region Accessors
        public int GetTotalNumEvents()
        {
            return AllStates.size();
        }

        public SystemState GetSubsystemNewState()
        {
            return AllStates.GetLastState();
        }

        public Task GetSubsytemNewTask(Asset asset)
        {
            return AllStates.GetLastTask(asset);
        }

        //public StateHistory GetStateHistory(Asset asset)
        //{
        //    return AllStates.Find(item => item.Asset == asset);
        //}

        public double GetLastTaskStart()
        {
            double lasttime = 0;
            foreach (KeyValuePair<Asset, double> assetTaskStarts in AllStates.GetLastEvent().TaskStarts)
            {
                lasttime = lasttime > assetTaskStarts.Value ? lasttime : assetTaskStarts.Value;
            }
            return lasttime;
        }

        public SystemState GetEndState()
        {
            return AllStates.GetLastState();
        }
        #endregion 

        /// <summary>
        /// Determine if the first schedule value is greater than the second
        /// </summary>
        /// <param name="elem1"></param>
        /// <param name="elem2"></param>
        /// <returns></returns>
        bool SchedGreater(SystemSchedule elem1, SystemSchedule elem2)
        {
            return elem1.ScheduleValue > elem2.ScheduleValue;
        }

        /// <summary>
        /// Utilitiy method to write the schedule to csv file
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="scheduleWritePath"></param>
        public static void WriteSchedule(SystemSchedule schedule, String scheduleWritePath) //TODO: Unit Test.
        {
            var csv = new StringBuilder();
            Dictionary<StateVariableKey<double>, SortedList<double, double>> stateTimeDData = new Dictionary<StateVariableKey<double>, SortedList<double, double>>();
            Dictionary<StateVariableKey<int>, SortedList<double, int>> stateTimeIData = new Dictionary<StateVariableKey<int>, SortedList<double, int>>();
            Dictionary<StateVariableKey<int>, SortedList<double, int>> stateTimeBData = new Dictionary<StateVariableKey<int>, SortedList<double, int>>(); // need 0s and 1 for matlab to read in csv
            Dictionary<StateVariableKey<Matrix<double>>, SortedList<double, Matrix<double>>> stateTimeMData = new Dictionary<StateVariableKey<Matrix<double>>, SortedList<double, Matrix<double>>>();
            Dictionary<StateVariableKey<Quaternion>, SortedList<double, Quaternion>> stateTimeQData = new Dictionary<StateVariableKey<Quaternion>, SortedList<double, Quaternion>>();
            string stateTimeData = "Time,";
            string stateData = "";
            csv.Clear();

            SystemState sysState=null;
            if (schedule.AllStates.Events.Count!= 0)
            {
                sysState = schedule.AllStates.Events.Peek().State;
            }
            

            while(sysState != null) { 
                foreach (var kvpDoubleProfile in sysState.Ddata)
                    foreach (var data in kvpDoubleProfile.Value.Data)
                        if (!stateTimeDData.ContainsKey(kvpDoubleProfile.Key))
                        {
                            var lt = new SortedList<double, double>();
                            lt.Add(data.Key, data.Value);
                            stateTimeDData.Add(kvpDoubleProfile.Key, lt);
                        }
                        else if (!stateTimeDData[kvpDoubleProfile.Key].ContainsKey(data.Key))
                            stateTimeDData[kvpDoubleProfile.Key].Add(data.Key, data.Value);
                        else
                            Console.WriteLine("idk"); //TERRIBLE!

                foreach (var kvpIntProfile in sysState.Idata)
                    foreach (var data in kvpIntProfile.Value.Data)
                        if (!stateTimeIData.ContainsKey(kvpIntProfile.Key))
                        {
                            var lt = new SortedList<double, int>();
                            lt.Add(data.Key, data.Value);
                            stateTimeIData.Add(kvpIntProfile.Key, lt);
                        }
                        else if (!stateTimeIData[kvpIntProfile.Key].ContainsKey(data.Key))
                            stateTimeIData[kvpIntProfile.Key].Add(data.Key, data.Value);

                foreach (var kvpBoolProfile in sysState.Bdata)
                    foreach (var data in kvpBoolProfile.Value.Data)
                        if (!stateTimeBData.ContainsKey(kvpBoolProfile.Key))
                        {
                            var lt = new SortedList<double, int>();
                            lt.Add(data.Key, (data.Value ? 1 : 0)); //convert to int for matlab to read in for csv
                            stateTimeBData.Add((StateVariableKey<int>)kvpBoolProfile.Key, lt);
                        }
                        else if (!stateTimeBData[kvpBoolProfile.Key].ContainsKey(data.Key))
                            stateTimeBData[(StateVariableKey<int>)kvpBoolProfile.Key].Add(data.Key, data.Value ? 1 : 0);

                foreach (var kvpMatrixProfile in sysState.Mdata)
                    foreach (var data in kvpMatrixProfile.Value.Data)
                        if (!stateTimeMData.ContainsKey(kvpMatrixProfile.Key))
                        {
                            var lt = new SortedList<double, Matrix<double>>();
                            lt.Add(data.Key, data.Value);
                            stateTimeMData.Add(kvpMatrixProfile.Key, lt);
                        }
                        else if (!stateTimeMData[kvpMatrixProfile.Key].ContainsKey(data.Key))
                            stateTimeMData[kvpMatrixProfile.Key].Add(data.Key, data.Value);
                foreach (var kvpQuatProfile in sysState.Qdata)
                    foreach (var data in kvpQuatProfile.Value.Data)
                        if (!stateTimeQData.ContainsKey(kvpQuatProfile.Key))
                        {
                            var lt = new SortedList<double, Quaternion>();
                            lt.Add(data.Key, data.Value);
                            stateTimeQData.Add(kvpQuatProfile.Key, lt);
                        }
                        else if (!stateTimeQData[kvpQuatProfile.Key].ContainsKey(data.Key))
                            stateTimeQData[kvpQuatProfile.Key].Add(data.Key, data.Value);
                sysState = sysState.PreviousState;
            }

            System.IO.Directory.CreateDirectory(scheduleWritePath);

            foreach(var list in stateTimeDData)
                writeStateVariable(list, scheduleWritePath);

            foreach(var list in stateTimeIData)
                writeStateVariable(list, scheduleWritePath);

            foreach (var list in stateTimeBData)
                writeStateVariable(list, scheduleWritePath);

            foreach (var list in stateTimeMData)
                writeStateVariable(list, scheduleWritePath);
            foreach (var list in stateTimeQData)
                writeStateVariable(list, scheduleWritePath);
        }
        
        /// <summary>
        /// Write out all the state variables in the schedule to file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="scheduleWritePath"></param>
        static void writeStateVariable<T>(KeyValuePair<StateVariableKey<T>, SortedList<double, T>> list, string scheduleWritePath) //TODO: Unit Test.
        {
            var csv = new StringBuilder();
            string fileName = list.Key.VariableName;

            string invalidChars = "";

            foreach (char c in System.IO.Path.GetInvalidPathChars())
                invalidChars += c;

            invalidChars += "(" + ")" + "/" + ".";

            foreach (char c in invalidChars)
                fileName = fileName.Replace(c, '_');

            csv.AppendLine("time" + "," + fileName);
            foreach (var k in list.Value)
                csv.AppendLine(k.Key + "," + k.Value);

            System.IO.File.WriteAllText(scheduleWritePath + "\\" + fileName + ".csv", csv.ToString());
            csv.Clear();
        }
    }
}