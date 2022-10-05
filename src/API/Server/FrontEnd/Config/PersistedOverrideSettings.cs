using CodeProject.AI.API.Common;

using System.IO;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// Manages persisted settings overrides.
    /// </summary>
    public class PersistedOverrideSettings
    {
        internal static string SettingsFilename    = "modulesettings.json";
        internal static string DevSettingsFilename = "modulesettings.development.json";

        private string _storagePath;

        /// <summary>
        /// Gets a value indicating whether we are currently in a development environment.
        /// </summary>
        public bool IsDevelopment
        {
            get
            {
                return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?
                                  .ToLower() == "development";
            }
        }

        /// <summary>
        /// Initialises an instance of the PersistedSettings class
        /// </summary>
        public PersistedOverrideSettings(string storagePath)
        {
            _storagePath = storagePath;
        }

        /// <summary>
        /// Loads a file containing the persisted override settings of the current setup
        /// </summary>
        /// <returns>A JsonObject containing the settings</returns>
        public async Task<JsonObject?> LoadSettings()
        {
            // In Dev, we'll try loading up the dev settings first, but if they don't exist load
            // production values (but in dev we only save to dev)
            string settingsFilePath;
            if (IsDevelopment)
            {
                settingsFilePath = Path.Combine(_storagePath, DevSettingsFilename);
                if (!File.Exists(settingsFilePath))
                    settingsFilePath = Path.Combine(_storagePath, SettingsFilename);
            }
            else
                settingsFilePath = Path.Combine(_storagePath, SettingsFilename);

            return await ModuleConfigExtensions.LoadSettings(settingsFilePath);
        }

        /// <summary>
        /// Saves the persisted override settings of the current setup to file.
        /// </summary>
        /// <returns>A JsonObject containing the settings</returns>
        public async Task<bool> SaveSettings(JsonObject? settings)
        {
            string settingsFilePath = IsDevelopment
                                    ? Path.Combine(_storagePath, DevSettingsFilename)
                                    : Path.Combine(_storagePath, SettingsFilename);

            return await ModuleConfigExtensions.SaveSettings(settings, settingsFilePath);
        }
    }
}
