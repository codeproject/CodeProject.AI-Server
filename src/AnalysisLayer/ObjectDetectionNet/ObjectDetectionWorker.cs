using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using Yolov5Net.Scorer;
using System.Collections.Concurrent;

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
        private const string            _defaultQueueName = "objectdetection_queue";
        private const string            _moduleName       = "Object Detection (Net)";

        private readonly ConcurrentDictionary<string, ObjectDetector> _detectors = new ();

        private readonly ILogger<ObjectDetector> _logger;
        private readonly string _mode;
        private readonly string _modelDir;
        private readonly string _customDir;

        private string _hardwareType      = "CPU";
        private string _executionProvider = string.Empty;

        private string[] _customFileList  = Array.Empty<string>();
        private DateTime _lastTimeCustomFileListGenerated = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the ObjectDetectionWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="config">The app configuration values.</param>
        public ObjectDetectionWorker(ILogger<ObjectDetector> logger, IConfiguration config)
            : base(logger, config, _moduleName, _defaultQueueName, _defaultModuleId)
        {
            _logger = logger;

            string currentPath = Directory.GetCurrentDirectory(); // AppContext.BaseDirectory;

            // HACK for debugging in VSCode
            if (System.Environment.GetEnvironmentVariable("LAUNCHED_SEPARATELY_IN_VSCODE") == "true")
                currentPath += "/src/AnalysisLayer/ObjectDetectionNet";

            _mode      = config.GetValue("MODEL_SIZE", "Medium") ?? "Medium";
            _modelDir  = config.GetValue("MODELS_DIR", Path.Combine(currentPath, "assets")) ?? "assets";
            _customDir = config.GetValue("CUSTOM_MODELs_DIR", Path.Combine(currentPath, "custom-models")) ?? "custom-models";

            if (!_modelDir.EndsWith("/") || !_modelDir.EndsWith("\\"))
                _modelDir += "/";

            if (!_customDir.EndsWith("/") || !_customDir.EndsWith("\\"))
                _customDir += "/";

            _modelDir  = Text.FixSlashes(_modelDir);
            _customDir = Text.FixSlashes(_customDir);
        }

        protected override void InitModule()
        {
            Logger.LogWarning("Please ensure you don't enable this module along side any other " +
                              "Object Detection module using the 'vision/detection' route and " +
                              "'objectdetection_queue' queue (eg. ObjectDetectionYolo). " +
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
            BackendResponseBase response;

            RequestPayload payload = request.payload;
            if (payload == null)
                return new BackendErrorResponse(-1, "No payload supplied for object detection.");
            if (string.IsNullOrEmpty(payload.command))
                return new BackendErrorResponse(-1, "No command supplied for object detection.");

            if (payload.command.EqualsIgnoreCase("list-custom") == true)               // list all models available
            {
                lock(_customFileList)
                {
                    // We'll only refresh the list of models at most once a minute
                    if (DateTime.Now - _lastTimeCustomFileListGenerated > TimeSpan.FromMinutes(1))
                    {
                        try
                        {
                            DirectoryInfo dir = new DirectoryInfo(_customDir);
                            FileInfo[] files = dir.GetFiles("*.onnx");
                            _customFileList = files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray();
                        }
                        catch (Exception e)
                        {
                            Console.Write(e);
                        }

                        _lastTimeCustomFileListGenerated = DateTime.Now;
                    }

                    response = new BackendCustomModuleListResponse
                    {
                        models = _customFileList // list all available models
                    };
                }
            }
            else if (payload.command.EqualsIgnoreCase("detect") == true)               // Perform 'standard' object detection
            {
                var file = payload.files?.FirstOrDefault();
                if (file is null)
                    return new BackendErrorResponse(-1, "No File supplied for object detection.");

                var minConfidenceValues = payload.values?
                                                 .FirstOrDefault(x => x.Key == "minConfidence")
                                                 .Value;
                if (!float.TryParse(minConfidenceValues?[0] ?? "", out float minConfidence))
                    minConfidence = 0.4f;

                string modelPath = (_mode ?? string.Empty.ToLower()) switch
                {
                    "large"  => _modelDir + "yolov5l.onnx",
                    "medium" => _modelDir + "yolov5m.onnx",
                    "small"  => _modelDir + "yolov5s.onnx",
                    "tiny"   => _modelDir + "yolov5n.onnx",
                    _        => _modelDir + "yolov5m.onnx"
                };

                response = DoDetection(modelPath, file, minConfidence);
            }
            else if (payload.command.EqualsIgnoreCase("custom") == true) // Perform custom object detection
            {
                var file = payload.files?.FirstOrDefault();
                if (file is null)
                    return new BackendErrorResponse(-1, "No File supplied for custom object detection.");

                var minConfidenceValues = payload.values?
                                                 .FirstOrDefault(x => x.Key == "minConfidence")
                                                 .Value;
                if (!float.TryParse(minConfidenceValues?[0] ?? "", out float minConfidence))
                    minConfidence = 0.4f;

                string modelName = "general";
                string modelDir  = _customDir;
                if (payload.urlSegments.Length > 0 && !string.IsNullOrEmpty(payload.urlSegments[0]))
                    modelName = payload.urlSegments[0];

                if (modelName.EqualsIgnoreCase("general"))  // Use the custom IP Cam general model
                {
                    modelName = "ipcam-general";
                    modelDir  = _customDir;
                }

                string modelPath = modelDir + modelName + ".onnx";
                response = DoDetection(modelPath, file, minConfidence);               
            }
            else
                 response = new BackendErrorResponse(-1, $"Command '{request.reqtype ?? "<None>"}' not found");

            return response;
        }

        /// <summary>
        /// Performs detection of objects in an image using the given model
        /// </summary>
        /// <param name="modelPath">The path to the model</param>
        /// <param name="file">The file object containing the image</param>
        /// <param name="minConfidenceValue">A string rep of the minimum detection confidence</param>
        /// <returns></returns>
        protected BackendResponseBase DoDetection(string modelPath, RequestFormFile file,
                                                  float minConfidence)
        {
            Logger.LogInformation($"Processing {file.filename}");

            if (!_detectors.TryGetValue(modelPath, out ObjectDetector? detector) || detector is null)
            {
                detector = new ObjectDetector(modelPath, _logger);
                _detectors.TryAdd(modelPath, detector);
            }

            if (detector is null)
                return new BackendErrorResponse(-1, $"Unable to create detector for model {modelPath}");

            _hardwareType      = detector.HardwareType;
            _executionProvider = detector.ExecutionProvider;

            Stopwatch sw = Stopwatch.StartNew();
            List<YoloPrediction>? yoloResult = detector.Predict(file.data);
            long inferenceMs = sw.ElapsedMilliseconds;

            if (yoloResult == null)
                return new BackendErrorResponse(-1, "Yolo returned null.");

            return new BackendObjectDetectionResponse
            {
                predictions = yoloResult.Where(x => x?.Rectangle != null && x.Score >= minConfidence)
                                        .Select(x =>
                                            new DetectionPrediction
                                            {
                                                confidence = x.Score,
                                                label = x?.Label?.Name,
                                                x_min = (int)x!.Rectangle.Left,
                                                y_min = (int)x!.Rectangle.Top,
                                                x_max = (int)(x!.Rectangle.Right),
                                                y_max = (int)(x!.Rectangle.Bottom)
                                            })
                                        .ToArray(),
                inferenceMs = inferenceMs
            };
        }

        protected override void GetHardwareInfo()
        {
            HardwareType      = _hardwareType;
            ExecutionProvider = _executionProvider;
        }
    }
}
