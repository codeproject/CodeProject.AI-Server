using System.Collections.Generic;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// The data sent by a server as part of the mesh broadcast to announce its availability in
    /// the mesh. This data is also returned as part of a ping to a server if broadcasting isn't
    /// possible due to network reasons.
    /// </summary>
    public class MeshServerBroadcastData: ServerResponse
    {
        /// <summary>
        /// Gets or sets the name of the system under which this instance is running (eg docker,
        /// windows, WSL)
        /// </summary>
        public string SystemDescription  { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the platform on which this system runs. eg Raspberry Pi, WSL, Docker or the
        /// OS if running native on 'standard' hardware.
        /// </summary>
        public string Platform  { get; set; } = string.Empty;

        /// <summary>
        /// The set of API routes that are mesh-enabled on the Server. A route is "/module/command"
        /// not the full path (/v1/module/command/).
        /// </summary>
        public IEnumerable<string>? EnabledRoutes { get; set; }

        /// <summary>
        /// Gets or sets whether this server is broadcasting its status to the Mesh.
        /// </summary>
        public bool IsBroadcasting { get; set; }

        /// <summary>
        /// Gets or sets whether this server is monitoring the status of servers in the mesh.
        /// /// </summary>
        public bool IsMonitoring { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this server accepts forwarded requests from 
        /// others servers in the mesh.
        /// </summary>
        public bool AcceptForwardedRequests { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this server will forward requests to other
        /// servers in the mesh.
        /// </summary>
        public bool AllowRequestForwarding { get; set; } = false;

        /// <summary>
        /// The set of hostnames that this server is aware of. Providing this as part of the 
        /// broadcast allows a server who hears the broadcast to alert this server if their name
        /// doesn't appear on that server's list.
        /// </summary>
        public IEnumerable<string>? KnownHostnames { get; set; }

        // TODO: Each server may have capabilities that may make it desirable to other servers when
        //       they are calculating which server to forward a request to. For instance, a server
        //       may be running a highly accurate model. This information will be added to this
        //       class in the future.
    }
}