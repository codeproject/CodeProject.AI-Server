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

            string programDataDir     = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string applicationDataDir = $"{programDataDir}\\{company}\\{product}".Replace('\\', Path.DirectorySeparatorChar);

            var inMemoryConfigData = new Dictionary<string, string> {
                { "ApplicationDataDir", applicationDataDir }
            };

            IHost? host = CreateHostBuilder(args)
                       .ConfigureAppConfiguration((hostingContext, config) =>
                       {
                            // Windows loads this stuff by default, and exceptions are only for osx/linux
                            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                               config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: true);
                               if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                   config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.osx.json"), optional: false, reloadOnChange: true);
                               else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                   config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.linux.json"), optional: false, reloadOnChange: true);
#if DEBUG
                               config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.development.json"), optional: false, reloadOnChange: true);
                               if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                                   config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.osx.development.json"), optional: false, reloadOnChange: true);
                               else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                   config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.linux.development.json"), optional: false, reloadOnChange: true);
#endif
                            }

                            config.AddInMemoryCollection(inMemoryConfigData);
                            config.AddJsonFile(Path.Combine(applicationDataDir, InstallConfig.InstallCfgFilename), reloadOnChange: true, optional: true);
                            config.AddJsonFile(Path.Combine(AppContext.BaseDirectory, VersionConfig.VersionCfgFilename), reloadOnChange: true, optional: true);
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
                Console.WriteLine($"\n\nUnable to start the server due to {ex.Message}.\nCheck that another instance is not running on the same port.");
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
            // Some faffing around: We want to launch a webpage, and in our infinite cleverness we
            // want the page to load external files (CSS, JS). For this we need to set the 
            // ContentRoot for the server. Easy, except we have to hunt for the correct directory.
            // ASSUMPTION: The content root will always be the /src/API/FrontEnd directory. When
            // this server is launched from an install, that install is clean and this exe is in
            // the /FrontEnd directory. When launched from within the dev environment, the exe is
            // buried deep down the labyrinth. Grab a torch and hunt.

            /* Currently not needed
            DirectoryInfo currentDir = new(AppContext.BaseDirectory);
            while (currentDir != null && currentDir.Name.ToLower() != "frontend" && currentDir.Name.ToLower() != "server")
            {
                if (currentDir.Parent == null)
                    throw new DirectoryNotFoundException("Unable to find the FrontEnd parent directory");
               
                currentDir = currentDir.Parent;
            }
            */

            return Host.CreateDefaultBuilder(args)

                        // configure for running as a Windows Service or LinuxSystemmd in addition
                        // as an executable in either OS.
                       .UseWindowsService()
                       .UseSystemd()
                       .ConfigureWebHostDefaults(webBuilder =>
                       {
                           webBuilder.UseShutdownTimeout(TimeSpan.FromMinutes(2));
                           webBuilder.ConfigureKestrel((hostbuilderContext, serverOptions) =>
                                       {
                                           _port = GetServerPort(hostbuilderContext);
                                           serverOptions.Listen(IPAddress.Any, _port);
                                           
                                           // Add a self-signed certificate to enable HTTPS locally
                                           // serverOptions.Listen(IPAddress.Loopback, _sPort,
                                           //    listenOptions => {
                                           //    {
                                           //        listenOptions.UseHttps("testCert.pfx", "testPassword");
                                           //    });
                                       })
                                       // .UseContentRoot(currentDir!.FullName) // for static files
                                       .UseStartup<Startup>();

                    // Or if we want to do this manually...
                    // webBuilder.UseUrls($"http://localhost:{_port}/", $"https://localhost:{_sPort}/");
                });
        }

        private static int GetServerPort(WebHostBuilderContext hostbuilderContext)
        {
            IConfiguration config = hostbuilderContext.Configuration;
            int port = config.GetValue<int>("PORT", -1);
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
        /// Opens the default browser on the given system with the given url.
        /// To be tested, and if there are issues, see also https://stackoverflow.com/a/53570859
        /// </summary>
        /// <param name="url"></param>
        public static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); // Works ok on windows
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);  // Works ok on linux
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url); // Not tested
                }
                else
                {
                    // System not supported
                }
            }
            catch
            { 
            }
        }
    }
}


