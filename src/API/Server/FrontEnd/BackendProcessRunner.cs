using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.API.Common;
using CodeProject.AI.API.Server.Backend;
using CodeProject.AI.Server.Backend;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// This background process manages the startup and shutdown of the backend processes.
    /// </summary>
    public class BackendProcessRunner : BackgroundService
    {
        // marker for path substitution
        const string RootPathMarker       = "%ROOT_PATH%";
        const string ModulesPathMarker    = "%MODULES_PATH%";
        const string PlatformMarker       = "%PLATFORM%";
        const string DataDirMarker        = "%DATA_DIR%";
        const string PythonBasePathMarker = "%PYTHON_BASEPATH%";
        const string PythonPathMarker     = "%PYTHON_PATH%";
        const string PythonRuntimeMarker  = "%PYTHON_RUNTIME%";

        private readonly FrontendOptions               _frontendOptions;

        // TODO: this really should be a singleton global that is initialized
        //       from the configuration but can be updated after.
        private readonly ModuleCollection              _modules;

        // TODO: Add Dictionary<string, ProcessStatus> _processStatuses; that tracks the status of
        //       each module. Remove the "state" properties from ModuleConfig.
        private readonly IConfiguration                _config;
        private readonly ILogger<BackendProcessRunner> _logger;
        private readonly QueueServices                 _queueServices;
        private readonly BackendRouteMap               _routeMap;
        private readonly List<Process>                 _runningProcesses = new();
        private readonly string?                       _appDataDirectory;

        private readonly ModuleCollection _emptyModuleList = new ModuleCollection();

        /// <summary>
        /// Gets the current platform name
        /// </summary>
        public static string Platform
        {
            get
            {
                // TODO: Docker is an environment in which we're running Linux. We probably need to have
                //       OS, Environment (Docker, VSCode), Platform (x86-64, Arm64),
                //       Accelerator (CPU, GPU-Gen5, nVidia)
                bool inDocker = (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "") == "true";
                if (inDocker)
                    return "Docker";  // which in our case implies that we are running in Linux

                // RuntimeInformation.GetPlatform() or RuntimeInformation.Platform would have been
                // too easy.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return "Windows";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "macOS-Arm" : "macOS";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return "Linux";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                    return "FreeBSD";

                return "Windows"; // Gotta be something...
            }
        }

        /// <summary>
        /// Gets a list of the startup processes.
        /// </summary>
        public ModuleCollection StartupProcesses
        {
            get { return _modules ?? _emptyModuleList; }
        }

        /// <summary>
        /// Gets a list of the processes names and statuses.
        /// </summary>
        public List<ProcessStatus> ProcessStatuses
        {
            get
            {
                return StartupProcesses.Select(entry => new ProcessStatus()
                {
                    ModuleId          = entry.Key ?? "Unknown",
                    Name              = entry.Value.Name,
                    Started           = entry.Value.Started,
                    LastSeen          = entry.Value.LastSeen,
                    Running           = entry.Value.Running,
                    Processed         = entry.Value.Processed ?? 0,
                    ExecutionProvider = entry.Value.ExecutionProvider ?? string.Empty,
                    HardwareId        = entry.Value.HardwareId ?? "CPU"
                }).ToList();
            }
        }

        /// <summary>
        /// Gets the directory that is the root of this system. 
        /// </summary>
        /// <param name="configRootPath">The root path specified in the config file.
        /// assumptions</param>
        /// <returns>A string for the adjusted path</returns>
        public static string GetRootPath(string? configRootPath)
        {
            string defaultPath = configRootPath ?? AppContext.BaseDirectory;
            // Correct for cross platform (win = \, linux = /)
            defaultPath = defaultPath.Replace('\\', Path.DirectorySeparatorChar);

            // Either the config file or lets assume it's the current dir if all else fails
            string rootPath = defaultPath;

            // If the config value is a relative path then add it to the current dir. This is where
            // we have to trust the config values are right, and we also have to trust that when
            // this server is called the "ASPNETCORE_ENVIRONMENT" flag is set as necessary in order
            // to ensure the appsettings.Development.json config files are includwed
            if (rootPath.StartsWith(".."))
                rootPath = Path.Combine(AppContext.BaseDirectory, rootPath!);

            // converts relative URLs and squashes the path to he correct absolute path
            rootPath = Path.GetFullPath(rootPath);

            /*
            // HACK: If we're running this server from the build output dir in development, and 
            // we haven't set the environment var ASPNETCORE_ENVIRONMENT=Development, then the path
            // will be wrong.
            bool devBuild = false;
            DirectoryInfo? info = new DirectoryInfo(rootPath);
            if (info.Name.ToLower() == "debug" || info.Name.ToLower() == "release")
            {
                while (info != null)
                {
                    // Console.WriteLine($"info.FullName = {info.FullName}");

                    info = info.Parent;
                    if (info?.Name.ToLower() == "src")
                    {
                        info = info.Parent;
                        break;
                    }
                }

                if (info != null)
                {
                    rootPath       = info.FullName;
                    devBuild = true;
                }
            }
            */

            return rootPath;
        }

        /// <summary>
        /// Initialises a new instance of the BackendProcessRunner.
        /// </summary>
        /// <param name="options">The FrontendOptions</param>
        /// <param name="modules">The Modules configuration.</param>
        /// <param name="config">The application configuration.</param>
        /// <param name="queueServices">The Queue management service.</param>
        /// <param name="routeMap">The RouteMap service.</param>
        /// <param name="logger">The logger.</param>
        public BackendProcessRunner(IOptions<FrontendOptions> options,
                                    IOptions<ModuleCollection> modules,
                                    IConfiguration config,
                                    QueueServices queueServices,
                                    BackendRouteMap routeMap,
                                    ILogger<BackendProcessRunner> logger)
        {
            _frontendOptions  = options.Value;
            _modules          = modules.Value;
            _config           = config;
            _logger           = logger;
            _queueServices    = queueServices;
            _routeMap         = routeMap;

            // ApplicationDataDir is set in Program.cs and added to an InMemoryCollection config set.
            _appDataDirectory = config.GetValue<string>("ApplicationDataDir");

            ExpandMacros();
        }

        /// <summary>
        /// Gets the backend process status for a queue.
        /// </summary>
        /// <param name="queueName">The Queue Name.</param>
        /// <returns>The status for the backend process, or false if the queue is invalid.</returns>
        public bool GetStatusForQueue(string queueName)
        {
            return StartupProcesses.FirstOrDefault(entry => 
                                                        entry.Value.RouteMaps!
                                                             .Any(x => string.Compare(x.Queue, queueName, true) == 0)
                                                  ).Value?.Running ?? false;
        }

        /// <inheritdoc></inheritdoc>
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackendProcessRunner Start");
            return base.StartAsync(cancellationToken);
        }

        /// <inheritdoc></inheritdoc>
        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("BackendProcessRunner Stop");

            // Doing the above in Parallel speeds things up
            Parallel.ForEach(_runningProcesses.Where(x => !x.HasExited), process =>
            {
                Console.WriteLine($"Shutting down {process.ProcessName}");
                // TODO: First send a "Shutdown" message to each process and wait a second or two.
                //       If the process continues to run, then we get serious.
                process.Kill(true);
            });

            return base.StopAsync(cancellationToken);
        }

        /// <inheritdoc></inheritdoc>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {        
            bool loggerIsValid = true; // You just never know, right?

            try
            {
                if (_modules is null)
                {
                    _logger.LogInformation("No Background AI Modules specified");
                    Logger.Log("No Background AI Modules specified");
                    return;
                }

                _logger.LogInformation("Starting Background AI Modules");
                Logger.Log("Starting Background AI Modules");
            }
            catch
            {
                loggerIsValid = false;
            }

            // Setup routes.  Do this first so they are active during debug without launching services.
            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                string moduleId      = entry.Key;

                // setup the routes for this module.
                if (IsEnabled(module))
                {
                    if (!(module.RouteMaps?.Any() ?? false))
                    {
                        Logger.Log($"No routes defined for {module.Name}");
                    }
                    else
                    {
                        foreach (var routeInfo in module.RouteMaps!)
                            _routeMap.Register(routeInfo);
                    }
                }
            }

            bool launchAnalysisServices = _config.GetValue("LaunchAnalysisServices", true);
            if (!launchAnalysisServices)
            {
                _logger.LogInformation("Skipping Background AI Modules startup");
                Logger.Log("Skipping Background AI Modules startup");

                return;
            }

            // Let's make sure the front end is up and running before we start the backend 
            // analysis services
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            if (loggerIsValid)
            {
                _logger.LogInformation($"Root Path:   {_frontendOptions.ROOT_PATH}");
                _logger.LogInformation($"Module Path: {_frontendOptions.MODULES_PATH}");
                _logger.LogInformation($"Python Path: {_frontendOptions.PYTHON_PATH}");
                _logger.LogInformation($"Temp Dir:    {Path.GetTempPath()}");
                _logger.LogInformation($"Data Dir:    {_appDataDirectory}");

                Logger.Log($"App directory {_frontendOptions.ROOT_PATH}");
                Logger.Log($"Analysis modules in {_frontendOptions.MODULES_PATH}");
            }

            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                string moduleId = entry.Key;
                    
                if (stoppingToken.IsCancellationRequested)
                    break;

                bool enabled = IsEnabled(module!);

                if (!enabled)
                    Logger.Log($"Not starting {module.Name}: Not set as enabled");

                if (enabled && !string.IsNullOrEmpty(module.FilePath))
                {
                    // _logger.LogError($"Starting {cmdInfo.Command}");

                    // create the required Queues
                    foreach (var routeInfo in module.RouteMaps)
                        if (!string.IsNullOrWhiteSpace(routeInfo.Queue))
                            _queueServices.EnsureQueueExists(routeInfo.Queue);

                    ProcessStartInfo procStartInfo = CreateProcessStartInfo(module, moduleId);

                    // Start the process
                    try
                    {
                        if (loggerIsValid)
                            _logger.LogInformation($"Starting {procStartInfo.FileName} {procStartInfo.Arguments}");

                        Process? process = new Process();
                        process.StartInfo           = procStartInfo;
                        process.EnableRaisingEvents = true;
                        process.OutputDataReceived += (sender, data) =>
                        {
                            string message = data.Data ?? string.Empty;

                            string filename = string.Empty;
                            if (sender is Process process)
                            {
                                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                                if (process.HasExited && message == string.Empty)
                                    message = "has exited";
                            }

                            if (string.IsNullOrEmpty(filename))
                                filename = "Process";

                            Logger.Log(filename + ": " + message);
                        };
                        process.ErrorDataReceived += (sender, data) =>
                        {
                            string error = data.Data ?? string.Empty;

                            string filename = string.Empty;
                            if (sender is Process process)
                            {
                                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                                if (process.HasExited && error == string.Empty)
                                    error = "has exited";
                            }

                            if (string.IsNullOrEmpty(filename))
                                filename = "Process";

                            if (string.IsNullOrEmpty(error))
                                error = "No error provided";

                            Logger.Log(filename + ": " + error);
                        };

                        if (!process.Start())
                            process = null;

                        if (process is not null)
                        {
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();

                            if (loggerIsValid)
                                _logger.LogInformation($"Started {module.Name} backend");

                            _runningProcesses.Add(process);
                            module.Started = DateTime.UtcNow;

                            Logger.Log($"Started {module.Name}");
                        }
                        else
                        {
                            if (loggerIsValid)
                                _logger.LogError($"Unable to start {module.Name} backend");

                            Logger.Log($"Unable to start {module.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (loggerIsValid)
                        {
                            _logger.LogError(ex, $"Error trying to start { module.Name}");

                            Console.WriteLine("-------------------------------------------------");
                            Console.WriteLine($"FilePath: {module.FilePath}");
                            Console.WriteLine("-------------------------------------------------");
                        }

                        Logger.Log($"Error running {module.FilePath}");
#if DEBUG
                        if (Platform == "Windows")
                            Logger.Log($"    Run /Installers/Dev/setup_dev_env_win.bat");
                        else
                            Logger.Log($"    In /Installers/Dev/, run 'bash setup_dev_env_linux.sh'");
                        Logger.Log($" ** Did you setup the Development environment?");
#else
                        Logger.Log($"Please check the CodeProject.AI installation completed successfully");
#endif
                    }
                }
            }
        }

        private ProcessStartInfo CreateProcessStartInfo(ModuleConfig module, string moduleId)
        {
            string? command = ExpandOption(module.Command) ??
                              GetCommandByRuntime(module.Runtime) ??
                              GetCommandByExtension(module.FilePath);

            // Correcting for cross platform (win = \, linux = /)
            string filePath = Path.Combine(_frontendOptions.MODULES_PATH!,
                                           module.FilePath!.Replace('\\', Path.DirectorySeparatorChar));
            string? workingDirectory = Path.GetDirectoryName(filePath);

            // Setup the process we're going to launch
            ProcessStartInfo? procStartInfo = (command == "execute")
                ? new ProcessStartInfo($"\"{filePath}\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
                : new ProcessStartInfo($"{command}", $"\"{filePath}\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = workingDirectory,
                    CreateNoWindow = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

            // Set the environment variables
            Dictionary<string, string?> environmentVars = BuildBackendEnvironmentVar(module);
            foreach (var kv in environmentVars)
                procStartInfo.Environment.TryAdd(kv.Key, kv.Value);

            // Queue is currently route specific, so we can't do this at the moment
            // procStartInfo.Environment.TryAdd("MODULE_QUEUE", cmdInfo.QueueName);
            procStartInfo.Environment.TryAdd("MODULE_ID", moduleId);

            return procStartInfo;
        }

        private bool IsEnabled(ModuleConfig module)
        {
            // Has it been explicitely activated?
            bool enabled = module.Activate ?? false;

            // Check the EnableFlags as backup. TODO: remove the Enable Flags
            if (module.EnableFlags?.Length > 0)
                foreach (var envVar in module.EnableFlags)
                    enabled = enabled || _config.GetValue(envVar, false);

            // If the platform list doesn't include the current platform, then veto the activation
            if (enabled && !module.Platforms!.Any(p => p.ToLower() == Platform.ToLower()))
                enabled = false;

            if (!enabled && module.Platforms!.Any(p => p.ToLower() == "all"))
                enabled = true;

            return enabled;
        }

        private string? GetCommandByRuntime(string? runtime)
        {
            if (runtime is null)
                return null;

            runtime = runtime.ToLower();

            // HACK: Ultimately we will have a set of "runtime" modules which will install and
            // register the runtimes we use. The registration will include the runtime name
            // (eg "python39") and the path to the runtime's launcher. For now we're going to 
            // just hardcode Python and .NET support.

            // If it is "Python" then use our default Python location (in this case, python 3.7)
            if (runtime == "python")
                runtime = "python37";

            // If it is a PythonNN command then replace our marker in the default python path to
            // match the requested interpreter location
            if (runtime.StartsWith("python") && !runtime.StartsWith("python3."))
                return _frontendOptions.PYTHON_PATH?.Replace(PythonRuntimeMarker,
                    // HACK: on docker the python command is in the format of python3.N
                            Platform == "Docker" ? runtime.Replace("python3", "python3.") : runtime);

            if (runtime == "dotnet")
                return "dotnet";

            return null;
        }

        private string? GetCommandByExtension(string? filename)
        {
            if (filename is null)
                return null;

            // HACK: Ultimately we will have a set of "runtime" modules which will install and
            // register the runtimes we use. The registration will include the runtime name
            // (eg "dotnet") and the file extensions that the runtime can unambiguously handle.
            // The "python39" runtime, for example, may want to register .py, but so would python37.
            // "dotnet" is welcome to register .dll as long as no other runtime module wants .dll too.

            switch (Path.GetExtension(filename))
            {
                case ".py": return GetCommandByRuntime("python");
                case ".dll": return "dotnet";
                case ".exe": return "execute";
                default:
                    throw new Exception("If neither Runtime nor Command is specified then FilePath must have an extension of '.py' or '.dll'.");
            }
        }

        /// <summary>
        /// Expands all the directory markers in the options.
        /// </summary>
        private void ExpandMacros()
        {
            if (_frontendOptions is null)
                return;

            // For Macro expansion in appsettings settings we have PYTHON_PATH which depends on
            // PYTHON_BASEPATH which usually depends on MODULES_PATH and both depend on ROOT_PATH.
            // Get and expand each of these in the correct order.

            _frontendOptions.ROOT_PATH       = GetRootPath(_frontendOptions.ROOT_PATH);
            _frontendOptions.MODULES_PATH    = Path.GetFullPath(ExpandOption(_frontendOptions.MODULES_PATH)!);

            _frontendOptions.PYTHON_BASEPATH = Path.GetFullPath(ExpandOption(_frontendOptions.PYTHON_BASEPATH)!);
            _frontendOptions.PYTHON_PATH     = ExpandOption(_frontendOptions.PYTHON_PATH);

            // Fix the slashes
            if (_frontendOptions.PYTHON_PATH?.Contains(Path.DirectorySeparatorChar) ?? false)
                _frontendOptions.PYTHON_PATH = Path.GetFullPath(_frontendOptions.PYTHON_PATH);

            Console.WriteLine("------------------------------------------------------------------");
            Console.WriteLine($"Expanded ROOT_PATH       = {_frontendOptions.ROOT_PATH}");
            Console.WriteLine($"Expanded MODULES_PATH    = {_frontendOptions.MODULES_PATH}");
            Console.WriteLine($"Expanded PYTHON_BASEPATH = {_frontendOptions.PYTHON_BASEPATH}");
            Console.WriteLine($"Expanded PYTHON_PATH     = {_frontendOptions.PYTHON_PATH}");
            Console.WriteLine("------------------------------------------------------------------");
        }

        /// <summary>
        /// Expands the directory markers in the string.
        /// </summary>
        /// <param name="value">The value to expand.</param>
        /// <returns>The expanded path.</returns>
        private string? ExpandOption(string? value)
        {
            if (value is null)
                return null;

            value = value.Replace(ModulesPathMarker,    _frontendOptions.MODULES_PATH);
            value = value.Replace(RootPathMarker,       _frontendOptions.ROOT_PATH);
            value = value.Replace(PlatformMarker,       Platform.ToLower());
            value = value.Replace(PythonBasePathMarker, _frontendOptions.PYTHON_BASEPATH);
            value = value.Replace(PythonPathMarker,     _frontendOptions.PYTHON_PATH);
            value = value.Replace(DataDirMarker,        _appDataDirectory);

            // Correct for cross platform (win = \, linux = /)
            value = value.Replace('\\', Path.DirectorySeparatorChar);

            return value;
        }

        /// <summary>
        /// Creates the collection of backend environment variables.
        /// </summary>
        private Dictionary<string, string?> BuildBackendEnvironmentVar(ModuleConfig module)
        {
            Dictionary<string, string?> processEnvironmentVars = new();

            if (_frontendOptions.EnvironmentVariables is not null)
                foreach (var entry in _frontendOptions.EnvironmentVariables)
                {
                    if (processEnvironmentVars.ContainsKey(entry.Key))
                        processEnvironmentVars[entry.Key] = ExpandOption(entry.Value.ToString());
                    else
                        processEnvironmentVars.Add(entry.Key, ExpandOption(entry.Value.ToString()));
                }

            if (module.EnvironmentVariables is not null)
                foreach (var entry in module.EnvironmentVariables)
                {
                    if (processEnvironmentVars.ContainsKey(entry.Key))
                        processEnvironmentVars[entry.Key] = ExpandOption(entry.Value.ToString());
                    else
                        processEnvironmentVars.Add(entry.Key, ExpandOption(entry.Value.ToString()));
                }

            Console.WriteLine();
            Console.WriteLine($"Setting Environment variables for {module.Name}");
            Console.WriteLine("------------------------------------------------------------------");
            foreach (var envVar in processEnvironmentVars)
                Console.WriteLine($"{envVar.Key.PadRight(16)} = {envVar.Value}");
            Console.WriteLine("------------------------------------------------------------------");

            return processEnvironmentVars;
        }
    }

    /// <summary>
    /// Extension methods for the BackendProcessRunner.
    /// </summary>
    public static class BackendProcessRunnerExtensions
    {
        /// <summary>
        /// Sets up the BackendProcessRunner.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <param name="configuration">The Configuration.</param>
        /// <returns></returns>
        public static IServiceCollection AddBackendProcessRunner(this IServiceCollection services,
                                                                 IConfiguration configuration)
        {
            services.Configure<FrontendOptions>(configuration.GetSection("FrontEndOptions"));
            services.Configure<ModuleCollection>(configuration.GetSection("Modules"));
            services.AddHostedService<BackendProcessRunner>();
            return services;
        }
    }
}
