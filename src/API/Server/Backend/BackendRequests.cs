using System;
using System.Text.Json.Serialization;

namespace CodeProject.SenseAI.API.Server.Backend
{
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Base class for queued requests for the backend.
    /// </summary>
    public class BackendRequestBase
    {
        /// <summary>
        /// Gets the request unique id.  Used to return the response to the correct caller.
        /// </summary>
        [JsonInclude]
        public string reqid { get; private set; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// For Object detection requests
    /// </summary>
    public class BackendObjectDetectionRequest : BackendRequestBase
    {
        /// <summary>
        /// The image id. 
        /// </summary>
        public string? imgid { get; set; }

        /// <summary>
        /// The minimum confidence bar for a positive match to be determined.
        /// </summary>
        public float? minconfidence { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "detection";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendObjectDetectionRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendObjectDetectionRequest(string? imageId, float? minimumConfidence)
        {
            imgid         = imageId;
            minconfidence = minimumConfidence;
        }
    }

    /// <summary>
    /// For Face detection requests
    /// </summary>
    public class BackendFaceDetectionRequest : BackendRequestBase
    {
        /// <summary>
        /// The image id. 
        /// </summary>
        public string? imgid { get; set; }

        /// <summary>
        /// The minimum confidence bar for a positive match to be determined.
        /// </summary>
        public float? minconfidence { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "detect";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceDetectionRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceDetectionRequest(string? imageId, float? minimumConfidence)
        {
            imgid         = imageId;
            minconfidence = minimumConfidence;
        }
    }

    public class BackendFaceMatchRequest : BackendRequestBase
    {
        public string[]? images { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "match";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceMatchRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceMatchRequest(string image1Id, string image2Id)
        {
            images = new string[] { image1Id, image2Id };
        }
    }
    public class BackendSceneDetectionRequest : BackendRequestBase
    {
        /// <summary>
        /// The image id. 
        /// </summary>
        public string? imgid { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "detection";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendSceneDetectionRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendSceneDetectionRequest(string? imageId)
        {
            imgid = imageId;
        }
    }

    public class BackendFaceRegisterRequest : BackendRequestBase
    {
        /// <summary>
        /// Gets or sets the id of the user for whom the images represent
        /// </summary>
        public string? userid { get; set; }

        /// <summary>
        /// Gets or sets the array of image Ids that were registered
        /// </summary>
        public string[]? images { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "register";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceRegisterRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceRegisterRequest(string userid, string[] imageids)
        {
            this.userid = userid;
            this.images = imageids;
        }
    }

    public class BackendFaceListRequest : BackendRequestBase
    {
        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "list";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceListRequest()
        {
        }
    }
    public class BackendFaceDeleteRequest : BackendRequestBase
    {
        /// <summary>
        /// Gets or sets the id of the user for whom the images represent
        /// </summary>
        public string? userid { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "delete";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceDeleteRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceDeleteRequest(string userid)
        {
            this.userid = userid;
        }
    }

    /// <summary>
    /// For face recognition requests
    /// </summary>
    public class BackendFaceRecognitionRequest : BackendRequestBase
    {
        /// <summary>
        /// The image id. 
        /// </summary>
        public string? imgid { get; set; }

        /// <summary>
        /// The minimum confidence bar for a positive match to be determined.
        /// </summary>
        public float? minconfidence { get; set; }

        /// <summary>
        /// The request type
        /// </summary>
        [JsonInclude]
        public string? reqtype { get; private set; } = "recognize";

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceRecognitionRequest()
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BackendFaceRecognitionRequest(string? imageId, float? minimumConfidence)
        {
            imgid         = imageId;
            minconfidence = minimumConfidence;
        }
    }

#pragma warning restore IDE1006 // Naming Styles
}