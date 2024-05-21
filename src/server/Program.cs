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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Common;
using CodeProject.AI.SDK.Utils;
using CodeProject.AI.Server.Backend;
using CodeProject.AI.Server.Modules;

namespace CodeProject.AI.Server
{
    /// <summary>
    /// The Application Entry Class.
    /// </summary>
    public class Program
    {
        static private ILogger? _logger = null;

        static private int _port = Constants.DefaultPort;
        // static private int _sPort = Constants.DefaultPortSsl; - eventually for SSL

        /// <summary>
        /// Gets or sets the Root Directory of the installation.
        /// </summary>
        public static string ApplicationRootPath { get; set; }

        /// <summary>
        /// Gets or sets the map of module Ids to module paths.
        /// </summary>
        public static Dictionary<string, (string, ModuleLocation)> ModuleIdModuleDirMap { get; set; } = new Dictionary<string, (string, ModuleLocation)>();

        /// <summary>
        /// The static constructor for the program
        /// </summary>
        static Program()
        {
            ApplicationRootPath = GetAppRootPath();
        }

        /// <summary>
        /// The Application Entry Point.
        /// </summary>
        /// <param name="args">The command line args.</param>
        public static async Task Main(string[] args)
        {
            var assembly        = Assembly.GetExecutingAssembly();
            string assemblyName = (assembly.GetName().Name ?? string.Empty) 
                                + (SystemInfo.IsWindows? ".exe" : ".dll");
            string companyName  = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
                                ?? Constants.Company;
            string productCat   = Constants.ProductCategory;
            string productName  = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                                ?? Constants.ProductName;

            string servicePath  = Path.Combine(AppContext.BaseDirectory, assemblyName);
            string serviceDesc  = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
                                ?? productName;

            // BE CAREFUL WITH THIS NAME. It needs to be:
            //  - "CodeProject.AI Server" for windows
            //  - "codeproject.ai-server" for Linux/macOS.
            string serviceName = productName;
            if (!SystemInfo.IsWindows)
                serviceName = productName.Replace(" ", "-").ToLower();

            await SystemInfo.InitializeAsync().ConfigureAwait(false);

            // lower cased as Linux has case sensitive file names
            string  os           = SystemInfo.OperatingSystem.ToLower();
            string  architecture = SystemInfo.Architecture.ToLower();
            string  edgeDevice   = SystemInfo.EdgeDevice.ToLower().Replace(" ", string.Empty);
            string? runtimeEnv   = SystemInfo.RuntimeEnvironment == SDK.Common.RuntimeEnvironment.Development
                                 ? Constants.Development : string.Empty;

            // GetProcessStatus a directory for the given platform that allows modules to store persisted data
            string programDataDir     = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationDataDir = programDataDir + "/" + companyName + "/" + productCat;
            applicationDataDir = applicationDataDir.Replace('/', Path.DirectorySeparatorChar);

            // .NET's suggestion for macOS and Linux aren't great. Let's do something different.
            if (SystemInfo.IsMacOS)
            {
                applicationDataDir = $"/Library/Application Support/{companyName}/{productCat}";
            }
            else if (SystemInfo.IsLinux)
            {
                applicationDataDir = $"/etc/{companyName.ToLower()}/{productCat.ToLower()}";
            }

            if (args.Length == 1)
            {
                if (args[0].EqualsIgnoreCase("/Install"))
                {
                    WindowsServiceInstaller.Install(servicePath, serviceName, serviceDesc);
                    return;
                }
                else if (args[0].EqualsIgnoreCase("/Uninstall"))
                {
                    WindowsServiceInstaller.Uninstall(serviceName);
                    KillOrphanedProcesses();
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
                    KillOrphanedProcesses();
                    return;
                }
                else if (args[0].EqualsIgnoreCase("/Clean"))
                {
                    RemoveOldModulesAndData(applicationDataDir);
                    return;
                }
            }

            // Prevent this app from starting more that one instance. Be careful, though: creating
            // a named mutex isn't guaranteed to work. For Linux, in particular, a mutex's name
            // must be a valid filename
            Mutex? mutex = null;
            try
            {
                mutex = new Mutex(false, serviceName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to create Mutex (but we'll carry on): " + ex.Message);
            }

            try
            {
                if (mutex is not null && !mutex.WaitOne(0))
                {
                    mutex.Dispose();
                    Console.WriteLine($"{productName} is already running. Exiting.");
                    return;
                }
            }
            catch (AbandonedMutexException)
            {
                Console.WriteLine($"Looks like {productName} may have exited incorrectly last time. Continuing.");
            }
            catch(Exception)
            {
                mutex!.Dispose();
                Console.WriteLine($"Unable to get an exclusive lock for {productName}. It could already be running. Exiting.");
                return;
            }

            // Make sure any processes that didn't get killed on the Service shutdown are now killed
            KillOrphanedProcesses();

            // Store this dir in the config settings so we can get to it later.
            var inMemoryConfigData = new Dictionary<string, string?> {
                { "ApplicationDataDir", applicationDataDir }
            };

            try
            {
                // Setup our custom Configuration Loader pipeline and build the configuration.
                var action = SetupConfigurationLoaders(args, os, architecture, edgeDevice, runtimeEnv,
                                                       applicationDataDir, inMemoryConfigData);
                IHostBuilder? hostBuilder = CreateHostBuilder(args).ConfigureAppConfiguration(action);
                IHost? host = hostBuilder.Build();

                _logger = host.Services.GetService<ILogger<Program>>();
                if (_logger != null)
                {
                    string installModulesFile = ModuleInstaller.InstallModulesFileName;
                    _logger.LogDebug($"Settings file: {installModulesFile} exists: {File.Exists(installModulesFile)}");

                    string systemInfo = SystemInfo.GetSystemInfo();
                    foreach (string line in systemInfo.Split('\n'))
                        _logger.LogInformation("** " + line.TrimEnd());

                    _logger.LogInformation($"** App DataDir:      {applicationDataDir}");

                    string info = await SystemInfo.GetVideoAdapterInfoAsync().ConfigureAwait(false);
                    foreach (string line in info.Split('\n'))
                        _logger.LogInformation(line.TrimEnd());
                
                    _logger.LogInformation($"*** STARTING CODEPROJECT.AI SERVER");
                }

                Task? hostTask = host.RunAsync();
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
                await hostTask.ConfigureAwait(false);

                Console.WriteLine("Shutting down");
            }
            catch (IOException io_ex)
            {
                if (io_ex.Message.Contains("the number of inotify instances has been reached"))
                {
                    Console.WriteLine("\n\nUnable to start the server due to too many file " +
                                      "watchers being in place. This is a system limit. Run\n\n" +
                                      "   sysctl fs.inotify\n\n to view your machine's limits. " +
                                      "To change the limits for this session only, run\n\n" +
                                      "   sudo sysctl fs.inotify.max_user_instances=1024\n\n" +
                                      "to set the max instance for you to 1024. This may help.");
                }
                else
                {
                    Console.WriteLine($"\n\nUnable to start the server due to IO error: {io_ex.Message}.");
                }

                Console.Write("Press Enter to close.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n\nUnable to start the server: {ex.Message}.\n" +
                                    "Check that another instance is not running on the same port.");
                Console.Write("Press Enter to close.");
                Console.ReadLine();
            }
            finally
            {
                mutex?.Dispose();
            }
        }

        /// <summary>
        /// Returns the absolute path to the root folder of the application
        /// </summary>
        /// <returns>A string</returns>
        private static string GetAppRootPath()
        {
            // Start from this assembly. We'll work our way up
            string rootPath = AppContext.BaseDirectory;

            // The server (this program) is under /src/server/bin/Debug/net7.0 while debugging, or
            // maybe under Release, but when deployed it's simply under /server. 
            // ASSUMPTION: There is no folder called "server" between /server and this assembly.
            // ASSUMPTION: This application was not installed in a folder named 'src'.
            
            DirectoryInfo? info = new DirectoryInfo(rootPath);
            while (info != null)
            {
                // Keep moving up until we hit /server
                if (info?.Name.ToLower() == "server")
                {
                    info = info.Parent; // This will be the root in the installed version

                    // For debug / dev environment, the parent is src
                    if (info?.Name.ToLower() == "src")
                        info = info.Parent;

                    // We're done. Pull the plug on this
                    break;
                }
                
                // Move up one level
                info = info?.Parent;
            }

            if (info != null)
                rootPath = info.FullName;

            return rootPath;
        }

        private static void RemoveOldModulesAndData(string applicationDataDir)
        {
            string baseDir   = GetAppRootPath();
            string offsetDir = SystemInfo.IsDevelopmentCode? "src/" : string.Empty;
            
            // Let's not do this for dev
            if (SystemInfo.RuntimeEnvironment == CodeProject.AI.SDK.Common.RuntimeEnvironment.Development)
                return;

            if (SystemInfo.IsWindows)
            {
                try
                {
                    Console.WriteLine("Removing all the Modules and Data created at runtime.");

                    string[] directories2del = { "downloads", "modules", "runtimes" };
                    foreach (string dir in directories2del)
                    {
                        string path = Path.Combine(baseDir, offsetDir + dir);
                        try
                        {
                            if (Directory.Exists(path))
                                Directory.Delete(path, true);
                        }
                        catch (Exception e)
                        {
                            // Handle exception here
                            Console.WriteLine("Error Deleting files:" + e.Message);
                        }
                    }

                    string logPath = Path.Combine(baseDir, "logs");
                    try
                    {
                        if (Directory.Exists(logPath))
                            Directory.Delete(logPath, true);
                    }
                    catch (Exception e)
                    {
                        // Handle exception here
                        Console.WriteLine("Error deleting log files: " + e.Message);
                    }

                    try
                    {
                        if (Directory.Exists(applicationDataDir))
                        {
                            // remove all the files except the installconfig.json file
                            foreach (string filename in Directory.GetFiles(applicationDataDir))
                                if (filename.EndsWith(InstallConfig.InstallCfgFilename, StringComparison.OrdinalIgnoreCase))
                                    File.Delete(Path.Combine(filename));
                        }
                    }
                    catch (Exception e)
                    {
                        // Handle exception here
                        Console.WriteLine("Error deleting application data directory: " + e.Message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception trying to Remove Modules and Data {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Kills all running modules. This is important to do in the case of services (eg Windows
        /// services or Linux systemd services) in order to ensure things are cleaned up properly.
        /// Sometimes a service ends, but the modules are not shut down properly.
        /// </summary>
        private static void KillOrphanedProcesses()
        {
            if (!SystemInfo.IsWindows)
                return;
                
            string baseDir      = GetAppRootPath();
            string offsetDir    = SystemInfo.IsDevelopmentCode? "src/" : string.Empty;
            string utilitiesDir = Path.Combine(baseDir, offsetDir, "SDK/Utilities/");

            try
            {
                Console.WriteLine("Stopping any orphaned Processes.");

                ProcessStartInfo procStartInfo;
                if (SystemInfo.IsWindows)
                    procStartInfo = new ProcessStartInfo(Path.Combine(utilitiesDir, "stop_all.bat"));
                else if (SystemInfo.IsMacOS)
                    procStartInfo = new ProcessStartInfo("bash", '"' + Path.Combine(utilitiesDir, "stop_all.sh") + '"');
                else
                    procStartInfo = new ProcessStartInfo("bash", Path.Combine(utilitiesDir, "stop_all.sh"));

                procStartInfo.UseShellExecute  = false;
                procStartInfo.WorkingDirectory = Path.GetDirectoryName(utilitiesDir);
                procStartInfo.CreateNoWindow   = false;
                procStartInfo.WindowStyle      = ProcessWindowStyle.Hidden;

                var process = Process.Start(procStartInfo);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception trying to stop orphaned Processes {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up our custom Configuration Loader Pipeline
        /// </summary>
        /// <param name="args">The command line arguments</param>
        /// <param name="os">The operating system</param>
        /// <param name="architecture">The architecture (x86, arm64 etc)</param>
        /// <param name="edgeDevice">The system name</param>
        /// <param name="runtimeEnv">Whether this is development or production</param>
        /// <param name="applicationDataDir">The path to the folder containing application data</param>
        /// <param name="inMemoryConfigData">The in-memory config data</param>
        /// <returns>An Action object</returns>
        private static Action<HostBuilderContext, IConfigurationBuilder> SetupConfigurationLoaders(string[] args,
            string os, string architecture, string edgeDevice, string? runtimeEnv,
            string applicationDataDir, Dictionary<string, string?> inMemoryConfigData)
        {
            // We don't want to put file watches on files, unless absolutely necessary
            bool reloadConfigOnChange = false;

            return (hostingContext, config) =>
            {
                // We assume the json files are in the same directory as the main assembly (which
                // is probably NOT the application's root folder, since we run our server from
                // the /server dir)
                string baseDir = AppContext.BaseDirectory;

                // RemoveProcessStatus the default sources and rebuild it.
                config.Sources.Clear();

                // add in the default appsettings.json file and its variants
                // In order
                // appsettings.json
                // appsettings.development.json
                // appsettings.os.json
                // appsettings.os.development.json
                // appsettings.os.architecture.json
                // appsettings.os.architecture.development.json
                // appsettings.device.json          device = raspberrypi, orangepi, radxarock, jetson, etc
                // appsettings.system.development.json

                AddConfigFileVariations("appsettings", os, architecture, edgeDevice, runtimeEnv, 
                                        config, reloadConfigOnChange, baseDir, false);

                // Add the appsettings.install.json file
                string settingsFile = Path.Combine(baseDir, ModuleInstaller.InstallModulesFileName);
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                // This allows us to add ad-hoc settings such as ApplicationDataDir
                config.AddInMemoryCollection(inMemoryConfigData);

                // Load the installconfig.json file so we have access to the install ID
                settingsFile = Path.Combine(applicationDataDir, InstallConfig.InstallCfgFilename);
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                // Load the version.json file so we have access to the Version info
                settingsFile = Path.Combine(baseDir, VersionConfig.VersionCfgFilename);
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                // Add the modulesettings.json files to get analysis module settings
                // NOTE: This method will load up the config with the files added above. If you need
                // to access a config value in AddModulesConfigFiles, ensure that value has been 
                // added before this point
                AddModulesConfigurationFiles(config);

                // Add the last saved config values for modules as set by the user
                AddUserOverrideConfigurationFiles(config, applicationDataDir, runtimeEnv, !SystemInfo.IsLinux);

                // Load the triggers.json file to load the triggers
                settingsFile = Path.Combine(baseDir, TriggersConfig.TriggersCfgFilename);
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                // Load Environment Variables into Configuration
                config.AddEnvironmentVariables();

                // Add command line back in to force it to have full override powers.
                if (args != null)
                    config.AddCommandLine(args);

                // Turn off reload on change for Linux
                if (SystemInfo.IsLinux)
                {
                    config.Sources.Where(s => s is FileConfigurationSource).ToList()
                                  .ForEach(s => ((FileConfigurationSource)s).ReloadOnChange = false);
                }

                // For debug
                // ListConfigSources(config.Sources);
                // ListEnvVariables(Environment.GetEnvironmentVariables());
            };
        }

        private static void AddConfigFileVariations(string baseFilename, string os, string architecture,
                                                    string edgeDevice, string? runtimeEnv,
                                                    IConfigurationBuilder config,
                                                    bool reloadConfigOnChange, string baseDir,
                                                    bool rootIsOptional)
        {
            string settingsFile = Path.Combine(baseDir, $"{baseFilename}.json");
            config.AddJsonFileSafe(settingsFile, optional: rootIsOptional, reloadOnChange: reloadConfigOnChange);

            if (!string.IsNullOrWhiteSpace(runtimeEnv))
            {
                settingsFile = Path.Combine(baseDir, $"{baseFilename}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
            }

            settingsFile = Path.Combine(baseDir, $"{baseFilename}.{os}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

            if (!string.IsNullOrWhiteSpace(runtimeEnv))
            {
                settingsFile = Path.Combine(baseDir, $"{baseFilename}.{os}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
            }

            settingsFile = Path.Combine(baseDir, $"{baseFilename}.{os}.{architecture}.json");
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

            if (!string.IsNullOrWhiteSpace(runtimeEnv))
            {
                settingsFile = Path.Combine(baseDir, $"{baseFilename}.{os}.{architecture}.{runtimeEnv}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
            }

            if (!string.IsNullOrWhiteSpace(edgeDevice))
            {
                settingsFile = Path.Combine(baseDir, $"{baseFilename}.{edgeDevice}.json");
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);

                if (!string.IsNullOrWhiteSpace(runtimeEnv))
                {
                    settingsFile = Path.Combine(baseDir, $"{baseFilename}.{edgeDevice}.{runtimeEnv}.json");
                    config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadConfigOnChange);
                }
            }
        }

        /// <summary>
        /// Adds the configuration files for each module to the config tree
        /// </summary>
        /// <remarks>
        /// TODO: This does not belong here and should be moved in to a Modules class. Loading of
        /// the module settings should not be done as part of the startup as this means modulesettings
        /// files can abort the Server startup. We could:
        ///   - create a separate ConfigurationBuilder
        ///   - clear the configuration sources
        ///   - add the modulesettings files as we do now
        ///   - build a configuration from this builder
        ///   - use this configuration to load the module settings
        /// The module class will have methods and properties to get the ModuleConfigs, and other
        /// things. To be done at a later date.
        /// </remarks>
        private static void AddModulesConfigurationFiles(IConfigurationBuilder config)
        {           
            bool reloadOnChange = !SystemInfo.IsDocker;

            // Load up the files already added to the config (just server settings) so we can query
            // the values and set things up
            IConfiguration configuration = config.Build();

            (var modulesDirPath, 
             var preInstalledModulesDirPath,
             var demoModulesDirPath,
             var externalModulesDirPath) = EnsureDirectories(configuration);

            // Scan the Modules' directories and add each modulesettings files to the config
            if (!string.IsNullOrWhiteSpace(modulesDirPath) && Directory.Exists(modulesDirPath))
            {                
                var directories = Directory.GetDirectories(modulesDirPath);
                foreach (string? directory in directories)
                {
                    // Bad assumption: A module's ID is same as the name of folder in which it lives.
                    // string? moduleId = new DirectoryInfo(directory).Name;

                    string? moduleId = ModuleConfigExtensions.GetModuleIdFromModuleSettings(directory);
                    if (moduleId is null || ModuleIdModuleDirMap.ContainsKey(moduleId))
                        continue;

                    // Check the modulesettings file and, if necessary, reformat it
                    ModuleConfigExtensions.RewriteOldModuleSettingsFile(directory);

                    // Load up (existing, or potentially updated) settings
                    config.AddModuleSettingsConfigFiles(directory, reloadOnChange);

                    ModuleIdModuleDirMap.Add(moduleId, (directory, ModuleLocation.Internal));
                }
            }

            // Scan the pre-installed Modules' directories and add each modulesettings files
            if (!string.IsNullOrWhiteSpace(preInstalledModulesDirPath) && 
                Directory.Exists(preInstalledModulesDirPath))
            {
                var directories = Directory.GetDirectories(preInstalledModulesDirPath);
                foreach (string? directory in directories)
                {
                    // Bad assumption: A module's ID is same as the name of folder in which it lives.
                    // string? moduleId = new DirectoryInfo(directory).Name;

                    string? moduleId = ModuleConfigExtensions.GetModuleIdFromModuleSettings(directory);
                    if (moduleId is null || ModuleIdModuleDirMap.ContainsKey(moduleId))
                        continue;

                    config.AddModuleSettingsConfigFiles(directory, reloadOnChange);
                    ModuleIdModuleDirMap.Add(moduleId, (directory, ModuleLocation.PreInstalled));
                }
            }

            // Scan the external Modules' directories and add each modulesettings files
            if (!string.IsNullOrWhiteSpace(externalModulesDirPath) && 
                Directory.Exists(externalModulesDirPath))
            {
                var directories = Directory.GetDirectories(externalModulesDirPath);
                foreach (string? directory in directories)
                {
                    // Bad assumption: A module's ID is same as the name of folder in which it lives.
                    // string? moduleId = new DirectoryInfo(directory).Name;

                    string? moduleId = ModuleConfigExtensions.GetModuleIdFromModuleSettings(directory);
                    if (moduleId is null || ModuleIdModuleDirMap.ContainsKey(moduleId))
                        continue;

                    config.AddModuleSettingsConfigFiles(directory, reloadOnChange);
                    ModuleIdModuleDirMap.Add(moduleId, (directory, ModuleLocation.External));
                }
            }

            // Finally, scan the demo Modules' directories and add each modulesettings files
            if (!string.IsNullOrWhiteSpace(demoModulesDirPath) && Directory.Exists(demoModulesDirPath))
            {
                var directories = Directory.GetDirectories(demoModulesDirPath);
                foreach (string? directory in directories)
                {
                    // Bad assumption: A module's ID is same as the name of folder in which it lives.
                    // string? moduleId = new DirectoryInfo(directory).Name;

                    string? moduleId = ModuleConfigExtensions.GetModuleIdFromModuleSettings(directory);
                    if (moduleId is null || ModuleIdModuleDirMap.ContainsKey(moduleId))
                        continue;

                    config.AddModuleSettingsConfigFiles(directory, reloadOnChange);
                    ModuleIdModuleDirMap.Add(moduleId, (directory, ModuleLocation.Demos));
                }
            }
        }

        private static (string?,string?, string?, string?) EnsureDirectories(IConfiguration configuration)
        {
            string rootPath = GetAppRootPath();
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                Console.WriteLine("No root path provided");
                return (null, null, null, null);
            }

            if (!Directory.Exists(rootPath))
            {
                Console.WriteLine($"The provided root path '{rootPath}' doesn't exist");
                return (null, null, null, null);
            }

            var moduleOptions                     = configuration.GetSection("ModuleOptions");
            string? runtimesDirPath               = moduleOptions["runtimesDirPath"];
            string? modulesDirPath                = moduleOptions["modulesDirPath"];
            string? preInstalledModulesDirPath    = moduleOptions["PreInstalledModulesDirPath"];
            string? demoModulesDirPath            = moduleOptions["DemoModulesDirPath"];
            string? externalModulesDirPath        = moduleOptions["ExternalModulesDirPath"];
            string? downloadedModulesDirPath      = moduleOptions["DownloadedModulePackagesDirPath"];
            string? downloadedModelsDirPath       = moduleOptions["DownloadedModelsPackagesDirPath"];
            string? moduleInstallerScriptsDirPath = moduleOptions["ModuleInstallerScriptsDirPath"];

            // make sure that all the require paths are defined
            if (string.IsNullOrWhiteSpace(runtimesDirPath))
            {
                Console.WriteLine("No runtime path provided");
                return (null, null, null, null);
            }

            if (string.IsNullOrWhiteSpace(modulesDirPath))
            {
                Console.WriteLine("No modules path provided");
                return (null, null, null, null);
            }

            if (string.IsNullOrWhiteSpace(downloadedModulesDirPath))
            {
                Console.WriteLine("No downloaded module Packages path provided");
                return (null, null, null, null);
            }

            if (string.IsNullOrWhiteSpace(downloadedModelsDirPath))
            {
                Console.WriteLine("No downloaded model Packages path provided");
                return (null, null, null, null);
            }

            if (string.IsNullOrWhiteSpace(moduleInstallerScriptsDirPath))
            {
                Console.WriteLine("No modules Installer path provided");
                return (null, null, null, null);
            }

            // get the full paths
            runtimesDirPath               = Text.FixSlashes(runtimesDirPath?.Replace("%ROOT_PATH%", rootPath));
            runtimesDirPath               = Path.GetFullPath(runtimesDirPath);
            downloadedModulesDirPath      = Text.FixSlashes(downloadedModulesDirPath?.Replace("%ROOT_PATH%", rootPath));
            downloadedModelsDirPath       = Text.FixSlashes(downloadedModelsDirPath?.Replace("%ROOT_PATH%", rootPath));
            moduleInstallerScriptsDirPath = Text.FixSlashes(moduleInstallerScriptsDirPath?.Replace("%ROOT_PATH%", rootPath));
            moduleInstallerScriptsDirPath = Path.GetFullPath(moduleInstallerScriptsDirPath);
            modulesDirPath                = Text.FixSlashes(modulesDirPath?.Replace("%ROOT_PATH%", rootPath));
            modulesDirPath                = Path.GetFullPath(modulesDirPath);
            preInstalledModulesDirPath    = Text.FixSlashes(preInstalledModulesDirPath?.Replace("%ROOT_PATH%", rootPath));
            preInstalledModulesDirPath    = Path.GetFullPath(preInstalledModulesDirPath);
            demoModulesDirPath            = Text.FixSlashes(demoModulesDirPath?.Replace("%ROOT_PATH%", rootPath));
            demoModulesDirPath            = Path.GetFullPath(demoModulesDirPath);
            externalModulesDirPath        = Text.FixSlashes(externalModulesDirPath?.Replace("%ROOT_PATH%", rootPath));
            if (!string.IsNullOrWhiteSpace(externalModulesDirPath))
                externalModulesDirPath    = Path.GetFullPath(externalModulesDirPath);

            // create the directories if the don't exist
            if (!Directory.Exists(runtimesDirPath))
            {
                Console.WriteLine($"Creating runtimes path '{runtimesDirPath}'");
                Directory.CreateDirectory(runtimesDirPath);
            }

            if (!Directory.Exists(downloadedModulesDirPath))
            {
                Console.WriteLine($"Creating downloaded modules package path '{downloadedModulesDirPath}'");
                Directory.CreateDirectory(downloadedModulesDirPath);
            }

            if (!Directory.Exists(downloadedModelsDirPath))
            {
                Console.WriteLine($"Creating downloaded models path '{downloadedModelsDirPath}'");
                Directory.CreateDirectory(downloadedModelsDirPath);
            }

            if (!Directory.Exists(moduleInstallerScriptsDirPath))
            {
                Console.WriteLine($"Creating modules installer path '{moduleInstallerScriptsDirPath}'");
                Directory.CreateDirectory(moduleInstallerScriptsDirPath);
            }

            if (!Directory.Exists(modulesDirPath))
            {
                Console.WriteLine($"Creating modules path '{modulesDirPath}'");
                Directory.CreateDirectory(modulesDirPath);
            }

            var srcPath = Path.Combine(rootPath, "src");

            // copy over the SDK if required.
            if (SystemInfo.IsDevelopmentCode && !srcPath.EqualsIgnoreCase(moduleInstallerScriptsDirPath))
            {
                Console.WriteLine("Copying SDK and Setup Scripts");

                File.Copy(Path.Combine(srcPath, "setup.bat"), Path.Combine(moduleInstallerScriptsDirPath, "setup.bat"), true);
                File.Copy(Path.Combine(srcPath, "setup.sh"),  Path.Combine(moduleInstallerScriptsDirPath, "setup.sh"), true);
                CopyDirectory(Path.Combine(srcPath, "SDK"),   Path.Combine(moduleInstallerScriptsDirPath, "SDK"), true);
            }

            return (modulesDirPath, preInstalledModulesDirPath, demoModulesDirPath, externalModulesDirPath);
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
        /// Adds the last-saved user configuration files to the configuration
        /// </summary>
        /// <param name="config"></param>
        /// <param name="applicationDataDir">The directory containing the persisted user data</param>
        /// <param name="runtimeEnv">The current runtime environment (production or development)</param>
        /// <param name="reloadOnChange">Whether to reload the config files if they change</param>
        private static void AddUserOverrideConfigurationFiles(IConfigurationBuilder config, 
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
                Console.WriteLine($"The provided application data directory path '{applicationDataDir}' doesn't exist. Creating ...");
                try
                {
                    Directory.CreateDirectory(applicationDataDir);
                }
                catch
                {
                    Console.WriteLine("Unable to create app data directory");
                    return;
                }
            }

            runtimeEnv = runtimeEnv?.ToLower();

            // The user-overridden settings for each module are combined into a single
            // modulesettings.json file
            ModuleConfigExtensions.RewriteOldUserModuleSettingsFile(applicationDataDir); // rewrite if needed
            string settingsFile = Path.Combine(applicationDataDir, Constants.ModuleSettingsFilename);
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);

            // ...but we have separate production and development files.
            if (runtimeEnv.EqualsIgnoreCase(Constants.Development))
            {
                ModuleConfigExtensions.RewriteOldUserModuleSettingsFile(applicationDataDir); // rewrite if needed
                settingsFile = Path.Combine(applicationDataDir, Constants.DevModuleSettingsFilename);
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: reloadOnChange);
            }

            // We store any updates to server (including mesh) settings in a separate file
            settingsFile = Path.Combine(applicationDataDir, Constants.ServerSettingsFilename);
            // If the file doesn't exist, create it with an empty object
            WriteEmptyConfigFileIfMissing(settingsFile);
            config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: true);

            if (runtimeEnv.EqualsIgnoreCase(Constants.Development))
            {
                settingsFile = Path.Combine(applicationDataDir, Constants.DevServerSettingsFilename);
                // If the file doesn't exist, create it with an empty object
                WriteEmptyConfigFileIfMissing(settingsFile);
                config.AddJsonFileSafe(settingsFile, optional: true, reloadOnChange: true);
            }
        }

        private static void WriteEmptyConfigFileIfMissing(string settingsFile)
        {
            if (File.Exists(settingsFile))
                return;

            try
            {
                File.WriteAllText(settingsFile, "{}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to create empty config file '{settingsFile}': {ex.Message}");
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
                                bool disableIPv6       = config.GetValue("ServerOptions:DisableIPv6", false);

                                _port = GetServerPort(hostbuilderContext);
                                bool foundPort = false;

                                var anyAddress = Socket.OSSupportsIPv6? IPAddress.IPv6Any : IPAddress.Any;
                                if (disableIPv6)
                                {
                                    anyAddress = IPAddress.Any;
                                    Console.WriteLine("Disabling IPv6 support");
                                }

                                // Listen on the port that the appsettings defines (we force the
                                // use of the default port. IsPortAvailable can sometimes be too
                                // conservative)
                                if (_port == Constants.DefaultPort || IsPortAvailable(_port))
                                {
                                    Console.WriteLine($"Server is listening on port {_port}");
                                    serverOptions.Listen(anyAddress, _port);
                                    foundPort = true;
                                }

                                // If we aren't listening to the default port (32168), then listen
                                // to it! (and don't bother asking if it's available. Just try it.)
                                if (_port != Constants.DefaultPort /* && IsPortAvailable(Constants.DefaultPort)*/)
                                {
                                    if (!foundPort)
                                        _port = Constants.DefaultPort;

                                    Console.WriteLine($"Server is listening on default port {_port}");
                                    serverOptions.Listen(anyAddress, Constants.DefaultPort);
                                    foundPort = true;
                                }

                                if (!disableLegacyPort)
                                {
                                    // Add some legacy ports. First macOS (port 5500)
                                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                    {
                                        if (_port != Constants.LegacyPortOsx &&
                                            IsPortAvailable(Constants.LegacyPortOsx))
                                        {
                                            if (!foundPort)
                                                _port = Constants.LegacyPortOsx;
                                        
                                            Console.WriteLine($"Server is also listening on legacy port {Constants.LegacyPortOsx}");
                                            serverOptions.Listen(anyAddress, Constants.LegacyPortOsx);
                                            foundPort = true;
                                        }
                                    }
                                    // Then everything else (port 5000)
                                    else
                                    {
                                        if (_port != Constants.LegacyPort &&
                                            IsPortAvailable(Constants.LegacyPort))
                                        {
                                            if (!foundPort)
                                                _port = Constants.LegacyPort;
                                        
                                            Console.WriteLine($"Server is also listening on legacy port {Constants.LegacyPort}");
                                            serverOptions.Listen(anyAddress, Constants.LegacyPort);
                                            foundPort = true;
                                        }
                                    }
                                }

                                if (!foundPort)
                                {
                                    _port = GetAvailablePort(anyAddress);
                                    serverOptions.Listen(anyAddress, _port);
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
                                            // Replace warning value from appsettings.json of "Red"
                                            // configuration.LogLevels[LogLevel.Error] = ConsoleColor.DarkRed;
                                        });
                            });

                    // Or if we want to do this manually...
                    // webBuilder.UseUrls($"http://localhost:{_port}/", $"https://localhost:{_sPort}/");
                });
        }

        /// <summary>
        /// Checks as to whether a given port on this machine is available for use.
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

            foreach (TcpConnectionInformation tcpInfo in tcpConnInfoArray)
            {
                if (tcpInfo.LocalEndPoint.Port == port)
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
        /// Opens the default browser on the given system with the given url. To be tested. If there
        /// are issues, see also https://stackoverflow.com/a/53570859
        /// </summary>
        /// <param name="url">The URL to open</param>
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