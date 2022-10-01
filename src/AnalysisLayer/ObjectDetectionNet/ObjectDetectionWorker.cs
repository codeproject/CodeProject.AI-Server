using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using CodeProject.AI.AnalysisLayer.SDK;
using Yolov5Net.Scorer;

namespace CodeProject.AI.AnalysisLayer.ObjectDetection.Yolo
{
    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.AI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET Core framework.
    /// </summary>
    public class ObjectDetectionWorker : CommandQueueWorker
    {
        private const string            _defaultModuleId  = "ObjectDetectionNet";
        private const string            _defaultQueueName = "detection_queue";
        private const string            _moduleName       = "Object Detection (Net)";

        private readonly ObjectDetector _objectDetector;

        /// <summary>
        /// Initializes a new instance of the ObjectDetectionWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="objectDetector">The Yolo Object Detector.</param>
        /// <param name="configuration">The app configuration values.</param>
        public ObjectDetectionWorker(ILogger<ObjectDetectionWorker> logger,
                                     ObjectDetector objectDetector,
                                     IConfiguration configuration)
            : base(logger, configuration, _moduleName, _defaultQueueName, _defaultModuleId)
        {
            _objectDetector = objectDetector;
        }

        protected override void InitModule()
        {
            Logger.LogWarning("Please ensure you don't enable this module along side any other " +
                              "Object Detection module using the 'vision/detection' route and " +
                              "'detection_queue' queue (eg. ObjectDetectionYolo). " +
                              "There will be conflicts");

            base.InitModule();
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public override BackendResponseBase ProcessRequest(BackendRequest request)
        {
            var file = request.payload?.files?.FirstOrDefault();
            if (file is null)
                return new BackendErrorResponse(-1, "No File supplied for object detection.");

            Logger.LogInformation($"Processing {file.filename}");

            List<YoloPrediction>? yoloResult = _objectDetector.Predict(file.data);
            if (yoloResult == null)
                return new BackendErrorResponse(-1, "Yolo returned null.");

            var minConfidenceValues = request.payload?.values?
                                                .FirstOrDefault(x => x.Key == "minConfidence")
                                                .Value;

            float.TryParse(minConfidenceValues?[0], out float minConfidence);

            var response = new BackendObjectDetectionResponse
            {
                predictions = yoloResult.Where(x => x.Score >= minConfidence)
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

            return response;
        }

        protected override void GetHardwareInfo()
        {
            HardwareType      = _objectDetector.HardwareType;
            ExecutionProvider = _objectDetector.ExecutionProvider;
        }
    }
}
