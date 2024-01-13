using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// Information on a server in the mesh we know of (as opposed to one discovered by UDP broadcast)
    /// </summary>
    public class KnownMeshServerPingStatus
    {
        /// <summary>
        /// The machine's netBIOS machine name or IPv4 address.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this server is actually the current server.
        /// </summary>
        public bool IsLoopback { get; set; }

        /// <summary>
        /// The most recent response time in ms
        /// </summary>
        public int ResponseMs { get; set; }

        /// <summary>
        /// The last HTTP status code received from a ping to this server
        /// </summary>
        public HttpStatusCode LastStatusCode { get; set; } = HttpStatusCode.OK;

        /// <summary>
        /// The time of the last server error, or MinValue if none
        /// </summary>
        public DateTime LastErrorUtcTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Creates a new instance of the ServerPingStatus class
        /// </summary>
        /// <param name="hostname">The machine netBIOS name or IPv4 address</param>
        public KnownMeshServerPingStatus(string hostname)
        {
            Hostname = hostname;
        }

        /// <summary>
        /// Sets the loopback status of this server based on the hostname specified for this server
        /// as well as the IPs identified on the current server.
        /// </summary>
        /// <param name="machineIPAddress">The IP address of this machine</param>
        /// <param name="ipAddresses">The list of IPs for the current machine</param>
        public void CheckIsLoopback(IPAddress machineIPAddress, IEnumerable<IPAddress> ipAddresses)
        {
            // Check be name first
            IsLoopback = Hostname.EqualsIgnoreCase("localhost");

            // If that doesn't show loopback, see if the hostname is actually an IP address and
            // check that
            if (!IsLoopback && !IPAddress.TryParse(Hostname, out IPAddress? ipAddress) && 
                ipAddress != null)
            {
                IsLoopback = IPAddress.IsLoopback(ipAddress)    || 
                             ipAddress.Equals(machineIPAddress) ||
                             ipAddresses.Any(a => ipAddress.Equals(a));
            }
        }

        /// <summary>
        /// Returns a string representation of this server status
        /// </summary>
        /// <returns>A string</returns>
        public override string ToString()
        {
            string description = Hostname;
            if (IsLoopback) description += " (localhost)";
            description += $" ({LastStatusCode})";

            return description;
        }        
    }
}
