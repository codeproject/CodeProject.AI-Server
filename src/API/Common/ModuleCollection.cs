using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using CodeProject.AI.SDK.Common;

namespace CodeProject.AI.API.Common
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
    /// Basic module information shared between module listings for download, and module 
    /// settings on installed modules.
    /// </summary>
    public class ModuleBase
    {
        /// <summary>
        /// Gets or sets the Id of the Module
        /// </summary>
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the Name to be displayed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this procoess should be activated on startup if
        /// no instruction to the contrary is seen. A default "Start me up" flag.
        /// </summary>
        public bool? Activate { get; set; }

        /// <summary>
        /// Gets or sets the platforms on which this module is supported.
        /// </summary>
        public string[] Platforms { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the Description for the module.
        /// </summary>
        public string? Description  { get; set; }

        /// <summary>
        /// Gets or sets the version of this module
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the date this module was released
        /// </summary>
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Gets or sets the date this module was last udpated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? LicenseUrl { get; set; }

        /// <summary>
        /// The string represtation of this module
        /// </summary>
        /// <returns>A string object</returns>
        public override string ToString()
        {
            return $"{Name} ({ModuleId}) {Version} {License ?? ""}";
        }        
    }

    /// <summary>
    /// Information required to start the backend processes.
    /// </summary>
    public class ModuleConfig : ModuleBase
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not this module comes pre-installed with
        /// this server setup.
        /// </summary>
        public bool PreInstalled { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether this process should support GPUs. This doesn't
        /// direct that a GPU must be ued, but instead alerts that app that it should support a GPU
        /// if possible. Setting this to false means "even if you can support a GPU, don't".
        /// </summary>
        public bool? SupportGPU { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the degree of parallelism (number of threads or number
        /// of tasks, depending on the implementation) to launch when running this module.
        /// 0 = default, which is (Number of CPUs - 1).
        /// </summary>
        public int? Parallelism { get; set; }

        /// <summary>
        /// Gets or sets the CUDA device id (device number) to use. This must be between 0 and the
        /// number of CUDA devices - 1. Default is 0.
        /// </summary>
        public int? CudaDeviceNumber { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds this module should pause after starting to ensure 
        /// any resources that require startup (eg GPUs) are fully activated before moving on.
        /// </summary>
        public int? PostStartPauseSecs { get; set; }

        /// <summary>
        /// Gets or sets the runtime used to execute the file at FilePath. For example, the runtime
        /// could be "dotnet" or "python39". 
        /// </summary>
        public string? Runtime { get; set; }

        /// <summary>
        /// Gets or sets the name of the queue this module should service when processing commands.
        /// </summary>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the command to execute the file at FilePath. If set, this overrides Runtime.
        /// An example would be "/usr/bin/python3". This property allows you to specify an explicit
        /// command in case the necessary runtime hasn't been registered, or in case you need to
        /// provide specific flags or naming alternative when executing the FilePath on different
        /// platforms. 
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        ///  Gets or sets the path, relative to the directory containing this module, of this module.
        /// </summary>
        public string? ModulePath { get; set; }

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
        /// the modules read the modulesetings.json files for their configuration.
        /// </remarks>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the path to the working directory file relative to the modules directory.
        /// If this is null then the working directory will be set as the directory from FilePath.
        /// </summary>
        public string? WorkingDirectory { get; set; }

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
                var summary = new StringBuilder();
                summary.AppendLine($"Module '{Name}' (ID: {ModuleId})");
                summary.AppendLine($"Active:      {Activate}");
                summary.AppendLine($"GPU:         Support {((SupportGPU == true)? "enabled" : "disabled")}");
                summary.AppendLine($"Parallelism: {Parallelism}");
                summary.AppendLine($"Platforms:   {string.Join(',', Platforms)}");
                summary.AppendLine($"FilePath:    {FilePath}");
                summary.AppendLine($"ModulePath:  {ModulePath}");
                summary.AppendLine($"Install:     {(PreInstalled ? "PreInstalled" : "PostInstalled")}");
                summary.AppendLine($"Runtime:     {Runtime}");
                summary.AppendLine($"Queue:       {Queue}");
                summary.AppendLine($"Start pause: {PostStartPauseSecs} sec");
                summary.AppendLine($"Valid:       {Valid}");
                summary.AppendLine($"Environment Variables");

                if (EnvironmentVariables is not null)
                {
                    int maxLength = EnvironmentVariables.Max(x => x.Key.ToString().Length);
                    foreach (var envVar in EnvironmentVariables)
                        summary.AppendLine($"  {envVar.Key.PadRight(maxLength)} = {envVar.Value}");
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
        /// Gets a value indicating whether or not this module is actually available. This depends 
        /// on having valid commands, settings, and importantly, being supported on this platform.
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="platform">The platform being tested</param>
        public static bool Available(this ModuleConfig module, string platform)
        {
            if (module is null)
                return false;

            return module.Valid && (module.Platforms!.Any(p => p.ToLower() == "all") ||
                                    module.Platforms!.Any(p => p.ToLower() == platform.ToLower()));
        }

        /* Not used
        /// <summary>
        /// Returns true if this module is running on the specified Queue
        /// </summary>
        /// <param name="module">This module</param>
        /// <param name="queueName">The name of the queue</param>
        /// <returns>True if running on the queue; false otherwise</returns>
        public static bool HasQueue(this ModuleConfig module, string queueName)
        {
            return module.Queue.EqualsIgnoreCase(queueName) == true;
        }
        */
        
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
            if (name.EqualsIgnoreCase("Activate"))
            {
                module.Activate = value?.ToLower() == "true";
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
        /// Sets or updates a value in the settings Json structure.
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

            if (!settings.ContainsKey("Modules") || settings["Modules"] is null)
                settings["Modules"] = new JsonObject();

            JsonObject? allModules = settings["Modules"] as JsonObject;
            allModules ??= new JsonObject();

            if (!allModules.ContainsKey(moduleId) || allModules[moduleId] is null)
                allModules[moduleId] = new JsonObject();

            var moduleSettings = (JsonObject)allModules[moduleId]!;

            // Handle pre-defined global values first
            if (name.EqualsIgnoreCase("Activate"))
            {
                moduleSettings["Activate"] = value?.ToLower() == "true";
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
        /// <returns>A Json object containing the settings from the settings file</returns>
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

                string content = await File.ReadAllTextAsync(path);
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
        public async static Task<bool> SaveSettings(JsonObject? settings, string path)
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

                await File.WriteAllTextAsync(path, configJson);

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

                await File.WriteAllTextAsync(path, configJson);

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
