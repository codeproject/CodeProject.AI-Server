using SkiaSharp;

namespace Yolov5Net.Scorer.Extensions
{
    public static class RectangleExtensions
    {
        static public float Area(this SKRect rect)
        {
            return rect.Width * rect.Height;
        }
    }
}
