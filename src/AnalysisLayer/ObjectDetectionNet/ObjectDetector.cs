using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

using SkiaSharp;
using Yolov5Net.Scorer;
using Yolov5Net.Scorer.Models;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK;

namespace CodeProject.AI.AnalysisLayer.ObjectDetection.Yolo
{
    /// <summary>
    /// An Object Detection Prediction.
    /// </summary>
    public class DetectionPrediction : BoundingBoxPrediction
    {
        public string? label { get; set; }
    }

    /// <summary>
    /// An Custom Object 'List available models' Response.
    /// </summary>
    public class BackendCustomModuleListResponse : BackendSuccessResponse
    {
        public string[]? models { get; set; }
    }

    /// <summary>
    /// An Object Detection Response.
    /// </summary>
    public class BackendObjectDetectionResponse : BackendSuccessResponse
    {
        public DetectionPrediction[]? predictions { get; set; }
    }

    /// <summary>
    /// An YoloV5 object detector.
    /// </summary>
    public class ObjectDetector
    {
        private readonly YoloScorer<YoloCocoP5Model>? _scorer = null;
        private readonly ILogger<ObjectDetector>      _logger;

        /// <summary>
        /// Gets or sets the execution provider.
        /// </summary>
        public string ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the hardware type (CPU or GPU).
        /// </summary>
        public string HardwareType { get; set; } = "CPU";

        public ObjectDetector(string modelPath, ILogger<ObjectDetector> logger)
        {
            _logger = logger;

            try
            {
                var modelFileInfo = new FileInfo(modelPath);
                SessionOptions sessionOpts = GetHardwareInfo();

                if (modelFileInfo.Exists)
                {
                    try
                    {
                        _scorer = new YoloScorer<YoloCocoP5Model>(modelPath, sessionOpts);
                    }
                    catch // something went wrong, probably the device is too old and no longer supported.
                    {
                        // fall back to CPU only
                        if (ExecutionProvider != "CPU")
                        {
	                        _logger.LogError($"Unable to load the model with {ExecutionProvider}. Falling back to CPU.");
	                        _scorer = new YoloScorer<YoloCocoP5Model>(modelPath);
	
	                        ExecutionProvider = "CPU";
	                        HardwareType      = "CPU";
                        }
                        else
                            _logger.LogError("Unable to load the model at " + modelPath);
                    }
                }
                else
                    _logger.LogError("Unable to load the model at " + modelPath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to initialise the YOLO scorer");
            }
        }

        private SessionOptions GetHardwareInfo()
        {
            var sessionOpts = new SessionOptions();

            bool supportGPU = (Environment.GetEnvironmentVariable("CPAI_MODULE_SUPPORT_GPU") ?? "false").ToLower() == "true";
            if (supportGPU)
            {
                string[]? providers = null;
                try
                {
                    providers = OrtEnv.Instance().GetAvailableProviders();
                }
                catch
                {
                }

                // Enable CUDA  -------------------
                if (providers?.Any(p => p.StartsWithIgnoreCase("CUDA")) ?? false)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_CUDA();

                        ExecutionProvider = "CUDA";
                        HardwareType      = "GPU";
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }

                // Enable OpenVINO -------------------
                if (providers?.Any(p => p.StartsWithIgnoreCase("OpenVINO")) ?? false)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_OpenVINO("GPU_FP16");
                        //sessionOpts.EnableMemoryPattern = false;
                        //sessionOpts.ExecutionMode = ExecutionMode.ORT_PARALLEL;

                        ExecutionProvider = "OpenVINO";
                        HardwareType      = "GPU";
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }

                // Enable DirectML -------------------
                if (providers?.Any(p => p.StartsWithIgnoreCase("DML")) ?? false)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_DML();
                        sessionOpts.EnableMemoryPattern    = false;
                        sessionOpts.ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL;
                        sessionOpts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL;

                        ExecutionProvider = "DirectML";
                        HardwareType      = "GPU";
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }
            }

            sessionOpts.AppendExecutionProvider_CPU();
            return sessionOpts;
        }

        /// <summary>
        /// Predicts the objects in an image file.
        /// </summary>
        /// <param name="filename">The filename of the image.</param>
        /// <returns>The predicted objects with bounding boxes and confidences.</returns>
        public List<YoloPrediction>? Predict(string filename)
        {
            var fi = new FileInfo(filename);
            if (!fi.Exists)
                return null;

            using SKImage? image = GetImage(filename);
            try
            {
                List<YoloPrediction>? predictions = Predict(image);

                return predictions;
            }
            finally
            {
                image?.Dispose();
            }
        }

        /// <summary>
        /// Predicts the objects in an image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns>The predicted objects with bounding boxes and confidences.</returns>
        public List<YoloPrediction>? Predict(SKImage? image)
        {
            if (image == null)
                return null;

            if (_scorer is null)
                return null;

            try
            {
                return _scorer.Predict(image);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to run prediction");
                return null;
            }
        }

        public List<YoloPrediction>? Predict(byte[]? imageData)
        {
            if (imageData == null)
                return null;

            var image = GetImage(imageData);
            if (image is null)
                return null;
                
            try
            {
                return _scorer?.Predict(image);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to run prediction");
                return null;
            }
            finally
            { 
                image?.Dispose(); 
            }
        }

        /// <summary>
        /// Loads a Bitmap from a file.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <returns>The Bitmap, or null.</returns>
        /// <remarks>SkiSharp handles more image formats than System.Drawing.</remarks>
        private SKImage? GetImage(string filename)
        {
            // TODO: Add error handling and port this to Maui
            var skiaImage = SKImage.FromEncodedData(filename);
            if (skiaImage is null)
                return null;

            return skiaImage; //.ToBitmap();
        }

        private SKImage? GetImage(byte[] imageData)
        {
            var skiaImage = SKImage.FromEncodedData(imageData);
            if (skiaImage is null)
                return null;

            return skiaImage; //.ToBitmap();
        }
    }
}
