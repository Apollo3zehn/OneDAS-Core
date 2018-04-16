﻿using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace OneDas.Extensibility
{
    [DataContract]
    public abstract class ExtensionSettingsBase
    {
        #region "Constructors"

        public ExtensionSettingsBase()
        {
            ExtensionIdentificationAttribute pluginIdentificationAttribute;

            if (!this.GetType().IsDefined(typeof(ExtensionContextAttribute), false))
            {
                throw new Exception(ErrorMessage.ExtensionSettingsBase_ExtensionContextAttributeNotDefined);
            }

            if (!this.GetType().IsDefined(typeof(ExtensionIdentificationAttribute), false))
            {
                throw new Exception(ErrorMessage.ExtensionSettingsBase_ExtensionDescriptionAttributeNotDefined);
            }

            pluginIdentificationAttribute = this.GetType().GetFirstAttribute<ExtensionIdentificationAttribute>();

            this.Description = new ExtensionDescription(FileVersionInfo.GetVersionInfo(this.GetType().Assembly.Location).ProductVersion, pluginIdentificationAttribute.Id, true);
        }

        #endregion

        #region "Properties"

        [DataMember]
        public ExtensionDescription Description { get; set; }

        #endregion

        #region "Methods"

        public virtual void Validate()
        {
            this.Description.Validate();
        }

        #endregion
    }
}
