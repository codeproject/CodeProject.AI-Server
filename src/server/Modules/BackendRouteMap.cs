using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// The data type of the route parameter
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RouteParameterType
    {
        /// <summary>
        /// String
        /// </summary>
        Text,
        /// <summary>
        /// Integer
        /// </summary>
        Integer,
        /// <summary>
        /// Floating point
        /// </summary>
        Float,
        /// <summary>
        /// Boolean
        /// </summary>
        Boolean,
        /// <summary>
        /// File object
        /// </summary>
        File,
        /// <summary>
        /// Object
        /// </summary>
        Object
    }

    /// <summary>
    /// Describes a parameter passed to, or returned from, a command sent to a route
    /// </summary>
    public struct RouteParameterInfo
    {
        /// <summary>
        /// Gets the Name of the parameter.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the type of the parameter.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Get the description of the parameter.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets the default value for this parameter if not provided to or returned from a process.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Gets the minimum value for this parameter if not provided to or returned from a process.
        /// </summary>
        public string? MinValue { get; set; }

        /// <summary>
        /// Gets the maximum value for this parameter if not provided to or returned from a process.
        /// </summary>
        public string? MaxValue { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this parameter is supplied by the system
        /// </summary>
        public bool System { get; set; }
    }

    /// <summary>
    /// Holds the route and command associated with a url.
    /// </summary>
    public struct ModuleRouteInfo
    {
        /// <summary>
        /// This is an array of routes added by the system to each module to handle common requests
        /// such as process_status or cancel_long_process.
        /// </summary>
        private static  ModuleRouteInfo[] _systemDefaultRouteMaps;

        /// <summary>
        /// This is an array of outputs added by the system: some are added by the base module code
        /// such as ModuleWorkerBase.s or module_runner.py (eg canUseGPU) and some are added by the
        /// proxy controller (eg analysisRoundTripMs). It would be best to have each party that is
        /// responsible for adding outputs do so by registering their outputs here, but for now
        /// we'll hardcode
        /// </summary>
        private static RouteParameterInfo[] _systemAppendedOutputs;

        /// <summary>
        /// The module's explicit outputs plus the system outputs automatically added
        /// </summary>
        private RouteParameterInfo[]? _fullOutputs;

        /// <summary>
        /// Gets the array of routes added by the system to each module to handle common requests
        /// such as process_status or cancel_long_process.
        /// </summary>
        public static ModuleRouteInfo[] SystemDefaultRouteMaps => _systemDefaultRouteMaps;

        /// <summary>
        /// Gets the name for this endpoint.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the Route for the endpoint, which does not include the base path. Currently our
        /// base path is 'v1/', so the full path is: "v1/" + Route.
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method to use when calling this endpoint
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the name of the command.
        /// </summary>
        public string Command { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this path is available to be used in mesh
        /// processing.
        /// </summary>
        public bool? MeshEnabled { get; set; } = true;

        /// <summary>
        /// Get the description of the endpoint.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets the inputs parameter information.
        /// </summary>
        public RouteParameterInfo[]? Inputs { get; set; }

        /// <summary>
        /// Gets the output parameter information.
        /// </summary>
        public RouteParameterInfo[]? Outputs { get; set; }

        /// <summary>
        /// Gets the list output parameter information returned to the client, including parameters
        /// added by the server itself such as timing or module info.
        /// </summary>
        public RouteParameterInfo[]? ReturnedOutputs
        {
            get
            {
                // Init _fullOutputs if need be, and if Outputs is null then easy peasy
                if (_fullOutputs is null && Outputs is null)
                    _fullOutputs = _systemAppendedOutputs;

                if (_fullOutputs is null)
                {
                    // Copy over what we currently have
                    List<RouteParameterInfo> fullSet = Outputs!.ToList();

                    // ...and tack on the system outputs *only* if we're not already returning them
                    foreach (var param in _systemAppendedOutputs)
                        if (!fullSet.Any(p => p.Name.EqualsIgnoreCase(param.Name)))
                            fullSet.Add(param);

                    _fullOutputs = fullSet.ToArray();
                }

                return _fullOutputs;
            }
        }

        /// <summary>
        /// The static constructor
        /// </summary>
        static ModuleRouteInfo()
        {
            // There are routes that are made available to all modules as part of status checks
            _systemDefaultRouteMaps = new ModuleRouteInfo[]
            {
                new ModuleRouteInfo()
                {
                    Name        = "Get Long Process Status",
                    Route       = "%MODULE_ID%/get_command_status",
                    Method      = "POST",
                    Command     = "get_command_status",
                    MeshEnabled = false,
                    Description = "Gets the status of a long running command.",
                    Inputs      = new RouteParameterInfo[]
                    {
                        new RouteParameterInfo()
                        {
                            Name        = "commandId",
                            Type        = "Text",
                            Description = "The id of the command to get the result for."
                        }
                    },
                    Outputs     = new RouteParameterInfo[]
                    {
                        new RouteParameterInfo()
                        {
                            Name        = "success",
                            Type        = "Boolean",
                            Description = "Whether or not the operation was successful"
                        }
                    }
                },
                new ModuleRouteInfo()
                {
                    Name        = "Cancel Long Process",
                    Route       = "%MODULE_ID%/cancel_command",
                    Method      = "POST",
                    Command     = "cancel_command",
                    MeshEnabled = false,
                    Description = "Cancels a long running command.",
                    Inputs      = new RouteParameterInfo[]
                    {
                        new RouteParameterInfo()
                        {
                            Name        = "commandId",
                            Type        = "Text",
                            Description = "The id of the command to cancel."
                        }
                    },
                    Outputs     = new RouteParameterInfo[]
                    {
                        new RouteParameterInfo()
                        {
                            Name        = "success",
                            Type        = "Boolean",
                            Description = "Whether or not the operation was successful"
                        }
                    }
                }
            };

            // There are outputs that are added by the system as part of routing
            _systemAppendedOutputs = new RouteParameterInfo[]
            {
                new RouteParameterInfo()
                {
                    Name        = "moduleId",
                    Type        = "String",
                    Description = "The Id of the module that processed this request.",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "moduleName",
                    Type        = "String",
                    Description = "The name of the module that processed this request.",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "command",
                    Type        = "String",
                    Description = "The command that was sent as part of this request. Can be detect, list, status.",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "statusData",
                    Type        = "Object",
                    Description = "[Optional] An object containing (if available) the current module status data.",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "inferenceDevice",
                    Type        = "String",
                    Description = "The name of the device handling the inference. eg CPU, GPU, TPU",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "analysisRoundTripMs",
                    Type        = "Integer",
                    Description = "The time (ms) for the round trip to the analysis module and back.",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "processedBy",
                    Type        = "String",
                    Description = "The hostname of the server that processed this request.",
                    System      = true
                },
                new RouteParameterInfo()
                {
                    Name        = "timestampUTC",
                    Type        = "String",
                    Description = "The timestamp (UTC) of the response.",
                    System      = true
                }
            };
        }

        /// <summary>
        /// Initializes a new instance of the BackendRouteInfo struct.
        /// </summary>
        /// <param name="name">The name of this endpoint.</param>
        /// <param name="route">The route for the endpoint. This is "image/alpr" not "v1/image/alpr".</param>
        /// <param name="method">The HTTP Method used to call the path/command</param>
        /// <param name="command">The command string that will be passed as part of the data
        /// sent to the server.</param>
        /// <param name="meshEnabled">Whether or not this path is allowed to be used for mesh 
        /// processing</param>
        /// <param name="description">A Description of the endpoint.</param>
        /// <param name="inputs">The input parameters information.</param>
        /// <param name="outputs">The output parameters information.</param>
        public ModuleRouteInfo(string name, string route, string method, string command, 
                               bool meshEnabled, string? description = null,
                               RouteParameterInfo[]? inputs = null,
                               RouteParameterInfo[]? outputs = null)
        {
            Name        = name;
            Route       = route.ToLower();
            Method      = method.ToUpper();
            Command     = command;
            MeshEnabled = meshEnabled;
            Description = description;
            Inputs      = inputs;
            Outputs     = outputs;
        }
    }

    /*
    /// <summary>
    /// Extension methods for the ModuleRouteInfo class
    /// </summary>
    public static class CommandRouteInfoExtensions
    {
        /// <summary>
        /// Returns true if this module is running on the specified Queue
        /// </summary>
        /// <param name="routeInfo">This ModuleRouteInfo object</param>
        /// <param name="queueName">The name of the queue</param>
        /// <returns>True if running on the queue; false otherwise</returns>
        public static bool IsQueue(this ModuleRouteInfo routeInfo, string queueName)
        {
            return routeInfo.Queue.EqualsIgnoreCase(queueName);
        }
    }
    */

    /// <summary>
    /// Defines the destination route information that is required to send a command to the front
    /// end server for processing by the backend analysis Modules.
    /// </summary>
    public class RouteQueueInfo
    {
        /// <summary>
        /// Gets the route for this object
        /// </summary>
        public string Route { get; private set; }

        /// <summary>
        /// Gets the HTTP Method for the route,
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// Gets the name of the Queue.
        /// </summary>
        public string QueueName { get; private set; }

        /// <summary>
        /// Gets the command identifier which distinguishes the backend operations to perform based 
        /// on the server's API endpoint.
        /// </summary>
        public string Command { get; private set; }

        /// <summary>
        /// Initializes a new instance of the RouteQueueInfo class.
        /// </summary>
        /// <param name="route">The route this endpoint. This doesn't include the "v1", so is just
        /// "image/alpr" not "v1/image/alpr".</param>
        /// <param name="method">The HTTP Method for the route.</param>
        /// <param name="queueName">The name of the Queue.</param>
        /// <param name="command">The backend operation identifier.</param>
        public RouteQueueInfo(string route, string method, string queueName, string command)
        {
            Route     = route.ToLower();
            Method    = method.ToUpper();
            QueueName = queueName.ToLower();
            Command   = command;
        }
    }

    /// <summary>
    /// Map for front end endpoints to backend queues. 
    /// </summary>
    /// <remarks>
    /// We include the whole RouteQueueInfo object because we might want to validate the parameters
    /// before sending the request to the backend.
    /// </remarks>
    public class BackendRouteMap
    {
        /// <summary>
        /// Maps a url to the queue and command associated with it.
        /// </summary>
        private ConcurrentDictionary<string, RouteQueueInfo> _routeQueueMap = new();

        /// <summary>
        /// Geth the routes in the Route Map.
        /// </summary>
        public IEnumerable<string> Routes => _routeQueueMap.Values
                                                           .Select(x => x.Route)
                                                           .Distinct()
                                                           .OrderBy(x => x)    
                                                           .ToList();

        /// <summary>
        /// Tries to get the route information for a path.
        /// </summary>
        /// <param name="route">The route to get the information for.</param>
        /// <param name="method">The HTTP Method used by the server's API endpoint.</param>
        /// <param name="queueInfo">The CommandRouteInfo instance to store the save the info to.</param>
        /// <returns>True if the path is in the Route Map, false otherwise.</returns>
        public bool TryGetValue(string route, string method, out RouteQueueInfo? queueInfo)
        {
            string key = MakeKey(route, method);
            
            // check for an exact match
            if (_routeQueueMap.TryGetValue(key, out queueInfo!))
                return true;

            // Find the best match. The longest route the has the correct Method and starts the path.
            queueInfo = _routeQueueMap!.Values
                    .Where(x => method.EqualsIgnoreCase(x.Method) && route.StartsWithIgnoreCase(x.Route))
                    .OrderByDescending(x => x.Route.Length)
                    .FirstOrDefault();

            return queueInfo is not null;        
        }

        private static string MakeKey(string route, string method)
        {
            return $"{method.ToLower()}_{route.ToLower()}";
        }

        /// <summary>
        /// Associates a url and command with a queue.
        /// </summary>
        /// <param name="route">The route this endpoint. This doesn't include the "v1", so is just
        /// "image/alpr" not "v1/image/alpr".</param>
        /// <param name="method">The HTTP Method used to call the path/command</param>
        /// <param name="queueName">The name of the queue that the request will be associated with.</param>
        /// <param name="command">The command that will be passed with the payload.</param>
        /// <param name="moduleId">The id of the module owning this route. Optional. If supplied, 
        /// lowercase moduleId will replace all instances of %MODULE_ID% in route, method, queue
        /// name and command</param> 
        public void Register(string route, string method, string queueName, string command,
                             string? moduleId = null)
        {
            string marker = "%MODULE_ID%";

            string key    = MakeKey(route.Replace(marker, moduleId), method.Replace(marker, moduleId));
            
            var routeInfo = new RouteQueueInfo(route.Replace(marker,     moduleId),
                                               method.Replace(marker,    moduleId),
                                               queueName.Replace(marker, moduleId),
                                               command.Replace(marker,   moduleId));
            _routeQueueMap[key] = routeInfo;
        }

        /// <summary>
        /// Associates a url and command with a queue.
        /// </summary>
        /// <param name="info">A <see cref="ModuleRouteInfo" /> structure containing the info to 
        /// register.</param>
        /// <param name="queueName">The name of the queue that the request will be associated with.
        /// </param>
        /// <param name="moduleId">The id of the module owning this route. Optional. If supplied, 
        /// lowercase moduleId will replace all instances of %MODULE_ID% in route, method, queue
        /// name and command</param> 
        public void Register(ModuleRouteInfo info, string queueName, string? moduleId = null)
        {
            Register(info.Route, info.Method, queueName, info.Command, moduleId);
        }
    }
}
