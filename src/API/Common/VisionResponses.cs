namespace CodeProject.AI.API.Common
{
#pragma warning disable IDE1006 // Naming Styles

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
