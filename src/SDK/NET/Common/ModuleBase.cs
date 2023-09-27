using System.Text.Json.Serialization;

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
    public class ModuleBase
    {
        /// <summary>
        /// Gets or sets the Id of the Module
        /// </summary>
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the Name to be displayed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the platforms on which this module is supported.
        /// </summary>
        public string[] Platforms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the number of MB of memory needed for this module to perform operations.
        /// If null, then no checks done.
        /// </summary>
        public int? RequiredMb { get; set; }

        /// <summary>
        /// Gets or sets the Description for the module.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the version of this module
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the list of module versions and the server version that matches
        /// each of these versions.
        /// </summary>
        public ModuleRelease[] ModuleReleases { get; set; } = Array.Empty<ModuleRelease>();

        /// <summary>
        /// Gets or sets the legacy structure containing a list of module versions and the server 
        /// version that matches each of these versions. This name is deprecated and is only here so
        /// we can read old modulesettings files. Once read these values will be transferred to
        /// ModuleReleases. Deprecated not just because it was a bad name, but also becauase it was
        /// a badly *spelled* name.
        /// </summary>
        public ModuleRelease[] VersionCompatibililty { get; set; } = Array.Empty<ModuleRelease>();

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? LicenseUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this module was pre-installed (eg Docker).
        /// If the module was preinstalled, this value is true, otherwise false.
        /// </summary>
        public bool PreInstalled { get; set; } = false;

        /// <summary>
        /// Gets or sets the absolute path to this module. 
        /// NOTE: This is not yet used, but here purely as an option.
        /// </summary>
        [JsonIgnore]
        public string ModulePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the absolute path to working directory for this module. 
        /// NOTE: This is not yet used, but here purely as an option.
        /// </summary>
        [JsonIgnore]
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// The string representation of this module
        /// </summary>
        /// <returns>A string object</returns>
        public override string ToString()
        {
            return $"{Name} ({ModuleId}) {Version} {License ?? ""}";
        }
    }
}