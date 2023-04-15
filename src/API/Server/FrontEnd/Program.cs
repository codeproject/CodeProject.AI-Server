using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using CodeProject.AI.API.Common;
using CodeProject.AI.SDK.Common;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            await SystemInfo.InitializeAsync();

            // lower cased as Linux has case sensitive file names
            string  os           = SystemInfo.OperatingSystem.ToLower();
            string  architecture = SystemInfo.Architecture.ToLower();
            string? runtimeEnv   = SystemInfo.RuntimeEnvironment == SDK.Common.RuntimeEnvironment.Development ||
                                   SystemInfo.IsDevelopmentCode ? "development" : string.Empty;

            var assembly         = Assembly.GetExecutingAssembly();
            var assemblyName     = (assembly.GetName().Name ?? string.Empty)
                                 + (os == "windows" ? ".exe" : ".dll");
            var serviceName      = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                                ?? assemblyName.Replace(".", " ");
            var servicePath      = Path.Combine(AppContext.BaseDirectory, assemblyName);

            var serviceDescription = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
                                  ?? string.Empty;

            if (args.Length == 1)
            {
                if (args[0].EqualsIgnoreCase("/Install"))
                {
                    WindowsServiceInstaller.Install(servicePath, serviceName, serviceDescription);
                    return;
                }
                else if (args[0].EqualsIgnoreCase("/Uninstall"))
                {
                    WindowsServiceInstaller.Uninstall(serviceName);
                    KillOrphanedProcesses(runtimeEnv);
                    return;
                }
                else if (args[0].EqualsIgnoreCase("/Start"))
                {
                    WindowsServiceInstaller.Start(serviceName);
                    return;
                }
                else if (args[0].EqualsIgnoreCase("/Stop"))
                {
                    WindowsServiceInstaller.Stop(serviceName);
                    KillOrphanedProcesses(runtimeEnv);
                    return;
                }
            }

            // make sure any processes that didn't get killed on the Service shutdown get killed now.
            KillOrphanedProcesses(runtimeEnv);

            // GetProcessStatus a directory for the given platform that allows momdules to store persisted data
            string programDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationDataDir = $"{programDataDir}\\{company}\\{product}".Replace('\\', Path.DirectorySeparatorChar);

            // .NET's suggestion for macOS and Linux aren't great. Let's do something different.
            if (os == "macos")
            {
                applicationDataDir = $"/Library/Application Support/{company}/{product}";
            }
            else if (os == "linux")
            {
                applicationDataDir = $"/etc/{company.ToLower()}/{product.ToLower()}";
            }

            // Store this dir in the config settings so we can get to it later.
            var inMemoryConfigData = new Dictionary<string, string?> {
                { "ApplicationDataDir", applicationDataDir }
            };

            bool reloadConfigOnChange = SystemInfo.ExecutionEnvironment != ExecutionEnvironment.Docker;

            // Setup our custom Configuration Loader pipeline and build the configuration.
            IHost? host = CreateHostBuilder(args)
                       .ConfigureAppConfiguration(SetupConfigurationLoaders(args, os, architecture,
                                                                            runtimeEnv, applicationDataDir,
                                                                            inMemoryConfigData,
                                                                            reloadConfigOnChange))
                       .Build()
                       ;

            _logger = host.Services.GetService<ILogger<Program>>();

            if (_logger != null)
            {
                string systemInfo = SystemInfo.GetSystemInfo();
                foreach (string line in systemInfo.Split('\n'))
                    _logger.LogInformation("** " + line.TrimEnd());

                _logger.LogInformation($"** App DataDir:      {applicationDataDir}");

                string info = await SystemInfo.GetGpuUsageInfo();
                foreach (string line in info.Split('\n'))
                    _logger.LogInformation(line.TrimEnd());

                info = await SystemInfo.GetVideoAdapterInfo();
                foreach (string line in info.Split('\n'))
                    _logger.LogInformation(line.TrimEnd());
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

        private static void KillOrphanedProcesses(string? runtimeEnv)
        {
            if (SystemInfo.OperatingSystem.EqualsIgnoreCase("Windows"))
            {
                try
                {
                    Console.WriteLine("Stopping any orphaned Processes.");
                    var scriptDir = Path.Combine(AppContext.BaseDirectory, 
                                                 runtimeEnv != "development" 
                                                            ? "..\\SDK\\Scripts\\"
                                                            : "..\\..\\..\\..\\..\\..\\SDK\\Scripts\\");
                    var startInfo = new ProcessStartInfo()
                    {
                        WindowStyle     = ProcessWindowStyle.Hidden,
                        FileName        = Path.Combine(scriptDir,"stop_all.bat"),
                        WorkingDirectory= Path.GetDirectoryName(scriptDir)
                    };

                    var process = Process.Start(startInfo);
                    process?.WaitForExit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception trying to stop orphaned Processes {ex.Message}");
                }

            }
        }

        /// <summary>
        /// Sets up our custom Configuration Loader Pipeline
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <param name="os">The operating system</param>
        /// <param name="architecture">The architecture (x86, arm64 etc)</param>
        /// <param name="runtimeEnv">Whether this is development or production</param>
        /// <param name="applicationDataDir">The path to the folder containing application data</param>
        /// <param name="inMemoryConfigData">The in-memory config data</param>
        /// <param name="reloadConfigOnChange">Whether to reload files if they are saved during runtime</param>
        /// <returns></returns>
        private static Action<HostBuilderContext, IConfigurationBuilder> SetupConfigurationLoaders(string[] args,
            string os, string architecture, string? runtimeEnv, string applicationDataDir,
            Dictionary<string, string?> inMemoryConfigData, bool reloadConfigOnChange)
        {
            return (hostingContext, config) =>
            {
                string baseDir = AppContext.BaseDirectory;

                // RemoveProcessStatus the default sources and rebuild it.
                config.Sources.Clear(); 

                // add in the default appsetting.json file and its variants
                // In order
                // appsettings.json
                // appsettings.development.json
                // appsettings.os.json
                // appsettings.os.development.json
                // appsettings.os.architecture.json
                // appsettings.os.architecture.development.json
                // appsettings.docker.json
                // appsettings.docker.development.json

                string settingsFile = Path.Combine(baseDir, "appsettings.json");
                config.AddJsonFile(settingsFile, optional: false, reloadOnChange: reloadConfigOnChange);

                if (!string.IsNullOrWhiteSpace(runtimeEnv))
                {
                    settingsFile = Path.Combine(baseDir, $"appsettings.{runtimeEnv}.json");
                    if (File.Exists(settingsFile))                
                        config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
                }

                settingsFile = Path.Combine(baseDir, $"appsettings.{os}.json");
                if (File.Exists(settingsFile))                
                    config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                if (!string.IsNullOrWhiteSpace(runtimeEnv))
                {
                    settingsFile = Path.Combine(baseDir, $"appsettings.{os}.{runtimeEnv}.json");
                    if (File.Exists(settingsFile))                
                        config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
                }

                settingsFile = Path.Combine(baseDir, $"appsettings.{os}.{architecture}.json");
                if (File.Exists(settingsFile))                
                    config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                if (!string.IsNullOrWhiteSpace(runtimeEnv))
                {
                    settingsFile = Path.Combine(baseDir, $"appsettings.{os}.{architecture}.{runtimeEnv}.json");
                    if (File.Exists(settingsFile))                
                        config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
                }

                if (SystemInfo.ExecutionEnvironment == ExecutionEnvironment.Docker)
                {
                    settingsFile = Path.Combine(baseDir, $"appsettings.docker.json");
                    if (File.Exists(settingsFile))                
                        config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                    if (!string.IsNullOrWhiteSpace(runtimeEnv))
                    {
                        settingsFile = Path.Combine(baseDir, $"appsettings.docker.{runtimeEnv}.json");
                        if (File.Exists(settingsFile))                
                            config.AddJsonFile(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
                    }                        
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
                LoadModulesConfiguration(config, runtimeEnv);

                // Load the last saved config values as set by the user
                LoadUserOverrideConfiguration(config, applicationDataDir, runtimeEnv, reloadConfigOnChange);

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
        private static void LoadModulesConfiguration(IConfigurationBuilder config, string? runtimeEnv)
        {
            bool reloadOnChange = SystemInfo.ExecutionEnvironment != ExecutionEnvironment.Docker;

            IConfiguration configuration = config.Build();
            (var modulesPath, var preInstalledModulesPath) = EnsureDirectories(configuration, runtimeEnv);

            // Scan the Modules' directories and add each modulesettings files to the config
            if (!string.IsNullOrWhiteSpace(modulesPath) && Directory.Exists(modulesPath))
            {
                var directories = Directory.GetDirectories(modulesPath);
                foreach (string? directory in directories)
                    ModuleSettings.LoadModuleSettings(config, directory, reloadOnChange);
            }

            // Scan the pre-installed Modules' directories and add each modulesettings files
            if (!string.IsNullOrWhiteSpace(preInstalledModulesPath) && Directory.Exists(preInstalledModulesPath))
            {
                var directories = Directory.GetDirectories(preInstalledModulesPath);
                foreach (string? directory in directories)
                    ModuleSettings.LoadModuleSettings(config, directory, reloadOnChange);
            }
        }

        private static (string?,string?) EnsureDirectories(IConfiguration configuration, string? runtimeEnv)
        {
            var serverOptions = configuration.GetSection("ServerOptions");
            string? rootPath  = serverOptions["ApplicationRootPath"];

            // GetProcessStatus the Application root Path
            rootPath = ModuleSettings.GetRootPath(rootPath);

            if (string.IsNullOrWhiteSpace(rootPath))
            {
                Console.WriteLine("No root path provided");
                return (null, null);
            }

            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"The provided root path '{rootPath}' doesn't exist");
                return (null, null);
            }

            var moduleOptions                  = configuration.GetSection("ModuleOptions");
            string? runtimesPath               = moduleOptions["RuntimesPath"];
            string? modulesPath                = moduleOptions["ModulesPath"];
            string? preInstalledModulesPath    = moduleOptions["PreInstalledModulesPath"];
            string? downloadedPackagesPath     = moduleOptions["DownloadedModulePackagesPath"];
            string? moduleInstallerScriptsPath = moduleOptions["ModuleInstallerScriptsPath"];

            // make sure that all the require paths are defined
            if (string.IsNullOrWhiteSpace(runtimesPath))
            {
                Console.WriteLine("No runtime path provided");
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(modulesPath))
            {
                Console.WriteLine("No modules path provided");
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(downloadedPackagesPath))
            {
                Console.WriteLine("No downloaded module Packages path provided");
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(moduleInstallerScriptsPath))
            {
                Console.WriteLine("No modules Installer path provided");
                return (null, null);
            }

            // get the full paths
            runtimesPath               = Text.FixSlashes(runtimesPath?.Replace("%ROOT_PATH%", rootPath));
            runtimesPath               = Path.GetFullPath(runtimesPath);
            downloadedPackagesPath     = Text.FixSlashes(downloadedPackagesPath?.Replace("%ROOT_PATH%", rootPath));
            downloadedPackagesPath     = Path.GetFullPath(downloadedPackagesPath);
            moduleInstallerScriptsPath = Text.FixSlashes(moduleInstallerScriptsPath?.Replace("%ROOT_PATH%", rootPath));
            moduleInstallerScriptsPath = Path.GetFullPath(moduleInstallerScriptsPath);
            modulesPath                = Text.FixSlashes(modulesPath?.Replace("%ROOT_PATH%", rootPath));
            modulesPath                = Path.GetFullPath(modulesPath);
            preInstalledModulesPath    = Text.FixSlashes(preInstalledModulesPath?.Replace("%ROOT_PATH%", rootPath));
            preInstalledModulesPath    = Path.GetFullPath(preInstalledModulesPath);

            // create the directories if the don't exist
            if (!Directory.Exists(runtimesPath))
            {
                Console.WriteLine($"Creating runtimes path '{runtimesPath}'");
                Directory.CreateDirectory(runtimesPath);
            }

            if (!Directory.Exists(downloadedPackagesPath))
            {
                Console.WriteLine($"Creating downloaded modules package path '{downloadedPackagesPath}'");
                Directory.CreateDirectory(downloadedPackagesPath);
            }

            if (!Directory.Exists(moduleInstallerScriptsPath))
            {
                Console.WriteLine($"Creating modules installer path '{moduleInstallerScriptsPath}'");
                Directory.CreateDirectory(moduleInstallerScriptsPath);
            }

            if (!Directory.Exists(modulesPath))
            {
                Console.WriteLine($"Creating modules path '{modulesPath}'");
                Directory.CreateDirectory(modulesPath);
            }

            var srcPath = Path.Combine(rootPath, "src");

            // copy over the SDK if required.
            if (runtimeEnv?.ToLower() == "development" && !srcPath.EqualsIgnoreCase(moduleInstallerScriptsPath))
            {
                Console.WriteLine("Copying SDK and Setup Scripts");

                File.Copy(Path.Combine(srcPath, "setup.bat"), Path.Combine(moduleInstallerScriptsPath, "setup.bat"), true);
                File.Copy(Path.Combine(srcPath, "setup.sh"), Path.Combine(moduleInstallerScriptsPath, "setup.sh"), true);
                CopyDirectory(Path.Combine(srcPath, "SDK"), Path.Combine(moduleInstallerScriptsPath, "SDK"),true);
            }

            return (modulesPath, preInstalledModulesPath);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        /// <summary>
        /// Loads the last-saved user configuration file
        /// </summary>
        /// <param name="config"></param>
        /// <param name="applicationDataDir">The directory containing the persisted user data</param>
        /// <param name="runtimeEnv">The current runtime environment (production or development)</param>
        /// <param name="reloadOnChange">Whether to reload the config files if they change</param>
        private static void LoadUserOverrideConfiguration(IConfigurationBuilder config, 
                                                          string applicationDataDir, 
                                                          string? runtimeEnv, bool reloadOnChange)
        {
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

            runtimeEnv = runtimeEnv?.ToLower();

            // For now, we'll store ALL module settings in the same file
            config.AddJsonFile(Path.Combine(applicationDataDir, "modulesettings.json"),
                               optional: true, reloadOnChange: reloadOnChange);

            if (!string.IsNullOrEmpty(runtimeEnv))
            {
                config.AddJsonFile(Path.Combine(applicationDataDir, $"modulesettings.{runtimeEnv}.json"),
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
                                IConfiguration config = hostbuilderContext.Configuration;
                                bool disableLegacyPort = config.GetValue("ServerOptions:DisableLegacyPort", false);

                                _port = GetServerPort(hostbuilderContext);
                                bool foundPort = false;

                                if (IsPortAvailable(_port))
                                {
                                    serverOptions.Listen(IPAddress.IPv6Any, _port);
                                    foundPort = true;
                                }

                                // We always want this port.
                                if (_port != 32168 && IsPortAvailable(32168))
                                {
                                    if (!foundPort)
                                        _port = 32168;

                                    serverOptions.Listen(IPAddress.IPv6Any, 32168);
                                    foundPort = true;
                                }

                                if (!disableLegacyPort)
                                {
                                    // Add some legacy ports
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                    {
                                        if (_port != 5500 && IsPortAvailable(5500))
                                        {
                                            if (!foundPort)
                                                _port = 5500;
                                        
                                            serverOptions.Listen(IPAddress.IPv6Any, 5500);
                                           foundPort = true;
                                        }
                                    }
                                    else
                                    {
                                        if (_port != 5000 && IsPortAvailable(5000))
                                        {
                                            if (!foundPort)
                                                _port = 5000;
                                        
                                            serverOptions.Listen(IPAddress.IPv6Any, 5000);
                                            foundPort = true;
                                        }
                                    }
                                }

                                if (!foundPort)
                                {
                                    _port = GetAvailablePort(IPAddress.IPv6Any);
                                    serverOptions.Listen(IPAddress.IPv6Any, _port);
                                    Console.WriteLine("Standard ports in use. Falling back to port " + _port);
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

        /// <summary>
        /// Checks as to whether a given port on this machine is avaialble for use.
        /// </summary>
        /// <param name="port">The port number</param>
        /// <returns>true if the port is available; false otherwise</returns>
        private static bool IsPortAvailable(int port)
        {
            bool isAvailable = true;

            // Evaluate current system tcp connections. This is the same information provided
            // by the netstat command line application, just in .Net strongly-typed object
            // form.  We will look through the list, and if our port we would like to use
            // in our TcpClient is occupied, we will set isAvailable to false.
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
            {
                if (tcpi.LocalEndPoint.Port == port)
                {
                    isAvailable = false;
                    break;
                }
            }

            return isAvailable;
        }

        /// <summary>
        /// Gets an available port for this server
        /// </summary>
        /// <returns></returns>    
        private static int GetAvailablePort(IPAddress address)
        {
            TcpListener listener = new TcpListener(address, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }

        private static int GetServerPort(WebHostBuilderContext hostbuilderContext)
        {
            IConfiguration config = hostbuilderContext.Configuration;

            int port = config.GetValue("CPAI_PORT", -1);
            if (port < 0)
                port = config.GetValue("ServerOptions:EnvironmentVariables:CPAI_PORT", -1);  // TODO: PORT_CLIENT

            if (port < 0)
            {
                // urls will be a string in format <url>:port[;<url>:port_n]*;
                string? urls = config.GetValue<string>("urls");
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
                    Process.Start("open", url);
                    // Process.Start("sensible-browser", url);
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
                    // Process.Start("open", url);
                    Process.Start("sensible-browser", url);

                }
                else
                {
                    throw;
                }
            }
        }
    }
}