﻿// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Utilities;
using HSFUniverse;
using MissionElements;
using UserModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace HSFSystem
{
    //[ExcludeFromCodeCoverage]
    public class Power : Subsystem
    {
        #region Attributes
        // Some Default Values
        protected double _batterySize = 1000000;
        protected double _fullSolarPanelPower = 150;
        protected double _penumbraSolarPanelPower = 75;

        protected StateVariableKey<double> DOD_KEY;
        protected StateVariableKey<double> POWIN_KEY;
        #endregion Attributes

        #region Constructors
        public Power(JObject PowerJson)
        {
            StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;
            JToken paramJson;
            if (PowerJson.TryGetValue("batterySize", stringCompare, out paramJson))
                this._batterySize = paramJson.Value<double>();
            if (PowerJson.TryGetValue("fullSolarPower", stringCompare, out paramJson))
                this._fullSolarPanelPower = paramJson.Value<double>();
            if (PowerJson.TryGetValue("penumbraSolarPower", stringCompare, out paramJson))
                this._penumbraSolarPanelPower = paramJson.Value<double>();

        }
        /// <summary>
        /// Constructor for built in subsystem
        /// Defaults: batterySize = 1000000, fullSolarPanelPower =150, penumbraSolarPanelPower = 75
        /// </summary>
        /// <param name="PowerNode"></param>
        /// <param name="asset"></param>
        public Power(XmlNode PowerNode)
        {

            if (PowerNode.Attributes["batterySize"] != null)
                _batterySize = (double)Convert.ChangeType(PowerNode.Attributes["batterySize"].Value, typeof(double));
            if (PowerNode.Attributes["fullSolarPower"] != null)
                _fullSolarPanelPower = (double)Convert.ChangeType(PowerNode.Attributes["fullSolarPower"].Value, typeof(double));
            if (PowerNode.Attributes["penumbraSolarPower"] != null)
                _penumbraSolarPanelPower = (double)Convert.ChangeType(PowerNode.Attributes["penumbraSolarPower"].Value, typeof(double));
        }

        /// <summary>
        /// Constructor for built in subsystem
        /// </summary>
        /// <param name="PowerNode"></param>
        /// <param name="asset"></param>
        /*
        public Power(XmlNode PowerNode, Asset asset) : base(PowerNode, asset)
        {
            
        }
        */
        #endregion Constructors

        #region Methods
        /// <summary>
        /// Calculate the solar panel power in depending on position
        /// </summary>
        /// <param name="shadow"></param>
        /// <returns></returns>
        protected double GetSolarPanelPower(ShadowState shadow)
        {
            switch (shadow)
            {
                case ShadowState.UMBRA:
                    return 0;
                case ShadowState.PENUMBRA:
                    return _penumbraSolarPanelPower;
                default:
                    return _fullSolarPanelPower;
            }
        }

        /// <summary>
        /// Calculate the solar panel power in over the time of the task
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="state"></param>
        /// <param name="position"></param>
        /// <param name="universe"></param>
        /// <returns></returns>
        protected HSFProfile<double> CalcSolarPanelPowerProfile(double start, double end, SystemState state, DynamicState position, Domain universe)
        {
            var POWIN_KEY = Dkeys[1];
            Sun sun = universe.GetObject<Sun>("SUN");
            // create solar panel profile for this event
            double freq = 5;
            ShadowState lastShadow = sun.castShadowOnPos(position, start);
            HSFProfile<double> solarPanelPowerProfile = new HSFProfile<double>(start, GetSolarPanelPower(lastShadow));

            for (double time = start + freq; time <= end; time += freq)
            {
                ShadowState shadow = sun.castShadowOnPos(position, time);
                // if the shadow state changes during this step, save the power data
                if (shadow != lastShadow)
                {
                    solarPanelPowerProfile[time] = GetSolarPanelPower(shadow);
                    lastShadow = shadow;
                }
            }
            state.AddValues(POWIN_KEY, solarPanelPowerProfile);
            return solarPanelPowerProfile;
        }

        /// <summary>
        /// Override of the canPerform method for the power subsystem
        /// </summary>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
        /// <param name="tasks"></param>
        /// <param name="universe"></param>
        /// <returns></returns>
        public override bool CanPerform(Event proposedEvent, Domain universe)
        {
            var DOD_KEY = Dkeys[0]; // Should be needed?

            double es = proposedEvent.GetEventStart(Asset);
            double te = proposedEvent.GetTaskEnd(Asset);
            double ee = proposedEvent.GetEventEnd(Asset);
            double powerSubPowerOut = 10;

            if (ee > SimParameters.SimEndSeconds)
            {
                Console.WriteLine("Simulation ended");
                return false;
            }

            // get the old DOD
            double olddod = NewState.GetLastValue(Dkeys.First()).Item2;

            // collect power profile out
            Delegate DepCollector;
            SubsystemDependencyFunctions.TryGetValue("DepCollector", out DepCollector);
            HSFProfile<double> powerOut = (HSFProfile<double>)DepCollector.DynamicInvoke(proposedEvent); // deps->callDoubleDependency("POWERSUB_getPowerProfile");
            powerOut = powerOut + powerSubPowerOut;
            // collect power profile in
            DynamicState position = Asset.AssetDynamicState;
            HSFProfile<double> powerIn = CalcSolarPanelPowerProfile(es, te, NewState, position, universe);
            // calculate dod rate
            HSFProfile<double> dodrateofchange = ((powerOut - powerIn) / _batterySize);

            bool exceeded = false;
            double freq = 1.0;
            HSFProfile<double> dodProf = dodrateofchange.lowerLimitIntegrateToProf(es, te, freq, 0.0, ref exceeded, 0, olddod);
            //why is exceeded not checked anywhere??

            NewState.AddValues(DOD_KEY, dodProf);
            return true;
        }

        /// <summary>
        /// Override of the canExtend method for the power subsystem
        /// </summary>
        /// <param name="newState"></param>
        /// <param name="universe"></param>
        /// <param name="evalToTime"></param>
        /// <returns></returns>
        /*
        public override bool CanExtend(Event proposedEvent, Domain universe, double evalToTime) {
            var DOD_KEY = Dkeys[0];
            double ee = proposedEvent.GetEventEnd(Asset);
            if (ee > SimParameters.SimEndSeconds)
                return false;

            Sun sun = universe.GetObject<Sun>("SUN");
            double te = proposedEvent.State.GetLastValue(DOD_KEY).Time;
            if (proposedEvent.GetEventEnd(Asset) < evalToTime)
                proposedEvent.SetEventEnd(Asset, evalToTime);

            // get the dod initial conditions
            double olddod = proposedEvent.State.GetValueAtTime(DOD_KEY, te).Value;

            // collect power profile out
            Delegate DepCollector;
            SubsystemDependencyFunctions.TryGetValue("DepCollector", out DepCollector);
            HSFProfile<double> powerOut = (HSFProfile<double>)DepCollector.DynamicInvoke(proposedEvent); // deps->callDoubleDependency("POWERSUB_getPowerProfile");
            // collect power profile in
            DynamicState position = Asset.AssetDynamicState;
            HSFProfile<double> powerIn = CalcSolarPanelPowerProfile(te, ee, proposedEvent.State, position, universe);
            // calculate dod rate
            HSFProfile<double> dodrateofchange = ((powerOut - powerIn) / _batterySize);

            bool exceeded_lower = false, exceeded_upper = false;
            double freq =  1.0;
            HSFProfile<double> dodProf = dodrateofchange.limitIntegrateToProf(te, ee, freq, 0.0, 1.0, ref exceeded_lower, ref exceeded_upper, 0, olddod);
            if (exceeded_upper)  // why is exceeded upper checked and not exceeded lower?  
                return false;
            if(dodProf.LastTime() != ee && ee == SimParameters.SimEndSeconds)
            {
                dodProf[ee] = dodProf.LastValue();
            }
            proposedEvent.State.AddValues(DOD_KEY, dodProf);
            return true;
        }
        */
        #endregion Methods
    }

}
