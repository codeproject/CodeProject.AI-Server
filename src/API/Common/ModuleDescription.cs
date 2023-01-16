using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.API.Common
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ModuleStatusType
    {
        /// <summary>
        /// No idea what's happening.
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown = 0,

        /// <summary>
        /// Not available. Maybe not valid, maybe not available on this platform.
        /// </summary>
        [EnumMember(Value = "NotAvailable")]
        NotAvailable,

        /// <summary>
        /// Available to be downloaded and installed on this platform.
        /// </summary>
        [EnumMember(Value = "Available")]
        Available,

        /// <summary>
        /// An update to an already-installed module is Available to be downloaded and installed
        /// on this platform.
        /// </summary>
        [EnumMember(Value = "UpdateAvailable")]
        UpdateAvailable,
        
        /// <summary>
        /// Currently downloading from the registry
        /// </summary>
        [EnumMember(Value = "Downloading")]
        Downloading,

        /// <summary>
        /// Unpacking the downloaded model and prepping for install
        /// </summary>
        [EnumMember(Value = "Unpacking")]
        Unpacking,

        /// <summary>
        /// Installing the module
        /// </summary>
        [EnumMember(Value = "Installing")]
        Installing,

        /// <summary>
        /// Tried to install but failed to install in a way that allowed a successful start
        /// </summary>
        [EnumMember(Value = "FailedInstall")]
        FailedInstall,

        /// <summary>
        /// Off to the races
        /// </summary>
        [EnumMember(Value = "Installed")]
        Installed,

        /// <summary>
        /// Stopping and uninstalling this module.
        /// </summary>
        [EnumMember(Value = "Uninstalling")]
        Uninstalling,

        /// <summary>
        /// Tried to uninstall but failed.
        /// </summary>
        [EnumMember(Value = "UninstallFailed")]
        UninstallFailed,

        /// <summary>
        /// Was installed, but no longer. Completely Uninstalled.
        /// </summary>
        [EnumMember(Value = "Uninstalled")]
        Uninstalled
    }

    /// <summary>
    /// A description of a downloadable AI analysis module.
    /// </summary>
    public class ModuleDescription : ModuleBase
    {
        /// <summary>
        /// Gets or sets the URL from where this module can be downloaded.
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the status of this module.
        /// </summary>
        public ModuleStatusType? Status { get; set; }

        /// <summary>
        /// Gets or sets the number of downloads of this module
        /// </summary>
        public int Downloads { get; set; }

        /// <summary>
        /// Gets or sets the version of this module currently installed.
        /// </summary>
        public string? CurrentInstalledVersion { get; set; }        

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        public bool Valid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ModuleId)    &&
                       !string.IsNullOrWhiteSpace(DownloadUrl) &&
                       !string.IsNullOrWhiteSpace(Name)        &&
                       Platforms?.Length > 0;
            }
        }
    }

    /// <summary>
    /// Extension methods for the ModuleDescription class
    /// </summary>
    public static class ModuleDescriptionExtensions
    {
        /// <summary>
        /// Gets a value indicating whether or not this module is actually available. This depends 
        /// on having valid commands, settings, and importantly, being supported on this platform.
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="platform">The platform being tested</param>
        public static bool Available(this ModuleDescription module, string platform)
        {
            if (module is null)
                return false;

            return module.Valid && ( module.Platforms!.Any(p => p.EqualsIgnoreCase("all")) ||
                                     module.Platforms!.Any(p => p.EqualsIgnoreCase(platform)) );
        }
    }
}
