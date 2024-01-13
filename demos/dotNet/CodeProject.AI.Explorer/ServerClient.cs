using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.API
{
    /// <summary>
    /// This is an example of a .NET client to call CodeProject.AI API Server.
    /// </summary>
    public class ServerClient : ApiClient
    {
        /// <summary>
        /// Initializes a new instance of the ServerClient class.
        /// </summary>
        /// <param name="port">The oort for the HTTP calls</param>
        public ServerClient(int port) : base(port)
        {
        }

        /// <summary>
        /// Find faces in an image.
        /// </summary>
        /// <param name="imagePath">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles for the faces found, if any.</returns>
        public async Task<ServerResponse> DetectFaces(string imagePath)
        {
            var request = new ServerRequestContent();
            if (!request.AddFile(imagePath))
                return new ServerErrorResponse("Image does not exist");

            return await PostAsync<DetectFacesResponse>("vision/face", request).ConfigureAwait(false);
        }

        /// <summary>
        /// Compares two images to see if the face(s) in the images are similar.
        /// </summary>
        /// <param name="image1FileName">The path to the first image file.</param>
        /// <param name="image2FileName">The path to the second image file.</param>
        /// <returns>A response that contains the similarity of the faces.</returns>
        public async Task<ServerResponse> MatchFaces(string image1FileName, string image2FileName)
        {
            var request = new ServerRequestContent();
            if (!request.AddFile(image1FileName, "image1"))
                return new ServerErrorResponse("Image does not exist");
            if (!request.AddFile(image2FileName, "image2"))
                return new ServerErrorResponse("Image does not exist");

            return await PostAsync<MatchFacesResponse>("vision/face/match", request).ConfigureAwait(false);
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

        /// <summary>
        /// Registers one or more face images against a user id.
        /// </summary>
        /// <param name="userId">The user id or name.</param>
        /// <param name="registerFileNames">The list of filename.</param>
        public async Task<ServerResponse> RegisterFace(string userId,
                                                     IEnumerable<string> registerFileNames)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new ServerErrorResponse("No user id provided");

            var request = new ServerRequestContent();
            request.AddParam("userid", userId);

            foreach (var (filePath, index) in registerFileNames.Select((name, index) => (name, index)))
                request.AddFile(filePath, $"image{index+1}");

            return await PostAsync<RegisterFaceResponse>($"vision/face/register", request).ConfigureAwait(false);
        }

        /// <summary>
        /// Recognizes one or more face images in an image.
        /// </summary>
        /// <param name="filename">The filename.</param>
        public async Task<ServerResponse> RecognizeFace(string filename, float minConfidence = 0.4f)
        {
            var request = new ServerRequestContent();
            request.AddParam("min_confidence", minConfidence.ToString());
            request.AddFile(filename);

            return await PostAsync<RecognizeFacesResponse>($"vision/face/recognize", request).ConfigureAwait(false);
        }

        public async Task<ServerResponse> DeleteRegisteredFace(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new ServerErrorResponse("No user id provided");

            var request = new ServerRequestContent();
            request.AddParam("userId", userId);

            return await PostAsync<DeleteFaceResponse>($"vision/face/delete", request).ConfigureAwait(false);
        }

        public async Task<ServerResponse> ListRegisteredFaces()
        {
            return await PostAsync<ListRegisteredFacesResponse>($"vision/face/list", null).ConfigureAwait(false);
        }
    }
}