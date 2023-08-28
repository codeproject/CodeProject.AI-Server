using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using CodeProject.AI.API.Server.Backend;
using CodeProject.AI.API.Common;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.API.Server.Frontend.Controllers
{
    // ------------------------------------------------------------------------------
    // When a backend analysis module starts it will register itself with the main 
    // Server. It does this by Posting as Register request to the Server which
    //  - provides the end part of url for the request
    //  - the name of the queue that the request will be sent to.
    //  - the command string that will be associated with the payload sent to the queue.
    //
    // To initiate an AI operation, the client will post a payload to the server
    // This is accomplished by
    //  - getting the url ending.
    //  - using this to get the queue name and command name
    //  - sending the above, plus a payload, to the queue
    //  - await the response
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
        private readonly TriggersConfig    _triggersConfig;
        private readonly TriggerTaskRunner _commandRunner;

        /// <summary>
        /// Initializes a new instance of the VisionController class.
        /// </summary>
        /// <param name="dispatcher">The Command Dispatcher instance.</param>
        /// <param name="routeMap">The Route Manager</param>
        /// <param name="modulesConfig">Contains the Collection of modules</param>
        /// <param name="triggersConfig">Contains the triggers</param>
        /// <param name="commandRunner">The command runner</param>
        public ProxyController(CommandDispatcher dispatcher, 
                               BackendRouteMap routeMap,
                               IOptions<ModuleCollection> modulesConfig,
                               IOptions<TriggersConfig> triggersConfig,
                               TriggerTaskRunner commandRunner)
        {
            _dispatcher     = dispatcher;
            _routeMap       = routeMap;
            _modules        = modulesConfig.Value;
            _triggersConfig = triggersConfig.Value;
            _commandRunner  = commandRunner;
        }

        /// <summary>
        /// Passes the payload to the queue for processing.
        /// </summary>
        /// <returns>The result of the command, or error.</returns>
        [HttpPost("{**path}")]
        public async Task<IActionResult> Post(string path)
        {
            if (_routeMap.TryGetValue(path, "POST", out RouteQueueInfo? routeInfo))
            {
                RequestPayload payload = CreatePayload(path, routeInfo!);

                Stopwatch sw = Stopwatch.StartNew();

                var response = await _dispatcher.QueueRequest(routeInfo!.QueueName, payload)
                                                .ConfigureAwait(false);

                long analysisRoundTripMs = sw.ElapsedMilliseconds;

                // if the response is a string, it was returned from the backend analysis module.
                if (response is string responseString)
                {
                    // Unwrap the response and add the analysisRoundTripMs property
                    JsonObject? jsonResponse = null;
                    if (!string.IsNullOrEmpty(responseString))
                        jsonResponse = JsonSerializer.Deserialize<JsonObject>(responseString);
                    jsonResponse ??= new JsonObject();                   
                    jsonResponse["analysisRoundTripMs"] = analysisRoundTripMs;

                    // Check for, and execute if needed, triggers
                    ProcessTriggers(routeInfo!.QueueName, jsonResponse);

                    // Wrap it back up
                    responseString = JsonSerializer.Serialize(jsonResponse) as string;
                    return new ContentResult
                    {
                        Content     = responseString,
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
                foreach (ModuleRouteInfo routeInfo in module.RouteMaps)
                {
                    string url = "http://localhost:32168";

                    // string version  = routeInfo.Version;
                    // string category = routeInfo.Category;
                    // string route    = routeInfo.Route;

                    int index = routeInfo.Path.IndexOf('/');
                    string version  = "v1";
                    string category = index > 0 ? routeInfo.Path.Substring(0, index) : routeInfo.Path;
                    string route    = index > 0 ? routeInfo.Path.Substring(index + 1) : string.Empty;

                    // string path  = $"/{version}/{category}/{route}";
                    string path     = $"{version}/{routeInfo.Path}";

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

        private RequestPayload CreatePayload(string path, RouteQueueInfo routeInfo)
        {
            // TODO: Add Segment list (string[]) and params (map of name/value)
            var endOfUrl = path.Remove(0, routeInfo.Path.Length);

            var segments    = new List<string>();
            var queryParams = new List<KeyValuePair<string, string?[]>>();
            var formFiles   = new List<RequestFormFile>();

            if (endOfUrl.StartsWith("/"))
                endOfUrl = endOfUrl[1..];

            // handle extra segments
            if (endOfUrl.Length > 0)
                segments.AddRange(endOfUrl.Split('/', StringSplitOptions.TrimEntries));

            // and the QueryString parameters
            var queryParts = Request.Query;
            if (queryParts?.Any() ?? false)
            {
                foreach (var param in queryParts)
                    queryParams.Add(new KeyValuePair<string, string?[]>(param.Key, param.Value.ToArray()));
            }

            try // if there are no Form values, then Request.Form throws.
            {
                IFormCollection form = Request.Form;
                
                // Add any Form values.
                queryParams.AddRange(form.Select(x => new KeyValuePair<string, string?[]>(x.Key, x.Value.ToArray())));

                // Add any form files
                formFiles.AddRange(form.Files.Select(x => new RequestFormFile
                {
                    name        = x.Name,
                    filename    = x.FileName,
                    contentType = x.ContentType,
                    data        = GetFileData(x)
                }));
            }
            catch
            {
                // nothing to do here, just no Form available
            }

            var payload = new RequestPayload
            {
                urlSegments = segments.ToArray(),
                command     = routeInfo.Command,
                values      = queryParams,
                files       = formFiles
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

        private void ProcessTriggers(string queueName, JsonObject response)
        {
            if (_triggersConfig.Triggers is null || _triggersConfig.Triggers.Length == 0)
                return;

            string platform = SystemInfo.Platform;

            try
            {
                foreach (Trigger trigger in _triggersConfig.Triggers)
                {
                    // If the trigger is queue specific, check
                    if (!string.IsNullOrWhiteSpace(trigger.Queue) &&
                        !trigger.Queue.EqualsIgnoreCase(queueName))
                        continue;

                    // Is there a task to run on this platform, and a property to look for?
                    TriggerTask? task = trigger.GetTask(platform);
                    if (string.IsNullOrEmpty(trigger.PropertyName) || task is null || 
                        string.IsNullOrEmpty(task.Command))
                        continue;

                    if (string.IsNullOrWhiteSpace(trigger.PredictionsCollectionName))
                    {
                        float.TryParse(response["confidence"]?.ToString(), out float confidence);
                        string? value = response[trigger.PropertyName]?.ToString();
                        if (trigger.Test(value, confidence))
                            _commandRunner.RunCommand(task);
                    }
                    else
                    {
                        var predictions = response[trigger.PredictionsCollectionName];
                        if (predictions is not null)
                        {
                            foreach (var prediction in predictions.AsArray())
                            {
                                if (prediction is null)
                                    continue;
                                    
                                float.TryParse(prediction["confidence"]?.ToString(), out float confidence);
                                string? value = prediction[trigger.PropertyName]?.ToString();
                                if (trigger.Test(value, confidence))
                                    _commandRunner.RunCommand(task);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
