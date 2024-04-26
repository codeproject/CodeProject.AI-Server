using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Server.Modules
{
    /// <summary>
    /// The old modulesettings.json format
    /// </summary>
    public class LegacyModuleConfig
    {
        /// <summary>
        /// Gets or sets the Id of the Module
        /// </summary>
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the Name to be displayed.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the version of this module
        /// </summary>
        public string? Version { get; set; }

        // Publishing Info =========================================================================

        /// <summary>
        /// Gets or sets the Description for the module.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the URL of the icon for this module.
        /// </summary>
        public string? IconURL { get; set; }

        /// <summary>
        /// Gets or sets the Category of this module.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the tech stack that this module is based on.
        /// </summary>
        public string? Stack { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? License { get; set; }

        /// <summary>
        /// Gets or sets the current version.
        /// </summary>
        public string? LicenseUrl { get; set; }

        /// <summary>
        /// Gets or sets the author or authors of this module
        /// </summary>
        public string? Author { get; set; }

        /// <summary>
        /// Gets or sets the homepage for this module
        /// </summary>
        public string? Homepage { get; set; }

        /// <summary>
        /// Gets or sets the name of the project this module is based on
        /// </summary>
        public string? BasedOn { get; set; }

        /// <summary>
        /// Gets or sets the URL of the project this module is based on
        /// </summary>
        public string? BasedOnUrl { get; set; }

        // Install Options =========================================================================

        /// <summary>
        /// Gets or sets the platforms on which this module is supported. Options include: windows,
        /// windows-arm64, linux, linux-arm64, macos, macos-arm64, raspberrypi, orangepi, radxarock,
        /// jetson. If any of these is preceded by a "!" then that platform is specifically not
        /// supported. This allows options such as "linux-arm64, !jetson" to mean all Linux arm64
        /// platforms except NVIDIA Jetson.
        /// </summary>
        public string[] Platforms { get; set; } = Array.Empty<string>();

        // <summary>
        // Gets or sets a value indicating whether this module was pre-installed (eg Docker). If
        // the module was preinstalled, this value is true, otherwise false.
        // </summary>
        // public bool PreInstalled { get; set; } = false;

        /// <summary>
        /// Gets or sets the list of module versions and the server version that matches each of
        /// these versions. This determines whether the module can be installed on a given server.
        /// </summary>
        public ModuleRelease[] ModuleReleases { get; set; } = Array.Empty<ModuleRelease>();

        // Launch Settings =========================================================================

        /// <summary>
        /// Gets or sets a value indicating whether this process should be activated on startup if
        /// no instruction to the contrary is seen. A default "Start me up" flag.
        /// </summary>
        public bool? AutoStart { get; set; }

        /// <summary>
        /// Gets or sets the path to the startup file relative to the module directory.
        /// </summary>
        /// <remarks>
        /// If no Runtime or Command is specified then a default runtime will be chosen based on
        /// the extension. Currently this is:
        ///     .py  => it will be started with the default Python interpreter
        ///     .dll => it will be started with the .NET runtime.
        /// 
        /// TODO: this is currently relative to the modules directory but should be relative
        /// to the directory containing the modulesettings.json file. This should be changed when
        /// the modules read the modulesettings.json files for their configuration.
        /// </remarks>
        public string? FilePath { get; set; }

        /// <summary>
        /// Gets or sets the runtime used to execute the file at FilePath. For example, the runtime
        /// could be "dotnet" or "python3.9". 
        /// </summary>
        public string? Runtime { get; set; }

        /// <summary>
        /// Gets or sets where the runtime executables for this module should be found. Valid
        /// values are:
        /// "Shared" - the runtime is installed in the /modules folder 
        /// "Local"  - the runtime is installed locally in this modules folder
        /// "System" - the runtime is installed in the system globally
        /// </summary>
        public RuntimeLocation RuntimeLocation  { get; set; } = RuntimeLocation.Local;

        /// <summary>
        /// Gets or sets the command to execute the file at FilePath. If set, this overrides Runtime.
        /// An example would be "/usr/bin/python3". This property allows you to specify an explicit
        /// command in case the necessary runtime hasn't been registered, or in case you need to
        /// provide specific flags or naming alternative when executing the FilePath on different
        /// platforms. 
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Gets or sets the number of seconds this module should pause after starting to ensure 
        /// any resources that require startup (eg GPUs) are fully activated before moving on.
        /// </summary>
        public int? PostStartPauseSecs { get; set; } = 3;

        /// <summary>
        /// Gets or sets the name of the queue this module should service when processing commands.
        /// </summary>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the number of MB of memory needed for this module to perform operations.
        /// If null, then no checks done.
        /// </summary>
        public int? RequiredMb { get; set; }

        // GPU Options =============================================================================

        /// <summary>
        /// Gets or sets a value indicating whether the installer should install GPU support such as
        /// GPU enabled libraries in order to provide GPU functionality when running. This doesn't
        /// direct that a GPU must be used, but instead provides the means for an app to use GPUs
        /// if it desires. Note that if InstallGPU = false, EnableGPU is set to false. Setting this
        /// allows you to force a module to install in CPU mode to work around show-stoppers that
        /// may occur when trying to install GPU enabled libraries.
        /// </summary>
        public bool? InstallGPU { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether this process should enable GPU functionality
        /// when running. This doesn't direct that a GPU must be used, but instead alerts that app
        /// that it should enable GPUs if possible. Setting this to false means "even if you can 
        /// use a GPU, don't". Great for working around GPU issues that would sink the ship.
        /// </summary>
        public bool? EnableGPU { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating the degree of parallelism (number of threads or number
        /// of tasks, depending on the implementation) to launch when running this module.
        /// 0 = default, which is (Number of CPUs - 1).
        /// </summary>
        public int? Parallelism { get; set; }

        /// <summary>
        /// Gets or sets the device name (eg CUDA device number, TPU device name) to use. Be careful to
        /// ensure this device exists.
        /// </summary>
        public string? AcceleratorDeviceName { get; set; }

        /// <summary>
        /// Gets or sets whether to use half-precision floating point ops on the hardware in use. This
        /// is an option for more recent PyTorch libraries and can speed things up nicely. Can be 'enable',
        /// 'disable' or 'force'
        /// </summary>
        public string? HalfPrecision { get; set; } = "enable";

        // UI Elements =============================================================================

        /// <summary>
        /// Gets or sets the UI components to be included in the Explorer web app that provides the
        /// means to explore and test this module.
        /// </summary>
        public ExplorerUI? ExplorerUI { get; set; }

        /// <summary>
        /// Gets or sets the menus to be displayed in the dashboard based on the current status of
        /// this module
        /// </summary>
        public DashboardMenu[]? Menus { get; set; }

        // Environment Vars ========================================================================

        /// <summary>
        /// Gets or sets the information to pass to the backend analysis modules.
        /// </summary>
        public Dictionary<string, object>? EnvironmentVariables { get; set; }

        // Route Maps ==============================================================================

        /// <summary>
        /// Gets or sets a list of RouteMaps.
        /// </summary>
        public ModuleRouteInfo[] RouteMaps { get; set; } = Array.Empty<ModuleRouteInfo>();

        /// <summary>
        /// Gets a value indicating whether or not this is a valid module that can actually be
        /// started.
        /// </summary>
        [JsonIgnore]
        public bool Valid
        {
            get
            {
                return !string.IsNullOrWhiteSpace(ModuleId)     &&
                       !string.IsNullOrWhiteSpace(Name)         &&
                       Platforms?.Length > 0                    &&
                       (ModuleReleases?.Length ?? 0) > 0        &&
                       (!string.IsNullOrWhiteSpace(Command) || 
                        !string.IsNullOrWhiteSpace(Runtime))    &&
                       !string.IsNullOrWhiteSpace(FilePath)     &&
                       (RouteMaps?.Length ?? 0) > 0;
            }
        }

        /// <summary>
        /// Converts this object to a ModuleConfig object
        /// </summary>
        /// <returns>A <see cref="ModuleConfig"/> object</returns>
        public ModuleConfig ToModuleConfig()
        {
            var module = new ModuleConfig()
            {
                ModuleId       = ModuleId,
                Name           = Name,
                Version        = Version,
                PublishingInfo = new PublishingInfo()
                {
                    Description = Description,
                    IconURL     = IconURL,
                    Category    = Category,
                    Stack       = Stack,
                    License     = License,
                    LicenseUrl  = LicenseUrl,
                    Author      = Author,
                    Homepage    = Homepage,
                    BasedOn     = BasedOn,
                    BasedOnUrl  = BasedOnUrl
                },
                InstallOptions = new InstallOptions()
                {
                    Platforms      = Platforms,
                    // PreInstalled   = PreInstalled,
                    ModuleReleases = ModuleReleases
                },
                LaunchSettings = new LaunchSettings()
                {
                    AutoStart          = AutoStart,
                    FilePath           = FilePath,
                    Runtime            = Runtime,
                    RuntimeLocation    = RuntimeLocation,
                    Command            = Command,
                    PostStartPauseSecs = PostStartPauseSecs,
                    Queue              = Queue,
                    Parallelism        = Parallelism,
                    RequiredMb         = RequiredMb
                },
                GpuOptions = new GpuOptions()
                {
                    InstallGPU            = InstallGPU,
                    EnableGPU             = EnableGPU,
                    AcceleratorDeviceName = AcceleratorDeviceName,
                    HalfPrecision         = HalfPrecision
                },
                UIElements = new UIElements()
                {
                    ExplorerUI = ExplorerUI,
                    Menus      = Menus
                },
                RouteMaps = RouteMaps
            };

            return module;
        }
    }
}