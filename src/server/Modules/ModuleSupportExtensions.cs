using System.Collections.Generic;
using System.Linq;

using CodeProject.AI.SDK.Common;
using CodeProject.AI.Server.Utilities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

            // HACK: The binding of the Dictionary has issues in NET 7.0.0 but was 'fixed'
            // in NET 7.0.2. We can't, however, assume all platforms will have this fix available
            // (currently not available on macOS for instance) so we stick with our brute force hack:
            // services.Configure<ModuleCollection>(configuration.GetSection("Modules"));
            services.AddOptions<ModuleCollection>()
                    .Configure(moduleCollection =>
                    {
                        List<string>? moduleIds        = configuration.GetSection("Modules")
                                                                      .GetChildren()
                                                                      .Select(x => x.Key).ToList();                       
                        IConfigurationSection? section = configuration.GetSection("ModuleOptions");
                        string modulesPath             = section["ModulesPath"] ?? string.Empty;
                        string preInstalledModulesPath = section["PreInstalledModulesPath"] ?? string.Empty;

                        foreach (var moduleId in moduleIds)
                        {
                            if (moduleId is not null)
                            {
                                ModuleConfig moduleConfig = new ModuleConfig();

                                configuration.Bind($"Modules:{moduleId}", moduleConfig);
                                moduleCollection.TryAdd(moduleId, moduleConfig);

                                // Complete the ModuleConfig's setup
                                moduleConfig.Initialise(moduleId, modulesPath, preInstalledModulesPath);
                            }
                        }
                    });

            services.AddSingleton<ModuleSettings,    ModuleSettings>();
            services.AddSingleton<ModuleInstaller, ModuleInstaller>();
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
    }
}
