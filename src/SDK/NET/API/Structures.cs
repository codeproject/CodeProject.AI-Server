namespace CodeProject.AI.SDK.API
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// The structure for the detected face information.
    /// </summary>
    public class DetectedFace
    {
        /// <summary>
        /// Gets or sets the confidence level of the face detection from 0 to 1.
        /// </summary>
        public float confidence { get; set; }

        /// <summary>
        /// Gets or sets the top of the bounding rectangle.
        /// </summary>
        public int y_min { get; set; }

        /// <summary>
        /// Gets or sets the left of the bounding rectangle.
        /// </summary>
        public int x_min { get; set; }

        /// <summary>
        /// Gets or sets the bottom of the bounding rectangle.
        /// </summary>
        public int y_max { get; set; }

        /// <summary>
        /// Gets or sets the right of the bounding rectangle.
        /// </summary>
        public int x_max { get; set; }
    }

    /// <summary>
    /// The structure for the detected object information.
    /// </summary>
    public class DetectedObject
    {
        /// <summary>
        /// Gets or sets the confidence level of the face detection from 0 to 1.
        /// </summary>
        public float confidence { get; set; }

        /// <summary>
        /// Gets or sets the label for the detected object.
        /// </summary>
        public string? label { get; set; }

        /// <summary>
        /// Gets or sets the top of the bounding rectangle.
        /// </summary>
        public int y_min { get; set; }

        /// <summary>
        /// Gets or sets the left of the bounding rectangle.
        /// </summary>
        public int x_min { get; set; }

        /// <summary>
        /// Gets or sets the bottom of the bounding rectangle.
        /// </summary>
        public int y_max { get; set; }

        /// <summary>
        /// Gets or sets the right of the bounding rectangle.
        /// </summary>
        public int x_max { get; set; }
    }

    public class RecognizedFace
    {
        public string? userid { get; set; }
        public float confidence { get; set; }
        public int y_min { get; set; }
        public int x_min { get; set; }
        public int y_max { get; set; }
        public int x_max { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
