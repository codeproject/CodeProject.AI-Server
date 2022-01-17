using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using System.Threading.Tasks;
using System.Threading;
using System.Linq;

using CodeProject.SenseAI.API.Server.Backend;
using CodeProject.SenseAI.API.Common;

namespace CodeProject.SenseAI.API.Server.Frontend.Controllers
{
    /// <summary>
    /// The Vision Operations
    /// </summary>
    [Route("v1/vision")]
    [ApiController]
    [DisableResponseChunking]
    public class VisionController : ControllerBase
    {
        private readonly CommandDispatcher _dispatcher;

        /// <summary>
        /// Initializes a new instance of the VisionController class.
        /// </summary>
        /// <param name="dispatcher">The Command Dispatcher instance.</param>
        public VisionController(CommandDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// Detect the Scene from an image.
        /// </summary>
        /// <param name="image">The Form file object.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A Response describing the scene with confidence level.</returns>
        /// <response code="200">Returns detected scene information, if any.</response>
        /// <response code="400">If the image in the Fomm data is null.</response>            
        [HttpPost("scene", Name = "DetectScene")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> DetectScene([FromForm] IFormFile image,
                                                    CancellationToken token)
        {
            var backendResponse = await _dispatcher.DetectScene(image, token);

            if (backendResponse is BackendSceneDetectResponse detectResponse)
            {
                var response = new DetectSceneResponse
                {
                    confidence = detectResponse.confidence,
                    label      = detectResponse.label
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        private ErrorResponse HandleErrorResponse(BackendResponseBase backendResponse)
        {
            if (backendResponse is BackendErrorResponse errorResponse)
                return new ErrorResponse(errorResponse.error, errorResponse.code);

            return new ErrorResponse("unexpected response", -1);
        }

        /// <summary>
        /// Detect objects in an image.
        /// </summary>
        /// <param name="image">The Form file object.</param>
        /// <param name="min_confidence">The minimum confidence level. Defaults to 0.4.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A list of object names, positions, and confidence levels.</returns>
        /// <response code="200">Returns the list of detected object information, if any.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        [HttpPost("detection", Name = "DetectObjects")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> DetectObjects([FromForm] IFormFile image, 
                                                      [FromForm] float? min_confidence,
                                                      CancellationToken token)
        {
            var backendResponse = await _dispatcher.DetectObjects(image, min_confidence, token);
            if (backendResponse is BackendObjectDetectionResponse detectResponse)
            {
                var response = new DetectObjectsResponse
                {
                    predictions = detectResponse?.predictions
                                 ?.OrderBy(prediction => prediction.confidence)
                                 ?.Select(prediction => new DetectedObject
                                 {
                                     label = prediction.label,
                                     confidence = prediction.confidence,
                                     x_min = (int)(prediction.x_min),
                                     x_max = (int)(prediction.x_max),
                                     y_min = (int)(prediction.y_min),
                                     y_max = (int)(prediction.y_max),

                                 })
                                 ?.ToArray()
                };

                return response;
            }
            
            return HandleErrorResponse(backendResponse);
        }

        /// <summary>
        /// Detect Faces in an image.
        /// </summary>
        /// <param name="image">The Form file object.</param>
        /// <param name="min_confidence">The minimum confidence level. Defaults to 0.4.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A list of face positions, and confidence levels.</returns>
        /// <response code="200">Returns the list of detected face information, if any.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        [HttpPost("face", Name = "DetectFaces")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> DetectFaces([FromForm] IFormFile image,
                                                    [FromForm] float? min_confidence,
                                                    CancellationToken token)
        {
            var backendResponse = await _dispatcher.DetectFaces(image, min_confidence, token);
            if (backendResponse is BackendFaceDetectionResponse detectResponse)
            {
                var response = new DetectFacesResponse
                {
                    predictions = detectResponse?.predictions
                                 ?.OrderBy(prediction => prediction.confidence)
                                 ?.Select(prediction => new DetectedFace
                                 {
                                     confidence = prediction.confidence,
                                     x_min = (int)(prediction.x_min),
                                     x_max = (int)(prediction.x_max),
                                     y_min = (int)(prediction.y_min),
                                     y_max = (int)(prediction.y_max),

                                 })
                                 ?.ToArray()
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        /// <summary>
        /// Match Faces in two different images.
        /// </summary>
        /// <param name="image1">The Form file object.</param>
        /// <param name="image2">The Form file object.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A list of object names, positions, and confidence levels.</returns>
        /// <response code="200">Similarity of the two faces.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        [HttpPost("face/match", Name = "MatchFaces")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> MatchFaces(IFormFile image1, IFormFile image2,
                                                   CancellationToken token)
        {
            var backendResponse = await _dispatcher.MatchFaces(image1, image2, token);
            if (backendResponse is BackendFaceMatchResponse matchResponse)
            {
                var response = new MatchFacesResponse
                {
                    similarity = matchResponse.similarity
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        /// <summary>
        /// Register Face for Recognition.
        /// </summary>
        /// <param name="userid">The if of the user for whom to register the images.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A list of object names, positions, and confidence levels.</returns>
        /// <response code="200">Success message.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        /// <remarks>This method should be a PUT, not a POST operation. We've left it as POST in
        /// order to maintain compatibility with the original DeepStack code.</remarks>
        [HttpPost("face/register", Name = "RegisterFace")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> RegisterFace([FromForm] string userid,
                                                     CancellationToken token)
        {
            var formFiles = HttpContext.Request.Form.Files;

            var backendResponse = await _dispatcher.RegisterFaces(userid, formFiles, token);
            if (backendResponse is BackendFaceRegisterResponse detectResponse)
            {
                var response = new RegisterFaceResponse
                {
                    success = true,
                    message = detectResponse.message
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        /// <summary>
        /// Recognize Faces in image.
        /// </summary>
        /// <param name="image">The image file.</param>
        /// <param name="min_confidence">The minimum confidence for recognition.</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A list of object names, positions, and confidence levels.</returns>
        /// <response code="200">Array of predictions.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        [HttpPost("face/recognize", Name = "RecognizeFaces")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> RecognizeFaces([FromForm] IFormFile image,
                                                       [FromForm] float? min_confidence,
                                                       CancellationToken token)
        {
            var backendResponse = await _dispatcher.RecognizeFaces(image, min_confidence, token);
            if (backendResponse is BackendFaceRecognitionResponse detectResponse)
            {
                var response = new RecognizeFacesResponse
                {
                    predictions = detectResponse?.predictions
                                 ?.OrderBy(prediction => prediction.confidence)
                                 ?.Select(prediction => new RecognizedFace
                                 {
                                     confidence = prediction.confidence,
                                     userid = prediction.userid,
                                     x_min = (int)(prediction.x_min),
                                     x_max = (int)(prediction.x_max),
                                     y_min = (int)(prediction.y_min),
                                     y_max = (int)(prediction.y_max),

                                 })
                                 ?.ToArray()
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        /// <summary>
        /// List Faces registered for recognition.
        /// </summary>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>A list of object names, positions, and confidence levels.</returns>
        /// <response code="200">Array of predictions.</response>
        /// <response code="400">If the image in the Form data is null.</response>            
        /// <remarks>This method should be a GET, not a POST operation. We've left it as POST in
        /// order to maintain compatibility with the original DeepStack code.</remarks>
        [HttpPost("face/list", Name = "ListFaces")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> ListRegisteredFaces(CancellationToken token)
        {
            var backendResponse = await _dispatcher.ListFaces(token);
            if (backendResponse is BackendListRegisteredFacesResponse listResponse)
            {
                var response = new ListRegisteredFacesResponse
                {
                    faces = listResponse?.faces
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }

        /// <summary>
        /// Delete a registered face.
        /// </summary>
        /// <param name="userid">The ID of the user whose face info should be deleted</param>
        /// <param name="token">The injected request aborted cancellation token.</param>
        /// <returns>Success indication.</returns>
        /// <response code="200">Array of predictions.</response>
        /// <response code="400">If the image in the Form data is null.</response>
        /// <remarks>This method should be a DELETE, not a POST operation. We've left it as POST in
        /// order to maintain compatibility with the original DeepStack code.</remarks>
        [HttpPost("face/delete", Name = "DeleteFaces")]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ResponseBase> DeleteRegisteredFaces([FromForm] string userid,
                                                               CancellationToken token)
        {
            var backendResponse = await _dispatcher.DeleteFaces(userid, token);
            if (backendResponse is BackendFaceDeleteResponse deleteResponse)
            {
                var response = new DeleteFaceResponse
                {
                    success = deleteResponse.success
                };

                return response;
            }

            return HandleErrorResponse(backendResponse);
        }
    }
}
