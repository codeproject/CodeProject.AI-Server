using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// The Startup class
    /// </summary>
    public class ServerVersionService
    {
        private static HttpClient? _client;
        private readonly ILogger _logger;
        private readonly ServerOptions _serverOptions;

        /// <summary>
        /// Initializes a new instance of the Startup class.
        /// </summary>
        /// <param name="versionOptions">The version Options instance.</param>
        /// <param name="installOptions">The install Options instance.</param>
        /// <param name="serverOptions">The module options instance</param>
        /// <param name="configuration">The Configuration instance</param>
        /// <param name="logger">The logger</param>
        public ServerVersionService(IOptions<VersionConfig> versionOptions,
                                    IOptions<InstallConfig> installOptions,
                                    IOptions<ServerOptions> serverOptions,
                                    IConfiguration configuration,
                                    ILogger<ServerVersionService> logger)
        {
            Configuration  = configuration;
            VersionConfig  = versionOptions.Value;
            InstallConfig  = installOptions.Value;
            _serverOptions = serverOptions.Value;
            _logger        = logger;
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

            if (_serverOptions.AllowInternetAccess == false)
                return version;

            if (_client is null)
            {
                _client = new HttpClient { Timeout = new TimeSpan(0, 0, 30) };

                try
                {
                    // This allows us to store some state on the server's side between status calls. 
                    // This is analogous to a session cookie.
                    // SECURITY: Always ensure that InstallConfig.Id does NOT contain personally
                    //           identifiable information. It should be a random GUID that can be
                    //           wiped or replaced on the installation side without issue.
                    _client.DefaultRequestHeaders.Add("X-CPAI-Server-Install", InstallConfig.Id.ToString());

                    // Handy to allow the checkee to return emergency info if the current installed
                    // version has issues. IMPORTANT: no personal information can be sent here. This
                    //  is purely things like OS / GPU.
                    string currentVersion = VersionConfig.VersionInfo?.Version ?? string.Empty;
                    _client.DefaultRequestHeaders.Add("X-CPAI-Server-Version", currentVersion);
                    
                    var sysProperties = SystemInfo.Summary;
                    var systemInfoJson = JsonSerializer.Serialize(sysProperties);
                    _client.DefaultRequestHeaders.Add("X-CPAI-Server-SystemInfo", systemInfoJson);
                }
                catch
                { }
            }

            try
            {
                string data = await _client.GetStringAsync(_serverOptions.ServerVersionCheckUrl)
                                           .ConfigureAwait(false);
                                           
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
                            string serverDownloadUrl = _serverOptions.ServerDownloadUrl!;
                            version.File = serverDownloadUrl;
                        }

                        // _logger.LogInformation($"Latest version available is {version.Version}");
                    }
                }
            }
            catch (Exception /* e*/)
            {
                // _logger.LogError($"Error checking for latest version: " + e.Message);
            }

            return version;
        }
    }
}
