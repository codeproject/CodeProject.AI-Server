using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.AnalysisLayer.SDK;
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

        // Tracks the status of each module
        private readonly Dictionary<string, ProcessStatus> _processStatuses;

        private readonly IConfiguration                _config;

        private readonly ILogger<BackendProcessRunner> _logger;
        private readonly QueueServices                 _queueServices;
        private readonly BackendRouteMap               _routeMap;
        private readonly Dictionary<string, Process>   _runningProcesses = new();
        private readonly string?                       _appDataDirectory;

        private readonly ModuleCollection _emptyModuleList = new();

        /// <summary>
        /// Gets the current platform name
        /// </summary>
        public static string Platform
        {
            get
            {
                // TODO: Docker is an environment in which we're running Linux. We probably need to
                // have OS, Environment (Docker, VSCode), Platform (x86-64, Arm64), Accelerator
                // (CPU, GPU-Gen5, NVIDIA)
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
        /// Gets a collection of the processes names and statuses.
        /// </summary>
        public Dictionary<string, ProcessStatus> ProcessStatuses
        {
            get { return _processStatuses; }
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
            // HACK: If we're running this server from the build output dir in  dev environment
            // then the path will be wrong.

            // 1. Scoot up the tree to check for build folders
            bool inDevEnvironment = false;
            DirectoryInfo? info = new DirectoryInfo(rootPath);
            while (info != null)
            {
                if (info.Name.ToLower() == "debug" || info.Name.ToLower() == "release")
                {
                    inDevEnvironment = true;
                    break;
                }

                info = info.Parent;
            }

            // 2. If we found a build folder, keep going up till we find the app root
            if (inDevEnvironment)
            {
                while (info != null)
                {
                    info = info.Parent;
                    if (info?.Name.ToLower() == "server")
                    {
                        info = info.Parent; // This will be the root in the installed version

                        // For debug / dev environment, the parent is API, followed by src
                        if (info?.Name.ToLower() == "api")
                        {
                            info = info.Parent;
                            if (info?.Name.ToLower() == "src")
                            {
                                info = info.Parent;
                                // HACK. Oh Lord, save me from this awfu hack. But it's the only
                                // way we can double-click the exe in the Release or Debug folder
                                // and have it run. Except this bit isn't working yet.
                                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "development");
                            }
                            else
                                info = null;
                        }

                        break;
                    }
                }

                if (info != null)
                    rootPath = info.FullName;
            }
            */

            return rootPath;
        }

        /// <summary>
        /// Initialises a new instance of the BackendProcessRunner.
        /// </summary>
        /// <param name="options">The Frontend Options</param>
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

            _processStatuses = new Dictionary<string, ProcessStatus>();
            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                string moduleId      = entry.Key;

                module.ModuleId = moduleId;

                if (!module.Valid)
                    continue;

                _processStatuses.Add(moduleId, new ProcessStatus()
                {
                    ModuleId = moduleId,
                    Name     = module.Name,
                    Status   = module.Activate == true
                             ? ProcessStatusType.Enabled : ProcessStatusType.NotEnabled,
                });
            }

            // ApplicationDataDir is set in Program.cs and added to an InMemoryCollection config set.
            _appDataDirectory = config.GetValue<string>("ApplicationDataDir");

            ExpandMacros();
        }

        /// <summary>
        /// Gets the backend process status for a queue.
        /// </summary>
        /// <param name="queueName">The Queue Name.</param>
        /// <returns>The status for the backend process, or false if the queue is invalid.</returns>
        public ProcessStatusType GetStatusForQueue(string queueName)
        {
            ModuleConfig? module = StartupProcesses.Values
                                                   .FirstOrDefault(module => module.HasQueue(queueName));

            if (module?.ModuleId == null)
                return ProcessStatusType.Unknown;

            ProcessStatus status = _processStatuses[module.ModuleId];
            return status == null ? ProcessStatusType.Unknown : status.Status;
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

            Parallel.ForEach(_modules.Values, module => KillProcess(module).Wait());

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
                    _logger.LogError("No Background AI Modules specified");
                    return;
                }

                _logger.LogTrace("Starting Background AI Modules");
            }
            catch
            {
                loggerIsValid = false;
            }

            bool launchAnalysisServices          = _config.GetValue("LaunchAnalysisServices", true);
            int  launchAnalysisServicesDelaySecs = _config.GetValue("LaunchAnalysisServicesDelaySecs", 3);

            // Setup routes.  Do this first so they are active during debug without launching services.
            foreach (var entry in _modules!)
            {
                ModuleConfig? module = entry.Value;
                string moduleId      = entry.Key;

                if (!module.Valid)
                    continue;

                // create the required Queues even in debug with LaunchAnalysisServices=false
                foreach (var routeInfo in module.RouteMaps)
                    if (!string.IsNullOrWhiteSpace(routeInfo.Queue))
                        _queueServices.EnsureQueueExists(routeInfo.Queue);

                ProcessStatus status = _processStatuses[moduleId];
                if (status == null)
                {
                    _logger.LogWarning($"No process status found defined for {module.Name}");
                    continue;
                }

                status.Status = ProcessStatusType.NotEnabled;

                // setup the routes for this module.
                if (IsEnabled(module))
                {
                    if (launchAnalysisServices)
                        status.Status = ProcessStatusType.Enabled;
                    else
                        status.Status = ProcessStatusType.NotStarted;

                    if (!(module.RouteMaps?.Any() ?? false))
                    {
                        _logger.LogWarning($"No routes defined for {module.Name}");
                    }
                    else
                    {
                        foreach (var routeInfo in module.RouteMaps!)
                            _routeMap.Register(routeInfo);
                    }
                }
            }

            if (!launchAnalysisServices)
            {
                _logger.LogWarning("Skipping Background AI Modules startup");
                return;
            }

            // Let's make sure the front end is up and running before we start the backend 
            // analysis services
            await Task.Delay(TimeSpan.FromSeconds(launchAnalysisServicesDelaySecs), stoppingToken);

            if (loggerIsValid)
            {
                _logger.LogDebug($"Root Path:   {_frontendOptions.ROOT_PATH}");
                _logger.LogDebug($"Module Path: {_frontendOptions.MODULES_PATH}");
                _logger.LogDebug($"Python Path: {_frontendOptions.PYTHON_PATH}");
                _logger.LogDebug($"Temp Dir:    {Path.GetTempPath()}");
                _logger.LogDebug($"Data Dir:    {_appDataDirectory}");

                _logger.LogDebug($"App directory {_frontendOptions.ROOT_PATH}");
                _logger.LogDebug($"Analysis modules in {_frontendOptions.MODULES_PATH}");
            }

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

                if (status.Status == ProcessStatusType.NotEnabled)
                {
                    // _logger.LogWarning($"Not starting {module.Name} (Not set as enabled)");
                }
                else
                {
                    _logger.LogInformation($"Attempting to start {module.Name}");
                    _logger.LogDebug($"  Runtime: {module.Runtime}, FilePath: {module.FilePath}");
                }

                if (status.Status == ProcessStatusType.Enabled)
                    await RestartProcess(module);
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

            if (!_runningProcesses.TryGetValue(module.ModuleId, out Process? process))
                return false;

            if (!process.HasExited)
            {
                _logger.LogInformation($"Sending shutdown request to {process.ProcessName}/{module.ModuleId}");
                RequestPayload payload = new() { command = "Quit" };
                await _queueServices.SendRequestAsync(module.QueueName() ?? "",
                                                      new BackendRequest("Quit", payload));

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

            return true;
        }

        /// <summary>
        /// Starts, or restarts if necessary, a process.
        /// </summary>
        /// <param name="module">The module to be started</param>
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
                _runningProcesses.Remove(module.ModuleId);
                status.Status = ProcessStatusType.Stopped;
            }
            else
                status.Status = ProcessStatusType.Stopped;

            // If we're actually meant to be killing this process, then just leave now.
            if (!IsEnabled(module))
                return true;

            if (module.RouteMaps?.Any() == true)
            {
                foreach (var routeInfo in module.RouteMaps)
                {
                    if (!string.IsNullOrWhiteSpace(routeInfo.Queue))
                    {
                        _queueServices.EnsureQueueExists(routeInfo.Queue);
                        _routeMap.Register(routeInfo);
                    }
                }
            }

            try
            {
                ProcessStartInfo procStartInfo = CreateProcessStartInfo(module);

                process = new Process();
                process.StartInfo = procStartInfo;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += SendOutputToLog;
                process.ErrorDataReceived  += SendErrorToLog;

                _runningProcesses.Add(module.ModuleId, process);

                // Start the process
                _logger.LogTrace($"Starting {ShrinkPath(process.StartInfo.FileName, 50)} {ShrinkPath(process.StartInfo.Arguments, 50)}");
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

                    _logger.LogInformation($"Started {module.Name} backend");

                    int postStartPauseSecs = module.PostStartPauseSecs ?? 5;

                    // Trying to reduce startup CPU and Memory for Docker
                    await Task.Delay(TimeSpan.FromSeconds(postStartPauseSecs));
                    status.Status = ProcessStatusType.Started;
                }
                else
                {
                     _logger.LogError($"Unable to start {module.Name} backend");
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
                if (Platform == "Windows")
                    _logger.LogError($"     Run /Installers/Dev/setup.dev.bat");
                else
                    _logger.LogError($"     In /Installers/Dev/, run 'bash setup.dev.sh'");

                _logger.LogError($"Exception: {ex.Message}");
#else
                _logger.LogError($"*** Please check the CodeProject.AI installation completed successfully");
#endif
            }

            if (process is null)
                _runningProcesses.Remove(module.ModuleId);

            return process != null;
        }

        private ProcessStartInfo CreateProcessStartInfo(ModuleConfig module)
        {
            string? command = ExpandOption(module.Command) ??
                              GetCommandByRuntime(module.Runtime) ??
                              GetCommandByExtension(module.FilePath);

            // Correcting for cross platform (win = \, linux = /)
            string filePath = Path.Combine(_frontendOptions.MODULES_PATH!,
                                           module.FilePath!.Replace('\\', Path.DirectorySeparatorChar));

            string? workingDirectory = module.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                workingDirectory = Path.GetDirectoryName(filePath);
            }
            else
            {
                workingDirectory = Path.Combine(_frontendOptions.MODULES_PATH!,
                                                workingDirectory!.Replace('\\', Path.DirectorySeparatorChar));
            }

            // Setup the process we're going to launch
