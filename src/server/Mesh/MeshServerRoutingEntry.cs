using System;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// This class contains information on a server participating in the mesh, and includes
    /// enough information to allow the request proxy to choose which server to pass a request
    /// to, and how to route the request.
    /// </summary>
    public class MeshServerRoutingEntry
    {
        /// <summary>
        /// Gets a value indicating whether or not this server is a local server.
        /// TODO: Replace with a ServerType (NodeType) enum with values "Network" or "LocalHost"
        /// </summary>
        public bool IsLocalServer { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the server is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets the hostname that is to be used for sending requests to this server. If the
        /// server is discovered via UDP broadcast then this will be either the IP address or
        /// hostname the server reports. If it is a known server this will be the known hostname
        /// hardcoded for this known server. 
        /// </summary>
        public string CallableHostname { get; }

        /// <summary>
        /// Gets or sets the endpoint address for this server. This is the address that was 
        /// reported to us when that server pinged us or sent a UDP broadcast.
        /// </summary>
        public string? EndPointIPAddress { get; set; }

        /// <summary>
        /// Gets or sets the time of the last contact (heartbeat or ping) from the server.
        /// </summary>
        public DateTime LastContactTime { get; set; }

        /// <summary>
        /// Gets or sets the status of the remote server as reported by that server
        /// </summary>
        public MeshServerBroadcastData Status { get; set; } = new();

        /// <summary>
        /// Creates a new instance of the ServiceStatus class.
        /// </summary>
        /// <param name="callableHostname">The hostname to be used for sending calls to this
        /// server (which could be different from the hostname this server reports for itself)
        /// </param>
        /// <param name="endPointIPAddress">The endpoint address for this server. This is the
        /// address that was reported to us when that server pinged us or sent a UDP broadcast.
        /// </param>
        /// <param name="isLocal">Whether or not this server is a local server.</param>
        public MeshServerRoutingEntry(string callableHostname, string? endPointIPAddress, bool isLocal)
        {
            CallableHostname  = callableHostname;
            EndPointIPAddress = endPointIPAddress;
            IsLocalServer     = isLocal;
        }

        /// <summary>
        /// Returns a string representation of this server status
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            string description = CallableHostname;

            if (EndPointIPAddress is not null)
                description += " / " + EndPointIPAddress;

            if (IsLocalServer) 
                description += " (localhost)";
            else if (!CallableHostname.EqualsIgnoreCase(Status.Hostname))
                description += $" ({Status.Hostname})";

            if (!IsActive) description  += " NOT ACTIVE";

            return description;
        }
    }
}