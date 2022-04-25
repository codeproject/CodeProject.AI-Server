using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Reflection;

namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The Application Entry Class.
    /// </summary>
    public class Program
    {
        static int _port = 5000;
        // static int _sPort = 5001;

        /// <summary>
        /// The Application Entry Point.
        /// </summary>
        /// <param name="args">The command line args.</param>
        public static async Task Main(string[] args)
        {
            // TODO: Pull these from the correct location
            const string company = "CodeProject";
            const string product = "SenseAI";

            var assembly           = Assembly.GetExecutingAssembly();
            var assemblyName       = assembly.GetName().Name ?? String.Empty;
            var servicePath        = assembly.Location.Remove(Assembly.GetExecutingAssembly().Location.Length - 4) + ".exe";
            var serviceName        = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                                   ?? assemblyName.Replace(".", " ");
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
            }

            string platform = "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                platform = "osx";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                platform = "linux";

            string programDataDir     = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationDataDir = $"{programDataDir}\\{company}\\{product}".Replace('\\', Path.DirectorySeparatorChar);
            if (platform == "osx")
                applicationDataDir = $"~/Library/Application Support/{company}/{product}";

            var inMemoryConfigData = new Dictionary<string, string> {
                { "ApplicationDataDir", applicationDataDir }
            };

            bool inVScode = (Environment.GetEnvironmentVariable("RUNNING_IN_VSCODE") ?? "") == "true";
            bool inDocker = (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "") == "true";

            if (inDocker)
                platform = "docker";  // which in our case implies that we are running in Linux

            IHost? host = CreateHostBuilder(args)
                       .ConfigureAppConfiguration((hostingContext, config) =>
                       {
                           string baseDir = AppContext.BaseDirectory;

                           // We've had issues where the default appsettings files not being loaded.
                           if (inVScode && platform != "windows")
                           {
                                config.AddJsonFile(Path.Combine(baseDir, "appsettings.json"),
                                                   optional: false, reloadOnChange: true);
                           }

                           config.AddJsonFile(Path.Combine(baseDir, $"appsettings.{platform}.json"),
                                              optional: true, reloadOnChange: true);

                           // ListEnvVariables(Environment.GetEnvironmentVariables());

                           string? aspNetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                           if (!string.IsNullOrWhiteSpace(aspNetEnv))
                           {
                                // We've had issues where the default appsettings files not being loaded.
                                if (inVScode && platform != "windows")
                                {
                                    config.AddJsonFile(Path.Combine(baseDir, $"appsettings.{aspNetEnv}.json"),
                                                       optional: true, reloadOnChange: true);
                                }
                                
                                config.AddJsonFile(Path.Combine(baseDir, $"appsettings.{platform}.{aspNetEnv}.json"),
                                                  optional: true, reloadOnChange: true);
                           }

                           config.AddInMemoryCollection(inMemoryConfigData);
                           config.AddJsonFile(Path.Combine(applicationDataDir, InstallConfig.InstallCfgFilename),
                                              reloadOnChange: true, optional: true);
                           config.AddJsonFile(Path.Combine(baseDir, VersionConfig.VersionCfgFilename), 
                                              reloadOnChange: true, optional: true);

                           // ListConfigSources(config.Sources);
                       })
                       .Build()
                       ;

            var logger = host.Services.GetService<ILogger<Program>>();

            Task? hostTask;
            hostTask = host.RunAsync();

#if DEBUG
            try
            {
                OpenBrowser($"http://localhost:{_port}/");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unable to open Dashboard on startup.");
            }
#endif
            try
            {
                await hostTask;
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
                                           
                                // Add a self-signed certificate to enable HTTPS locally
                                // serverOptions.Listen(IPAddress.Loopback, _sPort,
                                //    listenOptions => {
                                //    {
                                //        listenOptions.UseHttps("testCert.pfx", "testPassword");
                                //    });
                            })
                            .UseStartup<Startup>();

                            // Keep things clean and simple for now
                            webBuilder.ConfigureLogging(logging =>
                            {
                                logging.ClearProviders()
                                       .AddFilter("Microsoft", LogLevel.Warning)
                                       .AddFilter("System", LogLevel.Warning)
                                       .AddConsole();
                            });

                    // Or if we want to do this manually...
                    // webBuilder.UseUrls($"http://localhost:{_port}/", $"https://localhost:{_sPort}/");
                });
        }

        private static int GetServerPort(WebHostBuilderContext hostbuilderContext)
        {
            IConfiguration config = hostbuilderContext.Configuration;
            int port = config.GetValue<int>("PORT", -1);

            if (port < 0)
                port = config.GetValue<int>("FrontEndOptions:BackendEnvironmentVariables:PORT", -1);

            if (port < 0)
            {
                string urls = config.GetValue<string>("urls");
                if (!string.IsNullOrWhiteSpace(urls))
                {
                    if (!int.TryParse(urls.Split(':').Last().Trim('/'), out port))
                        port = _port;

                    config["PORT"] = port.ToString();
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


