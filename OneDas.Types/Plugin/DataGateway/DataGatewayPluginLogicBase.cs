﻿using System;
using System.Diagnostics;

namespace OneDas.Plugin
{
    public abstract class DataGatewayPluginLogicBase : PluginLogicBase
    {
        public DataGatewayPluginLogicBase(DataGatewayPluginSettingsBase settings) : base(settings)
        {
            this.Settings = settings;
        }

        public new DataGatewayPluginSettingsBase Settings { get; private set; }
        public Stopwatch LastSuccessfulUpdate { get; protected set; }

        public abstract void Configure();
        public abstract void UpdateIo(DateTime referenceDateTime);
    }
}