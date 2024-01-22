using System.Net.Http.Json;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.API
{
    /// <summary>
    /// Contains the data to be sent as part of a request to the server
    /// </summary>
    public class ServerRequestContent : MultipartFormDataContent
    {
        /// <summary>
        /// Adds a parameter to the request package
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <param name="value">The value of the parameter</param>
        /// <returns>true on success; false otherwise</returns>
        public bool AddParam(string name, string value)
        {
            Add(new StringContent(value), name);
            return true;
        }

        /// <summary>
        /// Adds a file to the request package
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <param name="name">The name of the file ("file" is default)</param>
        /// <returns>true on success; false otherwise</returns>
        public bool AddFile(string filePath, string name="file")
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
                return false;

            try
            {
                var image_data = fileInfo.OpenRead();
                Add(new StreamContent(image_data), name, Path.GetFileName(filePath));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// A base .NET API client for CodeProject.AI API Server.
    /// </summary>
    public class ApiClient: IDisposable
    {
        private HttpClient? _client;

        /// <summary>
        /// Gets or sets the IP address or host name for making calls to the API
        /// </summary>
        public string Hostname { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port for making calls to the API
        /// </summary>
        public int Port { get; set; } = 32168;

        /// <summary>
        /// Gets or sets the port for making calls to the API
        /// </summary>
        public string ApiVersion { get; set; } = "v1";

        /// <summary>
        /// Gets or sets the timeout in seconds for making calls to the API
        /// </summary>
        public int Timeout { get; set; } = 300;

        /// <summary>
        /// Gets the HttpClient
        /// </summary>
        protected HttpClient Client
        {
            get
            {
                if (_client is not null)
                {
                    if (_client.Timeout.TotalSeconds != Timeout)
                    {
                        _client?.Dispose();
                        _client = null;
                    }
                }

                if (_client is null)
                {
                    _client = new HttpClient
                    {
                        // BaseAddress = new Uri($"http://{Hostname}:{Port}/{ApiVersion}/"),
                        Timeout = TimeSpan.FromSeconds(Timeout)
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
        /// <returns>A <see cref="ServerResponse"/> object</returns>
        public async Task<ServerResponse> Ping()
        {
            return await GetAsync("server/status/ping");
        }

        /// <summary>
        /// Make a call to the server via the given route
        /// </summary>
        /// <param name="route">The route</param>
        /// <returns>A <see cref="ServerResponse"/> object</returns>
        public async Task<ServerResponse> GetAsync(string route)
        {
            ServerResponse? response = null;
            try
            {
                using HttpResponseMessage? httpResponse = await Client.GetAsync(GetUri(route))
                                                                      .ConfigureAwait(false);
                if (httpResponse?.IsSuccessStatusCode ?? false)
                {
                    response = await httpResponse.Content.ReadFromJsonAsync<ServerResponse>()
                                                         .ConfigureAwait(false);
                    if (response is null)
                        response = new ServerErrorResponse("No valid content returned from the server");
                    else
                        response.Code = httpResponse.StatusCode;
                }
                else
                    response = new ServerErrorResponse("Failed to get a valid response from the server");
            }
            catch (Exception ex)
            {
                response = new ServerErrorResponse("GetAsync error: " + ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Make a GET call to the server via the given route
        /// </summary>
        /// <param name="route">The route</param>
        /// <returns>A <see cref="ServerResponse"/> object</returns>
        public async Task<ServerResponse> GetAsync<T>(string route)
            where T : ServerResponse
        {
            ServerResponse? response = null;
            try
            {
                using HttpResponseMessage? httpResponse = await Client.GetAsync(GetUri(route))
                                                                      .ConfigureAwait(false);
                if (httpResponse?.IsSuccessStatusCode ?? false)
                {
                    response = await httpResponse.Content.ReadFromJsonAsync<T>()
                                                         .ConfigureAwait(false);
                    if (response is null)
                        response = new ServerErrorResponse("No valid content returned from the server");
                    else
                        response.Code = httpResponse.StatusCode;
                }
                else
                    response = new ServerErrorResponse("Failed to get a valid response from the server");
            }
            catch (Exception ex)
            {
                response = new ServerErrorResponse("GetAsync<T> error: " + ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Make a POST call to the server via the given route
        /// </summary>
        /// <param name="route">The route</param>
        /// <returns>A <see cref="ServerResponse"/> object</returns>
        public async Task<ServerResponse> PostAsync<T>(string route, ServerRequestContent? content = null) 
            where T : ServerResponse
        {
            ServerResponse? response;
            try
            {
                using var httpResponse = await Client.PostAsync(GetUri(route), content)
                                                     .ConfigureAwait(false);

                if (httpResponse?.IsSuccessStatusCode ?? false)
                {
                    response = await httpResponse.Content.ReadFromJsonAsync<T>()
                                                         .ConfigureAwait(false);
                    if (response is null)
                        response = new ServerErrorResponse("No valid content returned from the server");
                    else
                        response.Code = httpResponse.StatusCode;
                }
                else
                    response = new ServerErrorResponse("Failed to get a valid response from the server");
            }
            catch (Exception ex)
            {
                response = new ServerErrorResponse("PostAsync error: " + ex.Message);
            }

            return response;
        }

        protected Uri GetUri(string route)
        {
            return new Uri($"http://{Hostname}:{Port}/{ApiVersion}/{route}");
        }

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}