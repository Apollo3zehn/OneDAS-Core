﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OneDas.Hdf.Convert
{
    public class OneDasConvertContext
    {
        #region Fields

        private string _filePath;

        #endregion

        #region Constructors

        private OneDasConvertContext()
        {
            this.ProcessedPeriods = new List<DateTime>();
        }

        #endregion

        #region Properties

        public List<DateTime> ProcessedPeriods { get; set; }

        #endregion

        #region Methods

        public static OneDasConvertContext OpenOrCreate(string importDirectoryPath)
        {
            var _filePath = Path.Combine(importDirectoryPath, "metadata.json");

            OneDasConvertContext convertContext;

            if (File.Exists(_filePath))
                convertContext = JsonSerializer.Deserialize<OneDasConvertContext>(File.ReadAllText(_filePath));
            else
                convertContext = new OneDasConvertContext();

            return convertContext;
        }

        public void Save()
        {
            this.ProcessedPeriods.Sort();

            // save file
            var options = new JsonSerializerOptions { WriteIndented = true };
            var writeJsonString = JsonSerializer.Serialize(this, options);

            File.WriteAllText(_filePath, writeJsonString);
        }

        #endregion
    }
}
