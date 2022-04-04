
using System;
using System.Collections.Generic;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// Options used by the FrontEnd.
    /// </summary>
    public class FrontendOptions
    {
        /// <summary>
        /// Gets or sets the Root Directory of the installation.
        /// </summary>
        public string? ROOT_PATH { get; set; }

        /* These are last resort for helping correct paths...
         
        /// <summary>
        /// Gets or sets the name of the API Directory of the installation. This is only used to
        /// dynamically work up the directory chain to find the API directory in Development mode
        /// </summary>
        public string? APICODE_DIRNAME { get; set; }

        /// <summary>
        /// Gets or sets the name of the Directory in which the API server will be installed in
        /// production. This is used to assess at runtime where the exe is in relation to the root
        /// path.
        /// </summary>
        public string? SERVEREXE_DIRNAME { get; set; }
        */

        /// <summary>
        /// Gets or sets the root directory that contains the backend modules.
        /// </summary>
        public string? MODULES_PATH { get; set; }

        /// <summary>
        /// Gets or sets the base directory for the python interpreters.
        /// </summary>
        public string? PYTHON_BASEPATH { get; set; }

        /// <summary>
        /// Gets or sets the path to the Python 3.7 interpreter
        /// </summary>
        public string? PYTHON37_PATH { get; set; }

        /// <summary>
        /// Gets or sets the information to start all the backend processes.
        /// </summary>
        public StartupProcess[]? StartupProcesses { get; set; }

        /// <summary>
        /// Gets or sets the information to pass to the backend processes.
        /// </summary>
        public Dictionary<string, object>? BackendEnvironmentVariables { get; set; }
    }

    /// <summary>
    /// Information required to start the backend processes.
    /// </summary>
    public class StartupProcess
    {
        /// <summary>
        /// Gets or sets the Name to be displayed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets whether this process is currently running.
        /// </summary>
        public bool? Running { get; set; } = false;

        /// <summary>
        /// Gets or sets the name of the configuration value which enables this process.
        /// </summary>
        public string[] EnableFlags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets a value indicating whether this procoess should be activated on startup if
        /// no instruction to the contrary is seen. A default "Start me up" flag.
        /// </summary>
        public bool? Activate { get; set; }

        /// <summary>
        /// Gets or sets the name of the Queue used by this process.
        /// </summary>
        public string[] Queues { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the name of the command to be executed.
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or set the working directory for this command
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the arguments passed to the command.
        /// </summary>
        public string? Args { get; set; }

        /// <summary>
        /// Gets or sets the platforms on which this module is supported.
        /// </summary>
        public string[] Platforms { get; set; } = Array.Empty<string>();
    }
}
