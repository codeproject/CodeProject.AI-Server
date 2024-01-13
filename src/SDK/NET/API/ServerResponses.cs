using System.Net;

namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// A base class for server and module responses
    /// </summary>
    public class BaseResponse
    {
        /// <summary>
        /// Gets or sets a value indicating that the request was successfully executed. This does
        /// not mean the HTTP call was completed - that's reflected in 'Code'. This value reflects
        /// whether the operation that ran on the server completed correctly. The operation may have
        /// failed, but the HTTP call to report that failure was successful.
        /// </summary>
        public bool Success { get; set; } = true;
    }

    /// <summary>
    /// A base class for server responses
    /// </summary>
    public class ServerResponse: BaseResponse
    {
        /// <summary>
        /// Gets or sets the HTTP status code of this response.
        /// </summary>
        public HttpStatusCode Code { get; set; } = HttpStatusCode.OK;

        /// <summary>
        /// Gets or sets the hostname of the computer sending this response
        /// </summary>
        public string Hostname { get; set; }

        public ServerResponse()
        {
            Hostname = Environment.MachineName;
        }
    }

    /// <summary>
    /// The Response when requesting general info (eg logs) from one of the backend analysis modules
    /// </summary>
    public class ServerDataResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the data returned by the module response
        /// </summary>
        public object? Data { get; set; }
    }

    /// <summary>
    /// The Response when requesting general info (eg logs) from one of the backend analysis modules
    /// </summary>
    public class ServerDataResponse<T> : ServerResponse
    {
        public T? Data { get; set; }
    }

    public class VersionResponse : ServerResponse
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

    /// <summary>
    /// Represents a failed call to the server. The actual HTTP call may have succeeded, but the
    /// operation on the server may fail, meaning Code is 200, but success is false. Or Code could
    /// be not in the 200 range meaning the call itself to the server failed.
    /// </summary>
    public class ServerErrorResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the error message, if any. May be null if no error.
        /// </summary>
        public string? Error { get; set; }

        public ServerErrorResponse()
        {
            Success = false;
            Code    = HttpStatusCode.InternalServerError;
        }

        public ServerErrorResponse(string? error, HttpStatusCode code = HttpStatusCode.InternalServerError)
        {
            Success = false;
            Error   = error;
            Code    = code;
        }
    }

    public class SettingsResponse : ServerResponse
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
    public class ModuleStatusesResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of process statuses
        /// </summary>
        public List<ProcessStatus>? Statuses { get; set; }
    }

    /// <summary>
    /// The Response when requesting the status of the backend modules
    /// </summary>
    public class ModuleExplorerUIResponse: ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of module UI insertions
        /// </summary>
        public List<ExplorerUI> UiInsertions { get; set; } = new List<ExplorerUI>();
    }

    /// <summary>
    /// The Response when requesting basic info of the backend analysis modules
    /// </summary>
    public class ModuleListResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of modules
        /// </summary>
        public List<ModuleBase>? Modules { get; set; }
    }

    /// <summary>
    /// The Response when requesting information on installable modules
    /// </summary>
    public class ModuleListInstallableResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of module descriptions
        /// </summary>
        public List<ModuleDescription>? Modules { get; set; }
    }
}
