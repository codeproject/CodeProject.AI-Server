
using System.Threading.Tasks;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.SDK.Client
{
    /// <summary>
    /// This is an example of a .NET client to call CodeProject.AI API Server.
    /// </summary>
    public class SceneClassifierClient : ApiClient
    {
        /// <summary>
        /// Initializes a new instance of the ServerClient class.
        /// </summary>
        /// <param name="port">The port for the HTTP calls</param>
        public SceneClassifierClient(int port = 0) : base(port)
        {
        }

        /// <summary>
        /// Identify a scene in an image.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles for the faces found, if any.</returns>
        public async Task<ServerResponse> DetectScene(string imagePath)
        {
            var request = new ServerRequestContent();
            if (!request.AddFile(imagePath))
                return new ServerErrorResponse("Image does not exist");

            return await PostAsync<DetectSceneResponse>("vision/scene", request).ConfigureAwait(false);
        }
    }
}