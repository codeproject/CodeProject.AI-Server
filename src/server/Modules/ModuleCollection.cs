using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Models;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// The Response when requesting information on config settings of modules
    /// </summary>
    public class ModuleListConfigResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of module configs
        /// </summary>
        public List<ModuleConfig>? Modules { get; set; }
    }

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
    /// The collection of values that control how the module is launched and run.
    /// </summary>
    public class LaunchSettings
    {
        /// <summary>
        /// Gets or sets the logging noise level. Quiet = only essentials, Info = anything meaningful,
        /// Loud = the kitchen sink. Default is Info. Note that this value is only effective if 
        /// implemented by the module itself
        /// </summary>
        public LogVerbosity? LogVerbosity { get; set; } // = LogVerbosity.Info;

        /// <summary>
        /// Gets or sets a value indicating whether this process should be activated on startup if
        /// no instruction to the contrary is seen. A default "Start me up" flag.
        /// </summary>
        public bool? AutoStart { get; set; }

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
        /// Gets or sets the runtime used to execute the file at FilePath. For example, the runtime
        /// could be "dotnet" or "python3.9". 
        /// </summary>
        public string? Runtime { get; set; }

        /// <summary>
        /// Gets or sets where the runtime executables for this module should be found. Valid
        /// values are:
        /// "Shared" - the runtime is installed in the /modules folder 
        /// "Local" - the runtime is installed locally in this modules folder
        /// "System" - the runtime is installed in the system globally
        /// </summary>
        public RuntimeLocation RuntimeLocation  { get; set; } = RuntimeLocation.Local;

        /// <summary>
        /// Gets or sets the command to execute the file at FilePath. If set, this overrides Runtime.
        /// An example would be "/usr/bin/python3". This property allows you to specify an explicit
        /// command in case the necessary runtime hasn't been registered, or in case you need to
        /// provide specific flags or naming alternative when executing the FilePath on different
        /// platforms. 
        /// </summary>
        public string? Command { get; set; }

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
        /// Gets or sets a value indicating the degree of parallelism (number of threads or number
        /// of tasks, depending on the implementation) to launch when running this module.
        /// 0 = default, which is (Number of CPUs / 2).
        /// </summary>
        public int? Parallelism { get; set; }

        /// <summary>
        /// Gets or sets the number of MB of memory needed for this module to perform operations.
        /// If null, then no checks done.
        /// </summary>
        public int? RequiredMb { get; set; }
    }

    /// <summary>
    /// The collection of values that control how the module installs and uses GPU support
    /// </summary>
    public class GpuOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the installer should install GPU support such as
        /// GPU enabled libraries in order to provide GPU functionality when running. This doesn't
        /// direct that a GPU must be used, but instead provides the means for an app to use GPUs
        /// if it desires. Note that if InstallGPU = false, EnableGPU is set to false. Setting this
        /// allows you to force a module to install in CPU mode to work around show-stoppers that
        /// may occur when trying to install GPU enabled libraries.
        /// </summary>
        public bool? InstallGPU { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this process should enable GPU functionality
        /// when running. This doesn't direct that a GPU must be used, but instead alerts that app
        /// that it should enable GPUs if possible. Setting this to false means "even if you can 
        /// use a GPU, don't". Great for working around GPU issues that would sink the ship.
        /// </summary>
        public bool? EnableGPU { get; set; } = true;

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
    }

    /// <summary>
    /// The collection of UI elements for use in dashboards and explorers
    /// </summary>
    public class UIElements
    {
        private ExplorerUI? _explorerUI;
        private ModuleConfig? _parent;

        /// <summary>
        /// Gets or sets the UI components to be included in the Explorer web app that provides the
        /// means to explore and test this module.
        /// </summary>
        public ExplorerUI? ExplorerUI
        { 
            get
            {
                if (_explorerUI is null && _parent is not null)
                    _explorerUI = _parent.GetExplorerUI();
                return _explorerUI;
            }

            set { _explorerUI = value; }
        }

        /// <summary>
        /// Gets or sets the menus to be displayed in the dashboard based on the current status of
        /// this module
        /// </summary>
        public DashboardMenu[]? Menus { get; set; }

        /// <summary>
        /// Sets the parent of this object
        /// </summary>
        /// <param name="parent">The parent</param>
        public void SetParent(ModuleConfig parent) => _parent = parent;
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
        [Obsolete("Activate is deprecated, please use LaunchSettings.AutoStart instead.", false)]
        [JsonIgnore]
        public bool? Activate { get; set; }

        /// <summary>
        /// Gets or sets the collection of values that control how the module is launched and run.
        /// </summary>
        [JsonPropertyOrder(4)]
        public LaunchSettings? LaunchSettings { get; set; }

        /// <summary>
        /// Gets or sets the collection of values that control how the module installs and uses GPU
        /// support
        /// </summary>
        [JsonPropertyOrder(5)]
        public GpuOptions? GpuOptions { get; set; }

        /// <summary>
        /// Gets or sets the model requirements for this module
        /// </summary>
        [JsonPropertyOrder(7)]
        public ModelPackageAttributes[]? ModelRequirements { get; set; }
      
        /// <summary>
        /// Gets or sets the information to pass to the backend analysis modules.
        /// </summary>
        [JsonPropertyOrder(8)]
        public Dictionary<string, object>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the UI elements to be injected into UI apps such as dashboards or explorers
        /// </summary>
        [JsonPropertyOrder(9)]
        public UIElements? UIElements { get; set; }

        /// <summary>
        /// Gets or sets a list of RouteMaps.
        /// </summary>
        [JsonPropertyOrder(10)]
        public ModuleRouteInfo[] RouteMaps { get; set; } = Array.Empty<ModuleRouteInfo>();

        /// <summary>
        /// Gets or sets a value indicating whether the SettingsSummary property should return a
        /// value. This is a cheap way of turning off SettingsSummary serialisation at runtime.
        /// </summary>
        [JsonIgnore]
        public bool NoSettingsSummary { get; set; }

        /// <summary>
        /// Gets a text summary of the settings for this module.
        /// </summary>
        [JsonPropertyOrder(1000)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]        
        public string? SettingsSummary
        {
            get
            {
                if (NoSettingsSummary)
                    return null;

                // Allow the module path to wrap.
                // var path = moduleDirPath.Replace("\\", "\\<wbr>");
                // path = path.Replace("/", "/<wbr>");

                // or not...
                var path = ModuleDirPath;

                var summary = new StringBuilder();
                summary.AppendLine($"Module '{Name}' {Version} (ID: {ModuleId})");
                summary.AppendLine($"Valid:            {Valid}");
                summary.AppendLine($"Module Path:      {path}");
                summary.AppendLine($"Module Location:  {InstallOptions?.ModuleLocation}");
                summary.AppendLine($"AutoStart:        {LaunchSettings?.AutoStart}");
                summary.AppendLine($"Queue:            {LaunchSettings?.Queue}");
                summary.AppendLine($"Runtime:          {LaunchSettings?.Runtime}");
                summary.AppendLine($"Runtime Location: {LaunchSettings?.RuntimeLocation}");
                summary.AppendLine($"FilePath:         {LaunchSettings?.FilePath}");
                summary.AppendLine($"Start pause:      {LaunchSettings?.PostStartPauseSecs} sec");
                summary.AppendLine($"Parallelism:      {LaunchSettings?.Parallelism}");
                summary.AppendLine($"LogVerbosity:     {LaunchSettings?.LogVerbosity}");
                summary.AppendLine($"Platforms:        {string.Join(',', InstallOptions?.Platforms?? Array.Empty<string>())}");
                summary.AppendLine($"GPU Libraries:    {((GpuOptions?.InstallGPU == true)? "installed if available" : "not installed")}");
                summary.AppendLine($"GPU:              {((GpuOptions?.EnableGPU == true)?  "use if supported" : "do not use")}");
                summary.AppendLine($"Accelerator:      {GpuOptions?.AcceleratorDeviceName}");
                summary.AppendLine($"Half Precision:   {GpuOptions?.HalfPrecision}");
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

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        [JsonIgnore]
        public override bool Valid
        {
            get
            {
                return base.Valid && 
                       (InstallOptions?.ModuleReleases?.Length ?? 0) > 0      &&
                       LaunchSettings is not null                             &&
                       (!string.IsNullOrWhiteSpace(LaunchSettings.Command) || 
                        !string.IsNullOrWhiteSpace(LaunchSettings.Runtime))   &&
                       !string.IsNullOrWhiteSpace(LaunchSettings.FilePath)    &&
                       (RouteMaps?.Length ?? 0) > 0;
            }
        }
    }

    /// <summary>
    /// Extension methods for the ModuleConfig class
    /// </summary>
    public static class ModuleConfigExtensions
    {
        private static Dictionary<string, string?> _modulePathModuleIdMap { get; set; } = new Dictionary<string, string?>();

        /// <summary>
        /// ModuleConfig objects are typically created by deserialising a JSON file so we don't get
        /// a chance at create time to supply supplementary information or adjust values that may
        /// not have been set (eg moduleId). This method provides that opportunity.
        /// </summary>
        /// <param name="module">This module that requires initialisation</param>
        /// <param name="moduleId">The id of the module. This isn't included in the object in JSON
        /// file, instead, the moduleId is the key for the module's object in the JSON file</param>
        /// <param name="moduleDirPath">The path to the folder containing this module</param>
        /// <param name="moduleLocation">The location of this module</param>
        /// <returns>True on success; false otherwise</returns>
        public static bool Initialise(this ModuleConfig module, string moduleId,
                                      string moduleDirPath, ModuleLocation moduleLocation)
        {
            if (module is null)
                return false;

            module.ModuleId = moduleId;

            // Malformed settings.
            if (!module.Valid)
                return false;

            module.CheckVersionAgainstModuleReleases();

            module.ModuleDirPath    = moduleDirPath;
            module.WorkingDirectory = module.ModuleDirPath; // This once was allowed to be different to moduleDirPath

            module.InstallOptions!.ModuleLocation = moduleLocation;

            if (string.IsNullOrEmpty(module.LaunchSettings?.Queue))
                module.LaunchSettings!.Queue = moduleId.ToLower() + "_queue";

            if (module.LaunchSettings.LogVerbosity == LogVerbosity.Unknown)
                module.LaunchSettings!.LogVerbosity = LogVerbosity.Info;

            // Allow the UIElements to access this module so it can lazy load the Explorer UI
            module.UIElements ??= new UIElements();
            module.UIElements.SetParent(module);
            
            if (!(module.GpuOptions!.InstallGPU ?? false))
                module.GpuOptions.EnableGPU = false;

            return true;
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
                module.LaunchSettings!.AutoStart = value?.ToLower() == "true";
            }
            else if (name.EqualsIgnoreCase("Parallelism"))
            {
                if (int.TryParse(value, out int parallelism))
                    module.LaunchSettings!.Parallelism = parallelism;
            }
            else if (name.EqualsIgnoreCase("LogVerbosity"))
            {
                if (Enum.TryParse(value, out LogVerbosity verbosity))
                    module.LaunchSettings!.LogVerbosity = verbosity;
            }
            else if (name.EqualsIgnoreCase("PostStartPauseSecs"))
            {
                if (int.TryParse(value, out int pauseSec))
                    module.LaunchSettings!.PostStartPauseSecs = pauseSec;
            }

            else if (name.EqualsIgnoreCase("EnableGPU") ||
                     name.EqualsIgnoreCase("SupportGPU")) // Legacy from 9Oct2023
            {
                module.GpuOptions!.EnableGPU = value?.ToLower() == "true";
            }
            else if (name.EqualsIgnoreCase("UseHalfPrecision"))
            {
                module.GpuOptions!.HalfPrecision = value;
            }
            else if (name.EqualsIgnoreCase("AcceleratorDeviceName"))
            {
                module.GpuOptions!.AcceleratorDeviceName = value;
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
        public static string SettingsSummary(this ModuleConfig module, ModuleSettings moduleSettings)
        {
            var summary = module.SettingsSummary;

            // Expanding out the macros causes the display to be too wide. Replace root of dir, and
            // provide some privacy while we're at it
            string appRoot = CodeProject.AI.Server.Program.ApplicationRootPath!;
            summary = moduleSettings.ExpandOption(summary, module.ModuleDirPath);
            summary = summary?.Replace(appRoot, "&lt;root&gt;");

            return summary?.Trim() ?? string.Empty;
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

                if (name.EqualsIgnoreCase("Activate") || name.EqualsIgnoreCase("AutoStart"))
                {
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null)
                        launchSettings["AutoStart"] = value?.ToLower() == "true";
                }
                else if (name.EqualsIgnoreCase("Parallelism"))
                {
                    if (int.TryParse(value, out int parallelism))
                    {
                        JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                        if (launchSettings is not null)
                            launchSettings["Parallelism"] = parallelism;
                    }
                }
                else if (name.EqualsIgnoreCase("PostStartPauseSecs"))
                {
                    if (int.TryParse(value, out int pauseSec))
                    {
                        JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                        if (launchSettings is not null)
                            launchSettings["PostStartPauseSecs"] = pauseSec;
                    }
                }
                else if (name.EqualsIgnoreCase("LogVerbosity"))
                {
                    if (Enum.TryParse(value, out LogVerbosity verbosity))
                        moduleSettings["LogVerbosity"] = verbosity.ToString();
                }

                else if (name.EqualsIgnoreCase("EnableGPU") ||
                         name.EqualsIgnoreCase("SupportGPU")) // Legacy from 9Oct2023
                {
                    JsonObject? gpuOptions = getModuleSettingSection(moduleSettings, "GpuOptions");
                    if (gpuOptions is not null)
                        gpuOptions["EnableGPU"] = value?.ToLower() == "true";
                }
                else if (name.EqualsIgnoreCase("UseHalfPrecision"))
                {
                    JsonObject? gpuOptions = getModuleSettingSection(moduleSettings, "GpuOptions");
                    if (gpuOptions is not null)
                        gpuOptions["HalfPrecision"] = value;
                }
                else if (name.EqualsIgnoreCase("AcceleratorDeviceName"))
                {
                    JsonObject? gpuOptions = getModuleSettingSection(moduleSettings, "GpuOptions");
                    if (gpuOptions is not null)
                        gpuOptions["AcceleratorDeviceName"] = value;
                }

                else
                {
                    if (moduleSettings["EnvironmentVariables"] is null)
                        moduleSettings["EnvironmentVariables"] = new JsonObject();

                    var environmentVars = (JsonObject)moduleSettings["EnvironmentVariables"]!;
                    environmentVars[name.ToUpper()] = value;
                }

                // Clean up legacy values

                // Activate is now LaunchSettings.AutoStart
                if (moduleSettings["Activate"] is not null)
                {
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["AutoStart"] is null)
                         launchSettings["AutoStart"] = moduleSettings["Activate"];
                    moduleSettings.Remove("Activate");
                }

                // LogVerbosity is now LaunchSettings.Parallelism
                if (moduleSettings["LogVerbosity"] is not null)
                {
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["LogVerbosity"] is null)
                         launchSettings["LogVerbosity"] = moduleSettings["LogVerbosity"];
                    moduleSettings.Remove("LogVerbosity");
                }

                // GpuOptions.Parallelism is now LaunchSettings.Parallelism
                JsonObject? oldGpuOptions = getModuleSettingSection(moduleSettings, "GpuOptions");
                if (oldGpuOptions is not null && oldGpuOptions["Parallelism"] is not null)
                {
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["Parallelism"] is null)
                         launchSettings["Parallelism"] = oldGpuOptions["Parallelism"];
                    oldGpuOptions.Remove("Parallelism");
                }
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
        /// Gets a module's ID from their modulesettings.json file
        /// </summary>
        /// <param name="directoryPath">The full path to the module's folder</param>
        /// <returns>The module Id, or null if not successful</returns>
        public static string? GetModuleIdFromModuleSettings(string directoryPath)
        {
            // Check the cache before doing the expensive operation
            if (!_modulePathModuleIdMap.ContainsKey(directoryPath))
            {
                JsonObject? settings = JsonUtils.LoadJson($"{directoryPath}/modulesettings.json");
                string? moduleId = JsonUtils.ExtractValue(settings, "$.Modules.#keys[0]")?.ToString();
                _modulePathModuleIdMap.Add(directoryPath, moduleId);
            }

            return _modulePathModuleIdMap[directoryPath];
        }

        /// <summary>
        /// This attempts to load a collection of modulesettings.*.json files from a directory using
        /// the current ModuleConfig format. If this fails it attempts to load using the old, legacy
        /// format. If that succeeds the old json files are backed up and a single, new json file is
        /// written using the new format to replace the old format files. Handy after upgrades to
        /// the settings format
        /// </summary>
        /// <param name="directoryPath">The path to the directory containing the files to process</param>
        public static void RewriteOldModuleSettingsFile(string directoryPath)
        {
            var info = new DirectoryInfo(directoryPath);

            // Bad assumption: A module's ID is same as the name of folder in which it lives.
            // string moduleId = info.Name;

            string? moduleId = GetModuleIdFromModuleSettings(directoryPath);
            if (moduleId is null)
                return;

            // Load up the modulesettings.*.json files
            var config = new ConfigurationBuilder();
            config.AddModuleSettingsConfigFiles(directoryPath, false);
            IConfiguration configuration = config.Build();

            // Bind the values in the configuration to a ModuleConfig object
            var moduleConfig = new ModuleConfig();
            try
            {
                configuration.Bind($"Modules:{moduleId}", moduleConfig);
            }
            catch (Exception)
            {
                moduleConfig = null;
            }

            // If this didn't work then let's try binding to our legacy format.
            // if that works we rewrite the modulesettings files
            LegacyModuleConfig? legacyModuleConfig = null;
            if (moduleConfig is null || !moduleConfig.Valid)
            {
                try
                {
                    legacyModuleConfig = new LegacyModuleConfig();
                    configuration.Bind($"Modules:{moduleId}", legacyModuleConfig);

                    // Set the module ID and then test if it's valid. If it is, we're good
                    legacyModuleConfig.ModuleId = moduleId;
                }
                catch (Exception e)
                {
                    legacyModuleConfig = null;
                    Console.WriteLine($"Unable to load and bind settings in {directoryPath}. " + e.Message);
                }
            }

            if (legacyModuleConfig != null && legacyModuleConfig.Valid)
            {
                // At this point we have a modulesettings that isn't valid with our current schema,
                // but is valid with the old schema. We will convert the old schema to the new 
                // schema if the (presumably old) module will work with this (presumably newer) 
                // server. The one way to check that is to see if the module has a explore.html page.
                // This server requires this file. Without it the module cannot run in the explorer.
                bool moduleCompatibleWithNewServer = File.Exists(Path.Combine(directoryPath, "explore.html"));

                if (moduleCompatibleWithNewServer)
                {
                    Console.WriteLine($"** Rebuilding modulesettings file for {moduleId}.");

                    // 1. Backup all modulesettings files
                    string pattern = Constants.ModulesSettingFilenameNoExt + ".*";
                    foreach (string path in Directory.GetFiles(directoryPath, pattern))
                    {
                        if (!path.EndsWith(".bak"))
                            File.Copy(path, path + ".bak", true);
                    }

                    // 2. Convert old format to new format
                    moduleConfig = legacyModuleConfig.ToModuleConfig();

                    // 3. HACK: We don't want to save the summary here, so we disable Summary 
                    //    generation, then serialize
                    moduleConfig.NoSettingsSummary = true;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string configJson = JsonSerializer.Serialize(moduleConfig, options);
                    moduleConfig.NoSettingsSummary = false;

                    // 3a. Our settings need to be stored in dictionary form, so wrap (and indent)
                    configJson = configJson.Replace("\n", "\n    ");
                    configJson = "{\n  \"Modules\": {\n    \"" + moduleId + "\": " + configJson + "\n  }\n}";

                    // 4. Write the file
                    string settingsFilePath = Path.Combine(directoryPath, Constants.ModuleSettingsFilename);
                    File.WriteAllText(settingsFilePath, configJson);
                }
                else
                {
                    Console.WriteLine($"** Old modulesettings schema found for {moduleId}, but not compatible with this server version.");
                }
            }
        }

        /// <summary>
        /// This attempts to load the modulesettings.json file stored in the application data folder
        /// that stores the user overrides for module settings. This file contains all overridden
        /// settings for all modules in a single file. We've updated the schema, so let's check this
        /// file and update if needed.
        /// </summary>
        /// <param name="storagePath">The path to the persisted user override settings</param>
        public static async void RewriteOldUserModuleSettingsFile(string storagePath)
        {
            var settingStore = new PersistedOverrideSettings(storagePath);
            JsonObject? settings = await settingStore.LoadSettings().ConfigureAwait(false);
            if (settings is null)
                return;

            var allModules = settings["Modules"] as JsonObject;
            if (allModules is null)
                return;

            bool changesMade = false;
            foreach (var entry in allModules)
            {
                string moduleId = entry.Key;
                JsonObject? moduleSettings = entry.Value as JsonObject;
                if (moduleSettings is null)
                    continue;

                /* We only need to worry about the following changes
                - AutoStart             => LaunchSettings.AutoStart
                - PostStartPauseSecs    => LaunchSettings.PostStartPauseSecs
                - LogVerbosity          => LaunchSettings.LogVerbosity
                - Parallelism           => LaunchSettings.Parallelism
                - EnableGPU             => GpuOptions.EnableGPU
                - HalfPrecision         => GpuOptions.HalfPrecision
                - AcceleratorDeviceName => GpuOptions.AcceleratorDeviceName
                */

                // AutoStart => LaunchSettings.AutoStart
                if (moduleSettings["AutoStart"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["AutoStart"] is null)
                        launchSettings["AutoStart"] = moduleSettings["AutoStart"]!.GetValue<bool>();
                    moduleSettings.Remove("AutoStart");
                }

                // PostStartPauseSecs => LaunchSettings.PostStartPauseSecs
                if (moduleSettings["PostStartPauseSecs"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["PostStartPauseSecs"] is null)
                        launchSettings["PostStartPauseSecs"] = moduleSettings["PostStartPauseSecs"]!.GetValue<int>();
                    moduleSettings.Remove("PostStartPauseSecs");
                }
                
                // LogVerbosity => LaunchSettings.LogVerbosity
                if (moduleSettings["LogVerbosity"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["LogVerbosity"] is null)
                        launchSettings["LogVerbosity"] = moduleSettings["LogVerbosity"]!.GetValue<int>();
                    moduleSettings.Remove("LogVerbosity");
                }

                // Parallelism => LaunchSettings.Parallelism
                if (moduleSettings["Parallelism"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["Parallelism"] is null)
                        launchSettings["Parallelism"] = moduleSettings["Parallelism"]!.GetValue<int>();
                    moduleSettings.Remove("Parallelism");
                }

                // GpuOptions.Parallelism => LaunchSettings.Parallelism
                JsonObject? oldGpuOptions = getModuleSettingSection(moduleSettings, "GpuOptions");
                if (oldGpuOptions is not null && oldGpuOptions["Parallelism"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "LaunchSettings");
                    if (launchSettings is not null && launchSettings["Parallelism"] is null)
                         launchSettings["Parallelism"] = oldGpuOptions["Parallelism"];
                    oldGpuOptions.Remove("Parallelism");
                }

                // EnableGPU => GpuOptions.EnableGPU
                if (moduleSettings["EnableGPU"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "GpuOptions");
                    if (launchSettings is not null && launchSettings["EnableGPU"] is null)
                        launchSettings["EnableGPU"] = moduleSettings["EnableGPU"]!.GetValue<bool>();
                    moduleSettings.Remove("EnableGPU");
                }

                // HalfPrecision => GpuOptions.HalfPrecision
                if (moduleSettings["HalfPrecision"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "GpuOptions");
                    if (launchSettings is not null && launchSettings["HalfPrecision"] is null)
                        launchSettings["HalfPrecision"] = moduleSettings["HalfPrecision"]!.GetValue<string>();
                    moduleSettings.Remove("HalfPrecision");
                }

                // AcceleratorDeviceName => GpuOptions.AcceleratorDeviceName
                if (moduleSettings["AcceleratorDeviceName"] is not null)
                {
                    changesMade = true;
                    JsonObject? launchSettings = getModuleSettingSection(moduleSettings, "GpuOptions");
                    if (launchSettings is not null && launchSettings["AcceleratorDeviceName"] is null)
                        launchSettings["AcceleratorDeviceName"] = moduleSettings["AcceleratorDeviceName"];
                    moduleSettings.Remove("AcceleratorDeviceName");
                }
            }
            
            if (changesMade)
                await settingStore.SaveSettingsAsync(settings);
        }

        /// <summary>
        /// Gets the UI (HTML, CSS and JavaScript) to be inserted into the AI Explorer UI at runtime.
        /// </summary>
        /// <param name="module">This module</param>
        /// <returns>A UiInsertion object</returns>
        public static ExplorerUI? GetExplorerUI(this ModuleConfig module)
        {
            if (string.IsNullOrWhiteSpace(module.ModuleDirPath))
                return null;
                
            const string testHtmlFilename = "explore.html";

            ExplorerUI explorerUI = new ExplorerUI();
                
            string testHtmlFilepath = Path.Combine(module.ModuleDirPath, testHtmlFilename);
            if (File.Exists(testHtmlFilepath))
            {
                string contents = File.ReadAllText/*Async*/(testHtmlFilepath);

                explorerUI.Css    = ExtractComponent(contents, "/\\* START EXPLORER STYLE \\*/",
                                                               "/\\* END EXPLORER STYLE \\*/");
                explorerUI.Script = ExtractComponent(contents, "// START EXPLORER SCRIPT",
                                                               "// END EXPLORER SCRIPT");
                explorerUI.Html   = ExtractComponent(contents, "\\<!-- START EXPLORER MARKUP --\\>",
                                                               "\\<!-- END EXPLORER MARKUP --\\>");
            }

            return explorerUI;
        }

        private static JsonObject? getModuleSettingSection(JsonObject moduleSettings, string section)
        {
            if (moduleSettings.ContainsKey(section) && moduleSettings[section] is not null)
                return moduleSettings[section] as JsonObject;

            var jsonObject = new JsonObject();
            moduleSettings[section] = jsonObject;

            return jsonObject;
        }

        private static string ExtractComponent(string input, string startMarker, string endMarker)
        {
            string pattern = startMarker + "([\\s\\S]*)" + endMarker;
            Match match = Regex.Match(input, pattern, RegexOptions.Singleline);

            if (match.Success)
                return match.Groups[1].Value.Trim();

            return string.Empty;
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
        /// Creates a file containing the module information for all modules provided, that is
        /// suitable for deploying to the module registry.
        /// </summary>
        /// <param name="modules">This set of module configs</param>
        /// <param name="path">The path to save</param>
        /// <param name="versionInfo">The version info for the current server</param>
        /// <returns>true on success; false otherwise</returns>
        public async static Task<bool> CreateModulesListing(this ModuleCollection modules,
                                                            string path, VersionInfo versionInfo)
        {
            if (modules is null || string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var corrections = new dynamic [] {
                    new { OldModuleId = "ObjectDetectionNet",  NewModuleId = "ObjectDetectionYOLOv5Net"      },
                    new { OldModuleId = "ObjectDetectionYolo", NewModuleId = "ObjectDetectionYOLOv5-6.2"     },
                    new { OldModuleId = "Yolov5-3.1",          NewModuleId = "ObjectDetectionYOLOv5-3.1"     },
                    new { OldModuleId = "TrainingYoloV5",      NewModuleId = "TrainingObjectDetectionYOLOv5" }
                };

                var moduleList = modules.Values
                                        .Where(m => !corrections.Any(c => c.NewModuleId == m.ModuleId) &&   // Don't do modules with new names yet
                                               m.InstallOptions!.ModuleLocation == ModuleLocation.Internal)
                                        .OrderBy(m => m.ModuleId)
                                        .Select(m => new {
                                            ModuleId       = m.ModuleId,
                                            Name           = m.Name,
                                            Version        = m.Version,
                                            PublishingInfo = m.PublishingInfo,
                                            InstallOptions = new {
                                                Platforms      = m.InstallOptions!.Platforms,
                                                ModuleReleases = m.InstallOptions!.ModuleReleases.ToArray()
                                            },
                                            Downloads      = 0
                                        }).ToList();

                // Add renamed modules, but with their names, but only server revisions v2.4+
                foreach (var pair in corrections)
                {
                    ModuleConfig? module = modules.Values.Where(m => m.ModuleId == pair.NewModuleId).FirstOrDefault();
                    if (module is not null)
                    {
                        ModuleRelease[] post24Releases = module.InstallOptions!.ModuleReleases
                                                               .Where(r => string.IsNullOrWhiteSpace(r.ServerVersionRange?[0]) ||
                                                                           VersionInfo.Compare(r.ServerVersionRange[0], "2.4") >= 0)
                                                               .ToArray();
                        moduleList.Add(new {
                            ModuleId       = module.ModuleId,
                            Name           = module.Name,
                            Version        = module.Version,
                            PublishingInfo = module.PublishingInfo,
                            InstallOptions = new {
                                Platforms      = module.InstallOptions.Platforms,
                                ModuleReleases = post24Releases
                            },
                            Downloads      = 0
                        });
                    }
                }

                // Add renamed modules, but with their old names, and only up to server v2.4
                foreach (var pair in corrections)
                {
                    ModuleConfig? module = modules.Values.Where(m => m.ModuleId == pair.NewModuleId).FirstOrDefault();
                    if (module is not null)
                    {
                        ModuleRelease[] pre24Releases = module.InstallOptions!.ModuleReleases
                                                              .Where(r => string.IsNullOrWhiteSpace(r.ServerVersionRange?[0]) ||
                                                                          VersionInfo.Compare(r.ServerVersionRange[0], "2.4") < 0)
                                                              .ToArray();
                        moduleList.Add(new {
                            ModuleId       = (string?)pair.OldModuleId,
                            Name           = module.Name,
                            Version        = module.Version,
                            PublishingInfo = module.PublishingInfo,
                            InstallOptions = new {
                                Platforms      = module.InstallOptions.Platforms,
                                ModuleReleases = pre24Releases
                            },
                            Downloads      = 0
                        });
                    }
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string configJson = JsonSerializer.Serialize(moduleList, options);

                configJson += "\n/*\n\n" + CreateModulesListingHtml(modules, versionInfo) + "\n*/";

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
        /// Creates markdown representing the file modules available.
        /// </summary>
        /// <param name="modules">This set of module configs</param>
        /// <param name="versionInfo">The version info for the current server</param>
        /// <returns>A string</returns>
        private static string CreateModulesListingHtml(ModuleCollection modules,
                                                       VersionInfo versionInfo)
        {
            var moduleList = modules.Values.Where(m => m.InstallOptions!.ModuleLocation == ModuleLocation.Internal)
                                           .OrderBy(m => m.PublishingInfo!.Category)
                                           .ThenBy(m => m.Name);

            StringBuilder list;
            if (versionInfo is not null)
                list = new StringBuilder($"<p>Supporting CodeProject.AI Server {versionInfo.Version}.</p>");
            else
                list = new StringBuilder();
                
            string? currentCategory = string.Empty;
            foreach (var module in moduleList)
            {
                if (currentCategory != module.PublishingInfo!.Category)
                {
                    if (list.Length > 0)
                        list.AppendLine("</ul>");

                    list.AppendLine($"<h3>{module.PublishingInfo!.Category}</h3>");
                    list.Append("<ul>");
                    currentCategory = module.PublishingInfo!.Category;
                }

                list.AppendLine($"<li><b>{module.Name}</b>");

                list.AppendLine("<div class='small-text'>");
                list.AppendLine($"v{module.Version}");
                list.AppendLine($"<span class='tags mx-3'>{PlatformList(module.InstallOptions!.Platforms)}</span>");
                list.AppendLine($"{module.PublishingInfo.Stack}");
                list.AppendLine("</div>");

                list.AppendLine("<div>");
                list.AppendLine($"{module.PublishingInfo.Description}");
                list.AppendLine("</div>");

                string author  = string.IsNullOrWhiteSpace(module.PublishingInfo.Author)
                               ? "Anonymous Legend" : module.PublishingInfo.Author;
                string basedOn = string.IsNullOrWhiteSpace(module.PublishingInfo.BasedOn)
                               ? "this project" : module.PublishingInfo.BasedOn;

                list.AppendLine("<div class='text-muted'>");
                if (!string.IsNullOrWhiteSpace(module.PublishingInfo.Homepage))
                    list.Append($"<a href='{module.PublishingInfo.Homepage}'>Project</a> by {author}"); 
                else
                    list.Append($"By {author}");                 
                if (!string.IsNullOrWhiteSpace(module.PublishingInfo.BasedOnUrl))
                    list.Append($", based on <a href='{module.PublishingInfo.BasedOnUrl}'>{basedOn}</a>."); 
                else if (!string.IsNullOrWhiteSpace(module.PublishingInfo.BasedOn))
                    list.Append($", based on {module.PublishingInfo.BasedOn}."); 
                list.AppendLine("</div>");

                list.AppendLine("<br><br></li>");
            }

            if (list.Length > 0)
                list.AppendLine("</ul>");

            return list.ToString();
        }

        private static string PlatformList(string[] platforms)
        {
            var realNames = platforms.Select(p => {
                string suffix = string.Empty;
                if (p.StartsWith('!')) { suffix = "!"; p = p[1..]; }

                if (p.StartsWithIgnoreCase("macos"))       return suffix + "macOS";
                if (p.StartsWithIgnoreCase("raspberrypi")) return suffix + "Raspberry Pi";
                if (p.StartsWithIgnoreCase("orangepi"))    return suffix + "Orange Pi";
                if (p.StartsWithIgnoreCase("radxarock"))   return suffix + "Radxa ROCK";
                if (p.EqualsIgnoreCase("jetson"))          return suffix + "Jetson";
                return suffix + string.Concat(char.ToUpper(p[0]), p[1..]);
            });

            var removes = string.Join(" ", realNames.Where(p => p.StartsWith('!'))
                                                    .Select(p => $"<span class='t'>{p[1..]}</span>"));
            var keeps   = string.Join(" ", realNames.Where(p => !p.StartsWith('!'))
                                                    .Select(p => $"<span class='t'>{p}</span>"));

            string platformString = keeps;
            if (!string.IsNullOrEmpty(removes))
                platformString += " except " + removes;

            return platformString;
        }
    }
}
