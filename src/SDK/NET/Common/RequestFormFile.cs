using CodeProject.AI.SDK.Utils;

using SkiaSharp;

namespace CodeProject.AI.SDK.Common
{
    public class RequestFormFile
    {
        /// <summary>
        /// Gets or sets the form field name of the file being passed.
        /// </summary>
        public string? name { get; set; }

        /// <summary>
        /// Gets or sets the name of the file being passed.
        /// </summary>
        public string? filename { get; set; }

        /// <summary>
        /// Gets or sets the content type of the file being passed.
        /// </summary>
        public string? contentType { get; set; }

        /// <summary>
        /// Gets or sets the actual file data being passed.
        /// </summary>
        public byte[]? data { get; set; }

        /// <summary>
        /// Converts the RequestFormFile to an Image.
        /// </summary>
        /// <returns>The image, or null if conversion fails.</returns>
        public SKImage? AsImage()
        {
            return ImageUtils.GetImage(data);
        }
    }

#pragma warning restore IDE1006 // Naming Styles
}