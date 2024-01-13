using System;
using System.IO;
using System.Security.Policy;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// Manages the settings for the background process module runner.
    /// </summary>
    public class ModuleSettings
    {
        // marker for path substitution
        const string RootPathMarker                   = "%ROOT_PATH%";
        const string runtimesDirPathMarker            = "%RUNTIMES_PATH%";
        const string PreinstalledModulesDirPathMarker = "%PREINSTALLED_MODULES_PATH%";
        const string modulesDirPathMarker             = "%MODULES_PATH%";
        const string CurrentModuleDirPathMarker       = "%CURRENT_MODULE_PATH%";
        const string PlatformMarker                   = "%PLATFORM%";
        const string OSMarker                         = "%OS%";  
        const string DataDirMarker                    = "%DATA_DIR%";
        const string PythonPathMarker                 = "%PYTHON_PATH%";
        const string PythonDirectoryMarker            = "%PYTHON_DIRECTORY%";

        private readonly ServerOptions           _serverOptions;
        private readonly ModuleOptions           _moduleOptions;
        private readonly ILogger<ModuleSettings> _logger;
        private readonly string?                 _appDataDirectory;
         
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
        /// Gets or sets the root directory that contains the runtimes (eg Python interpreter).
        /// </summary>
        public string? RuntimesDirPath  => _moduleOptions.RuntimesDirPath!;

        /// <summary>
        /// Gets the absolute path to the AI modules that were pre-installed when the server was
        /// setup. For instance, during a Docker image build.
        /// </summary>
        public string PreInstalledModulesDirPath => _moduleOptions.PreInstalledModulesDirPath!;

        /// <summary>
        /// Gets the absolute path to the AI modules that have been downloaded and installed.
        /// </summary>
        public string ModulesDirPath => _moduleOptions.ModulesDirPath!;

        /// <summary>
        /// Gets the absolute path to the download modules zip packages that have been
        /// downloaded from the modules registry
        /// </summary>
        public string DownloadedModulePackagesDirPath => _moduleOptions.DownloadedModulePackagesDirPath!;

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
                if (SystemInfo.IsWindows)
                    return _moduleOptions.ModuleInstallerScriptsDirPath + "\\setup.bat";

                return _moduleOptions.ModuleInstallerScriptsDirPath + "/setup.sh";
            }
        }

        /// <summary>
        /// Adds a module's modulesettings.*.json files to a configuration builder in the correct
        /// order, taking into account the environment and platform.
        /// </summary>
        /// <param name="config">The IConfigurationBuilder object</param>
        /// <param name="moduleDirPath">The directory containing the module</param>
        /// <param name="reloadOnChange">Whether to trigger a reload if the files change</param>
        public static void LoadModuleSettings(IConfigurationBuilder config, string moduleDirPath,
                                              bool reloadOnChange)
        {
            string runtimeEnv      = SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development
                                   ? "development" : string.Empty;
            string os              = SystemInfo.OperatingSystem.ToLower();
            string architecture    = SystemInfo.Architecture.ToLower();
            string deviceSpecifier = string.Empty;
            if (SystemInfo.SystemName == "Raspberry Pi")
                deviceSpecifier = "raspberrypi";
            else if (SystemInfo.SystemName == "Orange Pi")
                deviceSpecifier = "orangepi";
            else if (SystemInfo.SystemName == "Jetson")
                deviceSpecifier = "jetson";

            // modulesettings.json
            // modulesettings.development.json
            // modulesettings.os.json
            // modulesettings.os.development.json
            // modulesettings.os.architecture.json
            // modulesettings.os.architecture.development.json
            // modulesettings.docker.json
            // modulesettings.docker.development.json
            // modulesettings.device_specifier.json     device_specifier = raspberrypi, orangepi, jetson
            // modulesettings.device_specifier.development.json

            string settingsFile = Path.Combine(moduleDirPath, "modulesettings.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{os}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{os}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{os}.{architecture}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{os}.{architecture}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            if (SystemInfo.IsDocker)
            {
                settingsFile = Path.Combine(moduleDirPath, $"modulesettings.docker.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            if (!string.IsNullOrEmpty(deviceSpecifier))
            {
                settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{deviceSpecifier}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

                if (!string.IsNullOrEmpty(runtimeEnv))
                {
                    settingsFile = Path.Combine(moduleDirPath, $"modulesettings.{deviceSpecifier}.{runtimeEnv}.json");
                    config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
                }
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
                              ILogger<ModuleSettings> logger)
        {
            _serverOptions = serverOptions.Value;
            _moduleOptions = moduleOptions.Value;
            _logger        = logger;

            // ApplicationDataDir is set in Program.cs and added to an InMemoryCollection config set.
            _appDataDirectory = config.GetValue<string>("ApplicationDataDir");

            ExpandMacros();
        }

        /// <summary>
        /// Returns a string that represents the path to the current directory a module lives in.
        /// Note that a module's folder is always the same name as its Id.
        /// REVIEW: [Matthew] module.moduleDirPath is set safely and can be used instead of this 
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetModuleDirPath(ModuleBase module)
        {
            if (module.PreInstalled)
                return Path.Combine(PreInstalledModulesDirPath, module.ModuleId!);

            return Path.Combine(ModulesDirPath, module.ModuleId!);
        }

        /// <summary>
        /// Returns a string that represents the working directory for a module.
        /// REVIEW: [Matthew] module.WorkingDirectory is set safely and can be used instead of this if you wish
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetWorkingDirectory(ModuleBase module)
        {
            return GetModuleDirPath(module);
        }

        /// <summary>
        /// Returns a string that represents the command to run to launch a module. Order of 
        /// precedence is the module's Command, the Runtime, and then a guess based on FilePath.
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string? GetCommandPath(ModuleConfig module)
        {
            string? command = ExpandOption(module.Command, GetModuleDirPath(module)) ??
                              GetCommandByRuntime(module) ??
                              GetCommandByFilepath(module);
            return command;
        }

        /// <summary>
        /// Returns a string that represents the absolute path to the file to be launched by a
        /// command to run to launch a module
        /// </summary>
        /// <param name="module">The module to launch</param>
        /// <returns>A string object</returns>
        public string GetFilePath(ModuleConfig module)
        {
            // Correcting for cross platform (win = \, linux = /)
            return Path.Combine(GetModuleDirPath(module), Text.FixSlashes(module.FilePath));
        }

        /// <summary>
        /// Gets the command to run based on the runtime, where the runtime was installed, and the
        /// path to the given module for which the command is to be run
        /// </summary>
        /// <param name="module">The module whose command we're looking to get</param>
        /// <returns>A command that can be run directly on the current OS</returns>
        private string? GetCommandByRuntime(ModuleConfig module)
        {
            if (module is null || module.Runtime is null)
                return null;

            string runtime = module.Runtime.ToLower();
            // _logger.LogTrace($"GetCommandByRuntime: Runtime={runtime}, Location={module.RuntimeLocation}");

            // HACK: Ultimately we will have a set of "runtime" plugins which will install and
            // register the runtimes we use. The registration will include the runtime name
            // (eg "python3.9") and the path to the runtime's launcher. For now we're going to 
            // just hardcode Python and .NET support.

            // If it is "Python" then use our default Python location
            if (runtime == "python" && !string.IsNullOrWhiteSpace(SystemInfo.DefaultPythonVersion))
                runtime = "python" + SystemInfo.DefaultPythonVersion;

            // If it is a Python3X command then replace our marker in the default python path to
            // match the requested interpreter location in order to build the 
            // "/runtimes/bin/linux/python38/venv/bin/python3" path.
            if (runtime.StartsWith("python"))
            {
                // HACK: In Docker, Python installations for downloaded modules can be local for 
                // downloaded modules, or shared for pre-installed modules. For preinstalled modules
                // hardcoded into the Docker image, the python installs and package installs are
                // done at the system level, and not in a virtual environment. This means Python
                // command is in the format of "python3.N" rather than
                // "/runtimes/bin/linux/python3N/venv/bin/python3"
                if (SystemInfo.IsDocker && module.PreInstalled)
                    return runtime;

                // In Docker we don't allow non-pre-installed modules to have shared venv's. Force
                // to Local, because this is where the venv will have been setup by the setup script
                if (SystemInfo.IsDocker && module.RuntimeLocation == "Shared")
                    module.RuntimeLocation = "Local";

                string pythonDir = runtime.Replace(".", "").ToLower();
                string commandPath = _moduleOptions.PythonRelativeInterpreterPath!
                                                   .Replace(PythonDirectoryMarker, pythonDir);
                commandPath = commandPath.TrimStart('\\','/');
                if (module.RuntimeLocation == "Shared")
                    commandPath = Path.Combine(_moduleOptions.RuntimesDirPath!, commandPath);
                else
                    commandPath = Path.Combine(GetModuleDirPath(module), commandPath);

                // Correct the path to handle any path traversals (eg ../) in the path
                if (commandPath?.Contains(Path.DirectorySeparatorChar) ?? false)
                    commandPath = Path.GetFullPath(commandPath);

                return commandPath;
            }

            // Trim the dotnet version. The launcher handles it all for us.
            if (runtime.StartsWith("dotnet"))
                return "dotnet";

            // Everything else is just a straight pass-through (note that 'execute' and 'launcher'
            // are just markers that say 'call the module's file directly - it is runnable')
            if (runtime == "execute" || runtime == "launcher")
                return runtime;

            return null;
        }

        private string? GetCommandByFilepath(ModuleConfig module)
        {
            if (module is null || module.FilePath  is null)
                return null;

            // HACK: Ultimately we will have a set of "runtime" plugins which will install and
            // register the runtimes we use. The registration will include the runtime name
            // (eg "dotnet") and the file extensions that the runtime can unambiguously handle.
            // The "python3.9" runtime, for example, may want to register .py, but so would python3.7.
            // "dotnet" is welcome to register .dll as long as no other runtime module wants .dll too.

            string extension = Path.GetExtension(module.FilePath);
            if (extension == ".py")
                module.Runtime = "python"; // "Generic" python, which will be set to specific version later

            return extension switch
            {
                ".py" => GetCommandByRuntime(module),
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
            // PYTHON_RELATIVE_BASEPATH which usually depends on RUNTIMES_PATH and both depend 
            // on ROOT_PATH.GetProcessStatus and expand each of these in the correct order.

            // _serverOptions.ApplicationRootPath           = GetRootPath(_serverOptions.ApplicationRootPath);

            _moduleOptions.RuntimesDirPath                  = Path.GetFullPath(ExpandOption(_moduleOptions.RuntimesDirPath)!);
            _moduleOptions.ModulesDirPath                   = Path.GetFullPath(ExpandOption(_moduleOptions.ModulesDirPath)!);
            _moduleOptions.PreInstalledModulesDirPath       = Path.GetFullPath(ExpandOption(_moduleOptions.PreInstalledModulesDirPath)!);
            _moduleOptions.DownloadedModulePackagesDirPath  = Path.GetFullPath(ExpandOption(_moduleOptions.DownloadedModulePackagesDirPath)!);
            _moduleOptions.ModuleInstallerScriptsDirPath    = Path.GetFullPath(ExpandOption(_moduleOptions.ModuleInstallerScriptsDirPath)!);

            _moduleOptions.ModuleListUrl                    = ExpandOption(_moduleOptions.ModuleListUrl);
            _moduleOptions.PythonRelativeInterpreterPath    = ExpandOption(_moduleOptions.PythonRelativeInterpreterPath);

            // Correct the slashes
            // _serverOptions.ApplicationRootPath           = Text.FixSlashes(_serverOptions.ApplicationRootPath);

            _moduleOptions.RuntimesDirPath                  = Text.FixSlashes(_moduleOptions.RuntimesDirPath);
            _moduleOptions.ModulesDirPath                   = Text.FixSlashes(_moduleOptions.ModulesDirPath);
            _moduleOptions.PreInstalledModulesDirPath       = Text.FixSlashes(_moduleOptions.PreInstalledModulesDirPath);
            _moduleOptions.ModuleInstallerScriptsDirPath    = Text.FixSlashes(_moduleOptions.ModuleInstallerScriptsDirPath);

            // _moduleOptions.ModuleListUrl                 = Text.FixSlashes(_moduleOptions.ModuleListUrl); - Don't: it will kill "file://"
            _moduleOptions.PythonRelativeInterpreterPath    = Text.FixSlashes(_moduleOptions.PythonRelativeInterpreterPath);

            // _logger.LogInformation($"ROOT_PATH              = {_serverOptions.ApplicationRootPath}");
            _logger.LogInformation($"RUNTIMES_PATH             = {_moduleOptions.RuntimesDirPath}");
            _logger.LogInformation($"PREINSTALLED_MODULES_PATH = {_moduleOptions.PreInstalledModulesDirPath}");
            _logger.LogInformation($"MODULES_PATH              = {_moduleOptions.ModulesDirPath}");
            _logger.LogInformation($"PYTHON_PATH               = {_moduleOptions.PythonRelativeInterpreterPath}");
            _logger.LogInformation($"Data Dir                  = {_appDataDirectory}");
        }

        /// <summary>
        /// Expands the directory markers in the string.
        /// </summary>
        /// <param name="value">The value to expand.</param>
        /// <param name="currentModuleDirPath">The path to the current module, if appropriate.</param>
        /// <returns>The expanded path.</returns>
        public string? ExpandOption(string? value, string? currentModuleDirPath = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            value = value.Replace(runtimesDirPathMarker, _moduleOptions.RuntimesDirPath);
            value = value.Replace(PreinstalledModulesDirPathMarker,  _moduleOptions.PreInstalledModulesDirPath);
            value = value.Replace(modulesDirPathMarker,  _moduleOptions.ModulesDirPath);
            value = value.Replace(RootPathMarker,        CodeProject.AI.Server.Program.ApplicationRootPath);
            value = value.Replace(PlatformMarker,        SystemInfo.Platform.ToLower());
            value = value.Replace(OSMarker,              SystemInfo.OperatingSystem.ToLower());
            value = value.Replace(PythonPathMarker,      _moduleOptions.PythonRelativeInterpreterPath);
            value = value.Replace(DataDirMarker,         _appDataDirectory);

            if (!string.IsNullOrEmpty(currentModuleDirPath))
                value = value.Replace(CurrentModuleDirPathMarker, currentModuleDirPath);

            // Correct for cross platform (win = \, linux = /) *only* for non-URLs. For URLs, we do
            // *not* change the slashes. Note that for file:// URLs for ModuleListUrl, the file disk
            // location will have it's path corrected in the PackageDownloader methods)
            if (!Uri.IsWellFormedUriString(value, UriKind.Absolute) && !value.StartsWithIgnoreCase("file://"))
                value = Text.FixSlashes(value);

            return value;
        }
    }
}
