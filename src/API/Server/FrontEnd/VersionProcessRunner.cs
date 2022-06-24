using System;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.API.Common;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend processes.
    /// </summary>
    public class VersionProcessRunner : BackgroundService
    {
        private readonly VersionService _versionService;

        /// <summary>
        /// Initialises a new instance of the VersionProcessRunner.
        /// </summary>
        /// <param name="versionService">The Queue management service.</param>
        public VersionProcessRunner(VersionService versionService)
        {
            _versionService = versionService;
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
                    Logger.Log($"Version check: Current Version is {_versionService.VersionConfig.VersionInfo.Version}");

                    int compare = VersionInfo.Compare(_versionService.VersionConfig.VersionInfo, latest);
                    if (compare < 0)
                    {
                        if (latest.SecurityUpdate ?? false)
                            Logger.Log($" ** A SECURITY UPDATE {latest.Version} is available ** ");
                        else
                            Logger.Log($" ** A new version {latest.Version} is available ** ");
                    }
                    else if (compare == 0)
                        Logger.Log("Version check: This is the latest version");
                    else
                        Logger.Log("Version check: This is a new, unreleased version");
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
