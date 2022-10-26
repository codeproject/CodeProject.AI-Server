using System.Text.Json.Serialization;

using SkiaSharp;

namespace CodeProject.AI.AnalysisLayer.SDK
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Base class for queued requests for the backend. The naming here is for legacy backwards 
    /// compatibility, and should probably be updated to something sensible.
    /// </summary>
    public class BackendRequestBase
    {
        /// <summary>
        /// Gets the request unique id.  Used to return the response to the correct caller.
        /// </summary>
        [JsonInclude]
        public string reqid { get; private set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the request type.
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; protected set; }
    }

    /// <summary>
    /// Request with payload.
    /// </summary>
    public class BackendRequest : BackendRequestBase
    {
        /// <summary>
        /// Gets or sets the payload.
        /// </summary>
        [JsonInclude]
        public RequestPayload payload { get; protected set; }

        /// <summary>
        /// Instantiates a new instance of the <cref="BackendRequest" /> class.
        /// TODO: Normalise the input. Currently reqtype == payload.command. One or the other, please.
        /// </summary>
        /// <param name="reqtype">The request type</param>
        /// <param name="payload">The request payload</param>
        public BackendRequest(string reqtype, RequestPayload payload)
        {
            this.reqtype = reqtype;
            this.payload = payload;
        }
    }

    public class RequestPayload
    {
        /// <summary>
        /// Gets or sets the request command
        /// </summary>
        public string? command { get; set; }

        /// <summary>
        /// Gets or sets the queue name.
        /// </summary>
        public string? queue { get; set; }

        /// <summary>
        /// Gets or sets the set of key-value pairs passed by a client as part of a request.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string[]?>>? values { get; set; }

        /// <summary>
        /// Gets or sets the set of FormFiles passed in by a client as part of a request.
        /// </summary>
        public IEnumerable<RequestFormFile>? files { get; set; }

        // The additional segements at the end of the url path.
        public string[] urlSegments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets a string value from the payload.
        /// </summary>
        /// <param name="key">The name of the value.</param>
        /// <param name="defaultValue">The default value to return if not present or.</param>
        /// <returns></returns>
        public string? GetValue(string key, string? defaultValue = null)
        {
            return values?.FirstOrDefault(x => x.Key == key).Value?[0] ?? defaultValue;
        }

        /// <summary>
        /// Gets an int value from the payload
        /// </summary>
        /// <param name="key">The name of the value.</param>
        /// <param name="intValue">The out variable that holds the result.</param>
        /// <param name="defaultValue">The default value to return if not present or.</param>
        /// <returns></returns>
        public bool TryGet(string key,out int intValue, int? defaultValue = null)
        {
            return int.TryParse(GetValue(key, defaultValue?.ToString()), out intValue);
        }

        /// <summary>
        /// Gets float value from the payload
        /// </summary>
        /// <param name="key">The name of the value.</param>
        /// <param name="floatValue">The out variable that holds the result.</param>
        /// <param name="defaultValue">The default value to return if not present or.</param>
        /// <returns></returns>
        public bool TryGet(string key, out float floatValue, float? defaultValue = null)
        {
            return float.TryParse(GetValue(key, defaultValue?.ToString()), out floatValue);
        }

        /// <summary>
        /// Gets boolean value from the payload
        /// </summary>
        /// <param name="key">The name of the value.</param>
        /// <param name="boolValue">The out variable that holds the result.</param>
        /// <param name="defaultValue">The default value to return if not present or.</param>
        /// <returns></returns>
        public bool TryGet(string key, out bool boolValue, bool? defaultValue = null)
        {
            return bool.TryParse(GetValue(key, defaultValue?.ToString()), out boolValue);
        }

        /// <summary>
        /// Gets a File from the payload by name.
        /// </summary>
        /// <param name="name">The name of the file.</param>
        /// <returns>The RequestFormFile or null.</returns>
        public RequestFormFile? GetFile(string name)
        {
            return files?.FirstOrDefault(x => x.name == name);
        }

        /// <summary>
        /// Gets a File from the payload by index.
        /// </summary>
        /// <param name="name">The name of the file.</param>
        /// <returns>The RequestFormFile or null.</returns>
        public RequestFormFile? GetFile(int index)
        {
            return files?.ElementAtOrDefault(index);
        }
    }

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
            // Using SkiaSharp as it handles more formats and mostly cross-platform.
            if (data == null)
                return null;

            var skiaImage = SKImage.FromEncodedData(data);
            return skiaImage;
        }
    }

#pragma warning restore IDE1006 // Naming Styles
}