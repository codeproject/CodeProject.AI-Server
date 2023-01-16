using System.Runtime.InteropServices;

using Hardware.Info;

namespace CodeProject.AI.SDK.Common
{
    public class SystemProperties
    {
        /// <summary>
        /// Represents the properties of the host environment.
        /// </summary>
        public class EnvironmentProperties
        {
            /// <summary>
            /// The platform (eg macOS-arm64)
            /// </summary>
            public string? Platform { get; set; }

            /// <summary>
            /// Operating system name
            /// </summary>
            public string? OSName { get; set; }

            /// <summary>
            /// Operating system version
            /// </summary>
            /// <value></value>
            public string? OSVersion { get; set; }

            /// <summary>
            /// Chip architecture (x86_64 etc)
            /// </summary>
            public string? Architecture { get; set; }

            /// <summary>
            /// Version of the .NET framework the server is running under
            /// </summary>
            public string? DotnetVersion { get; set; }

            /// <summary>
            /// The current execution environment (Docker or native)
            /// </summary>
            public string? ExcutionEnvironment { get; set; }
        };

        /// <summary>
        /// Represents the properties of all CPUs combined
        /// </summary>
        public class CpuProperties
        {
            public int Count { get; set; }
            public IList<CpuInfo>? CPUs { get; set; }
        };

        /// <summary>
        /// Represents the properties of a single CPU
        /// </summary>
        public class CpuInfo
        {
            public string? Name { get; set; }
            public uint NumberOfCores { get; set; }
            public uint ThreadCount { get; set; }
        };

        /// <summary>
        /// Represents what we know about System (non GPU) memory
        /// </summary>
        public class MemoryProperties
        {
            public ulong Total { get; set; }
            public ulong Available { get; set; }
        };

        // The underlying object that does the investigation into the properties.
        // The other properties mostly rely on this creature for their worth.
        private static HardwareInfo    _hardwareInfo = new HardwareInfo();

        public static EnvironmentProperties Host    { get; private set; }
        public static CpuProperties         CPU     { get; private set; }
        public static MemoryProperties      Memory  { get; private set; }
        public static NvidiaInfo?           Nvidia  { get; private set; }
        public static Object                Summary { get; private set; }

        static SystemProperties()
        {
            // Get all the information
            _hardwareInfo.RefreshOperatingSystem();
            _hardwareInfo.RefreshCPUList(false);
            _hardwareInfo.RefreshMemoryStatus();
            _hardwareInfo.RefreshVideoControllerList();

            Host      = GetEnvironmentInfo();
            CPU       = GetCpuInfo();
            Memory    = GetMemoryInfo();
            Nvidia    = SystemInfo.ParseNvidiaSmi();

            Summary = new
            {
                Host = new
                {
                    OperatingSystem = SystemInfo.OperatingSystem,
                    OSVersion       = Host.OSVersion,
                    TotalMemory     = Memory.Total,
                    Environment     = Host.ExcutionEnvironment?.ToString(),
                },
                CPU = new
                {
                    Count        = CPU.Count,
                    Architecture = SystemInfo.Architecture
                },
                GPU = new
                {
                    // update this when we can get multiple GPU data
                    Count = Nvidia is null ? 0 : 1,
                    GPUs  = Nvidia is null
                          ? new Object[] { }
                          : new Object[] {
                                new
                                {
                                    Name            = Nvidia.GpuName,
                                    Vendor          = "Nvidia",
                                    Memory          = Nvidia.TotalMemory,
                                    DriverVersion   = Nvidia.DriverVersion,
                                    CUDAVersion     = Nvidia.CudaVersion,
                                    ComputeCapacity = Nvidia.ComputeCapacity
                                }
                          }
                }
            };

        }

        private static MemoryProperties GetMemoryInfo()
        {
            return new MemoryProperties
            {
                Total     = _hardwareInfo.MemoryStatus.TotalPhysical,
                Available = _hardwareInfo.MemoryStatus.AvailablePhysical
            };
        }

        /// <summary>
        /// Gets a dictionary of System information properties.
        /// </summary>
        /// <returns>A Dictionary&lt;string, string></returns>
        private static EnvironmentProperties GetEnvironmentInfo()
        {
            var osProperties = new EnvironmentProperties
            {
                Platform            = SystemInfo.Platform ,
                OSName              = SystemInfo.OperatingSystem,
                OSVersion           = _hardwareInfo.OperatingSystem.VersionString,   // TODO: Ensure this doesn't have things like 'insider build' that could be interpreted as private info. Just major.minor.patch
                Architecture        = SystemInfo.Architecture,
                DotnetVersion       = RuntimeInformation.FrameworkDescription,
                ExcutionEnvironment = SystemInfo.ExecutionEnvironment.ToString()
            };

            return osProperties;
        }

        private static CpuProperties GetCpuInfo() 
        {
            var cpuList = new List<CpuInfo>();
            foreach (var cpu in _hardwareInfo.CpuList)
            {
                var cpuInfo = new CpuInfo
                {
                    Name          = cpu.Name,
                    NumberOfCores = cpu.NumberOfCores,
                    ThreadCount   = cpu.NumberOfLogicalProcessors
                };

                cpuList.Add(cpuInfo);
            }

            var cpuProperties = new CpuProperties
            {
                Count = _hardwareInfo.CpuList.Count,
                CPUs  = cpuList
            };

            return cpuProperties;
        }
    }
}
