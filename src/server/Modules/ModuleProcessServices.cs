using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CodeProject.AI.Server.Backend;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// Manages the ModuleProcesses.
    /// </summary>
    public class ModuleProcessServices
    {
        private static readonly ConcurrentDictionary<string, ProcessStatus> _processStatuses  = new();
        private static readonly ConcurrentDictionary<string, Process>       _runningProcesses = new();

        private readonly VersionConfig                  _versionConfig;
        private readonly ServerOptions                  _serverOptions;
        private readonly QueueServices                  _queueServices;
        private readonly ILogger<ModuleProcessServices> _logger;
        private readonly ModuleSettings                 _moduleSettings;
        private readonly BackendRouteMap                _routeMap;

        /// <summary>
        /// Initializes a new instance of the ModuleProcessServices class.
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="serverOptions">The Server Options.</param>
        /// <param name="queueServices">The QueueServices instance.</param>
        /// <param name="logger">The Logger.</param>
        /// <param name="moduleSettings">The module settings.</param>
        /// <param name="routeMap">The BackendRouteMap.</param>
        public ModuleProcessServices(IOptions<VersionConfig> versionOptions,
                                     IOptions<ServerOptions> serverOptions,
                                     QueueServices queueServices, 
                                     ILogger<ModuleProcessServices> logger,
                                     ModuleSettings moduleSettings,
                                     BackendRouteMap routeMap)
        {
            _versionConfig  = versionOptions.Value;
            _serverOptions  = serverOptions.Value;
            _queueServices  = queueServices;
            _logger         = logger;
            _moduleSettings = moduleSettings;
            _routeMap       = routeMap;
        }

        /// <summary>
        /// Gets the count of processes.
        /// </summary>
        public int Count => _processStatuses.Count;

        /// <summary>
        /// Gets the environment variables applied to all processes.
        /// </summary>
        public Dictionary<string, object>? GlobalEnvironmentVariables
        {
            get { return _serverOptions?.EnvironmentVariables; }
        }

        /// <summary>
        /// Adds a ProcessProcessStatus for a module.
        /// </summary>
        /// <param name="moduleId">The ModuleId.</param>
        /// <param name="processStatus">The ProcessStatus</param>
        public void Add(string moduleId, ProcessStatus processStatus)
        {
            _processStatuses.TryAdd(moduleId, processStatus);
        }

        /// <summary>
        /// Gets the ProcessStatus for a module.
        /// </summary>
        /// <param name="moduleId">The moduleId</param>
        /// <returns>The ProcessStatus or null if not available.</returns>
        public ProcessStatus? GetProcessStatus(string moduleId)
        {
            _processStatuses.TryGetValue(moduleId, out ProcessStatus? processStatus);
            return processStatus;
        }

        /// <summary>
        /// Try and get a modules ProcessStatus.
        /// </summary>
        /// <param name="moduleId">The Module Id.</param>
        /// <param name="status">The ProcessStatus object to hold the result.</param>
        /// <returns>True if successful.</returns>
        public bool TryGetProcessStatus(string moduleId, out ProcessStatus? status)
        {
            return _processStatuses.TryGetValue(moduleId, out status) && status is not null;
        }

        /// <summary>
        /// Gets an IEnumerable of the ProcessStatuses.
        /// </summary>
        /// <returns>The list of ProcessStatuses.</returns>
        public IEnumerable<ProcessStatus> ListProcessStatuses()
        {
            return _processStatuses.Values;
        }

        /// <summary>
        /// Removes the ProcessStatus for a module.
        /// </summary>
        /// <param name="moduleId">The module id.</param>
        /// <returns>True if successful.</returns>
        public bool RemoveProcessStatus(string moduleId)
        {
            if (_processStatuses.ContainsKey(moduleId))
                return _processStatuses.TryRemove(moduleId, out _);

            return true;
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
            {
                _logger.LogDebug($"{module.ModuleId} doesn't appear in the Process list, so can't stop it.");
                return true;
            }

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
                  
                // Send a 'Quit' request but give it time to wrap things up before we step in further
                var payload = new RequestPayload("Quit");
                payload.SetValue("moduleId", module.ModuleId);
                await _queueServices.SendRequestAsync(module.Queue!, new BackendRequest(payload))
                                    .ConfigureAwait(false);

                int shutdownServerDelaySecs = _moduleSettings.DelayAfterStoppingModulesSecs;
                if (shutdownServerDelaySecs > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(shutdownServerDelaySecs))
                              .ConfigureAwait(false);
                }

                try
                {
                    if (!process.HasExited)
                    {
                        _logger.LogInformation($"Forcing shutdown of {process.ProcessName}/{module.ModuleId}");
                        process.Kill(true);

                        Stopwatch stopWatch = Stopwatch.StartNew();
                        _logger.LogDebug($"Waiting for {module.ModuleId} to end.");
        
                        await process.WaitForExitAsync().ConfigureAwait(false);
        
                        stopWatch.Stop();
                        _logger.LogDebug($"{module.ModuleId} ended after {stopWatch.ElapsedMilliseconds} ms");
                    }
                    else
                        _logger.LogInformation($"{module.ModuleId} went quietly");
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex, $"Error trying to stop {module.Name} ({module.FilePath})");
                    _logger.LogError(ex.Message);
                    _logger.LogError(ex.StackTrace);
                }
                finally
                {
                    Console.WriteLine("Removing module from Processes list");
                    _runningProcesses.TryRemove(module.ModuleId, out process);
                    process?.Dispose();
                }
            }
            else
                _logger.LogInformation($"{module.ModuleId} has left the building");


            return true;
        }

        /// <summary>
        /// Sets up the process queue and creates a ProcessStatus entry.
        /// </summary>
        /// <param name="module">The module to be started</param>
        /// <param name="launchingProcess">Whether or not we will be actually launching the module's
        /// process</param>
        /// <param name="installSummary">The installation summary, in case we want to display this
        /// later on</param>
        public void AddProcess(ModuleConfig module, bool launchingProcess, string? installSummary)
        {
            if (module?.ModuleId is null)
                return;
                
            if (TryGetProcessStatus(module?.ModuleId!, out ProcessStatus? _))
                return;

            string? summary = module!.SettingsSummary;
            if (!string.IsNullOrEmpty(summary))
            {
                // Expanding out the macros causes the display to be too wide
                summary = _moduleSettings.ExpandOption(summary, module.ModulePath);

                // But we can mitigate this somewhat  
                string appRoot = CodeProject.AI.Server.Program.ApplicationRootPath!;
                summary = summary?.Replace(appRoot, "&lt;root&gt;");
            }
            
            ProcessStatus status = new ProcessStatus()
            {
                ModuleId       = module!.ModuleId,
                Name           = module.Name,
                Version        = module.Version,
                Queue          = module.Queue,
                Status         = ProcessStatusType.Unknown,
                StartupSummary = summary ?? string.Empty,
                InstallSummary = installSummary ?? string.Empty,
            };
            _processStatuses.TryAdd(module.ModuleId, status);

            // Set the status of the Process prior to launching. This will be updated post launch.
            SetPreLaunchProcessStatus(module, launchingProcess);

            SetupQueueAndRoutes(module);
        }
        
        /// <summary>
        /// Starts, or restarts (if necessary and possible) a process. 
        /// </summary>
        /// <param name="module">The module to be started</param>
        /// <returns>True on success; false otherwise</returns>
        public async Task<bool> StartProcess(ModuleConfig module)
        {
            if (module is null || string.IsNullOrWhiteSpace(module.ModuleId))
                return false;

            if (!TryGetProcessStatus(module.ModuleId, out ProcessStatus? processStatus))
            {
                processStatus = new ProcessStatus()
                {
                    ModuleId = module.ModuleId,
                    Name     = module.Name,
                    Version  = module.Version,
                    Queue    = module.Queue,
                    Status   = ProcessStatusType.Unknown
                };
                _processStatuses.TryAdd(module.ModuleId, processStatus);
            }

            // The module will need its status to be "Enabled" in order to be launched. We set
            // "launchModules" = true here since this method will be called after the server has
            // already started. Only on server start do we entertain the possibility that we won't
            // actually start a module. At all other times we ensure they start.
            if (!SetPreLaunchProcessStatus(module, true))
                return false;

            SetPreLaunchProcessStatus(module, true);

            if (processStatus!.Status != ProcessStatusType.Enabled)
                return false;

            Process? process = null;
            try
            {
                ProcessStartInfo procStartInfo = CreateProcessStartInfo(module);

                _logger.LogDebug("");
                _logger.LogDebug($"Attempting to start {module.ModuleId} with {procStartInfo.FileName} {procStartInfo.Arguments}");

                process = new Process
                {
                    StartInfo = procStartInfo,
                    EnableRaisingEvents = true
                };
                process.OutputDataReceived += SendOutputToLog;
                process.ErrorDataReceived  += SendErrorToLog;
                process.Exited             += ModuleExecutionComplete;

                _runningProcesses.TryAdd(module.ModuleId, process);

                // Start the process
                _logger.LogTrace($"Starting {Text.ShrinkPath(process.StartInfo.FileName, 50)} {Text.ShrinkPath(process.StartInfo.Arguments, 50)}");

                string summary = module.SettingsSummary;
                string[] lines = summary.Split('\n');

                _logger.LogInformation("");
                foreach (string line in lines)
                    _logger.LogInformation($"** {line.Trim()}");
                _logger.LogInformation("");

                processStatus.Status = ProcessStatusType.Starting;

                if (!process.Start())
                {
                    process = null;
                    processStatus.Status = ProcessStatusType.FailedStart;
                }

                if (process is not null)
                {
                    processStatus.Started = DateTime.UtcNow;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    _logger.LogInformation($"Started {module.Name} module");

                    int postStartPauseSecs = module.PostStartPauseSecs ?? 3;

                    // Trying to reduce startup CPU and instantaneous memory use for low resource
                    // environments such as Docker or RPi
                    await Task.Delay(TimeSpan.FromSeconds(postStartPauseSecs));
                    processStatus.Status = ProcessStatusType.Started;
                }
                else
                {
                    _logger.LogError($"Unable to start {module.Name} module");
                }
            }
            catch (Exception ex)
            {
                processStatus.Status = ProcessStatusType.FailedStart;

                _logger.LogError(ex, $"Error trying to start {module.Name} ({module.FilePath})");
                _logger.LogError(ex.Message);
                _logger.LogError(ex.StackTrace);
#if DEBUG
                _logger.LogError($" *** Did you setup the Development environment?");
                if (SystemInfo.IsWindows)
                    _logger.LogError($"     Run \\src\\setup.bat");
                else
                    _logger.LogError($"     In /src, run 'bash setup.sh'");

                _logger.LogError($"Exception: {ex.Message}");
#else
                _logger.LogError($"*** Please check the CodeProject.AI installation completed successfully");
#endif
            }

            if (process is null)
                _runningProcesses.TryRemove(module.ModuleId, out _);

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

            ProcessStatus? status = GetProcessStatus(module.ModuleId);
            if (status == null)
                return false;

            // We can't reuse a process (easily). Kill the old and create a brand new one
            if (_runningProcesses.TryGetValue(module.ModuleId, out Process? process) && process != null)
            {
                status.Status = ProcessStatusType.Stopping;
                await KillProcess(module).ConfigureAwait(false);
                status.Status = ProcessStatusType.Stopped;
            }
            else
                status.Status = ProcessStatusType.Stopped;

            // If we're actually meant to be killing this process, then just leave now.
            if (module.AutoStart == false || !module.Available(SystemInfo.Platform, _versionConfig.VersionInfo?.Version))
                return true;

            return await StartProcess(module).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the current status of the module just before it's to be started
        /// </summary>
        /// <param name="module">The module to be restarted</param>
        /// <param name="launchingProcess">Whether or not we will be actually launching the module's
        /// process</param>
        /// <returns>True on success; false otherwise</returns>
        public bool SetPreLaunchProcessStatus(ModuleConfig module, bool launchingProcess)
        {
            ProcessStatus? status = GetProcessStatus(module.ModuleId!);
            if (status == null)
            {
                _logger.LogWarning($"No process status found defined for {module.Name}");
                return false;
            }

            // setup the routes for this module.
            if (module.Available(SystemInfo.Platform, _versionConfig.VersionInfo?.Version))
            {
                if (!module.AutoStart == true)
                    status.Status = ProcessStatusType.NotEnabled;
                else if (launchingProcess)
                    status.Status = ProcessStatusType.Enabled;
                else
                    status.Status = ProcessStatusType.NotStarted;
            }
            else
                status.Status = ProcessStatusType.NotAvailable;

            return true;
        }

        /// <summary>
        /// Setups up the Queues and Routes for a module.
        /// </summary>
        /// <param name="module">The Module Configuration.</param>
        /// <returns>True if successful.</returns>
        public bool SetupQueueAndRoutes(ModuleConfig module)
        {
            if (string.IsNullOrWhiteSpace(module.Queue))
            {
                _logger.LogWarning($"No queue specified for {module.Name}");
                return false;
            }
                
            if (module.RouteMaps?.Any() != true)
            {
                _logger.LogWarning($"No routes defined for {module.Name}");
                return false;
            }

            _queueServices.EnsureQueueExists(module.Queue);

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

            _logger.LogTrace($"Command: {command}");

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

            if (!string.IsNullOrEmpty(filename))
                filename += ": ";

            var testString = message.ToLower();

            // We're picking up messages written to the console so let's provide a little help for
            // messages that are trying to get themselves categorised properly.
            // Optimisation: We probably should order these by info/trace/debug/warn/error/crit, but
            // for sanity we'll keep them in order of anxiety.
            if (testString.StartsWith("crit: "))
                _logger.LogCritical(filename + message.Substring("crit: ".Length));
            else if (testString.StartsWith("critical: "))
                _logger.LogCritical(filename + message.Substring("critical: ".Length));
            else if (testString.StartsWith("err: "))
                _logger.LogError(filename + message.Substring("err: ".Length));
            else if (testString.StartsWith("error: "))
                _logger.LogError(filename + message.Substring("error: ".Length));
            else if (testString.StartsWith("warn: "))
                _logger.LogWarning(filename + message.Substring("warn: ".Length));
            else if (testString.StartsWith("warning: "))
                _logger.LogWarning(filename + message.Substring("warning: ".Length));
            else if (testString.StartsWith("info: "))
                _logger.LogInformation(filename + message.Substring("info: ".Length));
            else if (testString.StartsWith("information: "))
                _logger.LogInformation(filename + message.Substring("information: ".Length));
            else if (testString.StartsWith("dbg: "))
                _logger.LogDebug(filename + message.Substring("dbg: ".Length));
            else if (testString.StartsWith("debug: "))
                _logger.LogDebug(filename + message.Substring("debug: ".Length));
            else if (testString.StartsWith("trc: "))
                _logger.LogTrace(filename + message.Substring("trc: ".Length));
            else if (testString.StartsWith("trace: "))
                _logger.LogTrace(filename + message.Substring("trace: ".Length));
            else
                _logger.LogInformation(filename + message);
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

            if (!string.IsNullOrEmpty(filename))
                filename += ": ";

            if (string.IsNullOrEmpty(error))
                error = "No error provided";

             _logger.LogError(filename + error);
        }

        /// <summary>
        /// This is called once the module's execution has completed (shutdown or crash)
        /// </summary>
        /// <param name="sender">The process</param>
        /// <param name="e">The event args</param>
        private void ModuleExecutionComplete(object? sender, EventArgs e)
        {
            if (sender is Process process)
            {
                string directory = process.StartInfo.WorkingDirectory;
                string? moduleId = new DirectoryInfo(directory).Name;
                if (moduleId is null)
                {
                    _logger.LogError($"Module in {directory} has shutdown, but can't find the module itself");
                    return;
                }

                _logger.LogInformation($"** Module {moduleId} has shutdown");

                // Remove this from the list of running processes
                if (_runningProcesses.TryGetValue(moduleId, out _))
                    _runningProcesses.TryRemove(moduleId, out _);
            }
        }

        /// <summary>
        /// Creates the collection of backend environment variables.
        /// </summary>
        /// <param name="module">The current module</param>
        /// <param name="currentModulePath">The path to the current module, if appropriate.</param>
        private Dictionary<string, string?> BuildBackendEnvironmentVar(ModuleConfig module,
                                                                       string? currentModulePath = null)
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
            processEnvironmentVars.TryAdd("CPAI_MODULE_SERVER_LAUNCHED", "true");
            processEnvironmentVars.TryAdd("CPAI_MODULE_ID",          module.ModuleId);
            processEnvironmentVars.TryAdd("CPAI_MODULE_NAME",        module.Name);
            processEnvironmentVars.TryAdd("CPAI_MODULE_PATH",        _moduleSettings.GetModulePath(module));
            processEnvironmentVars.TryAdd("CPAI_MODULE_PARALLELISM", module.Parallelism.ToString());
            processEnvironmentVars.TryAdd("CPAI_MODULE_QUEUENAME",   module.Queue);
            if ((module.RequiredMb ?? 0) > 0)
                processEnvironmentVars.TryAdd("CPAI_MODULE_REQUIRED_MB", module.RequiredMb?.ToString());
            processEnvironmentVars.TryAdd("CPAI_MODULE_SUPPORT_GPU", (module.SupportGPU ?? false).ToString());
            processEnvironmentVars.TryAdd("CPAI_ACCEL_DEVICE_NAME",  module.AcceleratorDeviceName);
            processEnvironmentVars.TryAdd("CPAI_HALF_PRECISION",     module.HalfPrecision);
            processEnvironmentVars.TryAdd("CPAI_LOG_VERBOSITY",      (module.LogVerbosity ?? LogVerbosity.Info).ToString());

            // Make sure the runtime environment variables used by the server are passed to the
            // child process. Otherwise the NET module may start in Production mode. We *hope* the
            // environment vars are passed down to to spawned processes, but we'll add these two
            // just in case.
            var aspnetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (aspnetEnv != null)
                processEnvironmentVars.TryAdd("ASPNETCORE_ENVIRONMENT", aspnetEnv);

            var dotnetEnv = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (dotnetEnv != null)
                processEnvironmentVars.TryAdd("DOTNET_ENVIRONMENT", dotnetEnv);

            return processEnvironmentVars;
        }
    }
}
