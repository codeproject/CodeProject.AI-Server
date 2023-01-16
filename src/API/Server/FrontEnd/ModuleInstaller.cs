using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using CodeProject.AI.API.Common;
using CodeProject.AI.API.Server.Frontend.Utilities;
using CodeProject.AI.SDK.Common;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// Manages the install/uninstall/update of modules.
    /// </summary>
    public class ModuleInstaller
    {
        private static List<ModuleDescription>? _lastValidDownloadableModuleList = null;
        private static DateTime _lastDownloadableModuleCheckTime = DateTime.MinValue;  
        private static bool _needsInitialModuleInstalls = false;

        private readonly ModuleSettings           _moduleSettings;
        private readonly ModuleRunner             _moduleRunner;
        private readonly ModuleOptions            _moduleOptions;
        private readonly ILogger<ModuleInstaller> _logger;


        /// <summary>
        /// Informs the installer system that installation of initial modules needs to be completed.
        /// </summary>
        public static void QueueInitialModulesInstallation()
        {
            _needsInitialModuleInstalls = true;
        }

        /// <summary>
        /// Performs the installation of the modules that need to be installed on first run.
        /// </summary>
        public static bool InstallInitialModules()
        {
            if (!_needsInitialModuleInstalls)
                return true;

            _needsInitialModuleInstalls = false;

            return true;
        }

        /// <summary>
        /// Force a reload of the downloadable module list next time it's queried.
        /// </summary>
        public static void RefreshDownloadableModuleList()
        {
            _lastDownloadableModuleCheckTime = DateTime.MinValue;
        }

        /// <summary>
        /// Creates a ModuleDescription object (a description of a downloadable module) from a ModuleConfig 
        /// object (a module's settings file).
        /// </summary>
        /// <param name="module">A ModuleConfig object</param>
        /// <param name="isInstalled">Is this module currently installed?</param>
        /// <returns>A ModuleDescription object</returns>
        public static ModuleDescription ModuleDescriptionFromModuleConfig(ModuleConfig module, bool isInstalled)
        {
            ModuleStatusType status = module.Available(SystemInfo.Platform) 
                                    ? (isInstalled ? ModuleStatusType.Installed : ModuleStatusType.Available)
                                    : ModuleStatusType.NotAvailable;
            
            return new ModuleDescription()
            {
                ModuleId    = module.ModuleId,
                Name        = module.Name,
                Platforms   = module.Platforms,
                Description = module.Description,                
                Version     = module.Version,
                License     = module.License,
                LicenseUrl  = module.LicenseUrl,
                LastUpdated = module.LastUpdated,
                Status      = status,
                CurrentInstalledVersion = module.Version
            };
        }

        /// <summary>
        /// Initialises a new instance of the ModuleInstaller.
        /// </summary>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="moduleRunner">The module runner instance</param>
        /// <param name="moduleOptions">The module Options</param>
        /// <param name="logger">The logger.</param>
        // <param name="config">The application configuration.</param>
        public ModuleInstaller(ModuleSettings moduleSettings,
                               ModuleRunner moduleRunner,
                               IOptions<ModuleOptions> moduleOptions,
                               ILogger<ModuleInstaller> logger)
        {
        
            _moduleSettings = moduleSettings;
            _moduleRunner   = moduleRunner;
            _moduleOptions  = moduleOptions.Value;
            _logger         = logger;
        }

        /// <summary>
        /// Gets a list of the modules available for download.
        /// </summary>
        /// <returns>A List of ModuleDescription objects</returns>
        public async Task<List<ModuleDescription>> GetDownloadableModules()
        {
#if DEBUG
            TimeSpan checkInterval = TimeSpan.FromSeconds(15);
#else
            TimeSpan checkInterval = TimeSpan.FromMinutes(5);
#endif
            List<ModuleDescription>? moduleList = null;

            if (DateTime.Now - _lastDownloadableModuleCheckTime > checkInterval)
            {
                _lastDownloadableModuleCheckTime = DateTime.Now;
                try
                {
                    string downloads = await DownloadPackages.DownloadTextFileAsync(_moduleOptions.ModuleListUrl!);

                    var options = new JsonSerializerOptions { 
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    moduleList = JsonSerializer.Deserialize<List<ModuleDescription>>(downloads, options);

                    if (moduleList is not null)
                    {
                        // HACK: for debug
                        if (_moduleOptions.ModuleListUrl.StartsWithIgnoreCase("file://"))
                        {
                            int basUrlLength = _moduleOptions.ModuleListUrl!.Length - "modules.json".Length;
                            string baseDownloadUrl = _moduleOptions.ModuleListUrl!.Substring(0, basUrlLength);
                            foreach (var module in moduleList)
                                module.DownloadUrl = baseDownloadUrl + $"downloads/{module.ModuleId}-{module.Version}.zip";
                        }
            
                        // Set the status of all entries based on availability on this platform
                        foreach (var module in moduleList)
                        {
                            module.Status = module.Available(SystemInfo.Platform)
                                          ? ModuleStatusType.Available : ModuleStatusType.NotAvailable;
                        }

                        // Update the status to 'Installed' or 'UpdateAvailable' of all modules that are currently running.
                        foreach (ModuleConfig? module in _moduleRunner.Modules.Values)
                        {
                            if (module?.Valid != true)
                                continue;

                            var downloadableModule = moduleList.FirstOrDefault(m => m.ModuleId == module.ModuleId
                                                                                    && m.Status == ModuleStatusType.Available);
                            if (downloadableModule is not null)
                            {
                                downloadableModule.Status                  = ModuleStatusType.Installed;
                                downloadableModule.CurrentInstalledVersion = module.Version;
                                
                                if (VersionInfo.Compare(downloadableModule.Version, module.Version) > 0)
                                    downloadableModule.Status = ModuleStatusType.UpdateAvailable;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error checking for available modules: " + e.Message);
                }
            }

            if (moduleList is null)
            {
                // Fall back to whatever we had before
                moduleList = _lastValidDownloadableModuleList;
            }
            else
            {
                // Go through the our list of modules, and for all modules that are Installed or
                // Available, set the status of each module as what we currently have. We do this
                // because we have just downloaded a new list (otherwise moduleList is null) and
                // we may be updating (eg installing or uninstalling) a module. We should preserve
                // the interim statuseseses.
                foreach (var module in moduleList)
                {
                    // Just check to see if we already have a status (which may have been updated)
                    if (_lastValidDownloadableModuleList is not null)
                    {
                        var existingDescription = _lastValidDownloadableModuleList
                                                     .FirstOrDefault(m => m.ModuleId == module.ModuleId);
                        if (existingDescription is not null && 
                            existingDescription.Status != ModuleStatusType.Unknown &&
                            existingDescription.Status != ModuleStatusType.Uninstalled &&
                            (module.Status == ModuleStatusType.Available       ||
                             module.Status == ModuleStatusType.UpdateAvailable || 
                             module.Status == ModuleStatusType.Installed))
                        {
                            module.Status = existingDescription!.Status;
                        }
                    }
                }

                // Update to the latest and greatest
                if (moduleList is not null)
                    _lastValidDownloadableModuleList = moduleList;                
            }
  
            return moduleList ?? new List<ModuleDescription>();
        }

        /// <summary>
        /// Installs the given module for a particular version.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="version">The version of the module to install</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> InstallModuleAsync(string moduleId, string version)
        {           
            _logger.LogInformation($"Preparing to install module '{moduleId}'");

            if (string.IsNullOrWhiteSpace(moduleId))
                return (false, "No module ID provided");

            // Get downloadable module info
            List<ModuleDescription> moduleList = await GetDownloadableModules();
            ModuleDescription? moduleDownload = moduleList.First(m => m.ModuleId?.EqualsIgnoreCase(moduleId) == true);
            if (moduleDownload is null)
                return (false, $"Unable to find the download info for '{moduleId}'");

            if (!moduleDownload.Valid)
                return (false, $"Module description for '{moduleId}' is invalid");

            // Check we don't have a current or newer version already installed            
            ModuleConfig? module = _moduleRunner.GetModule(moduleId);
            if (module is not null && module.Valid)
            {
                if (VersionInfo.Compare(moduleDownload.Version, module.Version) <= 0)
                    return (false, $"The same, or a newer version, of Module {moduleId} is already installed");

                // If current module is a lower version then uninstall first
                (bool success, string uninstallError) = await UninstallModuleAsync(moduleId);
                if (!success)
                    return (false, $"Unable to uninstall older version of {moduleId}: {uninstallError}");
            }

            // Download and unpack the module's installation package FOR THE REQUESTED VERSION
            string moduleDir     = _moduleSettings.GetModulePath(moduleDownload);
            string downloadPath  = _moduleSettings.DownloadedModulePackagesPath 
                                 + Path.DirectorySeparatorChar + moduleId + "-" + version + ".zip";

            // Console.WriteLine("Setting ModuleStatusType.Downloading");
            moduleDownload.Status = ModuleStatusType.Downloading;
            _logger.LogInformation($"Downloading module '{moduleId}'");

            bool downloaded = false;
            string error = string.Empty;

            if (System.IO.File.Exists(downloadPath))
            {
                _logger.LogInformation($" (using cached download for '{moduleId}')");
                downloaded = true;               
            }
            else
            {
                (downloaded, error) = await DownloadPackages.DownloadFileAsync(moduleDownload.DownloadUrl!,
                                                                               downloadPath);
            }

            if (downloaded && !System.IO.File.Exists(downloadPath))
            {
                downloaded = false;
                error      = "Module was downloaded but was not saved";
            }

            if (!downloaded)
            {
                // Console.WriteLine("Setting ModuleStatusType.Unknown");
                moduleDownload.Status = ModuleStatusType.Unknown;
                return (false, $"Unable to download module '{moduleId}' from {moduleDownload.DownloadUrl}. Error: {error}");
            }

            moduleDownload.Status = ModuleStatusType.Unpacking;
            if (!DownloadPackages.Extract(downloadPath, moduleDir, out var _))
            {
                // Console.WriteLine("Setting ModuleStatusType.Unknown");
                moduleDownload.Status = ModuleStatusType.Unknown;
                return (false, $"Unable to unpack module '{moduleId}'");
            }

            // Run the install script
            if (!System.IO.File.Exists(_moduleSettings.ModuleInstallerScriptPath))
            {
                moduleDownload.Status = ModuleStatusType.Unknown;
                return (false, $"Module '{moduleId}' install script not found");
            }

            _logger.LogInformation($"Installing module '{moduleId}'");
            // Console.WriteLine("Setting ModuleStatusType.Installing");
            moduleDownload.Status = ModuleStatusType.Installing;

            ProcessStartInfo procStartInfo;
            if (SystemInfo.OperatingSystem.EqualsIgnoreCase("Windows"))
                procStartInfo = new ProcessStartInfo(_moduleSettings.ModuleInstallerScriptPath, "--no-color");
            else
                procStartInfo = new ProcessStartInfo("bash", _moduleSettings.ModuleInstallerScriptPath + " --no-color");

            procStartInfo.UseShellExecute        = false;
            procStartInfo.WorkingDirectory       = moduleDir;
            procStartInfo.CreateNoWindow         = false;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError  = true;

            var process = new Process();
            process.StartInfo = procStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited             += ModuleInstallComplete;
            process.OutputDataReceived += SendOutputToLog;
            process.ErrorDataReceived  += SendErrorToLog;

            try
            {
                if (process.Start())
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                else
                {
                    // Console.WriteLine("Setting ModuleStatusType.FailedInstall");
                    moduleDownload.Status = ModuleStatusType.FailedInstall;
                    return (false, $"Unable to start Module '{moduleId}'");
                }
            }
            catch (Exception e)
            {
                // Console.WriteLine("Setting ModuleStatusType.FailedInstall");
                moduleDownload.Status = ModuleStatusType.FailedInstall;
                return (false, $"Unable to start Module '{moduleId}' (${e.Message})");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Uninstalls the given module.
        /// </summary>
        /// <param name="moduleId">The module to uninstall</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> UninstallModuleAsync(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return (false, "No module ID provided");

            if (_moduleRunner is null)
                return (false, "Unable to locate analysis module runner service");

            ModuleConfig? module = _moduleRunner.GetModule(moduleId);
            if (module is null)
                return (false, $"Unable to find module {moduleId}");

            // Get the module's directory
            string moduleDir = _moduleSettings.GetModulePath(module);
            if (!Directory.Exists(moduleDir))
                return (false, $"Unable to find {moduleId}'s install directory");

            // Get downloadable module info
            List<ModuleDescription> moduleList = await GetDownloadableModules();
            ModuleDescription? moduleDownload = moduleList.First(m => m.ModuleId?.EqualsIgnoreCase(moduleId) == true);

            // If the module to be uninstalled is no longer a download, create an entry and add it to the download list
            // so at least we can provide updates on it disappearing.
            if (moduleDownload is null)
                moduleDownload = ModuleInstaller.ModuleDescriptionFromModuleConfig(module, true);

            if (moduleDownload is null)
                return (false, $"Unable to find the download info for '{moduleId}'");

            Console.WriteLine("Setting ModuleStatusType.Uninstalling");
            moduleDownload.Status = ModuleStatusType.Uninstalling;

            if (!await _moduleRunner.KillProcess(module))
            {
                Console.WriteLine("Setting ModuleStatusType.Unknown");
                moduleDownload.Status = ModuleStatusType.Unknown;
                ModuleInstaller.RefreshDownloadableModuleList();

                return (false, $"Unable to kill {moduleId}'s process");
            }

            try
            {
                Directory.Delete(moduleDir, true);
                Console.WriteLine("Setting ModuleStatusType.Uninstalled");
                moduleDownload.Status = ModuleStatusType.Available;
            }
            catch (Exception e)
            {
                Console.WriteLine("Setting ModuleStatusType.UninstallFailed");
                moduleDownload.Status = ModuleStatusType.UninstallFailed;
                ModuleInstaller.RefreshDownloadableModuleList();
                
                return (false, $"Unable to delete install folder for {moduleId} ({e.Message})");
            }

            if (Directory.Exists(moduleDir)) // shouldn't actually be possible to get here if delete failed
            {
                Console.WriteLine("Setting ModuleStatusType.UninstallFailed");
                moduleDownload.Status = ModuleStatusType.UninstallFailed;
                ModuleInstaller.RefreshDownloadableModuleList();

                return (false, $"Unable to delete install folder for {moduleId}");
            }

            if (_moduleRunner.HasModule(moduleId) && !_moduleRunner.RemoveModule(moduleId))
            {
                moduleDownload.Status = ModuleStatusType.UninstallFailed;
                ModuleInstaller.RefreshDownloadableModuleList();
                
                return (false, "Unable to remove module from module list");
            }

            // Force an immediate reload
            ModuleInstaller.RefreshDownloadableModuleList();

            return (true, string.Empty);
        }

        private void SendOutputToLog(object sender, DataReceivedEventArgs data)
        {
            string? message = data?.Data;

            if (!string.IsNullOrWhiteSpace(message))
                _logger.LogInformation(message);
        }

        private void SendErrorToLog(object sender, DataReceivedEventArgs data)
        {
            string? message = data?.Data;

            if (!string.IsNullOrWhiteSpace(message))
                _logger.LogError(message);
        }

        /// <summary>
        /// This is called once the mmodule's install script has completed
        /// </summary>
        /// <param name="sender">The process</param>
        /// <param name="e">The event args</param>
        private async void ModuleInstallComplete(object? sender, EventArgs e)
        {
            if (sender is Process process)
            {
                string directory = process.StartInfo.WorkingDirectory;
                string? moduleId = new DirectoryInfo(directory).Name;
                if (moduleId is null)
                {
                    _logger.LogError("Module install complete, but can't find the installed module");
                    return;
                }

                // Get downloadable module info
                List<ModuleDescription> moduleList = await GetDownloadableModules();
                ModuleDescription? moduleDownload = moduleList.First(m => m.ModuleId?.EqualsIgnoreCase(moduleId) == true);
                if (moduleDownload is null)
                {
                    _logger.LogError("Unable to find recently installed module in downloadable module list");
                    return;
                }

                // Console.WriteLine("Setting ModuleStatusType.Installed");
                moduleDownload.Status = ModuleStatusType.Installed;
                _logger.LogInformation($"Module {moduleDownload.Name} ({moduleDownload.ModuleId})  installed successfully.");

                string moduleDir = _moduleSettings.DownloadedModulesPath
                                 + Path.DirectorySeparatorChar + moduleId;

                // Load up the module's settings and start the module
                var config = new ConfigurationBuilder();
                ModuleSettings.LoadModuleSettings(config, moduleDir, false);
                IConfiguration configuration  = config.Build();

                var moduleConfig = new ModuleConfig();
                configuration.Bind($"Modules:{moduleId}", moduleConfig);
                moduleConfig.ModuleId = moduleId;
                if (moduleConfig.Valid)
                {
                    _moduleRunner.Modules.TryAdd(moduleId, moduleConfig);
                    if (await _moduleRunner.StartProcess(moduleConfig))
                        _logger.LogInformation($"Module {moduleDownload.ModuleId} started successfully.");
                    else
                        _logger.LogError($"Unable to start newly installed Module {moduleDownload.ModuleId}.");
                }
                else
                    _logger.LogError($"Config for {moduleDownload.ModuleId} is invalid. Unable to start.");
            }
        }        
    }
}
