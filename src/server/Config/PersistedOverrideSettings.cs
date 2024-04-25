using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// Manages persisted settings overrides.
    /// </summary>
    public class PersistedOverrideSettings
    {
        private string _storagePath;

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
            if (SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development)
            {
                settingsFilePath = Path.Combine(_storagePath, Constants.DevModuleSettingsFilename);
                if (!File.Exists(settingsFilePath))
                    settingsFilePath = Path.Combine(_storagePath, Constants.ModuleSettingsFilename);
            }
            else
                settingsFilePath = Path.Combine(_storagePath, Constants.ModuleSettingsFilename);

            return await JsonUtils.LoadJsonAsync(settingsFilePath).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves the persisted override settings of the current setup to file.
        /// </summary>
        /// <returns>A JsonObject containing the settings</returns>
        public async Task<bool> SaveSettingsAsync(JsonObject? settings)
        {
            string settingsFilePath = SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development
                                    ? Path.Combine(_storagePath, Constants.DevModuleSettingsFilename)
                                    : Path.Combine(_storagePath, Constants.ModuleSettingsFilename);

            return await JsonUtils.SaveJsonAsync(settings, settingsFilePath).ConfigureAwait(false);
        }
    }
}
