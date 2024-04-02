using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using CodeProject.AI.Server.Backend;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Modules;
using CodeProject.AI.Server.Mesh;

namespace CodeProject.AI.Server.Controllers
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
        private static HttpClient _httpClient = new ()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string CPAI_Forwarded_Header = "X-CPAI-Forwarded";

        private readonly CommandDispatcher _dispatcher;
        private readonly BackendRouteMap _routeMap;
        private readonly ModuleCollection _installedModules;
        private readonly TriggersConfig _triggersConfig;
        private readonly TriggerTaskRunner _commandRunner;
        private readonly MeshManager _meshManager;
        private readonly ModuleProcessServices _moduleProcessService;

        private bool   _verbose = false;


        /// <summary>
        /// Initializes a new instance of the ProxyController class.
        /// </summary>
        /// <param name="dispatcher">The Command Dispatcher instance.</param>
        /// <param name="routeMap">The Route Manager</param>
        /// <param name="ModuleCollectionOptions">Contains the Collection of modules</param>
        /// <param name="triggersConfig">Contains the triggers</param>
        /// <param name="commandRunner">The command runner</param>
        /// <param name="meshManager">The mesh manager</param>
        /// <param name="moduleProcessService">The module process service</param>
        public ProxyController(CommandDispatcher dispatcher,
                               BackendRouteMap routeMap,
                               IOptions<ModuleCollection> ModuleCollectionOptions,
                               IOptions<TriggersConfig> triggersConfig,
                               TriggerTaskRunner commandRunner,
                               MeshManager meshManager,
                               ModuleProcessServices moduleProcessService)
        {
            _dispatcher           = dispatcher;
            _routeMap             = routeMap;
            _installedModules     = ModuleCollectionOptions.Value;
            _triggersConfig       = triggersConfig.Value;
            _commandRunner        = commandRunner;
            _meshManager          = meshManager;
            _moduleProcessService = moduleProcessService;
        }

        /// <summary>
        /// Passes the payload to the queue for processing.
        /// </summary>
        /// <param name="pathSuffix">The path for this request without the "v1". This will be in the
        /// form "category/module[/command]". eg "image/alpr" or "vision/custom/modelName".</param>
        /// <returns>The result of the command, or error.</returns>
        [HttpPost]
        [Route("{**pathSuffix}")]
        public async Task<IActionResult> Post(string pathSuffix)
        {
            if (_verbose)
                Debug.WriteLine("TRACE: Received call to " + pathSuffix);

            // check if this is a forwarded request and if so run locally.
            Microsoft.Extensions.Primitives.StringValues forwardedHeader;
            bool isForwardedRequest = Request.Headers.TryGetValue(CPAI_Forwarded_Header, 
                                                                  out forwardedHeader)
                                    && forwardedHeader == "true";

            if (isForwardedRequest && !_meshManager.AcceptForwardedRequests)
                return BadRequest("This server does not accept forwarded requests.");

            object? response = null;

            if (!isForwardedRequest && _meshManager.AllowRequestForwarding)
            {
                // Find the 'best' server to use for this request.
                MeshServerRoutingEntry? server = _meshManager.SelectServer(pathSuffix);

                // If a remote server was selected, forward the request to that server.
                if (server is not null && !server.IsLocalServer)
                {
                    if (_verbose)
                        Debug.WriteLine("TRACE: Forwarding to server " + server);

                    response = await DispatchRemoteRequest(pathSuffix, server).ConfigureAwait(false);
                    // return await DispatchRemoteRequest(pathSuffix, server).ConfigureAwait(false);
                }
            }

            // we have not forwarded, so this is a local request, do it the normal way.
            if (response is null && _routeMap.TryGetValue(pathSuffix, "POST", out RouteQueueInfo? routeInfo))
            {
                if (_verbose)
                    Debug.WriteLine("TRACE: Processing locally");

                response = await DispatchLocalRequest(pathSuffix, routeInfo!).ConfigureAwait(false); 
                // return await DispatchLocalRequest(pathSuffix, routeInfo!).ConfigureAwait(false);
            }
            else if (_verbose)
                Debug.WriteLine("ERROR: Unable to process: no suitable mesh server or local route found");

            if (response is null)
            {
                return NotFound();
            }
            else if (response is string responseString)
            {
                return new ObjectResult(responseString);
            }
            else if (response is JsonObject responseObject)
            {
                // Add common reporting properties
                responseObject["timestampUTC"] = DateTime.UtcNow.ToString("R");
                // Or to create a form that show DateKind, use
                // responseObject["timestampUTC"] = DateTime.Now.ToUniversalTime().ToString("O");

                // Report to debug
                // long timeMs = responseObject["analysisRoundTripMs"]?.GetValue<long>() ?? 0;
                // Debug.WriteLine($"INFO: {pathSuffix} call processed in {timeMs}ms");

                responseString = JsonSerializer.Serialize(responseObject);
                return new ContentResult
                {
                    Content     = responseString,
                    ContentType = "application/json",
                    StatusCode  = StatusCodes.Status200OK
                };                
            }
            else
            {
                return new ObjectResult(response);
            }
        }

        private async Task<IActionResult> SendCommandToModuleAsync(string moduleId, string commandId, string command)
        {
            if (string.IsNullOrEmpty(moduleId) || string.IsNullOrEmpty(commandId))
                return BadRequest("ModuleId and CommandId are required");

            ModuleConfig? moduleConfig = _installedModules.Values
                                          .FirstOrDefault(x => x.ModuleId == moduleId);
            if (moduleConfig is null)
                return BadRequest("Module not found");

            string? queue = moduleConfig.LaunchSettings?.Queue;
            if (string.IsNullOrEmpty(queue))
                return BadRequest("Module does not have a queue");

            var payload = CreatePayload("",
                new RouteQueueInfo("", "POST", queue, command));

            var response = await _dispatcher.SendRequestAsync(queue, payload).ConfigureAwait(false);
            if (response is null)
            {
                return NotFound();
            }
            else if (response is string responseString && !string.IsNullOrWhiteSpace(responseString))
            {
                JsonObject? responseObject = null;

                responseObject = JsonSerializer.Deserialize<JsonObject>(responseString) ?? new JsonObject();
                // Add common reporting properties
                if (responseObject is not null)
                {
                    responseObject["timestampUTC"] = DateTime.UtcNow.ToString("R");
                    // Or to create a form that show DateKind, use
                    // responseObject["timestampUTC"] = DateTime.Now.ToUniversalTime().ToString("O");

                    // Report to debug
                    // long timeMs = responseObject["analysisRoundTripMs"]?.GetValue<long>() ?? 0;
                    // Debug.WriteLine($"INFO: {pathSuffix} call processed in {timeMs}ms");

                    responseString = JsonSerializer.Serialize(responseObject);
                    return new ContentResult
                    {
                        Content = responseString,
                        ContentType = "application/json",
                        StatusCode = StatusCodes.Status200OK
                    };
                }
            }

            return new ObjectResult(response);
        }

        /// <summary>
        /// Gets a summary, in Markdown form, of the API for each module.
        /// </summary>
        [HttpGet("api")]
        public IActionResult ApiSummary()
        {
            CodeExampleGenerator sampleGenerator = new CodeExampleGenerator();

            TextInfo textInfo     = new CultureInfo("en-US", false).TextInfo;
            StringBuilder summary = new StringBuilder();

            IOrderedEnumerable<ModuleConfig> moduleList = _installedModules.Values
                                              .Where(module => module.RouteMaps?.Length > 0)
                                              .OrderBy(module => module.PublishingInfo!.Category)
                                              .ThenBy(module => module.Name)
                                              .ThenBy(module => module.RouteMaps[0].Route);

            string currentCategory = string.Empty;

            foreach (ModuleConfig module in moduleList)
            {
                string category = module.PublishingInfo!.Category ?? "Uncategorised";
                if (category != currentCategory)
                {
                    if (currentCategory == string.Empty)
                        summary.Append("\n\n\n");

                    summary.Append($"## {textInfo.ToTitleCase(category)}\n\n");
                    currentCategory = category;
                }
                
                foreach (ModuleRouteInfo routeInfo in module.RouteMaps)
                {
                    string url = "http://localhost:32168";

                    int index = routeInfo.Route.IndexOf('/');
                    string version = "v1";
                    string route   = index > 0 ? routeInfo.Route.Substring(index + 1) : string.Empty;
                    string path    = $"{version}/{routeInfo.Route}";

                    summary.Append($"### {routeInfo.Name}\n\n");
                    summary.Append($"{routeInfo.Description}\n\n");
                    summary.Append($"``` title=''\n");
                    summary.Append($"{routeInfo.Method}: {url}/{path}\n");
                    summary.Append($"```\n\n");

                    if (module.InstallOptions?.Platforms is not null)
                    {
                        summary.Append($"**Platforms**\n\n");
                        for (int i = 0; i < module.InstallOptions.Platforms.Length; i++)
                        {
                            string platform = module.InstallOptions.Platforms[i].ToLower() == "macos"
                                            ? "macOS" : textInfo.ToTitleCase(module.InstallOptions.Platforms[i]);
                            summary.Append(platform);
                            if (i < module.InstallOptions.Platforms.Length - 1)
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
                    if (routeInfo.ReturnedOutputs is null)
                    {
                        summary.Append("(None)\n\n");
                    }
                    else
                    {
                        summary.Append("``` json\n");
                        summary.Append("{\n");
                        foreach (RouteParameterInfo output in routeInfo.ReturnedOutputs)
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

        /// <summary>
        /// Passes a request to a remote server
        /// </summary>
        /// <param name="pathSuffix">The path for this request without the "v1". This will be in the
        /// form "category/module[/command]". eg "image/alpr" or "vision/custom/modelName".</param>
        /// <param name="server">The server that will be handling this request</param>
        /// <returns>An IActionResult</returns>
        // private async Task<IActionResult> DispatchRemoteRequest(string pathSuffix, 
        private async Task<object?> DispatchRemoteRequest(string pathSuffix, 
                                                          MeshServerRoutingEntry server)
        {
            Stopwatch sw = Stopwatch.StartNew();

            HttpResponseMessage? response = null;
            JsonObject? responseObject = null;
            
            string? error  = string.Empty;
            long elapsedMs;

            try
            {
                response = await ForwardAsync(server).ConfigureAwait(false);
                elapsedMs = sw.ElapsedMilliseconds;

                if (response?.IsSuccessStatusCode ?? false)
                {
                    responseObject = response!.Content.ReadFromJsonAsync<JsonObject>().Result;

                    // Sniff for success
                    if (!responseObject!.ContainsKey("success") || !(bool)responseObject["success"]!)
                    {
                        elapsedMs = 30_000;
                    }
                }
                else
                {
                    if (responseObject?.ContainsKey("error") == true)
                        error = $"{responseObject!["error"]} ({server.Status.Hostname})";
                    else
                        error = $"Error in DispatchRemoteRequest ({server.Status.Hostname})";
                    elapsedMs = 30_000;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in DispatchRemoteRequest ({server.Status.Hostname}): {ex}");

                // Bump the response to 30s to push this server out of contention. Maybe also add
                // exception info to the message
                error      = $"Exception when forwarding request to {server.Status.Hostname}";                
                elapsedMs  = 30_000;
            }
            finally
            {
                response?.Dispose();
            }

            if (responseObject is null)
            {
                // TODO: Surely there's a better way to do this
                var resp = new ServerErrorResponse(error, HttpStatusCode.InternalServerError);
                string jsonString = JsonSerializer.Serialize(resp);
                responseObject = JsonSerializer.Deserialize<JsonObject>(jsonString);
            }
                
            _meshManager.AddResponseTime(server, pathSuffix, (int)elapsedMs);
               
            // Add more info to the response object
            if (responseObject!.ContainsKey("analysisRoundTripMs"))
                responseObject["analysisRoundTripMs"] = elapsedMs;

            responseObject["processedBy"]  = server.Status.Hostname;
            return responseObject;

            /*
            responseObject["timestampUTC"] = DateTime.UtcNow.ToString("R");
            // Or to create a form that show DateKind, use
            // responseObject["timestampUTC"] = DateTime.Now.ToUniversalTime().ToString("O");

            // We don't update a module's status when that module is on a remove server
            // _moduleProcessService.UpdateProcessStatusData(responseObject);

            // Don't use JsonResult as it will chunk the response and Blue Iris will roll over and
            // die.
            string responseString = JsonSerializer.Serialize(responseObject);
            return new ContentResult
            {
                Content     = responseString,
                ContentType = "application/json",
                
                // NOTE: Always return a 200 even if the remote server failed. We are returning just
                // fine, and if the remote server failed then our Content will contain an object
                // that has success = false and an error message. However, the HTTP call itself was
                // still successful.
                StatusCode  = StatusCodes.Status200OK
            };
            */
        }

        /// <summary>
        /// Passes a request to the local (current) server
        /// </summary>
        /// <param name="pathSuffix">The path for this request without the "v1". This will be in the
        /// form "category/module[/command]". eg "image/alpr" or "vision/custom/modelName".</param>
        /// <param name="routeInfo">The route and queue to which this request should be placed</param>
        /// <returns>An IActionResult</returns>
        // private async Task<IActionResult> DispatchLocalRequest(string pathSuffix, RouteQueueInfo routeInfo)
        private async Task<object?> DispatchLocalRequest(string pathSuffix, RouteQueueInfo routeInfo)
        {
            // TODO: We have enough info in the routeInfo object to be able to validate that the
            //       request by checking that all the required values are present. Let's do that.
            RequestPayload payload = CreatePayload(pathSuffix, routeInfo!);

            Stopwatch sw = Stopwatch.StartNew();

            object response = await _dispatcher.SendRequestAsync(routeInfo!.QueueName, payload)
                                               .ConfigureAwait(false);

            long analysisRoundTripMs = sw.ElapsedMilliseconds;

            // if the response is a string, it was returned from the backend analysis module.
            if (response is string responseString)
            {
                // Unwrap the response and add the analysisRoundTripMs property
                JsonObject? responseObject = null;
                if (!string.IsNullOrEmpty(responseString))
                    responseObject = JsonSerializer.Deserialize<JsonObject>(responseString);

                responseObject ??= new JsonObject();
                responseObject["analysisRoundTripMs"] = analysisRoundTripMs;
                responseObject["processedBy"]         = "localhost";

                // responseObject["timestampUTC"] = DateTime.UtcNow.ToString("R");
                // Or to create a form that show DateKind, use
                // responseObject["timestampUTC"] = DateTime.Now.ToUniversalTime().ToString("O");

                _meshManager.AddResponseTime(null, pathSuffix, (int)analysisRoundTripMs);

                string? moduleId = responseObject?["moduleId"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(moduleId))
                {
                    _moduleProcessService.UpdateModuleLastSeen(moduleId);

                    var statusData = responseObject?["statusData"] as JsonObject;
                    if (statusData is not null)
                        _moduleProcessService.UpdateProcessStatusData(moduleId, statusData);
                }

                return responseObject;

                /*
                // Check for, and execute if needed, triggers
                ProcessTriggers(routeInfo!.QueueName, responseObject);

                // Wrap it back up. Don't use JsonResult as it will chunk the response and Blue Iris
                // will roll over and die.
                responseString = JsonSerializer.Serialize(responseObject) as string;
                return new ContentResult
                {
                    Content     = responseString,
                    ContentType = "application/json",
                    StatusCode  = StatusCodes.Status200OK
                };
                */
            }
            else
            {
                return response;
                // return new ObjectResult(response);
            }
        }

        /// <summary>
        /// Forwards the request to the target server.
        /// </summary>
        /// <param name="server">The MeshServerStatus of the target server.</param>
        /// <returns>A HttpResponseMessage</returns>
        private async Task<HttpResponseMessage> ForwardAsync(MeshServerRoutingEntry server)
        {
            HttpRequest originalRequest = Request;
            int? port         = originalRequest.Host.Port;
            string portString = port.HasValue ? $":{port}" : string.Empty;
            var queryString   = originalRequest.QueryString;

            string hostname = server.CallableHostname;
            if (!_meshManager.RouteViaHostName && server.EndPointIPAddress is not null)
                hostname = server.EndPointIPAddress;

            string url = $"http://{hostname}{portString}{originalRequest.Path}{queryString}";
            Uri targetUri = new Uri(url);

            // Create a MultipartFormDataContent object from the original request's form data,
            // including both form data and files
            MultipartFormDataContent multipartContent = new MultipartFormDataContent();

            // Add form data to the multipart content
            foreach (string key in Request.Form.Keys)
            {
                multipartContent.Add(new StringContent(originalRequest.Form[key].ToString()), key);
            }

            // Add files from the request to the multipart content
            foreach (IFormFile? file in Request.Form.Files)
            {
                Stream stream             = file.OpenReadStream();
                StreamContent fileContent = new StreamContent(stream);
                multipartContent.Add(fileContent, "image", file.FileName);
            }

            // Create a new request with the same method, headers and content as the original request
            HttpRequestMessage newRequest = new HttpRequestMessage(new HttpMethod(originalRequest.Method), targetUri)
            {
                Content = multipartContent
            };

            foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header in originalRequest.Headers)
                newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.AsEnumerable());

            newRequest.Headers.Add(CPAI_Forwarded_Header, "true");

            // Send the new request to the target server and get the response
            HttpResponseMessage response = await _httpClient.SendAsync(newRequest);

            return response;
        }

        private RequestPayload CreatePayload(string pathSuffix, RouteQueueInfo routeInfo)
        {
            // TODO: Add Segment list (string[]) and params (map of name/value)
            string endOfUrl = pathSuffix.Remove(0, routeInfo.Route.Length);

            var segments    = new List<string>();
            var queryParams = new List<KeyValuePair<string, string?[]>>();
            var formFiles   = new List<RequestFormFile>();

            if (endOfUrl.StartsWith("/"))
                endOfUrl = endOfUrl[1..];

            // handle extra segments
            if (endOfUrl.Length > 0)
                segments.AddRange(endOfUrl.Split('/', StringSplitOptions.TrimEntries));

            // and the QueryString parameters
            IQueryCollection queryParts = Request.Query;
            if (queryParts?.Any() ?? false)
            {
                foreach (KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> param in queryParts)
                    queryParams.Add(new KeyValuePair<string, string?[]>(param.Key, param.Value.ToArray()));
            }

            // The HasFormContentType check should be sufficient and checks for multipart/form-data,
            // and application/x-www-form-urlencoded. There should be no need to use a try/catch
            // here either, but the GetFileData() method might throw if the moon is in the wrong phase.
            if (Request.HasFormContentType && Request.Form is not null)
            {
                try // if there are no Form values, then Request.Form throws.
                {
                    IFormCollection form = Request.Form;

                    // Add any Form values.
                    queryParams.AddRange(form.Select(x => 
                        new KeyValuePair<string, string?[]>(x.Key, x.Value.ToArray())));

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
            }

            RequestPayload payload = new RequestPayload
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
            using Stream stream       = x.OpenReadStream();
            using BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, false);
            byte[] data               = new byte[x.Length];
            reader.Read(data, 0, data.Length);

            return data;
        }

        // TODO: This needs to be done in the background or else this will slow down the responses
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
                        JsonNode? predictions = response[trigger.PredictionsCollectionName];
                        if (predictions is not null)
                        {
                            foreach (JsonNode? prediction in predictions.AsArray())
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
                Console.WriteLine("Error processing triggers: " + ex.Message);
            }
        }
    }
}
