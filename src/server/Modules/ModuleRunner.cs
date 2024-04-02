using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.Server.Mesh;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend AI Analysis modules.
    /// </summary>
    public class ModuleRunner : BackgroundService
    {
        private readonly VersionConfig         _versionConfig;
        private readonly ServerOptions         _serverOptions;
        private readonly ILogger<ModuleRunner> _logger;
        private readonly ModuleInstaller       _moduleInstaller;

        // TODO: this really should be a singleton global that is initialized from the configuration
        //       but can be updated after.
        private readonly ModuleCollection      _installedModules;

        // This gets returned by the Modules property so could end up non-empty. We don't populate
        // it in our code, but there is potential that this object may be modified. Maybe a name
        // change?
        private readonly ModuleCollection      _emptyModuleList = new();

        /// <summary>
        /// Gets the environment variables applied to all processes.
        /// </summary>
        public Dictionary<string, object>? GlobalEnvironmentVariables
        {
            get { return _serverOptions?.EnvironmentVariables; }
        }

        /// <summary>
        /// Gets a list of the startup processes.
        /// </summary>
        public ModuleCollection InstalledModules => _installedModules ?? _emptyModuleList;

        /// <summary>
        /// Gets a collection of the processes names and statuses.
        /// </summary>
        public ModuleProcessServices ProcessService { get; }

        private readonly MeshMonitor _meshMonitor;

        /// <summary>
        /// Gets a reference to the ModuleSettings object.
        /// </summary>
        public ModuleSettings ModuleSettings { get; }

        /// <summary>
        /// Returns a module with the given module ID, or null if none found.
        /// </summary>
        /// <param name="moduleId">The module ID</param>
        /// <returns>A ModuleConfig object, or null if non found</returns>
        public ModuleConfig? GetModule(string moduleId) => InstalledModules.GetModule(moduleId);

        /// <summary>
        /// Initialises a new instance of the ModuleRunner.
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="serverOptions">The server Options</param>
        /// <param name="moduleOptions">The Modules configuration.</param>
        /// <param name="moduleSettings">The Module settings manager object.</param>
        /// <param name="moduleInstaller">The ModuleInstaller.</param>
        /// <param name="processService">The Module Process Status Service.</param>
        /// <param name="meshMonitor">The mesh monitor.</param>
        /// <param name="logger">The logger.</param>
        public ModuleRunner(IOptions<VersionConfig> versionOptions,
                            IOptions<ServerOptions> serverOptions,
                            IOptions<ModuleCollection> moduleOptions,
                            ModuleSettings moduleSettings,
                            ModuleInstaller moduleInstaller,
                            ModuleProcessServices processService, 
                            MeshMonitor meshMonitor,
                            ILogger<ModuleRunner> logger)
        {
            _versionConfig    = versionOptions.Value;
            _serverOptions    = serverOptions.Value;
            _installedModules = moduleOptions.Value;
            ModuleSettings    = moduleSettings;
            _moduleInstaller  = moduleInstaller;
            ProcessService    = processService;
            _meshMonitor      = meshMonitor;
            _logger           = logger;

            _logger.LogInformation($"** Server version:   {_versionConfig.VersionInfo!.Version}");
#if DEBUG
            // Create a modules.json file each time we run
            string path = Path.Combine(ModuleSettings.DownloadedModulePackagesDirPath,
                                       Constants.ModulesListingFilename);
            Task.Run(() => _installedModules.CreateModulesListing(path, _versionConfig.VersionInfo));
#endif
        }

        /// <inheritdoc></inheritdoc>
        public async override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("ModuleRunner Start");
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc></inheritdoc>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("ModuleRunner Stop");

            var tasks = new List<Task>();
            foreach (var module in _installedModules.Values)
                tasks.Add(KillProcess(module));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc></inheritdoc>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(100).ConfigureAwait(false); // let everything else start up as well

            if (_installedModules is null)
            {
                _logger.LogError("No Background AI Modules specified");
                return;
            }

            _logger.LogTrace("Starting Background AI Modules");

            bool launchModules            = ModuleSettings.LaunchModules;
            int  preLaunchModuleDelaySecs = ModuleSettings.DelayBeforeLaunchingModulesSecs;

            // Setup routes.  Do this first so they are active during debug without launching services.
            foreach (var entry in _installedModules!)
            {
                ModuleConfig? module = entry.Value;
                if (!module.Valid)
                    continue;

                // Add the processes (meaning: setup the process for listing and create the required
                // Queues) even if launchModules=false. This allows the server to list the processes
                // and also to listen on the queue for the process's module in case the module is
                // started by something other than the server. Eg a debugger.
                string? installSummary = await _moduleInstaller.GetInstallationSummaryAsync(module!.ModuleId!)
                                                               .ConfigureAwait(false);
                ProcessService.AddProcess(module, launchModules, installSummary);
            }

            if (launchModules)
            {
                // Let's make sure the front end is up and running before we start the backend 
                // analysis services
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(preLaunchModuleDelaySecs), stoppingToken)
                              .ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                }

                foreach (var entry in _installedModules!)
                {
                    ModuleConfig? module = entry.Value;
                    string moduleId = entry.Key;

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    if ((module?.Valid ?? false) == false)
                        continue;

                    await StartProcess(module).ConfigureAwait(false);
                }
            }
            else
            {
                _logger.LogWarning("Skipping Background AI Modules startup");
            }

            // Install Initial Modules last so already installed modules will run
            // while the installations are happening.
            if (!SystemInfo.IsDocker)
                await _moduleInstaller.InstallInitialModules().ConfigureAwait(false);

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
            }

            await _meshMonitor.StopMonitoringAsync();
            _logger.LogInformation("ModuleRunner Stopped");
        }

        /// <summary>
        /// Kills a process
        /// </summary>
        /// <param name="module">The module for the process to be killed</param>
        /// <returns>true on success</returns>
        public Task<bool> KillProcess(ModuleConfig module)
        {
            return ProcessService.KillProcess(module);
        }

        /// <summary>
        /// Starts, or restarts if necessary, a process.
        /// </summary>
        /// <param name="module">The module to be started</param>
        /// <returns>True on success; false otherwise</returns>
        public async Task<bool> StartProcess(ModuleConfig module)
        {
            if (module?.ModuleId is null)
                return false;

            if (!ProcessService.TryGetProcessStatus(module.ModuleId, out ProcessStatus? _))
            {
                string? installSummary = await _moduleInstaller.GetInstallationSummaryAsync(module.ModuleId)
                                                               .ConfigureAwait(false);
                ProcessService.AddProcess(module, true, installSummary);
            }

            return await ProcessService.StartProcess(module, null);
        }

        /// <summary>
        /// Stops, if necessary, and then restarts a process. Handy if settings have changed and we
        /// need the process to be updated.
        /// </summary>
        /// <param name="module">The module to be restarted</param>
        /// <returns>True on success; false otherwise</returns>
        public Task<bool> RestartProcess(ModuleConfig module)
        {
            return ProcessService.RestartProcess(module);
        }
    }
}
