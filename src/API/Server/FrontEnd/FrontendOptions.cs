
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
        /// Gets or sets the root directory of the Python backend.
        /// </summary>
        public string? APP_ROOT { get; set; }

        /// <summary>
        /// Gets or sets the directory containing the Python 3.7 virtual environment.
        /// </summary>
        public string? PYTHON_DIR { get; set; }

        /// <summary>
        /// Gets or sets the information to start all the backend processes.
        /// </summary>
        public StartupProcess[]? StartupProcesses { get; set; }
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

    /// <summary>
    /// The current version of the FrontEnd.
    /// </summary>
    public class VersionInfo
    {
        /// <summary>
        /// Gets or sets the major version.
        /// </summary>
        public int Major { get; set; }

        /// <summary>
        /// Gets or sets the minor version.
        /// </summary>
        public int Minor { get; set; }

        /// <summary>
        /// Gets or sets the patch number
        /// </summary>
        public int Patch { get; set; }

        /// <summary>
        /// Gets or sets the build number
        /// </summary>
        public int Build { get; set; }

        /// <summary>
        /// Gets or sets the pre-release identifier.
        /// </summary>
        public string? PreRelease { get; set; }

        /// <summary>
        /// Gets a string representation of the version
        /// </summary>
        public string Version
        {
            get
            {
                // https://semver.org/
                string version = $"{Major}.{Minor:00}";

                if (Patch > 0)
                    version += $".{Patch:001}";

                if (!string.IsNullOrWhiteSpace(PreRelease))
                    version += $"-{PreRelease}";

                if (Build > 0)
                    version += $"+{Build:0001}";

                return version;
            }
        }
    }
}
