namespace CodeProject.AI.SDK.Utils
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Contains extensions methods for the IConfigurationBuilder
    /// </summary>
    public static class ConfigExtensions
    {
        /// <summary>
        /// Adds a JSON config file to the config object. The file is tested for existence and
        /// correctness before being loaded. It is added to config if it exists and is valid, and if
        /// it does not exist.
        /// </summary>
        /// <param name="config">The configuration builder</param>
        /// <param name="settingsFile">The path to the settings file</param>
        /// <param name="optional">Is this an optional file</param>
        /// <param name="reloadOnChange">Reload if the file changes?</param>
        /// <returns>true on success; false otherwise</returns>
        public static bool AddJsonFileSafe(this IConfigurationBuilder config, string settingsFile,
                                           bool optional, bool reloadOnChange)
        {
            bool fileExists = File.Exists(settingsFile);

            // If reloadOnChange = true then what we're actually doing is registering a file for
            // monitoring, even if that file doesn't yet exist. So we still need to add ("register")
            // the file if reloadOnChange is true, even if it doesn't exist. Otherwise bail.
            if (!reloadOnChange && !fileExists)
                return false;

            // If the file exists then test that it's actually well-formed and loadable.
            if (fileExists && !JsonUtils.IsJsonFileValid(settingsFile))
                return false;

            // Either the file exists, or it doesn't exist but reloadOnChange=true so we need to
            // register it.
            config.AddJsonFile(settingsFile, optional: optional, reloadOnChange: reloadOnChange);

            return true;
        }
    }
}