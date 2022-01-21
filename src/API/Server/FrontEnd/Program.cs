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


namespace CodeProject.SenseAI.API.Server.Frontend
{
    /// <summary>
    /// The Application Entry Class.
    /// </summary>
    public class Program
    {
        static int _port  = 5000;
        // static int _sPort = 5001;

        /// <summary>
        /// The Application Entry Point.
        /// </summary>
        /// <param name="args">The command line args.</param>
        public static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args)
                       .ConfigureAppConfiguration((hostingContext, config) =>
                       {
                           config.AddJsonFile(InstallConfig.InstallCfgFilename, reloadOnChange: true, optional: true);
                           config.AddJsonFile(VersionConfig.VersionCfgFilename, reloadOnChange: true, optional: true);
                       })
                       .Build();
            var hostTask = host.RunAsync();

            OpenBrowser($"http://localhost:{_port}/");

            await hostTask;
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

            DirectoryInfo currentDir = new(AppContext.BaseDirectory);
            while (currentDir != null && currentDir.Name.ToLower() != "frontend")
            {
                if (currentDir.Parent == null)
                    throw new DirectoryNotFoundException("Unable to find the FrontEnd parent directory");

                currentDir = currentDir.Parent;
            }

            return Host.CreateDefaultBuilder(args)

                        // configure for running as a Windows Service or LinuxSystemmd in addition
                        // as an executable in either OS.
                       .UseWindowsService()
                       .UseSystemd()
                       .ConfigureWebHostDefaults(webBuilder =>
                       {
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
                                       .UseContentRoot(currentDir!.FullName) // for static files
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
    }
}


