using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.API.Common
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RouteParameterType
    {
        Text,
        Integer,
        Float,
        Boolean,
        File,
        Object
    }

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
    }

    /// <summary>
    /// Holds the route and command associated with a url.
    /// </summary>
    // TODO: Rename to CommandRouteInfo
    public struct ModuleRouteInfo
    {
        /// <summary>
        /// Gets the name for this endpoint.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the Path for the endpoint.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method to use when calling this endpoint
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string Command { get; set; }

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
        /// Initializes a new instance of the BackendRouteInfo struct.
        /// </summary>
        /// <param name="name">The name of this endpoint.</param>
        /// <param name="path">The relative path of the endpoint.</param>
        /// <param name="method">The HTTP Method used to call the path/command</param>
        /// <param name="command">The command string that will be passed as part of the data
        /// sent to the server.</param>
        /// <param name="description">A Description of the endpoint.</param>
        /// <param name="inputs">The input parameters information.</param>
        /// <param name="outputs">The output parameters information.</param>
        public ModuleRouteInfo(string name, string path, string method, string command, 
                               string? description = null,
                               RouteParameterInfo[]? inputs = null,
                               RouteParameterInfo[]? outputs = null)
        {
            Name        = name;
            Path        = path.ToLower();
            Method      = method.ToUpper();
            Command     = command;
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
    /// Defines the destination route information that is required to send a command to
    /// the front end server for processing by the backend analysis Modules.
    /// </summary>
    public class RouteQueueInfo
    {
        /// <summary>
        /// Gets the path for this route
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Gets the HTTP Method for the route,
        /// </summary>
        public string Method { get; private set; }

        /// <summary>
        /// Gets the name of the Queue.
        /// </summary>
        public string QueueName { get; private set; }

        /// <summary>
        /// Gets the command identifier which distiguishes the backend operations to perform based 
        /// on the frontend endpoint.
        /// </summary>
        public string Command { get; private set; }

        /// <summary>
        /// Initializes a new instance of the RouteQueueInfo class.
        /// </summary>
        /// <param name="path">The URL path for this route.</param>
        /// <param name="method">The HTTP Method for the route.</param>
        /// <param name="queueName">The name of the Queue.</param>
        /// <param name="command">The backend operation identifier.</param>
        public RouteQueueInfo(string path, string method, string queueName, string command)
        {
            Path      = path.ToLower();
            Method    = method.ToUpper();
            QueueName = queueName.ToLower();
            Command   = command;
        }
    }

    /// <summary>
    /// Map for front end endpoints to backend queues.
    /// </summary>
    // TODO: this does not require the whole RouteQueueInfo, just the Queue and Command and
    //       possibly the Method.
    // TODO: Rename to CommandRouteMap
    public class BackendRouteMap
    {
        /// <summary>
        /// Maps a url to the queue and command associated with it.
        /// </summary>
        private ConcurrentDictionary<string, RouteQueueInfo> _routeQueueMap = new();

        /// <summary>
        /// Tries to get the route information for a path.
        /// TODO: Update this so it iterates to find the best (longest?) match of a given request
        ///       path
        /// </summary>
        /// <param name="path">The path to get the information for.</param>
        /// <param name="method">The HTTP Method used by the frontend server endpoint.</param>
        /// <param name="queueInfo">The CommandRouteInfo instance to store the save the info to.</param>
        /// <returns>True if the path is in the Route Map, false otherwise.</returns>
        public bool TryGetValue(string path, string method, out RouteQueueInfo? queueInfo)
        {
            string key = MakeKey(path, method);
            
            // check for an exact match
            if (_routeQueueMap.TryGetValue(key, out queueInfo!))
                return true;

            // Find the best match. The longest route the has the correct Method and starts the path.
            queueInfo = _routeQueueMap!.Values
                    .Where(x => method.EqualsIgnoreCase(x.Method) && path.StartsWithIgnoreCase(x.Path))
                    .OrderByDescending(x => x.Path.Length)
                    .FirstOrDefault();

            return queueInfo is not null;        
        }

        private static string MakeKey(string path, string method)
        {
            return $"{method.ToLower()}_{path.ToLower()}";
        }

        /// <summary>
        /// Associates a url and command with a queue.
        /// </summary>
        /// <param name="name">The name of the route.</param>
        /// <param name="path">The relative path to use for the request.</param>
        /// <param name="method">The HTTP Method used to call the path/command</param>
        /// <param name="queueName">The name of the queue that the request will be associated with.</param>
        /// <param name="command">The command that will be passed with the payload.</param>
        public void Register(string path, string method, string queueName, string command)
        {
            string key          = MakeKey(path, method);
            var routeInfo       = new RouteQueueInfo(path, method, queueName, command);
            _routeQueueMap[key] = routeInfo;
        }

        /// <summary>
        /// Associates a url and command with a queue.
        /// </summary>
        /// <param name="info">A <cref="ModuleRouteInfo"> structure containing the info to register.</param>
        /// <param name="queueName">The name of the queue that the request will be associated with.</param>
        public void Register(ModuleRouteInfo info, string queueName)
        {
            Register(info.Path, info.Method, queueName, info.Command);
        }
    }
}
