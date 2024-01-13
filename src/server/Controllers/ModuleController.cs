using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.Server.Modules;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.Server.Controllers
{
    /// <summary>
    /// For managing the optional modules that form part of the system.
    /// </summary>
    [Route("v1/module")]
    [ApiController]
    public class ModuleController : ControllerBase
    {
        private readonly VersionConfig         _versionConfig;
        private readonly ModuleOptions         _moduleOptions;
        private readonly ModuleSettings        _moduleSettings;
        private readonly ModuleCollection      _installedModules;
        private readonly ModuleInstaller       _moduleInstaller;
        private readonly ModuleProcessServices _moduleProcessService;

        private readonly ILogger               _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="moduleOptions">The module options instance</param>
        /// <param name="moduleCollectionOptions">The collection of modules.</param>
        /// <param name="moduleInstaller">The module installer instance.</param>
        /// <param name="moduleProcessService">The module process service</param>
        /// <param name="logger">The logger</param>
        public ModuleController(IOptions<VersionConfig>    versionOptions,
                                ModuleSettings             moduleSettings,
                                IOptions<ModuleOptions>    moduleOptions,
                                IOptions<ModuleCollection> moduleCollectionOptions,
                                ModuleInstaller moduleInstaller,
                                ModuleProcessServices moduleProcessService,
                                ILogger<LogController> logger)
        {
            _versionConfig        = versionOptions.Value;
            _moduleOptions        = moduleOptions.Value;
            _moduleSettings       = moduleSettings;
            _installedModules     = moduleCollectionOptions.Value;
            _moduleInstaller      = moduleInstaller;
            _moduleProcessService = moduleProcessService;
            _logger               = logger;
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis services.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpPost("upload", Name = "UploadModule")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> UploadModule()
        {
            try // if there are no Form values, then Request.Form throws.
            {
                IFormCollection form = Request.Form;
                
                if (string.IsNullOrWhiteSpace(_moduleOptions.InstallPassword))
                    return CreateErrorResponse("No security credentials have been set for module uploads. Not proceeding.");

                // Add any form files
                var uploadedFile = form.Files.FirstOrDefault();
                if (uploadedFile is null || uploadedFile.Length == 0)
                    return CreateErrorResponse("No file was uploaded");

                string? password = form["install-pwd"][0];
                if (password is null || password != _moduleOptions.InstallPassword)
                    return CreateErrorResponse("The supplied module upload password was incorrect. Not proceeding.");

                string tempName      = Guid.NewGuid().ToString();
                string downloadDirPath  = _moduleSettings.DownloadedModulePackagesDirPath 
                                     + Path.DirectorySeparatorChar + tempName + ".zip";

                using (Stream fileStream = new FileStream(downloadDirPath, FileMode.Create, FileAccess.Write))
                {
                    await uploadedFile.CopyToAsync(fileStream).ConfigureAwait(false);
                    fileStream.Close();
                }

                (bool success, string error) = await _moduleInstaller.InstallModuleAsync(downloadDirPath, null)
                                                                     .ConfigureAwait(false);
    
                return success? new SuccessResponse() : CreateErrorResponse("Unable install module: " + error);
            }
            catch (Exception ex)
            {
                return CreateErrorResponse("Unable to upload and install module: " + ex.Message);
                // nothing to do here, just no Form available
            }
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis services. This may include
        /// modules that can't be downloaded. They will be marked appropriately.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/installed", Name = "ListInstalledModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> ListInstalledModules()
        {
            if (_installedModules?.Count is null || _installedModules.Count == 0)
                return CreateErrorResponse("No backend modules have been registered");

            string currentServerVersion = _versionConfig.VersionInfo!.Version;

            var modules = _installedModules?.Values?
                            .Select(module => ModuleInstaller.ModuleDescriptionFromModuleConfig(module, true, 
                                                                                                currentServerVersion,
                                                                                                _moduleSettings.ModulesDirPath,
                                                                                                _moduleSettings.PreInstalledModulesDirPath))
                            .ToList() ?? new List<ModuleDescription>();

            // Mark those modules that can't be downloaded
            List<ModuleDescription> downloadables = await _moduleInstaller.GetInstallableModules()
                                                                          .ConfigureAwait(false);
            foreach (ModuleDescription module in modules)
            {
                if (!downloadables.Any(download => download.ModuleId == module.ModuleId))
                    module.IsDownloadable = false;                
            }

            return new ModuleListResponse
            {
                Modules = modules
            };
        }

        /// <summary>
        /// Allows for a client to list of backend analysis modules available for download
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/available", Name = "ListAvailableModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> ListAvailableModules()
        {
            List<ModuleDescription> moduleList = await _moduleInstaller.GetInstallableModules()
                                                                       .ConfigureAwait(false);

            return new ModuleListResponse()
            {
                Modules = moduleList!
            };
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis modules as well as the
        /// modules that can be downloaded and installed.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list", Name = "ListAllModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> ListAllModules()
        {
            List<ModuleDescription> installableModules = await _moduleInstaller.GetInstallableModules()
                                                                               .ConfigureAwait(false) 
                                                       ?? new List<ModuleDescription>();
            
            string currentServerVersion = _versionConfig.VersionInfo!.Version;

            // Go through each module we currently have registered, and if that module doesn't 
            // appear in the available downloads then add it to the list of all modules and
            // ensure it's set as 'Installed'.
            foreach (ModuleConfig? module in _installedModules.Values)
            {
                if (module?.Valid != true)
                    continue;

                ModuleDescription? installedModuleDesc = null;
                if (installableModules.Count > 0)
                    installedModuleDesc = installableModules.FirstOrDefault(m => m.ModuleId == module.ModuleId);

                if (installedModuleDesc is null)
                {
                    var description = ModuleInstaller.ModuleDescriptionFromModuleConfig(module, true,
                                                                                        currentServerVersion,
                                                                                        _moduleSettings.ModulesDirPath,
                                                                                        _moduleSettings.PreInstalledModulesDirPath);
                    description.IsDownloadable = false;  
                    installableModules.Add(description);
                }
            };

            return new ModuleListResponse()
            {
                Modules = installableModules!
            };
        }

        /// <summary>
        /// Allows for a client to list the status of the backend analysis modules.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/status"/*, Name = "List Module Statuses"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ModuleResponseBase ListModulesStatus()
        {
            // Get the statuses
            var statuses = _moduleProcessService.ListProcessStatuses();
            var response = new ModuleStatusesResponse
            {
                Statuses = statuses
                            // .Where(module => module.Status != ProcessStatusType.NotEnabled)
                           .ToList()
            };

            return response;
        }

        /// <summary>
        /// Allows for a client to get a list of the HTML (and Javascript/CSS) that a module has
        /// provided for the AI Explorer. This javascript is injected into the explorer at runtime.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/explorer-html", Name = "Get Module Html inclusions")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ModuleResponseBase GetModulesExplorerUI()
        {
            var insertions = new List<ExplorerUI>(_installedModules.Count);
            foreach (var module in _installedModules.Values)
            {
                ExplorerUI? ui = module.GetExplorerUI();
                if (ui is not null && !ui.IsEmpty)
                    insertions.Add(ui);
            }

            var response = new ModuleExplorerUIResponse
            {
                UiInsertions = insertions
            };

            return response;
        }

        /// <summary>
        /// Manages requests to install the given module.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="version">The version of the module to install</param>
        /// <param name="noCache">Whether or not to ignore the download cache. If true, the module
        /// will always be freshly downloaded</param>
        /// <param name="verbosity">The amount of noise to output when installing</param>
        /// <returns>A Response Object.</returns>
        [HttpPost("install/{moduleId}/{version}", Name = "Install Module")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> InstallModuleAsync(string moduleId, string version,
                                                                 [FromQuery] bool noCache = false, 
                                                                 [FromQuery] LogVerbosity verbosity = LogVerbosity.Quiet)
        {
            var downloadTask = _moduleInstaller.DownloadAndInstallModuleAsync(moduleId, version,
                                                                              noCache, verbosity);
            (bool success, string error) = await downloadTask.ConfigureAwait(false);
            
            return success? new SuccessResponse() : CreateErrorResponse(error);
        }

        /// <summary>
        /// Manages requests to uninstall the given module.
        /// </summary>
        /// <returns>A Response Object.</returns>
        [HttpPost("uninstall/{moduleId}", Name = "Uninstall Module")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> UninstallModuleAsync(string moduleId)
        {
            (bool success, string error) = await _moduleInstaller.UninstallModuleAsync(moduleId)
                                                                 .ConfigureAwait(false);
            
            return success? new SuccessResponse() : CreateErrorResponse(error);
        }

        /// <summary>
        /// Gets the installation logs for the given module.
        /// </summary>
        /// <param name="moduleId">The module for which to get the logs</param>
        /// <returns>A Response Object.</returns>
        [HttpGet("install/{moduleId}/log", Name = "Get Install Logs")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ModuleResponseBase> GetInstallLogsAsync(string moduleId)
        {
            string? logs = await _moduleInstaller.GetInstallationSummary(moduleId)
                                                 .ConfigureAwait(false);
            return new ModuleResponse<string>() { Data = logs };
            
            /*
            // We should return an explanation if we can't return the install logs
            if (string.IsNullOrEmpty(error))
            {
                return new ModuleResponse<string>()
                {
                    data = logs
                };
            }

            return CreateErrorResponse(error);
            */
        }

        private ErrorResponse CreateErrorResponse(string message)
        {
            _logger.LogError(message);
            return new ErrorResponse(message);
        }
    }
}
