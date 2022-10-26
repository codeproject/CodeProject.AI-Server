using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// The Application Entry Class.
    /// </summary>
    public class Program
    {
        static private ILogger? _logger = null;

        static int _port = 32168;
        // static int _sPort = 5001; - eventually for SSL

        /// <summary>
        /// The Application Entry Point.
        /// </summary>
        /// <param name="args">The command line args.</param>
        public static async Task Main(string[] args)
        {
            // TODO: Pull these from the correct location
            const string company = "CodeProject";
            const string product = "AI";

            // lower cased as Linux has case sensitive file names
            string  platform  = BackendProcessRunner.Platform.ToLower();
            string? aspNetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLower();
            bool    inDocker  = (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "") == "true";

            var assembly      = Assembly.GetExecutingAssembly();
            var assemblyName  = (assembly.GetName().Name ?? string.Empty)
                              + (platform == "windows" ? ".exe" : ".dll");
            var serviceName   = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                              ?? assemblyName.Replace(".", " ");
            var servicePath   = Path.Combine(AppContext.BaseDirectory, assemblyName);

            var serviceDescription = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty;

            if (args.Length == 1)
            {
                if (args[0].Equals("/Install", StringComparison.OrdinalIgnoreCase))
                {
                    WindowsServiceInstaller.Install(servicePath, serviceName, serviceDescription);
                    return;
                }
                else if (args[0].Equals("/Uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    WindowsServiceInstaller.Uninstall(serviceName);
                    return;
                }
                else if (args[0].Equals("/Stop", StringComparison.OrdinalIgnoreCase))
                {
                    WindowsServiceInstaller.Stop(serviceName);
                    return;
                }
            }

            // Get a directory for the given platform that allows momdules to store persisted data
            string programDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationDataDir = $"{programDataDir}\\{company}\\{product}".Replace('\\', Path.DirectorySeparatorChar);

            // .NET's suggestion for macOS isn't great. Let's do something different.
            if (platform == "macos" || platform == "macos-arm")
                applicationDataDir = $"/Library/Application Support/{company}/{product}";

            // Store this dir in the config settings so we can get to it later.
            var inMemoryConfigData = new Dictionary<string, string> {
                { "ApplicationDataDir", applicationDataDir }
            };

            // bool inVScode = (Environment.GetEnvironmentVariable("RUNNING_IN_VSCODE") ?? "") == "true";
            bool reloadConfigOnChange = !inDocker;

            // Setup our custom Configuration Loader pipeline and build the configuration.
            IHost? host = CreateHostBuilder(args)
                       .ConfigureAppConfiguration(SetupConfigurationLoaders(args, platform, aspNetEnv,
                                                                            applicationDataDir,
                                                                            inMemoryConfigData,
                                                                            reloadConfigOnChange))
                       .Build()
                       ;

            _logger = host.Services.GetService<ILogger<Program>>();

            if (_logger != null)
            {
                string systemInfo = SystemInfo.GetSystemInfo();
                foreach (string line in systemInfo.Split('\n'))
                    _logger.LogInformation("** " + line.Trim());

                _logger.LogInformation($"** App DataDir:      {applicationDataDir}");

                string gpuInfo = await SystemInfo.GetGpuInfo();
                foreach (string line in gpuInfo.Split('\n'))
                    _logger.LogInformation(line.Trim());
            }

            Task? hostTask;
            hostTask = host.RunAsync();

#if DEBUG
            try
            {
                OpenBrowser($"http://localhost:{_port}/");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unable to open Dashboard on startup.");
            }
#endif
            try
            {
                await hostTask;

                Console.WriteLine("Shutting down");
            }
            catch (Exception ex)
            {
                // TODO: Host is gone, so no logger ??
                Console.WriteLine($"\n\nUnable to start the server: {ex.Message}.\n" +
                                  "Check that another instance is not running on the same port.");
                Console.Write("Press Enter to close.");
                Console.ReadLine();
            }
        }

        // Sets up our custom Configuration Loader Pipeline.
        private static Action<HostBuilderContext, IConfigurationBuilder> 
            SetupConfigurationLoaders(string[] args, string platform, string? aspNetEnv, 
                                      string applicationDataDir,
                                      Dictionary<string, string> inMemoryConfigData, 
                                      bool reloadConfigOnChange)
        {
            return (hostingContext, config) =>
            {
                string baseDir = AppContext.BaseDirectory;

                // Remove the default sources and rebuild it.
                config.Sources.Clear(); 

                // add in the default appsetting.json file and its variants
                config.AddJsonFile(Path.Combine(baseDir, "appsettings.json"),
                                    optional: false, reloadOnChange: reloadConfigOnChange);

                if (!string.IsNullOrWhiteSpace(aspNetEnv))
                {
                    config.AddJsonFile(Path.Combine(baseDir, $"appsettings.{aspNetEnv}.json"),
                                    optional: true, reloadOnChange: reloadConfigOnChange);
                }

                config.AddJsonFile(Path.Combine(baseDir, $"appsettings.{platform}.json"),
                                   optional: true, reloadOnChange: reloadConfigOnChange);

                // Load appsettings.platform.env.json files to allow slightly more
                // convenience for settings on other platforms
                if (!string.IsNullOrWhiteSpace(aspNetEnv))
                {
                    config.AddJsonFile(Path.Combine(baseDir, $"appsettings.{platform}.{aspNetEnv}.json"),
                                      optional: true, reloadOnChange: reloadConfigOnChange);
                }

                // This allows us to add ad-hoc settings such as ApplicationDataDir
                config.AddInMemoryCollection(inMemoryConfigData);

                // Load the installconfig.json file so we have access to the install ID
                config.AddJsonFile(Path.Combine(applicationDataDir, InstallConfig.InstallCfgFilename),
                                   reloadOnChange: reloadConfigOnChange, optional: true);

                // Load the version.json file so we have access to the Version info
                config.AddJsonFile(Path.Combine(baseDir, VersionConfig.VersionCfgFilename),
                                   reloadOnChange: reloadConfigOnChange, optional: true);

                // Load the modulesettings.json files to get analysis module settings
                LoadModulesConfiguration(config, aspNetEnv);

                // Load the last saved config values as set by the user
                LoadUserOverrideConfiguration(config, applicationDataDir, aspNetEnv);

                // Load Envinronmnet Variables into Configuration
                config.AddEnvironmentVariables();

                // Add command line back in to force it to have full override powers.
                if (args != null)
                    config.AddCommandLine(args);

                // For debug
                // ListConfigSources(config.Sources);
                // ListEnvVariables(Environment.GetEnvironmentVariables());
            };
        }

        // TODO: This does not belong here and should be moved in to a Modules class.
        // Loading of the module settings should not be done as part of the startup as this means 
        // modulesettings files can abort the Server startup.
        // We could:
        //      - create a separate ConfigurationBuilder
        //      - clear the configuration sources
        //      - add the modulesettings files as we do now
        //      - build a configuration from this builder
        //      - use this configuration to load the module settings
        // The module class will have methods and properties to get the ModuleConfigs, and other
        // things. To be done at a later date.
        private static void LoadModulesConfiguration(IConfigurationBuilder config, string? aspNetEnv)
        {
            bool reloadOnChange = (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "") != "true";

            IConfiguration configuration = config.Build();
            var options                  = configuration.GetSection("FrontEndOptions");
            string? rootPath             = options["ROOT_PATH"];
            string? modulesPath          = options["MODULES_PATH"];
            string? altModulesPath       = options["DOWNLOADED_MODULES_PATH"];

            // Get the Modules Path
            rootPath = BackendProcessRunner.GetRootPath(rootPath);

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                Console.WriteLine("No root path provided");
                return;
            }

            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"The provided root path '{rootPath}' doesn't exist");
                return;
            }

            modulesPath = modulesPath.Replace("%ROOT_PATH%", rootPath);
            modulesPath = modulesPath.Replace('\\', Path.DirectorySeparatorChar);
            modulesPath = Path.GetFullPath(modulesPath);

            altModulesPath = altModulesPath.Replace("%ROOT_PATH%", rootPath);
            altModulesPath = altModulesPath.Replace('\\', Path.DirectorySeparatorChar);
            altModulesPath = Path.GetFullPath(altModulesPath);

            if (string.IsNullOrWhiteSpace(modulesPath) && string.IsNullOrWhiteSpace(altModulesPath))
            {
                Console.WriteLine("No modules path provided");
                return;
            }

            if (!Directory.Exists(modulesPath) && !Directory.Exists(altModulesPath))
            {
                Console.WriteLine($"The provided modules paths '{modulesPath}' and '{altModulesPath}' don't exist");
                return;
            }

            string platform = BackendProcessRunner.Platform.ToLower();
            aspNetEnv = aspNetEnv?.ToLower();

            // Get the Modules Directories
            // Be careful of the order.
            if (!string.IsNullOrWhiteSpace(modulesPath) && Directory.Exists(modulesPath))
            {
                var directories = Directory.GetDirectories(modulesPath);
                foreach (string? directory in directories)
                {
                    config.AddJsonFile(Path.Combine(directory, "modulesettings.json"),
                                       optional: true, reloadOnChange: reloadOnChange);

                    if (!string.IsNullOrEmpty(aspNetEnv))
                    {
                        config.AddJsonFile(Path.Combine(directory, $"modulesettings.{aspNetEnv}.json"),
                                           optional: true, reloadOnChange: reloadOnChange);
                    }

                    config.AddJsonFile(Path.Combine(directory, $"modulesettings.{platform}.json"),
                                        optional: true, reloadOnChange: reloadOnChange);

                    if (!string.IsNullOrEmpty(aspNetEnv))
                    {
                        config.AddJsonFile(Path.Combine(directory, $"modulesettings.{platform}.{aspNetEnv}.json"),
                                          optional: true, reloadOnChange: reloadOnChange);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(altModulesPath) && Directory.Exists(altModulesPath))
            {
                var directories = Directory.GetDirectories(altModulesPath);
                foreach (string? directory in directories)
                {
                    config.AddJsonFile(Path.Combine(directory, "modulesettings.json"),
                                       optional: true, reloadOnChange: reloadOnChange);

                    if (!string.IsNullOrEmpty(aspNetEnv))
                    {
                        config.AddJsonFile(Path.Combine(directory, $"modulesettings.{aspNetEnv}.json"),
                                           optional: true, reloadOnChange: reloadOnChange);
                    }

                    config.AddJsonFile(Path.Combine(directory, $"modulesettings.{platform}.json"),
                                       optional: true, reloadOnChange: reloadOnChange);

                    if (!string.IsNullOrEmpty(aspNetEnv))
                    {
                        config.AddJsonFile(Path.Combine(directory, $"modulesettings.{platform}.{aspNetEnv}.json"),
                                           optional: true, reloadOnChange: reloadOnChange);
                    }
                }
            }
        }

        /// <summary>
        /// Loads the last-saved user configuration file
        /// </summary>
        /// <param name="config"></param>
        /// <param name="applicationDataDir">The directory containing the persisted user data</param>
        /// <param name="aspNetEnv">The current ASP.NET environment (Debug or Release)</param>
        private static void LoadUserOverrideConfiguration(IConfigurationBuilder config, 
                                                  string applicationDataDir, string? aspNetEnv)
        {
            bool reloadOnChange = (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "") != "true";

            if (string.IsNullOrWhiteSpace(applicationDataDir))
            {
                Console.WriteLine("No application data directory path provided");
                return;
            }

            if (!Directory.Exists(applicationDataDir))
            {
                Console.WriteLine($"The provided application data directory path '{applicationDataDir}' doesn't exist");
                return;
            }

            aspNetEnv = aspNetEnv?.ToLower();

            // For now, we'll store ALL module settings in the same file
            config.AddJsonFile(Path.Combine(applicationDataDir, "modulesettings.json"),
                               optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(aspNetEnv))
            {
                config.AddJsonFile(Path.Combine(applicationDataDir, $"modulesettings.{aspNetEnv}.json"),
                                   optional: true, reloadOnChange: reloadOnChange);
            }
        }

        /// <summary>
        /// Creates the Host Builder for the application
        /// </summary>
        /// <param name="args">The command line args</param>
        /// <returns>Returns the builder.</returns>
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                        // configure for running as a Windows Service or Linux Systemmd in addition
                        // as an executable in either OS.
                       .UseWindowsService()
                       .UseSystemd()
                       .ConfigureWebHostDefaults(webBuilder =>
                       {
                            webBuilder.UseShutdownTimeout(TimeSpan.FromSeconds(30));
                            webBuilder.ConfigureKestrel((hostbuilderContext, serverOptions) =>
                            {
                                _port = GetServerPort(hostbuilderContext);
                                serverOptions.Listen(IPAddress.IPv6Any, _port);
                                // We always want this port.
                                if (_port != 32168)
                                    serverOptions.Listen(IPAddress.IPv6Any, 32168);

                                // Add some legacy ports
                                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                {
                                    if (_port != 5500)
                                        serverOptions.Listen(IPAddress.IPv6Any, 5500);
                                }
                                else
                                {
                                    if (_port != 5000)
                                        serverOptions.Listen(IPAddress.IPv6Any, 5000);
                                }

                                // Add a self-signed certificate to enable HTTPS locally
                                // serverOptions.Listen(IPAddress.Loopback, _sPort,
                                //    listenOptions => {
                                //    {
                                //        listenOptions.UseHttps("testCert.pfx", "testPassword");
                                //    });
                            })
                            .UseStartup<Startup>();

                            // Keep things clean and simple for now
                            webBuilder.ConfigureLogging(builder =>
                            {
                                builder.ClearProviders()
                                       .AddFilter("Microsoft", LogLevel.Warning)
                                       .AddFilter("System", LogLevel.Warning)
                                       .AddServerLogger(configuration =>
                                       {
                                            // Replace warning value from appsettings.json of "Cyan"
                                            // configuration.LogLevels[LogLevel.Warning] = ConsoleColor.DarkCyan;
                                            // Replace warning value from appsettings.json of "Red"
                                            // configuration.LogLevels[LogLevel.Error] = ConsoleColor.DarkRed;
                                        });
                            });

                    // Or if we want to do this manually...
                    // webBuilder.UseUrls($"http://localhost:{_port}/", $"https://localhost:{_sPort}/");
                });
        }

        private static int GetServerPort(WebHostBuilderContext hostbuilderContext)
        {
            IConfiguration config = hostbuilderContext.Configuration;

            int port = config.GetValue("CPAI_PORT", -1);
            if (port < 0)
                port = config.GetValue("FrontEndOptions:EnvironmentVariables:CPAI_PORT", -1);  // TODO: PORT_CLIENT

            if (port < 0)
            {
                // urls will be a string in format <url>:port[;<url>:port_n]*;
                string urls = config.GetValue<string>("urls");
                if (!string.IsNullOrWhiteSpace(urls))
                {
                    var urlList = urls.Split(';');
                    if (urlList.Length > 0)
                        if (!int.TryParse(urlList[0].Split(':').Last().Trim('/'), out port))
                            port = _port;

                    config["CPAI_PORT"] = port.ToString();
                }
            }

            return port;
        }

        /// <summary>
        /// Lists the config sources
        /// </summary>
        /// <param name="sources">The sources</param>
        public static void ListConfigSources(IList<IConfigurationSource> sources)
        {
            foreach (var source in sources)
            {
                if (source is Microsoft.Extensions.Configuration.Json.JsonConfigurationSource jsonConfig)
                    Console.WriteLine($"Config source = {jsonConfig.Path}");
            }
        }

        /// <summary>
        /// Lists all current environment variables
        /// </summary>
        /// <param name="variables">The dict of environment variables</param>
        public static void ListEnvVariables(System.Collections.IDictionary variables)
        {
            foreach (string key in variables.Keys)
            {
                object? value = Environment.GetEnvironmentVariable(key);
                Console.WriteLine($"{key} = [{value ?? "null"}]");
            }
        }

        /// <summary>
        /// Opens the default browser on the given system with the given url.
        /// To be tested, and if there are issues, see also https://stackoverflow.com/a/53570859
        /// </summary>
        /// <param name="url"></param>
        public static void OpenBrowser(string url)
        {
            // HACK: Read this: https://github.com/dotnet/corefx/issues/10361 and 
            //       https://brockallen.com/2016/09/24/process-start-for-urls-on-net-core/

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("sensible-browser", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("sensible-browser", url);
                }
            }
            catch
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}