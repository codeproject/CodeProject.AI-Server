using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Diagnostics;
using System.Net.Http.Json;

namespace CodeProject.AI.AnalysisLayer.SDK
{
    public abstract class CommandQueueWorker : BackgroundService
    {
        private readonly string?       _queueName;
        private readonly string?       _moduleId;

        private readonly int           _parallelism = 1;

        private readonly ILogger       _logger;
        private readonly BackendClient _codeprojectAI;

        /// <summary>
        /// Gets or sets the name of this Module
        /// </summary>
        public string? ModuleName { get; set; }

        /// <summary>
        /// Gets or sets the name of the hardware acceleration execution provider
        /// </summary>
        public string? ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the hardware accelerator ID that's in use
        /// </summary>
        public string? HardwareId { get; set; } = "CPU";

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="configuration">The app configuration values.</param>
        /// <param name="defaultQueueName">The default Queue Name.</param>
        /// <param name="defaultModuleId">The default Module Id.</param>
        public CommandQueueWorker(ILogger logger, IConfiguration configuration,
                                  string moduleName,
                                  string defaultQueueName, string defaultModuleId)
        {
            _logger    = logger;
            ModuleName = moduleName;
            
            int port = configuration.GetValue<int>("CPAI_PORT");
            if (port == default)
                port = 5000;

            _queueName = configuration.GetValue<string>("CPAI_MODULE_QUEUE") ?? defaultQueueName;
            if (string.IsNullOrEmpty(_queueName))
                throw new ArgumentException("QueueName not initialized");

            _moduleId  = configuration.GetValue<string>("CPAI_MODULE_ID") ?? defaultModuleId;
            if (string.IsNullOrEmpty(_moduleId))
                throw new ArgumentException("ModuleId not initialized");

            _parallelism = configuration.GetValue<int>("CPAI_MODULE_PROCESSCOUNT", 1);

            _codeprojectAI = new BackendClient($"http://localhost:{port}/"
#if DEBUG
                , TimeSpan.FromMinutes(1)
#endif
            );
        }

        /// <summary>
        /// Sniff the hardware in use so we can report to the API server
        /// </summary>
        /// <remarks>
        /// This method should be implemented by each adapter, and each adapter should be asking
        /// the module that it's adapting what's actually going on with hardware, since it's the
        /// module that is responsible for choosing whether to use CPU or GPU, and what libraries
        /// it will use in doing so.
        ///
        /// The issue is that we need to avoid modifying the code of a given module: It's the
        /// adapter where we should be writing code. So either the adapter hardcodes the values
        /// based on what it knows of the module, or the adapter mimics whatever checks the module
        /// does, and honours whatever settings the module uses in order to report back what's
        /// happening. Or we dive in and plug into each module some hardware sniffing code.
        /// 
        /// The detection and selection of CPU/GPU suppport is a very tricky and complex issue.
        ///   - With ONNX you can actually install multiple Execution Providers and the runtime
        ///     will check which one to use based on a list of providers to check, and whether they
        ///     can be used in the current environment.  However, we haven't found a way to
        ///     determine which one is selected, especially in NET6 as the C# api is only exposing
        ///     a subset of the OnnxRuntime API.
        ///     OnnxRuntime.DirectML on Windows and WSL theorectically should handle all this, but
        ///     if fails to execute some models. 
        ///     There is a NuGet for OnnxRuntime for OpenVINO, but it is not publically available.
        ///     We were able to use this to verify that the Execution Providers to be used could be 
        ///     selected at runtime, so a GPU=Intel|AMD|NVIDIA|M1 could be an option if we can find
        ///     or build the Execution Providers for our requirements. There are more publically
        ///     available for Python and C/C++ than C#/NET.
        ///   - With PyTorch and TensorFlow in Python, you need to install the specific flavor(s)
        ///     of PyTorch and/or TensorFlow specific to your CPU and GPU, and have the appropriate
        ///     libraries and drivers for the GPU installed as well. It won't be easy, or
        ///     necessarily even possible, to install all the packages at install time and then
        ///     select them at runtime.
        ///
        /// Could we do a hardware sniff at install time to resolve this issue? An option is to 
        /// install ALL hardware / library possibilities at install time and then choose what to
        /// use at runtime. Doing this would make the installation package very large.
        ///
        /// Depending on the execution library used, different steps will be required to determine
        /// which GPU was actually used to run inference on the model and may require changes to
        /// the Module code. It may boil down to creating specific builds for the various
        /// combinations for the Modules and our our Module Installation system/UI will select the
        /// correct version based on sniffed hardware.
        /// </remarks>
        protected virtual void GetHardwareInfo()
        {
            var sniffer = new Hardware();
            sniffer.SniffHardwareInfo();
            ExecutionProvider = sniffer.ExecutionProvider;

            if (!string.IsNullOrEmpty(ExecutionProvider) &&
                !ExecutionProvider.Equals("CPU", StringComparison.OrdinalIgnoreCase))
            {
                HardwareId = "GPU";
            }
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            GetHardwareInfo();

            await Task.Delay(1_000, token).ConfigureAwait(false);

            _logger.LogInformation($"{ModuleName} Task Started.");
            await _codeprojectAI.LogToServer($"{ModuleName} module started.", $"{ModuleName}",
                                             LogLevel.Information, string.Empty, token);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < _parallelism; i++)
                tasks.Add(ProcessQueue(token));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ProcessQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // _logger.LogInformation("Checking Portrait Filter queue.");

                BackendRequest? request = null;
                try
                {
                    request = await _codeprojectAI.GetRequest(_queueName!, _moduleId!, token,
                                                              ExecutionProvider);

                    if (request is null)
                        continue;

                    Stopwatch stopWatch = Stopwatch.StartNew();
                    var response = ProcessRequest(request);
                    stopWatch.Stop();

                    HttpContent content = JsonContent.Create(response, response.GetType());
                    await _codeprojectAI.SendResponse(request.reqid, _moduleId!, content, token,
                                                      ExecutionProvider);

                    await _codeprojectAI.LogToServer($"Command completed in {stopWatch.ElapsedMilliseconds} ms.",
                                                     $"{ModuleName}", LogLevel.Information,
                                                     "command timing", token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{ModuleName} Exception");
                    continue;
                }
            }
        }

        /// <summary>
        /// Processes the request receive from the server queue.
        /// </summary>
        /// <param name="request">The Request data.</param>
        /// <returns>An object to serialize back to the server.</returns>
        public abstract BackendResponseBase ProcessRequest(BackendRequest request);

        /// <summary>
        /// Stop the process. Does nothing.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _logger.LogTrace($"{ModuleName} Task is stopping.");

            await base.StopAsync(token);
        }
    }
}
