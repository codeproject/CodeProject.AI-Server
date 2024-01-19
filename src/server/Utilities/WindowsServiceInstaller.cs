using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;

// based on https://github.com/microsoft/installer-project-samples/blob/main/NET_Core/WindowsService/NetCoreWinService/Program.cs
// should be extended to handle the equivalent in Linux and OSX.
namespace CodeProject.AI.Server
{
    /// <summary>
    /// Allows Windows Services to be installed and uninstalled
    /// </summary>
    public static class WindowsServiceInstaller
    {
        /// <summary>
        /// Max time to wait for Service to shut down.
        /// </summary>
        static readonly TimeSpan _stopTimeout = new TimeSpan(0, 5, 0);
        static readonly TimeSpan _startTimeout = new TimeSpan(0, 0, 30);

        /// <summary>
        /// Installs a Windows Service
        /// </summary>
        /// <param name="binpath">The path to the executable.</param>
        /// <param name="serviceName">The name to be call the service..</param>
        /// <param name="description">The description of the service.</param>
        public static void Install(string binpath, string serviceName, string description)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunSc($"create \"{serviceName}\" binpath= \"{binpath}\"  start= auto");
                RunSc($"description \"{serviceName}\" \"{description}\"");
                RunSc($"failure \"{serviceName}\"  reset= 30 actions= restart/5000/restart/5000/restart/5000");
                RunSc($"start \"{serviceName}\"");
            }
        }

        /// <summary>
        /// Uninstalls a Windows Service.
        /// </summary>
        /// <param name="serviceName">The Service Name.</param>
        public static void Uninstall(string serviceName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Stop(serviceName);

                Console.WriteLine($"Uninstalling the '{serviceName}' Windows Service");
                RunSc($"delete \"{serviceName}\"");
            }
        }

        /// <summary>
        /// Stops the named Windows Service.
        /// </summary>
        /// <param name="serviceName">The name of the Windows Service</param>
        public static void Stop(string serviceName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"Stopping the '{serviceName}' Windows Service");
                try
                {
                    var (found, sc) = FindService(serviceName);
                    if (found)
                    {
                        if (sc!.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, _stopTimeout);
                        }
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // TODO: log unable to stop the service
                }
                catch (Exception)
                {
                    // TODO: something else happened, log it
                    // The service might not be installed, which is ok.
                }
            }
        }

        /// <summary>
        /// Start the named Windows Service.
        /// </summary>
        /// <param name="serviceName">The name of the Windows Service</param>
        public static void Start(string serviceName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"Starting the '{serviceName}' Windows Service");
                try
                {
                    var (found, sc) = FindService(serviceName);
                    if (found)
                    {
                        if (sc!.Status != ServiceControllerStatus.Running)
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, _startTimeout);
                        }
                    }
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    // TODO: log unable to stop the service
                }
                catch (Exception)
                {
                    // TODO: something else happened, log it
                    // The service might not be installed, which is ok.
                }
            }
        }

        /// <summary>
        /// Finds a Service by name.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <returns>The related ServiceController, or null if not found.</returns>
        private static (bool found, ServiceController? service) FindService(string serviceName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var services = ServiceController.GetServices();
                foreach (var service in services)
                    if (service.ServiceName == serviceName)
                        return (true, service);
            }

            return (false, null);
        }
        /// <summary>
        /// Executes the sc command.
        /// </summary>
        /// <param name="args">The command arguments</param>
        private static void RunSc(string args)
        {
            var psi = new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName    = "sc.exe",
                Arguments   = args,
            };

            var process = Process.Start(psi);
            process?.WaitForExit();
        }
    }
}
