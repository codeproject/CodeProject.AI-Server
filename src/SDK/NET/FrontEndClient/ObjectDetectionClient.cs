
using System.Threading.Tasks;
using CodeProject.AI.SDK.API;
using CodeProject.AI.SDK.Client;

namespace CodeProject.AI.Server.Client
{
    /// <summary>
    /// This is an example of a .NET client to call CodeProject.AI API Server.
    /// </summary>
    public class ObjectDetectionClient : ApiClient
    {
        /// <summary>
        /// Initializes a new instance of the ServerClient class.
        /// </summary>
        /// <param name="port">The port for the HTTP calls</param>
        public ObjectDetectionClient(int port = 0) : base(port)
        {
        }

        /// <summary>
        /// Identify a scene in an image.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles and labels for the objects found, if
        /// any.</returns>
        public async Task<ServerResponse> DetectObjects(string imagePath)
        {
            var request = new ServerRequestContent();
            if (!request.AddFile(imagePath))
                return new ServerErrorResponse("Image does not exist");

            return await PostAsync<DetectObjectsResponse>("vision/detection", request).ConfigureAwait(false);
        }

        /// <summary>
        /// Identify a scene in an image.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles and labels for the objects found, if
        /// any.</returns>
        public async Task<ServerResponse> CustomDetectObjects(string imagePath,
                                                              string modelName = "ipcam-general")
        {
            var request = new ServerRequestContent();
            if (!request.AddFile(imagePath))
                return new ServerErrorResponse("Image does not exist");

            return await PostAsync<DetectObjectsResponse>($"vision/custom/{modelName}", request).ConfigureAwait(false);
        }
    }
}