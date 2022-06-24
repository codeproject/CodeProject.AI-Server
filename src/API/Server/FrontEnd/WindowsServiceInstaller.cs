using System.Diagnostics;
using System.Runtime.InteropServices;

// based on https://github.com/microsoft/installer-project-samples/blob/main/NET_Core/WindowsService/NetCoreWinService/Program.cs
// should be extended to handle the equivalent in Linux and OSX.
namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// Allows Windows Services to be installed and uninstalled
    /// </summary>
    public static class WindowsServiceInstaller
    {
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
                RunSc($"stop \"{serviceName}\"");
                RunSc($"delete \"{serviceName}\"");
            }
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
                FileName = "sc.exe",
                Arguments = args,
            };

            var process = Process.Start(psi);
            process?.WaitForExit();
        }
    }


}
