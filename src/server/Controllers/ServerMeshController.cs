using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using CodeProject.AI.Server.Mesh;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server.Controllers
{
    /// <summary>
    /// For managing the Mesh settings for the current server.
    /// </summary>
    [Route("v1/server/mesh")]
    [ApiController]
    public class ServerMeshController : ControllerBase
    {
        private readonly MeshManager _meshManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerMeshController"/> class.
        /// </summary>
        /// <param name="meshManager">The Mesh Manager.</param>
        public ServerMeshController(MeshManager meshManager)
        {
            _meshManager = meshManager;
        }

        /// <summary>
        /// Provides the means to update mesh settings via name/value.
        /// </summary>
        /// <returns>An IActionResult Object.</returns>
        [HttpPost("setting" /*, Name = "UpsertSetting"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpsertSettingAsync([FromForm] string name, 
                                                            [FromForm] string value)
        {
            bool success = false;

            if (name.EqualsIgnoreCase("Enable"))
            {
                if (bool.TryParse(value, out bool enable))
                    success = await _meshManager.EnableMeshAsync(enable);
            }
            else if (name.EqualsIgnoreCase("EnableBroadcast"))
            {
                if (bool.TryParse(value, out bool enable))
                    success = await _meshManager.EnableBroadcastAsync(enable);
            }
            else if (name.EqualsIgnoreCase("EnableMonitoring"))
            {
                if (bool.TryParse(value, out bool enable))
                    success = await _meshManager.EnableMonitoringAsync(enable);
            }
            else if (name.EqualsIgnoreCase("AllowForwarding"))
            {
                if (bool.TryParse(value, out bool allowForwarding))
                    success = await _meshManager.AllowForwardingRequestsAsync(allowForwarding);
            }
            else if (name.EqualsIgnoreCase("AcceptForwarded"))
            {
                if (bool.TryParse(value, out bool acceptForwarded))
                    success = await _meshManager.AcceptForwardedRequestsAsync(acceptForwarded);
            }

            return GetActionResult(success);
        }

        /// <summary>
        /// Enables or Disables Mesh status broadcasting.
        /// </summary>
        /// <returns>an OK response.</returns>
        [HttpPost("EnableBroadcast", Name = "EnableBroadcast")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EnableBroadcast([FromForm] bool state)
        {
            return GetActionResult(await _meshManager.EnableBroadcastAsync(state));
        }

        /// <summary>
        /// Enables or Disables Mesh status monitoring.
        /// </summary>
        /// <returns>an OK response.</returns>
        [HttpPost("EnableMonitoring", Name = "EnableMonitoring")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EnableMonitoring([FromForm] bool state)
        {
            return GetActionResult(await _meshManager.EnableMonitoringAsync(state));
        }

        /// <summary>
        /// Allows or disallows request forwarding.
        /// </summary>
        /// <returns>an OK response.</returns>
        [HttpPost("AllowForwarding", Name = "AllowForwarding")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AllowForwarding([FromForm] bool allowForwarding)
        {
            return GetActionResult(await _meshManager.AllowForwardingRequestsAsync(allowForwarding));
        }

        /// <summary>
        /// Sets whether or not this server will accept forwarded requests
        /// </summary>
        /// <returns>an OK response.</returns>
        [HttpPost("AcceptForwarded", Name = "AcceptForwarded")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AcceptForwarded([FromForm] bool acceptForwarded)
        {
            return GetActionResult(await _meshManager.AcceptForwardedRequestsAsync(acceptForwarded));
        }

        /// <summary>
        /// Registers a hostname and adds it to the list of known servers.
        /// </summary>
        /// <returns>A summary of the mesh server's information.</returns>
        [HttpPost("register/{hostname}", Name = "Register Server")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult RegisterServer(string hostname)
        {
            return GetActionResult(_meshManager.RegisterServer(hostname));
        }

        /// <summary>
        /// Gets a summary of the Mesh Server Info.
        /// </summary>
        /// <returns>A summary of the mesh server's information.</returns>
        [HttpGet("summary", Name = "MeshInfo")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetMeshMonitoringInfo()
        {
            MeshSummary info = _meshManager.GetMeshMonitoringInfo();
            return new JsonResult(info);
        }

        /// <summary>
        /// Gets the status of a Mesh Server that other servers can use for routing purposes.
        /// </summary>
        /// <returns>Status information.</returns>
        [HttpGet("status", Name = "MeshStatus")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult GetMeshServerBroadcastData()
        {
            MeshServerBroadcastData status = _meshManager.GetMeshServerBroadcastData();
            return new JsonResult(status);
        }

        private IActionResult GetActionResult(bool ok)
        {
            if (ok)
                return Ok();

            return new StatusCodeResult(StatusCodes.Status400BadRequest);
        }
    }
}
