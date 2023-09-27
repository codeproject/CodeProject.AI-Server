namespace CodeProject.AI.SDK
{
#pragma warning disable IDE1006 // Naming Styles
    public class BackendResponseBase
    {
        /// <summary>
        /// Gets or sets a value indicating success of the call
        /// </summary>
        public bool success { get; set; }

        /// <summary>
        /// DEPRECATED: Gets or sets the return code (follows the conventions of HTTP codes). 
        /// </summary>
        public int code { get; set; } = 200;

        /// <summary>
        /// Gets or sets the optional command associated with this request
        /// </summary>
        public string? command { get; set; }

        /// <summary>
        /// Gets or sets the Id of the Module handling this request
        /// </summary>
        public string? moduleId { get; set; }

        /// <summary>
        /// Gets or sets the execution provider (hardware or library) that handles the AI 
        /// acceleration.
        /// </summary>
        public string? executionProvider { get; set; }

        /// <summary>
        /// Gets or sets whether this module is able to make use of a GPU.
        /// </summary>
        public bool canUseGPU { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds required to perform the AI inference operation(s)
        /// for this response.
        /// </summary>
        public long inferenceMs { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds required to perform the AI processing for this
        /// response. This includes the inference, as well as any pre- and post-processing.
        /// </summary>
        public long processMs { get; set; }

        /// <summary>
        /// Gets or sets the number of milliseconds required to run the full task in processing this
        /// response.
        /// </summary>
        public long analysisRoundTripMs { get; set; }
    }

    /// <summary>
    /// An error response for a Queued Request.
    /// </summary>
    public class BackendErrorResponse : BackendResponseBase
    {
        /// <summary>
        /// Gets or sets the error message;
        /// </summary>
        public string? error { get; set; } = null;

        /// <summary>
        /// Instantiates a new instance of the <cref="BackendErrorResponse" /> class.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="errorCode">The error code.</param>
        public BackendErrorResponse(string errorMessage, int errorCode = 500)
        {
            success = false;
            code    = errorCode;
            error   = errorMessage;
        }
    }

    /// <summary>
    /// General success base class.
    /// </summary>
    public class BackendSuccessResponse : BackendResponseBase
    {
        /// <summary>
        /// Instantiates a new instance of the <cref="BackendSuccessResponse" /> class.
        /// </summary>
        public BackendSuccessResponse()
        {
            success = true;
        }
    }
    public class BoundingBoxPrediction
    {
        /// <summary>
        /// Gets or sets the confidence in the detection response
        /// </summary>
        public float confidence { get; set; }

        /// <summary>
        /// Gets or sets the lower y coordinate of the bounding box
        /// </summary>
        public int y_min { get; set; }

        /// <summary>
        /// Gets or sets the lower x coordinate of the bounding box
        /// </summary>
        public int x_min { get; set; }

        /// <summary>
        /// Gets or sets the upper y coordinate of the bounding box
        /// </summary>
        public int y_max { get; set; }

        /// <summary>
        /// Gets or sets the upper x coordinate of the bounding box
        /// </summary>
        public int x_max { get; set; }
    }


#pragma warning restore IDE1006 // Naming Styles
}
