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
        /// Gets or sets the platforms on which this module is supported. Options include: windows,
        /// windows-arm64, linux, linux-arm64, macos, macos-arm64, raspberrypi, orangepi, jetson.
        /// If any of these is preceded by a "!" then that platform is specifically not supported.
        /// This allows options such as "linux-arm64, !jetson" to mean all Linux arm64 platforms
        /// except NVIDIA Jetson.
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

            bool versionOK = !string.IsNullOrWhiteSpace(currentServerVersion);

            // First check: Does this module's version encompass a range of server versions that are
            // compatible with the current server?
            if (versionOK)
            {
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
            }

            // Second check: Is this module included in available platforms?
            bool available  = versionOK &&
                             ( module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase("all")) ||
                               module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase(SystemInfo.Platform)) );

            // Third check. In the second check we've checked directly against the current platform.
            // For any non Pi, non-Jetson device, SystemInfo.Platform is windows, mac or linux,
            // possibly with -arm64 appended. For Pi's or Jetson, platform is RaspberryPi, OrangePi
            // or Jetson. But if these aren't specified in the "Platforms" list then this module
            // will be marked as not compatible, so we do a fall-back test based on OS and
            // architecture. If a module is not meant to work for a given OS/architecture then it
            // should include "!Platform" (eg !Jetson) in the Platforms list.
            if (!available && !SystemInfo.Platform.EqualsIgnoreCase(SystemInfo.OSAndArchitecture))
                available = module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase(SystemInfo.OSAndArchitecture));

            // Final check: is the module specifically excluded from the current platform?
            return available && 
                   !module!.InstallOptions!.Platforms!.Any(p => p.EqualsIgnoreCase($"!{SystemInfo.Platform}"));
        }
    }
}