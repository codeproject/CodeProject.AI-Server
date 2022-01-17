using System;
using Microsoft.Extensions.Configuration;

using CodeProject.SenseAI.API.Common;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The Startup class
    /// </summary>
    public class VersionService
    {
        private static HttpClient? _client;

        /// <summary>
        /// Initializs a new instance of the Startup class.
        /// </summary>
        /// <param name="options">The Options instance.</param>
        /// <param name="configuration">The Configuration instance</param>
        public VersionService(IOptions<VersionInfo> options, IConfiguration configuration)
        {
            Configuration = configuration;
            VersionInfo   = options.Value;
        }

        /// <summary>
        /// Gets the application Configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the version info for the current instance.
        /// </summary>
        public VersionInfo VersionInfo { get; }

        /// <summary>
        /// Gets the latest version available for download
        /// </summary>
        public async Task<VersionInfo?> GetLatestVersion()
        {
            VersionInfo? version = null;

            if (_client is null)
                _client = new HttpClient { Timeout = new TimeSpan(0, 0, 30) };

            string updateCheckUrl = Configuration.GetValue<string>("UpdateCheckUrl");

            try
            {
                string data = await _client.GetStringAsync(updateCheckUrl);
                if (!string.IsNullOrWhiteSpace(data))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    version = JsonSerializer.Deserialize<VersionInfo>(data, options);
                    if (version is not null)
                    {
                        // A small adjustment. The version info contains the file *name* not a file
                        // URL, and that name is relative to the official download location. We
                        // return just the name as a naive protection against man in the middle
                        // attacks. The URL we send the user to will come from the local config
                        // settings.
                        if (!string.IsNullOrWhiteSpace(version.File))
                        {
                            string updateDownloadUrl = Configuration.GetValue<string>("UpdateDownloadUrl");
                            version.File = updateDownloadUrl + version.File;
                        }

                        Common.Logger.Log($"Latest version available is {version.Version}");
                    }
                }
            }
            catch (Exception e)
            {
                Common.Logger.Log($"Error checking for latest version: " + e.Message);
            }

            return version;
        }
    }
}
