using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Models;
using CodeProject.AI.Server.Utilities;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// Extension methods for the AI Module support.
    /// </summary>
    public static class ModuleSupportExtensions
    {
        /// <summary>
        /// Sets up the Module Support.
        /// </summary>
        /// <param name="services">The ServiceCollection.</param>
        /// <param name="configuration">The Configuration.</param>
        /// <returns></returns>
        public static IServiceCollection AddModuleSupport(this IServiceCollection services,
                                                          IConfiguration configuration)
        {
            // a test
            // ModuleConfig module = new();
            // configuration.Bind("Modules:OCR", module);

            // Setup the config objects
            services.Configure<ServerOptions>(configuration.GetSection("ServerOptions"));
            services.Configure<ModuleOptions>(configuration.GetSection("ModuleOptions"));
            services.Configure<InstallConfig>(configuration.GetSection(InstallConfig.InstallCfgSection));

            // HACK: The binding of the Dictionary has issues in NET 7.0.0 but was 'fixed'
            // in NET 7.0.2. We can't, however, assume all platforms will have this fix available
            // (currently not available on macOS for instance) so we stick with our brute force hack.

            // We should be able to just do:
            // services.Configure<ModuleCollection>(configuration.GetSection("Modules"));
            //
            // Instead we do...
            services.AddOptions<ModuleCollection>()
                    .Configure(installedModules =>
                    {
                        List<string>? moduleIds = configuration.GetSection("Modules")
                                                               .GetChildren()
                                                               .Select(x => x.Key).ToList();
                        foreach (var moduleId in moduleIds)
                        {
                            if (moduleId is not null)
                            {
                                ModuleConfig moduleConfig = new ModuleConfig();
                                configuration.Bind($"Modules:{moduleId}", moduleConfig);

                                if (!Program.ModuleIdModuleDirMap.ContainsKey(moduleId))
                                    continue;

                                (string moduleDirPath, ModuleLocation location) = Program.ModuleIdModuleDirMap[moduleId];
                                if (moduleConfig.Initialise(moduleId, moduleDirPath, location) && 
                                    moduleConfig.Valid)
                                {
                                    installedModules.TryAdd(moduleId, moduleConfig);
                                }
                                else
                                {
                                    Console.WriteLine($"Error: Unable to initialise module {moduleId}. Config was invalid.");
                                }
                            }
                        }
                    });

            services.AddSingleton<ModuleSettings,    ModuleSettings>();
            services.AddSingleton<ModuleInstaller,   ModuleInstaller>();
            services.AddSingleton<ModelDownloader,   ModelDownloader>();
            services.AddSingleton<PackageDownloader, PackageDownloader>();

            services.Configure<HostOptions>(hostOptions =>
            {
                hostOptions.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
            });

            services.AddSingleton<ModuleProcessServices>();

            // Add the runner
            services.AddHostedService<ModuleRunner>();

            return services;
        }

        /// <summary>
        /// Adds a module's modulesettings.*.json files to a configuration builder in the correct
        /// order, taking into account the environment and platform.
        /// </summary>
        /// <param name="config">The IConfigurationBuilder object</param>
        /// <param name="moduleDirPath">The directory containing the module</param>
        /// <param name="reloadOnChange">Whether to trigger a reload if the files change</param>
        public static IConfigurationBuilder AddModuleSettingsConfigFiles(this IConfigurationBuilder config,
                                                                         string moduleDirPath,
                                                                         bool reloadOnChange)
        {
            string runtimeEnv      = SystemInfo.RuntimeEnvironment == RuntimeEnvironment.Development
                                   ? "development" : string.Empty;
            string os              = SystemInfo.OperatingSystem.ToLower();
            string architecture    = SystemInfo.Architecture.ToLower();
            string deviceSpecifier = SystemInfo.EdgeDevice.ToLower();

            // Module settings files are loaded in this order. Each file will overwrite (but not
            // delete) settings of the previous file.
            // TODO: Remove .docker as a specifier.
            // modulesettings.json
            // modulesettings.development.json
            // modulesettings.os.json
            // modulesettings.os.development.json
            // modulesettings.os.architecture.json
            // modulesettings.os.architecture.development.json
            // modulesettings.docker.json
            // modulesettings.docker.development.json
            // modulesettings.device_specifier.json     device_specifier = raspberrypi, orangepi, radxarock, jetson
            // modulesettings.device_specifier.development.json

            string basename = Constants.ModulesSettingFilenameNoExt; // "modulesettings"
            string settingsFile = Path.Combine(moduleDirPath, $"{basename}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(moduleDirPath, $"{basename}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            settingsFile = Path.Combine(moduleDirPath, $"{basename}.{os}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(moduleDirPath, $"{basename}.{os}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            settingsFile = Path.Combine(moduleDirPath, $"{basename}.{os}.{architecture}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                settingsFile = Path.Combine(moduleDirPath, $"{basename}.{os}.{architecture}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            // Handle Docker specific settings
            if (SystemInfo.IsDocker)
            {
                settingsFile = Path.Combine(moduleDirPath, $"{basename}.docker.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
                if (!string.IsNullOrEmpty(runtimeEnv))
                {
                    settingsFile = Path.Combine(moduleDirPath, $"{basename}.docker.{runtimeEnv}.json");
                    config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
                }
            }

            // Handle device specific settings such as for Raspberry Pi, Orange Pi, Jetson
            if (!string.IsNullOrEmpty(deviceSpecifier))
            {
                settingsFile = Path.Combine(moduleDirPath, $"{basename}.{deviceSpecifier}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

                if (!string.IsNullOrEmpty(runtimeEnv))
                {
                    settingsFile = Path.Combine(moduleDirPath, $"{basename}.{deviceSpecifier}.{runtimeEnv}.json");
                    config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
                }
            }

            return config;
        }
    }
}
