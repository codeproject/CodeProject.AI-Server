using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Modules;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeProject.AI.Server.Controllers
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
    ///     "ObjectDetectionYOLOv5-6.2": {
    ///         "CUSTOM_MODELS_DIR" : "C:\\BlueIris\\AI",
    ///         "MODEL_SIZE" : "Large"
    ///     },
    ///     "FaceProcessing": {
    ///         "AutoStart" : "False"
    ///     }
    /// }
    ///</example>
    public class SettingsDict : Dictionary<string, Dictionary<string, string>>
    { }

    /// <summary>
    /// For updating the settings on the server and modules.
    /// </summary>
    [Route("v1/settings")]          // legacy route
    [Route("v1/server/settings")]   // new route as of 2.4.0
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IConfiguration        _config;
        private readonly ServerOptions         _serverOptions;
        private readonly ModuleSettings        _moduleSettings;
        private readonly ModuleCollection      _installedModules;
        private readonly ModuleProcessServices _moduleProcessServices;
        private readonly string                _storagePath;
        private readonly ILogger               _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="config">The configuration</param>
        /// <param name="serverOptions">The server options</param>
        /// <param name="moduleSettings">The moduleSettings.</param>
        /// <param name="moduleCollectionOptions">The collection of modules.</param>
        /// <param name="moduleProcessServices">The Module Process Services.</param>
        /// <param name="logger">The logger</param>
        public SettingsController(IConfiguration config,
                                  IOptions<ServerOptions> serverOptions,
                                  ModuleSettings moduleSettings,
                                  IOptions<ModuleCollection> moduleCollectionOptions,
                                  ModuleProcessServices moduleProcessServices,
                                  ILogger<LogController> logger)
        {
            _config                = config;
            _serverOptions         = serverOptions.Value;
            _moduleSettings        = moduleSettings;
            _installedModules      = moduleCollectionOptions.Value;
            _moduleProcessServices = moduleProcessServices;
            _storagePath           = _config["ApplicationDataDir"] 
                                   ?? throw new ApplicationException("ApplicationDataDir is not defined in configuration");
            _logger                = logger;
        }

        /// <summary>
        /// Manages requests to add / update a single setting for a specific module.
        /// </summary>
        /// <returns>A <see cref="ServerResponse"/> Object.</returns>
        [HttpPost("{moduleId}" /*, Name = "UpsertSetting"*/)]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ServerResponse> UpsertSettingAsync(string moduleId,
                                                             [FromForm] string name, 
                                                             [FromForm] string value)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return new ServerErrorResponse("No module ID provided");

            _logger.LogInformation($"Update {moduleId}. Setting {name}={value}");

            // We've been toggling between passing a name/value structure, and passing individual
            // params. This just normalises it and helps us switch between the two modes until we
            // settle on one.
            var settings = new SettingsPair() { Name = name, Value = value };
            if (settings == null || string.IsNullOrWhiteSpace(settings.Name))
               return new ServerErrorResponse("No setting or setting name provided");

            ModuleConfig? module = _installedModules.GetModule(moduleId);
            if (module is null)
                return new ServerErrorResponse($"No module with ID {moduleId} found");

            bool success = false;

            // Special case
            if (settings.Name.EqualsIgnoreCase("Restart"))
            {
                success = await _moduleProcessServices.RestartProcess(module).ConfigureAwait(false);
            }
            else
            {
                // Make the change to the module's settings
                module.UpsertSetting(settings.Name, settings.Value);

                if (settings.Name.EqualsIgnoreCase("AutoStart") && settings.Value.EqualsIgnoreCase("false"))
                    _logger.LogInformation($"*** Stopping {module.Name}");
                else
                    _logger.LogInformation($"*** Restarting {module.Name} to apply settings change");

                // Restart the module and persist the settings
                if (await _moduleProcessServices.RestartProcess(module).ConfigureAwait(false))
                {
                    var settingStore = new PersistedOverrideSettings(_storagePath);
                    var currentUserSettings = await settingStore.LoadSettings().ConfigureAwait(false);

                    if (ModuleConfigExtensions.UpsertSettings(currentUserSettings, module.ModuleId!,
                                                              settings.Name, settings.Value))
                    {
                        success = await settingStore.SaveSettingsAsync(currentUserSettings)
                                                    .ConfigureAwait(false);
                    }
                }
            }

            return new ServerResponse { Success = success };
        }

        /// <summary>
        /// Manages requests to add / update settings for one or more modules.
        /// </summary>
        /// <returns>A <see cref="ServerResponse"/> Object.</returns>
        [HttpPost("" /*, Name = "UpsertSettings"*/)]
        [Produces("application/json")]
        //[Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ServerResponse> UpsertSettingsAsync([FromBody] SettingsDict settings)
        {
            if (!settings.Any())
                return new ServerErrorResponse("No settings provided");

            bool restartSuccess = true;

            // Load up the current persisted settings so we can update and re-save them

            var settingStore = new PersistedOverrideSettings(_storagePath);
            var currentUserSettings = await settingStore.LoadSettings().ConfigureAwait(false);

            // Keep tabs on which modules need to be restarted
            List<string>? moduleIdsToRestart = new();

            foreach (var moduleSetting in settings)
            {
                string moduleId = moduleSetting.Key;

                // Special case
                if (moduleId.EqualsIgnoreCase("Global"))
                {
                    // Update all settings based on what's in the global settings. We'll get back a
                    // list of affected modules that need restarting.
                    Dictionary<string, string> globalSettings = moduleSetting.Value;
                    moduleIdsToRestart = LegacyParams.UpdateSettings(globalSettings, _installedModules,
                                                                     currentUserSettings);

                    continue;
                }

                // Targeting a specific module
                ModuleConfig? module = _installedModules.GetModule(moduleId);
                if (module is null)
                    continue;

                foreach (var setting in moduleSetting.Value)
                {
                    // Update each setting value for this module (here and now)
                    module.UpsertSetting(setting.Key, setting.Value);

                    // Add this setting to the persisted override settings (settings will maintain
                    // after server restart)
                    ModuleConfigExtensions.UpsertSettings(currentUserSettings, module.ModuleId!,
                                                          setting.Key, setting.Value);
                }

                if (!moduleIdsToRestart.Contains(module.ModuleId!, StringComparer.OrdinalIgnoreCase))
                    moduleIdsToRestart.Add(module.ModuleId!);
            }

            // Restart the modules that were updated
            foreach (string moduleId in moduleIdsToRestart)
            {
                ModuleConfig? module = _installedModules.GetModule(moduleId);
                if (module is not null)
                {
                    var restartTask = _moduleProcessServices.RestartProcess(module);
                    restartSuccess = await restartTask.ConfigureAwait(false) && restartSuccess;
                }
            }

            // Only persist these override settings if all modules restarted successfully
            bool success = restartSuccess && await settingStore.SaveSettingsAsync(currentUserSettings)
                                                               .ConfigureAwait(false);

            return new ServerResponse { Success = success };
        }

        /// <summary>
        /// Returns a list of module settings. A GET request.
        /// </summary>
        /// <param name="moduleId">The name of the module for which to get the settings.</param>
        /// <returns>A list of settings.</returns>
        /// <response code="200">Returns the list of detected object information, if any.</response>
        /// <response code="400"></response>            
        [HttpGet("{moduleId}" /*, , Name = "List Settings"*/)]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ServerResponse ListSettings(string? moduleId)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                return new ServerErrorResponse("No module ID provided");

            ModuleConfig? module = _installedModules.GetModule(moduleId);
            if (module is null)
                return new ServerErrorResponse($"No module found with ID {moduleId}");

            Dictionary<string, string?> processEnvironmentVars = new();
            _serverOptions.AddEnvironmentVariables(processEnvironmentVars);
            module.AddEnvironmentVariables(processEnvironmentVars);

            // Expand the environment variables
            foreach (string key in processEnvironmentVars.Keys)
                processEnvironmentVars[key] = _moduleSettings.ExpandOption(processEnvironmentVars[key]);

            var response = new SettingsResponse
            {
                Success  = true,
                Settings = new
                {
                    Autostart             = module.LaunchSettings!.AutoStart ?? false,
                    LogVerbosity          = module.LaunchSettings!.LogVerbosity,
                    PostStartPauseSecs    = module.LaunchSettings!.PostStartPauseSecs,
                    Parallelism           = module.LaunchSettings?.Parallelism,
                    InstallGPU            = module.GpuOptions?.InstallGPU,
                    EnableGPU             = module.GpuOptions?.EnableGPU,
                    AcceleratorDeviceName = module.GpuOptions?.AcceleratorDeviceName,
                    HalfPrecision         = module.GpuOptions?.HalfPrecision
                },
                EnvironmentVariables = processEnvironmentVars
            };

            return response;
        }
    }
}
