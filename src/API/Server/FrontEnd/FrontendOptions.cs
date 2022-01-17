
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
        public string? ROOT_DIR { get; set; }

        /// <summary>
        /// Gets or sets the name of the API Directory of the installation.
        /// </summary>
        public string? API_DIRNAME { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the backend modules.
        /// </summary>
        public string? MODULES_DIR { get; set; }

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
        public string? EnableFlag { get; set; }

        /// <summary>
        /// Gets or sets the name of the Queue used by this process.
        /// </summary>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the name of the command to be executed.
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the arguments passed to the command.
        /// </summary>
        public string? Args { get; set; }
    }
}
