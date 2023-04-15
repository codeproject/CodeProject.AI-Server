using System.Collections.Generic;

namespace Yolov5Net.Scorer.Models.Abstract
{
    /// <summary>
    /// Model descriptor.
    /// </summary>
    public abstract class YoloModel
    {
        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
        public abstract int Depth { get; set; }

        public abstract int Dimensions { get; set; }

        public abstract int[] Strides { get; set; }
        public abstract int[][][] Anchors { get; set; }
        public abstract int[] Shapes { get; set; }

        public abstract float Confidence { get; set; }
        public abstract float MulConfidence { get; set; }
        public abstract float Overlap { get; set; }

        // This shold be read from the model metadata
        public abstract string[] Outputs { get; set; }

        // This shold be read from the model metadata
        public abstract List<YoloLabel> Labels { get; set; }

        public abstract bool UseDetect { get; set; }
    }
}
