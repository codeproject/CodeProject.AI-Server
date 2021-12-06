using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        /// <summary>
        /// Initializes a new instance of the StatusController class.
        /// </summary>
        public StatusController()
        {
        }

        /// <summary>
        /// Allows for a client to ping the service to test for aliveness.
        /// </summary>
        /// <returns>The Request Object.</returns>
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
    }
}
