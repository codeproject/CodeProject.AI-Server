using System.Globalization;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

#if Windows
using System.Management;
#endif

#pragma warning disable CA1416 // Validate platform compatibility
namespace CodeProject.AI.SDK.Common
{
    public class GpuInfo
    {
        public string? GpuName { get; set; }
        public string? DriverVersion { get; set; }
        public int GpuUtilization { get; set; }
        public long MemoryUsed { get; set; }
        public long TotalMemory { get; set; }
    }

    public class NvidiaInfo : GpuInfo
    {
        public string? CudaVersion { get; set; }
        public string? ComputeCapacity { get; internal set; }
    }

    public enum RuntimeEnvironment
    {
        /// <summary>
        /// Unknown runtime environment
        /// </summary>
        Unknown,

        /// <summary>
        /// Running in production mode
        /// </summary>
        Development,

        /// <summary>
        /// Running in staging mode
        /// </summary>
        Staging,

        /// <summary>
        /// Running in production mode
        /// </summary>
        Production
    }

    public enum ExecutionEnvironment
    {
        /// <summary>
        /// Unknown execution environment
        /// </summary>
        Unknown,

        /// <summary>
        /// Running in Docker
        /// </summary>
        Docker,

        /// <summary>
        /// Running in Visual Studio Code
        /// </summary>
        VSCode,

        /// <summary>
        /// Running in Visual Studio
        /// </summary>
        VisualStudio,

        /// <summary>
        /// Running natively within the host OS
        /// </summary>
        Native
    }

    /// <summary>
    /// A simple utillity class for querying GPU info. An interesting project is 
    /// https://github.com/clSharp/Cloo, which works on AMD, Intel and Nvidia. However, it only
    /// seems to report hardware / driver config, not usage.
    /// Another interesting writeup is at https://www.cyberciti.biz/faq/howto-find-linux-vga-video-card-ram/
    /// </summary>
    public class SystemInfo
    {
        private static bool? _isDevelopment;
        private static bool? _hasNvidiaCard = null;

        /// <summary>
        /// Whether or not this system contains an Nvidia card. If the value is
        /// null it means we've not been able to determine.
        /// </summary>
        public static bool? HasNvidiaGPU => _hasNvidiaCard; 

        /// <summary>
        /// Gets a value indicating whether we are running development code.
        /// </summary>
        public static bool IsDevelopment
        {
            get
            {
                if (_isDevelopment is not null)
                    return (bool)_isDevelopment;

                _isDevelopment = false;

                // 1. Scoot up the tree to check for build folders
                DirectoryInfo? info = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (info != null)
                {
                    if (info.Name.EqualsIgnoreCase("debug") || info.Name.EqualsIgnoreCase("release"))
                    {
                        _isDevelopment = true;
                        break;
                    }

                    info = info.Parent;
                }

                return (bool)_isDevelopment;
            }
        }

