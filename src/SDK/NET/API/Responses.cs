using System.Net;

namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// A base class for server responses
    /// </summary>
    public class ServerResponse
    {
        /// <summary>
        /// Gets or sets the HTTP status code of this response.
        /// </summary>
        public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;
    }
    
    /// <summary>
    /// The common data for responses from analysis modules.
    /// </summary>
    public class ModuleResponseBase : ServerResponse
    {
        /// <summary>
        /// Gets or sets a value indicating that the request was successfully executed. This does
        /// not mean the HTTP call was completed - that's reflected in 'Code'. This value reflects
        /// whether the operation that ran on the server completed correctly. The operation may have
        /// failed, but the HTTP call to report that failure was successful.
        /// </summary>
        public bool Success { get; set; } = false;
    }

    public class SuccessResponse : ModuleResponseBase
    {
        public SuccessResponse()
        {
            Success = true;
        }
    }

    public class VersionResponse : SuccessResponse
    {
        public string? Message { get; set; }
        public VersionInfo? Version { get; set; }
    }

    public class VersionUpdateResponse : VersionResponse
    {
        /// <summary>
        /// Whether or not a new version is available
        /// </summary>
        public bool? UpdateAvailable { get; set; }

        /// <summary>
        /// The latest version available for download
        /// </summary>
        public VersionInfo? Latest { get; set; }

        /// <summary>
        /// The current version of this server
        /// </summary>
        public VersionInfo? Current { get; set; }
    }

    public class ErrorResponse : ModuleResponseBase
    {
        /// <summary>
        /// Gets or sets the error message, if any. May be null if no error.
        /// </summary>
        public string? Error { get; set; }

        public ErrorResponse()
        {
            Success = false;
            Code    = HttpStatusCode.InternalServerError;
        }

        public ErrorResponse(string? error, HttpStatusCode code = HttpStatusCode.InternalServerError)
        {
            Success = false;
            Error   = error;
            Code    = code;
        }
    }

    public class SettingsResponse : ModuleResponseBase
    {
        /// <summary>
        /// Gets or sets the module's environment variables
        /// </summary>
        public IDictionary<string, string?>? EnvironmentVariables { get; set; }

        /// <summary>
        /// Gets or sets the module's settings
        /// </summary>
        public dynamic? Settings { get; set; }
    }

    /// <summary>
    /// The Response when requesting the status of the backend modules
    /// </summary>
    public class ModuleStatusesResponse : SuccessResponse
    {
        public List<ProcessStatus>? Statuses { get; set; }
    }

    /// <summary>
    /// The Response when requesting the status of the backend modules
    /// </summary>
    public class ModuleExplorerUIResponse: SuccessResponse
    {
        public List<ExplorerUI> UiInsertions { get; set; } = new List<ExplorerUI>();
    }

    /// <summary>
    /// The Response when requesting the status of the backend analysis modules
    /// </summary>
    public class ModuleListResponse : SuccessResponse
    {
        public List<ModuleDescription>? Modules { get; set; }
    }

    /// <summary>
    /// The Response when requesting general info (eg logs) from one of the backend analysis modules
    /// </summary>
    public class ModuleResponse : SuccessResponse
    {
        public object? Data { get; set; }
    }

    /// <summary>
    /// The Response when requesting general info (eg logs) from one of the backend analysis modules
    /// </summary>
    public class ModuleResponse<T> : SuccessResponse
    {
        public T? Data { get; set; }
    }
}
