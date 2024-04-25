using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using Yolov8Net;

namespace CodeProject.AI.Modules.DotNetSimple
{
    /// <summary>
    /// An Object Detection Response.
    /// </summary>
    public class ObjectDetectionResponse : ModuleResponse
    {
        public string? Message { get; set; }
        public int Count { get; set; }
        public DetectedObject[]? Predictions { get; set; }
    }

    /// <summary>
    /// The is a .NET implementation of an Analysis module for Object Detection. This module will
    /// interact with the CodeProject.AI API Server backend, in that it will grab, process,
    /// and send back analysis requests and responses from and to the server backend.
    /// While intended for development and tests, this also demonstrates how a backend service can
    /// be created with the .NET Core framework.
    /// </summary>
    public class DotNetSimpleWorker : ModuleWorkerBase
    {
        private readonly ILogger<DotNetSimpleWorker> _logger;
        private readonly string _modelSize;
        private readonly string _modelDir;

#pragma warning disable CA2213 // Disposable fields should be disposed
        private IPredictor? _predictor = null;
#pragma warning restore CA2213 // Disposable fields should be disposed

        private long _numItemsFound;
        private Dictionary<string, long> _histogram = new Dictionary<string, long>();

        /// <summary>
        /// Initializes a new instance of the DotNetSimpleWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="config">The app configuration values.</param>
        /// <param name="hostApplicationLifetime">The applicationLifetime object</param>
        public DotNetSimpleWorker(ILogger<DotNetSimpleWorker> logger,
                                  IConfiguration config,
                                  IHostApplicationLifetime hostApplicationLifetime)
            : base(logger, config, hostApplicationLifetime)
        {
            _logger = logger;

            _modelSize = config.GetValue("MODEL_SIZE", "Medium") ?? "Medium";
            _modelDir  = config.GetValue("MODELS_DIR", Path.Combine(moduleDirPath!, "assets")) ?? "assets";

            if (!_modelDir.EndsWith("/") || !_modelDir.EndsWith("\\"))
                _modelDir += "/";

            _modelDir = Text.FixSlashes(_modelDir);
        }

        protected override void Initialize()
        {
            var modelPath = GetStandardModelPath();
            _predictor = YoloV8Predictor.Create(modelPath);

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
            ModuleResponse response;

            RequestPayload payload = request.payload;
            if (payload == null)
                return new ModuleErrorResponse("No payload supplied for object detection.");

            if (string.IsNullOrEmpty(payload.command))
                return new ModuleErrorResponse("No command supplied for object detection.");

            if (payload.command.EqualsIgnoreCase("detect") == true)               // Perform 'standard' object detection
            {
                var file = payload.files?.FirstOrDefault();
                if (file is null)
                    return new ModuleErrorResponse("No File supplied for object detection.");

                var minConfidenceValue = payload.GetValue("minConfidence", "0.4");
                if (!float.TryParse(minConfidenceValue, out float minConfidence))
                    minConfidence = 0.4f;

                response = DoDetection(file, minConfidence);
            }
            /*
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
            */
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

            return status.Merge(new
            {
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

            Initialize();
            ModuleResponse response = Process(request);
            Cleanup();

            if (response.Success)
                return 0;

            return 1;
        }
        
        protected override void Cleanup()
        {
            if (_predictor is not null)
            {
                _predictor.Dispose();
                _predictor = null;
            }
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

        private string GetStandardModelPath()
        {
            return (_modelSize ?? string.Empty.ToLower()) switch
            {
                "large"  => _modelDir + "yolov8l.onnx",
                "medium" => _modelDir + "yolov8m.onnx",
                "small"  => _modelDir + "yolov8s.onnx",
                "tiny"   => _modelDir + "yolov8n.onnx",
                _        => _modelDir + "yolov8m.onnx"
            };
        }

        /// <summary>
        /// Performs detection of objects in an image using the given model
        /// </summary>
        /// <param name="modelPath">The path to the model</param>
        /// <param name="file">The file object containing the image</param>
        /// <param name="minConfidenceValue">A string rep of the minimum detection confidence</param>
        /// <returns></returns>
        protected ModuleResponse DoDetection(RequestFormFile file, float minConfidence)
        {
            if (_predictor is null)
                return new ModuleErrorResponse($"Unable to create YOLOv8 predictor");

            if (file?.data == null)
                return new ModuleErrorResponse($"No file supplied");

            // Get the image from the data
            SixLabors.ImageSharp.Image sixLaborsImage;
            using (var stream = new System.IO.MemoryStream(file.data))
            {
                var image = Image.Load(stream/*, out IImageFormat format*/);
                sixLaborsImage = image.CloneAs<Rgba32>(); // Ensure the image is in the correct format
            }
            // if (sixLaborsImage is null)
            //    return new ModuleErrorResponse($"Unable to create image from image data");

            Stopwatch sw = Stopwatch.StartNew();
            var predictions = _predictor.Predict(sixLaborsImage);
            long inferenceMs = sw.ElapsedMilliseconds;

            if (predictions == null)
                return new ModuleErrorResponse("Yolo returned null.");

            var results = predictions.Where(x => x?.Rectangle != null && x.Score >= minConfidence);
            int count   = results.Count();

            string message;
            if (count > 3)
                message = "Found " + string.Join(", ", results.Take(3).Select(x => x?.Label?.Name ?? "item")) + "...";
            else if (count > 0)
                message = "Found " + string.Join(", ", results.Select(x => x?.Label?.Name ?? "item"));
            else
                message = "No objects found";

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
    }
}