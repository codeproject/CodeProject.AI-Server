using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;
using CodeProject.AI.Server.Backend;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Modules;

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
        private readonly ILogger               _logger;

        /// <summary>
        /// Initializes a new instance of the QueueController class.
        /// </summary>
        /// <param name="queueService">The QueueService.</param>
        /// <param name="moduleProcessService">The Module Process Service.</param>
        /// <param name="logger">The logger</param>
        public QueueController(QueueServices queueService,
                               ModuleProcessServices moduleProcessService,
                               ILogger<LogController> logger)
        {
            _queueService         = queueService;
            _moduleProcessService = moduleProcessService;
            _logger               = logger;
        }

        /// <summary>
        /// Gets a command from the named queue if available.
        /// </summary>
        /// <param name="name">The name of the Queue.</param>
        /// <param name="moduleId">The ID of the module making the request</param>
        /// <param name="executionProvider">The execution provider, typically the GPU library in 
        /// use. To be removed at server v2.6</param>
        /// <param name="canUseGPU">Whether or not the module can use the current GPU. To be removed
        /// at server v2.6</param>
        /// <param name="token">The aborted request token.</param>
        /// <returns>The Request Object.</returns>
        /// <remarks>
        /// HACK: executionProvider and canUseGPU are passed in by old pre server v2.5.2 modules
        /// and are to be removed. For old modules this is the only way to inform that dashboard as
        /// to the GPU capabilities of the module. Note executionProvider is being passed, but it
        /// should have been hardwareType. We will convert. REMOVE at server v2.6.
        /// </remarks>
        [HttpGet("{name}", Name = "GetRequestFromQueue")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<OkObjectResult> GetQueue([FromRoute] string name,
                                                   [FromQuery] string moduleId,
                                                   [FromQuery] string? executionProvider,   // Remove at server v2.6
                                                   [FromQuery] bool? canUseGPU,             // Remove at server v2.6
                                                   CancellationToken token)
        {
            _moduleProcessService.UpdateModuleLastSeen(moduleId);
            
            BackendRequestBase? request = await _queueService.DequeueRequestAsync(name, token)
                                                             .ConfigureAwait(false);
            if (request != null)
            {
                // We're going to sniff the request to see if it's a Quit command. If so it allows us
                // to update the status of the process. If it's a quit command then the process will
                // shut down and no longer updating its status via the queue. This is our last chance.
                if (request.reqtype?.ToLower() == "quit" && request is BackendRequest origRequest)
                {
                    string? requestModuleId = origRequest.payload?.GetValue("moduleId");
                    if (moduleId.EqualsIgnoreCase(requestModuleId))
                        _moduleProcessService.AdviseProcessShutdown(moduleId);
                }
            }

            // HACK: Remove at server v2.6
            if (executionProvider is not null && canUseGPU is not null)
            {
                string inferenceDevice = executionProvider.EqualsIgnoreCase("CPU") ? "CPU" : "GPU";
                _moduleProcessService.UpdateProcessStatusData(moduleId, inferenceDevice, canUseGPU);
            }

            return new OkObjectResult(request);
        }

        /// <summary>
        /// Sets the response that will be sent back to the caller of the API, for a command for the
        /// named queue if available.
        /// </summary>
        /// <param name="reqid">The id of the request the response is for.</param>
        /// <returns>The Request Object.</returns>
        [HttpPost("{reqid}", Name = "SetResponseInQueue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ObjectResult> SetResponse(string reqid)
        {
            string? responseString = null;
            using var bodyStream = Request.Body;
            if (bodyStream != null)
            {
                using var textreader = new StreamReader(bodyStream);
                responseString = await textreader.ReadToEndAsync().ConfigureAwait(false);
            }

            JsonObject? response = JsonSerializer.Deserialize<JsonObject>(responseString ?? "");

            string? command  = response?["command"]?.ToString();
            string? moduleId = response?["moduleId"]?.ToString();

            if (!string.IsNullOrWhiteSpace(moduleId))
            {
                _moduleProcessService.UpdateModuleLastSeen(moduleId);

                if (command is not null && !command.EqualsIgnoreCase("status"))
                    _moduleProcessService.UpdateModuleProcessingCount(moduleId);

                // HACK: Modules for server v2.5.1 (only) may pass status as part of the response
                JsonObject? statusData = response?["statusData"] as JsonObject;
                if (statusData is not null) 
                    _moduleProcessService.UpdateProcessStatusData(moduleId, statusData);
            }

            var success = _queueService.SetResult(reqid, responseString);
            if (!success)
                return BadRequest("failure to set response.");

            return Ok("Response saved.");
        }

        /// <summary>
        /// Allows modules to update their status be sending StatusData, and having this be updated
        /// in the module's associated ProcessStatus object 
        /// </summary>
        /// <param name="moduleId">The id of the request the response is for.</param>
        /// <param name="statusData"></param>
        /// <returns>The Request Object.</returns>
        // REVIEW: Possible rename this to BackendController and map both /vi/queue and /v1/backend
        //         to maintain backwards compatibility for the modules.
        [HttpPost("updatemodulestatus/{moduleId}", Name = "UpdateModuleStatusData")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ObjectResult UpdateModuleStatusData(string moduleId, [FromForm] string? statusData)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return NotFound("No Module specified");

            _moduleProcessService.UpdateModuleLastSeen(moduleId);

            bool statusUpdated = false;
            if (!string.IsNullOrEmpty(statusData))
            {
                JsonObject? statusObject = null;
                if (!string.IsNullOrWhiteSpace(statusData))
                    statusObject = JsonSerializer.Deserialize<JsonObject>(statusData ?? "");
        
                if (statusObject is not null)
                    statusUpdated = _moduleProcessService.UpdateProcessStatusData(moduleId, statusObject);
            }

            return statusUpdated? Ok("Module status updated") : NotFound("Module status data not updated");
        }
    }
}
