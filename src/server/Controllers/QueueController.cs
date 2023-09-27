using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CodeProject.AI.SDK;
using CodeProject.AI.Server.Backend;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Modules;
using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.Server.Controllers
{
    /// <summary>
    /// Handles pulling requests from the Command Queue and returning responses to the calling method.
    /// </summary>
    [Route("v1/queue")]
    [ApiController]
    public class QueueController : ControllerBase
    {
        private readonly QueueServices         _queueService;
        private readonly ModuleProcessServices _moduleProcessService;

        /// <summary>
        /// Initializes a new instance of the QueueController class.
        /// </summary>
        /// <param name="queueService">The QueueService.</param>
        /// <param name="moduleProcessService">The Module Process Service.</param>
        public QueueController(QueueServices queueService,
                               ModuleProcessServices moduleProcessService)
        {
            _queueService         = queueService;
            _moduleProcessService = moduleProcessService;
        }

        /// <summary>
        /// Gets a command from the named queue if available.
        /// </summary>
        /// <param name="name">The name of the Queue.</param>
        /// <param name="moduleId">The ID of the module making the request</param>
        /// <param name="executionProvider">The execution provider, typically the GPU library in use</param>
        /// <param name="canUseGPU">Whether or not the module can use the current GPU</param>
        /// <param name="token">The aborted request token.</param>
        /// <returns>The Request Object.</returns>
        [HttpGet("{name}", Name = "GetRequestFromQueue")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<OkObjectResult> GetQueue([FromRoute] string name,
                                                   [FromQuery] string moduleId,
                                                   [FromQuery] string? executionProvider,
                                                   [FromQuery] bool? canUseGPU,
                                                   CancellationToken token)
        {
            BackendRequestBase? request = await _queueService.DequeueRequestAsync(name, token)
                                                             .ConfigureAwait(false);

            bool shuttingDown = false;

            if (request != null)
            {
                // We're going to sniff the request to see if it's a Quit command. If so it allows us
                // to update the status of the process. If it's a quit command then the process will
                // shut down and no longer updating its status via the queue. This is our last chance.
                if (request.reqtype?.ToLower() == "quit" && request is BackendRequest origRequest)
                {
                    string? requestModuleId = origRequest.payload?.GetValue("moduleId");
                    shuttingDown = moduleId.EqualsIgnoreCase(requestModuleId);
                }
            }

            UpdateProcessStatus(moduleId, incrementProcessCount: false, executionProvider,
                                canUseGPU, shuttingDown);

            return new OkObjectResult(request);
        }

        /// <summary>
        /// Sets the response that will be sent back to the caller of the API, for a command for
        /// the named queue if available.
        /// </summary>
        /// <param name="reqid">The id of the request the response is for.</param>
        /// <returns>The Request Object.</returns>
        [HttpPost("{reqid}", Name = "SetResponseInQueue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ObjectResult> SetResponse(string reqid)
        {
            string? responseString = null;
            using var bodyStream = HttpContext.Request.Body;
            if (bodyStream != null)
            {
                using var textreader = new StreamReader(bodyStream);
                responseString = await textreader.ReadToEndAsync().ConfigureAwait(false);
            }

            var response = JsonSerializer.Deserialize<JsonObject>(responseString ?? "");
 
            string? command           = response?["command"]?.ToString();
            string? moduleId          = response?["moduleId"]?.ToString();
            string? executionProvider = response?["executionProvider"]?.ToString();
            bool   canUseGPU          = response?["canUseGPU"]?.ToString().EqualsIgnoreCase("true") ?? false;

            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                bool incrementProcessCount = command is not null && !command.EqualsIgnoreCase("status");
                UpdateProcessStatus(moduleId, incrementProcessCount: incrementProcessCount,
                                    executionProvider: executionProvider, canUseGPU: canUseGPU);
            }

            var success = _queueService.SetResult(reqid, responseString);
            if (!success)
                return BadRequest("failure to set response.");

            return Ok("Response saved.");
        }

        private void UpdateProcessStatus(string moduleId, bool incrementProcessCount = false,
                                         string? executionProvider = null, bool? canUseGPU = false,
                                         bool shuttingDown = false)
        {
            if (string.IsNullOrEmpty(moduleId))
                return;

            if (_moduleProcessService.TryGetProcessStatus(moduleId, out ProcessStatus? processStatus))
            {
                if (processStatus!.Status != ProcessStatusType.Stopping)
                    processStatus.Status = shuttingDown? ProcessStatusType.Stopping : ProcessStatusType.Started;

                processStatus.Started ??= DateTime.UtcNow;
                processStatus.LastSeen  = DateTime.UtcNow;

                if (incrementProcessCount)
                    processStatus.IncrementProcessedCount();

                if (string.IsNullOrWhiteSpace(executionProvider))
                {
                    processStatus.HardwareType      = "CPU";
                    processStatus.ExecutionProvider = string.Empty;
                }
                // Note that executionProvider will be "CPU" if not using a GPU enabled OnnxRuntime 
                //  or the GPU for the runtime is not available.
                else if (executionProvider.EqualsIgnoreCase("CPU"))
                {
                    processStatus.HardwareType      = "CPU";
                    processStatus.ExecutionProvider = string.Empty;
                }
                else
                {
                    processStatus.HardwareType      = "GPU";
                    processStatus.ExecutionProvider = executionProvider;
                }

                processStatus.CanUseGPU = canUseGPU;
            }
        }
    }
}
