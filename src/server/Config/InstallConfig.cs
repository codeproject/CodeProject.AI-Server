using System;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// Installation instance config values.
    /// </summary>
    public class InstallConfig
    {
        internal static string InstallCfgFilename = "installconfig.json";
        internal static string InstallCfgSection  = "install";

        /// <summary>
        /// Gets or sets the Id.
        /// </summary>
        public Guid Id { get; set; } = Guid.Empty;

        /// <summary>
        /// Gets or sets the Version.
        /// </summary>
        public string Version { get; set; } = string.Empty;
    }
}
