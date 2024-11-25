using System;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.SDK.Backend
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

#pragma warning restore IDE1006 // Naming Styles
}