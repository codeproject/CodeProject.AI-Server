using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CodeProject.AI.API.Common;
using System;

namespace CodeProject.AI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// For status updates on the server itself.
    /// </summary>
    [Route("v1/log")]
    [ApiController]
    public class LogController : ControllerBase
    {
        /// <summary>
        /// Manages requests to log or retrieve logs. A PUT request.
        /// </summary>
        /// <returns>A Response Object.</returns>
        [HttpPost("", Name = "Add Log Entry")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase AddLog([FromForm] string? entry)
        {
            if (entry == null)
                return new ErrorResponse("No log entry provided");

            Logger.Log(entry);

            return new ResponseBase
            {
                success = true,
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
        public ResponseBase ListLogs(int? last_id, int? count)
        {
            if (last_id is null)
                last_id = 0;

            if (count is null)
                count = 10;

            List<LogEntry> entries = Logger.List(last_id.Value, count.Value);
            var response = new LogListResponse()
            {
                entries = entries.ToArray()
            };

            return response;
        }
    }
}
