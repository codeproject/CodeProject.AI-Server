using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.AnalysisLayer.SDK;
using CodeProject.AI.API.Server.Backend;
using CodeProject.AI.API.Common;

namespace CodeProject.AI.API.Server.Frontend.Controllers
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
        /// <param name="executionProvider">The excution provider, typically the GPU library in use</param>
        /// <param name="token">The aborted request token.</param>
        /// <returns>The Request Object.</returns>
        [HttpGet("{name}", Name = "GetRequestFromQueue")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<OkObjectResult> GetQueue([FromRoute] string name,
                                                   [FromQuery] string moduleId,
                                                   [FromQuery] string? executionProvider,
                                                   CancellationToken token)
        {
            UpdateProcessStatus(moduleId, executionProvider: executionProvider);

            BackendRequestBase? response = await _queueService.DequeueRequestAsync(name, token);
            return new OkObjectResult(response);
        }

        /// <summary>
        /// Sets the response that will be sent back to the caller of the API, for a command for
        /// the named queue if available.
        /// </summary>
        /// <param name="reqid">The id of the request the response is for.</param>
        /// <param name="moduleId">The ID of the module making the request</param>
        /// <param name="executionProvider">The hardware accelerator execution provider.</param>
        /// <returns>The Request Object.</returns>
        [HttpPost("{reqid}", Name = "SetResponseInQueue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ObjectResult> SetResponse(string reqid, [FromQuery] string moduleId,
                                                    [FromQuery] string? executionProvider = null)
        {
            string? response     = null;
            using var bodyStream = HttpContext.Request.Body;
            if (bodyStream      != null)
            {
                using var textreader = new StreamReader(bodyStream);
                response = await textreader.ReadToEndAsync();
            }

            UpdateProcessStatus(moduleId, true, executionProvider: executionProvider);

            var success = _queueService.SetResult(reqid, response);
            if (!success)
                return BadRequest("failure to set response.");

            return Ok("Response saved.");
        }

        private void UpdateProcessStatus(string moduleId, bool incrementProcessCount = false,
                                         string? executionProvider = null)
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
                ProcessStatus status = backend.ProcessStatuses[moduleId];
                status.Status   = ProcessStatusType.Started;
                status.LastSeen = DateTime.UtcNow;

                if (incrementProcessCount)
                    status.IncrementProcessedCount();

                if (string.IsNullOrWhiteSpace(executionProvider))
                {
                    status.HardwareType      = "CPU";
                    status.ExecutionProvider = string.Empty;
                }
                // Note that executionProvider will be "CPU" if not using a GPU enabled OnnxRuntime 
                //  or the GPU for the runtime is not available.
                else if (string.Compare(executionProvider, "CPU", true) == 0)
                {
                    status.HardwareType      = "CPU";
                    status.ExecutionProvider = string.Empty;
                }
                else
                {
                    status.HardwareType      = "GPU";
                    status.ExecutionProvider = executionProvider;
                }
            }
        }
    }
}
