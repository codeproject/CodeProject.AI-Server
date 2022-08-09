using System;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.API.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend processes.
    /// </summary>
    public class VersionProcessRunner : BackgroundService
    {
        private readonly VersionService _versionService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initialises a new instance of the VersionProcessRunner.
        /// </summary>
        /// <param name="versionService">The Queue management service.</param>
        /// <param name="logger">The logger</param>
        public VersionProcessRunner(VersionService versionService, 
                                    ILogger<VersionProcessRunner> logger)
        {
            _versionService = versionService;
            _logger = logger;
        }

        /// <inheritdoc></inheritdoc>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let's make sure the front end is up and running before we start the version process
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            CheckCurrentVersion();
        }

        private async void CheckCurrentVersion()
        {
            // Grab the latest version info
            if (_versionService != null)
            {
                VersionInfo? latest = await _versionService.GetLatestVersion();
                if (latest != null && _versionService.VersionConfig?.VersionInfo != null)
                {
                    _logger.LogDebug($"Current Version is {_versionService.VersionConfig.VersionInfo.Version}");

                    int compare = VersionInfo.Compare(_versionService.VersionConfig.VersionInfo, latest);
                    if (compare < 0)
                    {
                        if (latest.SecurityUpdate ?? false)
                            _logger.LogInformation($" *** A SECURITY UPDATE {latest.Version} is available ** ");
                        else
                            _logger.LogInformation($" *** A new version {latest.Version} is available ** ");
                    }
                    else if (compare == 0)
                        _logger.LogInformation("Server: This is the latest version");
                    else
                        _logger.LogInformation("Server: This is a new, unreleased version");
                }
            }
        }
    }

    /// <summary>
    /// Extension methods for the VersionProcessRunner.
    /// </summary>
    public static class VersionProcessRunnerExtensions
    {
        /// <summary>
        /// Sets up the VersionProcessRunner.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <returns></returns>
        public static IServiceCollection AddVersionProcessRunner(this IServiceCollection services)
        {
            services.AddHostedService<VersionProcessRunner>();
            return services;
        }
    }
}
