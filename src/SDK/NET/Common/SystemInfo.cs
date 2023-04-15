using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Hardware.Info;

#pragma warning disable CA1416 // Validate platform compatibility
namespace CodeProject.AI.SDK.Common
{
    /// <summary>
    /// Represents the properties of all CPUs combined
    /// </summary>
    public class CpuCollection: List<CpuInfo>
    {
    };

    /// <summary>
    /// Represents the properties of a single CPU
    /// </summary>
    public class CpuInfo
    {
        public string? Name { get; set; }
        public uint NumberOfCores { get; set; }
        public uint LogicalProcessors { get; set; }
    };

    /// <summary>
    /// Represents what we know about System (non GPU) memory
    /// </summary>
    public class MemoryProperties
    {
        public ulong Total { get; set; }
        public ulong Available { get; set; }
    };

    public class GpuInfo
    {
        /// <summary>
        /// Gets or sets the name of the card
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the card's vendor
        /// </summary>
        public string? Vendor { get; set; }

        /// <summary>
        /// Gets or sets the driver version string
        /// </summary>
        public string? DriverVersion { get; set; }

        /// <summary>
        /// Gets or sets GPU utlisation as a percentage between 0 and 100
        /// </summary>
        public int Utilization { get; set; }

        /// <summary>
        /// Gets or sets the total memory used in bytes
        /// </summary>
        public ulong MemoryUsed { get; set; }

        /// <summary>
        /// Gets or sets the total card memory in bytes
        /// </summary>
        public ulong TotalMemory { get; set; }

        /// <summary>
        /// The string representation of this object
        /// </summary>
        /// <returns>A string object</returns>
        public virtual string Description
        {
            get
            {
                var info = new StringBuilder();
                info.Append(Name);

                if (TotalMemory > 0)
                    info.Append($" ({SystemInfo.FormatSizeBytes(TotalMemory, 0)})");

                if (!string.IsNullOrWhiteSpace(Vendor))
                    info.Append($" ({Vendor})");

                if (!string.IsNullOrWhiteSpace(DriverVersion))
                    info.Append($" Driver: {DriverVersion}");

                return info.ToString();
            }
        }
    }

    public class NvidiaInfo : GpuInfo
    {
        public NvidiaInfo() : base()
        {
            Vendor = "NVidia";
        }

        public string? CudaVersion { get; set; }
        public string? ComputeCapacity { get; internal set; }

        /// <summary>
        /// The string representation of this object
        /// </summary>
        /// <returns>A string object</returns>
        public override string Description
        {
            get
            {
                var info = base.Description 
                         + " CUDA: " + CudaVersion
                         + " Compute: " + ComputeCapacity;

                return info;
            }
        }  
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
    /// Provides information on the host system, including OS, CPU, GPU, runtimes and the overall
    /// environment in which the server is running.
    /// </summary>
    /// <remarks>
    /// Uses the Hardware.info package. Also see https://www.cyberciti.biz/faq/howto-find-linux-vga-video-card-ram/
    /// </remarks>
    public class SystemInfo
    {
        // The underlying object that does the investigation into the properties.
        // The other properties mostly rely on this creature for their worth.
        private static HardwareInfo? _hardwareInfo = null;
        private static bool?         _isDevelopment;
        private static bool?         _hasNvidiaCard;

        /// <summary>
        /// Gets the CPU properties for this system
        /// </summary>
        public static CpuCollection? CPU { get; private set; }

        /// <summary>
        /// Gets the Memory properties for this system
        /// </summary>
        public static MemoryProperties? Memory { get; private set; }

        /// Gets the GPU properties for this system
        public static GpuInfo? GPU { get; private set; }

        /// <summary>
        /// Gets a summary of the system that can be safely sent as telemetry (no personal info).
        /// </summary>
        public static object? Summary { get; private set; }

