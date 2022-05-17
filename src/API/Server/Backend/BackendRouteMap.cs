using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace CodeProject.SenseAI.Server.Backend
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
        public RouteParameterType Type { get; set; }

        /// <summary>
        /// Get the description of the parameter.
        /// </summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Holds the queue and command associated with a url.
    /// </summary>
    // TODO: this should be a Record.
    public struct BackendRouteInfo
    {
        /// <summary>
        /// Gets the Path for the endpoint.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets the name of the queue used by this endpoint.
        /// </summary>
        public string Queue { get; set; }

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
        /// <param name="Path">The relative path of the endpoint.</param>
        /// <param name="Queue">THe name of the Queue that the route will use.</param>
        /// <param name="Command">The command string that will be passed as part of the data
        /// sent to the queue.</param>
        /// <param name="Description">A Description of the endpoint.</param>
        /// <param name="Inputs">The input parameters information.</param>
        /// <param name="Outputs">The output parameters information.</param>
        public BackendRouteInfo(string Path, string Queue, string Command, 
                                string? Description = null,
                                RouteParameterInfo[]? Inputs = null,
                                RouteParameterInfo[]? Outputs = null)
        {
            this.Path        = Path.ToLower();
            this.Queue       = Queue;
            this.Command     = Command;
            this.Description = Description;
            this.Inputs      = Inputs;
            this.Outputs     = Outputs;
        }
    }

    public class BackendRouteMap
    {
        /// <summary>
        /// Maps a url to the queue and command associated with it.
        /// </summary>
        private ConcurrentDictionary<string, BackendRouteInfo> _urlCommandMap = new();

        /// <summary>
        /// Tries to get the route information for a path.
        /// </summary>
        /// <param name="path">The path to get the information for.</param>
        /// <param name="routeInfo">The BackendRouteInfo instance to store the save the info to.</param>
        /// <returns>True if the path is in the Route Map, false otherwise.</returns>
        public bool TryGetValue(string path, out BackendRouteInfo routeInfo)
        {
            var key = path.ToLower();
            return _urlCommandMap.TryGetValue(key, out routeInfo);
        }

        /// <summary>
        /// Associates a url and command with a queue.
        /// </summary>
        /// <param name="path">The relative path to use for the request.</param>
        /// <param name="queueName">The name of the queue that the request will be associated with.</param>
        /// <param name="command">The command that will be passed with the payload.</param>
        public void Register(string path, string queueName, string command)
        {
            BackendRouteInfo backendRouteInfo     = new BackendRouteInfo(path, queueName, command);
            Register(backendRouteInfo);
        }

        public void Register(BackendRouteInfo info)
        {
            _urlCommandMap[info.Path] = info;
        }

        /// <summary>
        /// Gets a list of the url registrations.
        /// </summary>
        /// <returns>An IEnumerable<KeyValuePair<string, BackendRouteInfo>> of the registrations.</returns>
        public IEnumerable<BackendRouteInfo> List()
        {
            return _urlCommandMap.Values.OrderBy(x => x.Path);
        }
    }
}
