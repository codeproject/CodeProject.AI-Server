using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.SenseAI.API.Server.Backend;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// Handles pulling requests from the Command Queue and
    /// returning reponses to the calling method.
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
        /// <param name="token">The aborted request token.</param>
        /// <returns>The Request Object.</returns>
        [HttpGet("{name}", Name = "GetRequestFromQueue")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<OkObjectResult> GetQueue(string name, CancellationToken token)
        {
            var response = await _queueService.DequeueRequestAsync(name, token);
            return new OkObjectResult(response);
        }

        /// <summary>
        /// Sets the response for a command from the named queue if available.
        /// </summary>
        /// <param name="reqid">The id of the request the response is for.</param>
        /// <returns>The Request Object.</returns>
        [HttpPost("{reqid}", Name = "SetResponseInQueue")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ObjectResult> SetResponse(string reqid)
        {
            string? response     = null;
            using var bodyStream = HttpContext.Request.Body;
            if (bodyStream      != null)
            {
                using var textreader = new StreamReader(bodyStream);
                response = await textreader.ReadToEndAsync();
            }

            var success = _queueService.SetResult(reqid, response);
            if (!success)
                return BadRequest("failure to set response.");
            else
                return Ok("Response saved.");
        }
    }
}
