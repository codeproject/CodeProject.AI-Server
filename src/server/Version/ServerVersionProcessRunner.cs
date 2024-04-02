using System;
using System.Threading;
using System.Threading.Tasks;
using CodeProject.AI.SDK.API;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// Runs a service at startup to check for the latest version of this application.
    /// </summary>
    public class ServerVersionProcessRunner : BackgroundService
    {
        private readonly ServerVersionService _versionService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initialises a new instance of the ServerVersionProcessRunner.
        /// </summary>
        /// <param name="versionService">The Queue management service.</param>
        /// <param name="logger">The logger</param>
        public ServerVersionProcessRunner(ServerVersionService versionService, 
                                          ILogger<ServerVersionProcessRunner> logger)
        {
            _versionService = versionService;
            _logger         = logger;
        }

        /// <inheritdoc></inheritdoc>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let's make sure the front end is up and running before we start the version process
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }
            
            await CheckCurrentVersionAsync().ConfigureAwait(false);
        }

        private async Task CheckCurrentVersionAsync()
        {
            // Grab the latest version info
            if (_versionService != null)
            {
                VersionInfo? latest = await _versionService.GetLatestVersion().ConfigureAwait(false);
                if (latest != null && _versionService.VersionConfig?.VersionInfo != null)
                {
                    _logger.LogDebug($"Current Version is {_versionService.VersionConfig.VersionInfo.Version}");

                    int compare = VersionInfo.Compare(_versionService.VersionConfig.VersionInfo, latest);
                    if (compare < 0)
                    {
                        if (latest.SecurityUpdate)
                            _logger.LogInformation($"*** A SECURITY UPDATE {latest.Version} is available");
                        else
                            _logger.LogInformation($"*** A new version {latest.Version} is available");
                    }
                    else if (compare == 0)
                        _logger.LogInformation("Server: This is the latest version");
                    else
                        _logger.LogInformation("*** Server: This is a new, unreleased version");
                }
            }
        }
    }

    /// <summary>
    /// Extension methods for the ServerVersionProcessRunner.
    /// </summary>
    public static class ServerVersionProcessRunnerExtensions
    {
        /// <summary>
        /// Sets up the ServerVersionProcessRunner.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <param name="configuration">The configuration.</param>
        /// <returns></returns>
        public static IServiceCollection AddVersionProcessRunner(this IServiceCollection services,
                                                                 IConfiguration configuration)
        {
            services.Configure<VersionConfig>(configuration.GetSection(VersionConfig.VersionCfgSection));
            services.AddSingleton<ServerVersionService, ServerVersionService>();
            services.AddHostedService<ServerVersionProcessRunner>();
            return services;
        }
    }
}
