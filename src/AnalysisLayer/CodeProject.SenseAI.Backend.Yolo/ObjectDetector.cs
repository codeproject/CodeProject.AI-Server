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
        private static readonly YoloScorer<YoloCocoP5Model> _scorer;

        static ObjectDetector()
        {
            _scorer = new YoloScorer<YoloCocoP5Model>("Assets/Weights/yolov5s.onnx");
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

            using var image                  = Image.FromFile(filename);
            List<YoloPrediction> predictions = Predict(image);

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
    }
}
