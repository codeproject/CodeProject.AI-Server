
using System.Text.Json;

namespace CodeProject.AI.AnalysisLayer.SDK
{
    /// <summary>
    /// Represents an HTTP client to get requests and return responses to the CodeProject.AI server.
    /// </summary>
    public class BackendClient
    {
        private static HttpClient? _httpClient;

        /// <summary>
        /// Creates a new instance of a BackendClient object.
        /// </summary>
        /// <param name="url">The URL of the API server</param>
        /// <param name="timeout">The timeout</param>
        public BackendClient(string url, TimeSpan timeout = default)
        {
            _httpClient ??= new HttpClient
            {
                BaseAddress = new Uri(url),
                Timeout = (timeout == default) ? TimeSpan.FromMinutes(1) : timeout
            };
        }

        /// <summary>
        /// Get a request from the CodeProject.AI Server queue.
        /// </summary>
        /// <param name="queueName">The Queue Name.</param>
        /// <param name="moduleId">The Id of the module making this request</param>
        /// <param name="token">A Cancellation Token.</param>
        /// <param name="executionProvider">The hardware acceleration execution provider</param>
        /// <returns>The BackendRequest or Null if error</returns>
        public async Task<BackendRequest?> GetRequest(string queueName, string moduleId,
                                                      CancellationToken token = default,
                                                      string? executionProvider = null)
        {
            // We're passing the moduleID as part of the GET request in order to give the server a
            // hint that this module is alive and well.
            string requestUri = $"v1/queue/{queueName}?moduleid={moduleId}";
            if (executionProvider != null)
                requestUri += $"&executionProvider={executionProvider}";

            BackendRequest? request = null;
            var httpResponse = await _httpClient!.GetAsync(requestUri, token).ConfigureAwait(false);

            if (httpResponse is not null &&
                httpResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var jsonString = await httpResponse.Content.ReadAsStringAsync(token)
                                                   .ConfigureAwait(false);

                request = JsonSerializer.Deserialize<BackendRequest>(jsonString,
                                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }

            return request;
        }

        /// <summary>
        /// Sends a response for a request to the CodeProject.AI Server.
        /// </summary>
        /// <param name="reqid">The Request ID.</param>
        /// <param name="moduleId">The Id of the module making this request</param>
        /// <param name="content">The content to send.</param>
        /// <param name="token">A Cancellation Token.</param>
        /// <param name="executionProvider">The hardware acceleration execution provider</param>
        /// <returns>A Task.</returns>
        public async Task SendResponse(string reqid, string moduleId, HttpContent content,
                                       CancellationToken token, string? executionProvider = null)
        {
            string requestUri = $"v1/queue/{reqid}?moduleid={moduleId}";
            if (executionProvider != null)
                requestUri += $"&executionProvider={executionProvider}";

            await _httpClient!.PostAsync(requestUri, content, token)
                              .ConfigureAwait(false);
        }

        /// <summary>
        /// Logs a message to the CodeProject.AI Server.
        /// </summary>
        /// <param name="message">The Message.</param>
        /// <param name="token">A Cancellation Token.</param>
        /// <returns>A Task.</returns>
        public async Task LogToServer(string message, CancellationToken token)
        {
            var form = new FormUrlEncodedContent(new[]
                { new KeyValuePair<string?, string?>("entry", message)}
            );

            /*var response = */
            await _httpClient!.PostAsync($"v1/log", form, token)
                              .ConfigureAwait(false);
        }
    }
}