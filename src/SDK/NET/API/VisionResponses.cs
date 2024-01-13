namespace CodeProject.AI.SDK.API
{
    /// <summary>
    /// The Response for a Face Detection Request.
    /// </summary>
    public class DetectFacesResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the array of Detected faces.  May be null or empty.
        /// </summary>
        public DetectedFace[]? Predictions { get; set; }
    }

    /// <summary>
    /// The Response for a Face Match request.
    /// </summary>
    public class MatchFacesResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the Similarity of the two faces from 0 to 1.
        /// </summary>
        public float Similarity { get; set; }
    }

    /// <summary>
    /// The Response for a Scene Detection request.
    /// </summary>
    public class DetectSceneResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the confidence level of the face detection from 0 to 1.
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Gets or sets the name of the detected scene.
        /// </summary>
        public string? Label { get; set; }
    }

    /// <summary>
    /// The Response for a Detect Objects request.
    /// </summary>
    public class DetectObjectsResponse : ServerResponse
    {
        /// <summary>
        /// Gets or sets the list of detected object predictions.
        /// </summary>
        public DetectedObject[]? Predictions { get; set; }
    }

    public class RegisterFaceResponse : ServerResponse
    {
        public string? Message { get; set; }
    }

    public class RecognizeFacesResponse : ServerResponse
    {
        public RecognizedFace[]? Predictions { get; set; }
    }

    public class ListRegisteredFacesResponse : ServerResponse
    {
        public string[]? Faces { get; set; }
    }

    public class DeleteFaceResponse : ServerResponse
    {
    }
}
