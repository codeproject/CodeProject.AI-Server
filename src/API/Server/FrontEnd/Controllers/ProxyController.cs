using CodeProject.SenseAI.API.Server.Backend;
using CodeProject.SenseAI.Server.Backend;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
{
    // ------------------------------------------------------------------------------
    // When a backend process starts it will register itself with the main SenseAI Server.
    // It does this by Posting as Register request to the Server which
    //  - provides the end part of url for the request
    //  - the name of the queue that the request will be sent to.
    //  - the command string that will be associated with the payload sent to the queue.
    //
    // To initiate an AI operation, the client will post a payload to the 
    // This is accomplished by
    //  - getting the url ending.
    //  - using this to get the queue name and command name
    //  - sending the above and payload to the queue
    //  - await the respons
    //  - return the response to the caller.
    // ------------------------------------------------------------------------------

    /// <summary>
    /// This controller just passes the payload to the backend queues for processing.
    /// </summary>
    [Route("v1")]
    [ApiController]
    public class ProxyController : ControllerBase
    {
        private readonly VisionCommandDispatcher _dispatcher;
        private readonly BackendRouteMap         _routeMap;

        /// <summary>
        /// Initializes a new instance of the VisionController class.
        /// </summary>
        /// <param name="dispatcher">The Command Dispatcher instance.</param>
        /// <param name="routeMap">The Route Manager</param>
        public ProxyController(VisionCommandDispatcher dispatcher, BackendRouteMap routeMap)
        {
            _dispatcher = dispatcher;
            _routeMap   = routeMap;
        }

        /// <summary>
        /// Passes the payload to the queue for processing.
        /// </summary>
        /// <returns>The result of the command, or error.</returns>
        [HttpPost("{**path}")]
        public async Task<IActionResult> Post(string path)
        {
            if (_routeMap.TryGetValue(path, out BackendRouteInfo routeInfo))
            {
                RequestPayload payload  = CreatePayload(routeInfo);
                var response = await _dispatcher.QueueRequest(routeInfo.Queue, routeInfo.Command,
                                                              payload);

                // if the response is a string, it was returned from the backend.
                if (response is string)
                {
                    return new ContentResult
                    {
                        Content     = response as string,
                        ContentType = "application/json",
                        StatusCode  = 200
                    };
                }
                else
                    return new ObjectResult(response);
            }
            else
                return BadRequest();
        }

        private RequestPayload CreatePayload(BackendRouteInfo routeInfo)
        {
            IFormCollection form = Request.Form;
            var requestValues = form.Select(x => new KeyValuePair<string, string[]?>(x.Key, x.Value.ToArray())).ToList();
            var payload       = new RequestPayload
            {
                command = routeInfo.Command,
                values  = requestValues,
                files   = form.Files.Select(x => new RequestFormFile
                {
                    name        = x.Name,
                    filename    = x.FileName,
                    contentType = x.ContentType,
                    data        = GetFileData(x)
                })
            };

            return payload;
        }

        private byte[] GetFileData(IFormFile x)
        {
            using var stream = x.OpenReadStream();
            using var reader = new BinaryReader(stream, Encoding.UTF8, false);
            var data = new byte[x.Length];
            reader.Read(data, 0, data.Length);

            return data;
        }
    }
}
