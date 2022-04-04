namespace CodeProject.SenseAI.API.Server.Backend
{
#pragma warning disable IDE1006 // Naming Styles
    /// <summary>
    /// Face Recognition Response
    /// </summary>
    public class BackendRecognitionResponse : BackendSuccessResponse
    {
        /// <summary>
        /// Gets or sets the confidence in the recognition response
        /// </summary>
        public float confidence { get; set; }

        /// <summary>
        /// Gets or sets the label to apply to the detected item
        /// </summary>
        public string? label { get; set; }
    }

    /// <summary>
    /// Face Recognition Response
    /// </summary>
    public class BackendSceneDetectResponse : BackendSuccessResponse
    {
        /// <summary>
        /// Gets or sets the confidence in the recognition response
        /// </summary>
        public float confidence { get; set; }

        /// <summary>
        /// Gets or sets the label to apply to the detected item
        /// </summary>
        public string? label { get; set; }
    }

    /// <summary>
    /// Face Match Response.
    /// </summary>
    public class BackendFaceMatchResponse : BackendSuccessResponse
    {
        /// <summary>
        /// Gets or sets the similarity in an object comparison response
        /// </summary>
        public float similarity { get; set; }
    }

    /// <summary>
    /// A bounding box with confidence level.
    /// </summary>
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

    /// <summary>
    /// A Face Detection Prediction.
    /// </summary>
    public class FaceDetectionPrediction : BoundingBoxPrediction
    {
    }

    /// <summary>
    /// A Face Recognition Prediction.
    /// </summary>
    public class FaceRecognitionPrediction : BoundingBoxPrediction
    {
        public string? userid { get; set; }
    }

    /// <summary>
    /// An Object Detection Prediction.
    /// </summary>
    public class DetectionPrediction : BoundingBoxPrediction
    {
        public string? label { get; set; }
    }

    /// <summary>
    /// A Registered Face Delete Response.
    /// </summary>
    public class BackendFaceDeleteResponse : BackendSuccessResponse
    {
    }

    /// <summary>
    /// A Face Registration Response.
    /// </summary>
    public class BackendFaceRegisterResponse : BackendSuccessResponse
    {
        public string? message { get; set; }
    }

    /// <summary>
    /// A Face Detection Response.
    /// </summary>
    public class BackendFaceDetectionResponse : BackendSuccessResponse
    {
        public FaceDetectionPrediction[]? predictions { get; set; }
    }

    /// <summary>
    /// A Face Recognition Response.
    /// </summary>
    public class BackendFaceRecognitionResponse : BackendSuccessResponse
    {
        public FaceRecognitionPrediction[]? predictions { get; set; }
    }

    /// <summary>
    /// A List Registered Face Response
    /// </summary>
    public class BackendListRegisteredFacesResponse : BackendSuccessResponse
    {
        public string[]? faces { get; set; }
    }

    /// <summary>
    /// An Object Detection Response.
    /// </summary>
    public class BackendObjectDetectionResponse : BackendSuccessResponse
    {
        public DetectionPrediction[]? predictions { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
