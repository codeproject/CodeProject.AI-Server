using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;

using SkiaSharp;
using System.Threading;

namespace CodeProject.AI.Modules.DotNetLongProcess
{
    /// <summary>
    /// A Response from our Long Process
    /// </summary>
    public class LongProcessModuleResponse : ModuleResponse
    {
        /// <summary>
        /// Gets or sets the message, if any, associated with the response.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Predictions from the long process
        /// </summary>
        public object[]? Predictions { get; set; }
    }

    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.AI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET Core framework.
    /// </summary>
    public class DotNetLongProcessWorker : ModuleWorkerBase
    {
        private const int _maxSteps = 10;

        private readonly ILogger<DotNetLongProcessWorker> _logger;
        private readonly string _modelSize;
        private readonly string _modelDir;

        private string _result    = string.Empty;     // our cumulative result
        private int    _step;

        /// <summary>
        /// Initializes a new instance of the DotNetLongProcessWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="config">The app configuration values.</param>
        /// <param name="hostApplicationLifetime">The applicationLifetime object</param>
        public DotNetLongProcessWorker(ILogger<DotNetLongProcessWorker> logger,
                                       IConfiguration config,
                                       IHostApplicationLifetime hostApplicationLifetime)
            : base(logger, config, hostApplicationLifetime)
        {
            _logger = logger;

            // Get some values from environment variables
            _modelSize = config.GetValue("MODEL_SIZE", "Medium") ?? "Medium";
            _modelDir  = config.GetValue("MODELS_DIR", Path.Combine(moduleDirPath!, "assets")) ?? "assets";
        }

        protected override void Initialize()
        {
            // ... typically we'd do something like load a model and initialise a predictor
            // _predictor = new ...

            // Report back on what hardware we're using
            InferenceDevice  = "CPU";
            InferenceLibrary = string.Empty;
            CanUseGPU        = false;

            base.Initialize();
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        protected override ModuleResponse Process(BackendRequest request)
        {
            // This is a long process module, so all we need to do here is return the long process
            // method that will be run
            var response = new ModuleResponse();
            response.LongProcessMethod = LongProcess;

            return response;
        }

        /// <summary>
        /// Performs the long running process
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The result of the process</returns>
        protected async Task<ModuleResponse> LongProcess(BackendRequest request,
                                                         CancellationToken cancellationToken)
        {
            // Was cancellation already requested?
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("Task was cancelled before it got started.");
                cancellationToken.ThrowIfCancellationRequested();
            }

            RequestPayload payload = request.payload;
            if (payload.command.EqualsIgnoreCase("command") == true)               // Perform 'command'
            {
                // An example of getting some input data for this long process
                var prompt         = payload.GetValue("prompt", null);
                var maxTokensVal   = payload.GetValue("max_tokens", "0");
                var temperatureVal = payload.GetValue("temperature", "0.4");
                int.TryParse(maxTokensVal, out int maxTokens);
                float.TryParse(temperatureVal, out float temperature);

                _step = 0;

                Stopwatch sw = Stopwatch.StartNew();

                // Typically we'd do something long here. It's a long process so maybe we need to
                // provide a callback that will send us regular updates. Inside the callback you may do
                // stuff like accumulate the results (eg a chat), or return interim results (eg image 
                // generation)
                // var predictions = _predictor.Predict(..., callback);

                // Instead we'll fake it for demonstration purposes
                for (int i = 0; i < _maxSteps; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine("Task cancelled");
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    await Task.Delay(1000).ConfigureAwait(false);
                    _step++;
                }

                long inferenceMs = sw.ElapsedMilliseconds;

                return new LongProcessModuleResponse()
                {
                    Success         = true,
                    Message         = $"The long process has completed in {_maxSteps} steps",
                    Predictions     = Array.Empty<object>(), // predictions
                    InferenceMs     = inferenceMs,
                    InferenceDevice = InferenceDevice
                };
            }
            else
            {
                return new LongProcessModuleResponse()
                {
                    Success = false,
                    Message = $"Command '{request.reqtype ?? "<None>"}' not found"
                };
            }
        }

        /// <summary>
        /// Returns an object containing current stats for this module
        /// </summary>
        /// <returns>An object</returns>
        protected override ExpandoObject? CommandStatus()
        {
            ExpandoObject? status = base.CommandStatus();
            if (status is null)
                status = new ExpandoObject();

            // Report back interim results (the step number)
            return status.Merge(new {
                Step    = _step,
                Message = $"Step {_step} of {_maxSteps}"
                // add whatever other properties (eg statistics) you wish to return here
            }.ToExpando());
        }            

        /// <summary>
        /// Called after `process` is called in order to update the stats on the number of successful
        /// and failed calls as well as average inference time.
        /// </summary>
        /// <param name="response"></param>
        protected override void UpdateStatistics(ModuleResponse? response)
        {
            if (response is null)
                return;

            base.UpdateStatistics(response);

            if (response.Success && response is LongProcessModuleResponse longProcessResponse &&
                longProcessResponse.Predictions is not null)
            {
                // We could add some statistics here, and then return them from the CommandStatus
                // method
            }
        }

        /// <summary>
        /// Called when the module is asked to execute a self-test to ensure it install and runs
        /// correctly
        /// </summary>
        /// <returns>True if the tests were successful; false otherwise.</returns>
        protected override int SelfTest()
        {
            // Setup the request and add some test data
            RequestPayload payload = new RequestPayload("command");
            payload.SetValue("minconfidence", "0.4");
            payload.AddFile(Path.Combine(moduleDirPath!, "test/home-office.jpg"));

            var request = new BackendRequest(payload);

            Initialize();
            ModuleResponse response = Process(request);
            Cleanup();

            if (response.Success)
                return 0;

            return 1;
        }
        
        protected override void Cleanup()
        {
            /*
            if (_predictor is not null)
            {
                _predictor.Dispose();
                _predictor = null;
            }
            */
        }

        /// <summary>
        /// Disposes of this classes resources
        /// </summary>
        public override void Dispose()
        {
            Cleanup();

            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}