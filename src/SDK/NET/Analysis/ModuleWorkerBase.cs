using System.Diagnostics;
using System.Net.Http.Json;

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
        private readonly string?       _queueName;
        private readonly string?       _moduleId;
        private readonly int           _parallelism     = 1;
        private readonly string?       _accelDeviceName = null;
        private readonly string        _halfPrecision   = "enable"; // Can be enable, disable or force
        private readonly string        _logVerbosity    = "info";   // Can be Quiet, Info or Loud

        private readonly BackendClient _apiClient;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly ILogger       _logger;

        private bool _cancelled = false;


        /// <summary>
        /// Gets or sets the name of this Module
        /// </summary>
        public string? ModuleName { get; set; }

        /// <summary>
        /// Gets or sets the path to this Module
        /// </summary>
        public string? ModulePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this module supports GPU acceleration
        /// </summary>
        public bool   SupportGPU  { get; set; } = true;

        /// <summary>
        /// Gets or sets the name of the hardware acceleration execution provider
        /// </summary>
        public string? ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets a value indicating whether or not this detector can use the current GPU
        /// </summary>
        public bool CanUseGPU { get; set; } = false;

        /// <summary>
        /// Gets or sets the hardware type that's in use (CPU or GPU)
        /// </summary>
        public string? HardwareType { get; set; } = "CPU";

        /// <summary>
        /// Gets the logger instance.
        /// </summary>
        public ILogger Logger { get => _logger; }

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="configuration">The app configuration values.</param>
        public ModuleWorkerBase(ILogger logger, IConfiguration configuration)
        {
            _cancelled = false;
            _logger    = logger;

            string currentModulePath = GetModuleDirectory();
            string currentDirName = new DirectoryInfo(currentModulePath).Name;

            _moduleId  = configuration.GetValue<string?>("CPAI_MODULE_ID", null)        ?? currentDirName;
            ModuleName = configuration.GetValue<string?>("CPAI_MODULE_NAME", null)      ?? _moduleId;
            ModulePath = configuration.GetValue<string?>("CPAI_MODULE_PATH", null)      ?? currentModulePath;
            _queueName = configuration.GetValue<string?>("CPAI_MODULE_QUEUENAME", null) ?? _moduleId.ToLower() + "_queue";

            int port = configuration.GetValue<int>("CPAI_PORT", 32168);

            _parallelism = configuration.GetValue<int>("CPAI_MODULE_PARALLELISM", 0);

            // We want this big enough to increase throughput, but not so big as to
            // cause thread starvation.
            if (_parallelism == 0)
                _parallelism = Environment.ProcessorCount/2;

            SupportGPU       = configuration.GetValue<bool>("CPAI_MODULE_SUPPORT_GPU",   true);
            _accelDeviceName = configuration.GetValue<string?>("CPAI_ACCEL_DEVICE_NAME", null);
            _halfPrecision   = configuration.GetValue<string?>("CPAI_HALF_PRECISION", null) ?? "enable"; // Can be enable, disable or force
            _logVerbosity    = configuration.GetValue<string?>("CPAI_LOG_VERBOSITY", null)  ?? "info";   // Can be Quiet, Info or Loud

            var token = _cancellationTokenSource.Token;
#if DEBUG
            _apiClient = new BackendClient($"http://localhost:{port}/", TimeSpan.FromSeconds(30), token);

            /*
            _logger.LogInformation($"CPAI_PORT:               {port}");
            _logger.LogInformation($"CPAI_MODULE_ID:          {defaultModuleId}");
            _logger.LogInformation($"CPAI_MODULE_PARALLELISM: {_parallelism}");
            _logger.LogInformation($"CPAI_MODULE_SUPPORT_GPU: {_supportGPU}");
            */
#else
            _apiClient = new BackendClient($"http://localhost:{port}/", token: token);
#endif
        }

        /// <summary>
        /// Stop the process.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
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

        protected string GetModuleDirectory()
        {
            string moduleDir = AppContext.BaseDirectory;
            DirectoryInfo? info = new DirectoryInfo(moduleDir);

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

            return moduleDir;
        }

        /* No longer really needed in this form

        /// <summary>
        /// Sniff the hardware in use so we can report to the API server
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method should be implemented by each adapter, and each adapter should be asking
        /// the module that it's adapting what's actually going on with hardware, since it's the
        /// module that is responsible for choosing whether to use CPU or GPU, and what libraries
        /// it will use in doing so.
        /// </para>
        /// <para>
        /// The issue is that we need to avoid modifying the code of a given module: It's the
        /// adapter where we should be writing code. So either the adapter hardcodes the values
        /// based on what it knows of the module, or the adapter mimics whatever checks the module
        /// does, and honours whatever settings the module uses in order to report back what's
        /// happening. Or we dive in and plug into each module some hardware sniffing code.
        /// </para>
        /// <para>
        /// The detection and selection of CPU/GPU support is a very tricky and complex issue.
        /// </para>
        /// <list type="bullet">
        /// <item>
        ///  <description>
        ///   <para>
        ///    With ONNX you can actually install multiple Execution Providers and the runtime
        ///    will check which one to use based on a list of providers to check, and whether they
        ///    can be used in the current environment.  However, we haven't found a way to
        ///    determine which one is selected, especially in NET7 as the C# API is only exposing
        ///    a subset of the OnnxRuntime API.
        ///   </para>
        ///   <para>
        ///    OnnxRuntime.DirectML on Windows and WSL theoretically should handle all this, but
        ///    if fails to execute some models. 
        ///   </para>
        ///   <para>
        ///    There is a NuGet for OnnxRuntime for OpenVINO, but it is not publicly available.
        ///    We were able to use this to verify that the Execution Providers to be used could be 
        ///    selected at runtime, so a GPU=Intel|AMD|NVIDIA|M1 could be an option if we can find
        ///    or build the Execution Providers for our requirements. There are more publicly
        ///    available packages for Python and C/C++ than C#/NET.
        ///   </para>
        ///  </description>
        /// </item>
        /// <item>
        ///  <description>
        ///   <para>
        ///    With PyTorch and TensorFlow in Python, you need to install the specific flavor(s)
        ///    of PyTorch and/or TensorFlow specific to your CPU and GPU, and have the appropriate
        ///    libraries and drivers for the GPU installed as well. It won't be easy, or
        ///    necessarily even possible, or practical given download sizes, to install all the
        ///    packages at install time and then select them at runtime.
        ///    </para>
        ///   </description>
        /// </item>
        /// </list>
        /// <para>
        /// Could we do a hardware sniff at install time to resolve this issue? An option is to 
        /// install ALL hardware / library possibilities at install time and then choose what to
        /// use at runtime. Doing this would make the installation package very large.
        /// </para>
        /// <para>
        /// Depending on the execution library used, different steps will be required to determine
        /// which GPU was actually used to run inference on the model and may require changes to
        /// the Module code. It may boil down to creating specific builds for the various
        /// combinations for the Modules and our our Module Installation system/UI will select the
        /// correct version based on sniffed hardware.
        /// </para>
        /// </remarks>
        protected virtual void GetHardwareInfo()
        {
            // Super naive: It's assuming that if it finds a GPU, it's using a GPU. Not actually likely.
            GpuInfo? gpu = SystemInfo.GetGpuInfo();
            if (gpu is not null)
            {
                HardwareType      = "GPU";
                ExecutionProvider = gpu.HardwareVendor;
            }
        }
        */

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
        protected abstract BackendResponseBase ProcessRequest(BackendRequest request);

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A Task</returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            _cancelled = false;

            await Task.Delay(1_000, token).ConfigureAwait(false);

            _apiClient.LogToServer($"{ModuleName} module started.", $"{ModuleName}",
                                        LogLevel.Information, string.Empty);

            InitModule();

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < _parallelism; i++)
                tasks.Add(ProcessQueue(token, i));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ProcessQueue(CancellationToken token, int i)
        {
            Task<BackendRequest?> requestTask = _apiClient.GetRequest(_queueName!, _moduleId!,
                                                                     token, ExecutionProvider, 
                                                                     CanUseGPU);
            Task? responseTask = null;
            BackendRequest? request;

            while (!token.IsCancellationRequested && !_cancelled)
            {
                try
                {
                    //Console.Write($" -{i}");
                    request = await requestTask.ConfigureAwait(false);
                    //Console.Write($" ?{i}");
                    requestTask = _apiClient.GetRequest(_queueName!, _moduleId!, token,
                                                       ExecutionProvider, CanUseGPU);
                    if (request is null)
                        continue;
                    //Console.Write($"!{i} ");

                    // Special shutdown request
                    string? requestModuleId = request.payload?.GetValue("moduleId");
                    if (request.payload?.command?.EqualsIgnoreCase("quit") == true &&
                        requestModuleId?.EqualsIgnoreCase(_moduleId) == true)
                    {
                        await StopAsync(token).ConfigureAwait(false);
                        return;
                    }

                    Stopwatch stopWatch = Stopwatch.StartNew();
                    BackendResponseBase response = ProcessRequest(request);
                    stopWatch.Stop();

                    long processMs = stopWatch.ElapsedMilliseconds;
                    response.processMs         = processMs;
                    response.moduleId          = _moduleId;
                    response.executionProvider = ExecutionProvider ?? string.Empty;
                    response.command           = request.payload?.command ?? string.Empty;
                    response.canUseGPU         = CanUseGPU;

                    HttpContent content = JsonContent.Create(response, response.GetType());

                    // Slightly faster as we don't wait for the request to complete before moving
                    // on to the next.
                    if (responseTask is not null)
                        await responseTask.ConfigureAwait(false);

                    responseTask = _apiClient.SendResponse(request.reqid, _moduleId!, content, token);

                    _apiClient.LogToServer($"Command completed in {response.processMs} ms.",
                                                $"{ModuleName}",  LogLevel.Information, 
                                                "command timing");
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
    }
}
