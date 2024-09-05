using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;

using Yolov5Net.Scorer;
using System.Dynamic;

#pragma warning disable CS0162 // unreachable code

namespace CodeProject.AI.Modules.ObjectDetection.YOLOv5
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
        
        private long _numItemsFound;
        private Dictionary<string, long> _histogram = new Dictionary<string, long>();
        private string[] _customFileList  = Array.Empty<string>();
        private DateTime _lastTimeCustomFileListGenerated = DateTime.MinValue;

        /// <summary>
        /// Initializes a new instance of the ObjectDetectionWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="config">The app configuration values.</param>
        /// <param name="hostApplicationLifetime">The applicationLifetime object</param>
        public ObjectDetectionWorker(ILogger<ObjectDetector> logger, IConfiguration config,
                                     IHostApplicationLifetime hostApplicationLifetime)
            : base(logger, config, hostApplicationLifetime)
        {
            _logger = logger;

            _mode      = config.GetValue("MODEL_SIZE", "Medium") ?? "Medium";
            _modelDir  = config.GetValue("MODELS_DIR", Path.Combine(moduleDirPath!, "assets")) ?? "assets";
            _customDir = config.GetValue("CUSTOM_MODELS_DIR", Path.Combine(moduleDirPath!, "custom-models")) ?? "custom-models";

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

        protected override void Initialize()
        {
            // Logger.LogWarning("Please ensure you don't enable this module along side any other " +
            //                  "Object Detection module using the 'vision/detection' route and " +
            //                  "'objectdetection_queue' queue (eg. ObjectDetectionYOLOv5-6.2). " +
            //                  "There will be conflicts");
#if CPU
            Console.WriteLine("ObjectDetection (.NET) built for CPU");
#elif CUDA
            Console.WriteLine("ObjectDetection (.NET) built for CUDA");
#elif OpenVINO
            Console.WriteLine("ObjectDetection (.NET) built for OpenVINO");
#elif DirectML
            Console.WriteLine("ObjectDetection (.NET) built for DirectML");
#endif
            base.Initialize();
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        protected override ModuleResponse Process(BackendRequest request)
        {
            ModuleResponse response;

            RequestPayload payload = request.payload;
            if (payload == null)
                return new ModuleErrorResponse("No payload supplied for object detection.");
            if (string.IsNullOrEmpty(payload.command))
                return new ModuleErrorResponse("No command supplied for object detection.");

            if (payload.command.EqualsIgnoreCase("list-custom") == true)               // list all models available
            {
                lock(_customFileList)
                {
                    // We'll only refresh the list of models at most once a minute
                    if (DateTime.Now - _lastTimeCustomFileListGenerated > TimeSpan.FromMinutes(1))
                    {
                        _lastTimeCustomFileListGenerated = DateTime.Now;

                        try
                        {
                            DirectoryInfo dir = new DirectoryInfo(_customDir);
                            FileInfo[] files = dir.GetFiles("*.onnx");
                            _customFileList = files.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray();
                        }
                        catch (Exception e)
                        {
                            // Console.Write(e);
                            response = new ModuleErrorResponse($"Unable to list custom models: " + e.Message);
                        }
                    }

                    response = new CustomModuleListResponse
                    {
                        Models = _customFileList // list all available models
                    };
                }
            }
            else if (payload.command.EqualsIgnoreCase("detect") == true)               // Perform 'standard' object detection
            {
                var file = payload.files?.FirstOrDefault();
                if (file is null)
                    return new ModuleErrorResponse("No File supplied for object detection.");

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
                    return new ModuleErrorResponse("No File supplied for custom object detection.");

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
            {
                response = new ModuleErrorResponse($"Command '{request.reqtype ?? "<None>"}' not found");
            }

            return response;
        }

        /// <summary>
        /// Returns an object containing current stats for this module
        /// </summary>
        /// <returns>An object</returns>
        protected override ExpandoObject? ModuleStatus()
        {
            var status = base.ModuleStatus();
            if (status is null)
                status = new ExpandoObject();

            return status.Merge(new {
                Histogram     = _histogram,
                NumItemsFound = _numItemsFound
            }.ToExpando());
        }            

        /// <summary>
        /// Called after `process` is called in order to update the stats on the number of successful
        /// and failed calls as well as average inference time.
        /// </summary>
        /// <param name="response"></param>
        protected override void UpdateStatistics(ModuleResponse? response)
        {
            base.UpdateStatistics(response);
            if (response is null)
                return;

            if (response.Success && response is ObjectDetectionResponse detectResponse &&
                detectResponse.Predictions is not null)
            {
                _numItemsFound += detectResponse.Count;
                foreach (var prediction in detectResponse.Predictions)
                {
                    string label = prediction.Label ?? "unknown";
                    if (_histogram.ContainsKey(label))
                        _histogram[label] = _histogram[label] + 1;
                    else
                        _histogram[label] = 1;
                }
            }
        }

        /// <summary>
        /// Called when the module is asked to execute a self-test to ensure it install and runs
        /// correctly
        /// </summary>
        /// <returns>True if the tests were successful; false otherwise.</returns>
        protected override int SelfTest()
        {
            RequestPayload payload = new RequestPayload("detect");
            payload.SetValue("minconfidence", "0.4");
            payload.AddFile(Path.Combine(moduleDirPath!, "test/home-office.jpg"));

            var request = new BackendRequest(payload);
            ModuleResponse response = Process(request);

            if (response.Success)
                return 0;

            return 1;
        }
        
        private string GetStandardModelPath()
        {
            return (_mode ?? string.Empty.ToLower()) switch
            {
                "large"  => _modelDir + "yolov5l.onnx",
                "medium" => _modelDir + "yolov5m.onnx",
                "small"  => _modelDir + "yolov5s.onnx",
                "tiny"   => _modelDir + "yolov5n.onnx",
                _        => _modelDir + "yolov5m.onnx"
            };
        }

        /// <summary>
        /// Performs detection of objects in an image using the given model
        /// </summary>
        /// <param name="modelPath">The path to the model</param>
        /// <param name="file">The file object containing the image</param>
        /// <param name="minConfidenceValue">A string rep of the minimum detection confidence</param>
        /// <returns></returns>
        protected ModuleResponse DoDetection(string modelPath, RequestFormFile file,
                                             float minConfidence)
        {
            // Logger.LogTrace($"Processing {file.filename}");

            Stopwatch traceSW = Stopwatch.StartNew();
            if (ShowTrace)
                Console.WriteLine($"Trace: Start DoDetection: {traceSW.ElapsedMilliseconds}ms");

            ObjectDetector? detector = GetDetector(modelPath);

            if (ShowTrace)
                Console.WriteLine($"Trace: Creating Detector: {traceSW.ElapsedMilliseconds}ms");

            if (detector is null)
                return new ModuleErrorResponse($"Unable to create detector for model {modelPath}");

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
                return new ModuleErrorResponse("Yolo returned null.");

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

            return new ObjectDetectionResponse
            {
                Count           = count,
                Message         = message,
                Predictions     = results.Select(x =>
                                    new DetectedObject
                                    {
                                        Confidence = x.Score,
                                        Label      = x?.Label?.Name,
                                        X_min      = (int)x!.Rectangle.Left,
                                        Y_min      = (int)x!.Rectangle.Top,
                                        X_max      = (int)(x!.Rectangle.Right),
                                        Y_max      = (int)(x!.Rectangle.Bottom)
                                    }).ToArray(),
                InferenceMs     = inferenceMs,
                InferenceDevice = InferenceDevice
            };
        }

        private void UpdateGpuInfo(ObjectDetector detector)
        {
            InferenceDevice  = detector.InferenceDevice;
            InferenceLibrary = detector.InferenceLibrary;
            CanUseGPU        = detector.CanUseGPU;
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