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
        /// correctness before being loaded.
        /// </summary>
        /// <param name="config">The configuration builder</param>
        /// <param name="settingsFile">The path to the settings file</param>
        /// <param name="optional">Is this an optional file</param>
        /// <param name="reloadOnChange">Reload if the file changes?</param>
        /// <returns>true on success; false otherwise</returns>
        public static bool AddJsonFileSafe(this IConfigurationBuilder config, string settingsFile,
                                            bool optional, bool reloadOnChange)
        {
            // Test the file exists
            if (!File.Exists(settingsFile))
                return false;

            // Test the file contents
            try
            {
                string contents  = File.ReadAllText(settingsFile);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var settings = JsonSerializer.Deserialize<JsonObject>(contents, options);
            }
            catch (Exception ex)
            {
                string error = $"Error loading {settingsFile}: {ex.Message}";
                Console.WriteLine("Error: " + error);

                return false;
            }

            config.AddJsonFile(settingsFile, optional: optional, reloadOnChange: reloadOnChange);

            return true;
        }
    }
}