using System.Diagnostics;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.Utils;

using SkiaSharp;

namespace CodeProject.AI.SDK
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Base class for queued requests for the backend. The naming here is for legacy backwards 
    /// compatibility, and should probably be updated to something sensible.
    /// </summary>
    /// <remarks>We should rename reqtype to command and just have BackendRequest. We don't need
    /// this base class.</remarks>
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
        /// <param name="payload">The request payload</param>
        public BackendRequest(RequestPayload payload)
        {
            this.reqtype = payload.command ?? string.Empty;
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
        /// Gets or sets the set of key-value pairs passed by a client as part of a request.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string?[]>>? values { get; set; }

        /// <summary>
        /// Gets or sets the set of FormFiles passed in by a client as part of a request.
        /// </summary>
        public IEnumerable<RequestFormFile>? files { get; set; }

        // The additional segments at the end of the url path.
        public string[] urlSegments { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Instantiates a new instance of the <cref="RequestPayload" /> class.
        /// </summary>
        public RequestPayload()
        {
        }

        /// <summary>
        /// Instantiates a new instance of the <cref="RequestPayload" /> class.
        /// </summary>
        /// <param name="command">The request command</param>
        public RequestPayload(string command)
        {
            this.command = command;
        }
       
        /// <summary>
        /// Sets a string value in payload.
        /// </summary>
        /// <param name="key">The name of the value.</param>
        /// <param name="value">The default value to return if not present or.</param>
        /// <param name="overwrite">If true, and the key already exists, then the value 
        /// for this key will be overwritten. Otherwise the value will be added to that
        /// key</param>
        public void SetValue(string key, string value, bool overwrite = true)
        {
            // We're using the KeyValuePair and Array classes for value items, both of
            // which are immutable. We're also specifying IEnumerable for the collection
            // of values, so we can't control what access methods we have. This means we
            // have to rebuild the entire values collection each time we want to change
            // something.

            // No current list, so create a new one. Easy.
            if (values is null || values.Count() == 0)
            {
                var newValues = new List<KeyValuePair<string, string?[]>>();
                newValues.Add(new KeyValuePair<string, string?[]>(key, new string[] { value }));
                values = newValues;
            }
            // Existing list, but key doesn't exist, so just add a new keyvalue pair. Easy.
            else if (!values.Any(pair => pair.Key != key))
            {
                var newValues = values.ToList();
                newValues.Add(new KeyValuePair<string, string?[]>(key, new string[] { value }));
                values = newValues;
            }
            // Existing list and key already exists. Annoying.
            else
            {           
                if (overwrite)
                {
                    // Create a new list with all values except the key, then add a fresh value
                    // with the new key value.
                    var newValues = values.Where(pair => pair.Key != key).ToList();
                    newValues.Add(new KeyValuePair<string, string?[]>(key, new string[] { value }));
                    values = newValues;
                }
                else
                {
                    // Get the old key value, create a new key value pair with the extra value,
                    // then create a new list with all values except the old key, and add back the
                    // modified key value pair.
                    KeyValuePair<string, string?[]> pair = values.Single(pair => pair.Key == key);
                    var newPair = new KeyValuePair<string, string?[]>(key, pair.Value.Append(value).ToArray());
                    var newValues = values.Where(pair => pair.Key != key).ToList();
                    newValues.Add(newPair);
                    values = newValues;
                }
            }
        }

        /// <summary>
        /// Gets a string value from the payload.
        /// </summary>
        /// <param name="key">The name of the value.</param>
        /// <param name="defaultValue">The default value to return if not present or.</param>
        /// <returns>A string</returns>
        /// <remarks>
        /// Note that payloads are essentially mirroring HTML forms, and as such there can be
        /// more than one value per key. This method will return only the first value.
        /// </remarks>
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
        /// Adds a file to the payload
        /// </summary>
        /// <param name="filePath">The path to the file to add</param>
        public void AddFile(string filePath)
        {
            try
            {
                var formFile = new RequestFormFile()
                {
                    name        = "image",
                    filename    = Path.GetFileName(filePath),
                    contentType = "image/" + Path.GetExtension(filePath).Substring(1),
                    data        = File.ReadAllBytes(filePath)
                };

                var allFiles = files as List<RequestFormFile> ?? new List<RequestFormFile>();                
                allFiles.Add(formFile);

                files = allFiles;
            }
            catch (Exception e)
            {
                Debug.WriteLine("Error adding file: " + e.Message);
            }
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
            return ImageUtils.GetImage(data);
        }
    }

#pragma warning restore IDE1006 // Naming Styles
}