using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

using SkiaSharp;
using Yolov5Net.Scorer;
using Yolov5Net.Scorer.Models;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Modules.ObjectDetection.Yolo
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
        public string? message { get; set; }
        public int count { get; set; }
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

        /// <summary>
        /// Gets or sets a value indicating whether or not this detector can use the current GPU
        /// </summary>
        public bool CanUseGPU { get; set; } = false;

        public ObjectDetector(string modelPath, ILogger<ObjectDetector> logger)
        {
            _logger = logger;

            try
            {
                var modelFileInfo = new FileInfo(modelPath);
                SessionOptions sessionOpts = GetSessionOptions();

                if (modelFileInfo.Exists)
                {
                    try
                    {
                        _scorer = new YoloScorer<YoloCocoP5Model>(modelPath, sessionOpts);
                    }
                    catch (Exception ex) // something went wrong, probably the device is too old and no longer supported.
                    {
                        // fall back to CPU only
                        if (ExecutionProvider != "CPU")
                        {
                            _logger.LogError(ex, $"Unable to load the model with {ExecutionProvider}. Falling back to CPU.");
                            _scorer = new YoloScorer<YoloCocoP5Model>(modelPath);
    
                            ExecutionProvider = "CPU";
                            HardwareType      = "CPU";
                        }
                        else
                            _logger.LogError(ex, $"Unable to load the model at {modelPath}");
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

        private SessionOptions GetSessionOptions()
        {
            bool supportGPU = (Environment.GetEnvironmentVariable("CPAI_MODULE_SUPPORT_GPU") ?? "true").ToLower() == "true";
            _logger.LogDebug($"ObjectDetection (.NET) supportGPU = {supportGPU}");

            var sessionOpts = new SessionOptions();
            
            string[]? providers = null;
            try
            {
                providers = OrtEnv.Instance().GetAvailableProviders();
            }
            catch
            {
            }

            foreach (var providerName in providers ?? Array.Empty<string>())
                _logger.LogDebug($"ObjectDetection (.NET) provider: {providerName}");

            // Enable CUDA  -------------------
            if (providers?.Any(p => p.StartsWithIgnoreCase("CUDA")) ?? false)
            {
                if (supportGPU)
                {
                    try
                    {
                        _logger.LogDebug($"ObjectDetection (.NET) setting ExecutionProvider = \"CUDA\"");

                        sessionOpts.AppendExecutionProvider_CUDA();

                        ExecutionProvider = "CUDA";
                        HardwareType      = "GPU";
                        CanUseGPU         = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"Exception setting ExecutionProvider = \"CUDA\"");
                        // do nothing, the provider didn't work so keep going
                    }
                }
                else
                    CanUseGPU = true;
            }

            // Enable OpenVINO -------------------
            if (providers?.Any(p => p.StartsWithIgnoreCase("OpenVINO")) ?? false)
            {
                if (supportGPU)
                {
                    try
                    {
                        _logger.LogDebug($"ObjectDetection (.NET) setting ExecutionProvider = \"OpenVINO\"");
                        sessionOpts.AppendExecutionProvider_OpenVINO("GPU_FP16");
                        //sessionOpts.EnableMemoryPattern = false;
                        //sessionOpts.ExecutionMode = ExecutionMode.ORT_PARALLEL;

                        ExecutionProvider = "OpenVINO";
                        HardwareType      = "GPU";
                        CanUseGPU         = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, $"Exception setting ExecutionProvider = \"OpenVINO\"");
                        // do nothing, the provider didn't work so keep going
                    }
                }
                else
                    CanUseGPU = true;
            }

            // Enable DirectML -------------------
            if (providers?.Any(p => p.StartsWithIgnoreCase("DML")) ?? false)
            {
                if (supportGPU)
                {
                    try
                    {
                        _logger.LogDebug($"ObjectDetection (.NET) setting ExecutionProvider = \"DML\"");
                        sessionOpts.EnableMemoryPattern    = false;
                        sessionOpts.ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL;
                        sessionOpts.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                        sessionOpts.AppendExecutionProvider_DML(); // Or set the device Id here in order to choose a card

                        ExecutionProvider = "DirectML";
                        HardwareType      = "GPU";
                        CanUseGPU         = true;
                    }
                    catch(Exception ex)
                    {
                        // do nothing, the provider didn't work so keep going
                        _logger.LogDebug(ex, $"Exception setting ExecutionProvider = \"DML\"");
                    }
                }
                else
                    CanUseGPU = true;
            }

            sessionOpts.AppendExecutionProvider_CPU();
            return sessionOpts;
        }

        /// <summary>
        /// Predicts the objects in an image file.
        /// </summary>
        /// <param name="filename">The filename of the image.</param>
        /// <returns>The predicted objects with bounding boxes and confidences.</returns>
        public List<YoloPrediction>? Predict(string filename, float minConfidence = 0.2F)
        {
            var fi = new FileInfo(filename);
            if (!fi.Exists)
                return null;

            using SKImage? image = ImageUtils.GetImage(filename);
            try
            {
                List<YoloPrediction>? predictions = Predict(image, minConfidence: minConfidence);

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
        public List<YoloPrediction>? Predict(SKImage? image, float minConfidence = 0.2F)
        {
            if (image == null)
                return null;

            if (_scorer is null)
                return null;

            try
            {
                return _scorer.Predict(image, minConfidence: minConfidence);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to run prediction");
                return null;
            }
        }

        public List<YoloPrediction>? Predict(byte[]? imageData, float minConfidence = 0.2F)
        {
            if (imageData == null)
                return null;

            var image = ImageUtils.GetImage(imageData);
            if (image is null)
                return null;
                
            try
            {
                return _scorer?.Predict(image, minConfidence: minConfidence);
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
    }
}
