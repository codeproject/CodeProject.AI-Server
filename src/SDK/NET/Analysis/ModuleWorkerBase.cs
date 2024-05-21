using System.Diagnostics;
using System.Dynamic;
using System.Net.Http.Json;

using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeProject.AI.SDK
{
    /// <summary>
    /// The base class from which a module should be derived.
    /// </summary>
    public abstract class ModuleWorkerBase : BackgroundService
    {
        private readonly string[] _doNotLogCommands = { 
            "list-custom", 
            "get_module_status", "status", "get_status", // status is deprecated alias
            "get_command_status"
        };

        private readonly TimeSpan      _status_delay = TimeSpan.FromSeconds(2);

        private readonly string?       _queueName;
        private readonly string?       _moduleId;
        private readonly int           _parallelism     = 1;
        private readonly string?       _accelDeviceName = null;
        private readonly string        _halfPrecision   = "enable"; // Can be enable, disable or force
        private readonly string        _logVerbosity    = "info";   // Can be Quiet, Info or Loud

        private readonly BackendClient _apiClient;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ILogger       _logger;
        private readonly IHostApplicationLifetime _appLifetime;

        private bool _cancelled         = false;
        private bool _performSelfTest   = false;
        private int  _successfulInferences;
        private long _totalSuccessInferenceMs;
        private int  _failedInferences;

        private Task<ModuleResponse>? _longRunningTask;
        private string? _longRunningCommandId;
        private ModuleResponse? _lastLongRunningOutput;
        private CancellationTokenSource? _longProcessCancellationTokenSource;

        /// <summary>
        /// Gets or sets the name of this Module
        /// </summary>
        public string? ModuleName { get; set; }

        /// <summary>
        /// Gets or sets the path to this Module
        /// </summary>
        public string? moduleDirPath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this module supports GPU acceleration
        /// </summary>
        public bool   EnableGPU  { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not this detector can use the current GPU
        /// </summary>
        public bool CanUseGPU { get; set; } = false;

        /// <summary>
        /// Gets or sets the hardware type that's in use (CPU or GPU)
        /// </summary>
        public string? InferenceDevice { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the name of the hardware acceleration execution provider (meaning the
        /// library being used to power the GPU, such as DirectML, Torch, TF etc)
        /// </summary>
        public string? InferenceLibrary { get; set; } = string.Empty;

        /// <summary>
        /// Gets the logger instance.
        /// </summary>
        public ILogger Logger { get => _logger; }

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="configuration">The app configuration values.</param>
        /// <param name="hostApplicationLifetime">The applicationLifetime object</param>
        public ModuleWorkerBase(ILogger logger, IConfiguration configuration,
                                IHostApplicationLifetime hostApplicationLifetime)
        {
            // _logger      = logger;

            using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = factory.CreateLogger<ModuleWorkerBase>();

            _cancelled   = false;
            _appLifetime = hostApplicationLifetime;

            string currentModuleDirPath = GetModuleDirectoryPath();
            string currentDirName       = new DirectoryInfo(currentModuleDirPath).Name;

            _moduleId        = configuration.GetValue<string?>("CPAI_MODULE_ID", null)        ?? currentDirName;
            ModuleName       = configuration.GetValue<string?>("CPAI_MODULE_NAME", null)      ?? _moduleId;
            moduleDirPath    = configuration.GetValue<string?>("CPAI_MODULE_PATH", null)      ?? currentModuleDirPath;
            _queueName       = configuration.GetValue<string?>("CPAI_MODULE_QUEUENAME", null) ?? _moduleId.ToLower() + "_queue";

            int port         = configuration.GetValue<int>("CPAI_PORT", 32168);
            _parallelism     = configuration.GetValue<int>("CPAI_MODULE_PARALLELISM", 0);

            EnableGPU        = configuration.GetValue<bool>("CPAI_MODULE_ENABLE_GPU",   true);
            _accelDeviceName = configuration.GetValue<string?>("CPAI_ACCEL_DEVICE_NAME", null);
            _halfPrecision   = configuration.GetValue<string?>("CPAI_HALF_PRECISION", null) ?? "enable"; // Can be enable, disable or force
            _logVerbosity    = configuration.GetValue<string?>("CPAI_LOG_VERBOSITY", null)  ?? "quiet";   // Can be Quiet, Info or Loud

            _performSelfTest = configuration.GetValue<bool>("CPAI_MODULE_DO_SELFTEST", false);

            // We have a reasonably short "long poll" time to ensure the dashboard gets regular pings
            // to indicate the module is still alive
            TimeSpan longPoll        = TimeSpan.FromSeconds(15); // For getting a request from the server
            TimeSpan responseTimeout = TimeSpan.FromSeconds(10); // For sending info to the server

            // We want this big enough to increase throughput, but not so big as to
            // cause thread starvation.
            if (_parallelism == 0)
                _parallelism = Environment.ProcessorCount/2;

            var token = _cancellationTokenSource.Token;
            _apiClient = new BackendClient($"http://localhost:{port}/", longPoll, responseTimeout,
                                           token: token);
        }

#region CodeProject.AI Module callbacks ============================================================

        /// <summary>
        /// Called before the main processing loops are started
        /// </summary>
        protected virtual void Initialize()
        {
        }

        /// <summary>
        /// Processes the request receive from the server queue.
        /// </summary>
        /// <param name="request">The Request data.</param>
        /// <returns>An object to serialize back to the server.</returns>
        protected abstract ModuleResponse Process(BackendRequest request);

        /// <summary>
        /// Returns an object containing current stats for this module
        /// </summary>
        /// <returns>An object</returns>
        protected virtual ExpandoObject? ModuleStatus()
        {
            ExpandoObject status = new {
                inferenceDevice      = InferenceDevice,
                inferenceLibrary     = InferenceLibrary,
                canUseGPU            = CanUseGPU,
                
                successfulInferences = _successfulInferences,
                failedInferences     = _failedInferences,
                numInferences        = _successfulInferences + _failedInferences,
                averageInferenceMs   = _successfulInferences > 0 
                                        ? _totalSuccessInferenceMs / _successfulInferences : 0
            }.ToExpando();

            return status;
        }

        /// <summary>
        /// Returns the status of a long running command
        /// </summary>
        /// <returns>A ExpandoObject object</returns>
        protected virtual ExpandoObject? CommandStatus()
        {
            return null;
        }

        /// <summary>
        /// Called when this module is about to be cancelled
        /// </summary>
        protected virtual void CancelCommandTask()
        {
        }

        /// <summary>
        /// Called after `process` is called in order to update the stats on the number of successful
        /// and failed calls as well as average inference time.
        /// </summary>
        /// <param name="response"></param>
        protected virtual void UpdateStatistics(ModuleResponse? response)
        {
            if (response is null)
                return;

            if (response.Success)
            {
                _successfulInferences++;
                _totalSuccessInferenceMs += response.InferenceMs;   
            }
            else
                _failedInferences++;
        }

        /// <summary>
        /// Called when the module is asked to execute a self-test to ensure it install and runs
        /// correctly
        /// </summary>
        /// <returns>An exit code for the test. 0 = no error.</returns>
        protected virtual int SelfTest()
        {
            return 0;
        }

        /// <summary>
        /// Called when the module is asked to shutdown. This provides a chance to clean up 
        /// resources.
        /// </summary>
        protected virtual void Cleanup()
        {
        }

#endregion CodeProject.AI Module callbacks =========================================================

        private async Task ModuleStatusUpdateLoop(CancellationToken token)
        {
            await Task.Yield();

            while (!token.IsCancellationRequested && !_cancelled)
            {
                await _apiClient.SendModuleStatus(_moduleId!, ModuleStatus(), token).ConfigureAwait(false);
                await Task.Delay(_status_delay, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// This is the main processing loop which polls the request queue on the server for this
        /// module, gets the requests from the server, calls the Process method, then sends the
        /// results back to the server
        /// </summary>
        /// <param name="token">The cancellation token</param>
        /// <param name="taskNumber">The task number, starting from 0. When this module is started,
        /// multiple ProcessQueue loops are setup based on the Parallelism value. taskNumber
        /// represents the loop number in order of creation.</param>
        /// <returns>A Task</returns>
        private async Task ProcessQueue(CancellationToken token, int taskNumber)
        {
            Task<BackendRequest?> requestTask = _apiClient.GetRequest(_queueName!, _moduleId!,
                                                                      token);
            Task? responseTask = null;
            BackendRequest? request;

            while (!token.IsCancellationRequested && !_cancelled)
            {
                try
                {
                    request     = await requestTask.ConfigureAwait(false);
                    requestTask = _apiClient.GetRequest(_queueName!, _moduleId!, token);
                    if (request is null)
                        continue;

                    string? command = request.payload?.command?.ToLower();
                    if (command == null)
                        continue;

                    // Special shutdown request
                    string? requestModuleId = request.payload?.GetValue("moduleId");
                    if (command == "quit" && requestModuleId?.EqualsIgnoreCase(_moduleId) == true)
                    {
                        await ShutDown(0);
                        return;
                    }

                    Stopwatch stopWatch = Stopwatch.StartNew();

                    ExpandoObject response = command switch
                    {
                        "get_module_status"  => GetModuleStatus(request),
                        "get_command_status" => await GetCommandStatus(request).ConfigureAwait(false),
                        "cancel_command"     => CancelRequest(request).ToExpando(),
                        _                    => ProcessModuleCommands(request)
                    };

                    stopWatch.Stop();

                    // Fill in system-added values for the response
                    response = response.Merge(new {
                        ModuleName = ModuleName,
                        ModuleId   = _moduleId,
                        ProcessMs  = stopWatch.ElapsedMilliseconds,
                        Command    = command ?? string.Empty,
                        RequestId  = request.reqid
                    }.ToExpando());

                    // Slightly faster as we don't wait for the request to complete before moving
                    // on to the next.
                    if (responseTask is not null)
                        await responseTask.ConfigureAwait(false);

                    /* This doesn't actually work
                    var options = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    HttpContent content = JsonContent.Create(response, response.GetType(), options: options);
                    */

                    response.ToCamelCase();
                    HttpContent content = JsonContent.Create(response, response.GetType());
                    responseTask = _apiClient.SendResponse(request.reqid, _moduleId!, content, token);
                }
                catch (TaskCanceledException) when (_cancelled)
                {
                    // Just ignore this.  We are probably shutting down.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{ModuleName} Exception");
                    continue;
                }
            }

            if (_cancelled)
                Console.WriteLine("Shutdown signal received. Ending loop");

            _cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Processes a command sent to a module.
        /// </summary>
        /// <param name="request">The incoming request</param>
        /// <returns>An expando object with the results</returns>
        private ExpandoObject ProcessModuleCommands(BackendRequest request)
        {
            ExpandoObject response;

            ModuleResponse moduleResponse = Process(request);

            if (!_doNotLogCommands.Contains(request.reqtype))
                UpdateStatistics(moduleResponse);

            if (moduleResponse.LongProcessMethod is not null)
            {
                if (_longRunningTask is not null && !_longRunningTask.IsCompleted)
                {
                    response = new
                    {
                        Success   = false,
                        CommandId = _longRunningCommandId,
                        Error     = "A long running command is already in progress"
                    }.ToExpando();
                }
                else
                {
                    // We have a previous long running process that is now done, but we
                    // have not stored (nor returned) the result. We can read the result
                    // now, but we have to start a new process, so...???
                    // if (_longRunningTask is not null && _longRunningTask.IsCompleted &&
                    //     _lastLongRunningLastOutput is null)
                    //    _lastLongRunningLastOutput = ...

                    // Store request Id as the command Id for later, reset the last result
                    string? commandId = request.reqid;

                    _longRunningCommandId  = commandId;
                    _lastLongRunningOutput = null;
                    _longRunningTask       = null;

                    // Start the long running process
                    Console.WriteLine("Starting long process with command ID " + commandId);

                    _longProcessCancellationTokenSource = new CancellationTokenSource();
                    CancellationToken cancellationToken = _longProcessCancellationTokenSource.Token;
                    _longRunningTask = moduleResponse.LongProcessMethod(request, cancellationToken);

                    response = new
                    {
                        Success = true,
                        CommandId = commandId,
                        Message = "Command is running in the background",
                        CommandStatus = "running"
                    }.ToExpando();
                }
            }
            else
            {
                response = moduleResponse.ToExpando();
            }

            return response;
        }

        /// <summary>
        /// This stops the application
        /// </summary>
        protected async Task ShutDown(int exitCode)
        {
            _cancelled = true;
            Environment.ExitCode = exitCode;

            await Task.Delay(1).ConfigureAwait(false);
            // The 'proper' way.
            _appLifetime.StopApplication();

            // The other proper way
            // await StopAsync(token).ConfigureAwait(false);
        }

        protected ExpandoObject GetModuleStatus(BackendRequest request)
        {
            return new { Success = false }.ToExpando();
        }

        protected async Task<ExpandoObject> GetCommandStatus(BackendRequest request)
        {
            string? commandId = request.payload?.GetValue("commandId");

            if (_longRunningTask is null)
                return new { Success = false, Error = "There is no current command running" }.ToExpando();

            if (string.IsNullOrWhiteSpace(commandId))
                return new { Success = false, Error = "No command ID provided" }.ToExpando();

            if (string.IsNullOrWhiteSpace(_longRunningCommandId))
                return new { Success = false, Error = "No command is currently in progress" }.ToExpando();

            if (!commandId.EqualsIgnoreCase(_longRunningCommandId))
                return new { Success = false, Error = $"The command {_longRunningCommandId} does not exist" }.ToExpando();

            if (_longRunningTask.IsCompleted)
            {
                // Get the output, but get it only once
                if (_lastLongRunningOutput is null)
                {
                    try
                    {
                        _lastLongRunningOutput = await _longRunningTask;
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"{nameof(OperationCanceledException)} thrown");
                        _lastLongRunningOutput = new ModuleErrorResponse("The long operation was cancelled");
                    }
                    finally
                    {
                        _longProcessCancellationTokenSource?.Dispose();
                        _longProcessCancellationTokenSource = null;
                    }
                }

                var completedStatus = new {
                    Success       = true,
                    Message       = "The command has completed",
                    CommandId     = commandId,
                    CommandStatus = "completed",
                    Result        = _lastLongRunningOutput
                }.ToExpando();

                return completedStatus.Merge(_lastLongRunningOutput.ToExpando());
            }

            // Finally, we have a command in progress, so return the intermediate status
            ExpandoObject runningStatus = new {
                Success       = true,
                Message       = "The command is still in progress",
                CommandId     = commandId,
                CommandStatus = "running"
            }.ToExpando();
            ExpandoObject? commandStatus = CommandStatus();

            var status = runningStatus.Merge(commandStatus);
            return status;
        }

        protected ModuleResponse CancelRequest(BackendRequest request)
        {
            string? commandId = request.payload.GetValue("commandId");
            if (string.IsNullOrWhiteSpace(commandId))
                return new ModuleErrorResponse("No command ID provided");

            if (string.IsNullOrWhiteSpace(_longRunningCommandId))
                return new ModuleErrorResponse("No long running command is currently in progress");

            if (!commandId.EqualsIgnoreCase(_longRunningCommandId))
                return new ModuleErrorResponse("The command ID provided does not match the current long running command");

            if (_longRunningTask is null)
                return new ModuleErrorResponse("The long running command task has been lost");
        
            // Call the module's override method. This is where the module author can do whatever
            // they need to do to gracefully shutdown the long running process.
            CancelCommandTask();

            // In the Python implementation we have a 'force_shutdown' var that, if set, will instruct
            // the calling code to forcibly shutdown the long process on cancel in case the 
            // 'CancelCommandTask' call didn't result in the long process actually shutting down. We
            // can't safely do that here. Instead we use the usual cancellation tokens and rely on 
            // the long process itself to shutdown as required
            _longProcessCancellationTokenSource?.Cancel();

            // NOTE: At this point the long running task may still be running! 
            if (_longRunningTask.IsCompleted)
            {
                _longRunningCommandId = null;
                _longRunningTask      = null;
            }

            var output = new ModuleLongProcessCancelResponse()
            {
                Success         = true,
                Message         =  "The command has been cancelled",
                CommandId       = request.reqid,
                CommandStatus   = "Cancelled",

                ModuleId        = _moduleId,
                ModuleName      = ModuleName,
                Command         = request.payload.command,
                RequestId       = request.reqid
            };

            return output;
        }

#region Service Worker Start/Stop/Cleanup overrides ================================================

        /// <summary>
        /// Start the process. We need to override ExecuteAsync since it's an abstract class. This
        /// service is started via a call to StartAsync which calls ExecuteAsync. We could override,
        /// StartAsync but the default implementation is sufficient, and we have to override
        /// ExecuteAsync anyway.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A Task</returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {           
            // The way BackgroundServices are Started, the running of the host and other services is
            // delayed until this method returns. The await Delay of any size is enough to allow the
            // host to start up and run the other services. A delay of 1 second allows the Server to
            // start up and be ready to receive requests.
            try
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // the loop will terminate if the cancellation token is cancelled.
            }

            _apiClient.LogToServer($"{ModuleName} module started.", $"{ModuleName}",
                                   LogLevel.Information, string.Empty);
            Initialize();

            if (_performSelfTest)
            {
                int exitCode = SelfTest();
                Cleanup();

                await ShutDown(exitCode);
            }
            else
            {
                _cancelled = false;
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < _parallelism; i++)
                    tasks.Add(ProcessQueue(token, i));

                tasks.Add(ModuleStatusUpdateLoop(token));

                await Task.WhenAll(tasks).ConfigureAwait(false);

                Cleanup();
            }
        }

        /// <summary>
        /// Stop the process.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns>A Task</returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _cancelled = true;

            _apiClient.LogToServer($"Shutting down {_moduleId}", _moduleId!,
                                   LogLevel.Information, string.Empty);

            _cancellationTokenSource.Cancel();
            _longProcessCancellationTokenSource?.Cancel();

            await base.StopAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes of this classes resources
        /// </summary>
        public override void Dispose()
        {
            _cancellationTokenSource?.Dispose();
            _longProcessCancellationTokenSource?.Dispose();

            base.Dispose();
            GC.SuppressFinalize(this);
        }

#endregion Service Worker Start/Stop/Cleanup =======================================================

        public static void ProcessArguments(string[]? args)
        {
            if (args is null)
                return;

            foreach (var arg in args)
            {
                if (arg == "--selftest")
                    Environment.SetEnvironmentVariable("CPAI_MODULE_DO_SELFTEST", "true");
            }
        }

        protected string GetModuleDirectoryPath()
        {
            string moduleDirPath = AppContext.BaseDirectory;
            DirectoryInfo? info  = new DirectoryInfo(moduleDirPath);

            // HACK: If we're running this server from the build output dir in dev environment
            // then the root path will be wrong.
            if (SystemInfo.IsDevelopmentCode)
            {
                while (info != null)
                {
                    info = info.Parent;
                    if (info?.Name.ToLower() == "bin")
                    {
                        info = info.Parent;
                        break;
                    }
                }
            }

            if (info != null)
                return info.FullName;

            return moduleDirPath;
        }
    }
}
