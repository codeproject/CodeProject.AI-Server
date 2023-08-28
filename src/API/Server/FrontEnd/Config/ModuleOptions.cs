using System;
using System.Collections.Generic;

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
        /// Gets or sets the timeout for installing a module
        /// </summary>
        public TimeSpan ModuleInstallTimeout { get; set; }

        /// <summary>
        /// The password that must be provided when uploading a new module for installation via
        /// the API.
        /// </summary>
        public string? InstallPassword { get; set; }

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
        /// Gets or sets the root directory that contains the runtimes (eg Python interpreter).
        /// </summary>
        public string? RuntimesPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the modules pre-installed when the server
        /// was setup. For instance, during a Docker image build.
        /// </summary>
        /// <remarks>Modules are usually downloaded and installed in the modulesPAth, but we can
        /// 'pre-install' them in situations like a Docker image. We pre-install modules in a
        /// separate folder than the downloaded and installed modules in order to avoid conflicts 
        /// (in Docker) when a user maps a local folder to the modules dir. Doing this to the 'pre
        /// insalled' dir would make the contents (the preinstalled modules) disappear.</remarks>
        public string? PreInstalledModulesPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the downloaded / sideloaded modules.
        /// </summary>
        public string? ModulesPath { get; set; }

         /// <summary>
        /// Gets the absolute path to the download modules zip packages that have been
        /// downloaded from the modules registry
        /// </summary>
        public string? DownloadedModulePackagesPath {get; set; }

        /// <summary>
        /// Gets or sets the directory that contains the Module Installers.
        /// </summary>
        public string? ModuleInstallerScriptsPath { get; set; }

        /// <summary>
        /// Gets or sets the templated path to the Python interpreter. This path is relative to
        /// the path containing the module or runtimes, depending on whether Python is installed
        /// locally to the mode or shared and is in the runtimes folder. This string may include
        /// a %PYTHON_RUNTIME% marker which will need to be replaced by the specific version of
        /// Python (eg python39).
        /// </summary>
        public string? PythonRelativeInterpreterPath { get; set; }

        /// <summary>
        /// Gets or sets the collection of modules to initially load and versions.
        /// </summary>
        /// <remarks>The KeyValue pair is (ModuleId, Version).</remarks>
        public Dictionary<string, string>? InitialModules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to run initial installs concurrently.
        /// </summary>
        public bool ConcurrentInitialInstalls { get; set; } = false;
    }
}
