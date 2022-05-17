using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeProject.SenseAI.AnalysisLayer.SDK;
using CodeProject.SenseAI.API.Server.Backend;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// Handles pulling requests from the Command Queue and returning reponses to the calling method.
    /// </summary>
    [Route("v1/queue")]
    [ApiController]
    public class QueueController : ControllerBase
    {
        private readonly QueueServices _queueService;

        /// <summary>
        /// Initializes a new instance of the QueueController class.
        /// </summary>
        /// <param name="queueService">The QueueService.</param>
        public QueueController(QueueServices queueService)
        {
            _queueService = queueService;
        }

        /// <summary>
        /// Gets a command from the named queue if available.
        /// </summary>
        /// <param name="name">The name of the Queue.</param>
        /// <param name="moduleId">The ID of the module making the request</param>
        /// <param name="token">The aborted request token.</param>
        /// <returns>The Request Object.</returns>
        [HttpGet("{name}", Name = "GetRequestFromQueue")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<OkObjectResult> GetQueue([FromRoute] string name, [FromQuery] string moduleId,
                                                    CancellationToken token)
        {
            // TODO: Get the id of the module that made this request from the 'moduleId' querystring
            // parameter and store the current time in a map of {moduleId : report_time} for health
            // reporting.
            // string? moduleId = Request.QueryString.Value["moduleId"];
            UpdateProcessStatus(moduleId);

            BackendRequestBase? response = await _queueService.DequeueRequestAsync(name, token);
            return new OkObjectResult(response);
        }

        /// <summary>
        /// Sets the response for a command from the named queue if available.
        /// </summary>
        /// <param name="reqid">The id of the request the response is for.</param>
        /// <param name="moduleId">The ID of the module making the request</param>
        /// <returns>The Request Object.</returns>
        [HttpPost("{reqid}", Name = "SetResponseInQueue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ObjectResult> SetResponse(string reqid, [FromQuery] string moduleId)
        {
            string? response     = null;
            using var bodyStream = HttpContext.Request.Body;
            if (bodyStream      != null)
            {
                using var textreader = new StreamReader(bodyStream);
                response = await textreader.ReadToEndAsync();
            }

            UpdateProcessStatus(moduleId, true);

            var success = _queueService.SetResult(reqid, response);
            if (!success)
                return BadRequest("failure to set response.");
            else
                return Ok("Response saved.");
        }

        private void UpdateProcessStatus(string moduleId, bool incrementProcessCount = false)
        {
            if (string.IsNullOrEmpty(moduleId))
                return;

            // Get the backend processor (DI won't work here due to the order things get fired up
            // in Main.
            var backend = HttpContext.RequestServices.GetServices<IHostedService>()
                                                     .OfType<BackendProcessRunner>()
                                                     .FirstOrDefault();
            if (backend is null)
                return;

            if (backend.StartupProcesses.ContainsKey(moduleId))
            {
                backend.StartupProcesses[moduleId].LastSeen = DateTime.UtcNow;
                if (incrementProcessCount)
                    backend.StartupProcesses[moduleId].Processed++;
            }
        }
    }
}