        /// <summary>
        /// Gets a value indicating the current runtime environment, meaning whether it's running
        /// as production, stage or development.
        /// </summary>
        public static RuntimeEnvironment RuntimeEnvironment
        {
            get
            {
                if (IsDevelopment ||
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").EqualsIgnoreCase("true"))
                    return RuntimeEnvironment.Development;

                return RuntimeEnvironment.Production;
            }
        }

        /// <summary>
        /// Gets a value indicating the current execution environment, meaning whether it's running
        /// in Docker, in an IDE, or running native on an OS.
        /// </summary>
        public static ExecutionEnvironment ExecutionEnvironment
        {
            get
            {
                // Another way to test for docker is to test if /.dockerenv exists, or to check if 
                // the term "docker" exists inside the file /proc/self/cgroup. For now we just define
                // a variable in the docker image itself.
                if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER").EqualsIgnoreCase("true"))
                    return ExecutionEnvironment.Docker;

                if (Environment.GetEnvironmentVariable("RUNNING_IN_VSCODE").EqualsIgnoreCase("true"))
                    return ExecutionEnvironment.VSCode;

                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VisualStudioVersion")) ||
                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VSVersion")))
                    return ExecutionEnvironment.VisualStudio;

                return ExecutionEnvironment.Native;
            }
        }

        /// <summary>
        /// Gets a value indicating if we're running in development or production build
        /// </summary>
        public static string BuildConfig
        {
            get
            {
#if DEBUG
                return "Debug";
#else
                return "Release";
#endif
            }
        }

        /// <summary>
        /// Gets the current platform name. This will be OS[-architecture]. eg Windows, macOS,
        /// Linux-Arm64.  Note that x64 won't have a suffix.
        /// </summary>
        public static string Platform
        {
            get
            {
                // RuntimeInformation.GetPlatform() or RuntimeInformation.Platform would have been
                // too easy.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return Architecture == "Arm64" ? "Windows-Arm64" : "Windows";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return Architecture == "Arm64" ? "macOS-Arm64" : "macOS";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return Architecture == "Arm64" ? "Linux-Arm64" : "Linux";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                    return "FreeBSD";

                return "Windows"; // Gotta be something...
            }
        }

        /// <summary>
        /// Gets the current Architecture. Supported architects are x64 and Arm64 only.
        /// </summary>
        public static string Architecture
        {
            get
            {
                if (RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64)
                    return "x64";

                if (RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                    return "Arm64";

                // Not supported, but supplied                
                if (RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm
#if NET7_0_OR_GREATER
                    || RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Armv6
#endif
                    )
                    return "Arm";

                // if (RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Wasm)
                //    return "Wasm";

                return "";
            }
        }

        /// <summary>
        /// Gets the current platform name. This will be OS[-architecture]. eg Windows, macOS,
        /// Linux-arm64. Note that x86_64 won't have a suffix.
        /// </summary>
        public static string OperatingSystem
        {
            get
            {
                // RuntimeInformation.GetOSPlatform() or RuntimeInformation.OSPlatform would have
                // been too easy.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return "Windows";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return "macOS";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return "Linux";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
                    return "FreeBSD";

                return "Windows"; // Gotta be something...
            }
        }

        /// <summary>
        /// Returns the Operating System version, corrected for Windows 11
        /// </summary>
        public static string OperatingSystemDescription
        {
            get
            {
                // See https://github.com/getsentry/sentry-dotnet/issues/1484. C'mon guys: technically
                // the version may be 10.x, but stick to the branding that the rest of the world understands.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    Environment.OSVersion.Version.Major >= 10 && 
                    Environment.OSVersion.Version.Build >= 22000)
                    return RuntimeInformation.OSDescription.Replace("Windows 10.", "Windows 11 version 10.");

                return RuntimeInformation.OSDescription;
            }
        }

        static SystemInfo()
        {
#if Windows
            _hasNvidiaCard = false;
            using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                IEnumerable<ManagementObject> managementObjects = searcher.Get().Cast<ManagementObject>();
                foreach (ManagementObject obj in managementObjects)
                {
                    string? name = obj?["Name"]?.ToString();
                    if (name is not null && name.ContainsIgnoreCase("Nvidia"))
                    {
                        _hasNvidiaCard = true;
                        break;
                    }
                }
            }
#endif
        }

        /// <summary>
        /// Returns basic system info
        /// </summary>
        /// <returns>A string object</returns>
        public static string GetSystemInfo()
        {       
            var info = new StringBuilder();

            info.AppendLine($"Operating System: {OperatingSystem} ({OperatingSystemDescription})");
            info.AppendLine($"Architecture:     {Architecture}");
            info.AppendLine($"Target:           {Platform}");
            info.AppendLine($"BuildConfig:      {BuildConfig}");
            info.AppendLine($"Execution Env:    {ExecutionEnvironment}");
            info.AppendLine($"Runtime Env:      {RuntimeEnvironment}");

            return info.ToString().Trim();
        }

