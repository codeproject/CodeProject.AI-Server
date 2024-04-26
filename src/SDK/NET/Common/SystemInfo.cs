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
        /// Gets or sets GPU utilisation as a percentage between 0 and 100
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

        /// <summary>
        /// The version of the cuDNN libraries that are installed
        /// </summary>
        public string? CuDNNVersionInstalled { get; set; }

        /// <summary>
        /// The compute capability of this card
        /// </summary>
        public string? ComputeCapability { get; internal set; }

        /// <summary>
        /// The string representation of this object
        /// </summary>
        public override string Description
        {
            get
            {
                var info = base.Description
                         + $", CUDA: {CudaVersionInstalled} (up to: {CudaVersionCapability})"
                         + ", Compute: " + ComputeCapability;

                if (CudaVersionInstalled is not null)
                    info += ", cuDNN: " + CuDNNVersionInstalled;

                return info;
            }
        }
    }

    /// <summary>
    /// Represents the runtimes that have been discovered
    /// </summary>
    public class Runtimes
    {
        /// <summary>
        /// Gets or sets the default Python install
        /// </summary>
        public string DefaultPython      { get; set; } = "Not found";

        /// <summary>
        /// Gets or sets the default .NET runtime (not SDK) on the system
        /// </summary>
        public string DotNetRuntime      { get; set; } = "Not found";

        /// <summary>
        /// Gets or sets the default .NET SDK on the system
        /// </summary>
        public string DotNetSDK          { get; set; } = "Not found";

        /// <summary>
        /// Gets or sets the version of Go installed
        /// </summary>
        public string Go                 { get; set; } = "Not found";

        /// <summary>
        /// Gets or sets the currently active installation of nodejs
        /// </summary>
        public string NodeJS             { get; set; } = "Not found";

        /// <summary>
        /// Gets or sets the current version of Rust
        /// </summary>
        public string Rust               { get; set; } = "Not found";
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
        private static HardwareInfo? _hardwareInfo;
        private static bool?         _isDevelopment;
        private static bool?         _hasNvidiaCard;
        private static string?       _cuDnnVersion;
        private static bool          _isWSL;
        private static string?       _dockerContainerId;
        private static string?       _osVersion;
        private static string?       _osName;
        private static bool          _isSSH;
        private static Runtimes      _runtimes = new Runtimes();

        private static TimeSpan      _nvidiaInfoRefreshTime = TimeSpan.FromSeconds(10);
        private static TimeSpan      _systemInfoRefreshTime = TimeSpan.FromSeconds(1);

        private static Task?         _monitorSystemUsageTask;
        private static Task?         _monitoryGpuUsageTask;
        private static bool          _monitoringStartedWarningIssued;
        private static int           _cpuUsage;
        private static string?       _hardwareVendor;
        private static string?       _edgeDevice;
        private static string?       _jetsonComputeCapability;

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
                if (IsDevelopmentCode
                    // Really should use the IHostEnvironment.IsDevelopment method, but needs a
                    // reference to IHostEnvironment.
                    // We also can't use these because if VSCode is running, these are set. This
                    // messes up production installs.
                    // || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").EqualsIgnoreCase(Constants.Development) ||
                    // || Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT").EqualsIgnoreCase(Constants.Development)
                )
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
        /// Returns the current machine name
        /// </summary>
        public static string MachineName
        {
            get { return Environment.MachineName; }
        }

        /// <summary>
        /// Gets the current Architecture (specifically x64, Arm or Arm64).
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
        /// Linux-Arm64, and for specific devices it will be the device name (RaspberryPi, OrangePi,
        /// RadxaROCK, Jetson). Note that x64 won't have a suffix.
        /// </summary>
        public static string Platform
        {
            get
            {
                // NOTE: Docker running on RPi, Orange Pi etc will report EdgeDevice as Raspberry Pi,
                //       Orange Pi etc.            
                if (EdgeDevice != string.Empty)
                    return EdgeDevice.Replace(" ", string.Empty);

                return OSAndArchitecture;
            }
        }

        /// <summary>
        /// Gets the name of the "system" under which we're running. System is a very generic term
        /// to reflect the sorts of things we need to know when a user has problems. "What system
        /// are you running under" typically means we want to know if they are using Docker, running
        /// Windows native, macOS on an M3 chip, or maybe they are on a Jetson. This is different 
        /// from the underlying <see cref="OperatingSystem"> or <see cref="EdgeDevice"/>.
        /// </summary>
        public static string SystemName
        {
            get
            {
                if (IsDocker)
                    return "Docker";

                if (IsWSL)
                    return $"WSL";
                   
                // NOTE: Docker running on RPi, Orange Pi etc will report EdgeDevice as Raspberry Pi,
                // Orange Pi etc.
                if (EdgeDevice != string.Empty)
                    return EdgeDevice;

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
        /// Gets the ID of the current docker container under which this app is running, or 
        /// string.Empty if no container found.
        /// </summary>
        public static string DockerContainerId
        {
            get { return _dockerContainerId ?? string.Empty; }
        }

        /// <summary>
        /// Gets a value indicating whether we are currently running in an SSH shell.
        /// </summary>
        public static bool IsSSH
        {
            get { return _isSSH; }
        }

        /// <summary>
        /// Gets the name of the edge device. Specifically, we get the name of the device if it is
        /// a Raspberry Pi, Orange Pi or NVIDIA Jetson. All other devices (at this stage) will
        /// return the empty string.
        /// </summary>
        public static string EdgeDevice
        {
            get
            {
                if (_edgeDevice is not null)
                    return _edgeDevice;

                _edgeDevice = string.Empty;

                if (IsLinux)
                {
                    try
                    {
                        // NOTE: Docker will map the sys folder, so in Docker, Raspberry Pi etc will
                        // be reported if running on an RPi (etc).

                        string modelInfo = File.ReadAllText/*Async*/("/sys/firmware/devicetree/base/model");
                        if (modelInfo.ContainsIgnoreCase("Raspberry Pi"))
                            _edgeDevice = "Raspberry Pi";
                        else if (modelInfo.ContainsIgnoreCase("Orange Pi"))
                            _edgeDevice = "Orange Pi";
                        else if (modelInfo.ContainsIgnoreCase("Radxa ROCK"))
                            _edgeDevice = "Radxa ROCK";
                        else if (modelInfo.ContainsIgnoreCase("Jetson"))
                            _edgeDevice = "Jetson";
                    }
                    catch {}
                }

                return _edgeDevice;
            }
        }

        /// <summary>
        /// Gets the hardware vendor of the current system. SPECIFICALLY: This is actually the
        /// vendor who made the CPU, not the machine itself.
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
                        // NOTE: Docker will map the sys folder, so in Docker, Raspberry Pi will be
                        //       reported if running on an RPi.

                        // string cpuInfo = File.ReadAllText("/proc/cpuinfo"); - no good for Orange Pi
                        string cpuInfo = File.ReadAllText/*Async*/("/sys/firmware/devicetree/base/model");
                        if (cpuInfo.ContainsIgnoreCase("Raspberry Pi"))
                            _hardwareVendor = "Raspberry Pi";
                        else if (cpuInfo.ContainsIgnoreCase("Orange Pi"))
                            _hardwareVendor = "Rockchip";
                        else if (cpuInfo.ContainsIgnoreCase("Radxa ROCK"))
                            _hardwareVendor = "Rockchip";
                        else if (cpuInfo.ContainsIgnoreCase("Jetson"))
                            _hardwareVendor = "NVIDIA";
                    }
                    catch {}

                    /*
                    if (_hardwareVendor is null)
                    {
                        try
                        {
                            string cpuInfo = File.ReadAllText("/proc/device-tree/model");
                            if (cpuInfo.Contains("Jetson"))
                                _hardwareVendor = "NVIDIA";
                        }
                        catch {}
                    }
                    */
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
                // WSL is Linux, so this isn't needed since the line below will handle it and do the
                // same thing. However, it's here because we may want to report this differently.
                if (IsWSL)
                    return $"{_osName} {_osVersion}";

                if (IsLinux)
                    return $"{_osName} {_osVersion}";

                // See https://github.com/getsentry/sentry-dotnet/issues/1484. 
                // C'mon guys: technically the version may be 10.x, but stick to the branding that
                // the rest of the world understands.
                if (IsWindows &&
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
            get { return _osVersion ?? string.Empty; }
        }

        /// <summary>
        /// Returns the runtimes that have been discovered on this system
        /// </summary>
        public static Runtimes Runtimes { get; } = new Runtimes();

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
                // If there is a corrupt WMI on Windows then HardwareInfo() throws.
                _hardwareInfo = new HardwareInfo();
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
            await GetDockerContainerIdAsync().ConfigureAwait(false);
            await CheckOSVersionNameAsync().ConfigureAwait(false);
            await CheckForSshAsync().ConfigureAwait(false);
            await GetcuDNNVersionAsync().ConfigureAwait(false);

            await GetRuntimesAsync().ConfigureAwait(false);

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

            if (IsWSL)
                info.AppendLine($"System:           {SystemName} ({RuntimeInformation.OSDescription})");
            else if (IsDocker)
                info.AppendLine($"System:           {SystemName} ({DockerContainerId})");
            else
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
                info.AppendLine($"GPU (Primary):    {gpuDesc}");
            }

            if (Memory is not null)
                info.AppendLine($"System RAM:       {FormatSizeBytes(Memory.Total, 0)}");

            info.AppendLine($"Platform:         {Platform}");
            info.AppendLine($"BuildConfig:      {BuildConfig}");
            if (SystemInfo.IsSSH)
                info.AppendLine($"Execution Env:    {ExecutionEnvironment} (SSH)");
            else
                info.AppendLine($"Execution Env:    {ExecutionEnvironment}");
            info.AppendLine($"Runtime Env:      {RuntimeEnvironment}");
            info.AppendLine("Runtimes installed:");
            info.AppendLine($"  .NET runtime:     {Runtimes.DotNetRuntime}");
            info.AppendLine($"  .NET SDK:         {Runtimes.DotNetSDK}");
            info.AppendLine($"  Default Python:   {Runtimes.DefaultPython}");
            info.AppendLine($"  Go:               {Runtimes.Go}");
            info.AppendLine($"  NodeJS:           {Runtimes.NodeJS}");
            info.AppendLine($"  Rust:             {Runtimes.Rust}");

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
                else if (EdgeDevice == "Raspberry Pi")
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
                else if (EdgeDevice == "Jetson")
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

                else if (EdgeDevice == "Raspberry Pi")
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
                else if (EdgeDevice == "Jetson")
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
            if (gpu is null && _hardwareInfo is not null)
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
                if (EdgeDevice == "Jetson")
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
            else if (EdgeDevice == "Jetson")
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
                        CuDNNVersionInstalled = _cuDnnVersion,
                        Utilization           = gpuUsage,
                        MemoryUsed            = memoryUsedMiB * 1024UL * 1024UL,
                        TotalMemory           = totalMemoryMiB * 1024UL * 1024UL,
                        ComputeCapability     = JetsonComputeCapability(),
                    };                    
                }
            }
            catch (Exception ex)
            {
                _hasNvidiaCard = false;
                Debug.WriteLine("Error getting Jetson hardware status: " + ex.ToString());
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
            if (EdgeDevice == "Raspberry Pi" || EdgeDevice == "Orange Pi" || 
                EdgeDevice == "Radxa ROCK" || IsMacOS)
            {
                _hasNvidiaCard = false;
                return null;
            }

            NvidiaInfo? gpu = null;

            try
            {
                string gpuName              = string.Empty;
                string driverVersion        = string.Empty;
                string computeCapability    = string.Empty;
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
                    computeCapability = results!["computecap"];

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
                    CudaVersionInstalled  = cudaVersion,
                    CuDNNVersionInstalled = _cuDnnVersion,
                    Utilization           = gpuUtilPercent,
                    MemoryUsed            = memoryUsedMiB * 1024 * 1024,
                    TotalMemory           = totalMemoryMiB * 1024 * 1024,
                    ComputeCapability     = computeCapability,
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
                if (_hardwareInfo is null)
                {
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    lock (Memory)
                    {
                        memoryTotal = (ulong)gcMemoryInfo.TotalAvailableMemoryBytes;
                        memoryUsed  = (ulong)gcMemoryInfo.MemoryLoadBytes;
                        memoryFree  = memoryTotal - memoryUsed;
                    }
                }
                else
                {
                    _hardwareInfo.RefreshMemoryStatus();
    
                    memoryFree  = _hardwareInfo?.MemoryStatus?.AvailablePhysical ?? 0;
                    memoryTotal = _hardwareInfo?.MemoryStatus?.TotalPhysical ?? 0;
                    memoryUsed  = memoryTotal - memoryFree;
                }
            }
            else if (IsLinux)
            {
                // if (_hardwareInfo is not null)
                // {
                //     _hardwareInfo.RefreshMemoryStatus();
                //     memoryFree  = _hardwareInfo?.MemoryStatus?.AvailablePhysical ?? 0;
                //     memoryTotal = _hardwareInfo?.MemoryStatus?.TotalPhysical ?? 0;
                //     memoryUsed  = memoryTotal - memoryFree;
                // }

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
                // macOS needs to be treated with some care, and _hardwareInfo returns (or returned)
                // bogus data on macOS. We'll mix and match tools to get results that make sense.

                if (_hardwareInfo is null)
                {
                    // You can try sysctl hw.memsize hw.physmem hw.usermem
                    // But it returns (commas added for readability):
                    //  hw.memsize: 17,179,869,184
                    //  hw.physmem:  2,147,483,648 (or 3,750,215,680, or ...)
                    //  hw.usermem:  1,392,398,336
                    // On a 16Gb machine. memsize and usermem are fine, but everything else bad

                    var mem = await GetProcessInfoAsync("/bin/bash", "-c \"sysctl -n hw.memsize\"",
                                                        "(?<memused>[\\d]+)").ConfigureAwait(false);
                    if ((mem?.Count ?? 0) > 0)
                        ulong.TryParse(mem!["memused"], out memoryUsed);
                }
                else
                {
                    // This seems to return proper total memory
                    _hardwareInfo.RefreshMemoryStatus();
                    memoryTotal = _hardwareInfo?.MemoryStatus?.TotalPhysical ?? 0;
                }

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
                    // TO TEST: Does the CPU name actually contain Rockchip for Orange Pi?
                    else if (cpu.Name.Contains("Rockchip"))
                        cpuInfo.HardwareVendor = "Rockchip";
                    else if (EdgeDevice == "Orange Pi")
                        cpuInfo.HardwareVendor = "Rockchip";
                    else if (EdgeDevice == "Radxa ROCK")
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
                var results = await GetProcessInfoAsync("/bin/bash", "-c \"uname -a\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _isWSL = results["output"]?.ContainsIgnoreCase("-microsoft-standard-WSL") == true;
            }
        }

        private async static Task GetDockerContainerIdAsync()
        {
            if (_dockerContainerId is not null)
                return;

            // Docker containers often have their container ID set as the hostname (which is the
            // NETBIOS name which is returned as MachineName here). We'll start with this as a defualt.
            _dockerContainerId = MachineName;

            if (IsDocker)
            {
                try 
                {
                    // Linux "usually" has a "/proc/self/cgroup" file. Except when it doesn't. This
                    // file allegedly contains the docker container ID
                    const string idFile = "/proc/self/cgroup";
                    if (File.Exists(idFile))
                    {
                        string dockerInfo = await File.ReadAllTextAsync(idFile);
                        Match match = Regex.Match(dockerInfo, @"[0-9a-f]{64}");
                        if (match.Success)
                            _dockerContainerId = match.Value;
                    }
                }
                catch
                {
                }
            }
        }

        private async static Task CheckOSVersionNameAsync()
        {
            // Default fallback
            _osName    = OperatingSystem;
            _osVersion = Environment.OSVersion.Version.Major.ToString();

            if (IsLinux)
            {
                // Output is in the form:
                var results = await GetProcessInfoAsync("/bin/bash", "-c \". /etc/os-release;echo $NAME\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _osName = results["output"]?.Trim(); // eg "ubuntu", "debian"

                results = await GetProcessInfoAsync("/bin/bash", "-c \". /etc/os-release;echo $VERSION_ID\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _osVersion = results["output"]?.Trim();    // eg. "22.04" for Ubuntu 22.04, "12" for Debian 12
            }
            else if (IsWindows)
            {
                if (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000)
                    _osVersion = "11";
            }
            else if (IsMacOS)
            {
                string command = "awk '/SOFTWARE LICENSE AGREEMENT FOR macOS/' '/System/Library/CoreServices/Setup Assistant.app/Contents/Resources/en.lproj/OSXSoftwareLicense.rtf' | awk -F 'macOS ' '{print $NF}' | awk '{print substr($0, 0, length($0)-1)}'";
                var results = await GetProcessInfoAsync("/bin/bash", $"-c \"{command}\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _osName = results["output"];  // eg. "Big Sur"

                results = await GetProcessInfoAsync("/bin/bash", "-c \"sw_vers -productVersion\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _osVersion = results["output"];    // eg."11.1" for macOS Big Sur
            }
        }

        private async static Task CheckForSshAsync()
        {
            _isSSH = false;

            if (!_isSSH && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSH_CLIENT")))
                _isSSH = true;

            if (!_isSSH && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SSH_TTY")))
                _isSSH = true;

            if (!_isSSH && !IsWindows)
            {
                var results = await GetProcessInfoAsync("/bin/bash", "-c \"ps -o comm= -p $PPID\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _isSSH = results["output"]?.ContainsIgnoreCase("sshd") == true;
            }

            if (!_isSSH && IsLinux)
            {
                var results = await GetProcessInfoAsync("/bin/bash", "-c \"pstree -s $$ | grep sshd\"", null)
                                                                                .ConfigureAwait(false);
                if (results is not null)
                    _isSSH = !string.IsNullOrWhiteSpace(results["output"]);
            }
        }

        private async static Task GetcuDNNVersionAsync()
        {
            if (IsWindows)
            {
                string paths = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

                // Windows: C:\Program Files\NVIDIA\CUDNN\v8.5\bin
                // WSL: /mnt/c/Program Files/NVIDIA/CUDNN/v8.5/bin
                
                // Match values in the output against the pattern
                Match match = Regex.Match(paths, "NVIDIA\\\\CUDNN\\\\v(?<version>\\d+.\\d+)\\\\bin",
                                        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
                if (match.Success)
                    _cuDnnVersion = match.Groups["version"].ToString();
            }
            else if (IsLinux)
            {
                string args = "-c \"dpkg -l 2>/dev/null | grep cudnn | head -n 1 | grep -oP '\\d+\\.\\d+\\.\\d+'\"";
                var results = await GetProcessInfoAsync("/bin/bash", args, null).ConfigureAwait(false);
                if (results is not null)
                    _cuDnnVersion = results["output"]?.Trim();
            }
        }

        private async static Task GetRuntimesAsync()
        {
            try
            {
                var pattern = "Microsoft.AspNetCore.App (?<version>\\d+\\.\\d+.\\d+)";
                var options = RegexOptions.IgnoreCase;
                var results = await GetProcessInfoAsync("dotnet", "--list-runtimes", pattern, options, true).ConfigureAwait(false);
                if (results is not null && results.Count > 0 && results.ContainsKey("version"))
                    Runtimes.DotNetRuntime = results!["version"];

                pattern = "(?<version>\\d+\\.\\d+.\\d+)";
                options = RegexOptions.IgnoreCase;
                results = await GetProcessInfoAsync("dotnet", "--list-sdks", pattern, options, true).ConfigureAwait(false);
                if (results is not null && results.Count > 0 && results.ContainsKey("version"))
                    Runtimes.DotNetSDK = results!["version"];

                var pythonExe = IsWindows? "python" : "python3";
                pattern = "Python (?<version>\\d+\\.\\d+.\\d+)";
                options = RegexOptions.IgnoreCase;
                results = await GetProcessInfoAsync(pythonExe, "--version", pattern, options).ConfigureAwait(false);
                if (results is not null && results.Count > 0 && results.ContainsKey("version"))
                    Runtimes.DefaultPython = results!["version"];

                pattern = "go version go(?<version>\\d+\\.\\d+\\.\\d+)";
                options = RegexOptions.IgnoreCase;
                results = await GetProcessInfoAsync("go", "version", pattern, options).ConfigureAwait(false);
                if (results is not null && results.Count > 0 && results.ContainsKey("version"))
                    Runtimes.Go = results!["version"];

                pattern = "v(?<version>\\d+\\.\\d+\\.\\d+)";
                options = RegexOptions.IgnoreCase;
                results = await GetProcessInfoAsync("node", "-v", pattern, options).ConfigureAwait(false);
                if (results is not null && results.Count > 0 && results.ContainsKey("version"))
                    Runtimes.NodeJS = results!["version"];

                pattern = "rustc (?<version>\\d+\\.\\d+\\.\\d+)";
                options = RegexOptions.IgnoreCase;
                results = await GetProcessInfoAsync("rustc", "--version", pattern, options).ConfigureAwait(false);
                if (results is not null && results.Count > 0 && results.ContainsKey("version"))
                    Runtimes.Rust = results!["version"];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to get runtime version: " + ex);
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
                                                Name              = GPU.Name,
                                                Vendor            = GPU.HardwareVendor,
                                                Memory            = GPU.TotalMemory,
                                                DriverVersion     = GPU.DriverVersion,
                                                CUDAVersion       = (GPU as NvidiaInfo)?.CudaVersionCapability,
                                                ComputeCapability = (GPU as NvidiaInfo)?.ComputeCapability
                                            }
                                         : new {
                                                Name              = GPU.Name,
                                                Vendor            = GPU.HardwareVendor,
                                                Memory            = GPU.TotalMemory,
                                                DriverVersion     = GPU.DriverVersion
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
        /// <param name="getLastMatch">If true return the last match, not the first match, in the
        /// case where <see cref="pattern"/> matches more than one line.
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
                                                                                        RegexOptions options = RegexOptions.None,
                                                                                        bool getLastMatch = false)
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
                    Match? valueMatch = null;
                    if (getLastMatch)
                    {
                        MatchCollection matches = Regex.Matches(output, pattern, options);
                        foreach (Match match in matches)
                            valueMatch = match;
                    }
                    else
                        valueMatch = Regex.Match(output, pattern, options);

                    if (valueMatch is not null)
                    {
                        // Look in the "pattern" string for "(?<name> ... )" explicit match patterns
                        // and extract the name. We'll then fill the dictionary using this name and 
                        // the value matched for that name's expression. 
                        Regex expression = new Regex(@"\(\?\<(?<matchName>([a-zA-Z_-]+))\>");
                        var captureNames = expression.Matches(pattern);

                        // For each name we found in the search pattern, get the value of the match
                        // of that name from the output string and store in the dictionary
                        foreach (Match captureNameMatch in captureNames)
                        {
                            string captureName = captureNameMatch.Groups[1].Value;
                            values[captureName] = valueMatch.Groups[captureName].Value;
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception e)
            {
                Console.WriteLine("Error in GetProcessInfoAsync: " + e.Message);
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

        private static string JetsonComputeCapability()
        {
            if (_jetsonComputeCapability is not null)
                return _jetsonComputeCapability;

            _jetsonComputeCapability = string.Empty;

            string modelInfo = File.ReadAllText/*Async*/("/sys/firmware/devicetree/base/model");
            if (modelInfo.ContainsIgnoreCase("Jetson"))
            {
                if (modelInfo.ContainsIgnoreCase("Orin"))        // Jetson AGX Orin, Jetson Orin NX, Jetson Orin Nano
                    _jetsonComputeCapability = "8.7";
                else if (modelInfo.ContainsIgnoreCase("Xavier")) // Jetson AGX Xavier, Jetson Xavier NX
                    _jetsonComputeCapability = "7.2";
                else if (modelInfo.ContainsIgnoreCase("TX2"))    // Jetson TX2
                    _jetsonComputeCapability = "6.2";
                else if (modelInfo.ContainsIgnoreCase("TX1"))    // Jetson TX1
                    _jetsonComputeCapability = "5.3";
                else if (modelInfo.ContainsIgnoreCase("TK1"))    // Jetson TK1
                    _jetsonComputeCapability = "3.2";
                else if (modelInfo.ContainsIgnoreCase("Nano"))   // Jetson Nano
                    _jetsonComputeCapability = "5.3";
            }

            return _jetsonComputeCapability;
        }
    }
}
#pragma warning restore CA1416 // Validate platform compatibility