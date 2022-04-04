using System;
using System.Text.Json.Serialization;

namespace CodeProject.SenseAI.API.Server.Backend
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
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; protected set; }
    }
#pragma warning restore IDE1006 // Naming Styles
}