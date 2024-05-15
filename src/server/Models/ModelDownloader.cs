using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeProject.AI.Server.Modules;
using CodeProject.AI.Server.Utilities;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server.Models
{
    /// <summary>
    /// Manages the install/uninstall/update of modules.
    /// </summary>
    public class ModelDownloader
    {
        private static ModelDownloadCollection?   _lastValidDownloadableModelList  = null;
        private readonly static Semaphore         _modelListSemaphore              = new (initialCount: 1, maximumCount: 1);
        private static DateTime                   _lastDownloadableModelsCheckTime = DateTime.MinValue;

        private readonly PackageDownloader        _packageDownloader;
        private readonly ModuleCollection         _installedModules;
        private readonly ModuleOptions            _moduleOptions;
        private readonly ServerOptions            _serverOptions;
        private readonly ILogger<ModelDownloader> _logger;

        /// <summary>
        /// Initialises a new instance of the ModelInstaller.
        /// </summary>
        /// <param name="serverOptions">The server options</param>
        /// <param name="moduleCollectionOptions">The module collection instance.</param>
        /// <param name="moduleOptions">The module Options</param>
        /// <param name="packageDownloader">The Package Downloader.</param>
        /// <param name="logger">The logger.</param>
        public ModelDownloader(IOptions<ServerOptions> serverOptions,
                               IOptions<ModuleCollection> moduleCollectionOptions,
                               IOptions<ModuleOptions> moduleOptions,
                               PackageDownloader packageDownloader,
                               ILogger<ModelDownloader> logger)
        {
            _serverOptions        = serverOptions.Value;
            _installedModules     = moduleCollectionOptions.Value;
            _moduleOptions        = moduleOptions.Value;
            _packageDownloader    = packageDownloader;
            _logger               = logger;
        }

        /// <summary>
        /// Force a reload of the downloadable module list next time it's queried.
        /// </summary>
        public static void RefreshDownloadableModelList()
        {
            _lastDownloadableModelsCheckTime = DateTime.MinValue;
        }

        /// <summary>
        /// Returns an array of downloadable models for the given module
        /// </summary>
        /// <param name="moduleId">THe id of the module whose models we're looking for</param>
        /// <returns>An array of <see cref="ModelDownload"/>s</returns>
        public async Task<ModelDownload[]> GetDownloadableModelsAsync(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return Array.Empty<ModelDownload>();

            ModelDownloadCollection modelsDict = await GetDownloadableModelsAsync();
            if (modelsDict.ContainsKey(moduleId))
                return modelsDict[moduleId];

            return Array.Empty<ModelDownload>();
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
        public async Task<ModelDownloadCollection> GetDownloadableModelsAsync()
        {
#if DEBUG
            TimeSpan checkInterval = TimeSpan.FromSeconds(15);
#else
            TimeSpan checkInterval = TimeSpan.FromHours(6);
#endif
            ModelDownloadCollection? downloadableModels = null;

            _modelListSemaphore.WaitOne();
            try
            {
                // 1. Get the models from CodeProject,com

                if (_serverOptions.AllowInternetAccess == true &&
                    (DateTime.Now - _lastDownloadableModelsCheckTime > checkInterval ||
                     _lastValidDownloadableModelList is null))
                {
                    _lastDownloadableModelsCheckTime = DateTime.Now;

                    // Download the list of downloadable modules as a JSON string, then deserialise
                    string downloadsJson = await _packageDownloader.DownloadTextFileAsync(_moduleOptions.ModelListUrl!)
                                                                   .ConfigureAwait(false);

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling         = JsonCommentHandling.Skip,
                        AllowTrailingCommas         = true
                    };
                    downloadableModels = JsonSerializer.Deserialize<ModelDownloadCollection>(downloadsJson,
                                                                                             options);
                    if (downloadableModels is null)
                        downloadableModels = new ModelDownloadCollection();

                    // 2. Combine the downloaded model list with the models each module starts off
                    //    with. The downloaded list takes precedence and will overwrite initial vals.
                    foreach (var moduleEntry in _installedModules)
                    {
                        string moduleId     = moduleEntry.Key;
                        ModuleConfig module = moduleEntry.Value;

                        ModelDownload[]? models = null;
                        if (downloadableModels!.ContainsKey(moduleId))
                        {
                            models = downloadableModels[moduleId];
                            if (models is null)
                            {
                                models = Array.Empty<ModelDownload>();
                                downloadableModels[moduleId] = models;
                            }
                        }
                        else
                        {
                            models = module.InstallOptions?.DownloadableModels
                                                           .Select(m => ModelDownload.FromConfig(m))
                                                           .ToArray();
                            if (models is null)
                                models = Array.Empty<ModelDownload>();

                            downloadableModels[moduleId] = models;
                        }
                    }

                    // 3. Fill in missing info if necessary
                    foreach (var modelEntry in downloadableModels)
                    {
                        string moduleId = modelEntry.Key;
                        ModelDownload[] models = modelEntry.Value;
                        foreach (ModelDownload model in models)
                        {
                            if (model?.Filename is null || model?.Folder is null)
                                continue;

                            /* Doesn't work like this: We don't store the downloaded zip in the
                                module's dir, we store the files *contained* in the zip. 
                            string path = Path.Combine(module.ModuleDirPath, model.Folder,
                                                        model.Filename);
                            model.Downloaded = File.Exists(path);
                            */

                            string path = Path.Combine(ModelCachePath(moduleId), model.Filename);
                            model.Cached = File.Exists(path);
                        }
                    }
                }

                if (downloadableModels is null)
                {
                    // Fall back to whatever we had before
                    downloadableModels = _lastValidDownloadableModelList;
                }
                else
                {
                    // Update to the latest and greatest
                    if (downloadableModels is not null)
                        _lastValidDownloadableModelList = downloadableModels;
                }
            }
#if DEBUG
            catch (Exception e)
            {
                _logger.LogError($"Error checking for available models: " + e.Message);
            }
#else
            catch (Exception)
            {
            }
#endif
            finally
            {
                _modelListSemaphore.Release();
            }
  
            return downloadableModels ?? new ModelDownloadCollection();
        }

        /// <summary>
        /// Gets the path to the folder containing cached model files for the given module
        /// </summary>
        /// <param name="moduleId">The module Id</param>
        /// <returns>A string</returns>
        private string ModelCachePath(string moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new Exception("Must provide a module Id");

            // if (string.IsNullOrWhiteSpace(moduleId))
            //    return null;

            // Shared cache folder
            // return _moduleOptions.DownloadedModelsPackagesDirPath;

            // Individual cache folder per module. **This is what the installers use!**
            return Path.Combine(_moduleOptions.DownloadedModulePackagesDirPath!, moduleId);
        }

        /// <summary>
        /// Downloads and Installs the given module for a particular version.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="filename">The filename of the model to install</param>
        /// <param name="installFolderName">The name of the directing in the module's directory where
        /// the model will be extracted</param>
        /// <param name="noCache">Whether or not to ignore the download cache. If true, the module
        /// will always be freshly downloaded</param>
        /// <param name="verbosity">The amount of noise to output when installing</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public async Task<(bool, string)> DownloadModelAsync(string moduleId, string filename,
                                                             string installFolderName,
                                                             bool noCache = false,
                                                             LogVerbosity verbosity = LogVerbosity.Quiet)
        {
        
            if (_serverOptions.AllowInternetAccess != true)
                return (false, "No internet access allowed");

            if (string.IsNullOrWhiteSpace(moduleId))
                return (false, "No module ID provided");

            // moduleId = moduleId.ToLower();

            _logger.LogInformation($"Preparing to download model '{filename}' for module {moduleId}");

            // Ensure we're downloading a model for a module that actually exists and is installed
            ModuleConfig? module = _installedModules.GetModule(moduleId);
            if (module is null || !module.Valid)
                return (false, $"A valid module for {moduleId} was not found");

            // Download the model's zip, and store in cache. We use MODULES cache location not MODELS
            // string downloadDirPath = _moduleOptions.DownloadedModelsPackagesDirPath...
            string downloadDirPath = _moduleOptions.DownloadedModulePackagesDirPath!;
            downloadDirPath = Path.Combine(downloadDirPath, moduleId, filename);

            // Console.WriteLine("Setting ModuleStatusType.Downloading");
            _logger.LogInformation($"Downloading model '{filename}' to '{downloadDirPath}'");

            bool downloaded = false;
            string error = string.Empty;

            if (!noCache && File.Exists(downloadDirPath))
            {
                _logger.LogInformation($" (using cached download for '{filename}')");
                downloaded = true;               
            }
            else
            {
                string downloadUrl = filename.StartsWithIgnoreCase("http") 
                                   ? filename : _moduleOptions.ModelStorageUrl + filename;
                (downloaded, error) = await _packageDownloader.DownloadFileAsync(downloadUrl, downloadDirPath, true)
                                                              .ConfigureAwait(false);
            }

            if (downloaded && !File.Exists(downloadDirPath))
            {
                downloaded = false;
                error      = "Model was downloaded but was not saved";
            }

            if (!downloaded)
                return (false, $"Unable to download '{filename}'. Error: {error}");

            // Extract the model package into the model directory in the module's directory
            string extractDir = Path.Combine(module.ModuleDirPath!, installFolderName);
            bool extracted = _packageDownloader.Extract(downloadDirPath, extractDir, out var _);
    
            // We've extracted the models from the package, so delete the model package (but Only if
            // we're not in dev mode)
            if (SystemInfo.RuntimeEnvironment != RuntimeEnvironment.Development)
                DeletePackageFile(downloadDirPath);

            if (!extracted)
                return (false, $"Unable to unpack model package '{downloadDirPath}'");

            return (true, string.Empty);            
        }

        /// <summary>
        /// Uninstalls the given model.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="filename">The filename of the model to install</param>
        /// <param name="installFolderName">The name of the directing in the module's directory where
        /// the model will be extracted</param>
        /// <returns>A Tuple containing true for success; false otherwise, and a string containing
        /// the error message if the operation was not successful.</returns>
        public (bool, string) DeleteModel(string moduleId, string filename, string installFolderName)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return (false, "No module ID provided");

            _logger.LogInformation($"Preparing to delete model package '{filename}' for module {moduleId}");

            // Ensure we're downloading a model for a module that actually exists and is installed
            ModuleConfig? module = _installedModules.GetModule(moduleId);
            if (module is null || !module.Valid)
                return (false, $"A valid module for {moduleId} was not found");

            // Download the model's zip, and store in cache. We use MODULES cache location not MODELS
            // string downloadDirPath = _moduleOptions.DownloadedModelsPackagesDirPath...
            string downloadDirPath = _moduleOptions.DownloadedModulePackagesDirPath!;
            downloadDirPath = Path.Combine(downloadDirPath, moduleId, filename);

            // Extract the model package into the model directory in the module's directory
            string extractDir = Path.Combine(module.ModuleDirPath!, installFolderName);

            try
            {
                if (Directory.Exists(extractDir))
                {
                    // The downloaded model package is extracted into extractDir. We can delete this
                    // folder, but that will delete the models AND ALL OTHER MODELS extracted into
                    // that folder. What we really need to know is: what models are included in the
                    // model package, and then only delete those.
                    // FURTHER WRINKLE: what if 2 download model packages have the same model? If we
                    // delete the model from one package, then it means we're deleting it from the
                    // other package unintentionally. A full can o' worms.

                    // Directory.Delete(extractDir, true);
                    // Console.WriteLine("Model files removed.");
                }
                else
                {
                    Console.WriteLine($"Unable to find {filename}'s install directory {extractDir ?? "null"}");
                }
            }
            catch (Exception e)
            {               
                _logger.LogError($"Unable to delete models from {filename} folder for {moduleId} ({e.Message})");
            }

            return (true, string.Empty);
        }

        private void DeletePackageFile(string downloadPackagePath)
        {
            try 
            {
                File.Delete(downloadPackagePath);
            }
            catch 
            {
            }
        }

        /// <summary>
        /// Takes a current (original) list of downloadable models and combines it with a list of
        /// downloadable models and returns the result for the given module.
        /// </summary>
        /// <param name="moduleId">The id of the module whose model downloads we're talking about</param>
        /// <param name="currentModels">The current list of downloadable models for this module</param>
        /// <param name="downloadableModelCollection">A dictionary of ModelConfigs downloaded from a
        /// web service representing Models that can be downloaded for each module</param>
        public ModelDownload[] MergeDownloadableModels(string moduleId, ModelDownload[]? currentModels,
                                                       ModelDownloadCollection downloadableModelCollection)
        {           
            List<ModelDownload> mergedModels  = currentModels?.ToList() ?? new List<ModelDownload>();

            // If the list of downloadable models from CodeProject included an entry for 
            // this module, then add/overwrite the hardcoded models for this module
            if (downloadableModelCollection.ContainsKey(moduleId))
            {
                ModelDownload[]? downloadableModels = downloadableModelCollection[moduleId];
                foreach (ModelDownload downloadableModel in downloadableModels)
                {
                    ModelDownload? existing = mergedModels.SingleOrDefault(m => 
                                                    m.Filename == downloadableModel.Filename);
                    if (existing is not null)
                        mergedModels.Remove(existing);

                    mergedModels.Add(downloadableModel);
                }
            }

            return mergedModels.ToArray();
        }
    }
}
