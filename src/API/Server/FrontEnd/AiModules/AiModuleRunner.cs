using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.API.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend AI Analysis modules.
    /// </summary>
    public class AiModuleRunner : BackgroundService
    {
        private readonly VersionConfig         _versionConfig;
        private readonly ServerOptions         _serverOptions;
        private readonly ILogger<AiModuleRunner> _logger;
        private readonly AiModuleInstaller       _moduleInstaller;

        // TODO: this really should be a singleton global that is initialized from the configuration
        //       but can be updated after.
        private readonly ModuleCollection      _modules;

        // This gets returned by the Modules property so could end up non-empty. We don't populate
        // it in our code, but there is potential that this object may be modified. Maybe a name
        // change?
        private readonly ModuleCollection _emptyModuleList = new();

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
        public ModuleCollection Modules => _modules ?? _emptyModuleList;

        /// <summary>
        /// Gets a collection of the processes names and statuses.
        /// </summary>
        public ModuleProcessServices ProcessStatuses { get; }

        /// <summary>
        /// Gets a reference to the ModuleSettings object.
        /// </summary>
        public ModuleSettings ModuleSettings { get; }

        /// <summary>
        /// Returns a module with the given module ID, or null if none found.
        /// </summary>
        /// <param name="moduleId">The module ID</param>
        /// <returns>A ModuleConfig object, or null if non found</returns>
        public ModuleConfig? GetModule(string moduleId) => Modules.GetModule(moduleId);

        /// <summary>
        /// Initialises a new instance of the AiModuleRunner.
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="serverOptions">The server Options</param>
        /// <param name="modules">The Modules configuration.</param>
        /// <param name="moduleSettings">The Module settings manager object.</param>
        /// <param name="moduleInstaller">The AiModuleInstaller.</param>
        /// <param name="processStatuses">The Module Process Status Service.</param>
        /// <param name="logger">The logger.</param>
        public AiModuleRunner(IOptions<VersionConfig> versionOptions,
                            IOptions<ServerOptions> serverOptions,
                            IOptions<ModuleCollection> modules,
                            ModuleSettings moduleSettings,
                            AiModuleInstaller moduleInstaller,
                            ModuleProcessServices processStatuses,   
                            ILogger<AiModuleRunner> logger)
        {
            _versionConfig   = versionOptions.Value;
            _serverOptions   = serverOptions.Value;
            _modules         = modules.Value;
            ModuleSettings   = moduleSettings;
            _moduleInstaller = moduleInstaller;
            ProcessStatuses  = processStatuses;
            _logger          = logger;

            // The very first thing we need to do is twofold:
            // 1. Remove invalid modules. This can happen in the case where a module was installed,
            //    a setting for that module then persisted in the settings override .json file, and
            //    then the module is removed. Our config system will load up the persisted override
            //    settings and see some settings for the (now removed) module, and add that fragment
            //    of a module settings to the modules list, resulting in an invalid module in the
            //    list.
            // 2. Update missing or optional properties. In the Json settings file, ModuleId is
            //    specified as a key for the module info object, but not a property. Queue can be
            //    specified, or can be left blank to allow a default to be used. Fix these, and
            //    a couple of other things up.

            _logger.LogInformation($"** Server version:   {_versionConfig.VersionInfo!.Version}");

            List<string> keys = _modules!.Keys.ToList();
            foreach (string moduleId in keys)
            {
                ModuleConfig? module = _modules[moduleId];

                // Complete the ModuleConfig's setup
                if (module is not null)
                {
                    module.Initialise(moduleId, moduleSettings.ModulesPath,
                                      moduleSettings.PreInstalledModulesPath);
                }
                
                if (module is null || !module.Valid)
                    _modules.Remove(moduleId, out _);
            }               

            foreach (ModuleConfig module in _modules.Values)
            {
                var status = module.Available(SystemInfo.Platform, _versionConfig.VersionInfo?.Version)
                           ? (module.AutoStart == true ? ProcessStatusType.Enabled 
                                                       : ProcessStatusType.NotEnabled)
                           : ProcessStatusType.NotAvailable;

                try 
                {
                    ProcessStatuses.Add(module.ModuleId!, new ProcessStatus()
                    {
                        ModuleId = module.ModuleId,
                        Name     = module.Name,
                        Status   = status
                    });
                }
                catch
                {
                    _logger.LogError($"Unable to add {module.ModuleId!} to process list");
                }
            }

#if DEBUG
            // Create a modules.json file each time we run
            Task.Run(() => _modules.CreateModulesListing("modules.json"));
#endif
        }

        /// <inheritdoc></inheritdoc>
        public async override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("ModuleRunner Start");
            await base.StartAsync(cancellationToken);
        }

        /// <inheritdoc></inheritdoc>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("ModuleRunner Stop");

            var tasks = new List<Task>();
            foreach (var module in _modules.Values)
                tasks.Add(KillProcess(module));

            await Task.WhenAll(tasks);

            await base.StopAsync(cancellationToken);
        }

        /// <inheritdoc></inheritdoc>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(100); // let everything else start up as well

            if (_modules is null)
            {
                _logger.LogError("No Background AI Modules specified");
                return;
            }

            _logger.LogTrace("Starting Background AI Modules");

            bool launchModules            = ModuleSettings.LaunchModules;
            int  preLaunchModuleDelaySecs = ModuleSettings.DelayBeforeLaunchingModulesSecs;

            // Setup routes.  Do this first so they are active during debug without launching services.
            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                if (!module.Valid)
                    continue;

                // This is the startup of the server, but we may not actually be starting all the 
                // modules. Ensure the process reflects our needs on whether we need it started.
                if (!ProcessStatuses.SetPreLaunchProcessStatus(module, launchModules))
                    continue;

                // Create the required Queues even if launchModules=false. This allows the server
                // to listen on that queeu should the module be started by something other than the
                // server. Eg a debugger.
                ProcessStatuses.SetupQueueAndRoutes(module);
            }

            if (!launchModules)
            {
                _logger.LogWarning("Skipping Background AI Modules startup");
            }
            else {
                // Let's make sure the front end is up and running before we start the backend 
                // analysis services
                await Task.Delay(TimeSpan.FromSeconds(preLaunchModuleDelaySecs), stoppingToken);

                foreach (var entry in _modules!)
                {
                    ModuleConfig? module = entry.Value;
                    string moduleId = entry.Key;

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    if (!module.Valid)
                        continue;

                    ProcessStatus? status = ProcessStatuses.GetProcessStatus(moduleId);
                    if (status == null)
                        continue;

                    await StartProcess(module);
                }
            }

            // Install Initial Modules last so already installed modules will run
            // while the installations are happening.
            if (SystemInfo.ExecutionEnvironment != ExecutionEnvironment.Docker)
            {
                _logger.LogInformation("Installing Initial Modules.");
                await _moduleInstaller.InstallInitialModules();
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
            _logger.LogInformation("ModuleRunner Stopped");
        }

        /// <summary>
        /// Kills a process
        /// </summary>
        /// <param name="module">The module for the process to be killed</param>
        /// <returns>true on success</returns>
        public Task<bool> KillProcess(ModuleConfig module)
        {
            return ProcessStatuses.KillProcess(module);
        }

        /// <summary>
        /// Starts, or restarts if necessary, a process.
        /// </summary>
        /// <param name="module">The module to be started</param>
        /// <returns>True on success; false otherwise</returns>
        public Task<bool> StartProcess(ModuleConfig module)
        {
            return ProcessStatuses.StartProcess(module);
        }

        /// <summary>
        /// Stops, if necessary, and then restarts a process. Handy if settings have changed and we
        /// need the process to be updated.
        /// </summary>
        /// <param name="module">The module to be restarted</param>
        /// <returns>True on success; false otherwise</returns>
        public Task<bool> RestartProcess(ModuleConfig module)
        {
            return ProcessStatuses.RestartProcess(module);
        }
    }
}
