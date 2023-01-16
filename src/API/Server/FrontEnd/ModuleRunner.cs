using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.API.Common;
using CodeProject.AI.API.Server.Backend;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend AI Analysis modules.
    /// </summary>
    public class ModuleRunner : BackgroundService
    {
        private readonly ServerOptions               _serverOptions;
        private readonly ModuleSettings              _moduleSettings;
        private readonly ILogger<ModuleRunner>       _logger;
        private readonly QueueServices               _queueServices;
        private readonly BackendRouteMap             _routeMap;
        private readonly Dictionary<string, Process> _runningProcesses = new();

        // TODO: this really should be a singleton global that is initialized
        //       from the configuration but can be updated after.
        private readonly ModuleCollection _modules;

        // Tracks the status of each module
        private readonly Dictionary<string, ProcessStatus> _processStatuses;

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
        public Dictionary<string, ProcessStatus> ProcessStatuses => _processStatuses;

        /// <summary>
        /// Gets a reference to the ModuleSettings object.
        /// </summary>
        public ModuleSettings ModuleSettings => _moduleSettings;

        /// <summary>
        /// Returns a module with the given module ID, or null if none found.
        /// </summary>
        /// <param name="moduleId">The module ID</param>
        /// <returns>A ModuleConfig object, or null if non found</returns>
        public ModuleConfig? GetModule(string moduleId) => Modules.GetModule(moduleId);

        /// <summary>
        /// Initialises a new instance of the ModuleRunner.
        /// </summary>
        /// <param name="serverOptions">The server Options</param>
        /// <param name="modules">The Modules configuration.</param>
        /// <param name="moduleSettings">The Module settings manager object.</param>
        /// <param name="queueServices">The Queue management service.</param>
        /// <param name="routeMap">The RouteMap service.</param>
        /// <param name="logger">The logger.</param>
        public ModuleRunner(IOptions<ServerOptions> serverOptions,
                            // IOptions<ModuleOptions> moduleOptions,
                            IOptions<ModuleCollection> modules,
                            ModuleSettings moduleSettings,
                            QueueServices queueServices,
                            BackendRouteMap routeMap,
                            ILogger<ModuleRunner> logger)
        {
            _serverOptions  = serverOptions.Value;
            _modules        = modules.Value;
            _moduleSettings = moduleSettings;
            _queueServices  = queueServices;
            _routeMap       = routeMap;
            _logger         = logger;

            // The very first thing we need to do is remove invalid modules. This can happen in the
            // case where a module was installed, a setting for that module then persisted in the
            // settings override .json file, and then the module is removed. Our config system will
            // load up the persisted override settings and see some settings for the (now removed)
            // module, and add that fragment of a module settings to the modules list, resulting in
            // an invalid module in the list.
            List<string> keys = _modules!.Keys.ToList();
            foreach (string moduleId in keys)
            {
                ModuleConfig? module = _modules[moduleId];
                
                // First, first: set the module's ModuleID. It's not set in modulesettings.json
                if (module is not null)
                    module.ModuleId = moduleId;

                if (module is null || !module.Valid)
                    _modules.Remove(moduleId, out _);
            }               

            _processStatuses = new Dictionary<string, ProcessStatus>();
            foreach (ModuleConfig module in _modules.Values)
            {
                var status = module.Available(SystemInfo.Platform)
                           ? (module.Activate == true ? ProcessStatusType.Enabled 
                                                      : ProcessStatusType.NotEnabled)
                           : ProcessStatusType.NotAvailable;

                _processStatuses.Add(module.ModuleId!, new ProcessStatus()
                {
                    ModuleId = module.ModuleId,
                    Name     = module.Name,
                    Status   = status
                });
            }
        }

        /// <inheritdoc></inheritdoc>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            ModuleInstaller.InstallInitialModules();

            _logger.LogInformation("ModuleRunner Start");
            return base.StartAsync(cancellationToken);
        }

        /// <inheritdoc></inheritdoc>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ModuleRunner Stop");

            Parallel.ForEach(_modules.Values, module => KillProcess(module).Wait());

            int shutdownServerDelaySecs = _moduleSettings.DelayAfterStoppingModulesSecs;
            Task.Delay(TimeSpan.FromSeconds(shutdownServerDelaySecs), cancellationToken);

            return base.StopAsync(cancellationToken);
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

            bool launchModules            = _moduleSettings.LaunchModules;
            int  preLaunchModuleDelaySecs = _moduleSettings.DelayBeforeLaunchingModulesSecs;

            // Setup routes.  Do this first so they are active during debug without launching services.
            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                if (!module.Valid)
                    continue;

                // This is the startup of the server, but we may not actually be starting all the 
                // modules. Ensure the process reflects our needs on whether we need it started.
                if (!SetPreLaunchProcessStatus(module, launchModules))
                    continue;

                // Create the required Queues even if launchModules=false. This allows the server
                // to listen on that queeu should the module be started by something other than the
                // server. Eg a debugger.
                SetupQueueAndRoutes(module);
            }

            if (!launchModules)
            {
                _logger.LogWarning("Skipping Background AI Modules startup");
                return;
            }

            // Let's make sure the front end is up and running before we start the backend 
            // analysis services
            await Task.Delay(TimeSpan.FromSeconds(preLaunchModuleDelaySecs), stoppingToken);

            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                string moduleId      = entry.Key;
                    
                if (stoppingToken.IsCancellationRequested)
                    break;

                if (!module.Valid)
                    continue;

                ProcessStatus status = _processStatuses[moduleId];
                if (status == null)
                    continue;

                await StartProcess(module);
            }
        }

        /// <summary>
        /// Kills a process
        /// </summary>
        /// <param name="module">The module for the process to be killed</param>
        /// <returns>true on success</returns>
        public async Task<bool> KillProcess(ModuleConfig module)
        {
            if (module is null || string.IsNullOrWhiteSpace(module.ModuleId))
                return false;

            // If we can't find this module as a running process then let's claim
            // success (It's not running, after all!) and sneak off.
            if (!_runningProcesses.TryGetValue(module.ModuleId, out Process? process))
                return true;
 
            bool hasExited = true;
            try
            {
                hasExited = process.HasExited;
            }
            catch
            { 
            }

            if (!hasExited)
            {
                _logger.LogInformation($"Sending shutdown request to {process.ProcessName}/{module.ModuleId}");

                // Create the Quit request with the ModuleId as a param
                RequestPayload payload = new()
                {
                    command = "Quit",
                    values = new List<KeyValuePair<string, string?[]>>
                    {
                       new KeyValuePair<string, string?[]>("moduleId", new string[] { module.ModuleId })
                    }
                };

                // Send the request but give it 1 second to wrap things up before we step in further
                await _queueServices.SendRequestAsync(module.Queue ?? "", new BackendRequest("Quit", payload));
                await Task.Delay(TimeSpan.FromSeconds(1));

                try
                {
                    if (!process.HasExited)
                    {
                        _logger.LogInformation($"Forcing shutdown of {process.ProcessName}/{module.ModuleId}");
                        process.Kill(true);
                    }
                    else
                        _logger.LogInformation($"{module.ModuleId} went quietly");
                }
                catch
                {
                }
            }
            else
                _logger.LogInformation($"{module.ModuleId} has left the building");

            Console.WriteLine("Removing module from Processes list");
            _runningProcesses.Remove(module.ModuleId);

            return true;
        }

        /// <summary>
        /// Starts, or restarts if necessary, a process.
        /// </summary>
        /// <param name="module">The module to be started</param>
        /// <returns>True on success; false otherwise</returns>
        public async Task<bool> StartProcess(ModuleConfig module)
        {
            if (module is null || string.IsNullOrWhiteSpace(module.ModuleId))
                return false;

            if (!_processStatuses.TryGetValue(module.ModuleId, out ProcessStatus? status))
            {
                status = new ProcessStatus()
                {
                    ModuleId = module.ModuleId,
                    Name     = module.Name,
                    Status   = ProcessStatusType.Unknown
                };
                _processStatuses.Add(module.ModuleId, status);
            }

            // The module will need its status to be "Enabled" in order to be launched. We set
            // "launchModules" = true here since this method will be called after the server has
            // already started. Only on server start do we entertain the possibility that we won't
            // actually start a module. At all other times we ensure they start.
            if (!SetPreLaunchProcessStatus(module, true))
                return false;

            SetupQueueAndRoutes(module);

            if (status.Status != ProcessStatusType.Enabled)
                return false;

            Process? process = null;
            try
            {
                ProcessStartInfo procStartInfo = CreateProcessStartInfo(module);

                process = new Process
                {
                    StartInfo           = procStartInfo,
                    EnableRaisingEvents = true
                };
                process.OutputDataReceived += SendOutputToLog;
                process.ErrorDataReceived  += SendErrorToLog;

                _runningProcesses.Add(module.ModuleId, process);

                // Start the process
                _logger.LogTrace($"Starting {Text.ShrinkPath(process.StartInfo.FileName, 50)} {Text.ShrinkPath(process.StartInfo.Arguments, 50)}");

                string summary = module.SettingsSummary;
                string[] lines = summary.Split('\n');

                _logger.LogInformation("");
                foreach (string line in lines)
                    _logger.LogInformation($"** {line.Trim()}");
                _logger.LogInformation("");

                status.Status = ProcessStatusType.Starting;

                if (!process.Start())
                {
                    process = null;
                    status.Status = ProcessStatusType.FailedStart;
                }

                if (process is not null)
                {
                    status.Started = DateTime.UtcNow;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    _logger.LogInformation($"Started {module.Name} module");

                    int postStartPauseSecs = module.PostStartPauseSecs ?? 5;

                    // Trying to reduce startup CPU and instantaneous memory use for low resource
                    // environments such as Docker or RPi
                    await Task.Delay(TimeSpan.FromSeconds(postStartPauseSecs));
                    status.Status = ProcessStatusType.Started;
                }
                else
                {
                     _logger.LogError($"Unable to start {module.Name} module");
                }
            }
            catch (Exception ex)
            {
                status.Status = ProcessStatusType.FailedStart;

                _logger.LogError(ex, $"Error trying to start {module.Name} ({module.FilePath})");
                _logger.LogError(ex.Message);
                _logger.LogError(ex.StackTrace);
#if DEBUG
                _logger.LogError($" *** Did you setup the Development environment?");
                if (SystemInfo.Platform == "Windows")
                    _logger.LogError($"     Run /src/setup.bat");
                else
                    _logger.LogError($"     In /src, run 'bash setup.sh'");

                _logger.LogError($"Exception: {ex.Message}");
#else
                _logger.LogError($"*** Please check the CodeProject.AI installation completed successfully");
#endif
            }

            if (process is null)
                _runningProcesses.Remove(module.ModuleId);

            return process != null;
        }

        /// <summary>
        /// Stops, if necessary, and then restarts a process. Handy if settings have changed and we
        /// need the process to be updated.
        /// </summary>
        /// <param name="module">The module to be restarted</param>
        /// <returns>True on success; false otherwise</returns>
        public async Task<bool> RestartProcess(ModuleConfig module)
        {
            if (module is null || string.IsNullOrWhiteSpace(module.ModuleId))
                return false;

            if (string.IsNullOrEmpty(module.FilePath))
                return false;

            ProcessStatus status = ProcessStatuses[module.ModuleId];

            // We can't reuse a process (easily). Kill the old and create a brand new one
            if (_runningProcesses.TryGetValue(module.ModuleId, out Process? process) && process != null)
            {
                status.Status = ProcessStatusType.Stopping;
                await KillProcess(module);
                status.Status = ProcessStatusType.Stopped;
            }
            else
                status.Status = ProcessStatusType.Stopped;

            // If we're actually meant to be killing this process, then just leave now.
            if (module.Activate == false || !module.Available(SystemInfo.Platform))
                return true;

            return await StartProcess(module);
        }

        /// <summary>
        /// Tests whether the specified module is being managed by this module runner.
        /// </summary>
        /// <param name="moduleId">The ID of the module to check</param>
        /// <returns>true if the module exists; false otherwise</returns>
        public bool HasModule(string moduleId)
        {
            return Modules.ContainsKey(moduleId);
        }

        /// <summary>
        /// Removes a module from the set of modules this runner knows about
        /// </summary>
        /// <param name="moduleId">The ID of the module to remove</param>
        /// <returns>true on success; false otherwise</returns>
        public bool RemoveModule(string moduleId)
        {
            Console.WriteLine("Removing module from Modules list");
            if (Modules.ContainsKey(moduleId))
                if (!Modules.TryRemove(moduleId, out ModuleConfig _))
                    return false;

            Console.WriteLine("Removing module from Process list");
            if (ProcessStatuses.ContainsKey(moduleId))
                if (!ProcessStatuses.Remove(moduleId))
                    return false;

            return true;
        }

        /// <summary>
        /// Sets the current status of the module just before it's to be started
        /// </summary>
        /// <param name="module">The module to be restarted</param>
        /// <param name="launchModules">Whether or not we're actually launching modules processes</param>
        /// <returns>True on success; false otherwise</returns>
        private bool SetPreLaunchProcessStatus(ModuleConfig module, bool launchModules)
        {
            ProcessStatus status = _processStatuses[module.ModuleId!];
            if (status == null)
            {
                _logger.LogWarning($"No process status found defined for {module.Name}");
                return false;
            }

            // setup the routes for this module.
            if (module.Available(SystemInfo.Platform))
            {
                if (!module.Activate == true)
                    status.Status = ProcessStatusType.NotEnabled;
                else if (launchModules)
                    status.Status = ProcessStatusType.Enabled;
                else
                    status.Status = ProcessStatusType.NotStarted;
            }
            else
                status.Status = ProcessStatusType.NotAvailable;

            return true;
        }

        private bool SetupQueueAndRoutes(ModuleConfig module)
        {
            if (!string.IsNullOrWhiteSpace(module.Queue))
                _queueServices.EnsureQueueExists(module.Queue);
            else
                _logger.LogWarning($"No Queue defined for {module.Name}");

            if (module.RouteMaps?.Any() != true)
            {
                _logger.LogWarning($"No routes defined for {module.Name}");
                return false;
            }

            foreach (var routeInfo in module.RouteMaps)
                 _routeMap.Register(routeInfo, module.Queue!);

            return true;
        }

        private ProcessStartInfo CreateProcessStartInfo(ModuleConfig module)
        {
            // We could combine these into a single method that returns a tuple
            // but this is not a place that needs optimisation. It needs clarity.
            string modulePath = _moduleSettings.GetModulePath(module);
            string workingDir = _moduleSettings.GetWorkingDirectory(module);
            string filePath   = _moduleSettings.GetFilePath(module);
            string? command   = _moduleSettings.GetCommandPath(module);

            // Setup the process we're going to launch
#if Windows
            // Windows paths can have spaces so need quotes
            var executableName = $"\"{filePath}\"";
#else
            // HACK: We are assuming the directories don't have spaces in Linux and MacOS
            // because the Process.Start is choking on the quotes
            var executableName = filePath;
#endif
            ProcessStartInfo? procStartInfo = (command == "execute" || command == "launcher")
                ? new ProcessStartInfo(executableName)
                {
                    UseShellExecute        = false,
                    WorkingDirectory       = workingDir,
                    CreateNoWindow         = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                }
                : new ProcessStartInfo($"{command}", $"\"{filePath}\"")
                {
                    UseShellExecute        = false,
                    WorkingDirectory       = workingDir,
                    CreateNoWindow         = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                };

            // Set the environment variables
            Dictionary<string, string?> environmentVars = BuildBackendEnvironmentVar(module, modulePath);
            foreach (var kv in environmentVars)
                procStartInfo.Environment.TryAdd(kv.Key.ToUpper(), kv.Value);

            return procStartInfo;
        }

        private void SendOutputToLog(object sender, DataReceivedEventArgs data)
        {
            string? message = data?.Data;

            string filename = string.Empty;
            if (sender is Process process)
            {
                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                if (string.IsNullOrWhiteSpace(filename))
                    filename = Path.GetFileName(process.StartInfo.FileName.Replace("\"", ""));

                if (process.HasExited && string.IsNullOrEmpty(message))
                    message = "has exited";
            }

            if (string.IsNullOrWhiteSpace(message))
                return;

            // if (string.IsNullOrEmpty(filename))
            //    filename = "Process";

            if (!string.IsNullOrEmpty(filename))
                filename += ": ";

            // Force ditch the MS logging scoping headings
            if (!message.StartsWith("info: ") && !message.EndsWith("[0]"))
                _logger.LogInformation(filename + message);

            // Console.WriteLine("REDIRECT STDOUT: " + filename + message);
        }

        private void SendErrorToLog(object sender, DataReceivedEventArgs data)
        {
            string? error = data?.Data;

            string filename = string.Empty;
            if (sender is Process process)
            {
                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                if (string.IsNullOrWhiteSpace(filename))
                    filename = Path.GetFileName(process.StartInfo.FileName.Replace("\"", ""));

                // This same logic (and output) is sent to stdout so no need to duplicate here.
                // if (process.HasExited && string.IsNullOrEmpty(error))
                //    error = "has exited";
            }

            if (string.IsNullOrWhiteSpace(error))
                return;

            // if (string.IsNullOrEmpty(filename))
            //    filename = "Process";
            if (!string.IsNullOrEmpty(filename))
                filename += ": ";

            if (string.IsNullOrEmpty(error))
                error = "No error provided";

            if (error.Contains("LoadLibrary failed with error 126") &&
                error.Contains("onnxruntime_providers_cuda.dll"))
            {
                error = "Attempted to load ONNX runtime CUDA provider. No luck, moving on...";
                _logger.LogInformation(filename + error);
            }
            else if (error != "info: Microsoft.Hosting.Lifetime[0]")
            {
                // TOTAL HACK. ONNX/Tensorflow output is WAY too verbose for an error
                if (error.Contains("I tensorflow/cc/saved_model/reader.cc:") ||
                    error.Contains("I tensorflow/cc/saved_model/loader.cc:"))
                    _logger.LogInformation(filename + error);
                else
                    _logger.LogError(filename + error);
            };

            // Console.WriteLine("REDIRECT ERROR: " + filename + error);
        }

        /// <summary>
        /// Creates the collection of backend environment variables.
        /// </summary>
        /// <param name="module">The current module</param>
        /// <param name="currentModulePath">The path to the current module, if appropriate.</param>
        private Dictionary<string, string?> BuildBackendEnvironmentVar(ModuleConfig module,
                                                                       string ?currentModulePath = null)
        {
            Dictionary<string, string?> processEnvironmentVars = new();
            _serverOptions.AddEnvironmentVariables(processEnvironmentVars);
            module.AddEnvironmentVariables(processEnvironmentVars);

            // Now perform the macro expansions

            // We could do this. But why waste the allocations...
            // processEnvironmentVars = processEnvironmentVars.ToDictionary(kvp => kvp.Key.ToUpper(),
            //                                                              kvp => ExpandOption(kvp.Value));

            var keys = processEnvironmentVars.Keys.ToList();
            foreach (string key in keys)
            {
                string? value = processEnvironmentVars[key.ToUpper()];
                processEnvironmentVars[key.ToUpper()] = _moduleSettings.ExpandOption(value, currentModulePath);
            }

            // And now add general vars
            processEnvironmentVars.TryAdd("CPAI_MODULE_ID",          module.ModuleId);
            processEnvironmentVars.TryAdd("CPAI_MODULE_NAME",        module.Name);
            processEnvironmentVars.TryAdd("CPAI_MODULE_PARALLELISM", module.Parallelism.ToString());
            processEnvironmentVars.TryAdd("CPAI_CUDA_DEVICE_NUM",    module.CudaDeviceNumber.ToString());
            processEnvironmentVars.TryAdd("CPAI_MODULE_SUPPORT_GPU", module.SupportGPU.ToString());
            processEnvironmentVars.TryAdd("CPAI_MODULE_QUEUENAME",   module.Queue);

            return processEnvironmentVars;
        }
    }

    /// <summary>
    /// Extension methods for the ModuleRunner.
    /// </summary>
    public static class ModuleRunnerExtensions
    {
        /// <summary>
        /// Sets up the ModuleRunner.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <param name="configuration">The Configuration.</param>
        /// <returns></returns>
        public static IServiceCollection AddModuleRunner(this IServiceCollection services,
                                                         IConfiguration configuration)
        {
            // a test
            // ModuleConfig module = new();
            // configuration.Bind("Modules:OCR", module);

            // Setup the config objects
            services.Configure<ServerOptions>(configuration.GetSection("ServerOptions"));
            services.Configure<ModuleOptions>(configuration.GetSection("ModuleOptions"));

            // The binding of the Dictionary has issues in NET7
            // Doing this BFI
            // services.Configure<ModuleCollection>(configuration.GetSection("Modules"));

            services.AddOptions<ModuleCollection>()
                    .Configure(moduleCollection =>
                    {
                        var moduleNames = configuration.GetSection("Modules").GetChildren().Select(x => x.Key).ToList();
                        foreach (var moduleName in moduleNames)
                        {
                            if (moduleName is not null)
                            {
                                ModuleConfig moduleConfig = new ModuleConfig();
                                configuration.Bind($"Modules:{moduleName}", moduleConfig);
                                moduleCollection.TryAdd(moduleName, moduleConfig);
                            }
                        }
                    });

            services.Configure<HostOptions>(hostOptions =>
            {
                hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            });

            // Add the runner
            // services.AddHostedService<ModuleRunner>();

            // Add the runner but ensure it's available via Dependency Injection
            services.AddSingleton<ModuleRunner>();
            services.AddHostedService<ModuleRunner>(p => p.GetRequiredService<ModuleRunner>());

            return services;
        }
    }
}
