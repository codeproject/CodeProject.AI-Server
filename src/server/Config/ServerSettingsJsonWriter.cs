using CodeProject.AI.SDK.Common;
using Microsoft.Extensions.Configuration;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// Saves the current mesh settings to file.
    /// </summary>
    public class ServerSettingsJsonWriter
    {
        private string _storagePath;
        
        private JsonSerializerOptions _jsonSerializeOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling         = JsonCommentHandling.Skip,
            AllowTrailingCommas         = true,
            WriteIndented               = true
        };

        /// <summary>
        /// Initialises an instance of the ServerSettingsJsonWriter class
        /// </summary>
        public ServerSettingsJsonWriter(IConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _storagePath = config["ApplicationDataDir"] 
                ?? throw new IndexOutOfRangeException("ApplicationDataDir not configured");
        }

        /// <summary>
        /// Saves the mesh settings to file.
        /// </summary>
        /// <returns>A JsonObject containing the settings</returns>
        /// <remarks>
        /// We will add other settings such as Queue, Server and Module settings to this persisted
        /// file. All settings relate to how the server manages its responsibilities. Specifically,
        /// the module settings define how the server manages the modules, but does not include
        /// settings on how the module itself behaves internally.
        /// </remarks>
        public async Task<bool> SaveSettingsAsync(MeshOptions options)
        {
            string settingsFilePath = SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development
                                    ? Path.Combine(_storagePath, Constants.DevServerSettingsFilename)
                                    : Path.Combine(_storagePath, Constants.ServerSettingsFilename);

            // TODO: Add Queue, Server and Module settings as well
            string settings = JsonSerializer.Serialize(options, _jsonSerializeOptions);
            settings = $"{{\n  \"{nameof(MeshOptions)}\": {settings}\n}}";

            try
            {
                await File.WriteAllTextAsync(settingsFilePath, settings)
                          .ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving mesh settings: {ex.Message}");
                return false;
            }
        }
    }
}
