using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using CodeProject.SenseAI.API.Common;

namespace CodeProject.SenseAI.Demo.Playground
{
    /// <summary>
    /// This is an example of a .NET client to call CodeProject SenseAI API Server.
    /// TODO: move under an SDKs directory called CodeProject.SenseAI.Sdk.Net.
    /// </summary>
    public class ApiClient
    {
        private HttpClient? _client;

        /// <summary>
        /// Gets or sets the PORT for making calls to the API
        /// </summary>
        public int Port { get; set; } = 5000;

        /// <summary>
        /// Gets or sets the timeout in seconds for making calls to the API
        /// </summary>
        public int Timeout { get; set; } = 5;

        /// <summary>
        /// Gets the HttpClient
        /// </summary>
        private HttpClient Client
        {
            get
            {
                if (_client is not null)
                {
                    if (_client?.BaseAddress?.Port != Port || _client.Timeout.TotalSeconds != Timeout)
                    {
                        _client?.Dispose();
                        _client = null;
                    }
                }

                if (_client == null)
                {
                    _client = new HttpClient
                    {
                        BaseAddress = new Uri($"http://localhost:{Port}/v1/"),
                        Timeout     = new TimeSpan(0, 0, Timeout)
                    };
                }

                return _client;
            }
        }

        /// <summary>
        /// Initializes a new instance of the ApiClient class.
        /// </summary>
        /// <param name="port">The oort for the HTTP calls</param>
        public ApiClient(int port)
        {
            Port = port;
        }

