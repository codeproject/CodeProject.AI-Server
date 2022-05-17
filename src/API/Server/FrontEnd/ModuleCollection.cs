using CodeProject.SenseAI.Server.Backend;

using System;
using System.Collections.Generic;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The set of modules for backend processing.
    /// </summary>
    public class ModuleCollection : Dictionary<string, ModuleConfig>
    {
    }

    /// <summary>
    /// Information required to start the backend processes.
    /// </summary>
    public class ModuleConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether this procoess should be activated on startup if
        /// no instruction to the contrary is seen. A default "Start me up" flag.
        /// </summary>
        public bool? Activate { get; set; }

        /// <summary>
        /// Gets or sets the Name to be displayed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the runtime used to execute the file at FilePath. For example, the runtime
        /// could be "dotnet" or "python39". 
        /// </summary>
        public string? Runtime { get; set; }

        /// <summary>
        /// Gets or sets the command to execute the file at FilePath. If set, this overrides Runtime.
        /// An example would be "/usr/bin/python3". This property allows you to specify an explicit
        /// command in case the necessary runtime hasn't been registered, or in case you need to
        /// provide specific flags or naming alternative when executing the FilePath on different
        /// platforms. 
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the path to the startup file relative to the module directory.
        /// </summary>
        /// <remarks>
        /// If no Runtime or Command is specified then a default runtime will be chosen based on
        /// the extension. Currently this is:
        ///     .py  => it will be started with the default Python interpreter
        ///     .dll => it will be started with the .NET runtime.
        /// </remarks>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the time this module was started.
        /// </summary>
        public DateTime? Started { get; set; } = null;

        /// <summary>
        /// Gets or sets the latest time a request from this module was spotted by the queue manager.
        /// </summary>
        public DateTime? LastSeen { get; set; } = null;

        /// <summary>
        /// Gets a value indicating whether this process is currently active
        /// </summary>
        public bool Running
        {
            get
            {
                return LastSeen != null && (DateTime.UtcNow - LastSeen!) < TimeSpan.FromSeconds(65);
            }
        }

        /// <summary>
        /// Gets or sets the number of requests processed
        /// </summary>
        public int? Processed { get; set; } = 0;

        /// <summary>
        /// Gets or sets the name of the configuration value which enables this process.
        /// </summary>
        public string[] EnableFlags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the information to pass to the backend processes.
        /// </summary>
        public Dictionary<string, object>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets a list of RouteMaps.
        /// </summary>
        public BackendRouteInfo[] RouteMaps { get; set; } = Array.Empty<BackendRouteInfo>();

        /// <summary>
        /// Gets or sets the platforms on which this module is supported.
        /// </summary>
        public string[] Platforms { get; set; } = Array.Empty<string>();
    }
}
