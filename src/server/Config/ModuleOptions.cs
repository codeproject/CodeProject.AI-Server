using System;
using System.Collections.Generic;

namespace CodeProject.AI.Server
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
        /// The URL the server uses to get a list of all downloadable models.
        /// </summary>
        public string? ModelListUrl { get; set; }

        /// <summary>
        /// The URL of the location of the models that can be downloaded.
        /// </summary>
        public string? ModelStorageUrl { get; set; }

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
        public string? RuntimesDirPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the modules pre-installed when the server
        /// was setup. For instance, during a Docker image build.
        /// </summary>
        /// <remarks>Modules are usually downloaded and installed in the modulesDirPath, but we can
        /// 'pre-install' them in situations like a Docker image. We pre-install modules in a
        /// separate folder than the downloaded and installed modules in order to avoid conflicts 
        /// (in Docker) when a user maps a local folder to the modules dir. Doing this to the 'pre
        /// installed' dir would make the contents (the preinstalled modules) disappear.</remarks>
        public string? PreInstalledModulesDirPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the downloaded / side-loaded modules.
        /// </summary>
        public string? ModulesDirPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the demo modules.
        /// </summary>
        public string? DemoModulesDirPath { get; set; }

        /// <summary>
        /// Gets or sets the root directory that contains the external modules (modules that aren't
        /// in this solution, but are in external solutions)
        /// </summary>
        public string? ExternalModulesDirPath  { get; set; }

        /// <summary>
        /// Gets the absolute path to the download modules zip packages that have been downloaded
        /// from the modules registry
        /// </summary>
        public string? DownloadedModulePackagesDirPath {get; set; }

        /// <summary>
        /// Gets the absolute path to the download model zip packages that have been downloaded
        /// from the model zoo.
        /// </summary>
        public string? DownloadedModelsPackagesDirPath { get; set; }

        /// <summary>
        /// Gets or sets the directory that contains the Module Installers.
        /// </summary>
        public string? ModuleInstallerScriptsDirPath { get; set; }

        /// <summary>
        /// Gets or sets the templated path to the Python interpreter. This path is relative to
        /// the path containing the module or runtimes, depending on whether Python is installed
        /// locally to the mode or shared and is in the runtimes folder. This string may include
        /// a %PYTHON_NAME% marker which will need to be replaced based on the specific version
        /// of Python (eg for Python3.9 it will be "python39").
        /// </summary>
        public string? PythonRelativeInterpreterPath { get; set; }

        /// <summary>
        /// Gets or sets the collection of modules to initially load and versions.
        /// </summary>
        /// <remarks>  
        /// This needs to be a single value so that lower config levels can override it.
        /// The format is "moduleid:version; moduleid:version; ..." 
        /// with the ":version" optional.
        /// </remarks>
        public string? InitialModules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to run initial installs concurrently.
        /// </summary>
        public bool ConcurrentInitialInstalls { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to install the initial modules.
        /// </summary>
        public bool InstallInitialModules { get; set; } = true;

        /// <summary>
        /// Gets a list of the initial modules to load.
        /// </summary>
        /// <remarks>
        /// The format is "moduleid:version; moduleid:version; ..." 
        /// with the ":version" optional.
        ///  </remarks>
        public List<InitialModule> GetInitialModulesList()
        {
            var initialModuleList = new List<InitialModule>();
            if (InitialModules is not null)
            {
                string[] moduleList = InitialModules.Split(';', 
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var module in moduleList)
                {
                    string[] moduleParts = module.Split(':',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    initialModuleList.Add(new InitialModule
                    {
                        ModuleId = moduleParts[0],
                        Version = (moduleParts.Length == 2) ? moduleParts[1] : string.Empty
                    });
                }
            }

            return initialModuleList;
        }
    }

    /// <summary>
    /// An initial module to load.
    /// </summary>
    public class InitialModule
    {
        /// <summary>
        /// The module id.
        /// </summary>
        public string ModuleId { get; set; } = string.Empty;

        /// <summary>
        /// The version of the module.
        /// </summary>  
        public string Version { get; set; } = string.Empty;
    }
}
