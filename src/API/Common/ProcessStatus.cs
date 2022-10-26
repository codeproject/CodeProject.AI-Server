using System.Runtime.Serialization;
using System.Text;
using System;
using System.Text.Json.Serialization;
using System.Threading;

namespace CodeProject.AI.API.Common
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProcessStatusType
    {
        /// <summary>
        /// No idea what's happening.
        /// </summary>
        [EnumMember(Value = "Unknown")]
        Unknown = 0,

        /// <summary>
        /// Not enabled (but available to be enabled)
        /// </summary>
        [EnumMember(Value = "NotEnabled")]
        NotEnabled,

        /// <summary>
        /// Not available. Maybe not valid, maybe not available on this platform.
        /// </summary>
        [EnumMember(Value = "NotAvailable")]
        NotAvailable,

        /// <summary>
        /// Good to go
        /// </summary>
        [EnumMember(Value = "Enabled")]
        Enabled,

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
    /// Represents that status of a process
    /// </summary>
    public class ProcessStatus
    {
        private int _processed;

        /// <summary>
        /// Gets or sets the module Id
        /// </summary>
        public string? ModuleId { get; set; }

        /// <summary>
        /// Gets or sets the module name
        /// </summary>
        public string? Name { get; set; }

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
        /// Gets the number of requests processed
        /// </summary>
        public int Processed { get => _processed; }

        /// <summary>
        /// Gets or sets the name of the hardware acceleration provider.
        /// </summary>
        public string? ExecutionProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hardware type (CPU or GPU)
        /// </summary>
        public string? HardwareType { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the human readable notes regarding how this process was started.
        /// </summary>
        public string? StartupSummary { get; set; } = string.Empty;

        /// <summary>
        /// Increments the number of requests processed by 1.
        /// </summary>
        /// <returns>the incremented value</returns>
        public int IncrementProcessedCount() => Interlocked.Increment(ref _processed);

        public string Summary
        {
            get
            {
                StringBuilder summary = new StringBuilder();

                // summary.AppendLine($"Process '{Name}' (ID: {ModuleId})");
                summary.AppendLine($"Started:      {Started?.ToLocalTime().ToString("dd-MMM-yyyy h:mm:ss tt")}");
                summary.AppendLine($"LastSeen:     {LastSeen?.ToLocalTime().ToString("dd-MMM-yyyy h:mm:ss tt")}");
                summary.AppendLine($"Status:       {Status}");
                summary.AppendLine($"Processed:    {Processed}");
                summary.AppendLine($"Provider:     {ExecutionProvider}");
                summary.AppendLine($"HardwareType: {HardwareType}");

                return summary.ToString();
            }
        }
    }
}
