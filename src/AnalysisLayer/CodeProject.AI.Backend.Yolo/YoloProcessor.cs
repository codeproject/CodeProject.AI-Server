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

namespace CodeProject.SenseAI.Analysis.Yolo
{
    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.SenseAI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET6 framework.
    /// </summary>
    public class YoloProcessor : BackgroundService
    {
        private const string CodeProjectSenseAiUrl     = "http://localhost:5000/v1/";
        private const string QueueName                 = "ObjectDetect";
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly ILogger<YoloProcessor> _logger;

        private ObjectDetector                  _objectDetector;
#if !USE_HTTPCLIENT
        private QueueServices                   _queueServices;
#endif

        static YoloProcessor()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(CodeProjectSenseAiUrl)
            };
        }

        /// <summary>
        /// Initializes a new instance of the YoloProcessor.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="queueServices">Command-result queue services.</param>
        /// <param name="objectDetector">The Yolo Object Detector.</param>
        public YoloProcessor(ILogger<YoloProcessor> logger,
#if !USE_HTTPCLIENT
                             QueueServices queueServices, 
#endif
                             ObjectDetector objectDetector)
        {
            _logger          = logger;

#if !USE_HTTPCLIENT
            _queueServices   = queueServices;
#endif
            _objectDetector = objectDetector ?? throw new ArgumentNullException(nameof(objectDetector));
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation tokent</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            _logger.LogInformation("Background YoloDetector Task Started.");
            await Task.Delay(250).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                BackendResponseBase response;
#if USE_HTTPCLIENT
                BackendObjectDetectionRequest? request = null;
                try
                {
                    //_logger.LogInformation("Yolo attempting to pull from Queue.");
                    var httpResponse = await _httpClient.GetAsync($"queue/{QueueName}", token)
                                                        .ConfigureAwait(false);

                    if (httpResponse is not null &&
                        httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var jsonString = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        request = JsonSerializer.Deserialize<BackendObjectDetectionRequest>(jsonString,
                                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Yolo Exception");
                    continue;
                }
#else
                var request = await _queueServices.DequeueRequestAsync(QueueName, token).ConfigureAwait(false);
#endif
                if (request == null)
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    continue;
                }
                else if (request is BackendObjectDetectionRequest detectRequest)
                {
                    if (string.IsNullOrWhiteSpace(detectRequest.imgid))
                    {
                        response = new BackendErrorResponse(-1, "Object Detection Invalid filename.");
                    }
                    else
                    {
                        _logger.LogInformation($"Processing {detectRequest.imgid}");
                        List<Yolov5Net.Scorer.YoloPrediction>? yoloResult = null;
                        try
                        {
                            yoloResult = _objectDetector.Predict(detectRequest.imgid);
                        }
                        catch(Exception ex)
                        {
                            _logger.LogError(ex, "Yolo Object Detector Exception");
                            yoloResult = null;
                        }

                        if (yoloResult == null)
                        {
                            response = new BackendErrorResponse(-1, "Yolo returned null.");
                        }
                        else
                        {
                            response = new BackendObjectDetectionResponse
                            {
                                success = true,
                                predictions = yoloResult
                                              .Where(x => x.Score >= (detectRequest.minconfidence ?? 0.4f))
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
                }
                else
                {
                    response = new BackendErrorResponse(-1, "Invalid Request Type.");
                }

 #if USE_HTTPCLIENT
                HttpContent? content = null;
                if (response is BackendObjectDetectionResponse detectResponse)
                    content = JsonContent.Create(detectResponse);
                else
                    content = JsonContent.Create(response as BackendErrorResponse);

                await _httpClient.PostAsync($"queue/{request.reqid}", content).ConfigureAwait(false);
#else
                string responseString;
                if (response is BackendDetectionResponse detectResponse)
                    responseString = JsonSerializer.Serialize(detectResponse);
                else
                    responseString = JsonSerializer.Serialize(response as BackendErrorResponse);

                _queueServices.SetResult(request.reqid, responseString);
#endif
            }
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
