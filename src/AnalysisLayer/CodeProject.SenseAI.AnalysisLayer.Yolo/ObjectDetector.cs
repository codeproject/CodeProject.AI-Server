using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using Yolov5Net.Scorer;
using Yolov5Net.Scorer.Models;

using CodeProject.SenseAI.AnalysisLayer.SDK;

namespace CodeProject.SenseAI.Analysis.Yolo
{
    /// <summary>
    /// An Object Detection Prediction.
    /// </summary>
    public class DetectionPrediction : BoundingBoxPrediction
    {
        public string? label { get; set; }
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
        private readonly ILogger<ObjectDetector>     _logger;

        public ObjectDetector(IConfiguration config, IHostEnvironment env, ILogger<ObjectDetector> logger)
        {
            _logger = logger;
            string path = AppContext.BaseDirectory;
#if DEBUG
            _logger.LogInformation($"Yolo Execution Path: {path}");
            if (!Directory.Exists(Path.Combine(path, "assets")))
            {
                // We have been started by the Frontend in debug mode. Look for the assets in the
                // project root. Move up from bin\debug\netX.0
                path = Path.Combine(path, "..\\..\\..");
            }
#endif
            string mode = config.GetValue<string>("MODE");
            string modelPath = (mode ?? string.Empty.ToLower()) switch
            {
                "low"    => "assets/yolov5n.onnx",
                "high"   => "assets/yolov5m.onnx",
                "medium" => "assets/yolov5s.onnx",
                _        => "assets/yolov5m.onnx"
            };

            try
            {
                string modelFilePath = Path.Combine(path, modelPath);
                var modelFileInfo = new FileInfo(modelFilePath);
                if (modelFileInfo.Exists)
                    _scorer = new YoloScorer<YoloCocoP5Model>(modelFilePath);
                else
                    _logger.LogError("Unable to load the model at " + modelFilePath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to initialise the YOLO scorer");
            }
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

            using Image? image = GetImage(filename);
            List<YoloPrediction>? predictions = Predict(image);

            return predictions;
        }

        /// <summary>
        /// Predicts the objects in an image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns>The predicted objects with bounding boxes and confidences.</returns>
        public List<YoloPrediction>? Predict(Image? image)
        {
            if (image == null)
                return null;

            if (_scorer is null)
                return null;

            return _scorer.Predict(image);
        }

        public List<YoloPrediction>? Predict(byte[]? imageData)
        {
            if (imageData == null)
                return null;

            var image = GetImage(imageData);

            return _scorer?.Predict(image);
        }

        /// <summary>
        /// Loads a Bitmap from a file.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <returns>The Bitmap, or null.</returns>
        /// <remarks>SkiSharp handles more image formats than System.Drawing.</remarks>
        private Image? GetImage(string filename)
        {
            var skiaImage = SKImage.FromEncodedData(filename);
            if (skiaImage is null)
                return null;

            return skiaImage.ToBitmap();
        }

        private Image? GetImage(byte[] imageData)
        {
            var skiaImage = SKImage.FromEncodedData(imageData);
            if (skiaImage is null)
                return null;

            return skiaImage.ToBitmap();
        }
    }
}