        /// <summary>
        /// Ping the server's health status
        /// </summary>
        /// <returns>A SuccessResponse object</returns>
        public async Task<ResponseBase> Ping()
        {
            ResponseBase? response = null;
            try
            {
                var content = new MultipartFormDataContent();
                using var httpResponse = await Client.GetAsync("status/ping");
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<SuccessResponse>();
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Find faces in an image.
        /// </summary>
        /// <param name="image_path">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles for the faces found, if any.</returns>
        public async Task<ResponseBase> DetectFaces(string image_path)
        {
            ResponseBase? response;

            var fileInfo = new FileInfo(image_path);
            if (!fileInfo.Exists)
                return new ErrorResponse("Image does not exist");

            var request = new MultipartFormDataContent();

            try
            {
                var image_data = fileInfo.OpenRead();
                var content    = new StreamContent(image_data);

                request.Add(content, "image", Path.GetFileName(image_path));

                using var httpResponse = await Client.PostAsync("vision/face", request);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<DetectFacesResponse>();
                response ??= new ErrorResponse("No response from the server");

            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Compares two images to see if the face(s) in the images are similar.
        /// </summary>
        /// <param name="image1FileName">The path to the first image file.</param>
        /// <param name="image2FileName">The pathe to the second image file.</param>
        /// <returns>A response that contains the similarity of the faces.</returns>
        public async Task<ResponseBase> MatchFaces(string image1FileName, string image2FileName)
        {
            ResponseBase? response;
            var f1 = new FileInfo(image1FileName);
            var f2 = new FileInfo(image2FileName);

            if (!f1.Exists)
                return new ErrorResponse("Image1 does not exist");
            if (!f2.Exists)
                return new ErrorResponse("Image2 does not exist" );
                
            var request = new MultipartFormDataContent();

            try
            {
                var image1_data = f1.OpenRead();
                var image2_data = f2.OpenRead();

                request.Add(new StreamContent(image1_data), "image1", Path.GetFileName(image1FileName));
                request.Add(new StreamContent(image2_data), "image2", Path.GetFileName(image2FileName));

                using var httpResponse = await Client.PostAsync("vision/face/match", request);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<MatchFacesResponse>();
                response ??= new ErrorResponse("No response from the server"); 
            }
            catch (Exception ex)
            {
                response = new ErrorResponse (ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Identify a scene in an image.
        /// </summary>
        /// <param name="image_path">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles for the faces found, if any.</returns>
        public async Task<ResponseBase> DetectScene(string image_path)
        {
            ResponseBase? response;
            var fileInfo = new FileInfo(image_path);
            if (!fileInfo.Exists)
                return new ErrorResponse("Image does not exist");

            var request = new MultipartFormDataContent();

            try
            {
                var image_data = fileInfo.OpenRead();

                request.Add(new StreamContent(image_data), "image", Path.GetFileName(image_path));

                using var httpResponse = await Client.PostAsync("vision/scene", request);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<DetectSceneResponse>();
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Identify a scene in an image.
        /// </summary>
        /// <param name="image_path">The path to the image file.</param>
        /// <returns>A response that has bounding rectangles and labels for the objects found, if
        /// any.</returns>
        public async Task<ResponseBase> DetectObjects(string image_path)
        {
            ResponseBase? response;
            var fileInfo = new FileInfo(image_path);
            if (!fileInfo.Exists)
                return new ErrorResponse("Image does not exist");

            var request = new MultipartFormDataContent();

            try
            {
                var image_data = fileInfo.OpenRead();

                request.Add(new StreamContent(image_data), "image", Path.GetFileName(image_path));

                using var httpResponse = await Client.PostAsync("vision/detection", request);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<DetectObjectsResponse>();
                //var json = await httpResponse.Content.ReadAsStringAsync();
                //response = System.Text.Json.JsonSerializer.Deserialize<DetectObjectsResponse>(json);
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Registers one or more face images against a user id.
        /// </summary>
        /// <param name="userId">The user id or name.</param>
        /// <param name="registerFileNames">The list of filename.</param>
        public async Task<ResponseBase> RegisterFace(string userId,
                                                     IEnumerable<string> registerFileNames)
        {
            ResponseBase? response = null;

            if (string.IsNullOrWhiteSpace(userId))
                return new ErrorResponse("No user id provided");

            if (!registerFileNames.Any())
                return new ErrorResponse("No valid image file found");

            var request = new MultipartFormDataContent();

            try
            {
                request.Add(new StringContent(userId), "userid");

                foreach (var (filename, index) in registerFileNames.Select((name, index) => (name, index)))
                {
                    var fileInfo = new FileInfo(filename);
                    if (!fileInfo.Exists)
                        return new ErrorResponse($"{filename} not found.");

                    var image_data = fileInfo.OpenRead();

                    request.Add(new StreamContent(image_data), $"image{index+1}",
                                                  Path.GetFileName(filename));
                }

                using var httpResponse = await Client.PostAsync("vision/face/register", request);
                httpResponse.EnsureSuccessStatusCode();

                // response = await httpResponse.Content.ReadFromJsonAsync<RegisterFaceResponse>();
                var json = await httpResponse.Content.ReadAsStringAsync();
                response = System.Text.Json.JsonSerializer.Deserialize<RegisterFaceResponse>(json);
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Recognizes one or more face images in an image.
        /// </summary>
        /// <param name="fileNames">The filename.</param>
        public async Task<ResponseBase> RecognizeFace(string? filename,
                                                      float? minConfidence = null)
        {
            ResponseBase? response;

            if (string.IsNullOrWhiteSpace(filename))
                return new ErrorResponse("No valid image filename");

            var request = new MultipartFormDataContent();

            try
            {
                var fileInfo = new FileInfo(filename);
                if (!fileInfo.Exists)
                    return new ErrorResponse($"{filename} not found.");

                var image_data = fileInfo.OpenRead();

                request.Add(new StreamContent(image_data), "image", Path.GetFileName(filename));
                if (minConfidence.HasValue)
                    request.Add(new StringContent(minConfidence.Value.ToString()), "min_confidence");

                using var httpResponse = await Client.PostAsync("vision/face/recognize", request);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<RecognizeFacesResponse>();
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        public async Task<ResponseBase> DeleteRegisteredFace(string userId)
        {
            ResponseBase? response = null;
            if (string.IsNullOrWhiteSpace(userId))
                return new ErrorResponse("No user id provided");

            try
            {
                var request = new MultipartFormDataContent
                {
                    { new StringContent(userId), "userid" }
                };

                using var httpResponse = await Client.PostAsync("vision/face/delete", request);
                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<DeleteFaceResponse>();
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }

        public async Task<ResponseBase> ListRegisteredFaces()
        {
            ResponseBase? response = null;
            try
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                using var httpResponse = await Client.PostAsync("vision/face/list", null);
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

                httpResponse.EnsureSuccessStatusCode();

                response = await httpResponse.Content.ReadFromJsonAsync<ListRegisteredFacesResponse>();
                response ??= new ErrorResponse("No response from the server");
            }
            catch (Exception ex)
            {
                response = new ErrorResponse(ex.Message);
            }

            return response;
        }
    }
}