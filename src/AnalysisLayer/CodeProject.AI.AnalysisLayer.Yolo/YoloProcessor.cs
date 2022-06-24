using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

using CodeProject.AI.AnalysisLayer.SDK;

namespace CodeProject.AI.Analysis.Yolo
{
    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.AI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET Core framework.
    /// TODO: Derive this from CommandQueueWorker
    /// </summary>
    public class YoloProcessor : BackgroundService
    {
        private const string                    DefaultModuleID = "ObjectDetection";
        private string                          _queueName = "detection_queue";
        private string                          _moduleId  = DefaultModuleID;

        private int                             _parallelism = Environment.ProcessorCount * 2; 
        private readonly ILogger<YoloProcessor> _logger;
        private readonly ObjectDetector         _objectDetector;
        private readonly BackendClient          _codeprojectAI;

        // TODO: These two should be in the SDK, not the Module code.

        /// <summary>
        /// Gets or sets the name of the hardware acceleration execution provider
        /// </summary>
        public string? ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the hardware accelerator ID that's in use
        /// </summary>
        public string? HardwareId { get; set; } = "CPU";

        /// <summary>
        /// Initializes a new instance of the YoloProcessor.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="objectDetector">The Yolo Object Detector.</param>
        /// <param name="configuration">The app configuration values.</param>
        public YoloProcessor(ILogger<YoloProcessor> logger,
                             ObjectDetector objectDetector,
                             IConfiguration configuration)
        {
            _logger = logger;

            _objectDetector = objectDetector ?? throw new ArgumentNullException(nameof(objectDetector));
            int port = configuration.GetValue<int>("PORT");
            if (port == default)
                port = 5000;

            _queueName = configuration.GetValue<string>("MODULE_QUEUE");
            if (_queueName == default)
                _queueName = "detection_queue";

            _moduleId = configuration.GetValue<string>("MODULE_ID");
            if (_moduleId == default)
                _moduleId = DefaultModuleID;

            _codeprojectAI = new BackendClient($"http://localhost:{port}/"
#if DEBUG
                ,TimeSpan.FromMinutes(1)
#endif
            );
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

            _logger.LogInformation("Background YoloDetector Task Started.");
            await _codeprojectAI.LogToServer("CodeProject.AI Object Detection module started.", token);

            List<Task> tasks = new List<Task>();
            for (int i= 0; i < _parallelism; i++)
                tasks.Add(ProcessQueue(token));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ProcessQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                BackendResponseBase response;
                BackendRequest? request = null;
                try
                {
                    request = await _codeprojectAI.GetRequest(_queueName, _moduleId, token,
                                                              ExecutionProvider);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Yolo Exception");
                    continue;
                }

                if (request is null)
                    continue;

                var file = request.payload?.files?.FirstOrDefault();
                    
                if (file is null)
                {
                    await _codeprojectAI.LogToServer("Object Detection Null or File.", token);
                    response = new BackendErrorResponse(-1, "Object Detection Invalid File.");
                }
                else
                {
                    _logger.LogInformation($"Processing {file.filename}");

                    List<Yolov5Net.Scorer.YoloPrediction>? yoloResult = null;
                    try
                    {
                        var imageData = file.data;

                        yoloResult = _objectDetector.Predict(imageData);
                    }
                    catch (Exception ex)
                    {
                        await _codeprojectAI.LogToServer($"Object Detection Error for {file.filename}.", token);
                        _logger.LogError(ex, "Yolo Object Detector Exception");
                        yoloResult = null;
                    }

                    if (yoloResult == null)
                    {
                        response = new BackendErrorResponse(-1, "Yolo returned null.");
                    }
                    else
                    {
                        var minConfidenceValues = request.payload?.values?
                                                         .FirstOrDefault(x => x.Key == "minConfidence")
                                                         .Value;

                        float.TryParse(minConfidenceValues?[0], out float minConfidence);

                        response = new BackendObjectDetectionResponse
                        {
                            predictions = yoloResult
                                            .Where(x => x.Score >= minConfidence)
                                            .Select(x =>
                                            new DetectionPrediction
                                            {
                                                confidence = x.Score,
                                                label = x.Label.Name,
                                                x_min = (int)x.Rectangle.X,
                                                y_min = (int)x.Rectangle.Y,
                                                x_max = (int)(x.Rectangle.X + x.Rectangle.Width),
                                                y_max = (int)(x.Rectangle.Y + x.Rectangle.Height)
                                            })
                                        .ToArray()
                        };
                    }
                }

                HttpContent? content = null;
                if (response is BackendObjectDetectionResponse detectResponse)
                    content = JsonContent.Create(detectResponse);
                else
                    content = JsonContent.Create(response as BackendErrorResponse);

                await _codeprojectAI.SendResponse(request.reqid, _moduleId, content, token, 
                                                  executionProvider: ExecutionProvider);
            }
        }

        /// <summary>
        /// Stop the process.  Does nothing.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation("Background YoloDetector Task is stopping.");

            await base.StopAsync(token);
        }

        protected /*override */ void GetHardwareInfo()
        {
            HardwareId        = _objectDetector.HardwareId;
            ExecutionProvider = _objectDetector.ExecutionProvider;
        }
    }
}
