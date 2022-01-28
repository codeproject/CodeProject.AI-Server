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

namespace CodeProject.SenseAI.Analysis.Yolo
{
    /// <summary>
    /// An YoloV5 object detector.
    /// </summary>
    public class ObjectDetector
    {
        private readonly YoloScorer<YoloCocoP5Model> _scorer;
        private readonly ILogger<ObjectDetector>     _logger;

        public ObjectDetector(IConfiguration config, IHostEnvironment env, ILogger<ObjectDetector> logger)
        {
            _logger = logger;
            var path = AppContext.BaseDirectory;
#if DEBUG
            _logger.LogInformation($"Yolo Execution Path: {path}");
            if (!Directory.Exists(Path.Combine(path, "Assets")))
            {
                // We have been started by the Frontend in debug mode
                // look for the assets in the project root.
                // Move up from bin\debug\net5.0
                path = Path.Combine(path, "..\\..\\..");
            }
#endif
            var mode = config.GetValue<string>("MODE");
            var modelPath = (mode ?? string.Empty.ToLower()) switch
            {
                "low"  => "Assets/yolov5n.onnx",
                "high" => "Assets/yolov5m.onnx",
                _      => "Assets/yolov5s.onnx"
            };
            _scorer = new YoloScorer<YoloCocoP5Model>(Path.Combine(path, modelPath));
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

            using var image                   = GetImage(filename);
            List<YoloPrediction>? predictions = (image is not null) ? Predict(image) : null;

            return predictions;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <returns>The predicted objects with bounding boxes and confidences.</returns>
        public List<YoloPrediction> Predict(Image image)
        {
            return _scorer.Predict(image);
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
    }
}
