namespace CodeProject.AI.AnalysisLayer.SDK
{
#pragma warning disable IDE1006 // Naming Styles
    public class BackendResponseBase
    {
        /// <summary>
        /// Gets or sets a value indicating success of the call
        /// </summary>
        public bool success { get; set; }
    }

    /// <summary>
    /// An error response for a Queued Request.
    /// </summary>
    public class BackendErrorResponse : BackendResponseBase
    {
        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public int code { get; set; } = 0;

        /// <summary>
        /// Gets or sets the error message;
        /// </summary>
        public string? error { get; set; } = null;

        /// <summary>
        /// Instantiates a new instance of the <cref="BackendErrorResponse" /> class.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="errorMessage">The error message.</param>
        public BackendErrorResponse(int errorCode, string errorMessage)
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
