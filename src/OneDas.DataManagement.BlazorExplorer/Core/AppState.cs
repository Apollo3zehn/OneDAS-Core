﻿using OneDas.DataManagement.Database;
using OneDas.Infrastructure;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OneDas.DataManagement.BlazorExplorer.Core
{
    public class AppState : BindableBase
    {
        #region Fields

        private string _searchString;
        private string _sampleRate;

        private DateTime _dateTimeBegin;
        private DateTime _dateTimeEnd;

        private CampaignContainer _campaignContainer;
        private List<VariableInfoViewModel> _variableGroup;

        private List<DatasetInfoViewModel> _selectedDatasets;
        private Dictionary<CampaignContainer, List<VariableInfoViewModel>> _campaignContainerToVariablesMap;

        #endregion

        #region Constructors

        public AppState()
        {
            this.Version = Assembly.GetEntryAssembly().GetName().Version.ToString();

            this.DateTimeBegin = DateTime.UtcNow.Date.AddDays(-2);
            this.DateTimeEnd = DateTime.UtcNow.Date.AddDays(-1);
            
            this.DateTimeBeginMaximum = this.DateTimeEnd;
            this.DateTimeEndMinimum = this.DateTimeBegin;

            this.FileGranularity = FileGranularity.Hour;
            this.FileGranularityValues = Utilities.GetEnumValues<FileGranularity>();

            this.FileFormat = FileFormat.CSV;
            this.FileFormatValues = Utilities.GetEnumValues<FileFormat>();

            this.CampaignContainers = Program.DatabaseManager.Database.CampaignContainers.AsReadOnly();
            this.SampleRateValues = this.CampaignContainers.SelectMany(campaignContainer =>
            {
                return campaignContainer.Campaign.Variables.SelectMany(variable =>
                {
                    return variable.Datasets.Select(dataset => dataset.Name.Split('_')[0])
                                            .Where(sampleRate => !sampleRate.Contains("600 s"));
                });
            }).Distinct().OrderBy(x => x, new SampleRateStringComparer()).ToList();
            this.NewsPaper = NewsPaper.Load();

            this.InitializeCampaignContainerToVariableMap();
            this.UpdateSelectedDatasets();
        }

        #endregion

        #region Properties

        public string Version { get; }
        
        public DateTime DateTimeBegin
        {
            get
            {
                return _dateTimeBegin;
            }
            set
            {
                _dateTimeBegin = value;
                this.DateTimeEndMinimum = value;
            }
        }

        public DateTime DateTimeBeginMaximum;

        public List<string> SampleRateValues { get; set; }

        public List<FileGranularity> FileGranularityValues { get; }

        public List<FileFormat> FileFormatValues { get; }

        public CampaignContainer CampaignContainer
        {
            get
            {
                return _campaignContainer;
            }
            set
            {
                this.SetProperty(ref _campaignContainer, value);

                _searchString = string.Empty;

                this.UpdateGroupedVariables();
                this.UpdateAttachments();
            }
        }

        public List<string> Attachments { get; private set; }

        public ReadOnlyCollection<CampaignContainer> CampaignContainers { get; }

        public Dictionary<string, List<VariableInfoViewModel>> GroupedVariables { get; private set; }

        public List<VariableInfoViewModel> VariableGroup
        {
            get { return _variableGroup; }
            set { base.SetProperty(ref _variableGroup, value); }
        }

        public NewsPaper NewsPaper { get; }

        public string SearchString
        {
            get { return _searchString; }
            set 
            {
                base.SetProperty(ref _searchString, value); 
                this.UpdateGroupedVariables();
            }
        }

        #endregion

        #region Serializable

        public DateTime DateTimeEnd
        {
            get
            {
                return _dateTimeEnd;
            }
            set
            {
                _dateTimeEnd = value;
                this.DateTimeBeginMaximum = value;
            }
        }

        public DateTime DateTimeEndMinimum;

        public string SampleRate
        {
            get { return _sampleRate; }
            set { base.SetProperty(ref _sampleRate, value); }
        }

        public FileGranularity FileGranularity { get; set; }

        public FileFormat FileFormat { get; set; }

        public List<DatasetInfoViewModel> SelectedDatasets
        {
            get { return _selectedDatasets; }
            set { base.SetProperty(ref _selectedDatasets, value); }
        }

        #endregion

        #region Methods

        public void OnDatasetSelectionChanged()
        {
            this.UpdateSelectedDatasets();
        }

        private void UpdateSelectedDatasets()
        {
            if (this.CampaignContainer != null)
            {
                this.SelectedDatasets = _campaignContainerToVariablesMap.ToList().SelectMany(entry =>
                {
                    return entry.Value.SelectMany(variable => variable.Datasets)
                                      .Where(dataset => dataset.IsSelected);
                }).ToList();
            }
            else
            {
                this.SelectedDatasets = new List<DatasetInfoViewModel>();
            }
        }

        private void UpdateAttachments()
        {
            this.Attachments = null;

            if (this.CampaignContainer != null)
            {
                var folderPath = Path.Combine(Environment.CurrentDirectory, "META", this.CampaignContainer.PhysicalName);

                if (Directory.Exists(folderPath))
                    this.Attachments = Directory.GetFiles(folderPath, "*").ToList();
            }
        }

        private void UpdateGroupedVariables()
        {
            this.VariableGroup = null;

            if (this.CampaignContainer != null)
            {
                this.GroupedVariables = new Dictionary<string, List<VariableInfoViewModel>>();

                foreach (var variable in _campaignContainerToVariablesMap[this.CampaignContainer])
                {
                    if (this.VariableMatchesFilter(variable))
                    {
                        var groupNames = variable.Group.Split('\n');

                        foreach (string groupName in groupNames)
                        {
                            var success = this.GroupedVariables.TryGetValue(groupName, out var group);

                            if (!success)
                            {
                                group = new List<VariableInfoViewModel>();
                                this.GroupedVariables[groupName] = group;
                            }

                            group.Add(variable);
                        }
                    }
                }

                foreach (var entry in this.GroupedVariables)
                {
                    entry.Value.Sort((x, y) => x.Name.CompareTo(y.Name));
                }
            }
        }

        private bool VariableMatchesFilter(VariableInfoViewModel variable)
        {
            if (string.IsNullOrWhiteSpace(this.SearchString))
                return true;

            if (variable.Name.Contains(this.SearchString, StringComparison.OrdinalIgnoreCase) 
             || variable.Description.Contains(this.SearchString, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void InitializeCampaignContainerToVariableMap()
        {
            _campaignContainerToVariablesMap = new Dictionary<CampaignContainer, List<VariableInfoViewModel>>();

            foreach (var campaignContainer in this.CampaignContainers)
            {
                _campaignContainerToVariablesMap[campaignContainer] = campaignContainer.Campaign.Variables.Select(variable =>
                {
                    var variableMeta = campaignContainer.CampaignMeta.Variables.FirstOrDefault(variableMeta => variableMeta.Name == variable.Name);
                    return new VariableInfoViewModel(variable, variableMeta);
                }).ToList();
            }
        }

        #endregion
    }
}
