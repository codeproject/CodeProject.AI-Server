using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeProject.SenseAI.API.Common
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

    /// <summary>
    /// The Response when requesting the list of log entries
    /// </summary>
    public class LogListResponse : SuccessResponse
    {
        public LogEntry[]? entries { get; set; }
    }

    /// <summary>
    /// The Response when requesting the status of the backend analysis services
    /// </summary>
    public class AnalysisServicesStatusResponse : SuccessResponse
    {
        public KeyValuePair<string, bool>[]? statuses { get; set; }
    }

    /// <summary>
    /// The Response for a Face Detection Request.
    /// </summary>
    public class DetectFacesResponse : SuccessResponse
    {
        /// <summary>
        /// Gets or sets the array of Detected faces.  May be null or empty.
        /// </summary>
        public DetectedFace[]? predictions { get; set; }
    }

    /// <summary>
    /// The Response for a Face Match request.
    /// </summary>
    public class MatchFacesResponse : SuccessResponse
    {
        /// <summary>
        /// Gets or sets the Similarity of the two faces from 0 to 1.
        /// </summary>
        public float similarity { get; set; }
    }

    /// <summary>
    /// The Response for a Scene Detection request.
    /// </summary>
    public class DetectSceneResponse : SuccessResponse
    {
        /// <summary>
        /// Gets or sets the confidence level of the face detection from 0 to 1.
        /// </summary>
        public float confidence { get; set; }

        /// <summary>
        /// Gets or sets the name of the detected scene.
        /// </summary>
        public string? label { get; set; }
    }

    /// <summary>
    /// The Response for a Detect Objects request.
    /// </summary>
    public class DetectObjectsResponse : SuccessResponse
    {
        /// <summary>
        /// Gets or sets the list of detected object predictions.
        /// </summary>
        public DetectedObject[]? predictions { get; set; }

        public int duration { get; set; } = 0;
    }

    public class RegisterFaceResponse : SuccessResponse
    {
        public string? message { get; set; }
    }

    public class RecognizeFacesResponse : SuccessResponse
    {
        public RecognizedFace[]? predictions { get; set; }
    }

    public class ListRegisteredFacesResponse : SuccessResponse
    {
        public string[]? faces { get; set; }
    }

    public class DeleteFaceResponse : SuccessResponse
    {
    }

#pragma warning restore IDE1006 // Naming Styles
}
