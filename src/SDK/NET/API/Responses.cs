using System.Text.Json.Serialization;

namespace CodeProject.AI.SDK.API
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
        /// <summary>
        /// Whether or not a new version is available
        /// </summary>
        public bool? updateAvailable { get; set; }

        /// <summary>
        /// The latest version available for download
        /// </summary>
        public VersionInfo? latest { get; set; }

        /// <summary>
        /// The current version of this server
        /// </summary>
        public VersionInfo? current { get; set; }
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
            success = false;
            this.error = error;
            this.code = code;
        }
    }

    public class SettingsResponse : ResponseBase
    {
        /// <summary>
        /// Gets or sets the module's environment variables
        /// </summary>
        public IDictionary<string, string?>? environmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the module's settings
        /// </summary>
        public dynamic? settings { get; set; }
    }

    /// <summary>
    /// The Response when requesting the status of the backend modules
    /// </summary>
    public class ModuleStatusesResponse : SuccessResponse
    {
        public List<ProcessStatus>? statuses { get; set; }
    }

    /// <summary>
    /// The Response when requesting the status of the backend analysis modules
    /// </summary>
    public class ModuleListResponse : SuccessResponse
    {
        public List<ModuleDescription>? modules { get; set; }
    }

    /// <summary>
    /// The Response when requesting general info (eg logs) from one of the backend analysis modules
    /// </summary>
    public class ModuleResponse : SuccessResponse
    {
        public object? data { get; set; }
    }

    /// <summary>
    /// The Response when requesting general info (eg logs) from one of the backend analysis modules
    /// </summary>
    public class ModuleResponse<T> : SuccessResponse
    {
        public T? data { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
