
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using CodeProject.SenseAI.AnalysisLayer.SDK;
using CodeProject.SenseAI.API.Server.Backend;
using CodeProject.SenseAI.Server.Backend;

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
    // TODO: add Version to the RouteMaps or remove the [Route("v1")] from the controller
    //      and include the v1 in the route
    [Route("v1")]
    [ApiController]
    public class ProxyController : ControllerBase
    {
        private readonly CommandDispatcher _dispatcher;
        private readonly BackendRouteMap   _routeMap;
        private readonly ModuleCollection  _modules;

        /// <summary>
        /// Initializes a new instance of the VisionController class.
        /// </summary>
        /// <param name="dispatcher">The Command Dispatcher instance.</param>
        /// <param name="routeMap">The Route Manager</param>
        /// <param name="modulesConfig">Contains the Collection of modules</param>
        public ProxyController(CommandDispatcher dispatcher, BackendRouteMap routeMap,
                               IOptions<ModuleCollection> modulesConfig)
        {
            _dispatcher = dispatcher;
            _routeMap   = routeMap;
            _modules    = modulesConfig.Value;
        }

        /// <summary>
        /// Passes the payload to the queue for processing.
        /// </summary>
        /// <returns>The result of the command, or error.</returns>
        [HttpPost("{**path}")]
        public async Task<IActionResult> Post(string path)
        {
            if (_routeMap.TryGetValue(path, "POST", out RouteQueueInfo routeInfo))
            {
                RequestPayload payload  = CreatePayload(routeInfo);
                var response = await _dispatcher.QueueRequest(routeInfo.QueueName, routeInfo.Command,
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
                return NotFound();
        }

        /// <summary>
        /// Gets a summary, in Markdown form, of the API for each module.
        /// </summary>
        [HttpGet("api")]
        public IActionResult ApiSummary()
        {
            var sampleGenerator = new CodeExampleGenerator();

            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            var summary = new StringBuilder();

            var moduleList = _modules.Values
                                     .Where(module => module.RouteMaps?.Length > 0)
                                     .OrderBy(module => module.RouteMaps[0].Path);

            string currentCategory = string.Empty;

            foreach (var module in moduleList)
            {
                if (module.Activate != true)
                    continue;

                foreach (ModuleRouteInfo routeInfo in module.RouteMaps)
                {
                    string url = "http://localhost:5000";

                    // string version  = routeInfo.Version;
                    // string category = routeInfo.Category;
                    // string route    = routeInfo.Route;

                    int index = routeInfo.Path.IndexOf('/');
                    string version  = "v1";
                    string category = index > 0 ? routeInfo.Path.Substring(0, index) : routeInfo.Path;
                    string route    = index > 0 ? routeInfo.Path.Substring(index + 1) : string.Empty;

                    string path     = $"/{version}/{category}/{route}";

                    if (category != currentCategory)
                    {
                        if (currentCategory == string.Empty)
                            summary.Append("\n\n\n");

                        summary.Append($"## {textInfo.ToTitleCase(category)}\n\n");
                        currentCategory = category;
                    }

                    summary.Append($"### {routeInfo.Name}\n\n");
                    summary.Append($"{routeInfo.Description}\n\n");
                    summary.Append($"``` title=''\n");
                    summary.Append($"{routeInfo.Method}: {url}/{path}\n");
                    summary.Append($"```\n\n");

                    if (module.Platforms is not null)
                    {
                        summary.Append($"**Platforms**\n\n");
                        for (int i = 0; i < module.Platforms.Length; i++)
                        {
                            string platform = module.Platforms[i].ToLower() == "macos"
                                            ? "macOS" : textInfo.ToTitleCase(module.Platforms[i]);
                            summary.Append(platform);
                            if (i < module.Platforms.Length - 1)
                                summary.Append(", ");
                        }
                        summary.Append("\n\n");
                    }

                    summary.Append($"**Parameters**\n\n");
                    if (routeInfo.Inputs is null)
                    {
                        summary.Append("(None)\n\n");
                    }
                    else
                    {
                        foreach (RouteParameterInfo input in routeInfo.Inputs)
                        {                           
                            summary.AppendLine($" - **{input.Name}** ({input.Type}): {input.Description}");
                            if (!string.IsNullOrWhiteSpace(input.DefaultValue))
                                summary.AppendLine($"   *Optional*. Defaults to {input.DefaultValue}");
                            summary.AppendLine();
                        }
                    }

                    summary.Append($"**Response**\n\n");
                    if (routeInfo.Outputs is null)
                    {
                        summary.Append("(None)\n\n");
                    }
                    else
                    {
                        summary.Append("``` json\n");
                        summary.Append("{\n");
                        foreach (RouteParameterInfo output in routeInfo.Outputs)
                            summary.Append($"  \"{output.Name}\": ({output.Type}) // {output.Description}\n");

                        summary.Append("}\n");
                        summary.Append("```\n");
                    }

                    string sample = sampleGenerator.GenerateJavascript(routeInfo);
                    if (!string.IsNullOrWhiteSpace(sample))
                    {
                        summary.Append("\n\n");
                        summary.Append("#### Example\n\n");
                        summary.Append(sample);
                    }

                    summary.Append("\n\n\n");
                }
            }

            return new ObjectResult(summary.ToString());
        }

        private RequestPayload CreatePayload(RouteQueueInfo routeInfo)
        {
            // TODO: Include querystring parameters as well.
            IFormCollection form = Request.Form;
            var requestValues = form.Select(x => new KeyValuePair<string, string[]?>(x.Key, x.Value.ToArray())).ToList();
            var payload       = new RequestPayload
            {
                command = routeInfo.Command,
                queue   = routeInfo.QueueName,
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
            var data         = new byte[x.Length];
            reader.Read(data, 0, data.Length);

            return data;
        }
    }
}