        /// <summary>
        /// Returns GPU info for the current system
        /// </summary>
        public static async ValueTask<string> GetVideoAdapterInfo()
        {
#if Windows
            var info = new StringBuilder();

            info.AppendLine("System GPU info:");
            int gpu3DUsage   = await Get3DGpuUsage();
            long gpuMemUsage = GetGpuMemoryUsage();
            info.AppendLine($"  GPU 3D Usage       {gpu3DUsage}%");
            info.AppendLine($"  GPU RAM Usage      {FormatSizeBytes(gpuMemUsage, 2)}");
            info.AppendLine();

            try
            {
                using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                {
                    info.AppendLine("Video adapter info:");

                    IEnumerable<ManagementObject> managementObjects = searcher.Get().Cast<ManagementObject>();
                    foreach (ManagementObject obj in managementObjects)
                    {
                        if (obj is not null)
                        {
                            string name       = obj["Name"]?.ToString() ?? "Unknown Video controller";

                            string adapterRAM = "NA";
                            if (long.TryParse(obj["AdapterRAM"]?.ToString() ?? String.Empty, out long ram))
                                adapterRAM = FormatSizeBytes(ram, 0);

                            info.AppendLine($"{name}:");
                            info.AppendLine($"  Device ID          {obj["DeviceID"]}");
                            info.AppendLine($"  Adapter RAM        {adapterRAM}");
                            info.AppendLine($"  Adapter DAC Type   {obj["AdapterDACType"]}");
                            // info.AppendLine($"Display Drivers {obj["InstalledDisplayDrivers"]}");
                            info.AppendLine($"  Driver Version     {obj["DriverVersion"]}");
                            info.AppendLine($"  Video Processor    {obj["VideoProcessor"]}");
                            info.AppendLine($"  Video Architecture {VideoArchitecture(obj["VideoArchitecture"]?.ToString())}");
                            info.AppendLine($"  Video Memory Type  {MemoryType(obj["VideoMemoryType"]?.ToString())}");
                        }
                    }
                }
            }
            catch
            {
            }

            return await new ValueTask<string>(info.ToString().Trim());
#else
            return await new ValueTask<string>(string.Empty);
#endif
        }

