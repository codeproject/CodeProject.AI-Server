using System.Runtime.Serialization;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.API;

namespace CodeProject.AI.SDK
{
    /// <summary>
    /// Describes the installation status of a module
    /// </summary>
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
        /// Is available to be downloaded and installed on this platform.
        /// </summary>
        [EnumMember(Value = "Available")]
        Available,

        /// <summary>
        /// An update to an already-installed module is available to be downloaded and installed
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
        /// Gets or sets the URL from where this module can be downloaded. This could be included in
        /// the modules.json file that ultimately populates this object, but more likely this value
        /// will be set by the server at some point.
        /// </summary>
        public string? DownloadUrl { get; set; }

        /// <summary>
        /// Gets or sets the status of this module.
        /// </summary>
        public ModuleStatusType? Status { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the module represented in this description can
        /// actually be downloaded (as opposed to being side-loaded or uploaded by a user)
        /// </summary>
        public bool IsDownloadable { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of downloads of this module. This could be included in the
        /// modules.json file that ultimately populates this object, but more likely this value
        /// will be set by the server at some point.
        /// </summary>
        public int Downloads { get; set; }

        /// <summary>
        /// Gets or sets the ModuleRelease of the latest release of this module that is compatible
        /// with the current server. This value is not deserialised, but instead must be set by the
        /// server.
        /// </summary>
        public ModuleRelease? LatestCompatibleRelease { get; set; }

        /// <summary>
        /// Gets or sets the Version of this module currently installed, or null of this module is
        /// not currently installed. This value is not deserialised, but instead must be set by the
        /// server.
        /// </summary>
        public string? CurrentlyInstalled { get; set; }

        /// <summary>
        /// Gets a value indicating whether or not an update for the currently installed version is
        /// available.
        /// </summary>
        public bool UpdateAvailable
        {
            get 
            {
                return VersionInfo.Compare(LatestCompatibleRelease?.ModuleVersion, CurrentlyInstalled) > 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        public override bool Valid
        {
            get
            {
                return base.Valid && !string.IsNullOrWhiteSpace(DownloadUrl);
            }
        }
    }

    /// <summary>
    /// Extension methods for the ModuleDescription class
    /// </summary>
    public static class ModuleDescriptionExtensions
    {
        /// <summary>
        /// ModuleDescription objects are typically created by deserialising a JSON file so we don't
        /// get a chance at create time to supply supplementary information or adjust values that
        /// may not have been set (eg moduleId). Specifically, this function will set the status and
        /// the moduleDirPath / WorkingDirectory, as well as setting the latest compatible version
        /// from the module's ModuleRelease list. But this could change without notice.
        /// </summary>
        /// <param name="module">This module that requires initialisation</param>
        /// <param name="currentServerVersion">The current version of the server</param>
        /// <param name="moduleDirPath">The path to the folder containing this module</param>
        /// <param name="moduleLocation">The location of this module</param>
        /// <returns>True on success; false otherwise</returns>
        public static void Initialise(this ModuleDescription module, string currentServerVersion, 
                                      string moduleDirPath, ModuleLocation moduleLocation)
        {           
            module.ModuleDirPath    = moduleDirPath;
            module.WorkingDirectory = module.ModuleDirPath; // This once was allowed to be different to moduleDirPath

            // Find the most recent version of this module that's compatible with the current server
            module.CheckVersionAgainstModuleReleases();
            SetLatestCompatibleVersion(module, currentServerVersion);

            // The module.IsCompatible() method is not used here because it doesn't check the
            // LatestCompatibleRelease property. However, it there is a LatestCompatibleRelease,
            // then the module is compatible. 

            module.Status = module.LatestCompatibleRelease is not null 
                            ? ModuleStatusType.Available : ModuleStatusType.NotAvailable;

            // Set the status of all entries based on availability on this platform
            //module.Status = string.IsNullOrWhiteSpace(module?.LatestCompatibleRelease?.ModuleVersion) 
            //              || !module.IsCompatible(currentServerVersion)
            //              ? ModuleStatusType.NotAvailable : ModuleStatusType.Available;
        }

        private static void SetLatestCompatibleVersion(ModuleDescription module, 
                                                       string currentServerVersion)
        {   
            if (module.InstallOptions is null || module.InstallOptions.ModuleReleases is null)
                return;

            module.LatestCompatibleRelease = null;

            foreach (ModuleRelease release in module!.InstallOptions!.ModuleReleases!)
            {
                if (release.ServerVersionRange is null || release.ServerVersionRange.Length < 2)
                    continue;

                string? minServerVersion = release.ServerVersionRange[0];
                string? maxServerVersion = release.ServerVersionRange[1];

                if (string.IsNullOrEmpty(minServerVersion)) minServerVersion = "0.0";
                if (string.IsNullOrEmpty(maxServerVersion)) maxServerVersion = currentServerVersion;

                if (VersionInfo.Compare(minServerVersion, currentServerVersion) <= 0 &&
                    VersionInfo.Compare(maxServerVersion, currentServerVersion) >= 0)
                {
                    if (module.LatestCompatibleRelease is null ||
                        VersionInfo.Compare(module.LatestCompatibleRelease.ModuleVersion, release.ModuleVersion) <= 0)
                    {
                        module.LatestCompatibleRelease = release;
                    }
                }
            }
        }
    }
}