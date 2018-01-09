﻿using System;
using System.Collections.Generic;
using OneDas.Infrastructure;
using OneDas.Types.Settings;

namespace OneDas.Plugin
{
    public class VariableDescription
    {
        #region "Constructors"

        public VariableDescription(ChannelHub channelHub)
        {
            this.Guid = channelHub.Guid;
            this.VariableName = channelHub.Name;
            this.DatasetName = $"{ GlobalSettings.NativeSampleRate / (int)channelHub.SampleRate } Hz"; ;
            this.Group = channelHub.Group;
            this.OneDasDataType = channelHub.OneDasDataType;
            this.SamplesPerDay = InfrastructureHelper.GetSamplesPerDayFromSampleRate(channelHub.SampleRate);
            this.Unit = channelHub.Unit;
            this.TransferFunctionSet = channelHub.TransferFunctionSet;
            this.DataStorageType = typeof(ExtendedDataStorageBase);
        }

        public VariableDescription(Guid guid, string variableName, string datasetName, string group, OneDasDataType oneDasDataType, ulong samplesPerDay, string unit, List<TransferFunction> transferFunctionSet, Type dataStorageType)
        {
            if (!dataStorageType is DataStorageBase)
            {
                throw new ArgumentException(ErrorMessage.VariableDescription_TypeNotSubclassOfDataStorage);
            }

            this.Guid = guid;
            this.VariableName = variableName;
            this.DatasetName = datasetName;
            this.Group = group;
            this.OneDasDataType = oneDasDataType;
            this.SamplesPerDay = samplesPerDay;
            this.Unit = unit;
            this.TransferFunctionSet = transferFunctionSet;
            this.DataStorageType = dataStorageType;
        }

        #endregion

        #region "Properties"

        public Guid Guid { get; private set; }
        public string VariableName { get; private set; }
        public string DatasetName { get; private set; }
        public string Group { get; private set; }
        public OneDasDataType OneDasDataType { get; private set; }
        public ulong SamplesPerDay { get; private set; }
        public string Unit { get; private set; }
        public List<TransferFunction> TransferFunctionSet { get; private set; }
        public Type DataStorageType { get; private set; }

        #endregion
    }
}