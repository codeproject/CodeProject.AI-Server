using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using CodeProject.AI.Server.Modules;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

/* -------------------------------------------------------------------------------------------------
Some Terminology:

"Mesh" is the abstract concept of a set of servers that will work together to achieve a goal.

We don't manage a "mesh" but we do manage the current server's participation in the mesh. There is
no way to shut down or control the mesh: only a way to manage individual participants.

"Mesh Server" is a server participating in the mesh
"Discovered Server" is a server that another server has discovered by listening to the UDP broadcast
"Known Server" is a server that is known and whose address is known and hardcoded into the appsettings.

------------------------------------------------------------------------------------------------- */

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// Provides functionality to monitor and manage the server mesh
    /// </summary>
    public class MeshManager
    {
        private readonly MeshMonitor                  _meshMonitor;
        private readonly IOptionsMonitor<MeshOptions> _meshOptions;
        private readonly ServerSettingsJsonWriter     _optionsJsonWriter;
        private readonly ModuleProcessServices        _processServices;
        private readonly Task                         _updateRouteMetricsTask;
        private readonly ConcurrentDictionary<string, RouteMetricsCollection> _routeMetrics = 
            new(StringComparer.OrdinalIgnoreCase);


        /// <summary>
        /// Gets a value indicating whether this server is participating in the mesh.
        /// </summary>
        public bool Enabled                 => _meshOptions.CurrentValue.Enable;

        /// <summary>
        /// Gets a value indicating whether we accept forwarded requests.
        /// </summary>
        public bool AcceptForwardedRequests => Enabled && _meshOptions.CurrentValue.AcceptForwardedRequests;

        /// <summary>
        /// Gets a value indicating whether we allow request forwarding.
        /// </summary>
        public bool AllowRequestForwarding  => Enabled && _meshOptions.CurrentValue.AllowRequestForwarding;

        /// <summary>
        /// Gets a value indicating whether requests to mesh servers will be done using host names
        /// or IP addresses
        /// </summary>
        public bool RouteViaHostName        => _meshMonitor.RouteViaHostname;

        /// <summary>
        /// Initializes a new instance of the MeshManager class.
        /// </summary>
        /// <param name="meshMonitor">The Mesh Monitor.</param>
        /// <param name="monitoredMeshOptions">The Mesh Options Monitor</param>
        /// <param name="optionsJsonWriter">Writes the Mesh JSON file.</param>
        /// <param name="processServices">The process services.</param>
        public MeshManager(MeshMonitor meshMonitor,
                           IOptionsMonitor<MeshOptions> monitoredMeshOptions,
                           ServerSettingsJsonWriter optionsJsonWriter,
                           ModuleProcessServices processServices)
        {
            _meshMonitor       = meshMonitor;
            _meshOptions       = monitoredMeshOptions;
            _optionsJsonWriter = optionsJsonWriter;
            _processServices   = processServices;

            // Add the event handlers to remove the metrics for the server when it goes active,
            // inactive or closes.
            _meshMonitor.OnActive   += ResetMetricsForServer;
            _meshMonitor.OnInActive += ResetMetricsForServer;
            _meshMonitor.OnClose    += ResetMetricsForServer;

            // When any module's state changes, have a heartbeat sent out to advise other servers in
            // the mesh that a route may have been added or removed for this server.
            _processServices.OnModuleStateChange = SendHeartbeat;

            // Start the task to remove old response times.
            _updateRouteMetricsTask = UpdateRouteMetricsLoop();
        }

        /// <summary>
        /// Select a server to use for the request. The server is selected based on the Effective
        /// Response Time for the route. How this is calculated depends on the algorithm used
        /// to determine the Effective Response Time and may change in the future.
        /// </summary>
        /// <param name="pathSuffix">The path for this request without the "v1". This will be in the
        /// form "category/module[/command]". eg "image/alpr" or "vision/custom/modelName".</param>
        /// <returns>A value indicating a remote server was selected and the remote URL.</returns>
        public MeshServerRoutingEntry? SelectServer(string pathSuffix)
        {
            if (!_meshMonitor.IsEnabled || !_meshMonitor.IsRunning || !_meshMonitor.DiscoveredServers.Any())
                return null;

            // The path in the ServerMeshStatus.EnabledRoutes may only be the start of the URL so we
            // need to check if the path starts with any of the paths in ServerMeshStatus.EnabledRoutes
            string route = GetRouteFromPathSuffix(pathSuffix);

            IEnumerable<MeshServerRoutingEntry> serverCandidates;
            serverCandidates = _meshMonitor.DiscoveredServers
                                           .Values
                                           .Where(x => x.IsActive)
                                           .Where(x => x.Status?
                                                        .EnabledRoutes?
                                                        .Any(r => r.EqualsIgnoreCase(route)) ?? false);      
            if (!serverCandidates.Any())
                return null;

            // find all servers with the same fastest response time (there may be more than one that
            // has the 'fastest' time). Note "First" will use RouteMetrics.CompareTo, which at the
            // moment orders by EffectiveResponseTime. It may in future use other signals such as
            // accuracy, cost, class list etc.
            
            // HACK: have to use EffectiveResponseTime because we can't group by the RouteMetrics
            //       Need to fix this.
            var possibleServers = serverCandidates
                                    .GroupBy(server => GetMetricsCollection(server)
                                                            .GetMetricsFromRoute(route)
                                                            .EffectiveResponseTime)
                                    .OrderBy(grouping => grouping.Key)
                                    .First();

            // select the fastest server, if there are multiple servers with the same response time
            // then select the local server if it is one of them, otherwise select the first one.
            MeshServerRoutingEntry fastestServer;
            fastestServer = possibleServers.First() ?? possibleServers.First(x => x.IsLocalServer);
                         
            return fastestServer;
        }

        /// <summary>
        /// Registers a server in the list of known servers.
        /// </summary>
        /// <param name="hostname">The hostname to register</param>
        /// <returns>True on success; false otherwise</returns>
        public bool RegisterServer(string hostname)
        {
            return _meshMonitor.RegisterKnownServer(hostname);
        }

        /// <summary>
        /// Returns info on how this current server participates in the mesh and what it knows about
        /// other servers also in the mesh.
        /// </summary>
        /// <returns>A <see cref="MeshSummary"/> object.</returns>
        public MeshSummary GetMeshMonitoringInfo()
        {
            if (!_meshMonitor.IsEnabled || !_meshMonitor.IsRunning || 
                !_meshMonitor.DiscoveredServers.Any())
            {
                return new MeshSummary(Array.Empty<MeshServerSummary>());
            }

            var meshServers = new List<MeshServerSummary>();

            foreach (MeshServerRoutingEntry server in _meshMonitor.DiscoveredServers.Values)
            {
                var meshRoutes = new List<MeshServerRoutePerformance>();
                RouteMetricsCollection metricsCollection = GetMetricsCollection(server);

                foreach (string route in server.Status.EnabledRoutes ?? Enumerable.Empty<string>())
                {
                    RouteMetrics metrics = metricsCollection.GetMetricsFromRoute(route);
                    meshRoutes.Add(new MeshServerRoutePerformance(route, metrics.EffectiveResponseTime,
                                                           metrics.NumberOfRequests));
                }

                var serverSummary = new MeshServerSummary(server, meshRoutes);
                meshServers.Add(serverSummary);
            };

            return new MeshSummary(meshServers);
        }

        /// <summary>
        /// Gets the current mesh status
        /// </summary>
        /// <returns>A <see cref="MeshServerBroadcastData" /> object</returns>
        public MeshServerBroadcastData GetMeshServerBroadcastData()
        {
            return _meshMonitor.MeshStatus;
        }

        /// <summary>
        /// Add a response time for the given path for the given mesh server.
        /// </summary>
        /// <param name="status">The mesh server status of the server. If null then the local server
        /// will be assumed</param>
        /// <param name="pathSuffix">The path for this request without the "v1". This will be in the
        /// form "category/module[/command]". eg "image/alpr" or "vision/custom/modelName".</param>
        /// <param name="responseTime">The response time to add to the samples.</param>
        public void AddResponseTime(MeshServerRoutingEntry? status, string pathSuffix, int responseTime)
        {
            MeshServerRoutingEntry? server = status ?? GetLocalMeshServer();
            if (server is null)
                return;

            string route = GetRouteFromPathSuffix(pathSuffix);
            RouteMetrics metrics = GetMetricsCollection(server).GetMetricsFromRoute(route);

            metrics.RecordRequest(responseTime);
        }

        /// <summary>
        /// Resets the metrics for the given server.
        /// </summary>
        /// <param name="status">The mesh server status of the server.</param>
        public void ResetMetricsForServer(MeshServerRoutingEntry status)
        {
            _routeMetrics.Remove(RouteMetricsKey(status), out _);
        }

        /// <summary>
        /// Returns the MeshServerStatus for the local machine
        /// </summary>
        /// <returns>A MeshServerStatus object</returns>
        private MeshServerRoutingEntry? GetLocalMeshServer()
        {
            return _meshMonitor.DiscoveredServers.Values
                                                 .Where(server => server.IsLocalServer)
                                                 .FirstOrDefault();
        }

        /// <summary>
        /// <para>
        /// Initially a request is sent via a path (eg v1/vision/custom/model-name) which gets to us
        /// as a path suffix (vision/custom/model-name). We need to know what route to use for this
        /// request, and in this case it is vision/custom/. However, there could be another route
        /// "vision/custom/list", which is not the route we want. 
        /// </para>
        /// <para>
        /// To handle this we find the route from in all routes in all active discovered servers
        /// which is contained in our path suffix. Our path suffix is vision/custom/model-name and
        /// the longest route that is contained in that is vision/custom/ (and not vision/custom/list).
        /// Of course in this case, if someone has a custom model called "list" then we're going to 
        /// have a problem.
        /// </para>
        /// <para>
        /// Once we have the longest matching route, we can then use that to get the  the servers 
        /// that have that route enabled.
        /// </para>
        /// </summary>
        /// <param name="pathSuffix">The path for this request without the "v1". This will be in the
        /// form "category/module[/command]". eg "image/alpr" or "vision/custom/modelName".</param>
        /// <returns>A string</returns>
        private string GetRouteFromPathSuffix(string pathSuffix)
        {
            string longestMatchingRoute = string.Empty;

            var routes = _meshMonitor.DiscoveredServers
                                     .Values
                                     .SelectMany(x => x.Status?.EnabledRoutes ?? Enumerable.Empty<string>())
                                     .Distinct();

            foreach (string candidateRoute in routes)
            {
                if (candidateRoute.Length > longestMatchingRoute.Length &&
                    pathSuffix.StartsWithIgnoreCase(candidateRoute))
                    longestMatchingRoute = candidateRoute;
            }

            return longestMatchingRoute;
        }

        private RouteMetricsCollection GetMetricsCollection(MeshServerRoutingEntry status)
        {
            return _routeMetrics.GetOrAdd(RouteMetricsKey(status), _ => new RouteMetricsCollection());
        }

        /// <summary>
        /// Returns a string to be used as the key for storing RouteMetricsCollection objects in the
        /// _routeMetrics dictionary.
        /// </summary>
        /// <param name="server">The MeshServerStatus whose RouteMetricsCollection will be stored.</param>
        /// <returns>A string</returns>
        private string RouteMetricsKey(MeshServerRoutingEntry server)
        {
            return server.CallableHostname;
        }

        /// <summary>
        /// This task periodically updates the timing metrics of each route of each server. A key
        /// purpose here is to look for routes that have had no activity in order to gradually reset
        /// their timing metrics to the point where they will be considered as part of the mesh
        /// again. The scenario here is that a route times out, and so is not chosen. The route
        /// recovers, but it's never used because its timing metric says it's too slow. By slowly
        /// resetting the metrics we bring it down to a point where it can have another crack at
        /// serving requests. 
        /// </summary> 
        private async Task UpdateRouteMetricsLoop()
        {
            while (true)
            {
                await Task.Delay(_meshOptions.CurrentValue.UpdateTimingMetricsInterval);
                if (!_meshMonitor.IsRunning)
                    break;

                foreach (RouteMetricsCollection metricsCollection in _routeMetrics.Values)
                {
                    foreach (RouteMetrics routeMetrics in metricsCollection.RouteMetrics)
                    {
                        routeMetrics.Update(_meshOptions.CurrentValue.RouteInactivityTimeout);
                    }
                }
            }
        }
       
        private async Task SendHeartbeat(ModuleConfig config)
        {
            await _meshMonitor.SendHeartbeatAsync();
        }

        internal Task<bool> EnableMeshAsync(bool enable)
        {
            MeshOptions options = _meshOptions.CurrentValue;
            options.Enable = enable;

            return UpdateMeshConfigSettings(options, true);
        }

        internal Task<bool> EnableBroadcastAsync(bool enable)
        {
            MeshOptions options = _meshOptions.CurrentValue;
            options.EnableStatusBroadcast = enable;

            return UpdateMeshConfigSettings(options, true);
        }

        internal Task<bool> EnableMonitoringAsync(bool enable)
        {
            MeshOptions options = _meshOptions.CurrentValue;
            options.EnableStatusMonitoring = enable;

            return UpdateMeshConfigSettings(options, true);
        }

        internal Task<bool> AllowForwardingRequestsAsync(bool allowForwarding)
        {
            MeshOptions options = _meshOptions.CurrentValue;
            options.AllowRequestForwarding = allowForwarding;

            return UpdateMeshConfigSettings(options);
        }

        internal Task<bool> AcceptForwardedRequestsAsync(bool state)
        {
            MeshOptions options = _meshOptions.CurrentValue;
            options.AcceptForwardedRequests = state;

            return UpdateMeshConfigSettings(options);
        }

        /// <summary>
        /// Updates the current options.
        /// </summary>
        /// <param name="options">The mesh options. Specifically, the mesh options that have been
        /// retrieved from _meshOptions.CurrentValue.</param>
        /// <param name="forceRestart">Whether or not to force the mesh monitor to restart.</param>
        /// <returns>A bool Task</returns>
        private async Task<bool> UpdateMeshConfigSettings(MeshOptions options, bool forceRestart = false)
        {
            if (SystemInfo.IsLinux)
            {
                // We need to manually kick the update for Linux since we're not watching for file
                // changes.
                await _meshMonitor.UpdateOptions(options, forceRestart);
            }

            return await _optionsJsonWriter.SaveSettingsAsync(options);
        }
    }
}
