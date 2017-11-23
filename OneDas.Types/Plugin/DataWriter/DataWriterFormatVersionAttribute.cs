﻿using System;

namespace OneDas.Plugin
{
    [AttributeUsage(validOn: AttributeTargets.Class, AllowMultiple = false)]
    public class DataWriterFormatVersionAttribute : Attribute
    {
        public DataWriterFormatVersionAttribute(int formatVersion)
        {
            this.FormatVersion = formatVersion;
        }

        public int FormatVersion { get; private set; }
    }
}

