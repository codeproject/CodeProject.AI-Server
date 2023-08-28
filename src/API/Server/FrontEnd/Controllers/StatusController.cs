using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using CodeProject.AI.API.Common;
using CodeProject.AI.SDK.Common;

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
        private readonly ServerOptions         _serverOptions;
        private readonly ModuleProcessServices _moduleProcessService;
        private readonly ModuleCollection      _moduleCollection;

        /// <summary>
        /// Initializes a new instance of the StatusController class.
        /// </summary>
        /// <param name="versionService">The Version instance.</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="serverOptions">The server options</param>
        /// <param name="moduleProcessService">The Module Process Services.</param>
        /// <param name="moduleCollection">The Module Collection.</param>
        public StatusController(ServerVersionService versionService,
                                ModuleSettings moduleSettings,
                                IOptions<ServerOptions> serverOptions,
                                ModuleProcessServices moduleProcessService,
                                IOptions<ModuleCollection> moduleCollection)
        {
            _versionService       = versionService;
            _moduleSettings       = moduleSettings;
            _serverOptions        = serverOptions.Value;
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

            systemStatus.AppendLine(gpuInfo);
            systemStatus.AppendLine();
            systemStatus.AppendLine();

            systemStatus.AppendLine(gpuVideoInfo);
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
                CpuUsage       = cpuUsage,
                SystemMemUsage = systemMemUsage,
                GpuUsage       = gpuUsage,
                GpuMemUsage    = gpuMemUsage,
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
            VersionInfo? latest = await _versionService.GetLatestVersion().ConfigureAwait(false);
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
                ModuleConfig? module = string.IsNullOrEmpty(process.ModuleId) ? null
                                     : _moduleCollection.GetModule(process.ModuleId);

                if (module is not null)
                {
                    process.StartupSummary = module.SettingsSummary ?? string.Empty;
                    if (string.IsNullOrEmpty(process.StartupSummary))
                    {
                        Console.WriteLine($"Unable to find module for {process.ModuleId}");
                    }
                    else
                    {
                        // Expanding out the macros causes the display to be too wide
                        // process.StartupSummary = _moduleSettings.ExpandOption(process.StartupSummary,
                        //                                                       module.ModulePath);
                        string appRoot = _serverOptions.ApplicationRootPath!;
                        process.StartupSummary = process.StartupSummary.Replace(appRoot, "&lt;root&gt;");
                    }
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