        /// <summary>
        /// Gets the current GPU utilisation as a %
        /// </summary>
        /// <returns>A float representing bytes</returns>
        public async static ValueTask<int> GetCpuUsage()
        {
            long usage = 0;

            try
            {
#if Windows
                /* This requires Admin rights to run
                long start_time = DateTime.Now.Ticks;
                var processes = Process.GetProcesses();
                long totalTimeUsedStart = processes.Sum(p => p.TotalProcessorTime.Ticks);

                await Task.Delay(1000);

                long endTime = DateTime.Now.Ticks;
                processes = Process.GetProcesses();
                long totalTimeUsedEnd = processes.Sum(p => p.TotalProcessorTime.Ticks);

                usage = 100 * (totalTimeUsedEnd - totalTimeUsedStart) /
                             (Environment.ProcessorCount * (endTime - start_time));
                */

                List<PerformanceCounter> utilization = GetCounters("Processor",
                                                                   "% Processor Time",
                                                                   "_Total");
                utilization.ForEach(x => x.NextValue());
                await Task.Delay(1000);
                usage = (int)utilization.Sum(x => x.NextValue());
#elif Linux
                var output = string.Empty;

                await Task.Run(() => {
                    var info = new ProcessStartInfo("top -b -n 1");
                    info.FileName  = "/bin/bash";
                    info.Arguments = "-c \"top -b -n 1\"";
                    info.RedirectStandardOutput = true;

                    using (var process = Process.Start(info))
                    {                
                        output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                        // Console.WriteLine(output);
                    }
                });

                // Output is in the form:
                // top - 08:38:12 up  1:20,  0 users,  load average: 0.00, 0.00, 0.00
                // Tasks:   5 total,   1 running,   4 sleeping,   0 stopped,   0 zombie
                // %Cpu(s):  0.0 us,  0.0 sy,  0.0 ni, ... <-- this line, sum of values 1-3
 
                var lines = output.Split("\n");

                string pattern = @"(?<userTime>[\d.]+)\s*us,\s*(?<systemTime>[\d.]+)\s*sy,\s*(?<niceTime>[\d.]+)\s*ni";
                Match match    = Regex.Match(lines[2], pattern, RegexOptions.ExplicitCapture);
                var userTime   = match.Groups["userTime"].Value;
                var systemTime = match.Groups["systemTime"].Value;
                var niceTime   = match.Groups["niceTime"].Value;

                usage = (int)(float.Parse(userTime) + float.Parse(systemTime) + float.Parse(niceTime));
#elif macOS
                var output = string.Empty;

                await Task.Run(() => {
                    var info = new ProcessStartInfo("top -l 2 | grep -E '^CPU'");
                    info.FileName  = "/bin/bash";
                    info.Arguments = "-c \"top -l 1 | grep -E '^CPU'\"";
                    info.RedirectStandardOutput = true;

                    using (var process = Process.Start(info))
                    {                
                        output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                        // Console.WriteLine(output);
                    }
                });

                // Output is in the form:
                // CPU usage: 12.33% user, 13.63% sys, 74.2% idle 
                string pattern = @"CPU usage:\s+(?<userTime>[\d.]+)%\s+user,\s*(?<systemTime>[\d.]+)%\s*sys,\s*(?<idleTime>[\d.]+)%\s*idle";
                Match match    = Regex.Match(output, pattern, RegexOptions.ExplicitCapture);
                var userTime   = match.Groups["userTime"].Value;
                var systemTime = match.Groups["systemTime"].Value;

                usage = (int)(float.Parse(userTime) + float.Parse(systemTime));
#else
                await Task.Delay(0);
#endif
            }
            catch (Exception /*e*/)
            {
            }

            return (int) usage;
        }

        /// <summary>
        /// Gets the amount of System memory currently in use
        /// </summary>
        /// <returns>A long representing bytes</returns>
        public static long GetSystemMemoryUsage()
        {
#if Windows
            var processes = Process.GetProcesses();
            return processes.Sum(p => p.WorkingSet64);
#elif Linux
            var info = new ProcessStartInfo("free -b");
            info.FileName = "/bin/bash";
            info.Arguments = "-c \"free -b\"";
            info.RedirectStandardOutput = true;

            var output = string.Empty;
            using(var process = Process.Start(info))
            {                
                output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                // Console.WriteLine(output);
            }

            // Output is in the form:
            //       total used free
            // Mem:    XXX  YYY  ZZZ <- We want tokens 1 - 3 from this line
            // Swap:   xxx  yyy  zzz
 
            var lines = output.Split("\n");
            var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);
    
            long totalMemory = long.Parse(memory[1]);
            long memoryUsed  = long.Parse(memory[2]);
            long memoryFree  = long.Parse(memory[3]);
 
            return memoryUsed;
#elif macOS
            // ps -caxm -orss= | awk '{ sum += $1 } END { print sum/1024 }' 
            var info = new ProcessStartInfo("ps -caxm -orss= | awk '{ sum += $1 } END { print sum * 1024 }'");
            info.FileName = "/bin/bash";
            info.Arguments = "-c \"ps -caxm -orss= | awk '{ sum += $1 } END { print sum * 1024 }'\"";
            info.RedirectStandardOutput = true;

            var output = string.Empty;
            using(var process = Process.Start(info))
            {                
                output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                // Console.WriteLine(output);
            }
 
            long memoryUsed = (long)float.Parse(output.Trim());
            return memoryUsed;
#else
            return 0;
#endif
        }

