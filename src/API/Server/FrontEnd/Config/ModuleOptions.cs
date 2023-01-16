namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// Options used by the Module runner.
    /// </summary>
    public class ModuleOptions
    {
        /// <summary>
        /// The URL the server uses to get a list of all downloadable modules.
        /// </summary>
        public string? ModuleListUrl { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to launch the backend AI analysis modules.
        /// </summary>
        public bool? LaunchModules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the number of seconds to delay the start of launching 
        /// the backend AI modules in order to given the server enough time to properly start up.
        /// </summary>
        public int? DelayBeforeLaunchingModulesSecs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the delay between stopping the background services and 
        /// passing control back to the server so it can stop. Ensures modules have time to stop
        /// properly
        /// </summary>
        public int? DelayAfterStoppingModulesSecs { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the pre-installed backend modules.
        /// </summary>
        public string? PreInstalledModulesPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the downloaded / sideloaded modules.
        /// </summary>
        public string? DownloadedModulesPath { get; set; }

         /// <summary>
        /// Gets the absolute path to the download modules zip packages that have been
        /// downloaded from the modules registry
        /// </summary>
        public string? DownloadedModulePackagesPath {get; set; }

        /// <summary>
        /// Gets or sets the directory that contains the Module Installers.
        /// </summary>
        public string? ModuleInstallerPath { get; set; }

        /// <summary>
        /// Gets or sets the base directory for the python interpreters.
        /// </summary>
        public string? PythonBasePath { get; set; }

        /// <summary>
        /// Gets or sets the tamplated path to the Python interpreter. This path
        /// may include a %PYTHON_RUNTIME% marker which will need to be replaced.
        /// </summary>
        public string? PythonInterpreterPath { get; set; }
    }
}
