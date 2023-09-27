using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// The set of modules for backend processing.
    /// </summary>
    public class ModuleCollection : ConcurrentDictionary<string, ModuleConfig>
    {
        /// <summary>
        /// This constructor allows our modules collection to be case insensitive on the key.
        /// </summary>
        public ModuleCollection() : base(StringComparer.OrdinalIgnoreCase) { }
    }

    /// <summary>
    /// Information required to start the backend processes.
    /// </summary>
    public class ModuleConfig : ModuleBase
    {
        /// <summary>
        /// Gets or sets the previous incarnation of 'AutoStart' (see below). This value will still
        /// live in some persistent config files on older systems, so we need to enable it to be
        /// loaded, but should always transfer this value to AutoStart and null this value so it
        /// doesn't get written back.
        /// </summary>
        public bool? Activate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this process should be activated on startup if
        /// no instruction to the contrary is seen. A default "Start me up" flag.
        /// </summary>
        public bool? AutoStart { get; set; }

        /// <summary>
        /// Gets or sets the runtime used to execute the file at FilePath. For example, the runtime
        /// could be "dotnet" or "python3.9". 
        /// </summary>
        public string? Runtime { get; set; }

        /// <summary>
        /// Gets or sets where the runtime executables for this module should be found. Valid
        /// values are:
        /// "Shared" - the runtime is installed in the /modules folder 
        /// "Local" - the runtime is installed locally in this modules folder
        /// </summary>
        /// <remarks>
        /// We set the default location to "Local" as this is the safest option and resolves
        /// an issue with installing in Docker as old modules do not have this value, and in
        /// Docker all modules are installed as Local.
        /// </remarks>
        public string RuntimeLocation  { get; set; } = "Local";

        /// <summary>
        /// Gets or sets the command to execute the file at FilePath. If set, this overrides Runtime.
        /// An example would be "/usr/bin/python3". This property allows you to specify an explicit
        /// command in case the necessary runtime hasn't been registered, or in case you need to
        /// provide specific flags or naming alternative when executing the FilePath on different
        /// platforms. 
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the path to the startup file relative to the module directory.
        /// </summary>
        /// <remarks>
        /// If no Runtime or Command is specified then a default runtime will be chosen based on
        /// the extension. Currently this is:
        ///     .py  => it will be started with the default Python interpreter
        ///     .dll => it will be started with the .NET runtime.
        /// 
        /// TODO: this is currently relative to the modules directory but should be relative
        /// to the directory containing the modulesettings.json file. This should be changed when
        /// the modules read the modulesettings.json files for their configuration.
        /// </remarks>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this process should support GPUs. This doesn't
        /// direct that a GPU must be used, but instead alerts that app that it should support a GPU
        /// if possible. Setting this to false means "even if you can support a GPU, don't".
        /// </summary>
        public bool? SupportGPU { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating the degree of parallelism (number of threads or number
        /// of tasks, depending on the implementation) to launch when running this module.
        /// 0 = default, which is (Number of CPUs - 1).
        /// </summary>
        public int? Parallelism { get; set; }

        /// <summary>
        /// Gets or sets the device name (eg CUDA device number, TPU device name) to use. Be careful to
        /// ensure this device exists.
        /// </summary>
        public string? AcceleratorDeviceName { get; set; }

        /// <summary>
        /// Gets or sets whether to use half-precision floating point ops on the hardware in use. This
        /// is an option for more recent PyTorch libraries and can speed things up nicely. Can be 'enable',
        /// 'disable' or 'force'
        /// </summary>
        public string? HalfPrecision { get; set; } = "enable";

        /// <summary>
        /// Gets or sets the logging noise level. Quiet = only essentials, Info = anything meaningful,
        /// Loud = the kitchen sink. Default is Info.
        /// </summary>
        public LogVerbosity? LogVerbosity { get; set; } // = LogVerbosity.Info;

        /// <summary>
        /// Gets or sets the number of seconds this module should pause after starting to ensure 
        /// any resources that require startup (eg GPUs) are fully activated before moving on.
        /// </summary>
        public int? PostStartPauseSecs { get; set; } = 3;

        /// <summary>
        /// Gets or sets the name of the queue this module should service when processing commands.
        /// </summary>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the information to pass to the backend analysis modules.
        /// </summary>
        public Dictionary<string, object>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets a list of RouteMaps.
        /// </summary>
        public ModuleRouteInfo[] RouteMaps { get; set; } = Array.Empty<ModuleRouteInfo>();

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        public bool Valid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ModuleId) &&
                       !string.IsNullOrWhiteSpace(Name)     &&
                       (!string.IsNullOrWhiteSpace(Command) || !string.IsNullOrWhiteSpace(Runtime)) &&
                       !string.IsNullOrWhiteSpace(FilePath) &&
                       RouteMaps?.Length > 0;
            }
        }

        /// <summary>
        /// Gets a text summary of the settings for this module.
        /// </summary>
        public string SettingsSummary
        {
            get
            {
                // Allow the module path to wrap.
                // var path = ModulePath.Replace("\\", "\\<wbr>");
                // path = path.Replace("/", "/<wbr>");

                // or not...
                var path = ModulePath;

                var summary = new StringBuilder();
                summary.AppendLine($"Module '{Name}' {Version} (ID: {ModuleId})");
                summary.AppendLine($"Module Path:   {path}");
                summary.AppendLine($"AutoStart:     {AutoStart}");
                summary.AppendLine($"Queue:         {Queue}");
                summary.AppendLine($"Platforms:     {string.Join(',', Platforms)}");
                summary.AppendLine($"GPU:           Support {((SupportGPU == true)? "enabled" : "disabled")}");
                summary.AppendLine($"Parallelism:   {Parallelism}");
                summary.AppendLine($"Accelerator:   {AcceleratorDeviceName}");
                summary.AppendLine($"Half Precis.:  {HalfPrecision}");
                summary.AppendLine($"Runtime:       {Runtime}");
                summary.AppendLine($"Runtime Loc:   {RuntimeLocation}");
                summary.AppendLine($"FilePath:      {FilePath}");
                summary.AppendLine($"Pre installed: {PreInstalled}");
                //summary.AppendLine($"Module Dir:  {ModulePath}");
                summary.AppendLine($"Start pause:   {PostStartPauseSecs} sec");
                summary.AppendLine($"LogVerbosity:  {LogVerbosity}");
                summary.AppendLine($"Valid:         {Valid}");
                summary.AppendLine($"Environment Variables");

                if (EnvironmentVariables is not null)
                {
                    int maxLength = EnvironmentVariables.Max(x => x.Key.ToString().Length);
                    foreach (var envVar in EnvironmentVariables)
                        summary.AppendLine($"   {envVar.Key.PadRight(maxLength)} = {envVar.Value}");
                }

                return summary.ToString().Trim();
            }
        }
    }

    /// <summary>
    /// Extension methods for the ModuleConfig class
    /// </summary>
    public static class ModuleConfigExtensions
    {
        /// <summary>
        /// ModuleConfig objects are typically created by deserialising a JSON file so we don't get
        /// a chance at create time to supply supplementary information or adjust values that may
        /// not have been set (eg moduleId). This method provides that opportunity.
        /// </summary>
        /// <param name="module">This module that requires initialisation</param>
        /// <param name="moduleId">The id of the module. This isn't included in the object in JSON
        /// file, instead, the moduleId is the key for the module's object in the JSON file</param>
        /// <param name="modulesPath">The path to the folder containing all downloaded and installed
        /// modules</param>
        /// <param name="preInstalledModulesPath">The path to the folder containing all pre-installed
        /// modules</param>
        /// <remarks>Modules are usually downloaded and installed in the modulesPAth, but we can
        /// 'pre-install' them in situations like a Docker image. We pre-install modules in a
        /// separate folder than the downloaded and installed modules in order to avoid conflicts 
        /// (in Docker) when a user maps a local folder to the modules dir. Doing this to the 'pre
        /// installed' dir would make the contents (the preinstalled modules) disappear.</remarks>
        public static void Initialise(this ModuleConfig module, string moduleId, string modulesPath,
                                      string preInstalledModulesPath)
        {
            if (module is null)
                return;

            module.ModuleId = moduleId;

            // Currently these are unused, but should replace calls to GetModulePath / GetWorkingDirectory
            if (module.PreInstalled)
                module.ModulePath = Path.Combine(preInstalledModulesPath, module.ModuleId!);
            else
                module.ModulePath = Path.Combine(modulesPath, module.ModuleId!);

            module.WorkingDirectory = module.ModulePath; // This once was allowed to be different to ModulePath

            if (string.IsNullOrEmpty(module.Queue))
                module.Queue = moduleId.ToLower() + "_queue";

            if (module.LogVerbosity == LogVerbosity.Unknown)
                module.LogVerbosity = LogVerbosity.Info;

            // Transfer old legacy value to new replacement property if it exists, and no new value
            // was set
            if (module.Activate is not null && module.AutoStart is null)
                module.AutoStart = module.Activate;
            if ((module.VersionCompatibililty?.Length ?? 0) > 0 && (module.ModuleReleases?.Length ?? 0) == 0)
                module!.ModuleReleases = module!.VersionCompatibililty!;

            // No longer used. These properties are still here to allow us to load legacy config files.
            module.Activate              = null;
            module.VersionCompatibililty = Array.Empty<ModuleRelease>();
        }
    
        /// <summary>
        /// Gets a value indicating whether or not this module is actually available. This depends 
        /// on having valid commands, settings, and importantly, being supported on this platform.
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="platform">The platform being tested</param>
        /// <param name="currentServerVersion">The version of the server, or null to ignore version issues</param>
        /// <returns>true if the module is available; false otherwise</returns>
        public static bool Available(this ModuleConfig module, string platform, string? currentServerVersion)
        {
            if (module is null)
                return false;

            // First check: Does this module's version encompass a range of server versions that are
            // compatible with the current server?
            bool versionOK = string.IsNullOrWhiteSpace(currentServerVersion);
            if (!versionOK)
            {
                if (module.ModuleReleases?.Any() ?? false)
                {
                    foreach (ModuleRelease release in module.ModuleReleases)
                    {
                        if (release.ServerVersionRange is null || release.ServerVersionRange.Length < 2)
                            continue;

                        string? minServerVersion = release.ServerVersionRange[0];
                        string? maxServerVersion = release.ServerVersionRange[1];

                        if (string.IsNullOrEmpty(minServerVersion)) minServerVersion = "0.0";
                        if (string.IsNullOrEmpty(maxServerVersion)) maxServerVersion = currentServerVersion;

                        if (release.ModuleVersion == module.Version &&
                            VersionInfo.Compare(minServerVersion, currentServerVersion) <= 0 &&
                            VersionInfo.Compare(maxServerVersion, currentServerVersion) >= 0)
                        {
                            versionOK = true;
                            break;
                        }
                    }
                }
                else // old modules will not have ModuleReleases, but we are backward compatible
                {
                    versionOK = true;
                }
            }

            // Second check: Is this module available on this platform?
            return module.Valid && versionOK &&
                   ( module.Platforms!.Any(p => p.ToLower() == "all") ||
                     module.Platforms!.Any(p => p.ToLower() == platform.ToLower()) );
        }

        /// <summary>
        /// Sets or updates a value in the ModuleConfig.
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
        public static void UpsertSetting(this ModuleConfig module, string name,
                                         string? value)
        {
            // Handle pre-defined global values first
            if (name.EqualsIgnoreCase("Activate") || name.EqualsIgnoreCase("AutoStart"))
            {
                module.AutoStart = value?.ToLower() == "true";
            }
            else if (name.EqualsIgnoreCase("SupportGPU"))
            {
                module.SupportGPU = value?.ToLower() == "true";
            }
            else if (name.EqualsIgnoreCase("Parallelism"))
            {
                if (int.TryParse(value, out int parallelism))
                    module.Parallelism = parallelism;
            }
            else if (name.EqualsIgnoreCase("UseHalfPrecision"))
            {
                module.HalfPrecision = value;
            }
            else if (name.EqualsIgnoreCase("AcceleratorDeviceName"))
            {
                module.AcceleratorDeviceName = value;
            }
            else if (name.EqualsIgnoreCase("LogVerbosity"))
            {
                if (Enum.TryParse(value, out LogVerbosity verbosity))
                    module.LogVerbosity = verbosity;
            }
            else if (name.EqualsIgnoreCase("PostStartPauseSecs"))
            {
                if (int.TryParse(value, out int pauseSec))
                    module.PostStartPauseSecs = pauseSec;
            }
            else
            {
                // with lock
                module.EnvironmentVariables ??= new();

                if (module.EnvironmentVariables.ContainsKey(name.ToUpper()))
                    module.EnvironmentVariables[name.ToUpper()] = value ?? string.Empty;
                else
                    module.EnvironmentVariables.TryAdd(name.ToUpper(), value ?? string.Empty);
            }
        }

        /// <summary>
        /// Gets a text summary of the settings for this module.
        /// </summary>
        public static string SettingsSummary(this ModuleConfig module, ModuleSettings moduleSettings,
                                             string? currentModulePath = null)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Module '{module.Name}' (ID: {module.ModuleId})");
            summary.AppendLine($"AutoStart:     {module.AutoStart}");
            summary.AppendLine($"Queue:         {module.Queue}");
            summary.AppendLine($"Platforms:     {string.Join(',', module.Platforms)}");
            summary.AppendLine($"GPU:           Support {((module.SupportGPU == true)? "enabled" : "disabled")}");
            summary.AppendLine($"Parallelism:   {module.Parallelism}");
            summary.AppendLine($"Accelerator:   {module.AcceleratorDeviceName}");
            summary.AppendLine($"Half Precis.:  {module.HalfPrecision}");
            summary.AppendLine($"Runtime:       {module.Runtime}");
            summary.AppendLine($"Runtime Loc:   {module.RuntimeLocation}");
            summary.AppendLine($"FilePath:      {module.FilePath}");
            summary.AppendLine($"Pre installed: {module.PreInstalled}");
            //summary.AppendLine($"Module Dir:  {module.ModulePath}");
            summary.AppendLine($"Start pause:   {module.PostStartPauseSecs} sec");
            summary.AppendLine($"LogVerbosity:  {module.LogVerbosity}");
            summary.AppendLine($"Valid:         {module.Valid}");
            summary.AppendLine($"Environment Variables");

            if (module.EnvironmentVariables is not null)
            {
                int maxLength = module.EnvironmentVariables.Max(x => x.Key.ToString().Length);
                foreach (var envVar in module.EnvironmentVariables)
                {
                    var value = moduleSettings.ExpandOption(envVar.Value?.ToString() ?? string.Empty,
                                                            currentModulePath);
                    summary.AppendLine($"   {envVar.Key.PadRight(maxLength)} = {envVar.Value}");
                }
            }

            return summary.ToString().Trim();
        }

        /// <summary>
        /// Sets or updates a value in the settings JSON structure.
        /// </summary>
        /// <param name="settings">This modules settings</param>
        /// <param name="moduleId">The id of the module``</param>
        /// <param name="name">The name of the setting</param>
        /// <param name="value">The value of the setting</param>
        public static bool UpsertSettings(JsonObject? settings, string moduleId,
                                          string name, string value)
        {
            if (settings is null)
                return false;

            // Lots of try/catch since this has been a point of issue and it's good to narrow it down
            try
            {
                if (!settings.ContainsKey("Modules") || settings["Modules"] is null)
                    settings["Modules"] = new JsonObject();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to create root modules object in settings: {e.Message}");
                return false;
            }

            JsonObject? allModules = null;
            try
            {
                allModules = settings["Modules"] as JsonObject;
                allModules ??= new JsonObject();

                if (!allModules.ContainsKey(moduleId) || allModules[moduleId] is null)
                    allModules[moduleId] = new JsonObject();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to create module object in modules collection: {e.Message}");
                return false;
            }

            try
            {
                var moduleSettings = (JsonObject)allModules[moduleId]!;

                // Handle pre-defined global values first
                if (name.EqualsIgnoreCase("Activate") || name.EqualsIgnoreCase("AutoStart"))
                {
                    moduleSettings["AutoStart"] = value?.ToLower() == "true";
                }
                else if (name.EqualsIgnoreCase("SupportGPU"))
                {
                    moduleSettings["SupportGPU"] = value?.ToLower() == "true";
                }
                else if (name.EqualsIgnoreCase("Parallelism"))
                {
                    if (int.TryParse(value, out int parallelism))
                        moduleSettings["Parallelism"] = parallelism;
                }
                else if (name.EqualsIgnoreCase("UseHalfPrecision"))
                {
                    moduleSettings["HalfPrecision"] = value;
                }
                else if (name.EqualsIgnoreCase("AcceleratorDeviceName"))
                {
                    moduleSettings["AcceleratorDeviceName"] = value;
                }
                else if (name.EqualsIgnoreCase("LogVerbosity"))
                {
                    if (Enum.TryParse(value, out LogVerbosity verbosity))
                        moduleSettings["LogVerbosity"] = verbosity.ToString();
                }
                else if (name.EqualsIgnoreCase("PostStartPauseSecs"))
                {
                    if (int.TryParse(value, out int pauseSec))
                        moduleSettings["PostStartPauseSecs"] = pauseSec;
                }
                else
                {
                    if (moduleSettings["EnvironmentVariables"] is null)
                        moduleSettings["EnvironmentVariables"] = new JsonObject();

                    var environmentVars = (JsonObject)moduleSettings["EnvironmentVariables"]!;
                    environmentVars[name.ToUpper()] = value;
                }

                // Clean up legacy values
                if (moduleSettings["Activate"] is not null && moduleSettings["AutoStart"] is null)
                    moduleSettings["AutoStart"] = moduleSettings["Activate"];
                moduleSettings.Remove("Activate");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to update module setting: {e.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds (and overrides if needed) the environment variables from this module into the
        /// given dictionary.
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="environmentVars">The dictionary to which the vars will be added/updated</param>
        public static void AddEnvironmentVariables(this ModuleConfig module,
                                                   Dictionary<string, string?> environmentVars)
        {
            if (module.EnvironmentVariables is not null)
            {
                foreach (var entry in module.EnvironmentVariables)
                {
                    string key = entry.Key.ToUpper();
                    if (environmentVars.ContainsKey(key))
                        environmentVars[key] = entry.Value.ToString();
                    else
                        environmentVars.Add(key, entry.Value.ToString());
                }
            }
        }

        /// <summary>
        /// Saves the module configurations for all modules to a file.
        /// </summary>
        /// <param name="path">The path to save</param>
        /// <returns>A JSON object containing the settings from the settings file</returns>
        public async static Task<JsonObject?> LoadSettings(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new JsonObject();

            if (!File.Exists(path))
                return new JsonObject();

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir))
                    return new JsonObject();

                string content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                // var settings = JsonSerializer.Deserialize<Dictionary<string, dynamic>>(content);
                var settings = JsonSerializer.Deserialize<JsonObject>(content);

                return settings;
            }
            catch /*(Exception ex)*/
            {
                return new JsonObject();
            }
        }

        /// <summary>
        /// Saves the module configurations for all modules to a file.
        /// </summary>
        /// <param name="settings">This set of module settings</param>
        /// <param name="path">The path to save</param>
        /// <returns>true on success; false otherwise</returns>
        public async static Task<bool> SaveSettingsAsync(JsonObject? settings, string path)
        {
            if (settings is null || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir))
                    return false;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string configJson = JsonSerializer.Serialize(settings, options);

                await File.WriteAllTextAsync(path, configJson).ConfigureAwait(false);

                return true;
            }
            catch /*(Exception ex)*/
            {
                // _logger.LogError($"Exception saving module settings: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Extension methods for the ModuleCollection class
    /// </summary>
    public static class ModuleCollectionExtensions
    {
        /// <summary>
        /// Returns a module with the given module ID, or null if none found.
        /// </summary>
        /// <param name="modules">This collection of modules</param>
        /// <param name="moduleId">The module ID</param>
        /// <returns>A ModuleConfig object, or null if non found</returns>
        public static ModuleConfig? GetModule(this ModuleCollection modules, string moduleId)
        {
            if (!modules.TryGetValue(moduleId, out ModuleConfig? module))
                return null;

            return module;
        }

        /// <summary>
        /// Saves the module configurations for all modules to a file.
        /// </summary>
        /// <param name="modules">This set of module configs</param>
        /// <param name="path">The path to save</param>
        /// <returns>true on success; false otherwise</returns>
        public async static Task<bool> SaveAllSettings(this ModuleCollection modules, string path)
        {
            if (modules is null || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrWhiteSpace(dir))
                    return false;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string configJson = JsonSerializer.Serialize(modules, options);

                await File.WriteAllTextAsync(path, configJson).ConfigureAwait(false);

                return true;
            }
            catch /*(Exception ex)*/
            {
                // _logger.LogError($"Exception saving module settings: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a file containing the module information for all registered modules that is
        /// suitable for deploying to the module registry.
        /// </summary>
        /// <param name="modules">This set of module configs</param>
        /// <param name="path">The path to save</param>
        /// <returns>true on success; false otherwise</returns>
        public async static Task<bool> CreateModulesListing(this ModuleCollection modules, string path)
        {
            if (modules is null || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var moduleList = modules.Values
                                        .OrderBy(m => m.ModuleId)
                                        .Select(m => new {
                                            ModuleId       = m.ModuleId,
                                            Name           = m.Name,
                                            Version        = m.Version,
                                            Description    = m.Description,
                                            Platforms      = m.Platforms,
                                            Runtime        = m.Runtime,
                                            ModuleReleases = m.ModuleReleases,
                                            License        = m.License,
                                            LicenseUrl     = m.LicenseUrl,
                                            Downloads      = 0
                                        });

                var options = new JsonSerializerOptions { WriteIndented = true };
                string configJson = JsonSerializer.Serialize(moduleList, options);

                await File.WriteAllTextAsync(path, configJson).ConfigureAwait(false);

                return true;
            }
            catch /*(Exception ex)*/
            {
                // _logger.LogError($"Exception saving module settings: {ex.Message}");
                return false;
            }
        }        
    }
}
