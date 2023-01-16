using SkiaSharp;

namespace CodeProject.AI.SDK.Common
{
    public static class SKRectExtensions
    {
        static public float Area(this SKRect rect)
        {
            return rect.Width * rect.Height;
        }
    }
}
