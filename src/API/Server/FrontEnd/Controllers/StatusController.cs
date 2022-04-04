using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeProject.SenseAI.API.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
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
        public VersionService VersionService { get; }

        /// <summary>
        /// Initializes a new instance of the StatusController class.
        /// </summary>
        /// <param name="versionService">The Version instance.</param>
        public StatusController(VersionService versionService)
        {
            VersionService = versionService;
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
                message = VersionService.VersionConfig.VersionInfo?.Version ?? string.Empty,
                version = VersionService.VersionConfig.VersionInfo,
                success = true
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
            VersionInfo? latest = await VersionService.GetLatestVersion();
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
                VersionInfo? current = VersionService.VersionConfig.VersionInfo;
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
                    version         = latest,
                    updateAvailable = updateAvailable
                };
            }
        }

        /// <summary>
        /// Allows for a client to list the status of the backend analysis services.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("analysis/list", Name = "ListAnalysisStatus")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase ListAnalysisStatus()
        {
            // Get the backend processor (DI won't work here due to the order things get fired up
            // in Main.
            var backend = HttpContext.RequestServices.GetServices<IHostedService>()
                                                     .OfType<BackendProcessRunner>()
                                                     .FirstOrDefault();
            if (backend is null)
                return new ErrorResponse("Unable to locate backend services");

            // List them out and return the status
            var response = new AnalysisServicesStatusResponse
            {
                statuses = backend.ProcessStatuses.ToArray()
            };

            return response;
        }
    }
}
