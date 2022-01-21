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
        /// <param name="versionOptions">The version Options instance.</param>
        /// <param name="installOptions">The install Options instance.</param>
        /// <param name="configuration">The Configuration instance</param>
        public VersionService(IOptions<VersionConfig> versionOptions,
                              IOptions<InstallConfig> installOptions,
                              IConfiguration configuration)
        {
            Configuration = configuration;
            VersionConfig = versionOptions.Value;
            InstallConfig = installOptions.Value;
        }

        /// <summary>
        /// Gets the application Configuration.
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// Gets the version info for the current instance.
        /// </summary>
        public VersionConfig VersionConfig { get; }

        /// <summary>
        /// Gets the install config for the current instance.
        /// </summary>
        public InstallConfig InstallConfig { get; }

        /// <summary>
        /// Gets the latest version available for download
        /// </summary>
        public async Task<VersionInfo?> GetLatestVersion()
        {
            VersionInfo? version = null;

            if (_client is null)
            {
                _client = new HttpClient { Timeout = new TimeSpan(0, 0, 30) };
                // Sending this allows us to store some state on the server's side between status
                // calls. Not used at the moment, but will be in the future. 
                // SECURITY: Always ensure that InstallConfig.Id does not contain personally
                //           identifiable information. It should just be a random GUID that can be
                //           wiped or replaced on the installation side without issue.
                _client.DefaultRequestHeaders.Add("X-CPSense-Install", InstallConfig.Id.ToString());
            }

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
                        // URL. We return just the name as a naive protection against man in the
                        // middle attacks. The actual URL we send the user to will come from the
                        // local config settings.
                        if (!string.IsNullOrWhiteSpace(version.File))
                        {
                            string updateDownloadUrl = Configuration.GetValue<string>("UpdateDownloadUrl");
                            version.File = updateDownloadUrl;
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
