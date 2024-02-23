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
        private readonly string[] _doNotLogCommands = { "list-custom", "status" };

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
        /// Gets or sets the name of the hardware acceleration execution provider
        /// </summary>
        public string? InferenceLibrary { get; set; } = "CPU";

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
        protected virtual void InitModule()
        {
        }

        /// <summary>
        /// Processes the request receive from the server queue.
        /// </summary>
        /// <param name="request">The Request data.</param>
        /// <returns>An object to serialize back to the server.</returns>
        protected abstract ModuleResponse ProcessRequest(BackendRequest request);

        /// <summary>
        /// Returns an object containing current stats for this module
        /// </summary>
        /// <returns>An object</returns>
        protected virtual dynamic? Status()
        {
            dynamic status = new ExpandoObject();
            status.inferenceDevice      = InferenceDevice;
            status.inferenceLibrary     = InferenceLibrary;
            status.canUseGPU            = CanUseGPU;

            status.successfulInferences = _successfulInferences;
            status.failedInferences     = _failedInferences;
            status.numInferences        = _successfulInferences + _failedInferences;
            status.averageInferenceMs   = _successfulInferences > 0 
                                        ? _totalSuccessInferenceMs / _successfulInferences : 0;

            return status;
        }

        /// <summary>
        /// Called after `process` is called in order to update the stats on the number of successful
        /// and failed calls as well as average inference time.
        /// </summary>
        /// <param name="response"></param>
        protected virtual void UpdateStatistics(ModuleResponse response)
        {
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

        private async Task StatusUpdateLoop(CancellationToken token)
        {
            await Task.Yield();

            while (!token.IsCancellationRequested && !_cancelled)
            {
                await _apiClient.SendStatus(_moduleId!, Status(), token).ConfigureAwait(false);
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

                    // Special shutdown request
                    string? requestModuleId = request.payload?.GetValue("moduleId");
                    if (request.payload?.command?.EqualsIgnoreCase("quit") == true &&
                        requestModuleId?.EqualsIgnoreCase(_moduleId) == true)
                    {
                        await ShutDown(0);
                        return;
                    }

                    Stopwatch stopWatch     = Stopwatch.StartNew();
                    ModuleResponse response = ProcessRequest(request);
                    stopWatch.Stop();

                    if (!_doNotLogCommands.Contains(request.reqtype))
                        UpdateStatistics(response);

                    long processMs      = stopWatch.ElapsedMilliseconds;
                    response.ModuleName = ModuleName;
                    response.ModuleId   = _moduleId;
                    response.ProcessMs  = processMs;
                    response.Command    = request.payload?.command ?? string.Empty;
                    response.RequestId  = request.reqid;

                    HttpContent content = JsonContent.Create(response, response.GetType());

                    // Slightly faster as we don't wait for the request to complete before moving
                    // on to the next.
                    if (responseTask is not null)
                        await responseTask.ConfigureAwait(false);

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
            InitModule();

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

                tasks.Add(StatusUpdateLoop(token));

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

            await base.StopAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes of this classes resources
        /// </summary>
        public override void Dispose()
        {
            _cancellationTokenSource?.Dispose();
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
