using System.Text.Json.Serialization;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.SDK
{
    /// <summary>
    /// Holds information on a given release of a module.
    /// </summary>
    public class ModuleRelease
    {
        /// <summary>
        /// The version of a module
        /// </summary>
        public string? ModuleVersion { get; set; }

        /// <summary>
        /// The Inclusive range of server versions for which this module version can be installed on
        /// </summary>
        public string[]? ServerVersionRange { get; set; }

        /// <summary>
        /// The date this version was released
        /// </summary>
        public string? ReleaseDate { get; set; }

        /// <summary>
        /// Any notes associated with this release
        /// </summary>
        public string? ReleaseNotes { get; set; }

        /// <summary>
        /// Gets or sets a string indicating how important this update is.
        /// </summary>
        public string? Importance { get; set; }
    }

    /// <summary>
    /// Basic module information shared between module listings for download, and module 
    /// settings on installed modules.
    /// </summary>
    public class PublishingInfo
    {
        /// <summary>
        /// Gets or sets the Description for the module.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the URL of the icon for this module.
        /// </summary>
        public string? IconURL { get; set; }

        /// <summary>
        /// Gets or sets the Category of this module.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the tech stack that this module is based on.
        /// </summary>
        public string? Stack { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? LicenseUrl { get; set; }

        /// <summary>
        /// Gets or sets the author or authors of this module
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the homepage for this module
        /// </summary>
        public string? Homepage { get; set; }

        /// <summary>
        /// Gets or sets the name of the project this module is based on
        /// </summary>
        public string? BasedOn { get; set; }

        /// <summary>
        /// Gets or sets the URL of the project this module is based on
        /// </summary>
        public string? BasedOnUrl { get; set; }
    }

    /// <summary>
    /// The installation options / settings for this module
    /// </summary>
    public class InstallOptions
    {
        /// <summary>
        /// Gets or sets the logging noise level. Quiet = only essentials, Info = anything meaningful,
        /// Loud = the kitchen sink. Default is Info. Note that this value is only effective if 
        /// implemented by the module itself
        /// </summary>
        public ModuleLocation ModuleLocation { get; set; } = ModuleLocation.Internal;

        /// <summary>
        /// Gets or sets the platforms on which this module is supported. Options include: windows,
        /// windows-arm64, linux, linux-arm64, macos, macos-arm64, raspberrypi, orangepi, radxarock,
        /// jetson. If any of these is preceded by a "!" then that platform is specifically not
        /// supported. This allows options such as "linux-arm64, !jetson" to mean all Linux arm64
        /// platforms except NVIDIA Jetson.
        /// </summary>
        public string[] Platforms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the list of module versions and the server version that matches each of
        /// these versions. This determines whether the module can be installed on a given server.
        /// </summary>
        public ModuleRelease[] ModuleReleases { get; set; } = Array.Empty<ModuleRelease>();

        /// <summary>
        /// Gets or sets the list of downloadable models for this module
        /// </summary>
        public ModelConfig[] DownloadableModels { get; set; } = Array.Empty<ModelConfig>();
    }

    /// <summary>
    /// Basic module information shared between module listings for download, and module 
    /// settings on installed modules.
    /// </summary>
    public class ModuleBase
    {
        /// <summary>
        /// Gets or sets the Id of the Module
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the Name to be displayed.
        /// </summary>
        /// 
        [JsonPropertyOrder(1)]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the version of this module
        /// </summary>
        [JsonPropertyOrder(2)]
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the publishing info for this module
        /// </summary>
        [JsonPropertyOrder(3)]
        public PublishingInfo? PublishingInfo { get; set; }

        /// <summary>
        /// Gets or sets the installation options / settings for this module
        /// </summary>
        [JsonPropertyOrder(6)]
        public InstallOptions? InstallOptions { get; set; }

        /// <summary>
        /// Gets or sets the absolute path to this module. 
        /// </summary>
        [JsonIgnore]
        public string ModuleDirPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute path to working directory for this module. 
        /// NOTE: This is not yet used, but here purely as an option.
        /// </summary>
        [JsonIgnore]
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        [JsonIgnore]
        public virtual bool Valid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ModuleId)  &&
                       !string.IsNullOrWhiteSpace(Name)      &&
                       InstallOptions?.Platforms?.Length > 0 &&
                       InstallOptions?.ModuleReleases.Length > 0;
            }
        }

        /// <summary>
        /// The string representation of this module
        /// </summary>
        /// <returns>A string object</returns>
        public override string ToString()
        {
            return $"{Name} ({ModuleId ?? "not set"}) {Version} {PublishingInfo?.License ?? ""}";
        }
    }

    /// <summary>
    /// Extension methods for the ModuleBase class
    /// </summary>
    public static class ModuleBaseExtensions
    {
        /// <summary>
        /// Checks that the version of this module is the same as the highest module version listed
        /// in the InstallOptions.ModuleReleases array. If there's a discrepancy, the version of the
        /// module is reset as the highest in the list, and a warning generated
        /// </summary>
        /// <param name="module">This module</param>
        public static void CheckVersionAgainstModuleReleases(this ModuleBase module)
        {
            if (module is null || !module.Valid)
                return;

            string? maxReleaseVersion = null;
            foreach (ModuleRelease release in module.InstallOptions!.ModuleReleases)
            {
                if (string.IsNullOrWhiteSpace(maxReleaseVersion))
                    maxReleaseVersion = release.ModuleVersion;
                else if (VersionInfo.Compare(maxReleaseVersion, release.ModuleVersion) < 0)
                    maxReleaseVersion = release.ModuleVersion;
            }

            if (!maxReleaseVersion.EqualsIgnoreCase(module.Version))
            {
                Console.WriteLine($"ERROR: Module {module.Name} has version {module.Version}, but ModelReleases has max version as {maxReleaseVersion}");
                module.Version = maxReleaseVersion;
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether or not this module is compatible with the given server
        /// version on the current system.
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="currentServerVersion">The version of the server, or null to ignore version
        /// </param>
        /// <returns>true if the module is available; false otherwise</returns>
        public static bool IsCompatible(this ModuleBase module, string? currentServerVersion)
        {
            if (module is null || !module.Valid)
                return false;

            bool available = true;  // Let's be optimistic

            if (string.IsNullOrWhiteSpace(currentServerVersion))
                available = false;

            string device = SystemInfo.EdgeDevice.Replace(" ", string.Empty);

            // Check Server Version: Check module ModuleReleases list against current server version
            if (available)
            {
                bool versionOK = false;
                if (module.InstallOptions?.ModuleReleases?.Any() ?? false)
                {
                    foreach (ModuleRelease release in module.InstallOptions.ModuleReleases)
                    {
                        if (release.ServerVersionRange is null || release.ServerVersionRange.Length < 2)
                            continue;

                        string? minServerVersion = release.ServerVersionRange[0];
                        string? maxServerVersion = release.ServerVersionRange[1];

                        if (string.IsNullOrEmpty(minServerVersion)) minServerVersion = "0.0";
                        if (string.IsNullOrEmpty(maxServerVersion)) maxServerVersion = currentServerVersion;

                        if (release.ModuleVersion == module.Version &&
                            VersionInfo.Compare(minServerVersion, currentServerVersion) <= 0 &&
                            VersionInfo.Compare(maxServerVersion, currentServerVersion) >= 0)
                        {
                            versionOK = true;
                            break;
                        }
                    }
                }

                available = versionOK;
            }

            // Check module available Platforms unless available platforms has "All"
            if (available && !module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase("all")))
            {
                bool platformOK = false;

                // Check if edge device name appears in list of supported platforms (eg raspberrypi)
                if (!string.IsNullOrWhiteSpace(device) && module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase(device)))
                    platformOK = true;

                // Check if OS and architecture appears in list of supported platforms (eg macos-arm64)
                if (module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase(SystemInfo.OSAndArchitecture))) 
                    platformOK = true;

                // POTENTIAL Check if OS and architecture appears in list of supported platforms (eg macos14-arm64)
                // if (module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase(SystemInfo.OSMajorVersionAndArchitecture))) 
                //    platformOK = true;

                available = platformOK;
            }

            // Now check for exclusions (ie a "!platform" entry in list of available platforms)
            if (available)
            {
                bool platformOK = true;

                // Check if !(edge device) appears in list of supported platforms (eg !raspberrypi)
                if (!string.IsNullOrWhiteSpace(device) && module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase($"!{device}")))
                    platformOK = false;

                // Check if !(OS and architecture) appears in list of supported platforms (eg !macos-arm64)
                if (module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase($"!{SystemInfo.OSAndArchitecture}")))
                    platformOK = false;

                // POTENTIAL Check if OS and architecture appears in list of supported platforms (eg (eg !macos14-arm64))
                // if (module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase($"!{SystemInfo.OSMajorVersionAndArchitecture}"))) 
                //    platformOK = false;

                available = platformOK;
            }

            // Final check: is the module specifically excluded from the current platform?
            return available;
        }
    }
}