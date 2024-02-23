
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;

using Microsoft.Extensions.Configuration;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.Server.Modules;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// A simple utillity class for managing legacy parameters and settings.
    /// </summary>
    public class LegacyParams
    {
        /// <summary>
        /// Sniff the configuration values for root level values that we should pass on for
        /// backwards compatibility with legacy modules, and add them to the set of environment
        /// variables that will be set when starting the processes.
        /// 
        /// Here's how it works:
        /// 
        /// Environment variables that an analysis module would typically access are set in 
        /// ModuleRunner.CreateProcessStartInfo. The values that CreateProcessStartInfo gets are 
        /// from the server's appsettings.json in the EnvironmentVariables section, or from the 
        /// backend analysis module's modulesettings.json file in its EnvironmentVariables section.
        /// These two sets of variables are combined into one set and then used to set the
        /// Environment variables for the backend analysis module being launched.
        /// 
        /// To override these values via the command line you just set the value of the environment
        /// variable using its name. The "name" is the tricky bit. In the appsettings.json file you
        /// may have "USE_CUDA" in the EnvironmentVariable section, but its fully qualified name
        /// for the command line would be ServerOptions:EnvironmentVariables:CPAI_PORT. In the
        /// object detection module's variables in modulesettings.json, changing the value USE_CUDA
        /// requires the command line parameter Modules:ObjectDetectionYOLOv5-6.2:EnvironmentVariables:USE_CUDA.
        /// 
        /// So, to override these values at the command line is ludicrously verbose. Instead we
        /// will choose a subset of these variables that we know are in use in the wild and provide
        /// simple names that are mapped to the complicated names.
        /// 
        /// This is all horribly hardcoded, but the point here is that over time this list will
        /// disappear.
        /// </summary>
        public static void PassThroughLegacyCommandLineParams(IConfiguration configuration)
        {
            var keyValues = new Dictionary<string, string?>();

            // Go through the configuration looking for root level keys that could have been passed
            // via command line or otherwise. For the ones we find, convert them to value or values
            // that should be stored in configuration in a way that modules can access.
            foreach (KeyValuePair<string, string?> pair in configuration.AsEnumerable())
            {
                // Port.
                if (pair.Key.Equals("PORT", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["ServerOptions:EnvironmentVariables:CPAI_PORT"] = pair.Value;

                // Activation
                if (pair.Key.Equals("VISION-FACE", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("VISION_FACE", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:FaceProcessing:LaunchSettings:AutoStart"] = pair.Value;

                if (pair.Key.Equals("VISION-SCENE", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("VISION_SCENE", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:SceneClassification:LaunchSettings:AutoStart"] = pair.Value;

                if (pair.Key.Equals("VISION-DETECTION", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("VISION_DETECTION", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:ObjectDetectionYOLOv5-3.1:LaunchSettings:AutoStart"] = pair.Value;
                    keyValues["Modules:ObjectDetectionYOLOv5-6.2:LaunchSettings:AutoStart"] = pair.Value;
                    keyValues["Modules:ObjectDetectionYOLOv5Net:LaunchSettings:AutoStart"]  = pair.Value;
                }

                // Mode, which convolutes resolution and model size
                if (pair.Key.Equals("MODE", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:MODE"]        = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:MODE"]   = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:MODE"] = pair.Value;

                    string modelSize = pair.Value ?? "Medium";
                    if (pair.Value?.Equals("High", StringComparison.InvariantCultureIgnoreCase) ?? false)
                        modelSize = "Large";
                    else if (pair.Value?.Equals("Low", StringComparison.InvariantCultureIgnoreCase) ?? false)
                        modelSize = "Small";

                    keyValues["Modules:ObjectDetectionYOLOv5-6.2:EnvironmentVariables:MODEL_SIZE"] = modelSize;
                    keyValues["Modules:ObjectDetectionYOLOv5Net:EnvironmentVariables:MODEL_SIZE"]  = modelSize;
                }

                // Using CUDA?
                if (pair.Key.Equals("CUDA_MODE", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:USE_CUDA"]        = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:USE_CUDA"]   = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:USE_CUDA"] = pair.Value;

                    keyValues["Modules:ObjectDetectionYOLOv5Net:EnvironmentVariables:USE_CUDA"]  = pair.Value;
                    keyValues["Modules:ObjectDetectionYOLOv5-6.2:EnvironmentVariables:USE_CUDA"] = pair.Value;

                    keyValues["Modules:ObjectDetectionYOLOv5Net:EnvironmentVariables:CPAI_MODULE_ENABLE_GPU"]    = pair.Value;
                    keyValues["Modules:ObjectDetectionYOLOv5-6.2:EnvironmentVariables:CPAI_MODULE_ENABLE_GPU"]   = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:CPAI_MODULE_ENABLE_GPU"] = pair.Value;
                }

                // Model Directories
                if (pair.Key.Equals("DATA_DIR", StringComparison.InvariantCultureIgnoreCase))
                {
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:DATA_DIR"]        = pair.Value;
                    keyValues["Modules:VisionObjectDetection:EnvironmentVariables:DATA_DIR"] = pair.Value;
                    keyValues["Modules:SceneClassification:EnvironmentVariables:DATA_DIR"]   = pair.Value;
                    keyValues["Modules:ObjectDetection:EnvironmentVariables:DATA_DIR"]       = pair.Value;
                }

                // Custom Model Directories. Deepstack compatibility and thge docs are ambiguous
                if (pair.Key.Equals("MODELSTORE-DETECTION", StringComparison.InvariantCultureIgnoreCase) ||
                    pair.Key.Equals("MODELSTORE_DETECTION", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:ObjectDetectionYOLOv5-6.2:EnvironmentVariables:CUSTOM_MODELS_DIR"] = pair.Value;

                // Temp Directories
                if (pair.Key.Equals("TEMP_PATH", StringComparison.InvariantCultureIgnoreCase))
                    keyValues["Modules:FaceProcessing:EnvironmentVariables:TEMP_PATH"] = pair.Value;
            }

            // Now update the Configuration
            foreach (var pair in keyValues)
                configuration[pair.Key] = pair.Value;
        }

        /// <summary>
        /// <para>This is as bad as the method above. Instead of handling command line parameters,
        /// this method takes a section JSON object that contains settings for one or more modules
        /// in a format like</para>
        /// <example>
        /// {
        ///     "ObjectDetectionYOLOv5-6.2": {
        ///         EnableGpu: "True"
        ///     },
        ///     "FaceProcessing": {
        ///         "AutoStart" : "False"
        ///     }
        /// }
        ///</example>
        /// <para>We update module settings in-memory so that the server can immediately restart the
        /// module with the updated settings without needing to reload settings, and we also update
        /// the overrideSettings JsonObject which contains the set of current override settings. 
        /// These settings are stored in a JSON file and reloaded when the server reloads, meaning
        /// the changes we've made will be persisted (and will override the settings in the
        /// modulesettings.json files for each module).</para>
        /// </summary>
        /// <param name="newSettings">The new settings to apply</param>
        /// <param name="installedModules">The set of installed modules</param>
        /// <param name="overrideSettings">The collection of override settings to be persisted</param>
        /// <returns>A list of module IOs that were updated (and hence may need restarting)</returns>
        public static List<string> UpdateSettings(Dictionary<string, string> newSettings,
                                                  ModuleCollection installedModules,
                                                  JsonObject? overrideSettings)
        {
            List<string> modulesUpdated = new();

            foreach (var setting in newSettings)
            {
                // Port.
                if (setting.Key.Equals("PORT", StringComparison.InvariantCultureIgnoreCase))
                {
                    foreach (var entry in installedModules)
                    {
                        MakeSettingUpdate(installedModules, overrideSettings, entry.Key,
                                          "CPAI_PORT", setting.Value, modulesUpdated);
                    }
                }

                // Activation
                if (setting.Key.Equals("VISION-DETECTION", StringComparison.InvariantCultureIgnoreCase) ||
                    setting.Key.Equals("VISION_DETECTION", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-6.2",
                                      "AutoStart", setting.Value, modulesUpdated);
                    // MakeSettingUpdate(modules, overrideSettings, "ObjectDetectionYOLOv5Net",
                    //                  "AutoStart", setting.Value, modulesUpdated);
                }
                if (setting.Key.Equals("VISION-FACE", StringComparison.InvariantCultureIgnoreCase) ||
                    setting.Key.Equals("VISION_FACE", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "FaceProcessing",
                                      "AutoStart", setting.Value, modulesUpdated);
                }
                if (setting.Key.Equals("VISION-SCENE", StringComparison.InvariantCultureIgnoreCase) ||
                    setting.Key.Equals("VISION_SCENE", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "SceneClassification",
                                      "AutoStart", setting.Value, modulesUpdated);
                }

                // Mode, which is effectively model size
                if (setting.Key.Equals("MODE", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "FaceProcessing",
                                      "MODE", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "SceneClassification",
                                      "MODE", setting.Value, modulesUpdated);

                    string modelSize = "Medium";
                    if (setting.Value.Equals("High", StringComparison.InvariantCultureIgnoreCase))
                        modelSize = "Large";
                    else if (setting.Value.Equals("Low", StringComparison.InvariantCultureIgnoreCase))
                        modelSize = "Small";

                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-6.2",
                                      "MODEL_SIZE", modelSize, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5Net",
                                      "MODEL_SIZE", modelSize, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-3.1",
                                      "MODEL_SIZE", modelSize, modulesUpdated);
                }

                // Using CUDA?
                if (setting.Key.Equals("CUDA_MODE", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "FaceProcessing",
                                      "USE_CUDA", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "SceneClassification",
                                      "USE_CUDA", setting.Value, modulesUpdated);

                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-6.2",
                                      "USE_CUDA", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5Net",
                                      "USE_CUDA", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-3.1",
                                      "USE_CUDA", setting.Value, modulesUpdated);

                    MakeSettingUpdate(installedModules, overrideSettings, "FaceProcessing",
                                      "CPAI_MODULE_ENABLE_GPU", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "SceneClassification",
                                      "CPAI_MODULE_ENABLE_GPU", setting.Value, modulesUpdated);

                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-6.2",
                                      "CPAI_MODULE_ENABLE_GPU", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5Net",
                                      "CPAI_MODULE_ENABLE_GPU", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-3.1",
                                      "CPAI_MODULE_ENABLE_GPU", setting.Value, modulesUpdated);
                }

                // Model Directories
                if (setting.Key.Equals("DATA_DIR", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "VisionObjectDetection",
                                      "DATA_DIR", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "FaceProcessing",
                                      "DATA_DIR", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "SceneClassification",
                                      "DATA_DIR", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-6.2",
                                      "DATA_DIR", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-3.1",
                                      "DATA_DIR", setting.Value, modulesUpdated);
                }

                // Custom Model Directories. Deepstack compatibility and the docs are ambiguous
                if (setting.Key.Equals("MODELSTORE-DETECTION", StringComparison.InvariantCultureIgnoreCase) ||
                    setting.Key.Equals("MODELSTORE_DETECTION", StringComparison.InvariantCultureIgnoreCase))
                {
                    MakeSettingUpdate(installedModules, overrideSettings, "VisionObjectDetection",
                                      "MODELSTORE_DETECTION", setting.Value, modulesUpdated);
                    MakeSettingUpdate(installedModules, overrideSettings, "ObjectDetectionYOLOv5-6.2",
                                      "CUSTOM_MODELS_DIR", setting.Value, modulesUpdated);
                }
            }

            return modulesUpdated;
        }

        private static void MakeSettingUpdate(ModuleCollection installedModules,
                                              JsonObject? overrideSettings, string moduleId,
                                              string settingName, string settingValue,
                                              List<string> modulesUpdated)
        {
            ModuleConfig? module = installedModules.GetModule(moduleId);
            if (module is null || string.IsNullOrWhiteSpace(module.ModuleId))
                return;

            // Change in-memory values so module can be restarted now
            module.UpsertSetting(settingName, settingValue);

            // Update persisted settings so if server is restarted setting are reapplied
            if (ModuleConfigExtensions.UpsertSettings(overrideSettings, module.ModuleId!, settingName,
                                                      settingValue))
            {
                // Remember that this module needs to be restarted
                if (!modulesUpdated.Contains(module.ModuleId, StringComparer.OrdinalIgnoreCase))
                    modulesUpdated.Add(module.ModuleId);
            }
        }
    }
}