using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;

using Yolov5Net.Scorer;
using CodeProject.AI.SDK.Utils;

#pragma warning disable CS0162 // unreachable code

namespace CodeProject.AI.Modules.ObjectDetection.Yolo
{
    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.AI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET Core framework.
    /// </summary>
    public class ObjectDetectionWorker : ModuleWorkerBase
    {
        private const bool ShowTrace = false;

        private readonly ConcurrentDictionary<string, ObjectDetector> _detectors = new ();

        private readonly ILogger<ObjectDetector> _logger;
        private readonly string _mode;
        private readonly string _modelDir;
        private readonly string _customDir;

        private string[] _customFileList  = Array.Empty<string>();
        private DateTime _lastTimeCustomFileListGenerated = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the ObjectDetectionWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="config">The app configuration values.</param>
        public ObjectDetectionWorker(ILogger<ObjectDetector> logger, IConfiguration config)
            : base(logger, config)
        {
            _logger = logger;

            _mode      = config.GetValue("MODEL_SIZE", "Medium") ?? "Medium";
            _modelDir  = config.GetValue("MODELS_DIR", Path.Combine(ModulePath!, "assets")) ?? "assets";
            _customDir = config.GetValue("CUSTOM_MODELS_DIR", Path.Combine(ModulePath!, "custom-models")) ?? "custom-models";

            if (!_modelDir.EndsWith("/") || !_modelDir.EndsWith("\\"))
                _modelDir += "/";

            if (!_customDir.EndsWith("/") || !_customDir.EndsWith("\\"))
                _customDir += "/";

            _modelDir  = Text.FixSlashes(_modelDir);
            _customDir = Text.FixSlashes(_customDir);

            // Determine the GPU Hardware/ExecutionProvider
            var modelPath = GetStandardModelPath();
            var detector = GetDetector(modelPath, addToCache: false);
            UpdateGpuInfo(detector);
        }

        protected override void InitModule()
        {
            // Logger.LogWarning("Please ensure you don't enable this module along side any other " +
            //                  "Object Detection module using the 'vision/detection' route and " +
            //                  "'objectdetection_queue' queue (eg. ObjectDetectionYolo). " +
            //                  "There will be conflicts");
#if CPU
            Logger.LogInformation("ObjectDetection (.NET) built for CPU");
#elif CUDA
            Logger.LogInformation("ObjectDetection (.NET) built for CUDA");
#elif OpenVINO
            Logger.LogInformation("ObjectDetection (.NET) built for OpenVINO");
#elif DirectML
            Logger.LogInformation("ObjectDetection (.NET) built for DirectML");
#endif
            base.InitModule();
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        protected override BackendResponseBase ProcessRequest(BackendRequest request)
        {
            BackendResponseBase response;

            RequestPayload payload = request.payload;
            if (payload == null)
                return new BackendErrorResponse("No payload supplied for object detection.");
            if (string.IsNullOrEmpty(payload.command))
                return new BackendErrorResponse("No command supplied for object detection.");

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
                    return new BackendErrorResponse("No File supplied for object detection.");

                var minConfidenceValue = payload.GetValue("minConfidence", "0.4");
                if (!float.TryParse(minConfidenceValue, out float minConfidence))
                    minConfidence = 0.4f;

                string modelPath = GetStandardModelPath();

                response = DoDetection(modelPath, file, minConfidence);
            }
            else if (payload.command.EqualsIgnoreCase("custom") == true) // Perform custom object detection
            {
                var file = payload.files?.FirstOrDefault();
                if (file is null)
                    return new BackendErrorResponse("No File supplied for custom object detection.");

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
                 response = new BackendErrorResponse($"Command '{request.reqtype ?? "<None>"}' not found");

            return response;
        }

        private string GetStandardModelPath()
        {
            return (_mode ?? string.Empty.ToLower()) switch
            {
                "large" => _modelDir + "yolov5l.onnx",
                "medium" => _modelDir + "yolov5m.onnx",
                "small" => _modelDir + "yolov5s.onnx",
                "tiny" => _modelDir + "yolov5n.onnx",
                _ => _modelDir + "yolov5m.onnx"
            };
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

            Stopwatch traceSW = Stopwatch.StartNew();
            if (ShowTrace)
                Console.WriteLine($"Trace: Start DoDetection: {traceSW.ElapsedMilliseconds}ms");

            ObjectDetector? detector = GetDetector(modelPath);

            if (ShowTrace)
                Console.WriteLine($"Trace: Creating Detector: {traceSW.ElapsedMilliseconds}ms");

            if (detector is null)
                return new BackendErrorResponse($"Unable to create detector for model {modelPath}");

            if (ShowTrace)
                Console.WriteLine($"Trace: Setting hardware type: {traceSW.ElapsedMilliseconds}ms");

            UpdateGpuInfo(detector);

            if (ShowTrace)
                Console.WriteLine($"Trace: Start Predict: {traceSW.ElapsedMilliseconds}ms");

            Stopwatch sw = Stopwatch.StartNew();
            List<YoloPrediction>? yoloResult = detector.Predict(file.data, minConfidence);
            long inferenceMs = sw.ElapsedMilliseconds;

            if (ShowTrace)
                Console.WriteLine($"Trace: End Predict: {traceSW.ElapsedMilliseconds}ms");

            if (yoloResult == null)
                return new BackendErrorResponse("Yolo returned null.");

            if (ShowTrace)
                Console.WriteLine($"Trace: Start Processing results: {traceSW.ElapsedMilliseconds}ms");

            var results = yoloResult.Where(x => x?.Rectangle != null && x.Score >= minConfidence);
            int count = results.Count();
            string message = string.Empty;
            if (count > 3)
                message = "Found " + string.Join(", ", results.Take(3).Select(x => x?.Label?.Name ?? "item")) + "...";
            else if (count > 0)
                message = "Found " + string.Join(", ", results.Select(x => x?.Label?.Name ?? "item"));
            else
                message = "No objects found";

            if (ShowTrace)
                Console.WriteLine($"Trace: Sending results: {traceSW.ElapsedMilliseconds}ms");

            return new BackendObjectDetectionResponse
            {
                count = count,
                message = message,
                predictions = results.Select(x =>
                                new DetectionPrediction
                                {
                                    confidence = x.Score,
                                    label = x?.Label?.Name,
                                    x_min = (int)x!.Rectangle.Left,
                                    y_min = (int)x!.Rectangle.Top,
                                    x_max = (int)(x!.Rectangle.Right),
                                    y_max = (int)(x!.Rectangle.Bottom)
                                }).ToArray(),
                inferenceMs = inferenceMs
            };
        }

        private void UpdateGpuInfo(ObjectDetector detector)
        {
            HardwareType = detector.HardwareType;
            ExecutionProvider = detector.ExecutionProvider;
            CanUseGPU = detector.CanUseGPU;
        }

        private ObjectDetector GetDetector(string modelPath, bool addToCache = true)
        {
            if (!_detectors.TryGetValue(modelPath, out ObjectDetector? detector) || detector is null)
            {

                detector = new ObjectDetector(modelPath, _logger);
                if (addToCache)
                    _detectors.TryAdd(modelPath, detector);
            }

            return detector;
        }
    }
}
#pragma warning restore CS0162 // unreachable code