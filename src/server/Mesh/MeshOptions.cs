using System;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// This class contains the configuration for the Mesh.
    /// </summary>
    public class MeshOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether this server is broadcasting its status to the
        /// mesh. This property is an override for all other "enable" flags and allows a simple off
        /// switch.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this server is broadcasting its status to the
        /// mesh.
        /// </summary>
        /// <remarks>
        /// This property allows a server to monitor the mesh but remain incognito by not
        /// broadcasting its status.
        /// </remarks>
        public bool EnableStatusBroadcast { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this server is monitoring the status of
        /// the servers on the mesh.
        /// </summary>
        /// <remarks>
        /// This property isn't useful in practice and is only here for symmetry. There is no use
        /// case where a server would not be enabled but not monitoring the mesh and this property
        /// can, in practice, be replaced by the "Enable" property.
        /// </remarks>
        public bool EnableStatusMonitoring { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this server accepts forwarded requests from 
        /// others servers in the mesh.
        /// </summary>
        /// <remarks>
        /// This property is provided to allow a server to be in the mesh but to not accept 
        /// forwarded requests from other servers. For example, if a server needs to manage its load
        /// it could temporarily halt the processing of requests from the mesh until the high load
        /// situation has passed.
        /// </remarks>
        public bool AcceptForwardedRequests { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this server will forward requests to other
        /// servers in the mesh.
        /// </summary>
        /// <remarks>
        /// This property is provided to allow a server to keep all processing onsite. For example,
        /// if a server is processing sensitive and needs to ensure that the data will remain on the
        /// local server then it could temporarily halt the forwarding of requests while processing
        /// this sensitive data.
        /// </remarks>
        public bool AllowRequestForwarding { get; set; } = true;

        /// <summary>
        /// Gets or sets the set of IP addresses that should be included in the mesh. These are 
        /// typically IP addresses that cannot broadcast to this server due to IPSEC or other issues
        /// but are still able to be contacted directly. This server will need to occasionally ping
        /// the servers at these addresses to get mesh status info.
        /// </summary>
        public string[] KnownMeshHostnames { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets timeout for pinging the known mesh servers
        /// </summary>
        public TimeSpan MeshServerPingTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the name of the service.
        /// </summary>
        public string ServiceName { get; set; } = "CodeProject.AI-Mesh";

        /// <summary>
        /// Gets or sets the port to use for the UDP messages.
        /// </summary>
        public int Port { get; set; } = 32168;

        /// <summary>
        /// Gets or sets the time between HEARTBEAT messages.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the time between sending pings to servers we only know via IP address
        /// </summary>
        public TimeSpan ServerPingInterval { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets or sets the time to wait before retrying a ping to a server that had an error
        /// </summary>
        public TimeSpan PingErrorRecoveryTimeout { get; set; } = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Gets or sets the time of not receiving a HEARTBEAT from a server to consider the server
        /// as inactive (failed or removed from the mesh).
        /// </summary>
        public TimeSpan HeartbeatInactiveTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets how often we manually update route timing metrics. Timing metrics are 
        /// updated each time a route is used, but if a route isn't being used (eg due to a timeout)
        /// then this check will enable the bad timing values to be reset so that a route's timings
        /// go back to a point where it will again be chosen to process requests.
        /// </summary>
        public TimeSpan UpdateTimingMetricsInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Gets or sets how long the local server hasn't used a route on a remote server before the
        /// timing metrics for that route are adjusted to increase the probability that the route
        /// will again be chosen to service a request. This provides the means to gradually allow a
        /// route that failed to have another shot at things.
        /// </summary>
        public TimeSpan RouteInactivityTimeout { get; set; } = TimeSpan.FromSeconds(60);
    }
}
