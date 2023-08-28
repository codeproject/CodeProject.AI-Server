using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

namespace CodeProject.AI.SDK
{
    /// <summary>
    /// Represents an HTTP client to get requests and return responses to the CodeProject.AI server.
    /// </summary>
    public class BackendClient
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions =
                new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private record  LoggingData(string message, string category, LogLevel logLevel, string label);

        private static HttpClient? _httpClient;
        private Channel<LoggingData> _loggingQueue = Channel.CreateBounded<LoggingData>(1024);

        private int _errorPauseSecs = 0;
        private Task? loggingTask;

        /// <summary>
        /// Creates a new instance of a BackendClient object.
        /// </summary>
        /// <param name="url">The URL of the API server</param>
        /// <param name="timeout">The timeout</param>
        /// <param name="token">A CancellationToken.</param>
        public BackendClient(string url, TimeSpan timeout = default, CancellationToken token = default)
        {
            _httpClient ??= new HttpClient
            {
                BaseAddress = new Uri(url),
                Timeout = (timeout == default) ? TimeSpan.FromMinutes(1) : timeout
            };

            loggingTask = ProcessLoggingQueue(token);
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

            // TODO: A better way to pass this is via header:
            // string requestUri = $"v1/queue/{queueName}";
            // var request = new HttpRequestMessage() {
            //     RequestUri = new Uri(requestUri),
            //     Method = HttpMethod.Get,
            // };
            // request.DefaultRequestHeaders.Add("X-CPAI-Moduleid", moduleid);
            // if (executionProvider != null)
            //     request.DefaultRequestHeaders.Add("X-CPAI-ExecutionProvider", executionProvider);
            // httpResponse = await _httpClient!.SendAsync(request, token).ConfigureAwait(false);

            string requestUri = $"v1/queue/{queueName.ToLower()}?moduleid={moduleId}";
            if (executionProvider != null)
                requestUri += $"&executionProvider={executionProvider}";

            BackendRequest? request = null;
            try
            {
                request = await _httpClient!.GetFromJsonAsync<BackendRequest>(requestUri, token)
                                            .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // This is probably due to timing out and therefore no JSON to parse.
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Console.WriteLine($"Unable to get request from {queueName} for {moduleId}");
                _errorPauseSecs = Math.Min(_errorPauseSecs > 0 ? _errorPauseSecs * 2 : 5, 60);

                if (!token.IsCancellationRequested && _errorPauseSecs > 0)
                {
                    Console.WriteLine($"Pausing on error for {_errorPauseSecs} secs.");
                    await Task.Delay(_errorPauseSecs * 1_000, token).ConfigureAwait(false);
                }
            }

            return request;
        }

        /// <summary>
        /// Sends a response for a request to the CodeProject.AI Server.
        /// </summary>
        /// <param name="reqid">The Request ID.</param>
        /// <param name="moduleId">The module sending this response.</param>
        /// <param name="content">The content to send.</param>
        /// <param name="token">A Cancellation Token.</param>
        /// <returns>A Task.</returns>
        public async Task SendResponse(string reqid, string moduleId, HttpContent content,
                                       CancellationToken token)
        {
            try
            {
                await _httpClient!.PostAsync($"v1/queue/{reqid}", content, token)
                                  .ConfigureAwait(false);
            }
            catch 
            {
                Console.WriteLine($"Unable to send response from module {moduleId} (#reqid {reqid})");
            }
        }

        /// <summary>
        /// Logs a message to the CodeProject.AI Server.
        /// </summary>
        /// <param name="message">The Message.</param>
        /// <param name="category">The log category</param>
        /// <param name="logLevel">The log level.</param>
        /// <param name="label">The label</param>
        /// <param name="token">A Cancellation Token.</param>
        /// <returns>A Task.</returns>
        public ValueTask LogToServer(string message, string category,
                                      LogLevel logLevel, string label,
                                      CancellationToken token)
        {
            _loggingQueue.Writer.TryWrite(new LoggingData(message, category, logLevel, label));
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Called to process the logging data pulled off a queue by a bacground task. See the
        /// LogToServer method above.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="token"></param>
        /// <returns>A Task</returns>
        private async Task SendLoggingData(LoggingData data, CancellationToken token)
        { 
            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string?, string?>("entry",     data.message),
                new KeyValuePair<string?, string?>("category",  data.category),
                new KeyValuePair<string?, string?>("label",     data.label),
                new KeyValuePair<string?, string?>("log_level", data.logLevel.ToString())
            });

            try
            {
                await _httpClient!.PostAsync($"v1/log", form, token).ConfigureAwait(false);
            }
            catch
            {
                Console.WriteLine($"Unable to send message \"{data.message}\" to API server");
            }
        }

        private async Task ProcessLoggingQueue(CancellationToken token = default)
        {
            while(!token.IsCancellationRequested)
            {
                LoggingData data = await _loggingQueue.Reader.ReadAsync(token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    try
                    {
                        await SendLoggingData(data, token).ConfigureAwait(false);
                    }
                    catch(Exception e)
                    {
                        Debug.Write(e);
                    }
                }
            }

            _loggingQueue.Writer.Complete();
        }
    }
}