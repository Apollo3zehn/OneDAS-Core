﻿using OneDas.DataManagement.Database;
using OneDas.DataManagement.Extensibility;
using System;

namespace OneDas.DataManagement.Explorer.Core
{
    public class ZipSettings
    {
        public ZipSettings(SparseCampaignInfo campaign,
                           DataReaderExtensionBase nativeDataReader,
                           DataReaderExtensionBase aggregationDataReader,
                           DateTime begin,
                           DateTime end,
                           ulong samplesPerDay,
                           ulong blockSizeLimit)
        {
            this.Campaign = campaign;
            this.NativeDataReader = nativeDataReader;
            this.AggregationDataReader = aggregationDataReader;
            this.Begin = begin;
            this.End = end;
            this.SamplesPerDay = samplesPerDay;
            this.BlockSizeLimit = blockSizeLimit;
        }


        public SparseCampaignInfo Campaign { get; }

        public DataReaderExtensionBase NativeDataReader { get; }

        public DataReaderExtensionBase AggregationDataReader { get; }

        public DateTime Begin { get; }

        public DateTime End { get; }

        public ulong SamplesPerDay { get; }

        public ulong BlockSizeLimit { get; }
    }
}
