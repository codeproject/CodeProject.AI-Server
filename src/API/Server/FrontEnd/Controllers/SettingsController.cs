using CodeProject.AI.API.Common;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System;
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
    /// A Dictionary of Dictionaries of settings.
    /// </summary>
    /// <remarks>
    /// The Key in the outer dictionary is the section or module name.
    /// The Key in the inner dictionary is the setting name.
    /// The Value in the inner dictionary is the setting value
    /// </remarks>
    /// <example>
    /// {
    ///     "Global":{
    ///         "USE_CUDA" : "True"
    ///     },
    ///     "Objectdetectionyolo": {
    ///         "CUSTOM_MODELS_DIR" : "C:\\BlueIris\\AI",
    ///         "MODEL_SIZE" : "Large"
    ///     },
    ///     "FaceProcessing": {
    ///         "Activate" : "False"
    ///     }
    /// }
    ///</example>
    public class SettingsDict : Dictionary<string, Dictionary<string, string>>
    { }

    /// <summary>
    /// For updating the settings on the server and modules.
    /// </summary>
    [Route("v1/settings")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IConfiguration   _config;
        private readonly FrontendOptions  _frontendOptions;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">The configuration</param>
        /// <param name="options">The Frontend Options</param>
        public SettingsController(IConfiguration config,
                                  IOptions<FrontendOptions> options)
        {
            _config          = config;
            _frontendOptions = options.Value;
        }

        /// <summary>
        /// Manages requests to add / update a single setting for a specific module.
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

            // We've been toggling between passing a name/value structure, and passing individual
            // params. This just normalises it and helps us switch between the two modes until we
            // settle on one.
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

            ModuleConfig? module = backend.StartupProcesses.GetModule(moduleId);
            if (module is null)
                return new ErrorResponse($"No module with ID {moduleId} found");

            // Make the change to the module's settings
            module.UpsertSetting(settings.Name, settings.Value);

            // Restart the module and persist the settings
            bool success = false;
            if (await backend.RestartProcess(module))
            {
                var settingStore = new PersistedOverrideSettings(_config["ApplicationDataDir"]);
                var overrideSettings = await settingStore.LoadSettings();

                if (ModuleConfigExtensions.UpsertSettings(overrideSettings, module.ModuleId!,
                                                          settings.Name, settings.Value))
                {
                    success = await settingStore.SaveSettings(overrideSettings);
                }
            }

            return new ResponseBase { success = success };
        }

        /// <summary>
        /// Manages requests to add / update settings for one or more modules.
        /// </summary>
        /// <returns>A Response Object.</returns>
        [HttpPost("", Name = "UpsertSettings")]
        [Produces("application/json")]
        //[Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> UpsertSettingsAsync([FromBody] SettingsDict settings)
        {
            if (!settings.Any())
                return new ErrorResponse("No settings provided");

            // Get the backend processor (DI won't work here due to the order things get fired up
            // in Main.
            var backend = HttpContext.RequestServices.GetServices<IHostedService>()
                                                     .OfType<BackendProcessRunner>()
                                                     .FirstOrDefault();
            if (backend is null)
                return new ErrorResponse("Unable to get list of modules");

            bool restartSuccess = true;

            // Load up the current persisted settings so we can update and re-save them
            var settingStore = new PersistedOverrideSettings(_config["ApplicationDataDir"]);
            var overrideSettings = await settingStore.LoadSettings();

            // Keep tabs on which modules need to be restarted
            List<string>? moduleIdsToRestart = new();

            foreach (var moduleSetting in settings)
            {
                string moduleId = moduleSetting.Key;

                // Special case
                if (moduleId.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    // Update all settings based on what's in the global settings. We'll get back a
                    // list of affected modules that need restarting.
                    Dictionary<string, string> globalSettings = moduleSetting.Value;
                    ModuleCollection modules                  = backend.StartupProcesses;
                    moduleIdsToRestart = LegacyParams.UpdateSettings(globalSettings, modules,
                                                                     overrideSettings);

                    continue;
                }

                // Targeting a specific module
                ModuleConfig? module = backend.StartupProcesses.GetModule(moduleId);
                if (module is null)
                    continue;

                foreach (var setting in moduleSetting.Value)
                {
                    // Update each setting value for this module (here and now)
                    module.UpsertSetting(setting.Key, setting.Value);

                    // Add this setting to the persisted override settings (settings will maintain
                    // after server restart)
                    ModuleConfigExtensions.UpsertSettings(overrideSettings, module.ModuleId!,
                                                          setting.Key, setting.Value);
                }

                if (!moduleIdsToRestart.Contains(module.ModuleId!, StringComparer.OrdinalIgnoreCase))
                    moduleIdsToRestart.Add(module.ModuleId!);
            }

            // Restart the modules that were updated
            foreach (string moduleId in moduleIdsToRestart)
            {
                ModuleConfig? module = backend.StartupProcesses.GetModule(moduleId);
                if (module is not null)
                    restartSuccess = await backend.RestartProcess(module) && restartSuccess;
            }

            // Only persist these override settings if all modules restarted successfully
            bool success = restartSuccess && await settingStore.SaveSettings(overrideSettings);

            return new ResponseBase { success = success };
        }

        /// <summary>
        /// Returns a list of module settings. A GET request.
        /// </summary>
        /// <param name="moduleId">The name of the module for which to get the settings.</param>
        /// <returns>A list of settings.</returns>
        /// <response code="200">Returns the list of detected object information, if any.</response>
        /// <response code="400"></response>            
        [HttpGet("{moduleId}", Name = "List Settings")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ResponseBase ListSettings(string? moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return new ErrorResponse("No module ID provided");

            // Get the backend processor (DI won't work here due to the order things get fired up
            // in Main.
            var backend = HttpContext.RequestServices.GetServices<IHostedService>()
                                                     .OfType<BackendProcessRunner>()
                                                     .FirstOrDefault();
            if (backend is null)
                return new ErrorResponse("Unable to get list of modules");

            ModuleConfig? module = backend.StartupProcesses.GetModule(moduleId);
            if (module is null)
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
