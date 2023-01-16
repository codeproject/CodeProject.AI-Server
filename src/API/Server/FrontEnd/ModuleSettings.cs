using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using CodeProject.AI.API.Common;
using CodeProject.AI.SDK.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// Manages the settings for the background process module runner.
    /// </summary>
    public class ModuleSettings
    {
        // marker for path substitution
        const string RootPathMarker              = "%ROOT_PATH%";
        const string ModulesPathMarker           = "%MODULES_PATH%";
        const string DownloadedModulesPathMarker = "%DOWNLOADED_MODULES_PATH%";
        const string CurrentModulePathMarker     = "%CURRENT_MODULE_PATH%";
        const string PlatformMarker              = "%PLATFORM%";
        const string OSMarker                    = "%OS%";  
        const string DataDirMarker               = "%DATA_DIR%";
        const string PythonBasePathMarker        = "%PYTHON_BASEPATH%";
        const string PythonPathMarker            = "%PYTHON_PATH%";
        const string PythonRuntimeMarker         = "%PYTHON_RUNTIME%";

        private readonly ServerOptions         _serverOptions;
        private readonly ModuleOptions         _moduleOptions;
        private readonly ILogger<ModuleRunner> _logger;
        private readonly string?               _appDataDirectory;

        /// <summary>
        /// Gets a value indicating whether to launch the backend AI analysis modules.
        /// </summary>
        public bool LaunchModules => _moduleOptions.LaunchModules ?? true;

        /// <summary>
        /// Gets a value indicating the number of seconds to delay the start of launching the 
        /// backend AI modules in order to given the server enough time to properly start up.
        /// </summary>
        public int DelayBeforeLaunchingModulesSecs => _moduleOptions.DelayBeforeLaunchingModulesSecs ?? 0;

        /// <summary>
        /// Gets a value indicating the delay between stopping the background services and passing
        /// control back to the server so it can stop. Ensures modules have time to stop properly
        /// </summary>
        public int DelayAfterStoppingModulesSecs => _moduleOptions.DelayAfterStoppingModulesSecs ?? 3;

        /// <summary>
        /// Gets the absolute path to the downloaded / sideloaded modules.
        /// </summary>
        public string DownloadedModulesPath => _moduleOptions.DownloadedModulesPath!;

        /// <summary>
        /// Gets the absolute path to the pre-installed modules.
        /// </summary>
        public string PreInstalledModulesPath => _moduleOptions.PreInstalledModulesPath!;

        /// <summary>
        /// Gets the absolute path to the download modules zip packages that have been
        /// downloaded from the modules registry
        /// </summary>
        public string DownloadedModulePackagesPath => _moduleOptions.DownloadedModulePackagesPath!;

        /// <summary>
        /// Gets the path to the modules installer script. This will be a batch file or bash file
        /// depending on the current operating system. This script runs from the app's root directory
        /// but has the *modules* directory as the working directory. This script will find the 
        /// module's install script, and run that, initiating the install.
        /// </summary>
        public string ModuleInstallerScriptPath
        {
            get
            {
                if (SystemInfo.OperatingSystem.EqualsIgnoreCase("windows"))
                    return _moduleOptions.ModuleInstallerPath + "\\setup.bat";

                return _moduleOptions.ModuleInstallerPath + "/setup.sh";
            }
        }

        /// <summary>
        /// Gets the directory that is the root of this system. 
        /// </summary>
        /// <param name="configRootPath">The root path specified in the config file.
        /// assumptions</param>
        /// <returns>A string for the adjusted path</returns>
        public static string GetRootPath(string? configRootPath)
        {
            string defaultPath = configRootPath ?? AppContext.BaseDirectory;

            // Correct for cross platform (win = \, linux = /)
            defaultPath = Text.FixSlashes(defaultPath);

            // Either the config file or lets assume it's the current dir if all else fails
            string rootPath = defaultPath;

            // If the config value is a relative path then add it to the current dir. This is where
            // we have to trust the config values are right, and we also have to trust that when
            // this server is called the "ASPNETCORE_ENVIRONMENT" flag is set as necessary in order
            // to ensure the appsettings.Development.json config files are included
            if (rootPath.StartsWith(".."))
                rootPath = Path.Combine(AppContext.BaseDirectory, rootPath!);

            // converts relative URLs and squashes the path to he correct absolute path
            rootPath = Path.GetFullPath(rootPath);

            // HACK: If we're running this server from the build output dir in dev environment
            // then the root path will be wrong.
            if (SystemInfo.IsDevelopment)
            {
                DirectoryInfo? info = new DirectoryInfo(rootPath);
                while (info != null)
                {
                    info = info.Parent;
                    if (info?.Name.ToLower() == "server")
                    {
                        info = info.Parent; // This will be the root in the installed version

                        // For debug / dev environment, the parent is API, followed by src
                        if (info?.Name.ToLower() == "api")
                        {
                            info = info.Parent;
                            if (info?.Name.ToLower() == "src")
                                info = info.Parent;
                            else
                                info = null; // We should have seen "src" for development code
                        }
                        break;
                    }
                }

                if (info != null)
                    rootPath = info.FullName;
            }

            return rootPath;
        }

        /// <summary>
        /// Adds a module's modulesettings.*.json files to a configuration builder in the correct
        /// order, taking into account the environment and platform.
        /// </summary>
        /// <param name="config">The IConfigurationBuilder object</param>
        /// <param name="modulePath">The directory containing the module</param>
        /// <param name="reloadOnChange">Whether to trigger a reload if the files change</param>
        public static void LoadModuleSettings(IConfigurationBuilder config, string modulePath,
                                              bool reloadOnChange)
        {
            string runtimeEnv   = SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development ||
                                  SystemInfo.IsDevelopment ? "development" : string.Empty;
            string os           = SystemInfo.OperatingSystem.ToLower();
            string architecture = SystemInfo.Architecture.ToLower();

            // modulesettings.json
            // modulesettings.development.json
            // modulesettings.os.json
            // modulesettings.os.development.json
            // modulesettings.os.architecture.json
            // modulesettings.os.architecture.development.json
            // modulesettings.docker.json
            // modulesettings.docker.development.json

            string settingsFile = Path.Combine(modulePath, "modulesettings.json");
            if (File.Exists(settingsFile))
                config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(modulePath, $"modulesettings.{runtimeEnv}.json");
                if (File.Exists(settingsFile))
                    config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            settingsFile = Path.Combine(modulePath, $"modulesettings.{os}.json");
            if (File.Exists(settingsFile))
                config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(modulePath, $"modulesettings.{os}.{runtimeEnv}.json");
                if (File.Exists(settingsFile))
                    config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            settingsFile = Path.Combine(modulePath, $"modulesettings.{os}.{architecture}.json");
            if (File.Exists(settingsFile))
                config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(modulePath, $"modulesettings.{os}.{architecture}.{runtimeEnv}.json");
                if (File.Exists(settingsFile))
                    config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }
        }

        /// <summary>
        /// Initialises a new instance of the ModuleSettings.
        /// </summary>
        /// <param name="config">The application configuration.</param>
        /// <param name="serverOptions">The server Options</param>
        /// <param name="moduleOptions">The module Options</param>
        /// <param name="logger">The logger.</param>
        public ModuleSettings(IConfiguration config,
                              IOptions<ServerOptions> serverOptions,
                              IOptions<ModuleOptions> moduleOptions,
                              ILogger<ModuleRunner> logger)
        {
            _serverOptions = serverOptions.Value;
            _moduleOptions = moduleOptions.Value;
            _logger        = logger;

            // ApplicationDataDir is set in Program.cs and added to an InMemoryCollection config set.
            _appDataDirectory = config.GetValue<string>("ApplicationDataDir");

            ExpandMacros();
        }

        /// <summary>
        /// Returns a string that represents the path to the folder that contains the given module's
        /// folder. This will typically be /AnalysisLayer or /modules, depending on how the module
        /// was installed.
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetModuleBasePath(ModuleConfig module)
        {
            return module.PreInstalled ? PreInstalledModulesPath : DownloadedModulesPath;
        }

        /// <summary>
        /// Returns a string that represents the current directory a module lives in.
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetModulePath(ModuleConfig module)
        {
            string modulesBasePath = GetModuleBasePath(module);
            string modulePath = Path.Combine(modulesBasePath, Text.FixSlashes(module.ModulePath));

            return modulePath;
        }

        /// <summary>
        /// Returns a string that represents the current directory a module lives in.
        /// ASSUMPTION: The folder this module will be installed in, and will run from, is named
        /// the same as the module's ID.
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetModulePath(ModuleDescription module)
        {
            string modulesBasePath = DownloadedModulesPath;
            string modulePath      = Path.Combine(modulesBasePath, module.ModuleId!);

            return modulePath;
        }

        /// <summary>
        /// Returns a string that represents the working directory for a module.
        /// </summary>
        /// <remarks>
        /// The working directory isn't necessarily the dir the executed file is in. eg. .NET
        /// exes can be buried deep in /bin/Debug/net6/net6.0-windows. The working directory also
        /// isn't the Module directory, since the actual executable code for a module could be in a
        /// subdirectory of that module. So we start by assuming it's the path where the executed
        /// file is, but allow for an override (in the case of .NET development) if provided.
        /// </remarks>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetWorkingDirectory(ModuleConfig module)
        {
            string workingDir = Text.FixSlashes(module.WorkingDirectory);
            if (string.IsNullOrWhiteSpace(workingDir))
            {
                string filePath = GetFilePath(module);
                workingDir = Path.GetDirectoryName(filePath) ?? string.Empty;
            }
            else
            {
                string modulesBasePath = GetModuleBasePath(module);
                workingDir = Path.Combine(modulesBasePath, workingDir);
            }

            return workingDir;
        }

        /// <summary>
        /// Returns a string that represents the command to run to launch a module
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string? GetCommandPath(ModuleConfig module)
        {
            // Correcting for cross platform (win = \, linux = /)
            string modulesBasePath = GetModuleBasePath(module);
            string modulePath      = Path.Combine(modulesBasePath, Text.FixSlashes(module.ModulePath));
            string? command        = ExpandOption(module.Command, modulePath) ??
                                     GetCommandByRuntime(module.Runtime) ??
                                     GetCommandByExtension(module.FilePath);
            return command;
        }

        /// <summary>
        /// Returns a string that represents the file to be launched by a command to run to launch
        /// a module
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetFilePath(ModuleConfig module)
        {
            // Correcting for cross platform (win = \, linux = /)
            string modulesBasePath = GetModuleBasePath(module);
            string modulePath      = Path.Combine(modulesBasePath, Text.FixSlashes(module.ModulePath));
            string filePath        = Path.Combine(modulePath, Text.FixSlashes(module.FilePath));

            return filePath;
        }

        private string? GetCommandByRuntime(string? runtime)
        {
            if (runtime is null)
                return null;

            runtime = runtime.ToLower();

            // HACK: Ultimately we will have a set of "runtime" modules which will install and
            // register the runtimes we use. The registration will include the runtime name
            // (eg "python39") and the path to the runtime's launcher. For now we're going to 
            // just hardcode Python and .NET support.

            // If it is "Python" then use our default Python location (in this case, python 3.7)
            if (runtime == "python")
                runtime = "python37";

            // If it is a PythonNN command then replace our marker in the default python path to
            // match the requested interpreter location
            if (runtime.StartsWith("python") && !runtime.StartsWith("python3."))
            {
                // HACK: on docker the python command is in the format of python3.N
                string launcher = SystemInfo.ExecutionEnvironment == ExecutionEnvironment.Docker
                                ? runtime.Replace("python3", "python3.") : runtime;
                return _moduleOptions.PythonInterpreterPath?.Replace(PythonRuntimeMarker, launcher);
            }

            if (runtime == "dotnet" || runtime == "execute" || runtime == "launcher")
                return runtime;

            return null;
        }

        private string? GetCommandByExtension(string? filename)
        {
            if (filename is null)
                return null;

            // HACK: Ultimately we will have a set of "runtime" modules which will install and
            // register the runtimes we use. The registration will include the runtime name
            // (eg "dotnet") and the file extensions that the runtime can unambiguously handle.
            // The "python39" runtime, for example, may want to register .py, but so would python37.
            // "dotnet" is welcome to register .dll as long as no other runtime module wants .dll too.

            return Path.GetExtension(filename) switch
            {
                ".py" => GetCommandByRuntime("python"),
                ".dll" => "dotnet",
                ".exe" => "execute",
                _ => throw new Exception("If neither Runtime nor Command is specified then FilePath must have an extension of '.py' or '.dll'."),
            };
        }

        /// <summary>
        /// Expands all the directory markers in the options.
        /// </summary>
        private void ExpandMacros()
        {
            if (_serverOptions is null)
                return;

            // For Macro expansion in appsettings settings we have PYTHON_PATH which depends on
            // PYTHON_BASEPATH which usually depends on MODULES_PATH and both depend on ROOT_PATH.
            // Get and expand each of these in the correct order.

            _serverOptions.ApplicationRootPath     = GetRootPath(_serverOptions.ApplicationRootPath);

            _moduleOptions.PreInstalledModulesPath = Path.GetFullPath(ExpandOption(_moduleOptions.PreInstalledModulesPath)!);
            _moduleOptions.DownloadedModulesPath   = Path.GetFullPath(ExpandOption(_moduleOptions.DownloadedModulesPath)!);
            _moduleOptions.DownloadedModulePackagesPath = Path.GetFullPath(ExpandOption(_moduleOptions.DownloadedModulePackagesPath)!);
            _moduleOptions.ModuleInstallerPath     = Path.GetFullPath(ExpandOption(_moduleOptions.ModuleInstallerPath)!);

            _moduleOptions.PythonBasePath          = Path.GetFullPath(ExpandOption(_moduleOptions.PythonBasePath)!);
            _moduleOptions.PythonInterpreterPath   = ExpandOption(_moduleOptions.PythonInterpreterPath);

            // Corect the slashes
            _serverOptions.ApplicationRootPath     = Text.FixSlashes(_serverOptions.ApplicationRootPath);
            _moduleOptions.PreInstalledModulesPath = Text.FixSlashes(_moduleOptions.PreInstalledModulesPath);
            _moduleOptions.PythonBasePath          = Text.FixSlashes(_moduleOptions.PythonBasePath);
            _moduleOptions.PythonInterpreterPath   = Text.FixSlashes(_moduleOptions.PythonInterpreterPath);
            _moduleOptions.DownloadedModulesPath   = Text.FixSlashes(_moduleOptions.DownloadedModulesPath);
            _moduleOptions.ModuleInstallerPath     = Text.FixSlashes(_moduleOptions.ModuleInstallerPath);

            // Correct the path to handle any path traversals (eg ../) in the path
            if (_moduleOptions.PythonInterpreterPath?.Contains(Path.DirectorySeparatorChar) ?? false)
                _moduleOptions.PythonInterpreterPath = Path.GetFullPath(_moduleOptions.PythonInterpreterPath);

            _logger.LogDebug("------------------------------------------------------------------");
            _logger.LogDebug($"ROOT_PATH               = {_serverOptions.ApplicationRootPath}");
            _logger.LogDebug($"MODULES_PATH            = {_moduleOptions.PreInstalledModulesPath}");
            _logger.LogDebug($"DOWNLOADED_MODULES_PATH = {_moduleOptions.DownloadedModulesPath}");
            _logger.LogDebug($"PYTHON_BASEPATH         = {_moduleOptions.PythonBasePath}");
            _logger.LogDebug($"PYTHON_PATH             = {_moduleOptions.PythonInterpreterPath}");
            _logger.LogDebug($"Temp Dir:                {Path.GetTempPath()}");
            _logger.LogDebug($"Data Dir:                {_appDataDirectory}");
            _logger.LogDebug("------------------------------------------------------------------");
        }

        /// <summary>
        /// Expands the directory markers in the string.
        /// </summary>
        /// <param name="value">The value to expand.</param>
        /// <param name="currentModulePath">The path to the current module, if appropriate.</param>
        /// <returns>The expanded path.</returns>
        public string? ExpandOption(string? value, string? currentModulePath = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            value = value.Replace(ModulesPathMarker,            _moduleOptions.PreInstalledModulesPath);
            value = value.Replace(DownloadedModulesPathMarker,  _moduleOptions.DownloadedModulesPath);
            value = value.Replace(RootPathMarker,               _serverOptions.ApplicationRootPath);
            value = value.Replace(PlatformMarker,               SystemInfo.Platform.ToLower());
            value = value.Replace(OSMarker,                     SystemInfo.OperatingSystem.ToLower());
            value = value.Replace(PythonBasePathMarker,         _moduleOptions.PythonBasePath);
            value = value.Replace(PythonPathMarker,             _moduleOptions.PythonInterpreterPath);
            value = value.Replace(DataDirMarker,                _appDataDirectory);

            if (!string.IsNullOrEmpty(currentModulePath))
                value = value.Replace(CurrentModulePathMarker, currentModulePath);

            // Correct for cross platform (win = \, linux = /)
            value = Text.FixSlashes(value);

            return value;
        }
    }
}
