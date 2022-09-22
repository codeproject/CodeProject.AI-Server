using CodeProject.AI.API.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeProject.AI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// A settings name/value
    /// </summary>
    public class SettingsPair
    {
        /// <summary>
        /// Gets or sets the setting name
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// Gets or sets the value
        /// </summary>
        public string? Value { get; set; } = null;
    }

    /// <summary>
    /// For updating the settings on the server and modules.
    /// </summary>
    [Route("v1/settings")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IConfiguration   _config;
        private readonly FrontendOptions  _frontendOptions;

        // TODO: this really should be a singleton global that is initialized
        //       from the configuration but can be updated after.
        private readonly ModuleCollection _modules;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">The configuration</param>
        /// <param name="options">The Frontend Options</param>
        /// <param name="modules">The Modules configuration.</param>
        public SettingsController(IConfiguration config,
                                  IOptions<FrontendOptions> options, 
                                  IOptions<ModuleCollection> modules)
        {
            _config          = config;
            _modules         = modules.Value;
            _frontendOptions = options.Value;
        }

        /// <summary>
        /// Manages requests to add / update settings.
        /// </summary>
        /// <returns>A Response Object.</returns>
        [HttpPost("{moduleId}", Name = "UpsertSetting")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> UpsertSettingAsync(string moduleId, [FromForm] string name, 
                                                           [FromForm] string value)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return new ErrorResponse("No module ID provided");

            // We've been toggling between passing name/value and passing discrete params. This
            // just normalises it for later.
            var settings = new SettingsPair() { Name = name, Value = value };
            if (settings == null || string.IsNullOrWhiteSpace(settings.Name))
               return new ErrorResponse("No setting or setting name provided");

            // Get the backend processor (DI won't work here due to the order things get fired up
            // in Main.
            var backend = HttpContext.RequestServices.GetServices<IHostedService>()
                                                     .OfType<BackendProcessRunner>()
                                                     .FirstOrDefault();
            if (backend is null)
                return new ErrorResponse("Unable to get list of modules");

            if (!backend.StartupProcesses.TryGetValue(moduleId, out ModuleConfig? module) || module == null)
                return new ErrorResponse($"No module with ID {moduleId} found");

            module.UpsertSetting(settings.Name, settings.Value);

            bool success = false;
            if (await backend.RestartProcess(module))
            {
                string appDataDir       = _config["ApplicationDataDir"];
                string settingsFilePath = Path.Combine(appDataDir, "modulesettings.json");

                /* PROBLEM: This saves ALL the settings, meaning that the modulesettings files in
                            the module's folders will no longer be effective. We save a file with
                            all settings (basically a snapshot) then load that file last, meaning 
                            it (the old snapshot) will override all settings in the modulesettings
                            files. What we need to do is load the saved settings, add the new
                            settings, then save that file. But save ONLY the setting that was just
                            modified.

                _modules.SaveAllSettings(settingsFilePath);
                */

                // Probably worth combining this into one method.
                var allSettings = await ModuleConfigExtensions.LoadSettings(settingsFilePath);
                if (ModuleConfigExtensions.UpsertSettings(allSettings, module.ModuleId!,
                                                          settings.Name, settings.Value))
                {
                    success = await ModuleConfigExtensions.SaveSettings(allSettings, settingsFilePath);
                }
            }

            return new ResponseBase { success = success };
        }

        /// <summary>
        /// Returns a list of log entries. A GET request.
        /// </summary>
        /// <param name="moduleId">The name of the module for which to get the settings.</param>
        /// <returns>A list of settings.</returns>
        /// <response code="200">Returns the list of detected object information, if any.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        [HttpGet("{moduleId}", Name = "List Settings")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase ListSettings(string? moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return new ErrorResponse("No module ID provided");

            if (!_modules.TryGetValue(moduleId, out ModuleConfig? module) || module == null)
                return new ErrorResponse($"No module found with ID {moduleId}");

            Dictionary<string, string?> processEnvironmentVars = new();
            _frontendOptions.AddEnvironmentVariables(processEnvironmentVars);
            module.AddEnvironmentVariables(processEnvironmentVars);

            var response = new SettingsResponse
            {
                success  = true,
                settings = new
                {
                    activate           = module.Activate ?? false,
                    supportGPU         = module.SupportGPU,
                    parallelism        = module.Parallelism,
                    postStartPauseSecs = module.PostStartPauseSecs
                },
                environmentVariables = processEnvironmentVars
            };

            return response;
        }
    }
}
