using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace CodeProject.AI.API.Common
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// The common data for responses.
    /// </summary>
    public class ResponseBase
    {
        /// <summary>
        /// Gets or sets a value indicating that the request was successfully executed.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public bool success { get; set; } = false;
    }

    public class SuccessResponse : ResponseBase
    {
        public SuccessResponse()
        {
            success = true;
        }
    }

    public class VersionResponse : SuccessResponse
    {
        public string? message { get; set; }
        public VersionInfo? version { get; set; }
    }

    public class VersionUpdateResponse : VersionResponse
    {
        public bool? updateAvailable { get; set; }
    }

    public class ErrorResponse : ResponseBase
    {
        /// <summary>
        /// Gets or sets the error message, if any.  May be null if no error.
        /// </summary>
        public string? error { get; set; }

        /// <summary>
        /// Gets or sets an error code.  Zero if no code.
        /// </summary>
        public int code { get; set; }

        public ErrorResponse()
        {
            success = false;
        }

        public ErrorResponse(string? error, int code = 0)
        {
            this.success = false;
            this.error   = error;
            this.code    = code;
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProcessStatusType
    {
        [EnumMember(Value = "Unknown")]
        Unknown = 0,

        [EnumMember(Value = "NotEnabled")]
        NotEnabled,

        [EnumMember(Value = "Enabled")]
        Enabled,

        [EnumMember(Value = "Starting")]
        Starting,

        [EnumMember(Value = "Started")]
        Started,

        [EnumMember(Value = "NotStarted")]
        NotStarted,

        [EnumMember(Value = "FailedStart")]
        FailedStart,

        [EnumMember(Value = "Crashed")]
        Crashed,

        [EnumMember(Value = "Stopped")]
        Stopped
    }

    /// <summary>
    /// Represents that status of a process
    /// </summary>
    public class ProcessStatus
    {
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
        /// Gets or sets the number of requests processed
        /// </summary>
        public int Processed { get; set; }

        /// <summary>
        /// Gets or sets the name of the hardware acceleration provider.
        /// </summary>
        public string? ExecutionProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hardware (chip) identifier
        /// </summary>
        public string? HardwareId { get; set; } = "CPU";
    }

    /// <summary>
    /// The Response when requesting the status of the backend analysis services
    /// </summary>
    public class AnalysisServicesStatusResponse : SuccessResponse
    {
        public List<ProcessStatus>? statuses { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
