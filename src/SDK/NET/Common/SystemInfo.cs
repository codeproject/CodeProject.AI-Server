using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using Hardware.Info;
using CodeProject.AI.SDK.Utils;

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
        public string? Name              { get; set; }
        public string? HardwareVendor    { get; set; }
        public uint    NumberOfCores     { get; set; }
        public uint    LogicalProcessors { get; set; }
    };

    /// <summary>
    /// Represents what we know about System (non GPU) memory
    /// </summary>
    public class MemoryProperties
    {
        public ulong Total     { get; set; }
        public ulong Used      { get; set; }
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
        public string? HardwareVendor { get; set; }

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

                if (!string.IsNullOrWhiteSpace(HardwareVendor))
                    info.Append($" ({HardwareVendor})");

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
            HardwareVendor = "NVIDIA";
        }

        /// <summary>
        /// The CUDA version that the current driver is capable of using
        /// </summary> 
        public string? CudaVersionCapability { get; set; }

        /// <summary>
        /// The actual version of CUDA that's installed
        /// </summary>
        public string? CudaVersionInstalled { get; set; }

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
                         + $" CUDA: {CudaVersionInstalled} (max supported: {CudaVersionCapability})"
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
        private static HardwareInfo _hardwareInfo = new HardwareInfo();
        private static bool?        _isDevelopment;
        private static bool?        _hasNvidiaCard;
        private static bool         _isWSL;
        private static string?      _defaultPythonVersion;

        private static TimeSpan     _nvidiaInfoRefreshTime = TimeSpan.FromSeconds(10);
        private static TimeSpan     _systemInfoRefreshTime = TimeSpan.FromSeconds(1);

        private static Task?        _monitorSystemUsageTask;
        private static Task?        _monitoryGpuUsageTask;
        private static bool         _monitoringStartedWarningIssued;
        private static int          _cpuUsage;
        private static string?      _hardwareVendor;

        /// <summary>
        /// Gets the CPU properties for this system
        /// </summary>
        public static CpuCollection? CPU { get; private set; }

        /// <summary>
        /// Gets the Memory properties for this system
        /// </summary>
        public static MemoryProperties Memory { get; private set; } = new MemoryProperties();

        /// <summary>
        /// Gets the GPU properties for this system
        /// </summary>
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
                    // Really should use the IHostEnvironment.IsDevelopment method, but needs a
                    // reference to IHostEnvironment.
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").EqualsIgnoreCase("Development") ||
                    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT").EqualsIgnoreCase("Development"))
                {
                    return RuntimeEnvironment.Development;
                }

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
        /// Returns OS[-architecture]. eg Windows, macOS, Linux-Arm64. Note that x64 won't have a
        /// suffix.
        /// </summary>
        public static string OSAndArchitecture
        {
            get
            {
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
        /// Gets the current platform name. This will be OS[-architecture]. eg Windows, macOS,
        /// Linux-Arm64.  Note that x64 won't have a suffix.
        /// </summary>
        public static string Platform
        {
            get
            {
                if (HardwareVendor == "Raspberry Pi" || HardwareVendor == "Orange Pi")
                    return HardwareVendor.Replace(" ", string.Empty);

                if (HardwareVendor == "NVIDIA Jetson")
                    return "Jetson";

                return OSAndArchitecture;
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

                return string.Empty;
            }
        }
        /// <summary>
        /// Gets the name of the system under which we're running. Windows, WSL, macOS, Docker,
        /// Raspberry Pi, Orange Pi, Jetson, Linux, macOS.
        /// </summary>
        public static string SystemName
        {
            get
            {
                if (IsDocker)
                    return "Docker";

                if (IsWSL)
                    return "WSL";
                   
                if (HardwareVendor == "Raspberry Pi" || HardwareVendor == "Orange Pi")
                    return HardwareVendor;

                if (HardwareVendor == "NVIDIA Jetson")
                    return "Jetson";

                return OperatingSystem;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we are currently running under WSL.
        /// </summary>
        public static bool IsWSL
        {
            get { return _isWSL; }
        }

        /// <summary>
        /// Gets the hardware vendor of the current system.
        /// </summary>
        public static string HardwareVendor
        {
            get
            {
                if (_hardwareVendor is not null)
                    return _hardwareVendor;

                if (IsMacOS)
                    _hardwareVendor = "Apple";

                if (IsLinux)
                {
                    try
                    {
                        // string cpuInfo = File.ReadAllText("/proc/cpuinfo"); - no good for Orange Pi
                        string cpuInfo = File.ReadAllText/*Async*/("/sys/firmware/devicetree/base/model");
                        if (cpuInfo.ContainsIgnoreCase("Raspberry Pi"))
                            _hardwareVendor = "Raspberry Pi";
                        else if (cpuInfo.ContainsIgnoreCase("Orange Pi"))
                            _hardwareVendor = "Orange Pi";
                    }
                    catch {}

                    if (_hardwareVendor is null)
                    {
                        try
                        {
                            string cpuInfo = File.ReadAllText("/proc/device-tree/model");
                            if (cpuInfo.Contains("Jetson"))
                                _hardwareVendor = "NVIDIA Jetson";
                        }
                        catch {}
                    }
                }

                // Intel and AMD chips are generic, so just report them.
                if (_hardwareVendor is null && CPU is not null)
                {
                    if (CPU[0].HardwareVendor == "Intel")
                        _hardwareVendor = "Intel";
                    else if (CPU[0].HardwareVendor == "AMD")
                        _hardwareVendor = "AMD";
                }

                if (_hardwareVendor is null)
                    _hardwareVendor = "Unknown";

                return _hardwareVendor;
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
        /// Gets a value indicating whether the current OS is Windows
        /// </summary>
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// Gets a value indicating whether the current OS is Linux
        /// </summary>
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        /// <summary>
        /// Gets a value indicating whether the current OS is macOS
        /// </summary>
        public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>
        /// Gets a value indicating whether the current OS is FreeBSD
        /// </summary>
        public static bool IsFreeBSD => RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);

        /// <summary>
        /// Gets a value indicating whether we are currently running in Docker
        /// </summary>
        public static bool IsDocker => ExecutionEnvironment == ExecutionEnvironment.Docker;

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
        /// Returns the default Python version for the current system (only returns Major.Minor)
        /// </summary>
        public static string DefaultPythonVersion
        {
            get { return _defaultPythonVersion is null? string.Empty : _defaultPythonVersion; }
        }

        /// <summary>
        /// Returns a value indicating whether system resources (Memory, CPU) are being monitored.
        /// </summary>
        public static bool IsResourceUsageMonitoring  => _monitorSystemUsageTask != null;

        /// <summary>
        /// Initializes the SystemInfo class.
        /// </summary>
        public static async Task InitializeAsync()
        {
            try
            {
                _hardwareInfo.RefreshCPUList(false); // false = no CPU %. Saves 21s delay on first use
                _hardwareInfo.RefreshMemoryStatus();
                _hardwareInfo.RefreshVideoControllerList();
            }
            catch
            {
            }

            CPU = GetCpuInfo();
            GPU = GetGpuInfo(); 
            await GetMemoryInfoAsync().ConfigureAwait(false);
            
            await CheckForWslAsync().ConfigureAwait(false);

            var pattern = "python (?<version>\\d\\.\\d+)";
            var options = RegexOptions.IgnoreCase;
            var results = await GetProcessInfoAsync("python", "--version", pattern, options).ConfigureAwait(false);
            if ((results?.Count ?? 0) > 0)
                _defaultPythonVersion = results!["version"];

            _monitorSystemUsageTask = MonitorSystemUsageAsync();
            _monitoryGpuUsageTask   = MonitorNvidiaGpuUsageAsync();
                
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

            info.AppendLine($"System:           {SystemName}");
            info.AppendLine($"Operating System: {OperatingSystem} ({OperatingSystemDescription})");

            if (CPU is not null)
            {
                var cpus = new StringBuilder();
                if (!string.IsNullOrEmpty(CPU[0].Name))
                {
                    cpus.Append(CPU[0].Name);
                    if (!string.IsNullOrWhiteSpace(CPU[0].HardwareVendor))
                        cpus.Append($" ({CPU[0].HardwareVendor})");
                    cpus.Append("\n                  ");
                }

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
                // Wrap the lines
                gpuDesc = gpuDesc.Replace("Driver:",  "\n                  Driver:");
                // gpuDesc = gpuDesc.Replace("Compute:", "\n                  Compute:");
                info.AppendLine($"GPU:              {gpuDesc}");
            }

            if (Memory is not null)
                info.AppendLine($"System RAM:       {FormatSizeBytes(Memory.Total, 0)}");

            info.AppendLine($"Target:           {Platform}");
            info.AppendLine($"BuildConfig:      {BuildConfig}");
            info.AppendLine($"Execution Env:    {ExecutionEnvironment}");
            info.AppendLine($"Runtime Env:      {RuntimeEnvironment}");
            info.AppendLine($".NET framework:   {RuntimeInformation.FrameworkDescription}");
            info.AppendLine($"Default Python:   {DefaultPythonVersion}");

            return info.ToString().Trim();
        }

        /// <summary>
        /// Returns GPU idle info for the current system
        /// </summary>
        public static async ValueTask<string> GetGpuUsageInfoAsync()
        {
            int    gpu3DUsage  = await GetGpuUsageAsync().ConfigureAwait(false);
            string gpuMemUsage = FormatSizeBytes(await GetGpuMemoryUsageAsync().ConfigureAwait(false), 1);

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
        public static async ValueTask<int> GetCpuTempAsync()
        {
            return await new ValueTask<int>(0);

            // macOS: sudo powermetrics --samplers smc | grep -i "CPU die temperature"
        }
        */

        /// <summary>
        /// Returns Video adapter info for the current system
        /// </summary>
        public static async ValueTask<string> GetVideoAdapterInfoAsync()
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

            return await new ValueTask<string>(info.ToString().Trim()).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the CPU Usage for this system.
        /// </summary>
        /// <returns>An int representing the percentage of CPU capacity used</returns>
        /// <remarks>This method could be (and was) a property, but to stick to the format we had
        /// where we use 'Get' to query an instantaneous value it's been switched to a method.</remarks>
        public static int GetCpuUsage()
        {
            CheckMonitoringStarted();

            return _cpuUsage;
        }

        /// <summary>
        /// Gets the amount of System memory currently in use
        /// </summary>
        /// <returns>A long representing bytes</returns>
        /// <remarks>This method could be (and was) a property, but to stick to the format we had
        /// where we use 'Get' to query an instantaneous value it's been switched to a method.</remarks>
        public static ulong GetSystemMemoryUsage()
        {
            CheckMonitoringStarted();

            lock (Memory)
            {
                return Memory.Used;
            };
        }

        /// <summary>
        /// Gets the current GPU utilisation as a %
        /// </summary>
        /// <returns>An int representing bytes</returns>
        public async static ValueTask<int> GetGpuUsageAsync()
        {
            CheckMonitoringStarted();

            // NVIDIA cards are continuously monitored. Grab the latest and go.
            NvidiaInfo? gpuInfo = GPU as NvidiaInfo;
            if (gpuInfo is not null)
                return gpuInfo.Utilization;

            int usage = 0;

            try
            {
                if (IsWindows)
                {
                    List<PerformanceCounter> utilization = GetCounters("GPU Engine",
                                                                       "Utilization Percentage",
                                                                       "engtype_3D");

                    // See https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.performancecounter.nextvalue?view=dotnet-plat-ext-6.0#remarks
                    // If the calculated value of a counter depends on two counter reads, the first
                    // read operation returns 0.0. The recommended delay time between calls to the
                    // NextValue method is one second
                    utilization.ForEach(x => x.NextValue());
                    await Task.Delay(_systemInfoRefreshTime).ConfigureAwait(false);

                    usage = (int)utilization.Sum(x => x.NextValue());
                }
                else if (SystemName == "Raspberry Pi")
                {
                    ulong maxFreq = 0;
                    ulong freq    = 0;

                    string args = "get_config core_freq";
                    var pattern = @"=(?<maxFreq>\d+)";
                    var results = await GetProcessInfoAsync("vcgencmd", args, pattern).ConfigureAwait(false);
                    if ((results?.Count ?? 0) > 0 && ulong.TryParse(results!["maxFreq"], out maxFreq))
                        maxFreq *= 1_000_000;

                    args    = "measure_clock core";
                    pattern = @"=(?<freq>\d+)";
                    results = await GetProcessInfoAsync("vcgencmd", args, pattern).ConfigureAwait(false);
                    if ((results?.Count ?? 0) > 0)
                        ulong.TryParse(results!["freq"], out freq);

                    if (maxFreq > 0)
                        usage = (int)(freq * 100 / maxFreq);
                }
                else if (HardwareVendor == "NVIDIA Jetson")
                {
                    // NVIDIA card, so we won't even reach here
                }
                else if (IsLinux) // must come after Pi, Jetson
                {
                    // ...
                }
                else if (IsMacOS)
                {
                    // macOS doesn't provide non-admin access to GPU info
                }
            }
            catch
            {
            }

            return usage;
        }

        /// <summary>
        /// Gets the amount of GPU memory currently in use
        /// </summary>
        /// <returns>A long representing bytes</returns>
        public async static ValueTask<ulong> GetGpuMemoryUsageAsync()
        {
            CheckMonitoringStarted();

            // NVIDIA cards are continuously monitored. Grab the latest and go.
            NvidiaInfo? gpuInfo = GPU as NvidiaInfo;
            if (gpuInfo is not null)
                return gpuInfo.MemoryUsed;

            ulong gpuMemoryUsed = 0;

            try
            {
                if (IsWindows)
                {
                    List<PerformanceCounter> counters = GetCounters("GPU Process Memory",
                                                                    "Dedicated Usage", null);
                    gpuMemoryUsed = (ulong)counters.Sum(x => (long)x.NextValue());
                }

                else if (SystemName == "Raspberry Pi")
                {
                    /*
                    vcgencmd get_mem <type>
                    Where type is:
                        arm: total memory assigned to arm (incorrect on systems > 16GB)
                        gpu: total memory assigned to gpu
                        malloc_total: total memory assigned to gpu malloc heap
                        malloc: free gpu memory in malloc heap
                        reloc_total: total memory assigned to gpu relocatable heap
                        reloc: free gpu memory in relocatable heap
                    */

                    string args = "get_mem malloc_total";
                    var pattern = @"=(?<memused>\d+)";
                    var results = await GetProcessInfoAsync("vcgencmd", args, pattern).ConfigureAwait(false);
                    if ((results?.Count ?? 0) > 0 && ulong.TryParse(results!["memused"], out ulong memUsed))
                        gpuMemoryUsed = memUsed * 1024 * 1024;
                }
                else if (SystemName == "NVIDIA Jetson")
                {
                    // NVIDIA card, so we won't even reach here
                }
                else if (IsLinux) // must come after Pi, Jetson
                {
                    // ...
                }
                else if (IsMacOS)
                {
                    // macOS doesn't provide non-admin access to GPU info
                }
            }
            catch 
            {
            }

            return gpuMemoryUsed;
        }

        public static GpuInfo? GetGpuInfo()
        {
            if (Architecture == "Arm64" && IsMacOS)
            {
                return new GpuInfo
                {
                    Name           = "Apple Silicon",
                    HardwareVendor = "Apple"
                };
            }

            // We may already have a GPU object set via the continuous NVIDIA monitoring.
            GpuInfo? gpu = GPU;
            if (gpu is null)
            {
                foreach (var videoController in _hardwareInfo.VideoControllerList)
                {
                    if (string.IsNullOrWhiteSpace(videoController.Manufacturer))
                        continue;

                    gpu = new GpuInfo
                    {
                        Name           = videoController.Name,
                        HardwareVendor = videoController.Manufacturer,
                        DriverVersion  = videoController.DriverVersion,
                        TotalMemory    = videoController.AdapterRAM
                    };

                    break;
                }
            }

            return gpu;
        }

        /// <summary>
        /// A task that constantly updates the GPU usage for NVIDIA cards in the background. Note
        /// that this method ONLY monitors NVIDIA cards. If there is no NVIDIA card it will return.
        /// 
        /// This method sets the static GPU object with whatever it finds, which is then used in 
        /// methods like GetGpuUsage. If there's no NVIDIA card, GetGpuUsage will use the NVIDIA
        /// info, otherwise it will fall through and perform whatever platform specific lookups it
        /// needs to in order to get the non-NVIDIA info it needs.
        /// </summary>
        /// <returns>A Task</returns>
        private static async Task MonitorNvidiaGpuUsageAsync()
        {
            while (true)
            {
                GpuInfo? gpuInfo;
                if (HardwareVendor == "NVIDIA Jetson")
                    gpuInfo = await ParseJetsonTegraStatsAsync().ConfigureAwait(false);
                else
                    gpuInfo = await ParseNvidiaSmiAsync().ConfigureAwait(false);

                if (gpuInfo is not null)
                    GPU = gpuInfo;

                if (HasNvidiaGPU == false)
                    break;

                await Task.Delay(_nvidiaInfoRefreshTime).ConfigureAwait(false);
            }
        }

        private static void CheckMonitoringStarted()
        {
            if ( !IsResourceUsageMonitoring && !_monitoringStartedWarningIssued)
            {
                _monitoringStartedWarningIssued = true;

                // Setting both fore and background colour so this is save in Light and Dark mode.
                ConsoleColor oldForeColour = Console.ForegroundColor;
                ConsoleColor oldBackColour = Console.BackgroundColor;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;

                Console.WriteLine("Warning: To monitor CPU and GPU resource usage you must call SystemInfo.Initialize");

                Console.ForegroundColor = oldForeColour;
                Console.BackgroundColor = oldBackColour;
            }
        }        

        /// <summary>
        /// A task that constantly updates the System usage (CPU and memory) in the background
        /// TODO: break this into classes for each OS
        /// </summary>
        /// <returns>A Task</returns>
        private static async Task MonitorSystemUsageAsync()
        {
            if (IsWindows)
            {
                var cpuIdleCounter = new PerformanceCounter("Processor", "% Idle Time", "_Total");

                var idleOld = (int)cpuIdleCounter.NextValue();
                while (true)
                {
                    // CPU%
                    await Task.Delay(_systemInfoRefreshTime).ConfigureAwait(false);
                    var idle  = (int)cpuIdleCounter.NextValue();

                    // Take the average of previous and current measurements
                    _cpuUsage = 100 - (idle + idleOld) / 2;
                    idleOld   = idle;

                    // Memory Info
                    await GetMemoryInfoAsync().ConfigureAwait(false);
                }
            }
            else if (HardwareVendor == "NVIDIA Jetson")
            {
                // Jetson board is continuously monitored, so nothing to do here
            }
            else if (IsLinux)
            {
                while (true)
                {
                    // Easier but not yet tested
                    // _hardwareInfo.RefreshCPUList(true);
                    // int usage = (int) _hardwareInfo.CpuList.Average(cpu => (float)cpu.PercentProcessorTime);

                    // Output is in the form:
                    // top - 08:38:12 up  1:20,  0 users,  load average: 0.00, 0.00, 0.00
                    // Tasks:   5 total,   1 running,   4 sleeping,   0 stopped,   0 zombie
                    // %Cpu(s):  0.0 us,  0.0 sy,  0.0 ni, ... <-- this line, sum of values 1-3

                    var results = await GetProcessInfoAsync("/bin/bash", "-c \"top -b -n 1\"").ConfigureAwait(false);
                    if ((results?.Count ?? 0) > 0)
                    {
                        var lines = results!["output"]?.Split("\n");

                        if (lines is not null && lines.Length > 2)
                        {
                            string pattern = @"(?<userTime>[\d.]+)\s*us,\s*(?<systemTime>[\d.]+)\s*sy,\s*(?<niceTime>[\d.]+)\s*ni";
                            Match match = Regex.Match(lines[2], pattern, RegexOptions.ExplicitCapture);
                            var userTime   = match.Groups["userTime"].Value;
                            var systemTime = match.Groups["systemTime"].Value;
                            var niceTime   = match.Groups["niceTime"].Value;

                            _cpuUsage = (int)(float.Parse(userTime) + float.Parse(systemTime) + float.Parse(niceTime));
                        }
                    }

                    // Memory Info
                    await GetMemoryInfoAsync().ConfigureAwait(false);

                    await Task.Delay(_systemInfoRefreshTime).ConfigureAwait(false);
                }
            }
            else if (IsMacOS)
            {
                while (true)
                {
                    // oddly, hardware.info hasn't yet added CPU usage for macOS

                    // Output is in the form:
                    // CPU usage: 12.33% user, 13.63% sys, 74.2% idle 
                    string pattern = @"CPU usage:\s+(?<userTime>[\d.]+)%\s+user,\s*(?<systemTime>[\d.]+)%\s*sys,\s*(?<idleTime>[\d.]+)%\s*idle";
                    var results = await GetProcessInfoAsync("/bin/bash",  "-c \"top -l 1 | grep -E '^CPU'\"",
                                                            pattern).ConfigureAwait(false);
                    if ((results?.Count ?? 0) > 0)
                        _cpuUsage = (int)(float.Parse(results!["userTime"]) + float.Parse(results["systemTime"]));

                    // Memory Info
                    await GetMemoryInfoAsync().ConfigureAwait(false);

                    await Task.Delay(_systemInfoRefreshTime).ConfigureAwait(false);
                }
            }
            else
            {
                Console.WriteLine("WARNING: Getting CPU usage for unknown OS: " + OperatingSystem);
                await Task.Delay(0).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Returns information on the first NVIDIA GPU found on the Jetson boards using the
        /// tegrastats utility. If an NVIDIA card is found, the GPU property of this class will
        /// contain the info we found. Otherwise, this method will return null and GPU will
        /// remain unchanged.
        /// </summary>
        /// <returns>A nullable NvidiaInfo object</returns>
        private async static ValueTask<NvidiaInfo?> ParseJetsonTegraStatsAsync()
        {
            if (HasNvidiaGPU == false)
                return null;

            NvidiaInfo? gpu = null;

            try
            {
                // Get CUDA version
                // cat /usr/local/cuda/version.txt
                // Output: CUDA Version 10.2.89
                string cudaVersion = string.Empty;
                string pattern     = @"CUDA Version (?<cudaVersion>[\d.]+)";
                var results = await GetProcessInfoAsync("/bin/bash",  "-c \"cat /usr/local/cuda/version.txt\"",
                                                   pattern).ConfigureAwait(false);
                if ((results?.Count ?? 0) > 0)
                    cudaVersion = results!["cudaVersion"];

                // Get hardware stats
                var info = new ProcessStartInfo("tegrastats");
                info.RedirectStandardOutput = true;

                using var process = Process.Start(info);
                if (process?.StandardOutput is null)
                    return null;

                // We just need one line
                string? output = await process.StandardOutput.ReadLineAsync()
                                                             .ConfigureAwait(false);
                process.Kill();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    // format out output is
                    // RAM 2893/3956MB (lfb 5x2MB) SWAP 233/1978MB (cached 2MB) CPU [21%@102,15%@102,21%@102,19%@102]
                    //   EMC_FREQ 0% GR3D_FREQ 0% PLL@18C CPU@20.5C PMIC@100C GPU@20C AO@26C thermal@20C
                    //   POM_5V_IN 2056/2056 POM_5V_GPU 40/40 POM_5V_CPU 161/161
                    pattern = @"RAM (?<memUsed>\d+?)/(?<memTotal>\d+?).*CPU \[(?<cpuUsage>\d+?)%.*GR3D_FREQ (?<gpuUsage>\d+?)%";
                    Match valueMatch = Regex.Match(output, pattern, RegexOptions.ExplicitCapture);

                    ulong.TryParse(valueMatch.Groups[1].Value, out ulong memoryUsedMiB);
                    ulong.TryParse(valueMatch.Groups[2].Value, out ulong totalMemoryMiB);
                    int.TryParse(valueMatch.Groups[3].Value, out _cpuUsage);
                    int.TryParse(valueMatch.Groups[4].Value, out int gpuUsage);

                    gpu = new NvidiaInfo
                    {
                        Name                  = "NVIDIA Jetson",
                        DriverVersion         = "",
                        CudaVersionCapability = cudaVersion,
                        CudaVersionInstalled  = cudaVersion,
                        Utilization           = gpuUsage,
                        MemoryUsed            = memoryUsedMiB * 1024UL * 1024UL,
                        TotalMemory           = totalMemoryMiB * 1024UL * 1024UL,
                        ComputeCapacity       = JetsonComputeCapability(HardwareVendor),
                    };                    
                }
            }
            catch (Exception ex)
            {
                _hasNvidiaCard = false;
                Debug.WriteLine(ex.ToString());
                return null;
            }

            return gpu;
        }

        /// <summary>
        /// Returns information on the first NVIDIA GPU found (TODO: Extend to multiple GPUs). If an
        /// NVIDIA card is found, the GPU property of this class will contain the info we found. 
        /// Otherwise, this method will return null and GPU will remain unchanged.
        /// </summary>
        /// <returns>A nullable NvidiaInfo object</returns>
        private async static ValueTask<NvidiaInfo?> ParseNvidiaSmiAsync()
        {
            // It would be nice to know if an NVIDIA card exists before calling the nvidia-smi
            // utility in order to avoid unnecessary exception and error output. We could query
            // _hardwareInfo.VideoControllerList and see if one contains "NVIDIA", but that won't
            // work under docker. So, we can try looking for the nvidia-smi command directly.
            // ** TO BE TESTED **
            // if (_hasNvidiaCard is null)
            //    _hasNvidiaCard = CheckCommandExists("nvidia-smi");

            if (HasNvidiaGPU == false)
                return null;

            // Quick test for NVidia card
            if (SystemName == "Raspberry Pi" || SystemName == "Orange Pi" || IsMacOS)
            {
                _hasNvidiaCard = false;
                return null;
            }

            NvidiaInfo? gpu = null;

            try
            {
                string gpuName              = string.Empty;
                string driverVersion        = string.Empty;
                string computeCapacity      = string.Empty;
                string cudaVersion          = string.Empty;
                string cudaVersionInstalled = string.Empty;
                ulong  memoryFreeMiB        = 0;
                ulong  totalMemoryMiB       = 0;
                int    gpuUtilPercent       = 0;

                // Example call and response
                // nvidia-smi --query-gpu=count,name,driver_version,memory.total,memory.free,utilization.gpu,compute_cap --format=csv,noheader
                // 1, NVIDIA GeForce RTX 3060, 512.96, 12288 MiB, 10473 MiB, 4 %, 8.6
                // BUT WAIT: For old cards we don't get compute_cap. So we break this up.

                string args    = "--query-gpu=count,name,driver_version,memory.total,memory.free,utilization.gpu --format=csv,noheader";
                string pattern = @"\d+,\s+(?<gpuname>.+?),\s+(?<driver>[\d.]+),\s+(?<memtotal>\d+)\s*MiB,\s+(?<memfree>\d+)\s*MiB,\s+(?<gpuUtil>\d+)\s*%";
                var results    = await GetProcessInfoAsync("nvidia-smi", args, pattern).ConfigureAwait(false);

                // Failure to run nvidia-smi = no NVIDIA card installed
                if (results is null)
                {
                    _hasNvidiaCard = false;
                    return null;
                }

                if (results.Count > 0)
                {
                    gpuName       = results["gpuname"];
                    driverVersion = results["driver"];
                    ulong.TryParse(results["memfree"], out memoryFreeMiB);
                    ulong.TryParse(results["memtotal"], out totalMemoryMiB);
                    int.TryParse(results["gpuUtil"], out gpuUtilPercent);
                }

                ulong memoryUsedMiB = totalMemoryMiB - memoryFreeMiB;

                // Get Compute Capability if we can
                // nvidia-smi --query-gpu=compute_cap --format=csv,noheader
                // 8.6
                args    = "--query-gpu=compute_cap --format=csv,noheader";
                pattern = @"(?<computecap>[\d\.]*)";
                results = await GetProcessInfoAsync("nvidia-smi", args, pattern).ConfigureAwait(false);
                if ((results?.Count ?? 0) > 0)
                    computeCapacity = results!["computecap"];

                // Get CUDA info. Output is in the form:
                //  Thu Dec  8 08:45:30 2022
                //  +-----------------------------------------------------------------------------+
                //  | NVIDIA-SMI 512.96       Driver Version: 512.96       CUDA Version: 11.6     |
                //  |-------------------------------+----------------------+----------------------+
                pattern = @"Driver Version:\s+(?<driver>[\d.]+)\s*CUDA Version:\s+(?<cuda>[\d.]+)";
                results = await GetProcessInfoAsync("nvidia-smi", "", pattern).ConfigureAwait(false);
                if ((results?.Count ?? 0) > 0)
                    cudaVersion = cudaVersionInstalled = results!["cuda"];


                // Get actual installed CUDA info. Form is:
                //  nvcc: NVIDIA (R) Cuda compiler driver
                //  Copyright (c) 2005-2022 NVIDIA Corporation
                //  Built on Tue_May__3_19:00:59_Pacific_Daylight_Time_2022
                //  Cuda compilation tools, release 11.7, V11.7.64
                //  Build cuda_11.7.r11.7/compiler.31294372_0
                pattern = @"Cuda compilation tools, release [\d.]+, V(?<cuda>[\d.]+)";
                results = await GetProcessInfoAsync("nvcc", "--version", pattern).ConfigureAwait(false);
                if ((results?.Count ?? 0) > 0)
                    cudaVersionInstalled = results!["cuda"];

                // If we've reached this point we definitely have an NVIDIA card.
                _hasNvidiaCard = true;

                gpu = new NvidiaInfo
                {
                    Name                  = gpuName,
                    DriverVersion         = driverVersion,
                    CudaVersionCapability = cudaVersion,
                    CudaVersionInstalled  = cudaVersionInstalled,
                    Utilization           = gpuUtilPercent,
                    MemoryUsed            = memoryUsedMiB * 1024 * 1024,
                    TotalMemory           = totalMemoryMiB * 1024 * 1024,
                    ComputeCapacity       = computeCapacity,
                };
            }
            catch (Exception/* ex */)
            {
                _hasNvidiaCard = false;
#if DEBUG
                // Debug.WriteLine(ex.ToString());
#endif
                return null;
            }

            // We need to be careful. In between us setting GPU here, another call to GetGpuInfo may
            // have set GPU to null or GpuInfo (Sure, I can't actually picture how, but it's possible)
            // return GPU as NvidiaInfo;
            return gpu;
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

        private static async Task GetMemoryInfoAsync()
        {
            ulong memoryTotal = 0;
            ulong memoryFree  = 0;
            ulong memoryUsed  = 0;

            if (IsWindows)
            {
                /*
                // An alternative to using _hardwareInfo
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                lock (Memory)
                {
                    memoryTotal = (ulong)gcMemoryInfo.TotalAvailableMemoryBytes;
                    memoryUsed  = (ulong)gcMemoryInfo.MemoryLoadBytes;
                    memoryFree  = memoryTotal - memoryUsed;
                }
                */
                _hardwareInfo.RefreshMemoryStatus();
    
                memoryFree  = _hardwareInfo?.MemoryStatus?.AvailablePhysical ?? 0;
                memoryTotal = _hardwareInfo?.MemoryStatus?.TotalPhysical ?? 0;
                memoryUsed  = memoryTotal - memoryFree;
            }
            else if (IsLinux)
            {
                // Not tested (maybe?)
                // _hardwareInfo.RefreshMemoryStatus();
                // memoryFree  = _hardwareInfo?.MemoryStatus?.AvailablePhysical ?? 0;
                // memoryTotal = _hardwareInfo?.MemoryStatus?.TotalPhysical ?? 0;
                // memoryUsed  = memoryTotal - memoryFree;

                // Output is in the form:
                //       total used free
                // Mem:    XXX  YYY  ZZZ <- We want tokens 1 - 3 from this line
                // Swap:   xxx  yyy  zzz

                var results = await GetProcessInfoAsync("/bin/bash",  "-c \"free -b\"").ConfigureAwait(false);
                if ((results?.Count ?? 0) > 0)
                {
                    var lines = results!["output"]?.Split("\n");
                    if (lines is not null && lines.Length > 1)
                    {
                        var memory = lines[1].Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        memoryTotal = ulong.Parse(memory[1]);
                        memoryUsed  = ulong.Parse(memory[2]);
                        memoryFree  = ulong.Parse(memory[3]);
                    }
                }
            }
            else if (IsMacOS)
            {
                // _hardwareInfo returns bogus data on macOS
                // You can try sysctl hw.memsize hw.physmem hw.usermem
                // But it returns (without commas - added for readability):
                //  hw.memsize: 17,179,869,184
                //  hw.physmem:  2,147,483,648
                //  hw.usermem:  1,392,398,336
                // On a 16Gb machine. memsize and usermem are fine, but everything else bad

                // This seems to return proper total memory
                _hardwareInfo.RefreshMemoryStatus();
                memoryTotal = _hardwareInfo?.MemoryStatus?.TotalPhysical ?? 0;

                // and this returns used
                string args = "-c \"ps -caxm -orss= | awk '{ sum += $1 } END { print sum * 1024 }'\"";
                var results = await GetProcessInfoAsync("/bin/bash", args, "(?<memused>[\\d.]+)").ConfigureAwait(false);
                if ((results?.Count ?? 0) > 0)
                    ulong.TryParse(results!["memused"], out memoryUsed);

                memoryFree = memoryTotal - memoryUsed;
            }
            else
            {
                Console.WriteLine("WARNING: Getting memory usage for unknown OS: " + OperatingSystem);
            }

            lock (Memory)
            {
                Memory.Total     = memoryTotal;
                Memory.Used      = memoryUsed;
                Memory.Available = memoryFree;
            }
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

                    if (Architecture == "Arm64" && OperatingSystem == "macOS")
                        cpuInfo.HardwareVendor = "Apple";
                    else if (cpu.Name.Contains("Raspberry"))
                        cpuInfo.HardwareVendor = "Raspberry Pi";
                    else if (cpu.Name.Contains("Jetson"))
                        cpuInfo.HardwareVendor = "NVIDIA";
                    else if (cpu.Name.Contains("Intel"))
                        cpuInfo.HardwareVendor = "Intel";
                    else if (cpu.Name.Contains("AMD"))
                        cpuInfo.HardwareVendor = "AMD";
                    else if (SystemName == "Orange Pi")
                        cpuInfo.HardwareVendor = "Rockchip";

                    cpus.Add(cpuInfo);
                }
            }

            // There's obviously at least 1.
            if (cpus.Count == 0)
                cpus.Add(new CpuInfo() { NumberOfCores = 1, LogicalProcessors = 1 });

            return cpus;
        }

        private async static Task CheckForWslAsync() 
        {
            _isWSL = false;

            if (IsLinux)
            {
                // Output is in the form:
                // Linux MachineName 5.15.90.1-microsoft-standard-WSL2 #1 SMP Fri Jan 27 02:56:13...
                var results = await GetProcessInfoAsync("/bin/bash", "-c \"uname -a\"", null).ConfigureAwait(false);
                if (results is not null)
                    _isWSL = results["output"]?.ContainsIgnoreCase("-microsoft-standard-WSL") == true;
            }
        }

        private static void InitSummary()
        {
            Summary = new
            {
                Host = new
                {
                    SystemName      = SystemName,
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
                                                Vendor          = GPU.HardwareVendor,
                                                Memory          = GPU.TotalMemory,
                                                DriverVersion   = GPU.DriverVersion,
                                                CUDAVersion     = (GPU as NvidiaInfo)?.CudaVersionCapability,
                                                ComputeCapacity = (GPU as NvidiaInfo)?.ComputeCapacity
                                            }
                                         : new {
                                                Name            = GPU.Name,
                                                Vendor          = GPU.HardwareVendor,
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
        /// <param name="options">The regex options</param>
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
        private async static ValueTask<Dictionary<string, string>?> GetProcessInfoAsync(string command,
                                                                                        string args,
                                                                                        string? pattern = null,
                                                                                        RegexOptions options = RegexOptions.None)
        {
            // We always need this
            options |= RegexOptions.ExplicitCapture;

            var values = new Dictionary<string, string>();

            try
            {
                var info = new ProcessStartInfo(command, args);
                info.RedirectStandardOutput = true;

                using var process = Process.Start(info);
                if (process?.StandardOutput is null)
                    return values;

                string? output = await process.StandardOutput.ReadToEndAsync()
                                                             .ConfigureAwait(false) ?? string.Empty;

                // Raw output
                values["output"] = output;

                // Extracted values
                if (!string.IsNullOrEmpty(pattern))
                {
                    // Match values in the output against the pattern
                    Match valueMatch = Regex.Match(output, pattern, options);

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
#if DEBUG
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
#else
            catch
            {
                return null;
            }
#endif
            return values;
        }

        private static bool CheckCommandExists(string command)
        {
            try
            {
                string? paths = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrWhiteSpace(paths))
                    return File.Exists(command) || File.Exists(command + ".exe") || File.Exists(command + ".bat");

                string[] pathDirs = paths.Split(Path.PathSeparator);

                foreach (string pathDir in pathDirs)
                {
                    string commandPath = Path.Combine(pathDir, command);
                    
                    // Check if the file exists and is executable
                    if (File.Exists(commandPath) || 
                        File.Exists(commandPath + ".exe") || 
                        File.Exists(commandPath + ".bat"))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                Console.WriteLine($"Error checking for {command}: {ex.Message}");
                return false;
            }
        }

        private static string JetsonComputeCapability(string hardwareName)
        {
            return hardwareName switch  
            {  
                "Jetson AGX Orin"   => "8.7",
                "Jetson Orin NX"    => "8.7",
                "Jetson Orin Nano"  => "8.7",
                "Jetson AGX Xavier" => "7.2",
                "Jetson Xavier NX"  => "7.2",
                "Jetson TX2"        => "6.2",
                "Jetson Nano"       => "5.3",
                _ => "5.3"  
            };  
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility