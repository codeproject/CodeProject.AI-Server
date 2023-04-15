using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

using CodeProject.AI.API.Common;
using CodeProject.AI.SDK.Common;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// For status updates on the server itself.
    /// </summary>
    [Route("v1/status")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        /// <summary>
        /// Gets the version service instance.
        /// </summary>
        private readonly ServerVersionService  _versionService;
        private readonly ModuleSettings        _moduleSettings;
        private readonly ModuleProcessServices _moduleProcessService;
        private readonly ModuleCollection      _moduleCollection;

        /// <summary>
        /// Initializes a new instance of the StatusController class.
        /// </summary>
        /// <param name="versionService">The Version instance.</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="moduleProcessService">The Module Process Services.</param>
        /// <param name="moduleCollection">The Module Collection.</param>
        public StatusController(ServerVersionService versionService,
                                ModuleSettings moduleSettings,
                                ModuleProcessServices moduleProcessService,
                                IOptions<ModuleCollection> moduleCollection)
        {
            _versionService       = versionService;
            _moduleSettings       = moduleSettings;
            _moduleProcessService = moduleProcessService;
            _moduleCollection     = moduleCollection.Value;
        }

        /// <summary>
        /// Allows for a client to ping the service to test for aliveness.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("ping", Name = "Ping")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase GetPing()
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
        [HttpGet("version", Name = "Version")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase GetVersion()
        {
            var response = new VersionResponse
            {
                message = _versionService.VersionConfig.VersionInfo?.Version ?? string.Empty,
                version = _versionService.VersionConfig.VersionInfo,
                success = true
            };

            return response;
        }

        /// <summary>
        /// Allows for a client to retrieve the current system status (GPU imfo mainly)
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("system-status", Name = "System Status")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<object> GetSystemStatus()
        {
            // run these in parallel as they have a Task.Delay(1000) in them.
            var cpuUsageTask     = SystemInfo.GetCpuUsage();
            var gpuUsageTask     = SystemInfo.GetGpuUsage();
            var gpuInfoTask      = SystemInfo.GetGpuUsageInfo();
            var gpuVideoInfoTask = SystemInfo.GetVideoAdapterInfo();
            var serverVersion    = _versionService.VersionConfig?.VersionInfo?.Version ?? string.Empty;

            var systemStatus = new StringBuilder();
            systemStatus.AppendLine($"Server version:   {serverVersion}");
            systemStatus.AppendLine(SystemInfo.GetSystemInfo());
            systemStatus.AppendLine();
            systemStatus.AppendLine();

            systemStatus.AppendLine(await gpuInfoTask);
            systemStatus.AppendLine();
            systemStatus.AppendLine();

            systemStatus.AppendLine(await gpuVideoInfoTask);
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

                    systemStatus.AppendLine($"  {envVar.Key.PadRight(maxLength)} = {value}");
                }
            }

            var response = new
            {
                CpuUsage       = await cpuUsageTask,
                SystemMemUsage = await SystemInfo.GetSystemMemoryUsage(),
                GpuUsage       = await gpuUsageTask,
                GpuMemUsage    = await SystemInfo.GetGpuMemoryUsage(),
                ServerStatus   = systemStatus.ToString()
            };

            return response;
        }

        /// <summary>
        /// Allows for a client to retrieve the current Paths.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("Paths", Name = "Paths")]
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
        [HttpGet("updateavailable", Name = "UpdateAvailable")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> GetUpdateAvailable()
        {
            VersionInfo? latest = await _versionService.GetLatestVersion();
            if (latest is null)
            {
                return new VersionUpdateResponse
                {
                    message = "Unable to retrieve latest version",
                    success = false
                };
            }
            else
            {
                VersionInfo? current = _versionService.VersionConfig.VersionInfo;
                if (current == null)
                {
                    return new VersionUpdateResponse
                    {
                        message = "Unable to retrieve current version",
                        success = false
                    };
                }

                bool updateAvailable = VersionInfo.Compare(current, latest) < 0;
                string message = updateAvailable
                               ? $"An update to version {latest.Version} is available"
                               : "This is the current version";

                return new VersionUpdateResponse
                {
                    success         = true,
                    message         = message,
                    latest          = latest,
                    current         = current,
                    version         = latest, // To be removed
                    updateAvailable = updateAvailable
                };
            }
        }

        /// <summary>
        /// Allows for a client to list the status of the backend analysis modules.
        /// TODO: move this to modules controller, path is modules/status/list
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("analysis/list", Name = "ListAnalysisStatus")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase ListAnalysisStatus()
        {
            // COMMENTED: not possible.
            //if (_moduleRunner.ProcessStatuses is null)
            //    return new ErrorResponse("No backend analysis modules have been registered");

            foreach (ProcessStatus process in _moduleProcessService.ListProcessStatuses())
            {
                if (!string.IsNullOrEmpty(process.ModuleId))
                {
                    ModuleConfig? module   = _moduleCollection.GetModule(process.ModuleId);
                    process.StartupSummary = module?.SettingsSummary ?? string.Empty;
                    if (string.IsNullOrEmpty(process.StartupSummary))
                        Console.WriteLine($"Unable to find module for {process.ModuleId}");
                }
            }

            // ListProcessStatuses then out and return the status
            var response = new AnalysisServicesStatusResponse
            {
                statuses = _moduleProcessService.ListProcessStatuses()
                            // .Where(module => module.Status != ProcessStatusType.NotEnabled)
                           .ToList()
            };

            return response;
        }
    }
}
