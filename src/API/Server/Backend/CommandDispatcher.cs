using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CodeProject.SenseAI.API.Server.Backend
{
    /// <summary>
    /// Creates and Dispatches commands appropriately.
    /// </summary>
    public  class CommandDispatcher
    {
        private const string SceneQueueName     = "scene_queue";
        private const string DetectionQueueName = "detection_queue";
        private const string FaceQueueName      = "face_queue";

        private readonly QueueServices  _queueServices;
        private readonly BackendOptions _settings;

        /// <summary>
        /// Initializes a new instance of the CommandDispatcher class.
        /// </summary>
        /// <param name="queueServices">The Queue Services.</param>
        /// <param name="options">The Backend Options.</param>
        public CommandDispatcher(QueueServices queueServices, IOptions<BackendOptions> options)
        {
            _queueServices = queueServices;
            _settings      = options.Value;
        }

        public void CreateKnownQueues()
        {
            _queueServices.EnsureQueueExists(SceneQueueName);
            _queueServices.EnsureQueueExists(DetectionQueueName);
            _queueServices.EnsureQueueExists(FaceQueueName);
        }

        /// <summary>
        /// Saves a Form File to the temp directory.
        /// </summary>
        /// <param name="image">The Form File.</param>
        /// <returns>The saved file name.</returns>
        public async Task<string?> SaveFileToTempAsync(IFormFile image)
        {
            var imageFilename = $"{Guid.NewGuid():B}{Path.GetExtension(image.FileName)}";

            var tempDir       = Path.GetTempPath();

            var dirPath       = Path.Combine(tempDir, _settings.ImageTempDir);
            var di            = new DirectoryInfo(dirPath);
            if (!di.Exists)
                di.Create();

            var filePath      = Path.Combine(tempDir, _settings.ImageTempDir, imageFilename);
            var fileInfo      = new FileInfo(filePath);

            try
            {
                using var imageStream = image.OpenReadStream();
                using var tempFile    = fileInfo.OpenWrite();
                await imageStream.CopyToAsync(tempFile);
            }
            catch
            {
                return null;
            }

            return filePath;
        }

        /// <summary>
        /// Executes a Detect Objects Command.
        /// </summary>
        /// <param name="image">The Form File for the image to be processed.</param>
        /// <param name="minConfidence">The minimum confidence for the detected objects.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A list of the detected objects or an error response.</returns>
        public async Task<BackendResponseBase> DetectObjects(IFormFile image, float? minConfidence, 
                                                             CancellationToken token = default)
        {
            var filename = await SaveFileToTempAsync(image);
            if (filename == null)
                return new BackendErrorResponse(-1, "Unable to save file");

            try
            {
                var response = await _queueServices.SendRequestAsync<BackendObjectDetectionResponse>(DetectionQueueName,
                                                    new BackendObjectDetectionRequest(filename, minConfidence ?? 0.4F)
                                                    , token);
                return response;
            }
            finally
            {
                var fileInfo = new FileInfo(filename);
                fileInfo.Delete();
            }
        }

        /// <summary>
        /// Executes a Detect Scene Command.
        /// </summary>
        /// <param name="image">The Form File for the image to be processed.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A label and confidence for the detected scene or an error response.</returns>
        public async Task<BackendResponseBase> DetectScene(IFormFile image,
                                                           CancellationToken token = default)
        {
            var filename = await SaveFileToTempAsync(image);
            if (filename == null)
                return new BackendErrorResponse(-1, "Unable to save file");

            try
            {
                var response = await _queueServices.SendRequestAsync<BackendSceneDetectResponse>(SceneQueueName,
                                                    new BackendSceneDetectionRequest(filename), token);
                return response;
            }
            finally
            {
                var fileInfo = new FileInfo(filename);
                fileInfo.Delete();
            }
        }


        /// <summary>
        /// Executes a Detect Faces Command.
        /// </summary>
        /// <param name="image">The Form File for the image to be processed.</param>
        /// <param name="minConfidence">The minimum confidence for the detected objects.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A list of the detected Faces or an error response.</returns>
        public async Task<BackendResponseBase> DetectFaces(IFormFile image, float? minConfidence,
                                                           CancellationToken token = default)
        {
            var filename = await SaveFileToTempAsync(image);
            if (filename == null)
                return new BackendErrorResponse(-1, "Unable to save file");

            try
            {
                var response = await _queueServices.SendRequestAsync<BackendFaceDetectionResponse>(FaceQueueName,
                                                    new BackendFaceDetectionRequest(filename, minConfidence ?? 0.4F)
                                                    , token);
                return response;
            }
            finally
            {
                var fileInfo = new FileInfo(filename);
                fileInfo.Delete();
            }
        }

        /// <summary>
        /// Executes a Match Faces Command.
        /// </summary>
        /// <param name="image1">The Form File for the image to be processed.</param>
        /// <param name="image2">The Form File for the image to be processed.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A value indicating the similarity of the two faces.</returns>
        public async Task<BackendResponseBase> MatchFaces(IFormFile image1, IFormFile image2,
                                                          CancellationToken token = default)
        {
            var filename1 = await SaveFileToTempAsync(image1);
            if (filename1 == null)
                return new BackendErrorResponse(-1, "Unable to save file1");

            var filename2 = await SaveFileToTempAsync(image2);
            if (filename2== null)
                return new BackendErrorResponse(-1, "Unable to save file2");

            try
            {
                var response = await _queueServices.SendRequestAsync<BackendFaceMatchResponse>(FaceQueueName,
                                                    new BackendFaceMatchRequest(filename1, filename2)
                                                    , token);
                return response;
            }
            finally
            {
                // delete the temporary files
                var fileInfo = new FileInfo(filename1);
                fileInfo.Delete();
                fileInfo = new FileInfo(filename2);
                fileInfo.Delete();
            }
        }

        /// <summary>
        /// Executes a Register Faces Command.
        /// </summary>
        /// <param name="userId">The images are of this user.</param>
        /// <param name="images">The Form File images of the given user.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A value indicating the similarity of the two faces.</returns>
        public async Task<BackendResponseBase> RegisterFaces(string userId, 
                                                             IFormFileCollection images,
                                                             CancellationToken token = default)
        {
            var filenames = new List<string>(images.Count);
            foreach (var image in images)
            {
                if (image == null)
                    continue;

                var filename = await SaveFileToTempAsync(image);
                if (filename == null)
                    return new BackendErrorResponse(-1, $"Unable to save {image.FileName}");

                filenames.Add(filename);
            }

            try
            {
                var response = await _queueServices.SendRequestAsync<BackendFaceRegisterResponse>(FaceQueueName,
                                                    new BackendFaceRegisterRequest(userId, filenames.ToArray()),
                                                    token);
                return response;
            }
            finally
            {
                // delete the temporary files
                foreach (var filename in filenames)
                {
                    var fileInfo = new FileInfo(filename);
                    fileInfo.Delete();
                }
            }
        }

        /// <summary>
        /// Executes a Recognize Faces Command.
        /// </summary>
        /// <param name="image">The image potentially containing faces.</param>
        /// <param name="minConfidence">The minimum confidence for the detected faces.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A list of the recognized Faces or an error response.</returns>
        public async Task<BackendResponseBase> RecognizeFaces(IFormFile image, float? minConfidence,
                                                              CancellationToken token = default)
        {
            var filename = await SaveFileToTempAsync(image);
            if (filename == null)
                return new BackendErrorResponse(-1, "Unable to save file");

            try
            {
                var response = await _queueServices.SendRequestAsync<BackendFaceRecognitionResponse>(FaceQueueName,
                                                    new BackendFaceRecognitionRequest(filename, minConfidence ?? 0.4F)
                                                    , token);
                return response;
            }
            finally
            {
                var fileInfo = new FileInfo(filename);
                fileInfo.Delete();
            }
        }

        /// <summary>
        /// Executes a List Faces Command.
        /// </summary>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A list of the registered Faces or an error response.</returns>
        public async Task<BackendResponseBase> ListFaces(CancellationToken token = default)
        {
            try
            {
                var response = await _queueServices.SendRequestAsync<BackendListRegisteredFacesResponse>(FaceQueueName,
                                                    new BackendFaceListRequest(), token);
                return response;
            }
            finally
            {
            }
        }

        /// <summary>
        /// Executes a Delete Faces Command.
        /// </summary>
        /// <param name="userId">The id of the user whose face data will be deleted.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A list of the recognized Faces or an error response.</returns>
        public async Task<BackendResponseBase> DeleteFaces(string userid,
                                                           CancellationToken token = default)
        {
            try
            {
                var response = await _queueServices.SendRequestAsync<BackendFaceDeleteResponse>(FaceQueueName,
                                                    new BackendFaceDeleteRequest(userid), token);
                return response;
            }
            finally
            {
            }
        }
    }
}
