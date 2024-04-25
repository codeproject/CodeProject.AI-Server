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
        private const string                      _installModulesFileName          = "installmodules.json";

        private static List<ModuleDescription>?   _lastValidDownloadableModuleList = null;
        private readonly static Semaphore         _moduleListSemaphore             = new (initialCount: 1, maximumCount: 1);
        private static DateTime                   _lastDownloadableModuleCheckTime = DateTime.MinValue;
        private static bool                       _needsInitialModuleInstalls      = false;

        private readonly VersionConfig            _versionConfig;
        private readonly ModuleSettings           _moduleSettings;
        private readonly ModuleProcessServices    _moduleProcessService;
        private readonly PackageDownloader        _packageDownloader;
        private readonly ModuleCollection         _installedModules;
        private readonly ModuleOptions            _moduleOptions;
        private readonly ServerOptions            _serverOptions;
        private readonly ILogger<ModuleInstaller> _logger;

        /// <summary>
        /// Gets the name of the file that contains the list of modules to install on first run.
        /// </summary>
        static public string InstallModulesFileName => Path.Combine(AppContext.BaseDirectory, _installModulesFileName);

        /// <summary>
        /// Initialises a new instance of the ModuleInstaller.
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="serverOptions">The server options</param>
        /// <param name="moduleCollectionOptions">The module collection instance.</param>
        /// <param name="moduleOptions">The module Options</param>
        /// <param name="moduleProcessServices">The moduleProcessServices.</param>
        /// <param name="packageDownloader">The Package Downloader.</param>
        /// <param name="logger">The logger.</param>
        public ModuleInstaller(IOptions<VersionConfig> versionOptions,
                               ModuleSettings moduleSettings,
                               IOptions<ServerOptions> serverOptions,
                               IOptions<ModuleCollection> moduleCollectionOptions,
                               IOptions<ModuleOptions> moduleOptions,
                               ModuleProcessServices moduleProcessServices,
                               PackageDownloader packageDownloader,
                               ILogger<ModuleInstaller> logger)
        {
            _versionConfig        = versionOptions.Value;
            _moduleSettings       = moduleSettings;
            _serverOptions        = serverOptions.Value;
            _installedModules     = moduleCollectionOptions.Value;
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
            // We won't install initial modules unless we've been instructed via a call to 
            // QueueInitialModulesInstallation to do so.
            if (_needsInitialModuleInstalls)
            {
                if (!_moduleOptions.InstallInitialModules)
                {
                    _logger.LogInformation($"Installing initial Modules has been disabled");
                    return false;
                }

                // Just because we need at least one await
                await Task.Delay(1).ConfigureAwait(false);

                // Add the initial installed tasks here
                // eg var result = await InstallModuleAsync("TextSummary", "1.1");
                var modulesToInstall = _moduleOptions.GetInitialModulesList();

                if (modulesToInstall?.Any() ?? false)
                {
                    _logger.LogInformation($"** Setting up initial modules. Please be patient...");


                    if (!_moduleOptions.ConcurrentInitialInstalls)
                    {
                        foreach (var installModule in modulesToInstall)
                        {
                            try
                            {
                                _logger.LogInformation($"** Installing initial module {installModule.ModuleId}.");

                                var downloadTask = DownloadAndInstallModuleAsync(installModule.ModuleId, installModule.Version);
                                (bool success, string error) = await downloadTask.ConfigureAwait(false);
                                if (!success)
                                    _logger.LogInformation($"Unable to install {installModule.ModuleId}: " + error);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Exception during DownloadAndInstallModuleAsync({installModule.ModuleId}, {installModule.Version})");
                            }
                        }
                    }
                    else
                    {
                        List<Task<(bool success, string message)>> installTasks = new();
                        foreach (var installModule in modulesToInstall)
                        {
                            try
                            {
                                _logger.LogInformation($"** Installing initial module {installModule.ModuleId}.");
                                installTasks.Add(DownloadAndInstallModuleAsync(installModule.ModuleId, installModule.Version));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Exception during DownloadAndInstallModuleAsync({installModule.ModuleId}, {installModule.Version})");
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

                // If this method is run again (why would it be?) then ensure we don't reinstall
                // the initial modules.
                _needsInitialModuleInstalls = false;

                // delete
                if (File.Exists(InstallModulesFileName))
                    File.Delete(InstallModulesFileName);
            }

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
        /// <returns>A ModuleDescription object</returns>
        public static ModuleDescription ModuleDescriptionFromModuleConfig(ModuleConfig module,
                                                                          bool isInstalled,
                                                                          string serverVersion)
        {
            var moduleDescription = new ModuleDescription()
            {
                ModuleId           = module.ModuleId,
                Name               = module.Name,
                Version            = module.Version,
                PublishingInfo     = module.PublishingInfo,
                InstallOptions     = module.InstallOptions,
                CurrentlyInstalled = module.Version
            };

            // Set initial properties. Most importantly it sets the status. 
            moduleDescription.Initialise(serverVersion, module.ModuleDirPath,
                                 module.InstallOptions!.ModuleLocation);

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
        public async Task<List<ModuleDescription>> GetInstallableModulesAsync()
        {
#if DEBUG
            TimeSpan checkInterval = TimeSpan.FromSeconds(15);
#else
            TimeSpan checkInterval = TimeSpan.FromMinutes(5);
#endif
            List<ModuleDescription>? downloadableModuleList = null;

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
                        ReadCommentHandling         = JsonCommentHandling.Skip,
                        AllowTrailingCommas         = true
                    };
                    downloadableModuleList = JsonSerializer.Deserialize<List<ModuleDescription>>(downloads, options);

                    // Initialise each downloadableModule description
                    if (downloadableModuleList is not null)
                    {
                        // HACK: for debug
                        if (_moduleOptions.ModuleListUrl.StartsWithIgnoreCase("file://"))
                        {
                            int baseUrlLength = _moduleOptions.ModuleListUrl!.Length - Constants.ModulesListingFilename.Length;
                            string baseDownloadUrl = _moduleOptions.ModuleListUrl![..baseUrlLength].TrimEnd('\\', '/');
                            if (baseDownloadUrl == "file://")
                                baseDownloadUrl = _moduleSettings.DownloadedModulePackagesDirPath;
                            foreach (var downloadableModule in downloadableModuleList)
                            {
                                downloadableModule.DownloadUrl = baseDownloadUrl + Path.DirectorySeparatorChar 
                                                   + $"{downloadableModule.ModuleId}-{downloadableModule.Version}.zip";
                            }
                        }

                        string currentServerVersion = _versionConfig.VersionInfo?.Version ?? string.Empty;
                        foreach (var downloadableModule in downloadableModuleList)
                        {
                            if (downloadableModule is null)
                                continue;

                            // ASSUMPTION: All runtime-installed modules will be installed in a
                            //             folder that's the same name as the module ID.
                            string moduleDirPath = _moduleSettings.ModulesDirPath
                                                 + Path.DirectorySeparatorChar
                                                 + downloadableModule.ModuleId;
                            downloadableModule.Initialise(currentServerVersion, moduleDirPath,
                                                          ModuleLocation.Internal);
                        }

                        // Update the status to 'Installed' or 'UpdateAvailable' for all listed
                        // modules that we are currently running.
                        foreach (ModuleConfig? module in _installedModules.Values)
                        {
                            if (module?.Valid != true)
                                continue;

                            // Find downloadableModule (a downloadableModule we're currently running) in the list of 
                            // downloadable modules.
                            var downloadableModule = downloadableModuleList.FirstOrDefault(m => m.ModuleId == module.ModuleId
                                                                               && m.Status == ModuleStatusType.Available);
                            if (downloadableModule is not null)
                            {
                                downloadableModule.Status = ModuleStatusType.Installed;

                                // LatestCompatibleRelease shouldn't be null at this point , but just in case.
                                if (VersionInfo.Compare(downloadableModule.LatestCompatibleRelease?.ModuleVersion ?? "0.0.0", 
                                                        module.Version) > 0)
                                    downloadableModule.Status = ModuleStatusType.UpdateAvailable;
                            }
                        }
                    }
                }

                if (downloadableModuleList is null)
                {
                    // Fall back to whatever we had before
                    downloadableModuleList = _lastValidDownloadableModuleList;
                }
                else
                {
                    // Go through the our list of modules, and for all modules that are Installed or
                    // Available, set the status of each module as what we currently have. We do this
                    // because we have just downloaded a new list (otherwise moduleList is null) and
                    // we may be updating (eg installing or uninstalling) a module. We should preserve
                    // the interim statuseseses.
                    foreach (var downloadableModule in downloadableModuleList)
                    {
                        // Just check to see if we already have a status (which may have been updated)
                        if (_lastValidDownloadableModuleList is not null &&
                            (downloadableModule.Status == ModuleStatusType.Available ||
                             downloadableModule.Status == ModuleStatusType.UpdateAvailable ||
                             downloadableModule.Status == ModuleStatusType.Installed))
                        {
                            var existingDescription = _lastValidDownloadableModuleList
                                                            .FirstOrDefault(m => m.ModuleId == downloadableModule.ModuleId);
                            if (existingDescription is not null)
                            {
                                if (existingDescription.Status == ModuleStatusType.UninstallFailed)
                                {
                                    // If the uninstall failed but ultimately the downloadableModule's
                                    // dir was emptied, then mark it as done.
                                    string moduleDirPath = existingDescription.ModuleDirPath;
                                    if (!Directory.Exists(moduleDirPath) ||
                                        !Directory.EnumerateFileSystemEntries(moduleDirPath).Any())
                                    {
                                        existingDescription.Status = ModuleStatusType.Uninstalled;
                                        downloadableModule.Status  = ModuleStatusType.Available;
                                    }
                                }

                                if (existingDescription.Status != ModuleStatusType.Unknown         &&
                                    existingDescription.Status != ModuleStatusType.UpdateAvailable &&
                                    existingDescription.Status != ModuleStatusType.Uninstalled)
                                {
                                    downloadableModule.Status = existingDescription!.Status;
                                }
                            }
                        }
                    }

                    // Update to the latest and greatest
                    if (downloadableModuleList is not null)
                        _lastValidDownloadableModuleList = downloadableModuleList;
                }
            }
#if DEBUG
            catch (Exception e)
            {
                _logger.LogError($"Error checking for available modules: " + e.Message);
            }
#else
            catch (Exception)
            {
            }
#endif
            finally
            {
                _moduleListSemaphore.Release();
            }
  
            return downloadableModuleList ?? new List<ModuleDescription>();
        }

        /// <summary>
        /// Downloads and Installs the given module for a particular version.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="version">The version of the module to install</param>
        /// <param name="noCache">Whether or not to ignore the download cache. If true, the module
        /// will always be freshly downloaded</param>
        /// <param name="verbosity">The amount of noise to output when installing</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> DownloadAndInstallModuleAsync(string moduleId, 
                                                                        string version,
                                                                        bool noCache = false,
                                                                        LogVerbosity verbosity = LogVerbosity.Quiet)
        {           
            if (string.IsNullOrWhiteSpace(moduleId))
                return (false, "No module ID provided");

            _logger.LogInformation($"Preparing to install module '{moduleId}'");

            ModuleDescription? moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);
            if (moduleDownload is null)
                return (false, $"Unable to find the download info for '{moduleId}'");

            if (!moduleDownload.Valid)
                return (false, $"Module description for '{moduleId}' is invalid");

            // If no version specified, download the latest and greatest
            if (string.IsNullOrWhiteSpace(version))
                version = moduleDownload.Version!;

            // Check we don't have a current or newer version already installed            
            ModuleConfig? module = _installedModules.GetModule(moduleId);

            // A pre-installed module is installed in the /preinstalled-modules folder. This can be
            // uninstalled, and then downloaded and reinstalled, but we need to ensure it's
            // uninstalled before we re-install. This isn't actually critical, because we can have
            // 2 modules installed at the same time: the last one spotted will be the one that gets
            // launched (pre-installed are checked first, then post-installed, so latest installed
            // wins)
            // Some notes:
            // "PreInstalled" is only set to true for the modulesettings for a module installed in a
            // docker image. Never for a module outside of this, so the modules.json file listing
            // downloadable modules should always have this "false".
            // "module.InstallOptions?.PreInstalled" is the setting for the currently installed 
            // module, not the module that can be downloaded.
            // GIVEN ALL THAT: who cares. We can totally uninstall / reinstall something pre-installed.
            // if (module is not null && module.InstallOptions?.PreInstalled == true)
            //    return (false, $"Module description for '{moduleId}' is invalid. A 'pre-installed' module can't be downloaded");

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
            // string moduleDirName = _moduleSettings.GetModuleDirPath(moduleDownload);
            // string moduleDirName = moduleDownload.ModuleDirPath;
            string downloadDirPath = _moduleSettings.DownloadedModulePackagesDirPath 
                                   + Path.DirectorySeparatorChar + moduleId + "-" + version + ".zip";

            // Console.WriteLine("Setting ModuleStatusType.Downloading");
            moduleDownload.Status = ModuleStatusType.Downloading;
            _logger.LogInformation($"Downloading module '{moduleId}'");

            bool downloaded = false;
            string error = string.Empty;

            if (!noCache && System.IO.File.Exists(downloadDirPath))
            {
                _logger.LogInformation($" (using cached download for '{moduleId}')");
                downloaded = true;               
            }
            else
            {
                (downloaded, error) = await _packageDownloader.DownloadFileAsync(moduleDownload.DownloadUrl!,
                                                                                 downloadDirPath, true)
                                                              .ConfigureAwait(false);
            }

            if (downloaded && !System.IO.File.Exists(downloadDirPath))
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

            return await InstallModuleAsync(downloadDirPath, moduleId, verbosity).ConfigureAwait(false);
        }

        /// <summary>
        /// Installs the module that is stored in the package file given my moduleId in the path
        /// given by installPackagePath.
        /// </summary>
        /// <param name="installPackagePath">The path to the installer zip package</param>
        /// <param name="moduleId">The module to install</param>
        /// <param name="verbosity">The amount of noise to output when installing</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> InstallModuleAsync(string installPackagePath,
                                                             string? moduleId,
                                                             LogVerbosity verbosity = LogVerbosity.Quiet)
        {
            ModuleDescription? moduleDownload = null;
            string? moduleDirPath             = null;

            // A module that was uploaded via the API won't have a moduleID provided. It will be in
            // the modulesettings.json file in the module's install package.
            bool isUploadedModule = string.IsNullOrWhiteSpace(moduleId);

            // If we do not know the module we're installing then base the filenames on the filepath
            // until we can determine the actual module Id. Otherwise, if we know the module then
            // we can update its progress now. 
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                string tempName = Path.GetFileNameWithoutExtension(installPackagePath);
                moduleDirPath = Path.Combine(_moduleSettings.ModulesDirPath, Text.FixSlashes(tempName));
            }
            else
            {
                moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);    
                if (moduleDownload is not null)
                {
                    moduleDownload.Status = ModuleStatusType.Unpacking;
                    moduleDirPath = moduleDownload.ModuleDirPath;
                }
            }

            if (string.IsNullOrWhiteSpace(moduleDirPath))
                return (false, $"Unable to determine module directory for '{installPackagePath}'");

            // At this point we have the directory name of the module we're to install, so we can
            // extract the install package into this directory
            bool extracted = _packageDownloader.Extract(installPackagePath, moduleDirPath!, out var _);
    
            // We've extracted the module from the package, so delete the model package (but Only if
            // we're not in dev mode)
            if (SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                DeletePackageFile(installPackagePath);

            if (!extracted)
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Unknown;
                    
                return (false, $"Unable to unpack module in '{installPackagePath}'");
            }

            // We have extracted the package into the appropriate folder. The name of the folder
            // should be the same as the module ID. We're paranoid, so we're going to load up the
            // module's modulesettings.json file and read the module ID directly from that file.

            string? moduleIdFromSettingsFile = null;
            try
            {
                string content = await File.ReadAllTextAsync(Path.Combine(moduleDirPath,
                                                                          Constants.ModuleSettingsFilename))
                                           .ConfigureAwait(false);

                var documentOptions = new JsonDocumentOptions
                {
                    CommentHandling     = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var jsonSettings = JsonDocument.Parse(content, documentOptions).RootElement;
                var jsonModules  = jsonSettings.EnumerateObject().FirstOrDefault();
                var jsonModule   = jsonModules.Value.EnumerateObject().FirstOrDefault();
                moduleIdFromSettingsFile = jsonModule.Name;
            }
            catch
            {
                if (SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                    DeletePackageDirectory(moduleDirPath);

                return (false, $"Unable to load module configuration from '{installPackagePath}'");
            }

            if (string.IsNullOrWhiteSpace(moduleIdFromSettingsFile))
            {
                if (isUploadedModule && SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                    DeletePackageDirectory(moduleDirPath);

                return (false, $"Unable to read module Id from settings in '{installPackagePath}'");
            }

            // If no module Id was passed to this method then now is the time to move this anonymous
            // installation folder into its final home. ASSUMING a module of this Id doesn't already
            // exist.
            // TODO: We should not pass in a moduleID. Just follow the path of getting the module ID
            //       from the modulesettings.json file and do it properly
            if (string.IsNullOrWhiteSpace(moduleId)) // NO module Id was passed in
            {
                if (_installedModules.ContainsKey(moduleIdFromSettingsFile))
                {
                    DeletePackageDirectory(moduleDirPath);
                    return (false, $"A module of id {moduleIdFromSettingsFile} has already been installed. Please uninstall before uploading again.");
                }

                string newModuleDirName = Path.Combine(_moduleSettings.ModulesDirPath, 
                                                       Text.FixSlashes(moduleIdFromSettingsFile));
                Directory.Move(moduleDirPath, newModuleDirName);

                moduleId  = moduleIdFromSettingsFile;
                moduleDirPath = newModuleDirName;
            }
            else    // A module ID was passed in.
            {
                // Not strictly necessary, but probably a good idea
                if (!moduleId.EqualsIgnoreCase(moduleIdFromSettingsFile))
                {
                    return (false, $"The module to install ({moduleIdFromSettingsFile}) has a " +
                                   $"different module ID than was specified ({moduleId}). Quitting.");
                }
            }

            // Run the install script
            if (!File.Exists(_moduleSettings.ModuleInstallerScriptPath))
            {
                // Let's allow there to not be an install script
                /*
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.FailedInstall;

                return (false, $"Module '{moduleId}' install script not found");
                */

                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Installed;

                return (true, string.Empty);
            }

            _logger.LogInformation($"Installing module '{moduleId}'");
            _logger.LogDebug($"Installer script at '{_moduleSettings.ModuleInstallerScriptPath}'");

            if (moduleDownload is not null)
                moduleDownload.Status = ModuleStatusType.Installing;

            ProcessStartInfo procStartInfo;
            if (SystemInfo.IsWindows)
            {
                string command = _moduleSettings.ModuleInstallerScriptPath;
                procStartInfo = new ProcessStartInfo(command);
                procStartInfo.Arguments = $" --verbosity {verbosity} --launcher server";
            }
            else
            {
                string command = $"\"{_moduleSettings.ModuleInstallerScriptPath}\" --verbosity {verbosity}  --launcher server";
                procStartInfo = new ProcessStartInfo("bash", command);
            }
            
            procStartInfo.UseShellExecute        = false;
            procStartInfo.WorkingDirectory       = moduleDirPath;
            procStartInfo.CreateNoWindow         = false;
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError  = true;

            // Setup installer process
            using var process = new Process();
            process.StartInfo           = procStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited             += ModuleInstallCompleteAsync;

            // Setup log file
            using StreamWriter logWriter = File.AppendText(Path.Combine(moduleDirPath, _installLogFileName));

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
                return (false, $"Timed out attempting to install Module '{moduleId}' ({e.Message})");
            }
            catch (Exception e)
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.FailedInstall;

                await logWriter.WriteLineAsync($"Unable to install Module '{moduleId}' ({e.Message})")
                               .ConfigureAwait(false);
                return (false, $"Unable to install Module '{moduleId}' ({e.Message})");
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

            if (_installedModules is null)
                return (false, "Unable to locate analysis module collection");

            ModuleConfig? module = _installedModules.GetModule(moduleId);
            if (module is null)
                return (false, $"Unable to find module {moduleId}");

            ModuleDescription? moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);

            // If the module to be uninstalled is no longer a download, create an entry and add it
            // to the download list so at least we can provide updates on it disappearing.
            if (moduleDownload is null)
            {
                moduleDownload = ModuleDescriptionFromModuleConfig(module, true,
                                                                   _versionConfig.VersionInfo!.Version);
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

                moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Unknown;

                return (false, $"Unable to kill {moduleId}'s process");
            }

            _moduleProcessService.RemoveProcessStatus(moduleId);

            string moduleDirPath = module.ModuleDirPath; 

            try
            {
                if (Directory.Exists(moduleDirPath))
                    Directory.Delete(moduleDirPath, true);
                else    
                    Console.WriteLine($"Unable to find {moduleId}'s install directory {moduleDirPath ?? "null"}");

                Console.WriteLine("Module files removed. Setting module state to Available");

                moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.Available;
            }
            catch (Exception e)
            {               
                _logger.LogError($"Unable to delete install folder for {moduleId} ({e.Message})");
                _logger.LogInformation("Will wait a moment: sometimes a delete just needs time to complete");
                await Task.Delay(3).ConfigureAwait(false);
            }

            if (Directory.Exists(moduleDirPath)) // shouldn't actually be possible to get here if delete failed
            {
                Console.WriteLine("Setting ModuleStatusType.UninstallFailed");
                moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.UninstallFailed;

                RefreshDownloadableModuleList();

                return (false, $"Unable to delete install folder for {moduleId}");
            }

            if (_installedModules.ContainsKey(moduleId) && 
                !_installedModules.TryRemove(moduleId, out _))
            {
                if (moduleDownload is not null)
                    moduleDownload.Status = ModuleStatusType.UninstallFailed;
    
                RefreshDownloadableModuleList();
                
                return (false, "Unable to remove module from installed module list");
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
        public async Task<string?> GetInstallationSummaryAsync(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return null; // (null, "No module ID provided");

            // if (SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development)
            //    return (false, $"Can't uninstall {moduleId} when running in Development");

            if (_installedModules is null)
                return null; // (null, "Unable to locate analysis module collection");

            ModuleConfig? module = _installedModules.GetModule(moduleId);
            if (module is null)
                return null; // (null, $"Unable to find module {moduleId}");
        
            try
            {
                string path = Path.Combine(module.ModuleDirPath, _installLogFileName);
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
        private async Task<ModuleDescription?> GetInstallableModuleDescriptionAsync(string moduleId)
        {
            List<ModuleDescription> moduleList = await GetInstallableModulesAsync().ConfigureAwait(false);
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
                log.Flush();

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
                log.Flush();

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

            // Bad assumption: A module's ID is same as the name of folder in which it lives.
            // string? moduleId = new DirectoryInfo(directory).Name;

            string? moduleId = ModuleConfigExtensions.GetModuleIdFromModuleSettings(directory);
            return moduleId;
        }

        /// <summary>
        /// This is called once the module's install script has completed
        /// </summary>
        /// <param name="sender">The process</param>
        /// <param name="e">The event args</param>
        private async void ModuleInstallCompleteAsync(object? sender, EventArgs e)
        {
            string? moduleId = GetModuleIdFromEventSender(sender);
            if (moduleId is null)
            {
                _logger.LogError("Module install complete, but can't find the installed module");
                return;
            }

            ModuleDescription? moduleDownload = await GetInstallableModuleDescriptionAsync(moduleId).ConfigureAwait(false);
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

            // ASSUMPTION: All runtime-installed modules will be installed in a folder that's the
            //             same name as the module ID.
            string moduleDirPath = _moduleSettings.ModulesDirPath + Path.DirectorySeparatorChar
                                 + moduleId;

            // Load up the module's settings and start the module
            var config = new ConfigurationBuilder();
            config.AddModuleSettingsConfigFiles(moduleDirPath, false);
            IConfiguration configuration  = config.Build();

            // Bind the values in the configuration to a ModuleConfig object
            var moduleConfig = new ModuleConfig();
            configuration.Bind($"Modules:{moduleId}", moduleConfig);

            // Complete the ModuleConfig's setup. 
            if (moduleConfig.Initialise(moduleId, moduleDirPath, ModuleLocation.Internal))
            {
                // If we're updating a module then we'll have the old info on this module in our list
                // of installed modules. Remove the old one so we can replace with the updated info.
                if (_installedModules.ContainsKey(moduleId))
                    _installedModules.Remove(moduleId, out ModuleConfig? _);
                _installedModules.TryAdd(moduleId, moduleConfig);

                // CHECK that the module id gets updated
                string? installSummary = await GetInstallationSummaryAsync(moduleId);
                _moduleProcessService.AddProcess(moduleConfig, true, installSummary, true);

                if (!(moduleConfig.LaunchSettings!.AutoStart ?? false))
                    _logger.LogInformation($"Module {moduleId} not configured to AutoStart.");
                else if (await _moduleProcessService.StartProcess(moduleConfig, installSummary).ConfigureAwait(false))
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
