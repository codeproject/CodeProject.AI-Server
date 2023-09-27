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
        private readonly VersionConfig    _versionConfig;
        private readonly ModuleOptions    _moduleOptions;
        private readonly ModuleSettings   _moduleSettings;
        private readonly ModuleCollection _moduleCollection;
        private readonly ModuleInstaller  _moduleInstaller;
        private readonly ILogger          _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="versionOptions">The server version Options</param>
        /// <param name="moduleSettings">The module settings instance</param>
        /// <param name="moduleOptions">The module options instance</param>
        /// <param name="moduleCollection">The collection of modules.</param>
        /// <param name="moduleInstaller">The module installer instance.</param>
        /// <param name="logger">The logger</param>
        public ModuleController(IOptions<VersionConfig>    versionOptions,
                                ModuleSettings             moduleSettings,
                                IOptions<ModuleOptions>    moduleOptions,
                                IOptions<ModuleCollection> moduleCollection,
                                ModuleInstaller moduleInstaller,
                                ILogger<LogController> logger)
        {
            _versionConfig    = versionOptions.Value;
            _moduleOptions    = moduleOptions.Value;
            _moduleSettings   = moduleSettings;
            _moduleInstaller  = moduleInstaller;
            _moduleCollection = moduleCollection.Value;
            _logger           = logger;
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis services.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpPost("upload", Name = "UploadModule")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> UploadModule()
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
                string downloadPath  = _moduleSettings.DownloadedModulePackagesPath 
                                     + Path.DirectorySeparatorChar + tempName + ".zip";

                using (Stream fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                {
                    await uploadedFile.CopyToAsync(fileStream).ConfigureAwait(false);
                    fileStream.Close();
                }

                (bool success, string error) = await _moduleInstaller.InstallModuleAsync(downloadPath, null)
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
        public async Task<ResponseBase> ListInstalledModules()
        {
            if (_moduleCollection?.Count is null || _moduleCollection.Count == 0)
                return CreateErrorResponse("No backend modules have been registered");

            string currentServerVersion = _versionConfig.VersionInfo!.Version;

            var modules = _moduleCollection?.Values?
                            .Select(module => ModuleInstaller.ModuleDescriptionFromModuleConfig(module, true, 
                                                                                                currentServerVersion,
                                                                                                _moduleSettings.ModulesPath,
                                                                                                _moduleSettings.PreInstalledModulesPath))
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
                modules = modules
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
        public async Task<ResponseBase> ListAvailableModules()
        {
            List<ModuleDescription> moduleList = await _moduleInstaller.GetInstallableModules()
                                                                       .ConfigureAwait(false);

            return new ModuleListResponse()
            {
                modules = moduleList!
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
        public async Task<ResponseBase> ListAllModules()
        {
            List<ModuleDescription> installableModules = await _moduleInstaller.GetInstallableModules()
                                                                               .ConfigureAwait(false) 
                                                       ?? new List<ModuleDescription>();
            
            string currentServerVersion = _versionConfig.VersionInfo!.Version;

            // Go through each module we currently have registered, and if that module doesn't 
            // appear in the available downloads then add it to the list of all modules and
            // ensure it's set as 'Installed'.
            foreach (ModuleConfig? module in _moduleCollection.Values)
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
                                                                                        _moduleSettings.ModulesPath,
                                                                                        _moduleSettings.PreInstalledModulesPath);
                    description.IsDownloadable = false;  
                    installableModules.Add(description);
                }
            };

            return new ModuleListResponse()
            {
                modules = installableModules!
            };
        }

        /// <summary>
        /// Manages requests to install the given module.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="version">The version of the module to install</param>
        /// <param name="noCache">Whether or not to ignore the download cache. If true, the module
        /// will always be freshly downloaded</param>
        /// <returns>A Response Object.</returns>
        [HttpPost("install/{moduleId}/{version}/{nocache:bool?}", Name = "Install Module")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> InstallModuleAsync(string moduleId, string version,
                                                           bool noCache = false)
        {
            var downloadTask = _moduleInstaller.DownloadAndInstallModuleAsync(moduleId, version, noCache);
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
        public async Task<ResponseBase> UninstallModuleAsync(string moduleId)
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
        public async Task<ResponseBase> GetInstallLogsAsync(string moduleId)
        {
            string? logs = await _moduleInstaller.GetInstallationSummary(moduleId)
                                                 .ConfigureAwait(false);
            return new ModuleResponse<string>() { data = logs };
            
            /*
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
