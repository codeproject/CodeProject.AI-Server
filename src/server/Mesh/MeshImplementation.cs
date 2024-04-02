using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;
using CodeProject.AI.Server.Modules;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

// -------------------------------------------------------------------------------------------------
// This file implements the BaseMeshMonitor class and the IMeshServerBroadcastBuilder interface.
// -------------------------------------------------------------------------------------------------

namespace CodeProject.AI.Server.Mesh
{
    /// <summary>
    /// The Mesh for the Servers.
    /// </summary>
    public class MeshMonitor : BaseMeshMonitor<MeshServerBroadcastData>
    {
        /// <summary>
        /// Creates a new instance of the Mesh class.
        /// </summary>
        /// <param name="meshConfig">The Mesh Options</param>
        /// <param name="statusBuilder">The mesh node status builder.</param>
        /// <param name="logger">A logger.</param>
        public MeshMonitor(IOptionsMonitor<MeshOptions> meshConfig,
                           MeshServerBroadcastBuilder statusBuilder,
                           ILogger<MeshMonitor> logger)
            : base(meshConfig, statusBuilder, logger)
        {
        }
    }

    /// <summary>
    /// Gets the nodes status to send to the Mesh.
    /// </summary>
    /// <remarks>
    /// The 'Build' method is abstracted using an interface to allow easier mocking and testing
    /// </remarks>
    public class MeshServerBroadcastBuilder : IMeshServerBroadcastBuilder<MeshServerBroadcastData>
    {
        private readonly ModuleCollection             _modules;
        private readonly ModuleProcessServices        _processServices;
        private readonly IOptionsMonitor<MeshOptions> _monitoredMeshOptions;

        /// <summary>
        /// Initializes a new instance of the MeshNodeStatusBuilder class.
        /// </summary>
        /// <param name="modules">The installed modules.</param>
        /// <param name="monitoredMeshOptions">The MeshOptions monitoring object.</param>
        /// <param name="processServices">The Module Process Services.</param>
        public MeshServerBroadcastBuilder(IOptions<ModuleCollection> modules,
                                          IOptionsMonitor<MeshOptions> monitoredMeshOptions, // TODO: handle config changes
                                          ModuleProcessServices processServices)
        {
            _modules              = modules.Value;
            _processServices      = processServices;
            _monitoredMeshOptions = monitoredMeshOptions;
        }

        /// <summary>
        /// Build the current node's broadcast package for mesh availability announcements
        /// </summary>
        /// <returns>A <see cref="MeshServerBroadcastData"/> object</returns>
        /// <remarks>
        /// REVIEW: This has now served it's purpose and is to be moved as a simple function into
        ///         MeshMonitor
        ///</remarks>
        public MeshServerBroadcastData Build(BaseMeshMonitor<MeshServerBroadcastData> meshMonitor)
        {
            // Get the running modules
            IOrderedEnumerable<string?> runningModulesIds = _processServices
                                                               .ListProcessStatuses()
                                                               .Where(p => p.Status == ProcessStatusType.Started)
                                                               .Select(p => p.ModuleId)
                                                               .Distinct()
                                                               .OrderBy(m => m);
            IEnumerable<ModuleConfig> runningModules = _modules.Values
                                                               .Where(m => runningModulesIds.Contains(m.ModuleId));

            // Get their routes
            List<string> enabledRoutes;
            enabledRoutes     = runningModules
                                .SelectMany(m => m.RouteMaps.Where(r => r.MeshEnabled ?? true)
                                                            .Select(r => r.Route))
                                .Distinct()
                                .OrderBy(s => s)
                                .ToList();

            // Hostnames of known servers in the mesh
            var knownHostnames = meshMonitor.DiscoveredServers.Values
                                                              .Where(s => !s.Status.Hostname.EqualsIgnoreCase(meshMonitor.LocalHostname))
                                                              .Select(s => s.Status.Hostname);

            // Description and platform
            string systemDescription = $"{SystemInfo.SystemName} ({SystemInfo.OSAndArchitecture})";
            if (SystemInfo.GPU is not null)
            {
                if (SystemInfo.GPU.HardwareVendor == "Apple" || 
                    SystemInfo.GPU.HardwareVendor == "NVIDIA" ||
                    SystemInfo.GPU.HardwareVendor == "Intel")
                {
                    systemDescription += " " + (SystemInfo.GPU.Name ?? "GPU");
                }
            }

            string platform = SystemInfo.Platform;
            if (SystemInfo.IsDocker)
                platform = "Docker";

            // For other settings
            MeshOptions meshOptions = _monitoredMeshOptions.CurrentValue;

            // Now build.
            return new MeshServerBroadcastData
            {
                Hostname                = SystemInfo.MachineName,
                SystemDescription       = systemDescription,
                Platform                = platform,
                EnabledRoutes           = enabledRoutes,
                KnownHostnames          = knownHostnames,
                IsBroadcasting          = meshOptions.EnableStatusBroadcast,
                IsMonitoring            = meshOptions.EnableStatusMonitoring,
                AcceptForwardedRequests = meshOptions.AcceptForwardedRequests,
                AllowRequestForwarding  = meshOptions.AllowRequestForwarding
            };
        }
    }
}
