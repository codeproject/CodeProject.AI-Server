
using System.Management;

namespace CodeProject.AI.AnalysisLayer.SDK
{
    public class Hardware
    {
        /// <summary>
        /// Gets or sets the execution provider.
        /// </summary>
        public string ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the hardware ID.
        /// </summary>
        public string HardwareId { get; set; } = "CPU";

        /// <summary>
        /// Queries the system for the hardware capabilities.
        /// </summary>
        /// <remarks>This is naive at best, and doesn't reflect what a given software package might
        /// actually be able to utilise, nor does it reflect what's been enabled in code, or even
        /// the choices a given piece of code has made.</remarks>
        public void SniffHardwareInfo()
        {
#pragma warning disable CA1416 // Validate platform compatibility
#if Windows
            var searcher = new ManagementObjectSearcher("select * from Win32_VideoController ");

            string description = string.Empty;

            foreach (ManagementObject device in searcher.Get())
            {
                if (device["CurrentBitsPerPixel"] != null && device["CurrentHorizontalResolution"] != null)
                {
                    if ((string)device["DeviceID"] == "VideoController2")
                    {
                        HardwareId  = device["DeviceID"].ToString() ?? "CPU";
                        description = device["Description"].ToString() ?? string.Empty;
                        Console.WriteLine(HardwareId);
                        Console.WriteLine(description);
                    }
                }
            }
#endif
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }
}