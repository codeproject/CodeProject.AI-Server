using SkiaSharp;

namespace CodeProject.AI.SDK.Utils
{
    /// <summary>
    /// Represents an HTTP client to get requests and return responses to the CodeProject.AI server.
    /// </summary>
    public class ImageUtils
    {
        /// <summary>
        /// Loads a Bitmap from a file.
        /// </summary>
        /// <param name="filename">The file name.</param>
        /// <returns>The image, or null.</returns>
        /// <remarks>SkiSharp handles more image formats than System.Drawing.</remarks>
        public static SKImage? GetImage(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            // TODO: Add error handling and port this to Maui
            var skiaImage = SKImage.FromEncodedData(filename);
            if (skiaImage is null)
                return null;

            return skiaImage; //.ToBitmap();
        }

        /// <summary>
        /// Get an image from a byte array.
        /// </summary>
        /// <param name="imageStream">The stream</param>
        /// <returns>An image</returns>
        public static SKImage? GetImage(byte[]? imageData)
        {
            if (imageData == null)
                return null;

            var skiaImage = SKImage.FromEncodedData(imageData);
            if (skiaImage is null)
                return null;

            return skiaImage; //.ToBitmap();
        }

        /// <summary>
        /// Gets an image from a stream
        /// </summary>
        /// <param name="imageStream">The stream</param>
        /// <returns>A SKImage object</returns>
        /// <remarks>
        /// With this we don't have to extract the bytes into a byte[], SkiaSharp can work with the
        /// stream from the IFormFile directly, and handles multiple formats. A big space and time
        /// savings.
        /// TODO: update the coded in the NNNQueueWorkers to use this. Will need to update
        /// RequestFormFile to hold the stream. This will require RFF and any holder of RFF to be 
        /// IDisposable.
        /// </remarks>
        public static SKImage? GetImage(Stream imageStream)
        {
            if (imageStream == null)
                return null;

            var skiaImage = SKImage.FromEncodedData(imageStream);
            if (skiaImage is null)
                return null;

            return skiaImage; //.ToBitmap();
        }
    }

    public static class SKRectExtensions
    {
        static public float Area(this SKRect rect)
        {
            return rect.Width * rect.Height;
        }
    }
}