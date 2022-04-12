using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace CodeProject.SenseAI.Server.Backend
{

    /// <summary>
    /// Holds the queue and command associated with a url.
    /// </summary>
    public struct BackendRouteInfo
    {
        /// <summary>
        /// Gets the name of the queue.
        /// </summary>
        public string Queue { get; }

        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string Command { get; }

        /// <summary>
        /// Initializes a new instance of the BackendRouteInfo struct.
        /// </summary>
        /// <param name="queueName">THe name of the Queue that the route will use.</param>
        /// <param name="command">The command string that will be passed as part of the data
        /// sent to the queue.</param>
        public BackendRouteInfo(string queueName, string command)
        {
            Queue   = queueName;
            Command = command;
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
            _urlCommandMap[path.ToLower()] = new BackendRouteInfo(queueName, command);
        }
    }
}
