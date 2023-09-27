using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Utilities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// Manages the install/uninstall/update of modules.
    /// </summary>
    public class ModuleInstaller
    {
        private const string                      _installLogFileName              = "install.log";

        private static List<ModuleDescription>?   _lastValidDownloadableModuleList = null;
        private readonly static Semaphore         _moduleListSemaphore             = new Semaphore(initialCount: 1, maximumCount: 1);
        private static DateTime                   _lastDownloadableModuleCheckTime = DateTime.MinValue;
        private static bool                       _needsInitialModuleInstalls      = false;

        private readonly VersionConfig            _versionConfig;
        private readonly ModuleSettings           _moduleSettings;
        private readonly ModuleProcessServices    _moduleProcessService;
        private readonly PackageDownloader        _packageDownloader;
        private readonly ModuleCollection         _moduleCollection;
        private readonly ModuleOptions            _moduleOptions;
        private readonly ServerOptions            _serverOptions;
        private readonly ILogger<ModuleInstaller> _logger;

        /// <summary>
        /// Initialises a new instance of the AiModuleInstaller.
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="serverOptions">The server options</param>
        /// <param name="moduleCollection">The module collection instance.</param>
        /// <param name="moduleOptions">The module Options</param>
        /// <param name="moduleProcessServices">The moduleProcessServices.</param>
        /// <param name="packageDownloader">The Package Downloader.</param>
        /// <param name="logger">The logger.</param>
        public ModuleInstaller(IOptions<VersionConfig> versionOptions,
                                 ModuleSettings moduleSettings,
                                 IOptions<ServerOptions> serverOptions,
                                 IOptions<ModuleCollection> moduleCollection,
                                 IOptions<ModuleOptions> moduleOptions,
                                 ModuleProcessServices moduleProcessServices,
                                 PackageDownloader packageDownloader,
                                 ILogger<ModuleInstaller> logger)
        {
            _versionConfig        = versionOptions.Value;
            _moduleSettings       = moduleSettings;
            _serverOptions        = serverOptions.Value;
            _moduleCollection     = moduleCollection.Value;
            _moduleOptions        = moduleOptions.Value;
            _moduleProcessService = moduleProcessServices;
            _packageDownloader    = packageDownloader;
            _logger               = logger;
        }

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
        public async Task<bool> InstallInitialModules()
        {
            if (!_needsInitialModuleInstalls)
                return true;

            // Having this code prevents accidentally installing and overwriting current modules, 
            // but also prevents us from testing. Far better to comment out the 
            // if (SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development)
            // {
            //     _logger.LogInformation($"Can't install Modules when running in Development");
            //     return false;
            // }

            // Just because we need at least one await
            await Task.Delay(1).ConfigureAwait(false);

            // Add the initial installed tasks here
            // eg var result = await InstallModuleAsync("TextSummary", "1.1");

            if (_moduleOptions.InitialModules?.Any() ?? false)
            {
                _logger.LogInformation($"** Setting up initial modules. Please be patient...");

                if (!_moduleOptions.ConcurrentInitialInstalls)
                {
                    foreach (var idVersion in _moduleOptions.InitialModules)
                    {
                        try
                        {
                            _logger.LogInformation($"** Installing initial module {idVersion.Key}.");

                            var downloadTask = DownloadAndInstallModuleAsync(idVersion.Key, idVersion.Value);
                            (bool success, string error) = await downloadTask.ConfigureAwait(false);
                            if (!success)
                                _logger.LogInformation($"Unable to install {idVersion.Key}: " + error);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Exception during DownloadAndInstallModuleAsync({idVersion.Key}, {idVersion.Value})");
                        }
                    }
                }
                else
                {
                    List<Task<(bool success, string message)>> installTasks = new();
                    foreach (var idVersion in _moduleOptions.InitialModules)
                    {
                        try
                        {
                            _logger.LogInformation($"** Installing initial module {idVersion.Key}.");
                            installTasks.Add(DownloadAndInstallModuleAsync(idVersion.Key, idVersion.Value));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Exception during DownloadAndInstallModuleAsync({idVersion.Key}, {idVersion.Value})");
                        }
                    }

                    foreach (var task in installTasks)
                    {
                        try
                        {
                            var result = await task.ConfigureAwait(false);
                            if (!result.success)
                                _logger.LogError(result.message ?? "Unknown Error Installing Initial Modules.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception during InstallInitialModules");
                        }
                    }
                }
            }

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
        /// Creates a ModuleDescription object (a description of a downloadable module) from a
        /// ModuleConfig object (a module's settings file). We have this method is in order to
        /// backfill our list of ModuleDescription we get from the list modules service from
        /// CodeProject.com. A module may have been removed from that list, or never added (if
        /// a module was side-loaded privately). In order to manage (in this case, uninstall)
        /// such a module it will need to appear on a list of modules. We have the module's
        /// ModuleConfig (it's installed and maybe even running!) so build a ModuleDescription
        /// from the ModuleConfig.
        /// </summary>
        /// <param name="module">A ModuleConfig object</param>
        /// <param name="isInstalled">Is this module currently installed?</param>
        /// <param name="serverVersion">The version of the current server, or null to ignore
        /// version checks</param>
        /// <param name="modulesPath">The absolute path to the folder containing all downloaded and
        /// installed modules</param>
        /// <param name="preInstalledModulesPath">The absolute path to the folder containing all
        /// pre-installed modules</param>
        /// <returns>A ModuleDescription object</returns>
        public static ModuleDescription ModuleDescriptionFromModuleConfig(ModuleConfig module,
                                                                          bool isInstalled,
                                                                          string serverVersion,
                                                                          string modulesPath,
                                                                          string preInstalledModulesPath)
        {
            var moduleDescription = new ModuleDescription()
            {
                ModuleId       = module.ModuleId,
                Name           = module.Name,
                Version        = module.Version,

                Description    = module.Description,                
                Platforms      = module.Platforms,
                License        = module.License,
                LicenseUrl     = module.LicenseUrl,

                PreInstalled   = module.PreInstalled,
                ModuleReleases = module.ModuleReleases
            };

            // Set initial properties. Most importantly it sets the status. 
            moduleDescription.Initialise(serverVersion, modulesPath, preInstalledModulesPath);

            // if a module is installed then that beats any other status
            if (isInstalled)
                moduleDescription.Status = ModuleStatusType.Installed;

            return moduleDescription;
        }

        /// <summary>
        /// Gets a list of the modules available for download.
        /// </summary>
        /// <returns>A ListProcessStatuses of ModuleDescription objects</returns>
        /// <remarks>The basic idea here is we want to get a list of downloadable modules, but we
        /// want that list to reflect the current state of the system as well. By this we mean that
        /// if we download a list of modules and some are not available for install, or some are in
        /// the process of being installed or uninstalled, then our list of downloadable modules
        /// should reflect this. Easy, but... 
        /// 1. We need to cache the downloadable list. Asking for it too often is a bad idea.
        /// 2. When we get an updated list we need to ensure that the statuses of each module are
        ///    accurate. The most accurate data is actually in the list we return, so we need to
        ///    transfer info from the old list to the newly downloaded list each time we fetch an
        ///    updated list.
        /// </remarks>
        public async Task<List<ModuleDescription>> GetInstallableModules()
        {
#if DEBUG
            TimeSpan checkInterval = TimeSpan.FromSeconds(15);
#else
            TimeSpan checkInterval = TimeSpan.FromMinutes(5);
#endif
            List<ModuleDescription>? moduleList = null;

            _moduleListSemaphore.WaitOne();
            try
            {
                if (_serverOptions.AllowInternetAccess != false &&
                    (DateTime.Now - _lastDownloadableModuleCheckTime > checkInterval ||
                     _lastValidDownloadableModuleList is null))
                {
                    _lastDownloadableModuleCheckTime = DateTime.Now;

                    // Download the list of downloadable modules as a JSON string, then deserialise
                    string downloads = await _packageDownloader.DownloadTextFileAsync(_moduleOptions.ModuleListUrl!)
                                                               .ConfigureAwait(false);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    moduleList = JsonSerializer.Deserialize<List<ModuleDescription>>(downloads, options);

                    // Initialise each module description
                    if (moduleList is not null)
                    {
                        // HACK: for debug
                        if (_moduleOptions.ModuleListUrl.StartsWithIgnoreCase("file://"))
                        {
                            int basUrlLength = _moduleOptions.ModuleListUrl!.Length - "modules.json".Length;
                            string baseDownloadUrl = _moduleOptions.ModuleListUrl![..basUrlLength];
                            if (baseDownloadUrl == "file://")
                                baseDownloadUrl = _moduleSettings.DownloadedModulePackagesPath;
                            foreach (var module in moduleList)
                                module.DownloadUrl = baseDownloadUrl + Path.DirectorySeparatorChar + $"{module.ModuleId}-{module.Version}.zip";
                        }

                        string currentServerVersion = _versionConfig.VersionInfo?.Version ?? string.Empty;
                        foreach (var module in moduleList)
                        {
                            module.Initialise(currentServerVersion, _moduleSettings.ModulesPath,
                                              _moduleSettings.PreInstalledModulesPath);
                        }

                        // Update the status to 'Installed' or 'UpdateAvailable' for all listed
                        // modules that we are currently running.
                        foreach (ModuleConfig? module in _moduleCollection.Values)
                        {
                            if (module?.Valid != true)
                                continue;

                            // Find module (a module we're currently running) in the list of 
                            // downloadable modules.
                            var downloadableModule = moduleList.FirstOrDefault(m => m.ModuleId == module.ModuleId
                                                                               && m.Status == ModuleStatusType.Available);
                            if (downloadableModule is not null)
                            {
                                downloadableModule.Status = ModuleStatusType.Installed;

                                if (VersionInfo.Compare(downloadableModule.Version, module.Version) > 0)
                                    downloadableModule.Status = ModuleStatusType.UpdateAvailable;
                            }
                        }
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
                        if (_lastValidDownloadableModuleList is not null &&
                            (module.Status == ModuleStatusType.Available ||
                             module.Status == ModuleStatusType.UpdateAvailable ||
                             module.Status == ModuleStatusType.Installed))
                        {
                            var existingDescription = _lastValidDownloadableModuleList
                                                            .FirstOrDefault(m => m.ModuleId == module.ModuleId);
                            if (existingDescription is not null)
                            {
                                if (existingDescription.Status == ModuleStatusType.UninstallFailed)
                                {
                                    // If the uninstall failed but ultimately the module's dir was
                                    //  emptied, then mark it as done.
                                    string moduleDir = _moduleSettings.GetModulePath(existingDescription);
                                    if (!Directory.Exists(moduleDir) ||
                                        !Directory.EnumerateFileSystemEntries(moduleDir).Any())
                                    {
                                        existingDescription.Status = ModuleStatusType.Uninstalled;
                                        module.Status              = ModuleStatusType.Available;
                                    }
                                }

                                if (existingDescription.Status != ModuleStatusType.Unknown         &&
                                    existingDescription.Status != ModuleStatusType.UpdateAvailable &&
                                    existingDescription.Status != ModuleStatusType.Uninstalled)
                                {
                                    module.Status = existingDescription!.Status;
                                }
                            }
                        }
                    }

                    // Update to the latest and greatest
                    if (moduleList is not null)
                        _lastValidDownloadableModuleList = moduleList;
                }
            }
            catch (Exception /*e*/)
            {
                // _logger.LogError($"Error checking for available modules: " + e.Message);
            }
            finally
            {
                _moduleListSemaphore.Release();
            }
  
            return moduleList ?? new List<ModuleDescription>();
        }

        /// <summary>
        /// Downloads and Installs the given module for a particular version.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="version">The version of the module to install</param>
        /// <param name="noCache">Whether or not to ignore the download cache. If true, the module
        /// will always be freshly downloaded</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> DownloadAndInstallModuleAsync(string moduleId, 
                                                                        string version,
                                                                        bool noCache = false)
        {           
            if (string.IsNullOrWhiteSpace(moduleId))
                return (false, "No module ID provided");

            _logger.LogInformation($"Preparing to install module '{moduleId}'");

            ModuleDescription? moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);
            if (moduleDownload is null)
                return (false, $"Unable to find the download info for '{moduleId}'");

            if (!moduleDownload.Valid)
                return (false, $"Module description for '{moduleId}' is invalid");

            // If no version specified, download the latest and greatest
            if (string.IsNullOrWhiteSpace(version))
                version = moduleDownload.Version!;

            // Check we don't have a current or newer version already installed            
            ModuleConfig? module = _moduleCollection.GetModule(moduleId);

            // Sanity check
            if (module is not null && module.PreInstalled)
                return (false, $"Module description for '{moduleId}' is invalid. A 'pre-installed' module can't be downloaded");

            if (module is not null && module.Valid && moduleDownload.Status == ModuleStatusType.Installed)
            {
                if (VersionInfo.Compare(moduleDownload.Version, module.Version) <= 0)
                    return (false, $"{moduleId} is already installed");

                // If current module is a lower version then uninstall first
                (bool success, string uninstallError) = await UninstallModuleAsync(moduleId).ConfigureAwait(false);
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

            if (!noCache && System.IO.File.Exists(downloadPath))
            {
                _logger.LogInformation($" (using cached download for '{moduleId}')");
                downloaded = true;               
            }
            else
            {
                (downloaded, error) = await _packageDownloader.DownloadFileAsync(moduleDownload.DownloadUrl!, downloadPath)
                                                              .ConfigureAwait(false);
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

            return await InstallModuleAsync(downloadPath, moduleId).ConfigureAwait(false);
        }

        /// <summary>
        /// Installs the module in stored in given file.
        /// </summary>
        /// <param name="installPackagePath">The path to the installer zip package</param>
        /// <param name="moduleId">The module to install</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> InstallModuleAsync(string installPackagePath, string? moduleId)
        {
            ModuleDescription? moduleDownload = null;
            string? moduleDir                 = null;

            // A module that was uploaded via the API won't have a moduleID provided. It will be in
            // the modulesettings.json file in the module's install package.
            bool uploadedModule = string.IsNullOrWhiteSpace(moduleId);

            // If we do not know the module we're installing then base filenames on the filepath
            // until we can determine the actual module Id. Otherwise, if we know the module then
            // we can update its progress now. 
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                string tempName = Path.GetFileNameWithoutExtension(installPackagePath);
                moduleDir = Path.Combine(_moduleSettings.ModulesPath, Text.FixSlashes(tempName));
            }
            else
            {
                moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);    
                if (moduleDownload is not null)
                {
                    moduleDownload.Status = ModuleStatusType.Unpacking;
                    moduleDir = _moduleSettings.GetModulePath(moduleDownload);
                }
            }

            if (string.IsNullOrWhiteSpace(moduleDir))
                return (false, $"Unable to determine module directory for '{installPackagePath}'");

            bool extracted = _packageDownloader.Extract(installPackagePath, moduleDir!, out var _);
    
            if (SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                DeletePackageFile(installPackagePath);

            if (!extracted)
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Unknown;
                    
                return (false, $"Unable to unpack module in '{installPackagePath}'");
            }

            /* - Doesn't yet work but the vague idea here is to use the same method we use
                 to load modulesettings files. Using this method means we handle all the
                 possible modulesettings.*.json names, meaning only one has to exist for this
                 to work. Doing it manually means we are assuming a modulesettings.json always
                 exists.

            var config = new ConfigurationBuilder();
            ModuleSettings.LoadModuleSettings(config, moduleDir, false);
            IConfiguration configuration  = config.Build();
            var moduleCollection = new ModuleCollection();
            configuration.Bind($"Modules", moduleCollection);
            var settings = configuration.GetSection("Modules");
            */

            string? settingsModuleId = null;
            try
            {
                string content = await File.ReadAllTextAsync(Path.Combine(moduleDir, "modulesettings.json"))
                                           .ConfigureAwait(false);

                var documentOptions = new JsonDocumentOptions
                {
                    CommentHandling     = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var jsonSettings = JsonDocument.Parse(content, documentOptions).RootElement;
                var jsonModules  = jsonSettings.EnumerateObject().FirstOrDefault();
                var jsonModule   = jsonModules.Value.EnumerateObject().FirstOrDefault();
                settingsModuleId = jsonModule.Name;
            }
            catch
            {
                if (SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                    DeletePackageDirectory(moduleDir);

                return (false, $"Unable to load module configuration from '{installPackagePath}'");
            }

            if (string.IsNullOrWhiteSpace(settingsModuleId))
            {
                if (uploadedModule && SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                    DeletePackageDirectory(moduleDir);

                return (false, $"Unable to read module Id from settings in '{installPackagePath}'");
            }

            // If no module Id was passed to this method then now is the time to move this anonymous
            // installation folder into its final home. ASSUMING a module of this Id doesn't already
            // exist.
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                if (_moduleCollection.ContainsKey(settingsModuleId))
                {
                    DeletePackageDirectory(moduleDir);
                    return (false, $"A module of id {settingsModuleId} has already been installed. Please uninstall before uploading again.");
                }

                string newModuleDir = Path.Combine(_moduleSettings.ModulesPath, Text.FixSlashes(settingsModuleId));
                Directory.Move(moduleDir, newModuleDir);

                moduleId  = settingsModuleId;
                moduleDir = newModuleDir;
            }
            else
            {
                // Not strictly necessary, but probably a good idea
                if (!moduleId.EqualsIgnoreCase(settingsModuleId))
                    return (false, $"The module to install ({settingsModuleId}) has a different module ID than was specified ({moduleId}). Quitting.");
            }

            // Run the install script
            if (!File.Exists(_moduleSettings.ModuleInstallerScriptPath))
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Unknown;

                return (false, $"Module '{moduleId}' install script not found");
            }

            _logger.LogInformation($"Installing module '{moduleId}'");
            _logger.LogDebug($"Installer script at '{_moduleSettings.ModuleInstallerScriptPath}'");

            if (moduleDownload is not null)
                moduleDownload.Status = ModuleStatusType.Installing;

            ProcessStartInfo procStartInfo;
            if (SystemInfo.IsWindows)
                procStartInfo = new ProcessStartInfo(_moduleSettings.ModuleInstallerScriptPath);
            else if (SystemInfo.IsMacOS)
                procStartInfo = new ProcessStartInfo("bash", '"' + _moduleSettings.ModuleInstallerScriptPath + '"');
            else
                procStartInfo = new ProcessStartInfo("bash", _moduleSettings.ModuleInstallerScriptPath);

            // Don't stop the colour! We use the colours in the dashboard, and will strip them for
            // our log output.
            // procStartInfo.Arguments           = "--no-color";
            procStartInfo.UseShellExecute        = false;
            procStartInfo.WorkingDirectory       = moduleDir;
            procStartInfo.CreateNoWindow         = false;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError  = true;

            // Setup installer process
            using var process = new Process();
            process.StartInfo           = procStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited             += ModuleInstallComplete;

            // Setup log file
            using StreamWriter logWriter = File.AppendText(Path.Combine(moduleDir, _installLogFileName));

            // Setup handling of install script output
            process.OutputDataReceived += (object s, DataReceivedEventArgs e) => SendOutputToLog(logWriter, s, e);
            process.ErrorDataReceived  += (object s, DataReceivedEventArgs e) => SendErrorToLog(logWriter, s, e);

            try
            {
                if (process.Start())
                {
                    // Capture the process output for logging.
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Wait for the Process to complete before exiting the method or else the 
                    // Process may be killed at some random time when the process variable is GC.
                    using var cts = new CancellationTokenSource(_moduleOptions.ModuleInstallTimeout);
                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

                    _logger.LogInformation($"Installer exited with code {process.ExitCode}");
                    await logWriter.WriteLineAsync($"Installer exited with code {process.ExitCode}")
                                   .ConfigureAwait(false);
                }
                else
                {
                    if (moduleDownload is not null)
                        moduleDownload.Status = ModuleStatusType.FailedInstall;

                    await logWriter.WriteLineAsync($"Unable to start the Module installer for '{moduleId}'")
                                   .ConfigureAwait(false);
                    return (false, $"Unable to start the Module installer for '{moduleId}'");
                }
            }
            catch (OperationCanceledException e) // installation took to long.
            {
                if (!process.HasExited)
                    process.Kill(true);

                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.FailedInstall;

                await logWriter.WriteLineAsync($"Timed out attempting to install Module '{moduleId}'")
                               .ConfigureAwait(false);
                return (false, $"Timed out attempting to install Module '{moduleId}' (${e.Message})");
            }
            catch (Exception e)
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.FailedInstall;

                await logWriter.WriteLineAsync($"Unable to install Module '{moduleId}' (${e.Message})")
                               .ConfigureAwait(false);
                return (false, $"Unable to install Module '{moduleId}' (${e.Message})");
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

            // if (SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development)
            //    return (false, $"Can't uninstall {moduleId} when running in Development");

            if (_moduleCollection is null)
                return (false, "Unable to locate analysis module collection");

            ModuleConfig? module = _moduleCollection.GetModule(moduleId);
            if (module is null)
                return (false, $"Unable to find module {moduleId}");

            // GetProcessStatus the module's directory
            string moduleDir = _moduleSettings.GetModulePath(module); 
            if (!Directory.Exists(moduleDir))
                return (false, $"Unable to find {moduleId}'s install directory {moduleDir ?? "null"}");

            ModuleDescription? moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);

            // If the module to be uninstalled is no longer a download, create an entry and add it
            // to the download list so at least we can provide updates on it disappearing.
            if (moduleDownload is null)
            {
                moduleDownload = ModuleDescriptionFromModuleConfig(module, true,
                                                                   _versionConfig.VersionInfo!.Version,
                                                                   _moduleSettings.ModulesPath,
                                                                   _moduleSettings.PreInstalledModulesPath);
                moduleDownload.IsDownloadable = false;
            }

            if (moduleDownload is null)
                return (false, $"Unable to find the download info for '{moduleId}'");

            // Console.WriteLine("Setting ModuleStatusType.Uninstalling");
            moduleDownload.Status = ModuleStatusType.Uninstalling;

            if (!await _moduleProcessService.KillProcess(module).ConfigureAwait(false))
            {
                Console.WriteLine("Setting ModuleStatusType.Unknown");
                RefreshDownloadableModuleList();

                moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Unknown;

                return (false, $"Unable to kill {moduleId}'s process");
            }

            _moduleProcessService.RemoveProcessStatus(moduleId);

            try
            {
                Directory.Delete(moduleDir, true);
                Console.WriteLine("Setting newly deleted module to ModuleStatusType.Available");

                moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Available;
            }
            catch (Exception e)
            {               
                _logger.LogError($"Unable to delete install folder for {moduleId} ({e.Message})");
                _logger.LogInformation("Will wait a moment: sometimes a delete just needs time to complete");
                await Task.Delay(3).ConfigureAwait(false);
            }

            if (Directory.Exists(moduleDir)) // shouldn't actually be possible to get here if delete failed
            {
                Console.WriteLine("Setting ModuleStatusType.UninstallFailed");
                moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.UninstallFailed;

                RefreshDownloadableModuleList();

                return (false, $"Unable to delete install folder for {moduleId}");
            }

            if (_moduleCollection.ContainsKey(moduleId) && 
                !_moduleCollection.TryRemove(moduleId, out _))
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.UninstallFailed;
    
                RefreshDownloadableModuleList();
                
                return (false, "Unable to remove module from module list");
            }

            // Force an immediate reload
            RefreshDownloadableModuleList();

            return (true, string.Empty);
        }

        /// <summary>
        /// Returns the installation logs for the given module (if they exist.
        /// </summary>
        /// <param name="moduleId">The module whose logs we want to retrieve</param>
        /// <returns>A Tuple containing the logs, or null, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<string?> GetInstallationSummary(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return null; // (null, "No module ID provided");

            // if (SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development)
            //    return (false, $"Can't uninstall {moduleId} when running in Development");

            if (_moduleCollection is null)
                return null; // (null, "Unable to locate analysis module collection");

            ModuleConfig? module = _moduleCollection.GetModule(moduleId);
            if (module is null)
                return null; // (null, $"Unable to find module {moduleId}");
        
            try
            {
                string path = Path.Combine(module.ModulePath, _installLogFileName);
                string? logs = null;

                if (File.Exists(path))
                {
                    logs = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    if (logs is not null)
                        logs = logs.Trim();
                }

                return logs; // (logs, null);
            }
            catch (Exception /*e*/)
            {
                // string error = $"Unable to retrieve install logs for {moduleId} ({e.Message})";
                // _logger.LogError(error);
                return null; // (null, error);
            }
        }

        /// <summary>
        /// Gets the module for the given module ID based on the latest list of modules retrieved 
        /// from the download list. This list is constantly being refreshed, so getting a 
        /// ModuleDescription and hanging onto it can mean you have a reference to an object that is
        /// no longer in the 'main' list that's being used to provide status updates. This means if,
        /// say, you modify the stale object you hold, that update may not be reflected elsewhere
        /// </summary>
        /// <param name="moduleId">The id of the module to get</param>
        /// <returns>A ModuleDescription object, or null if not found.</returns>
        private async Task<ModuleDescription?> GetInstallableModuleDescription(string moduleId)
        {
            List<ModuleDescription> moduleList = await GetInstallableModules().ConfigureAwait(false);
            return moduleList.FirstOrDefault(m => m.ModuleId?.EqualsIgnoreCase(moduleId) == true);
        }

        private void SendOutputToLog(TextWriter log, object sender, DataReceivedEventArgs e)
        {
            string? message = e?.Data;

            if (!string.IsNullOrWhiteSpace(message))
            {
                message = Text.StripSpinnerChars(message);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss: ");
                log.WriteLine(timestamp + Text.StripXTermColors(message));

                string? moduleId = GetModuleIdFromEventSender(sender);
                if (moduleId is not null)
                    message = moduleId + ": " + message;

                _logger.LogInformation(message);
            }
        }

        private void SendErrorToLog(TextWriter log, object sender, DataReceivedEventArgs e)
        {
            string? message = e?.Data;

            if (!string.IsNullOrWhiteSpace(message))
            {
                message = Text.StripSpinnerChars(message);
                
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss: ");
                log.WriteLine(timestamp + Text.StripXTermColors(message));

                string? moduleId = GetModuleIdFromEventSender(sender);
                if (moduleId is not null)
                    message = moduleId + ": " + message;

                _logger.LogError(message);
            }
        }
        
        /// <summary>
        /// Gets a module ID from an event
        /// </summary>
        /// <param name="sender">The source of the event</param>
        /// <returns>A module ID, or null if none could be found</returns>
        private string? GetModuleIdFromEventSender(object? sender)
        {
            if (sender is null || sender is not Process process)
                return null;

            string directory = process.StartInfo.WorkingDirectory;
            string? moduleId = new DirectoryInfo(directory).Name;

            return moduleId;
        }

        /// <summary>
        /// This is called once the module's install script has completed
        /// </summary>
        /// <param name="sender">The process</param>
        /// <param name="e">The event args</param>
        private async void ModuleInstallComplete(object? sender, EventArgs e)
        {
            string? moduleId = GetModuleIdFromEventSender(sender);
            if (moduleId is null)
            {
                _logger.LogError("Module install complete, but can't find the installed module");
                return;
            }

            ModuleDescription? moduleDownload = await GetInstallableModuleDescription(moduleId).ConfigureAwait(false);
            if (moduleDownload is null)
            {
                _logger.LogError("Unable to find recently installed module in downloadable module list");
                // Keep going: this could have been an uploaded module
                // return;
            }

            // Console.WriteLine("Setting ModuleStatusType.Installed");
            if (moduleDownload is not null)
                moduleDownload.Status = ModuleStatusType.Installed;

            _logger.LogInformation($"Module {moduleId} installed successfully.");

            string moduleDir = _moduleSettings.ModulesPath + Path.DirectorySeparatorChar + moduleId;

            // Load up the module's settings and start the module
            var config = new ConfigurationBuilder();
            ModuleSettings.LoadModuleSettings(config, moduleDir, false);
            IConfiguration configuration  = config.Build();

            // Bind the values in the configuration to a ModuleConfig object
            var moduleConfig = new ModuleConfig();
            configuration.Bind($"Modules:{moduleId}", moduleConfig);

            // Complete the ModuleConfig's setup
            moduleConfig.Initialise(moduleId, _moduleSettings.ModulesPath,
                                    _moduleSettings.PreInstalledModulesPath);

            if (moduleConfig.Valid)
            {
                _moduleCollection.TryAdd(moduleId, moduleConfig);

                string? installSummary = await GetInstallationSummary(moduleId);
                _moduleProcessService.AddProcess(moduleConfig, true, installSummary);

                if (!(moduleConfig.AutoStart ?? false))
                    _logger.LogInformation($"Module {moduleId} not configured to AutoStart.");
                else if (await _moduleProcessService.StartProcess(moduleConfig).ConfigureAwait(false))
                    _logger.LogInformation($"Module {moduleId} started successfully.");
                else
                    _logger.LogError($"Unable to start newly installed Module {moduleId}.");
            }
            else                         
                _logger.LogError($"Config for {moduleId} is invalid. Unable to start.");
        }
        
        private void DeletePackageFile(string installPackagePath)
        {
            try 
            {
                System.IO.File.Delete(installPackagePath);
            }
            catch 
            {
            }
        }

        private void DeletePackageDirectory(string installPackageDir)
        {
            try 
            {
                System.IO.Directory.Delete(installPackageDir);
            }
            catch 
            {
            }
        }
    }
}