        /// <summary>
        /// Whether or not this system contains an Nvidia card. If the value is
        /// null it means we've not been able to determine.
        /// </summary>
        public static bool? HasNvidiaGPU => _hasNvidiaCard; 

        /// <summary>
        /// Gets a value indicating whether we are running development code.
        /// </summary>
        public static bool IsDevelopmentCode
        {
            get
            {
                if (_isDevelopment is not null)
                    return (bool)_isDevelopment;

                _isDevelopment = false;

                // 1. Scoot up the tree to check for build folders
                var info = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
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
                if (IsDevelopmentCode ||
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
        /// Gets the hardware vendor.
        /// </summary>
        public static string Vendor
        {
            get
            {
                if (OperatingSystem == "macOS")
                    return "Apple";

                if (OperatingSystem == "Linux")
                {
                    try
                    {
                        string cpuInfo = File.ReadAllText("/proc/cpuinfo");
                        if (cpuInfo.Contains("Raspberry Pi"))
                            return "Raspberry Pi";
                    }
                    catch {}
                }

                // Intel and AMD chips...

                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the current Operating System name.
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
        /// Returns the Operating System description, with corrections for Windows 11
        /// </summary>
        public static string OperatingSystemDescription
        {
            get
            {
                // See https://github.com/getsentry/sentry-dotnet/issues/1484. 
                // C'mon guys: technically the version may be 10.x, but stick to the branding that
                // the rest of the world understands.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
                    Environment.OSVersion.Version.Major >= 10 && 
                    Environment.OSVersion.Version.Build >= 22000)
                    return RuntimeInformation.OSDescription.Replace("Windows 10.", "Windows 11 version 10.");

                return RuntimeInformation.OSDescription;
            }
        }

        /// <summary>
        /// Returns the Operating System version (Major, Minor, Build, Revision)
        /// </summary>
        public static string OperatingSystemVersion
        {
            get { return Environment.OSVersion.Version.ToString(); }
        }

        /// <summary>
        /// Static constructor
        /// </summary>
        static SystemInfo()
        {
           // InitializeAsync();
        }

        /// <summary>
        /// Initializes the SystemInfo class async. This method is here in case we want to avoid a
        /// static constructor that makes potentially blocking calls to async methods. The static
        /// method would be removed, and this method would be called at the start of whatever app
        /// used this class. 
        /// </summary>
        /// <returns></returns>
        public static async Task InitializeAsync()
        {
            // Not necessary.
            // await Task.Run(() =>
            // {
                try
                {
                    _hardwareInfo = new HardwareInfo();
                    _hardwareInfo.RefreshCPUList(false); // false = no CPU %. Saves 21s delay on first use
                    _hardwareInfo.RefreshMemoryStatus();
                    _hardwareInfo.RefreshVideoControllerList();
                }
                catch
                {
                }
            // });

            GPU    = await GetGpuInfo(); // <- This await is the reason we have async messiness.
            CPU    = GetCpuInfo();
            Memory = GetMemoryInfo();

            InitSummary();
        }

        /// <summary>
        /// Returns basic system info
        /// </summary>
        /// <returns>A string object</returns>
        public static string GetSystemInfo()
        {       
            var info = new StringBuilder();

            string? gpuDesc = GPU?.Description;

            info.AppendLine($"Operating System: {OperatingSystem} ({OperatingSystemDescription})");

            if (CPU is not null)
            {
                var cpus = new StringBuilder();
                if (!string.IsNullOrEmpty(CPU[0].Name))
                    cpus.Append(CPU[0].Name + "\n                  ");

                cpus.Append(CPU.Count + " CPU");
                if (CPU.Count != 1)
                    cpus.Append("s");

                if (CPU[0].NumberOfCores > 0)
                {
                    cpus.Append($" x {CPU[0].NumberOfCores} core");
                    if (CPU[0].NumberOfCores != 1)
                        cpus.Append("s");
                }
                cpus.Append(".");
                if (CPU[0].LogicalProcessors > 0)
                    cpus.Append($" {CPU[0].LogicalProcessors} logical processors");

                info.AppendLine($"CPUs:             {cpus} ({Architecture})");
            }

            if (!string.IsNullOrWhiteSpace(gpuDesc))
            {
                gpuDesc = gpuDesc.Replace("Driver:", "\n                  Driver:");
                info.AppendLine($"GPU:              {gpuDesc}");
            }
            if (Memory is not null)
                info.AppendLine($"System RAM:       {FormatSizeBytes(Memory.Total, 0)}");
            info.AppendLine($"Target:           {Platform}");
            info.AppendLine($"BuildConfig:      {BuildConfig}");
            info.AppendLine($"Execution Env:    {ExecutionEnvironment}");
            info.AppendLine($"Runtime Env:      {RuntimeEnvironment}");
            info.AppendLine($".NET framework:   {RuntimeInformation.FrameworkDescription}");
            // info.AppendLine($"Python versions:  {PythonVersions}");

            return info.ToString().Trim();
        }

        /// <summary>
        /// Returns GPU usage info for the current system
        /// </summary>
        public static async ValueTask<string> GetGpuUsageInfo()
        {
            int    gpu3DUsage  = await GetGpuUsage();
            string gpuMemUsage = FormatSizeBytes(await GetGpuMemoryUsage(), 1);

            var info = new StringBuilder();
            info.AppendLine("System GPU info:");
            info.AppendLine($"  GPU 3D Usage       {gpu3DUsage}%");
            info.AppendLine($"  GPU RAM Usage      {gpuMemUsage}");

            return info.ToString().Trim();
        }

        /* Maybe we need this, but could be tricky for all platforms
        /// <summary>
        /// Returns the CPU temperature in C.
        /// </summary>
        /// <returns>The temperature in C</returns>
        public static async ValueTask<int> GetCpuTemp()
        {
            return await new ValueTask<int>(0);

            // macOS: sudo powermetrics --samplers smc | grep -i "CPU die temperature"
        }
        */

        /// <summary>
        /// Returns Video adapter info for the current system
        /// </summary>
        public static async ValueTask<string> GetVideoAdapterInfo()
        {
            var info = new StringBuilder();

            info.AppendLine("Video adapter info:");
            if (_hardwareInfo != null)
            {
                foreach (var videoController in _hardwareInfo.VideoControllerList)
                {
                    string adapterRAM = FormatSizeBytes(videoController.AdapterRAM, 0);

                    info.AppendLine($"  {videoController.Name}:");
                    // info.AppendLine($"    Adapter RAM        {adapterRAM}"); - terribly inaccurate
                    info.AppendLine($"    Driver Version     {videoController.DriverVersion}");
                    // info.AppendLine($"    Driver Date        {videoController.DriverDate}");
                    info.AppendLine($"    Video Processor    {videoController.VideoProcessor}");
                }
            }

            return await new ValueTask<string>(info.ToString().Trim());
        }

        /// <summary>
        /// Gets the current GPU utilisation as a %
        /// </summary>
        /// <returns>A float representing bytes</returns>
        public async static ValueTask<int> GetCpuUsage()
        {
            int usage = 0;

            // ANNOYANCE: We have Windows, Linux and macOS defined as constants in the Common.targets
            // file in /SDK/NET. These constants work great in Windows. Sometimes in Linux. Never in
            // macOS on Apple Silicon. So we work around it.
            try
            {
// #if Windows
                if (OperatingSystem == "Windows")
                {
                    // Easier, but this incurs a 21 sec delay at startup, and after 15 min idle.
                    // _hardwareInfo.RefreshCPUList(true);
                    // int usage = (int) _hardwareInfo.CpuList.Average(cpu => (float)cpu.PercentProcessorTime);

                    List<PerformanceCounter> utilization = GetCounters("Processor",
                                                                       "% Processor Time",
                                                                       "_Total");
                    utilization.ForEach(x => x.NextValue());
                    await Task.Delay(1000);
                    usage = (int)utilization.Sum(x => x.NextValue());
                }
// #elif Linux
                else if (OperatingSystem == "Linux")
                {
                    // Easier but not yet tested
                    // _hardwareInfo.RefreshCPUList(true);
                    // int usage = (int) _hardwareInfo.CpuList.Average(cpu => (float)cpu.PercentProcessorTime);

                    // Output is in the form:
                    // top - 08:38:12 up  1:20,  0 users,  load average: 0.00, 0.00, 0.00
                    // Tasks:   5 total,   1 running,   4 sleeping,   0 stopped,   0 zombie
                    // %Cpu(s):  0.0 us,  0.0 sy,  0.0 ni, ... <-- this line, sum of values 1-3

                    var results = await GetProcessInfo("/bin/bash",  "-c \"top -b -n 1\"");
                    var lines = results["output"]?.Split("\n");

                    if (lines is not null && lines.Length > 2)
                    {
                        string pattern = @"(?<userTime>[\d.]+)\s*us,\s*(?<systemTime>[\d.]+)\s*sy,\s*(?<niceTime>[\d.]+)\s*ni";
                        Match match    = Regex.Match(lines[2], pattern, RegexOptions.ExplicitCapture);
                        var userTime   = match.Groups["userTime"].Value;
                        var systemTime = match.Groups["systemTime"].Value;
                        var niceTime   = match.Groups["niceTime"].Value;

                        usage = (int)(float.Parse(userTime) + float.Parse(systemTime) + float.Parse(niceTime));
                    }
                }
// #elif macOS
                else if (OperatingSystem == "macOS")
                {
                    // oddly, hardware.info hasn't yet added CPU usage for macOS

                    // Output is in the form:
                    // CPU usage: 12.33% user, 13.63% sys, 74.2% idle 
                    string pattern = @"CPU usage:\s+(?<userTime>[\d.]+)%\s+user,\s*(?<systemTime>[\d.]+)%\s*sys,\s*(?<idleTime>[\d.]+)%\s*idle";
                    var results = await GetProcessInfo("/bin/bash",  "-c \"top -l 1 | grep -E '^CPU'\"",
                                                       pattern);
                    usage = (int)(float.Parse(results["userTime"]) + float.Parse(results["systemTime"]));
                }
// #else
                else
                {
                    Console.WriteLine("WARNING: Getting CPU usage for unknown OS: " + OperatingSystem);
                    await Task.Delay(0);
                }
// #endif
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to get CPU use: " + e);
            }

            return (int) usage;
        }

        /// <summary>
        /// Gets the amount of System memory currently in use
        /// </summary>
        /// <returns>A long representing bytes</returns>
        public async static ValueTask<ulong> GetSystemMemoryUsage()
        {
            ulong memoryUsed = 0;

            // ANNOYANCE: We have Windows, Linux and macOS defined as constants in the Common.targets
            // file in /SDK/NET. These constants work great in Windows. Sometimes in Linux. Never in
            // macOS on Apple Silicon. So we work around it.

// #if Windows
            if (OperatingSystem == "Windows")
            {
                var processes = Process.GetProcesses();
                memoryUsed = (ulong)processes.Sum(p => p.WorkingSet64);
            }
// #elif Linux
            else if (OperatingSystem == "Linux")
            {
                // Easier but not yet tested
                // _hardwareInfo.RefreshMemoryStatus();
                // return _hardwareInfo.MemoryStatus.TotalPhysical - _hardwareInfo.MemoryStatus.AvailablePhysical;

                // Output is in the form:
                //       total used free
                // Mem:    XXX  YYY  ZZZ <- We want tokens 1 - 3 from this line
                // Swap:   xxx  yyy  zzz

                var results = await GetProcessInfo("/bin/bash",  "-c \"free -b\"");
                var lines = results["output"]?.Split("\n");

                if (lines is not null && lines.Length > 1)
                {
                    var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    memoryUsed  = ulong.Parse(memory[2]);
                    // ulong totalMemory = ulong.Parse(memory[1]);
                    // ulong memoryFree  = ulong.Parse(memory[3]);
                }
            }
// #elif macOS
            else if (OperatingSystem == "macOS")
            {
                var results = await GetProcessInfo("/bin/bash", 
                                                   "-c \"ps -caxm -orss= | awk '{ sum += $1 } END { print sum * 1024 }'\"",
                                                   "(?<memused>[\\d.]+)");
                ulong.TryParse(results["memused"], out memoryUsed);
            }
// #else
            else
            {
                Console.WriteLine("WARNING: Getting memory usage for unknown OS: " + OperatingSystem);
            }
// #endif
            return memoryUsed;
        }

        /// <summary>
        /// Gets the current GPU utilisation as a %
        /// </summary>
        /// <returns>An int representing bytes</returns>
        public async static ValueTask<int> GetGpuUsage()
        {
            NvidiaInfo? gpuInfo = await ParseNvidiaSmi();
            if (gpuInfo is not null)
                return gpuInfo.Utilization;

            int usage = 0;

// #if Windows
            try
            {
                if (OperatingSystem == "Windows")
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
// #else
                else if (Vendor == "Raspberry Pi")
                {
                    var results = await GetProcessInfo("vcgencmd", "get_config core_freq", @"=(?<maxFreq>\d+)");
                    if (ulong.TryParse(results["maxFreq"], out ulong maxFreq))
                        maxFreq *= 1_000_000;

                    results = await GetProcessInfo("vcgencmd", "measure_clock core", @"=(?<freq>\d+)");
                    ulong.TryParse(results["freq"], out ulong freq);

                    if (maxFreq > 0)
                        usage = (int)(freq * 100 / maxFreq);
                }
            }
            catch
            {
            }
// #endif
            return usage;
        }

        /// <summary>
        /// Gets the amount of GPU memory currently in use
        /// </summary>
        /// <returns>A long representing bytes</returns>
        public async static ValueTask<ulong> GetGpuMemoryUsage()
        {
            NvidiaInfo? gpuInfo = await ParseNvidiaSmi();
            if (gpuInfo is not null)
                return gpuInfo.MemoryUsed;

            ulong memoryUsed = 0;

            try
            {
// #if Windows
                if (OperatingSystem == "Windows")
                {
                    List<PerformanceCounter> counters = GetCounters("GPU Process Memory",
                                                                    "Dedicated Usage", null);
                    // gpuCounters.ForEach(x => x.NextValue());
                    // Thread.Sleep(1000);

                    memoryUsed = (ulong)counters.Sum(x => (long)x.NextValue());
                }

                if (Vendor == "Raspberry Pi")
                {
                    var results = await GetProcessInfo("vcgencmd", "get_mem malloc_total", @"=(?<memused>\d+)");
                    if (ulong.TryParse(results["memused"], out ulong memUsed))
                        memoryUsed = memUsed * 1024 * 1024;
                }
// #endif
            }
            catch 
            {
            }

            return memoryUsed;
        }

        public async static ValueTask<GpuInfo?> GetGpuInfo()
        {
            if (Architecture == "Arm64" && OperatingSystem == "macOS")
            {
                return new GpuInfo
                {
                    Name   = "Apple Silicon",
                    Vendor = "Apple"
                };
            }

            GpuInfo? gpu = await ParseNvidiaSmi();
            if (gpu is null && _hardwareInfo != null)
            {
                foreach (var videoController in _hardwareInfo.VideoControllerList)
                {
                    if (string.IsNullOrWhiteSpace(videoController.Manufacturer))
                        continue;

                    gpu = new GpuInfo
                    {
                        Name          = videoController.Name,
                        Vendor        = videoController.Manufacturer,
                        DriverVersion = videoController.DriverVersion,
                        TotalMemory   = videoController.AdapterRAM
                    };

                    break;
                }
            }

            return gpu;
        }

        /// <summary>
        /// Returns information on the first GPU found (TODO: Extend to multiple GPUs)
        /// </summary>
        /// <returns>An NvidiaInfo object</returns>
        private async static ValueTask<NvidiaInfo?> ParseNvidiaSmi()
        {
            // Do an initial fast check to see if we have an Nvidia card. This saves a process call
            // and exception in the case there's not Nvidia card. If _hardwareInfo is null then we
            // just plow on ahead regardless.
            if (_hasNvidiaCard is null && _hardwareInfo is not null)
            {
                _hasNvidiaCard = false;
                foreach (var videoController in _hardwareInfo.VideoControllerList)
                {
                    if (videoController.Manufacturer.ContainsIgnoreCase("NVidia"))
                        _hasNvidiaCard = true;
                }
            }
           
            if (_hasNvidiaCard == false)
                return null;
                
            // TODO: Cache this value once a second

            try
            {
                // Example call and response
                // nvidia-smi --query-gpu=count,name,driver_version,memory.total,memory.free,utilization.gpu,compute_cap --format=csv,noheader
                // 1, NVIDIA GeForce RTX 3060, 512.96, 12288 MiB, 10473 MiB, 4 %, 8.6
                // BUT WAIT: For old cards we don't get compute_cap. So we break this up.

                string args    = "--query-gpu=count,name,driver_version,memory.total,memory.free,utilization.gpu --format=csv,noheader";
                string pattern = @"\d+,\s+(?<gpuname>.+?),\s+(?<driver>[\d.]+),\s+(?<memtotal>\d+)\s*MiB,\s+(?<memfree>\d+)\s*MiB,\s+(?<gpuUtil>\d+)\s*%";
                var results    = await GetProcessInfo("nvidia-smi", args, pattern);

                var gpuName         = results["gpuname"];
                var driverVersion   = results["driver"];
                ulong.TryParse(results["memfree"],  out ulong memoryFreeMiB);
                ulong.TryParse(results["memtotal"], out ulong totalMemoryMiB);
                int.TryParse(results["gpuUtil"],    out int gpuUtilPercent);

                ulong memoryUsedMiB = totalMemoryMiB - memoryFreeMiB;

                // Get Compute Capability if we can
                // nvidia-smi --query-gpu=compute_cap --format=csv,noheader
                // 8.6
                args    = "--query-gpu=compute_cap --format=csv,noheader";
                pattern = @"(?<computecap>[\d\.]*)";
                results = await GetProcessInfo("nvidia-smi", args, pattern);
                string computeCapacity = results["computecap"];

                // Get CUDA info. Output is in the form:
                //  Thu Dec  8 08:45:30 2022
                //  +-----------------------------------------------------------------------------+
                //  | NVIDIA-SMI 512.96       Driver Version: 512.96       CUDA Version: 11.6     |
                //  |-------------------------------+----------------------+----------------------+
                pattern = @"Driver Version:\s+(?<driver>[\d.]+)\s*CUDA Version:\s+(?<cuda>[\d.]+)";
                results = await GetProcessInfo("nvidia-smi", "", pattern);
                string cudaVersion = results["cuda"];

                // If we've reached this point we definitely have an Nvidia card.
                _hasNvidiaCard = true;

                return new NvidiaInfo
                {
                    Name            = gpuName,
                    DriverVersion   = driverVersion,
                    CudaVersion     = cudaVersion,
                    Utilization     = gpuUtilPercent,
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
        
        /// <summary>
        /// Format Size from bytes to a Kb, MiB or GiB string, where 1KiB = 1024 bytes.
        /// </summary>
        /// <param name="bytes">Number of bytes.</param>
        /// <param name="rounding">The number of significant decimal places (precision) in the
        /// return value.</param>
        /// <param name="useBinaryMultiples">If true then use MiB (1024 x 1024)B not MB (1000 x 1000)B.
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
        public static string FormatSizeBytes(ulong bytes, int rounding, bool useBinaryMultiples = true,
                                             string itemAbbreviation = "B", bool compact = false)
        {
            if (bytes == 0)
                return "0";

            string result;
            string format = "#,###." + new string('#', rounding);
            string spacer = compact ? string.Empty : " ";

            // We're trusting that the compiler will pre-compute the values here.
            ulong kiloDivisor = useBinaryMultiples? 1024UL                      : 1000UL;
            ulong megaDivisor = useBinaryMultiples? 1024UL * 1024               : 1000UL * 1000;
            ulong gigaDivisor = useBinaryMultiples? 1024UL * 1024 * 1024        : 1000UL * 1000 * 1000;
            ulong teraDivisor = useBinaryMultiples? 1024UL * 1024 * 1024 * 1024 : 1000UL * 1000 * 1000 * 1000;

            // 1KB = 1024 bytes. 1kB = 1000 bytes. 1KiB = 1024 bytes. 
            // Read https://physics.nist.gov/cuu/Units/binary.html. If you dare.
            string kilos = (useBinaryMultiples ? "Ki" : "k") + itemAbbreviation;
            string megas = (useBinaryMultiples ? "Mi" : "m") + itemAbbreviation;
            string gigas = (useBinaryMultiples ? "Gi" : "g") + itemAbbreviation;
            string teras = (useBinaryMultiples ? "Ti" : "t") + itemAbbreviation;

            if (bytes > teraDivisor)            // more than a teraunit
            {
                result = Math.Round(bytes / (float)teraDivisor,
                                    rounding).ToString(format, CultureInfo.CurrentCulture) +
                                    spacer + teras;
            }
            else if (bytes > gigaDivisor)       // more than a gigaunit but less than a teraunit
            {
                result = Math.Round(bytes / (float)gigaDivisor,
                                    rounding).ToString(format, CultureInfo.CurrentCulture) +
                                    spacer + gigas;
            }
            else if (bytes > megaDivisor)       // more than a megaunit but less than a gigaunit
            {
                result = Math.Round(bytes / (float)megaDivisor,
                                    rounding).ToString(format, CultureInfo.CurrentCulture) +
                                    spacer + megas;
            }
            else if (bytes > kiloDivisor)       // more than a kilounit but less than a megaunit
            {
                result = Math.Round(bytes / (float)kiloDivisor,
                                    rounding).ToString(format, CultureInfo.CurrentCulture) +
                                    spacer + kilos;
            }
            else                                // less than a kilounit
            {
                result = bytes.ToString(format, CultureInfo.CurrentCulture) + spacer + itemAbbreviation;
            }

            return result;
        }

        private static MemoryProperties GetMemoryInfo()
        {
            return new MemoryProperties
            {
                Total     = _hardwareInfo?.MemoryStatus.TotalPhysical ?? 0,
                Available = _hardwareInfo?.MemoryStatus.AvailablePhysical ?? 0
            };
        }

        private static CpuCollection GetCpuInfo() 
        {
            var cpus = new CpuCollection();
            if (_hardwareInfo != null)
            {
                foreach (var cpu in _hardwareInfo.CpuList)
                {
                    var cpuInfo = new CpuInfo
                    {
                        Name              = cpu.Name,
                        NumberOfCores     = cpu.NumberOfCores,
                        LogicalProcessors = cpu.NumberOfLogicalProcessors
                    };

                    cpus.Add(cpuInfo);
                }
            }

            // There's obviously at least 1.
            if (cpus.Count == 0)
                cpus.Add(new CpuInfo() { NumberOfCores = 1, LogicalProcessors = 1 });

            return cpus;
        }

        private static void InitSummary()
        {
            Summary = new
            {
                Host = new
                {
                    OperatingSystem = OperatingSystem,
                    OSVersion       = OperatingSystemVersion,
                    TotalMemory     = Memory?.Total ?? 0,
                    Environment     = ExecutionEnvironment.ToString(),
                },
                CPU = new
                {
                    Count             = CPU?.Count ?? 0,
                    Name              = CPU?[0].Name ?? "",
                    Cores             = CPU?[0].NumberOfCores ?? 0,
                    LogicalProcessors = CPU?[0].LogicalProcessors ?? 0,
                    Architecture      = Architecture
                },
                GPUs = GPU is null
                      ? Array.Empty<object>()
                      : new Object[] {
                        GPU is NvidiaInfo? new {
                                                Name            = GPU.Name,
                                                Vendor          = GPU.Vendor,
                                                Memory          = GPU.TotalMemory,
                                                DriverVersion   = GPU.DriverVersion,
                                                CUDAVersion     = (GPU as NvidiaInfo)?.CudaVersion,
                                                ComputeCapacity = (GPU as NvidiaInfo)?.ComputeCapacity
                                            }
                                         : new {
                                                Name            = GPU.Name,
                                                Vendor          = GPU.Vendor,
                                                Memory          = GPU.TotalMemory,
                                                DriverVersion   = GPU.DriverVersion
                                            }
                        }
            };
        }

        /*
        private static string GetPythonVersions()
        {
            // Windows: py -0
            // Linux:   ls -1 /usr/bin/python* | grep '.*[2-3].[0-9]$' | sed 's/\/usr\/bin\///g'
        }
        */
        
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
        /// Runs a command/args as a process, grabs the output of this process, then creates a
        /// dictionary containing name/value pairs based on a regex pattern against the output.
        /// An entry "output" containing the full output of the process will always be included.
        /// </summary>
        /// <param name="command">The command to run</param>
        /// <param name="args">The args for the command</param>
        /// <param name="pattern">The regex pattern to apply to the output of the command. Each
        /// named expression in that pattern will be the name of an entry in the return dictionary</param>
        /// <returns>A Dictionary</returns>
        /// <remarks>Suppose you call the process "dotnet" with args "--version". Say you want to 
        /// extract the Major/Minor version, so your pattern is @"(?<major>\d+)\.(?<minor>\d+)".
        /// The results from this call will be a dictionary with:
        ///    "output" = "7.0.201",
        ///    "major"  = "7"
        ///    "minor"  = "0"
        /// "output" is always added as the first item in the dictionary, and is the full text output
        /// by the process call. The names "major", and "minor" were pulled from the regex pattern.
        /// </remarks>
        private async static ValueTask<Dictionary<string, string>> GetProcessInfo(string command,
                                                                                  string args,
                                                                                  string? pattern = null)
        {
            var values = new Dictionary<string, string>();

            try
            {
                var info = new ProcessStartInfo(command, args);
                info.RedirectStandardOutput = true;

                using var process = Process.Start(info);
                if (process?.StandardOutput is null)
                    return values;

                string? output = await process.StandardOutput.ReadToEndAsync() ?? string.Empty;

                // Raw output
                values["output"] = output;

                // Extracted values
                if (!string.IsNullOrEmpty(pattern))
                {
                    // Match values in the output against the pattern
                    Match valueMatch = Regex.Match(output, pattern, RegexOptions.ExplicitCapture);

                    // Match the names in the pattern against our match name pattern
                    Regex expression = new Regex(@"\(\?\<(?<matchName>([a-zA-Z_-]+))\>");
                    var results = expression.Matches(pattern);

                    // For each name we found in the search pattern, get the value of the match of that
                    // name from the output string and store in the dictionary
                    foreach (Match patternMatch in results)
                    {
                        string matchName = patternMatch.Groups[1].Value;
                        values[matchName] = valueMatch.Groups[matchName].Value;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return values;
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility