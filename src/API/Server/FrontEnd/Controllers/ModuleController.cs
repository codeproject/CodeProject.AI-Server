using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using CodeProject.AI.API.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// For managing the optional modules that form part of the system.
    /// </summary>
    [Route("v1/module")]
    [ApiController]
    public class ModuleController : ControllerBase
    {
        private readonly ModuleOptions   _moduleOptions;
        private readonly ModuleRunner    _moduleRunner;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ILogger         _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="moduleOptions">The module Options</param>
        /// <param name="moduleRunner">The module runner instance</param>
        /// <param name="moduleInstaller">The module installer instance</param>
        /// <param name="logger">The logger</param>
        public ModuleController(IOptions<ModuleOptions> moduleOptions,
                                ModuleRunner moduleRunner,
                                ModuleInstaller moduleInstaller,
                                ILogger<LogController> logger)
        {
            _moduleOptions   = moduleOptions.Value;
            _moduleRunner    = moduleRunner;
            _moduleInstaller = moduleInstaller;
            _logger          = logger;
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis services.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list/installed", Name = "ListInstalledModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase ListInstalledModules()
        {
            if (_moduleRunner.Modules?.Count is null || _moduleRunner.Modules.Count == 0)
                return CreateErrorResponse("No backend modules have been registered");

            var response = new ModuleListResponse
            {
                modules = _moduleRunner!.Modules?.Values?
                                        .Select(module => ModuleInstaller.ModuleDescriptionFromModuleConfig(module, true))
                                        .ToList() ?? new List<ModuleDescription>()
            };

            return response;
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
            List<ModuleDescription> moduleList = await _moduleInstaller.GetDownloadableModules();
            var response = new ModuleListResponse()
            {
                modules = moduleList!
            };

            return response;
        }

        /// <summary>
        /// Allows for a client to list the installed backend analysis services.
        /// </summary>
        /// <returns>A ResponseBase object.</returns>
        [HttpGet("list", Name = "ListAllModules")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> ListAllModules()
        {
            if (_moduleRunner.ProcessStatuses is null)
                return CreateErrorResponse("No analysis modules have been registered");

            List<ModuleDescription> downloadableModules = await _moduleInstaller.GetDownloadableModules() 
                                                        ?? new List<ModuleDescription>();
            
            // Go through each module we currently have registered, and if that module doesn't 
            // appear in the available downloads then add it to the list of all modules and
            // ensure it's set as 'Installed'.
            foreach (ModuleConfig? module in _moduleRunner.Modules.Values)
            {
                if (module?.Valid != true)
                    continue;

                ModuleDescription? installedModuleDesc = null;
                if (downloadableModules.Count > 0)
                    installedModuleDesc = downloadableModules.FirstOrDefault(m => m.ModuleId == module.ModuleId);

                if (installedModuleDesc is null)
                {
                    // We have an installed module that isn't in our module registry. Add it to the
                    // list of installable modules in order to allow it to be, ironically, uninstalled
                    downloadableModules.Add(ModuleInstaller.ModuleDescriptionFromModuleConfig(module, true));
                }
            };

            var response = new ModuleListResponse()
            {
                modules = downloadableModules!
            };

            return response;
        }

        /// <summary>
        /// Manages requests to install the given module.
        /// </summary>
        /// <param name="moduleId">The module to install</param>
        /// <param name="version">The version of the module to install</param>
        /// <returns>A Response Object.</returns>
        [HttpPost("install/{moduleId}/{version}", Name = "Install Module")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> InstallModuleAsync(string moduleId, string version)
        {
            (bool success, string error) = await _moduleInstaller.InstallModuleAsync(moduleId, version);
            
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
            (bool success, string error) = await _moduleInstaller.UninstallModuleAsync(moduleId);
            
            return success? new SuccessResponse() : CreateErrorResponse(error);
        }

        private ErrorResponse CreateErrorResponse(string message)
        {
            _logger.LogError(message);
            return new ErrorResponse(message);
        }
    }
}
