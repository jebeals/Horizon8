﻿// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Xml;
using log4net;
using Newtonsoft.Json.Linq;
using UserModel;
using static IronPython.Modules._ast;

namespace MissionElements
{
    /// <summary>
    /// An action to be performed at a target, with limitations and suggestions for scheduling.
    /// </summary>
    [Serializable]
    public class Task
    {
        public string Name { get; private set; }
        // the type of task being performed (will always be converted to a lowercase string in the constructor
        public string Type { get; private set; }

        // the target associated with the task 
        public Target Target { get; private set; }

        // The maximum number of times the task should be performed by the ENTIRE SYSTEM (all assets count towards this)
        public int MaxTimesToPerform { get; private set; }

        // Logger for log file
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// Constructor that creates a new task to be performed at the given target, with the given scheduling limitations
        /// </summary>
        /// <param name="type"></param>
        /// <param name="target"></param>
        /// <param name="maxTimesToPerform"></param>
        public Task(string name, string type, Target target, int maxTimesToPerform)
        {
            Name = name;
            Type = type.ToLower();
            Target = target;
            MaxTimesToPerform = maxTimesToPerform;
        }

        /// <summary>
        /// Load Targets into a list to be passed to scheduler using JSON data
        /// </summary>
        /// <param name="taskListJson"></param>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public static bool LoadTasks(JObject taskListJson, Stack<Task> tasks)
        {
            if (taskListJson == null)
                return false;

            log.Info("Loading tasks into simulation... for scenario" + SimParameters.ScenarioName);
            Console.WriteLine("Loading tasks into simulation... for scenario {0}.  ", SimParameters.ScenarioName);

            // Default number of times to perform a task
            int maxTimesPerform = 1;
            string msg;

            if (JsonLoader<JToken>.TryGetValue("tasks", taskListJson, out JToken taskList))
            {
                foreach (JObject taskJson in taskList)
                {
                    if (!JsonLoader<string>.TryGetValue("name", taskJson, out string taskName))
                    {
                        msg = $"Task loading error.  Tasks must have a NAME.";
                        log.Fatal(msg);
                        Console.WriteLine(msg);
                        return false;
                    }

                    if (!JsonLoader<string>.TryGetValue("type", taskJson, out string taskType))
                    {
                        msg = $"Task loading error.  Tasks must have a TYPE at task named '{taskName}'";
                        log.Fatal(msg);
                        Console.WriteLine(msg);
                        return false;
                    }

                    if (!JsonLoader<int>.TryGetValue("maxTimes", taskJson, out maxTimesPerform))
                    {
                        msg = $"Task loading warning.  Task loaded without Max Times parameter for task '{taskName}'";
                        log.Warn(msg);
                        Console.WriteLine(msg);
                    }

                    if (JsonLoader<JObject>.TryGetValue("target", taskJson, out JObject targetJson))
                        tasks.Push(new Task(taskName, taskType, new Target(targetJson), maxTimesPerform));
                    else
                    {
                        msg = $"Task loading error.  Tasks must have a TARGET for task '{taskName}'";
                        log.Fatal(msg);
                        Console.WriteLine(msg);
                        return false;
                    }
                }
            }
            msg = $"Loaded {tasks.Count} tasks for scenario {SimParameters.ScenarioName}.";
            log.Info(msg);
            Console.WriteLine(msg);

            return true;
        }
        #region Overrides
        /// <summary>
        /// Override of the ToString method
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Target.Name;
        }
        #endregion
    }

    // This was changed to a string type instead of an enum to support custom taskTypes on 1/31/2022
    // The three types of tasks supported by Horizon
    //public enum TaskType { EMPTY, COMM, IMAGING, FLYALONG, RECOVERY }

  
}
