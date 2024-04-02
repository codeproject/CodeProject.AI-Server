using System.Linq;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.Server.Modules;
using CodeProject.AI.SDK.API;
using System;

namespace CodeProject.AI.Server.Controllers
{
    /// <summary>
    /// For status updates on the server itself.
    /// </summary>
    /// <remarks>
    /// In the HttpGet attribute for the methods we've commented out the Name parameter for the 
    /// moment in order to allow us to have two routes to the same methods. /status was our old 
    /// route, and /server is the new route. This will only cause issues if we need to use the 
    /// RouteName (eg in route generation).
    /// </remarks>
    [Route("v1/status")]         // legacy route
    [Route("v1/server/status")]  // new route as of 2.4.0
    [ApiController]
    public class ServerStatusController : ControllerBase
    {
        /// <summary>
        /// Gets the version service instance.
        /// </summary>
        private readonly ServerVersionService  _versionService;
        private readonly ModuleSettings        _moduleSettings;
        private readonly ModuleProcessServices _moduleProcessService;

        /// <summary>
        /// Initializes a new instance of the ServerController class.
        /// </summary>
        /// <param name="versionService">The Version instance.</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="moduleProcessService">The Module Process Services.</param>
        public ServerStatusController(ServerVersionService versionService,
                                      ModuleSettings moduleSettings,
                                      ModuleProcessServices moduleProcessService)
        {
            _versionService       = versionService;
            _moduleSettings       = moduleSettings;
            _moduleProcessService = moduleProcessService;
        }

        /// <summary>
        /// Allows for a client to ping the service to test for aliveness.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("ping"/*, Name = "Ping"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ServerResponse GetPing()
        {
            /* This is a simple and sensible response. But let's do better
            var response = new ResponseBase
            {
                success = true,
            };
            return response;
            */

            return GetVersion();
        }

        /// <summary>
        /// Allows for a client to retrieve the current API server version.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("version"/*, Name = "Version"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ServerResponse GetVersion()
        {
            var response = new VersionResponse
            {
                Message = _versionService.VersionConfig.VersionInfo?.Version ?? string.Empty,
                Version = _versionService.VersionConfig.VersionInfo,
                Success = true
            };

            return response;
        }

        /// <summary>
        /// Allows for a client to retrieve the current system status (GPU info mainly)
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("system-status"/*, Name = "System Status"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<object> GetSystemStatus()
        {
            var serverVersion     = _versionService.VersionConfig?.VersionInfo?.Version ?? string.Empty;

            // Run these in parallel as they have a Task.Delay(1000) in them.
            string gpuInfo        = await SystemInfo.GetGpuUsageInfoAsync().ConfigureAwait(false);
            int    gpuUsage       = await SystemInfo.GetGpuUsageAsync().ConfigureAwait(false);
            string gpuVideoInfo   = await SystemInfo.GetVideoAdapterInfoAsync().ConfigureAwait(false);
            ulong  gpuMemUsage    = await SystemInfo.GetGpuMemoryUsageAsync().ConfigureAwait(false);
            int    cpuUsage       = SystemInfo.GetCpuUsage();
            ulong  systemMemUsage = SystemInfo.GetSystemMemoryUsage();

            var systemStatus = new StringBuilder();
            systemStatus.AppendLine($"Server version:   {serverVersion}");
            systemStatus.AppendLine(SystemInfo.GetSystemInfo());
            systemStatus.AppendLine();
            systemStatus.AppendLine();

            systemStatus.AppendLine(gpuVideoInfo);
            systemStatus.AppendLine();
            systemStatus.AppendLine();

            systemStatus.AppendLine(gpuInfo);
            systemStatus.AppendLine();
            systemStatus.AppendLine();

            var environmentVariables = _moduleProcessService?.GlobalEnvironmentVariables;
            if (environmentVariables is not null)
            {
                systemStatus.AppendLine();
                systemStatus.AppendLine("Global Environment variables:");
                int maxLength = environmentVariables.Max(x => x.Key.ToString().Length);
                foreach (var envVar in environmentVariables)
                {
                    string? value = envVar.Value?.ToString() ?? string.Empty;
                    value = _moduleSettings.ExpandOption(value, null);

                    // Let's hide personal info for those pasting this info publicly
                    string appRoot = CodeProject.AI.Server.Program.ApplicationRootPath!;
                    value = value?.Replace(appRoot, "&lt;root&gt;");

                    systemStatus.AppendLine($"  {envVar.Key.PadRight(maxLength)} = {value}");
                }
            }

            return new ServerDataResponse() 
            {
                Data = new
                {
                    CpuUsage       = cpuUsage,
                    SystemMemUsage = systemMemUsage,
                    GpuUsage       = gpuUsage,
                    GpuMemUsage    = gpuMemUsage,
                    ServerStatus   = systemStatus.ToString()
                }
            };
        }

        /// <summary>
        /// Allows for a client to retrieve the current Paths.
        /// </summary>
        /// <returns>An ObjectResult object.</returns>
        [HttpGet("Paths" /*, Name = "Paths" */)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ObjectResult GetPaths([FromServices] IWebHostEnvironment env)
        {
            var response = new
            {
                env.ContentRootPath,
                env.WebRootPath,
                env.EnvironmentName,
            };

            return new ObjectResult(response);
        }

        /// <summary>
        /// Allows for a client to retrieve whether an update for this API server is available.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("updateavailable" /*, Name = "UpdateAvailable"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ServerResponse> GetUpdateAvailable()
        {
            VersionInfo? latest = await _versionService.GetLatestVersion().ConfigureAwait(false);
            if (latest is null)
            {
                return new VersionUpdateResponse
                {
                    Message = "Unable to retrieve latest version",
                    Success = false
                };
            }
            else
            {
                VersionInfo? current = _versionService.VersionConfig.VersionInfo;
                if (current == null)
                {
                    return new VersionUpdateResponse
                    {
                        Message = "Unable to retrieve current version",
                        Success = false
                    };
                }

                bool updateAvailable = VersionInfo.Compare(current, latest) < 0;
                string message = updateAvailable
                               ? $"An update to version {latest.Version} is available"
                               : "This is the current version";

                return new VersionUpdateResponse
                {
                    Success         = true,
                    Message         = message,
                    Latest          = latest,
                    Current         = current,
                    Version         = latest, // To be removed
                    UpdateAvailable = updateAvailable
                };
            }
        }

        /// <summary>
        /// Allows for a client to list the status of the backend analysis modules.
        /// DEPRECATED: This call should now be made via ModuleController.
        /// via the route module/list/status
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("modules/list"/*, Name = "List Module Statuses"*/)]
        [HttpGet("analysis/list"/*, Name = "List Module Statuses"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ServerResponse ListAnalysisStatus()
        {
#if DEBUG
            throw new NotSupportedException("ServerStatusController.ListAnalysisStatus is no longer supported");
#else
            // Get the statuses
            var statuses = _moduleProcessService.ListProcessStatuses();
            var response = new ModuleStatusesResponse
            {
                Statuses = statuses
                            // .Where(module => module.Status != ProcessStatusType.NoAutoStart)
                           .ToList()
            };

            return response;
#endif
        }
    }
}
