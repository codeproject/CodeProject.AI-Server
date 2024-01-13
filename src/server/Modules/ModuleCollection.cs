#define PROCESS_MODULE_RENAMES
// This ensures we generate the correct modules.json for server version < 2.4

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Utils;

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
    /// Information required to start the backend processes.
    /// </summary>
    public class ModuleConfig : ModuleBase
    {
        private ExplorerUI? _explorerUI;

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
        /// Gets or sets the UI components to be included in the Explorer web app that provides the
        /// means to explore and test this module.
        /// </summary>
        public ExplorerUI? ExplorerUI
        { 
            get
            {
                if (_explorerUI is null)
                    _explorerUI = this.GetExplorerUI();
                return _explorerUI;
            }

            set { _explorerUI = value; }
        }

        /// <summary>
        /// Gets or sets a list of RouteMaps.
        /// </summary>
        public ModuleRouteInfo[] RouteMaps { get; set; } = Array.Empty<ModuleRouteInfo>();

        /// <summary>
        /// Gets a text summary of the settings for this module.
        /// </summary>
        public string SettingsSummary
        {
            get
            {
                // Allow the module path to wrap.
                // var path = moduleDirPath.Replace("\\", "\\<wbr>");
                // path = path.Replace("/", "/<wbr>");

                // or not...
                var path = ModuleDirPath;

                var summary = new StringBuilder();
                summary.AppendLine($"Module '{Name}' {Version} (ID: {ModuleId})");
                summary.AppendLine($"Module Path:   {path}");
                summary.AppendLine($"AutoStart:     {AutoStart}");
                summary.AppendLine($"Queue:         {Queue}");
                summary.AppendLine($"Platforms:     {string.Join(',', Platforms)}");
                summary.AppendLine($"GPU Libraries: {((InstallGPU == true)? "installed if available" : "not installed")}");
                summary.AppendLine($"GPU Enabled:   {((EnableGPU == true)? "enabled" : "disabled")}");
                summary.AppendLine($"Parallelism:   {Parallelism}");
                summary.AppendLine($"Accelerator:   {AcceleratorDeviceName}");
                summary.AppendLine($"Half Precis.:  {HalfPrecision}");
                summary.AppendLine($"Runtime:       {Runtime}");
                summary.AppendLine($"Runtime Loc:   {RuntimeLocation}");
                summary.AppendLine($"FilePath:      {FilePath}");
                summary.AppendLine($"Pre installed: {PreInstalled}");
                //summary.AppendLine($"Module Dir:  {moduleDirPath}");
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

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        public override bool Valid
        {
            get
            {
                return base.Valid && 
                       (!string.IsNullOrWhiteSpace(Command) || !string.IsNullOrWhiteSpace(Runtime)) &&
                       !string.IsNullOrWhiteSpace(FilePath) &&
                       RouteMaps?.Length > 0;
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
        /// <param name="modulesDirPath">The path to the folder containing all downloaded and installed
        /// modules</param>
        /// <param name="preInstalledModulesDirPath">The path to the folder containing all pre-installed
        /// modules</param>
        /// <remarks>Modules are usually downloaded and installed in the modulesDirPath, but we can
        /// 'pre-install' them in situations like a Docker image. We pre-install modules in a
        /// separate folder than the downloaded and installed modules in order to avoid conflicts 
        /// (in Docker) when a user maps a local folder to the modules dir. Doing this to the 'pre
        /// installed' dir would make the contents (the preinstalled modules) disappear.</remarks>
        public static void Initialise(this ModuleConfig module, string moduleId, string modulesDirPath,
                                      string preInstalledModulesDirPath)
        {
            if (module is null)
                return;

            module.ModuleId = moduleId;

            if (module.PreInstalled)
                module.ModuleDirPath = Path.Combine(preInstalledModulesDirPath, module.ModuleId!);
            else
                module.ModuleDirPath = Path.Combine(modulesDirPath, module.ModuleId!);

            module.WorkingDirectory = module.ModuleDirPath; // This once was allowed to be different to moduleDirPath

            if (string.IsNullOrEmpty(module.Queue))
                module.Queue = moduleId.ToLower() + "_queue";

            if (module.LogVerbosity == LogVerbosity.Unknown)
                module.LogVerbosity = LogVerbosity.Info;

            if (!(module.InstallGPU ?? false))
                module.EnableGPU = false;
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
            else if (name.EqualsIgnoreCase("EnableGPU") ||
                     name.EqualsIgnoreCase("SupportGPU")) // Legacy from 9Oct2023
            {
                module.EnableGPU = value?.ToLower() == "true";
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
                                             string? currentModuleDirPath = null)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Module '{module.Name}' (ID: {module.ModuleId})");
            summary.AppendLine($"AutoStart:     {module.AutoStart}");
            summary.AppendLine($"Queue:         {module.Queue}");
            summary.AppendLine($"Runtime:       {module.Runtime}");
            summary.AppendLine($"Runtime Loc:   {module.RuntimeLocation}");
            summary.AppendLine($"Platforms:     {string.Join(',', module.Platforms)}");
            summary.AppendLine($"GPU Libraries: {((module.InstallGPU == true)? "installed if available" : "not installed")}");
            summary.AppendLine($"GPU Enabled:   {((module.EnableGPU == true)? "enabled" : "disabled")}");
            summary.AppendLine($"Parallelism:   {module.Parallelism}");
            summary.AppendLine($"Accelerator:   {module.AcceleratorDeviceName}");
            summary.AppendLine($"Half Precis.:  {module.HalfPrecision}");
            summary.AppendLine($"FilePath:      {module.FilePath}");
            summary.AppendLine($"Pre installed: {module.PreInstalled}");
            //summary.AppendLine($"Module Dir:  {module.ModuleDirPath}");
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
                                                            currentModuleDirPath);
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
                else if (name.EqualsIgnoreCase("EnableGPU") ||
                         name.EqualsIgnoreCase("SupportGPU")) // Legacy from 9Oct2023
                {
                    moduleSettings["EnableGPU"] = value?.ToLower() == "true";
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
#if PROCESS_MODULE_RENAMES
                    new { OldModuleId = "ObjectDetectionNet",  NewModuleId = "ObjectDetectionYOLOv5Net"      },
                    new { OldModuleId = "ObjectDetectionYolo", NewModuleId = "ObjectDetectionYOLOv5-6.2"     },
                    new { OldModuleId = "Yolov5-3.1",          NewModuleId = "ObjectDetectionYOLOv5-3.1"     },
                    new { OldModuleId = "TrainingYoloV5",      NewModuleId = "TrainingObjectDetectionYOLOv5" }
#endif
                };

                var moduleList = modules.Values
#if PROCESS_MODULE_RENAMES
                                        .Where(m => !corrections.Any(c => c.NewModuleId == m.ModuleId)) // Don't do modules with new names yet
#endif
                                        .OrderBy(m => m.ModuleId)
                                        .Select(m => new {
                                            ModuleId       = m.ModuleId,
                                            Name           = m.Name,
                                            Version        = m.Version,
                                            Description    = m.Description,
                                            Category       = m.Category,
                                            Platforms      = m.Platforms,
                                            Runtime        = m.Runtime,
                                            ModuleReleases = m.ModuleReleases,
                                            License        = m.License,
                                            LicenseUrl     = m.LicenseUrl,
                                            Author         = m.Author,
                                            Homepage       = m.Homepage,
                                            BasedOn        = m.BasedOn,
                                            BasedOnUrl     = m.BasedOnUrl,
                                            Downloads      = 0
                                        }).ToList();

#if PROCESS_MODULE_RENAMES
                // Add renamed modules, but with their names, but only server revisions v2.4+
                foreach (var pair in corrections)
                {
                    ModuleConfig? module = modules.Values.Where(m => m.ModuleId == pair.NewModuleId).FirstOrDefault();
                    if (module is not null)
                    {
                        moduleList.Add(new {
                            ModuleId       = module.ModuleId,
                            Name           = module.Name,
                            Version        = module.Version,
                            Description    = module.Description,
                            Category       = module.Category,
                            Platforms      = module.Platforms,
                            Runtime        = module.Runtime,
                            ModuleReleases = module.ModuleReleases.Where(r => string.IsNullOrWhiteSpace(r.ServerVersionRange?[0]) ||
                                                                              VersionInfo.Compare(r.ServerVersionRange[0], "2.4") >= 0).ToArray(),
                            License        = module.License,
                            LicenseUrl     = module.LicenseUrl,
                            Author         = module.Author,
                            Homepage       = module.Homepage,
                            BasedOn        = module.BasedOn,
                            BasedOnUrl     = module.BasedOnUrl,
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
                        moduleList.Add(new {
                            ModuleId       = (string?)pair.OldModuleId,
                            Name           = module.Name,
                            Version        = module.Version,
                            Description    = module.Description,
                            Category       = module.Category,
                            Platforms      = module.Platforms,
                            Runtime        = module.Runtime,
                            ModuleReleases = module.ModuleReleases.Where(r => string.IsNullOrWhiteSpace(r.ServerVersionRange?[0]) ||
                                                                              VersionInfo.Compare(r.ServerVersionRange[0], "2.4") < 0).ToArray(),
                            License        = module.License,
                            LicenseUrl     = module.LicenseUrl,
                            Author         = module.Author,
                            Homepage       = module.Homepage,
                            BasedOn        = module.BasedOn,
                            BasedOnUrl     = module.BasedOnUrl,
                            Downloads      = 0
                        });
                    }
                }
#endif
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
            var moduleList = modules.Values.OrderBy(m => m.Category)
                                           .ThenBy(m => m.Name);

            StringBuilder list;
            if (versionInfo is not null)
                list = new StringBuilder($"<p>Supporting CodeProject.AI Server {versionInfo.Version}.</p>");
            else
                list = new StringBuilder();
                
            string? currentCategory = string.Empty;
            foreach (var module in moduleList)
            {
                if (currentCategory != module.Category)
                {
                    if (list.Length > 0)
                        list.AppendLine("</ul>");

                    list.AppendLine($"<h3>{module.Category}</h3>");
                    list.Append("<ul>");
                    currentCategory = module.Category;
                }

                list.AppendLine($"<li><b>{module.Name}</b>");

                list.AppendLine("<div class='small-text'>");
                list.AppendLine($"v{module.Version}");
                list.AppendLine($"<span class='tags mx-3'>{PlatformList(module.Platforms)}</span>");
                list.AppendLine($"{RuntimeString(module.Runtime)}");
                list.AppendLine("</div>");

                list.AppendLine("<div>");
                list.AppendLine($"{module.Description}");
                list.AppendLine("</div>");

                string author  = string.IsNullOrWhiteSpace(module.Author)? "Anonymous Legend" : module.Author;
                string basedOn = string.IsNullOrWhiteSpace(module.BasedOn)? "this project" : module.BasedOn;

                list.AppendLine("<div class='text-muted'>");
                if (!string.IsNullOrWhiteSpace(module.Homepage))
                    list.Append($"<a href='${module.Homepage}'>Project</a> by {author}"); 
                else
                    list.Append($"By {author}");                 
                if (!string.IsNullOrWhiteSpace(module.BasedOnUrl))
                    list.Append($", based on <a href='${module.BasedOnUrl}'>{basedOn}</a>."); 
                else if (!string.IsNullOrWhiteSpace(module.BasedOn))
                    list.Append($", based on {module.BasedOn}."); 
                list.AppendLine("</div>");

                list.AppendLine("<br><br></li>");
            }

            if (list.Length > 0)
                list.AppendLine("</ul>");

            return list.ToString();
        }

        private static string RuntimeString(string? runtime)
        {
            if (runtime is null)
                return string.Empty;

            if (runtime == "dotnet") 
                return ".NET";

            if (runtime.StartsWith("python"))
                return string.Concat("P", runtime.AsSpan(1));

            return runtime;
        }

        private static string PlatformList(string[] platforms)
        {
            var realNames = platforms.Select(p => {
                string suffix = string.Empty;
                if (p.StartsWith('!')) { suffix = "!"; p = p[1..]; }

                if (p.StartsWithIgnoreCase("macos"))       return suffix + "macOS";
                if (p.StartsWithIgnoreCase("raspberrypi")) return suffix + "Raspberry Pi";
                if (p.StartsWithIgnoreCase("orangepi"))    return suffix + "Orange Pi";
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
