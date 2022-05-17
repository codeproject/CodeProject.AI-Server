using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeProject.SenseAI.AnalysisLayer.SDK
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Base class for queued requests for the backend.
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
    }

#pragma warning restore IDE1006 // Naming Styles
}