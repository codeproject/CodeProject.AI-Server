using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using CodeProject.SenseAI.API.Common;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// For status updates on the server itself.
    /// </summary>
    [Route("v1/status")]
    [ApiController]
    public class StatusController : ControllerBase
    {
        private readonly IOptions<VersionInfo> _options;

        /// <summary>
        /// Initializes a new instance of the StatusController class.
        /// </summary>
        /// <param name="options">The Options instance.</param>
        public StatusController(IOptions<VersionInfo> options)
        {
            _options = options;
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
            var response = new ResponseBase
            {
                success = true,
            };

            return response;
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
            var response = new ResponseBase
            {
                message = _options.Value.Version,
                success = true,
            };

            return response;
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

            // Get the list of processes that could be run
            var startupProcesses = backend.StartupProcesses;

            // List them out and return the status
            var response = new AnalysisServicesStatusResponse();
            List<KeyValuePair<string, bool>> statuses = new();

            if (startupProcesses is not null)
            {
                foreach (var cmd in startupProcesses)
                {
                    statuses.Add(new KeyValuePair<string, bool>(cmd.Name ?? "Unknown", 
                                                                cmd.Running ?? false));
                }

                response.statuses = statuses.ToArray();
            }

            return response;
        }
    }
}
