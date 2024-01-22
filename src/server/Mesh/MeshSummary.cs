using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// Represents a summary of the information we have gleaned from another server in the mesh.
    /// </summary>
    /// <remarks>
    /// This is (currently) only used to report summary info to the UI
    /// </remarks>
    public class MeshServerSummary: MeshServerRoutingEntry
    {
        /// <summary>
        /// Gets or sets the route information.
        /// </summary>
        public IEnumerable<MeshServerRoutePerformance> RouteInfos { get; }

        /// <summary>
        /// Gets a human readable summary of this object
        /// </summary>
        public string Summary
        {
            get
            {
                string indent = "    ";
               
                var summary = new StringBuilder();
                summary.AppendLine($"{indent}Server {Status.Hostname}");
                summary.AppendLine($"{indent}System:              {Status.SystemDescription}");
                if (IsLocalServer)
                {
                    summary.AppendLine($"{indent}IP Address:          {CallableHostname}");
                    // summary.AppendLine($"{indent}All addresses:       {string.Join(", ", AllIPAddresses)}");
                }
                summary.AppendLine($"{indent}Active:              {IsActive}");
                summary.AppendLine($"{indent}Forwarding Requests: {Status.AllowRequestForwarding}");
                summary.AppendLine($"{indent}Accepting Requests:  {Status.AllowRequestForwarding}");

                if (Status.KnownHostnames is not null)
                {
                    // summary.AppendLine(" ");
                    summary.AppendLine($"{indent}Known servers:");

                    foreach (string knownServer in Status.KnownHostnames)
                        summary.AppendLine($"{indent}{indent}{knownServer}");
                }

                // summary.AppendLine(" ");
                summary.AppendLine($"{indent}Routes:");

                int maxPathLength = 0;
                foreach (MeshServerRoutePerformance route in RouteInfos)
                    if (route.Route.Length > maxPathLength)
                        maxPathLength = route.Route.Length;

                foreach (MeshServerRoutePerformance route in RouteInfos)
                {
                    int padding = maxPathLength + 4;
                    // string hit = route.NumberOfRequests == 1 ? "hit" : "hits";
                    string hit  = "requests";

                    summary.Append($"{indent}{indent}{route.Route.PadRight(padding)}");
                    summary.Append($"{route.EffectiveResponseTime}ms, ");
                    summary.Append($"{route.NumberOfRequests:N0} {hit}");
                    summary.AppendLine();
                }

                return summary.ToString();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MeshServerSummary"/> class.
        /// </summary>
        /// <param name="server">The MeshServerRoutingInfo object.</param>
        /// <param name="routeInfos">The route information.</param>
        public MeshServerSummary(MeshServerRoutingEntry server,
                                IEnumerable<MeshServerRoutePerformance> routeInfos)
            : base(server.CallableHostname, server.EndPointIPAddress, server.IsLocalServer)
        {
            IsActive          = server.IsActive;
            Status            = server.Status; 
            RouteInfos        = routeInfos;
        }
    }

    /// <summary>
    /// Contains information on how this current server participates in the mesh and what it knows 
    /// about other servers also in the mesh.
    /// </summary>
    /// <remarks>
    /// This is currently used purely for UI reporting
    /// </remarks>
    public class MeshSummary
    {
        /// <summary>
        /// Gets the local Mesh server information
        /// </summary>
        public MeshServerSummary? LocalServer
        {
            get
            {
                foreach (var serverInfo in ServerInfos)
                    if (serverInfo.IsLocalServer)
                        return serverInfo;

                return null;
            }
        }

        /// <summary>
        /// Gets or sets the collection of server information.
        /// </summary>
        public IEnumerable<MeshServerSummary> ServerInfos { get; set; }

        /// <summary>
        /// Gets a human readable summary of the mesh status
        /// </summary>
        public string Summary
        {
            get
            {
                string indent = "    ";

                MeshServerSummary? localServer = LocalServer;

                var summary = new StringBuilder();
                summary.AppendLine($"Current Server mesh status");
                summary.AppendLine(" ");

                if (localServer is null)
                {
                    summary.AppendLine($"{indent}Active:       false");
                    summary.AppendLine($"{indent}Broadcasting: false");
                    summary.AppendLine($"{indent}Monitoring:   false");
                    summary.AppendLine(" ");
                }
                else
                {
                    summary.AppendLine($"{indent}Broadcasting: {localServer.Status.IsBroadcasting}");
                    summary.AppendLine($"{indent}Monitoring:   {localServer.Status.IsMonitoring}");
                    summary.AppendLine(" ");
                    summary.Append(localServer.Summary);
                    summary.AppendLine(" ");
                }
                
                int remoteServerCount = ServerInfos.Count();
                if (localServer is not null)
                    remoteServerCount--;

                summary.AppendLine($"Remote Servers in mesh: {remoteServerCount}");
                summary.AppendLine(" ");

                int count = 0;
                foreach (MeshServerSummary serverInfo in ServerInfos)
                {
                    if (!serverInfo.IsLocalServer)
                    {
                        if (count > 0)
                            summary.AppendLine(" ");

                        summary.Append(serverInfo.Summary);
                        count++;
                    }
                }

                return summary.ToString().Trim();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MeshSummary"/> class.
        /// </summary>
        /// <param name="serverInfos">The information about the servers and their routes.</param>
        public MeshSummary(IEnumerable<MeshServerSummary> serverInfos)
        {
            ServerInfos = serverInfos;
        }
    }
}