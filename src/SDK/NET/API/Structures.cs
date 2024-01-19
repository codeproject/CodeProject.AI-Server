namespace CodeProject.AI.SDK.API
{
    public class BoundingBox
    {
        /// <summary>
        /// Gets or sets the lower y coordinate of the bounding box
        /// </summary>
        public int Y_min { get; set; }

        /// <summary>
        /// Gets or sets the lower x coordinate of the bounding box
        /// </summary>
        public int X_min { get; set; }

        /// <summary>
        /// Gets or sets the upper y coordinate of the bounding box
        /// </summary>
        public int Y_max { get; set; }

        /// <summary>
        /// Gets or sets the upper x coordinate of the bounding box
        /// </summary>
        public int X_max { get; set; }
    }

    /// <summary>
    /// The structure for the detected face information.
    /// </summary>
    public class DetectedFace: BoundingBox
    {
        /// <summary>
        /// Gets or sets the confidence level of the face detection from 0 to 1.
        /// </summary>
        public float Confidence { get; set; }
    }

    /// <summary>
    /// The structure for the detected object information.
    /// </summary>
    public class DetectedObject : BoundingBox
    {
        /// <summary>
        /// Gets or sets the label for the object detected
        /// </summary>
        public string? Label { get; set; }

        /// <summary>
        /// Gets or sets the confidence in the detection response
        /// </summary>
        public float Confidence { get; set; }
    }

    public class RecognizedFace : BoundingBox
    {
        /// <summary>
        /// Gets or sets the ID of the user whose face was detected
        /// </summary>
        public string? Userid { get; set; }

        /// <summary>
        /// Gets or sets the confidence in the detection response
        /// </summary>
        public float Confidence { get; set; }
    }
}
