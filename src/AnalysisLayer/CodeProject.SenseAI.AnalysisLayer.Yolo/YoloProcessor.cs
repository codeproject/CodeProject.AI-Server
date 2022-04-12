#define USE_HTTPCLIENT

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.SenseAI.API.Server.Backend;
using Microsoft.Extensions.Configuration;
using CodeProject.SenseAI.API.Common;
using System.Text.Json.Nodes;

namespace CodeProject.SenseAI.Analysis.Yolo
{
    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.SenseAI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET Core framework.
    /// </summary>
    public class YoloProcessor : BackgroundService
    {
        private const string                    _queueName = "detection_queue";
        private static HttpClient?              _httpClient;
        private int                             _parallelism = 4; // 4 also seems to be good on my machine.
        private readonly ILogger<YoloProcessor> _logger;
        private readonly ObjectDetector         _objectDetector;

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

            _httpClient ??= new HttpClient { 
                BaseAddress = new Uri($"http://localhost:{port}/")
#if DEBUG
                ,Timeout = TimeSpan.FromMinutes(1)
#endif
            };
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);

            _logger.LogInformation("Background YoloDetector Task Started.");
            await LogToServer("SenseAI Object Detection module started.", token);

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
                    //_logger.LogInformation("Yolo attempting to pull from Queue.");
                    var httpResponse = await _httpClient!.GetAsync($"v1/queue/{_queueName}", token)
                                                        .ConfigureAwait(false);

                    if (httpResponse is not null &&
                        httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsonString = await httpResponse.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                        request = JsonSerializer.Deserialize<BackendRequest>(jsonString,
                                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    }
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
                    await LogToServer("Object Detection Null or File.", token);
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
                            await LogToServer($"Object Detection Error for {file.filename}.", token);
                        _logger.LogError(ex, "Yolo Object Detector Exception");
                        yoloResult = null;
                    }

                    if (yoloResult == null)
                    {
                        response = new BackendErrorResponse(-1, "Yolo returned null.");
                    }
                    else
                    {
                        var minConfidenceValues = request.payload?.values?.FirstOrDefault(x => x.Key == "minConfidence")
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

                await _httpClient.PostAsync($"v1/queue/{request.reqid}", content, token).ConfigureAwait(false);
            }
        }

        private async Task LogToServer(string message, CancellationToken token)
        {
            var form = new FormUrlEncodedContent(new[]
                { new KeyValuePair<string?, string?>("entry", message)}
            );
            var response = await _httpClient!.PostAsync($"v1/log", form, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Stop the process.  Does nothing.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation("QBackground YoloDetector Task is stopping.");

            await base.StopAsync(token);
        }
    }
}