        /// <summary>
        /// Gets the current GPU utilisation as a %
        /// </summary>
        /// <returns>An int representing bytes</returns>
        public async static ValueTask<int> Get3DGpuUsage()
        {
            NvidiaInfo? gpuInfo = ParseNvidiaSmi();
            if (gpuInfo is not null)
                return gpuInfo.GpuUtilization;

#if Windows
            int usage = 0;
            try
            {
                List<PerformanceCounter> utilization = GetCounters("GPU Engine",
                                                                   "Utilization Percentage",
                                                                   "engtype_3D");

                // See https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.performancecounter.nextvalue?view=dotnet-plat-ext-6.0#remarks
                // If the calculated value of a counter depends on two counter reads, the first
                // read operation returns 0.0. The recommended delay time between calls to the
                // NextValue method is one second
                utilization.ForEach(x => x.NextValue());
                await Task.Delay(1000);

                usage = (int)utilization.Sum(x => x.NextValue());
            }
            catch
            {
            }

            return usage;
#else
            return await new ValueTask<int>(0);
#endif
        }

        /// <summary>
        /// Gets the amount of GPU memory currently in use
        /// </summary>
        /// <returns>A long representing bytes</returns>
        public static long GetGpuMemoryUsage()
        {
            NvidiaInfo? gpuInfo = ParseNvidiaSmi();
            if (gpuInfo is not null)
                return gpuInfo.MemoryUsed;

            try
            {
#if Windows
                List<PerformanceCounter> counters = GetCounters("GPU Process Memory",
                                                                "Dedicated Usage", null);
                // gpuCounters.ForEach(x => x.NextValue());
                // Thread.Sleep(1000);

                return counters.Sum(x => (long)x.NextValue());
#else
                return 0;
#endif
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns information on the first GPU found (TODO: Extend to multiple GPUs)
        /// </summary>
        /// <returns></returns>
        public static NvidiaInfo? ParseNvidiaSmi()
        {
            if (SystemInfo.HasNvidiaGPU == false)
                return null;
                
            // TODO: Cache this value once a second

            try
            {
                var info = new ProcessStartInfo("nvidia-smi", "--query-gpu=count,name,driver_version,memory.total,memory.free,utilization.gpu,compute_cap --format=csv,noheader");
                info.RedirectStandardOutput = true;

                string output = string.Empty;
                using(var process = Process.Start(info))
                {                
                    output = process?.StandardOutput?.ReadToEnd() ?? string.Empty;
                    // Console.WriteLine(output);
                }

                string pattern      = @"\d+,\s+(?<gpuname>.+?),\s+(?<driver>[\d.]+),\s+(?<memused>\d+)\s*MiB,\s+(?<memtotal>\d+)\s*MiB,\s+(?<gpuUtil>\d+)\s*%,\s+(?<computecap>.*?)\s";
                Match match         = Regex.Match(output, pattern, RegexOptions.ExplicitCapture);
                var gpuName         = match.Groups["gpuname"].Value;
                var driverVersion   = match.Groups["driver"].Value;
                var memUsedStr      = match.Groups["memused"].Value;
                var memTotalStr     = match.Groups["memtotal"].Value;
                var gpuUtilStr      = match.Groups["gpuUtil"].Value;
                var computeCapacity = match.Groups["computecap"].Value;

                long.TryParse(memUsedStr, out long memoryUsedMiB);
                long.TryParse(memTotalStr, out long totalMemoryMiB);
                int.TryParse(gpuUtilStr,  out int gpuUtilPercent);

                // Get CUDA info        
                info = new ProcessStartInfo("nvidia-smi");
                info.RedirectStandardOutput = true;

                output = string.Empty;
                using(var process = Process.Start(info))
                {                
                    output = process?.StandardOutput.ReadToEnd() ?? string.Empty;
                    // Console.WriteLine(output);
                }

                /* Output is in the form:
                    Thu Dec  8 08:45:30 2022
                    +-----------------------------------------------------------------------------+
                    | NVIDIA-SMI 512.96       Driver Version: 512.96       CUDA Version: 11.6     |
                    |-------------------------------+----------------------+----------------------+
                */
 
                pattern         = @"Driver Version:\s+(?<driver>[\d.]+)\s*CUDA Version:\s+(?<cuda>[\d.]+)";
                match           = Regex.Match(output, pattern, RegexOptions.ExplicitCapture);
                var cudaVersion = match.Groups["cuda"].Value;
 
                return new NvidiaInfo
                {
                    GpuName         = gpuName,
                    DriverVersion   = driverVersion,
                    CudaVersion     = cudaVersion,
                    GpuUtilization  = gpuUtilPercent,
                    MemoryUsed      = memoryUsedMiB * 1024 * 1024,
                    TotalMemory     = totalMemoryMiB * 1024 * 1024,
                    ComputeCapacity = computeCapacity,
                };
            }
            catch(Exception ex) 
            {
                _hasNvidiaCard = false;
                Debug.WriteLine(ex.ToString());
                return null;
            }
        }

        // https://docs.microsoft.com/en-us/windows/win32/cimwin32prov/win32-videocontroller
        private static string VideoArchitecture(string? typeId)
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

        private static List<PerformanceCounter> GetCounters(string category, string metricName,
                                                            string? instanceEnding = null)
        {
            var perfCategory = new PerformanceCounterCategory(category);
            var counterNames = perfCategory.GetInstanceNames();

            if (instanceEnding == null)
            {
                return counterNames.SelectMany(counterName => perfCategory.GetCounters(counterName))
                                   .Where(counter => counter.CounterName.Equals(metricName))
                                   .ToList();
            }

            return counterNames.Where(counterName => counterName.EndsWith(instanceEnding))
                               .SelectMany(counterName => perfCategory.GetCounters(counterName))
                               .Where(counter => counter.CounterName.Equals(metricName))
                               .ToList();

        }

        /// <summary>
        /// Format Size from bytes to a Kb, MiB or GiB string, where 1KiB = 1024 bytes.
        /// </summary>
        /// <param name="bytes">Number of bytes.</param>
        /// <param name="rounding">The number of significant decimal places (precision) in the
        /// return value.</param>
        /// <param name="useMebiBytes">If true then use MiB (1024 x 1024)B not MB (1000 x 1000)B.
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
        private static string FormatSizeBytes(long bytes, int rounding, bool useMebiBytes = true,
                                              string itemAbbreviation = "B",
                                              bool compact = false)
        {
            string result;
            string format = "#,###." + new string('#', rounding);
            string spacer = compact ? string.Empty : " ";

            int kiloDivisor = useMebiBytes? 1024 : 1000;
            int megaDivisor = useMebiBytes? 1024 * 1024: 1000 * 1000;
            int gigaDivisor = useMebiBytes? 1024 * 1024 * 1024: 1000 * 1000 * 1000;

            if (itemAbbreviation == "B" && useMebiBytes)
                itemAbbreviation = "iB";

            // We're trusting that the compiler will pre-compute the literals here.

            if (bytes < megaDivisor)            // less than a megaunit
            {
                if (bytes < kiloDivisor)        // less than a kilounit
                {
                    result = bytes.ToString("N0", CultureInfo.CurrentCulture) + spacer + itemAbbreviation;
                }
                else                            // more than a kilounit
                {
                    result = Math.Round(bytes / (float)kiloDivisor,
                                        rounding).ToString(format, CultureInfo.CurrentCulture) +
                                        spacer + "K" + itemAbbreviation;
                }
            }
            else                                // greater than a megaunit
            {
                if (bytes > gigaDivisor)        // greater than a gigaunit
                {
                    result = Math.Round(bytes / (float)gigaDivisor,
                                        rounding).ToString(format, CultureInfo.CurrentCulture) +
                                        spacer + "G" + itemAbbreviation;
                }
                else                            // greater than a megaunit
                {
                    result = Math.Round(bytes / (float)megaDivisor,
                                        rounding).ToString(format, CultureInfo.CurrentCulture) +
                                        spacer + "M" + itemAbbreviation; // megaunits
                }
            }

            return result;
        }

    }
}
#pragma warning restore CA1416 // Validate platform compatibility
