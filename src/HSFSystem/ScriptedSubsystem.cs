﻿// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using IronPython.Hosting;
using MissionElements;
using HSFUniverse;
using Utilities;
using Newtonsoft.Json.Linq;

namespace HSFSystem
{
    public class ScriptedSubsystem : Subsystem
    {
        #region Attributes
        // A reference to the python scripted class
        private dynamic _pythonInstance;

        // Overide the accessors in order to modify the python instance
        public override List<Subsystem> DependentSubsystems
        {
            get { return (List<Subsystem>)_pythonInstance.DependentSubsystems; }
            set { _pythonInstance.DependentSubsystems = (List<Subsystem>)value; }
        }

        public override Dictionary<string, Delegate> SubsystemDependencyFunctions
        {
            get { return (Dictionary<string, Delegate>)_pythonInstance.SubsystemDependencyFunctions; }
            set { _pythonInstance.SubsystemDependencyFunctions = (Dictionary<string, Delegate>)value; }
        }

        //public override bool IsEvaluated
        //{
        //    get { return (bool)_pythonInstance.IsEvaluated; }
        //    set { _pythonInstance.IsEvaluated = (bool)value; }
        //}

        public override SystemState NewState
        {
            get { return (SystemState)_pythonInstance.NewState; }
            set { _pythonInstance.NewState = value; }
        }

        public override MissionElements.Task Task
        {
            get { return (MissionElements.Task)_pythonInstance.Task; }
            set { _pythonInstance.Task = value; }
        }

        private readonly string src = "";
        private readonly string className = "";
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor to build ScriptedSubsystem from JSON input
        /// </summary>
        /// <param name="scriptedSubsystemJson"></param>
        /// <param name="asset"></param>
        /// <exception cref="ArgumentException"></exception>
        public ScriptedSubsystem(JObject scriptedSubsystemJson, Asset asset)
        {
            StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;

            this.Asset = asset;
            //if(scriptedSubsystemJson.TryGetValue("name", stringCompare, out JToken nameJason))
            //    this.Name = this.Asset.Name.ToLower() + "." + nameJason.ToString().ToLower();
            //else
            //{
            //    Console.WriteLine($"Error loading subsytem of type {this.Type}, missing Name attribute");
            //    throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing Name attribute\"");
            //}

            if (scriptedSubsystemJson.TryGetValue("src", stringCompare, out JToken srcJason))
            {
                this.src = srcJason.ToString();
                this.src = Path.Combine(Utilities.DevEnvironment.RepoDirectory, src.Replace('\\', '/')); //Replace backslashes with forward slashes, if applicable
            }
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing Src attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing Src attribute");
            }

            if (scriptedSubsystemJson.TryGetValue("className", stringCompare, out JToken classNameJason))
                this.className = classNameJason.ToString();
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
            }

            InitSubsystem(scriptedSubsystemJson);
        }

        private void InitSubsystem(params object[] parameters)
        {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            var ops = engine.Operations;
            // Search paths are for importing modules from python scripts, not for executing python subsystem files
            var p = engine.GetSearchPaths();
            p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\PythonSubs");
            p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\");
            p.Add(AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\samples\Aeolus\pythonScripts");

            // Trying to use these so we can call numpy, etc...  Does not seem to work 8/31/23
            p.Add(@"C:\Python310\Lib\site-packages\");
            p.Add(@"C:\Python310\Lib");

            engine.SetSearchPaths(p);
            engine.ExecuteFile(src, scope);
            var pythonType = scope.GetVariable(className);
            // Look into this, string matters - related to file name, I think
            _pythonInstance = ops.CreateInstance(pythonType);//, parameters);
            Delegate depCollector = _pythonInstance.GetDependencyCollector();
            SubsystemDependencyFunctions = new Dictionary<string, Delegate>
            {
                { "DepCollector", depCollector }
            };

            _pythonInstance.Asset = this.Asset;
            _pythonInstance.Name = this.Name;
            DependentSubsystems = new List<Subsystem>();
        }
        #endregion

        #region Methods

        public void SetStateVariable<T>(ScriptedSubsystemHelper HSFHelper, string StateName, StateVariableKey<T> key)
        {
            HSFHelper.PythonInstance.SetStateVariable(_pythonInstance, StateName, key);

        }

        public void SetSubsystemParameter(ScriptedSubsystemHelper HSFHelper, string paramenterName, dynamic parameterValue)
        {
            HSFHelper.PythonInstance.SetStateVariable(_pythonInstance, paramenterName, parameterValue);
        }

        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            //if (IsEvaluated)
            //    return true;

            //// Check all dependent subsystems
            //foreach (var sub in DependentSubsystems)
            //{
            //    if (!sub.IsEvaluated)
            //        if (sub.CanPerform(proposedEvent, environment) == false)
            //            return false;
            //}

            //_task = proposedEvent.GetAssetTask(Asset); //Find the correct task for the subsystem
            //_newState = proposedEvent.State;
            //IsEvaluated = true;

            // Call the can perform method that is in the python class
            bool perform = false;
            try
            {
                perform = _pythonInstance.CanPerform(proposedEvent, environment);
            } catch (Exception ex)
            {
                Console.WriteLine($"Error in subsystem CanPerform call {this.Name} for task type {Task.Type}. With exception {ex}");
            }
            return perform;
        }

        public override bool CanExtend(Event proposedEvent, Domain environment, double evalToTime)
        {
            dynamic extend = _pythonInstance.CanExtend(proposedEvent, environment, evalToTime);
            return (bool)extend;
        }

        public Delegate GetDepFn(string depFnName, ScriptedSubsystem depSub)
        {
            // Access the python instance, call DepFinder from python model, return the Delegate fn requested
            var pythonInstance = depSub._pythonInstance;
            Dictionary<String, Delegate> theBook = pythonInstance.DepFinder(depFnName);
            Delegate page = theBook[depFnName];
            return page;
        }
        #endregion
    }
}
