using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using CodeProject.AI.SDK.API;

namespace CodeProject.AI.SDK
{
    /// <summary>
    /// Describes the state of a process that is running a module
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProcessStatusType
    {
        /// <summary>
        /// No idea what's happening.
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown = 0,

        /// <summary>
        /// Not set to auto-start (but available to be started)
        /// </summary>
        [EnumMember(Value = "NoAutoStart")]
        NoAutoStart,

        /// <summary>
        /// Not available. Maybe not valid, maybe not available on this platform.
        /// </summary>
        [EnumMember(Value = "NotAvailable")]
        NotAvailable,

        /// <summary>
        /// Will be started when the server starts
        /// </summary>
        [EnumMember(Value = "AutoStart")]
        AutoStart,

        /// <summary>
        /// It's ready to rock and/or roll but wasn't started (maybe due to debugging settings)
        /// </summary>
        [EnumMember(Value = "NotStarted")]
        NotStarted,

        /// <summary>
        /// Starting up, but not yet started
        /// </summary>
        [EnumMember(Value = "Starting")]
        Starting,

        /// <summary>
        /// Restarting an already started process
        /// </summary>
        [EnumMember(Value = "Restarting")]
        Restarting,

        /// <summary>
        /// Off to the races
        /// </summary>
        [EnumMember(Value = "Started")]
        Started,

        /// <summary>
        /// Oh that's not good
        /// </summary>
        [EnumMember(Value = "FailedStart")]
        FailedStart,

        /// <summary>
        /// That's even worse
        /// </summary>
        [EnumMember(Value = "Crashed")]
        Crashed,

        /// <summary>
        /// A controlled stop.
        /// </summary>
        [EnumMember(Value = "Stopping")]
        Stopping,

        /// <summary>
        /// Park brake on, turn off the engine. We're done.
        /// </summary>
        [EnumMember(Value = "Stopped")]
        Stopped
    }

    /// <summary>
    /// Represents that status of a process that is running a module
    /// </summary>
    public class ProcessStatus
    {
        private int _requestCount;

        /// <summary>
        /// Gets or sets the module Id
        /// </summary>
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the name of the queue this module is processing
        /// </summary>
        public string? Queue { get; set; }

        /// <summary>
        /// Gets or sets the module name
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the module version
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets the UTC time the module was started
        /// </summary>
        public DateTime? Started { get; set; }

        /// <summary>
        /// Gets or sets the UTC time the module was last seen making a request to the backend queue
        /// </summary>
        public DateTime? LastSeen { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the module is running
        /// </summary>
        public ProcessStatusType Status { get; set; } = ProcessStatusType.Unknown;

        /// <summary>
        /// The status data as returned by the module
        /// </summary>
        public JsonObject? StatusData { get; set; }

        /// <summary>
        /// The array of models that can be downloaded by this module
        /// </summary>
        public ModelDownload[]? DownloadableModels { get; set; }

        /// <summary>
        /// Gets the number of requests processed
        /// </summary>
        public int RequestCount { get => _requestCount; }

        /// <summary>
        /// Gets or sets the menus to be displayed in the dashboard based on the current status of
        /// this module.
        /// </summary>
        /// <remarks>
        /// This value is initially populated by the modulesettings.json file, but could change
        /// depending on the state of the module. eg GPU options could be offered, but if the module
        /// then changes to CPU-only, those GPU options may be removed. It all depends on the status.
        /// </remarks>
        public DashboardMenu[]? Menus { get; set; }

        /// <summary>
        /// Gets or sets the human readable notes regarding how this process was installed.
        /// </summary>
        public string? InstallSummary { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the human readable notes regarding how this process was started.
        /// </summary>
        public string? StartupSummary { get; set; } = string.Empty;

        /// <summary>
        /// Increments the number of requests processed by 1.
        /// </summary>
        /// <returns>the incremented value</returns>
        public int IncrementRequestCount() => Interlocked.Increment(ref _requestCount);

        /// <summary>
        /// The string representation of this module
        /// </summary>
        /// <returns>A string object</returns>
        public override string ToString()
        {
            return $"{Name} ({ModuleId}) {Status}";
        }

        /// <summary>
        /// Gets a human readable summary of the process running the given module
        /// </summary>
        public string Summary
        {
            get
            {
                StringBuilder summary = new StringBuilder();

                // summary.AppendLine($"Process '{Name}' (ID: {ModuleId})");
                string timezone = TimeZoneInfo.Local.StandardName;
                string format   = "dd MMM yyyy h:mm:ss tt";
                string started  = (Started is null) ? "Not seen" 
                                : Started.Value.ToLocalTime().ToString(format) + " " + timezone;
                string lastSeen = (LastSeen is null) ? "Not seen"
                                : LastSeen.Value.ToLocalTime().ToString(format) + " " + timezone;

                summary.AppendLine($"Status Data:  {StatusData}");
                summary.AppendLine($"Started:      {started}");
                summary.AppendLine($"LastSeen:     {lastSeen}");
                summary.AppendLine($"Status:       {Status}");
                summary.AppendLine($"Requests:     {RequestCount} (includes status calls)");

                return summary.ToString();
            }
        }
    }
}