#if Windows
            // Windows paths can have spaces so need quotes
            var executableName = $"\"{filePath}\"";
#else
            // the I'm assuming the directories don't have spaces in Linux and MacOS
            // because the Process.Start is choking on the quotes
            var executableName = filePath;
#endif
            ProcessStartInfo? procStartInfo = (command == "execute" || command == "launcher")
                ? new ProcessStartInfo(executableName)
                {
                    UseShellExecute        = false,
                    WorkingDirectory       = workingDirectory,
                    CreateNoWindow         = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                }
                : new ProcessStartInfo($"{command}", $"\"{filePath}\"")
                {
                    UseShellExecute        = false,
                    WorkingDirectory       = workingDirectory,
                    CreateNoWindow         = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                };

            // Set the environment variables
            Dictionary<string, string?> environmentVars = BuildBackendEnvironmentVar(module);
            foreach (var kv in environmentVars)
                procStartInfo.Environment.TryAdd(kv.Key, kv.Value);

            _logger.LogDebug("__________________________________________________________________");
            _logger.LogDebug("");
            _logger.LogDebug($"Setting Environment variables for {module.Name?.ToUpper()}");
            foreach (var envVar in environmentVars)
                _logger.LogDebug($"{envVar.Key,-16} = {envVar.Value}");
            _logger.LogDebug("__________________________________________________________________");

            return procStartInfo;
        }

        private static bool IsEnabled(ModuleConfig module)
        {
            // Has it been explicitely activated?
            bool enabled = module.Activate ?? false;

            // If the platform list doesn't include the current platform, then veto the activation
            if (enabled && !module.Platforms!.Any(p => p.ToLower() == "all") &&
                !module.Platforms!.Any(p => p.ToLower() == Platform.ToLower()))
                enabled = false;

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
            {
                // HACK: on docker the python command is in the format of python3.N
                string launcher = Platform == "Docker" ? runtime.Replace("python3", "python3.") 
                                                       : runtime;
                return _frontendOptions.PYTHON_PATH?.Replace(PythonRuntimeMarker, launcher);
            }

            if (runtime == "dotnet" || runtime == "execute" || runtime == "launcher")
                return runtime;

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

            return Path.GetExtension(filename) switch
            {
                ".py" => GetCommandByRuntime("python"),
                ".dll" => "dotnet",
                ".exe" => "execute",
                _ => throw new Exception("If neither Runtime nor Command is specified then FilePath must have an extension of '.py' or '.dll'."),
            };
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
            /* This same logic (and output) is sent to stdout so no need to duplicate here.
            if (sender is Process process)
            {
                filename = Path.GetFileName(process.StartInfo.Arguments.Replace("\"", ""));
                if (process.HasExited && string.IsNullOrEmpty(error))
                    error = "has exited";
            }
            */

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

            _logger.LogDebug("------------------------------------------------------------------");
            _logger.LogDebug($"Expanded ROOT_PATH       = {_frontendOptions.ROOT_PATH}");
            _logger.LogDebug($"Expanded MODULES_PATH    = {_frontendOptions.MODULES_PATH}");
            _logger.LogDebug($"Expanded PYTHON_BASEPATH = {_frontendOptions.PYTHON_BASEPATH}");
            _logger.LogDebug($"Expanded PYTHON_PATH     = {_frontendOptions.PYTHON_PATH}");
            _logger.LogDebug("------------------------------------------------------------------");
        }

        /// <summary>
        /// Expands the directory markers in the string.
        /// </summary>
        /// <param name="value">The value to expand.</param>
        /// <returns>The expanded path.</returns>
        private string? ExpandOption(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

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
            _frontendOptions.AddEnvironmentVariables(processEnvironmentVars);
            module.AddEnvironmentVariables(processEnvironmentVars);

            // Now perform the macro expansions

            // We could do this. But why waste the allocations...
            // processEnvironmentVars = processEnvironmentVars.ToDictionary(kvp => kvp.Key,
            //                                                              kvp => ExpandOption(kvp.Value));

            var keys = processEnvironmentVars.Keys.ToList();
            foreach (string key in keys)
            {
                string? value = processEnvironmentVars[key];
                processEnvironmentVars[key] = ExpandOption(value);
            }

            // And now add general vars
            processEnvironmentVars.TryAdd("CPAI_MODULE_ID",          module.ModuleId);
            processEnvironmentVars.TryAdd("CPAI_MODULE_NAME",        module.Name);
            processEnvironmentVars.TryAdd("CPAI_MODULE_PARALLELISM", module.Parallelism.ToString());
            processEnvironmentVars.TryAdd("CPAI_CUDA_DEVICE_NUM",    module.CudaDeviceNumber.ToString());
            processEnvironmentVars.TryAdd("CPAI_MODULE_SUPPORT_GPU", module.SupportGPU.ToString());
            processEnvironmentVars.TryAdd("CPAI_MODULE_QUEUENAME",   module.QueueName());

            return processEnvironmentVars;
        }

        private static string ShrinkPath(string path, int maxLength)
        {
            if (path.Length <= maxLength)
                return path;

            var parts = new List<string>(path.Split(new char[]{ '\\', '/' }));

            string start = parts[0] + "\\" + parts[1];
            parts.RemoveAt(1);
            parts.RemoveAt(0);

            string end = parts[^1];
            parts.RemoveAt(parts.Count - 1);

            parts.Insert(0, "...");
            while (parts.Count > 1 &&
                   start.Length + end.Length + parts.Sum(p => p.Length) + // Total length of parts
                   parts.Count > maxLength)                               // + '\' for each part
            {
                parts.RemoveAt(parts.Count - 1);
            }

            string mid = string.Empty;
            if (parts.Count > 0)
                parts.ForEach(p => mid += p + "\\");

            return start + mid + end;
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
            services.Configure<HostOptions>(hostOptions =>
            {
                hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            });
            services.AddHostedService<BackendProcessRunner>();
            return services;
        }
    }
}
