using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK.API;


namespace CodeProject.AI.Server.Controllers
{
    /// <summary>
    /// For status updates on the server itself.
    /// </summary>
    [Route("v1/log")]
    [ApiController]
    public class LogController : ControllerBase
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">The logger</param>
        public LogController(ILogger<LogController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Manages requests to log or retrieve logs. A PUT request.
        /// </summary>
        /// <returns>A Response Object.</returns>
        [HttpPost("", Name = "Add Log Entry")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ServerResponse AddLog([FromForm] string? entry,
                                     [FromForm] string? category,
                                     [FromForm] string? label,
                                     [FromForm] LogLevel? log_level)
        {
            if (entry == null)
                return new ServerErrorResponse("No log entry provided");

            // We're using the .NET logger which means we don't have a huge amount of control
            // when it comes to adding extra info. We'll encode category and label info in the
            // leg message itself using special markers: [[...]] for category, {{..}} for label

            string msg = string.Empty;
            if (!string.IsNullOrWhiteSpace(category))
                msg += "[[" + category + "]]";
            if (!string.IsNullOrWhiteSpace(label))
                msg += "{{" + label + "}}";

            // strip out any terminal colourisation
            entry = Regex.Replace(entry, "\\[\\d+(;\\d+)\\d+m", string.Empty);

            msg += entry;

            switch (log_level)
            {
                case LogLevel.None:         break;
                case LogLevel.Trace:       _logger.LogTrace(msg);       break;
                case LogLevel.Debug:       _logger.LogDebug(msg);       break;
                case LogLevel.Information: _logger.LogInformation(msg); break;
                case LogLevel.Warning:     _logger.LogWarning(msg);     break;
                case LogLevel.Error:       _logger.LogError(msg);       break;
                case LogLevel.Critical:    _logger.LogCritical(msg);    break;
                default:                   _logger.LogInformation(msg); break;
            }

            return new ServerResponse
            {
                Success = true,
            };
        }

        /// <summary>
        /// Returns a list of log entries. A GET request.
        /// </summary>
        /// <param name="last_id">Return all items with an id greater than this id.</param>
        /// <param name="count">The number of log items to return. Defaults to 10</param>
        /// <returns>A list of log items.</returns>
        /// <response code="200">Returns the list of detected object information, if any.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        [HttpGet("list", Name = "ListLogs")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ServerResponse ListLogs(int? last_id, int? count)
        {
            if (last_id is null)
                last_id = 0;

            if (count is null)
                count = 10;

            List<LogEntry> entries = ServerLogger.List(last_id.Value, count.Value);
            var response = new LogListResponse()
            {
                Entries = entries.ToArray()!
            };

            return response;
        }
    }
}
