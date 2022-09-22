using System.Globalization;
using System;
using System.Linq;
#if Windows
using System.Management;
#endif
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CodeProject.AI.API.Server.Frontend
{
    /// <summary>
    /// A simple utillity class for querying GPU info. An interesting project is 
    /// https://github.com/clSharp/Cloo, which works on AMD, Intel and Nvidia. However, it only
    /// seems to report hardware / driver config, not usage.
    /// </summary>
    public class GPUInfo
    {
        /// <summary>
        /// Returns GPU info for the current system
        /// </summary>
        public static async ValueTask<string> GetGpuInfo()
        {
#if Windows
            var info = new StringBuilder();
            #pragma warning disable CA1416 // Validate platform compatibility
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                info.AppendLine("Video adapter info:");

                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                {
                    if (obj is not null)
                    {
                        string adapterRAM = "NA";
                        if (long.TryParse(obj["AdapterRAM"]?.ToString() ?? String.Empty, out long ram))
                            adapterRAM = FormatSizeBytes(ram, 0);

                        info.AppendLine($"Name               - {obj["Name"]}");
                        info.AppendLine($"Device ID          - {obj["DeviceID"]}");
                        info.AppendLine($"Adapter RAM        - {adapterRAM}");
                        info.AppendLine($"Adapter DAC Type   - {obj["AdapterDACType"]}");
                        // info.AppendLine($"Display Drivers - {obj["InstalledDisplayDrivers"]}");
                        info.AppendLine($"Driver Version     - {obj["DriverVersion"]}");
                        info.AppendLine($"Video Processor    - {obj["VideoProcessor"]}");
                        info.AppendLine($"Video Architecture - {Architecture(obj["VideoArchitecture"]?.ToString())}");
                        info.AppendLine($"Video Memory Type  - {MemoryType(obj["VideoMemoryType"]?.ToString())}");
                        info.AppendLine($"GPU 3D Usage       - {await Get3DGpuUsage()}");
                        info.AppendLine($"GPU RAM Usage      - {FormatSizeBytes((long)GetGpuMemoryUsage(), 2)}");
                    }
                }
            }
            #pragma warning restore CA1416 // Validate platform compatibility
            return info.ToString();
#else
            return await new ValueTask<string>(string.Empty);
#endif
        }

        // https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller
        private static string Architecture(string? typeId)
        {
            if (typeId == null)
                return "N/A";

            return typeId switch
            {
                "1" => "Other",
                "2" => "Unknown",
                "3" => "CGA",
                "4" => "EGA",
                "5" => "VGA",
                "6" => "SVGA",
                "7" => "MDA",
                "8" => "HGC",
                "9" => "MCGA",
                "10" => "8514A",
                "11" => "XGA",
                "12" => "Linear Frame Buffer",
                "160" => "PC-98",
                _ => $"Unlisted ({typeId})"
            };
        }

        private static string MemoryType(string? typeId)
        {
            if (typeId == null)
                return "N/A";

            return typeId switch
            {
                "1" => "Other",
                "2" => "Unknown",
                "3" => "VRAM",
                "4" => "DRAM",
                "5" => "SRAM",
                "6" => "WRAM",
                "7" => "EDO RAM",
                "8" => "Burst Synchronous DRAM",
                "9" => "Pipelined Burst SRAM",
                "10" => "CDRAM",
                "11" => "3DRAM",
                "12" => "SDRAM",
                "13" => "SGRAM",
                _ => $"Unlisted ({typeId})"
            };
        }

        private static List<PerformanceCounter> GetCounters(string category, string? instanceEnding,
                                                            string metricName)
        {
#pragma warning disable CA1416 // Validate platform compatibility
            var perfCategory = new PerformanceCounterCategory(category);
            var counterNames = perfCategory.GetInstanceNames();

            if (instanceEnding == null)
                return counterNames.SelectMany(counterName => perfCategory.GetCounters(counterName))
                                   .Where(counter => counter.CounterName.Equals(metricName))
                                   .ToList();

            return counterNames.Where(counterName => counterName.EndsWith(instanceEnding))
                               .SelectMany(counterName => perfCategory.GetCounters(counterName))
                               .Where(counter => counter.CounterName.Equals(metricName))
                               .ToList();

#pragma warning restore CA1416 // Validate platform compatibility
        }

        /// <summary>
        /// Gets the current GPU utilisation
        /// </summary>
        /// <returns>A float representing bytes</returns>
        public async static ValueTask<int> Get3DGpuUsage()
        {
#if Windows
#pragma warning disable CA1416 // Validate platform compatibility

            List<PerformanceCounter> utilization = GetCounters("GPU Engine", "engtype_3D", "Utilization Percentage");

            // See https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.performancecounter.nextvalue?view=dotnet-plat-ext-6.0#remarks
            // If the calculated value of a counter depends on two counter reads, the first read operation returns 0.0.
            // The recommended delay time between calls to the NextValue method is one second
            utilization.ForEach(x => x.NextValue());
            await Task.Delay(1000);

            var usage = utilization.Sum(x => x.NextValue());

            return (int)usage;

#pragma warning restore CA1416 // Validate platform compatibility
#else
            return await new ValueTask<int>(0);
#endif
        }

        /// <summary>
        /// Gets the amount of GPU memory currently in use
        /// </summary>
        /// <returns>A float representing bytes</returns>
        public static float GetGpuMemoryUsage()
        {
#if Windows
#pragma warning disable CA1416 // Validate platform compatibility

            List<PerformanceCounter> counters = GetCounters("GPU Process Memory", null, "Dedicated Usage");
            // gpuCounters.ForEach(x => x.NextValue());
            // Thread.Sleep(1000);

            return counters.Sum(x => x.NextValue());

#pragma warning restore CA1416 // Validate platform compatibility
#else
            return 0;
#endif
        }

        /// <summary>
        /// Format Size from bytes to a Kb, Mb or Gb string, where 1K = 1024 bytes.
        /// </summary>
        /// <param name="bytes">Number of bytes.</param>
        /// <param name="rounding">The number of significant decimal places (precision) in the
        /// return value.</param>
        /// <param name="itemAbbreviation">The item abbreviation (eg 'B' for bytes).</param>
        /// <param name="compact">If set to <c>true</c> then compact the string result (no spaces).
        /// </param>
        /// <returns>Returns formatted size.</returns>
        /// <example>
        /// +----------------------+
        /// | input  |   output    |
        /// -----------------------|
        /// |  1024  |    1 Kb     |
        /// |  225   |  225 B      |
        /// +----------------------+
        /// </example>
        private static string FormatSizeBytes(long bytes, int rounding, string itemAbbreviation = "B",
                                             bool compact = false)
        {
            string result;
            string format = "#,###." + new string('#', rounding);
            string spacer = compact ? string.Empty : " ";

            // We're trusting that the compiler will pre-compute the literals here.

            if (bytes < 1024 * 1024)            // less than a megaunit
            {
                if (bytes < 1024)               // less than a kilounit
                {
                    result = bytes.ToString("N0", CultureInfo.CurrentCulture) + spacer + itemAbbreviation;
                }
                else                            // more than a kilounit
                {
                    result = Math.Round(bytes / 1024.0,
                                        rounding).ToString(format, CultureInfo.CurrentCulture) +
                                        spacer + "K" + itemAbbreviation;
                }
            }
            else                                // greater than a megaunit
            {
                if (bytes > 1024 * 1024 * 1024) // greater than a gigaunit
                {
                    result = Math.Round(bytes / (1024.0 * 1024.0 * 1024.0),
                                        rounding).ToString(format, CultureInfo.CurrentCulture) +
                                        spacer + "G" + itemAbbreviation;
                }
                else                            // greater than a megaunit
                {
                    result = Math.Round(bytes / (1024.0 * 1024.0),
                                        rounding).ToString(format, CultureInfo.CurrentCulture) +
                                        spacer + "M" + itemAbbreviation; // megaunits
                }
            }

            return result;
        }
    }
}
