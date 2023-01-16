using System.Management;

namespace CodeProject.AI.SDK
{
    public class Hardware
    {
        /// <summary>
        /// Gets or sets the execution provider (eg CUDA, OpenVino etc).
        /// </summary>
        public string ExecutionProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the device ID.
        /// </summary>
        public string DeviceId { get; set; } = "CPU";

        /// <summary>
        /// Queries the system for the hardware capabilities.
        /// </summary>
        /// <remarks>This is naive at best, and doesn't reflect what a given software package might
        /// actually be able to utilise, nor does it reflect what's been enabled in code, or even
        /// the choices a given piece of code has made. I'm not selling this, am I?</remarks>
        public void SniffHardwareInfo()
        {
#pragma warning disable CA1416 // Validate platform compatibility
#if Windows
            try
            {
                var searcher = new ManagementObjectSearcher("select * from Win32_VideoController ");

                string description = string.Empty;

                foreach (ManagementObject device in searcher.Get())
                {
                    if (device["CurrentBitsPerPixel"] != null && device["CurrentHorizontalResolution"] != null)
                    {
                        if ((string)device["DeviceID"] == "VideoController2")
                        {
                            DeviceId = device["DeviceID"].ToString() ?? "CPU";
                            description = device["Description"].ToString() ?? string.Empty;
                            Console.WriteLine(DeviceId);
                            Console.WriteLine(description);
                        }
                    }
                }
            }
            catch 
            {
            }
#endif
#pragma warning restore CA1416 // Validate platform compatibility
        }
    }
}