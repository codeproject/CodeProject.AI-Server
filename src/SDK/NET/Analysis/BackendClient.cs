using System.Diagnostics;
using System.Dynamic;
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

        private static HttpClient? _httpGetRequestClient;
        private static HttpClient? _httpSendResponseClient;
        private Channel<LoggingData> _loggingQueue = Channel.CreateBounded<LoggingData>(1024);

        private int _errorPauseSecs = 0;
        private Task? loggingTask;

        /// <summary>
        /// Creates a new instance of a BackendClient object.
        /// </summary>
        /// <param name="url">The URL of the API server</param>
        /// <param name="getRequestTimeout">The timeout for getting a request from the server's
        /// request queue. This is essentially the "long poll" timeout.
        /// <param name="sendResponseTimeout">The timeout for sending a response back to the server.
        /// <param name="token">A CancellationToken.</param>
        public BackendClient(string url,
                             TimeSpan getRequestTimeout = default,
                             TimeSpan sendResponseTimeout = default,
                             CancellationToken token = default)
        {
            _httpGetRequestClient ??= new HttpClient
            {
                BaseAddress = new Uri(url),
                Timeout     = (getRequestTimeout == default) ? TimeSpan.FromSeconds(15) : getRequestTimeout
            };

            _httpSendResponseClient ??= new HttpClient
            {
                BaseAddress = new Uri(url),
                Timeout     = (sendResponseTimeout == default) ? TimeSpan.FromSeconds(30) : sendResponseTimeout
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
        /// <param name="canUseGPU">Whether or not this module can make use of the current GPU</param>
        /// <returns>The BackendRequest or Null if error</returns>
        public async Task<BackendRequest?> GetRequest(string queueName, string moduleId,
                                                      CancellationToken token = default)
        {
            // We're passing the moduleID as part of the GET request in order to give the server a
            // hint that this module is alive and well.
            string requestUri = $"v1/queue/{queueName.ToLower()}?moduleId={moduleId}";

            BackendRequest? request = null;
            try
            {
                // HttpResponseMessage is a disposable object, so make sure to dispose of it.
                using HttpResponseMessage response = await _httpGetRequestClient!.GetAsync(requestUri, token);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    request = await response.Content.ReadFromJsonAsync<BackendRequest>();
            }
            catch (JsonException)
            {
#if DEBUG
                Debug.WriteLine($"JsonException in GetRequest for {moduleId}");
#endif
            }
            catch (TimeoutException)
            {
#if DEBUG
                Debug.WriteLine($"Timeout in GetRequest for {moduleId}");
#endif
            }
            catch (TaskCanceledException)
            {
#if DEBUG
                Debug.WriteLine($"TaskCanceledException in GetRequest for {moduleId}");
#endif
            }
#if DEBUG
            catch (Exception ex)
            {
                Debug.WriteLine("Error in GetRequest: " + ex.Message);
#else
            catch (Exception /*ex*/)
            {
#endif
                Console.WriteLine($"Unable to get request from {queueName} for {moduleId}");
                _errorPauseSecs = Math.Min(_errorPauseSecs > 0 ? _errorPauseSecs + 1 : 5, 30);

                if (!token.IsCancellationRequested && _errorPauseSecs > 0)
                {
                    Console.WriteLine($"Pausing on error for {_errorPauseSecs} secs.");
                    try
                    {
                        await Task.Delay(_errorPauseSecs * 1_000, token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                    }
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
                using (var response = await _httpSendResponseClient!.PostAsync($"v1/queue/{reqid}", content, token)
                                              .ConfigureAwait(false))
                {
                    // We're not doing anything with the response, but we need to dispose of it.
                }
            }
            catch 
            {
                Console.WriteLine($"Unable to send response from module {moduleId} (#reqid {reqid})");
            }
        }

        /// <summary>
        /// Sends status to the CodeProject.AI Server.
        /// </summary>
        /// <param name="moduleId">The module sending this response.</param>
        /// <param name="token">A Cancellation Token.</param>
        /// <returns>A Task.</returns>
        public async Task SendModuleStatus(string moduleId, ExpandoObject? statusData, CancellationToken token)
        {
            MultipartFormDataContent content = new()
            {
                { new StringContent(moduleId), "moduleId" },
            };

            if (statusData != null)
            {
                string json = JsonSerializer.Serialize(statusData, jsonSerializerOptions);
                content.Add(new StringContent(json), "statusData");
            }

            try
            {
                using (var response = await _httpSendResponseClient!.PostAsync($"v1/queue/updatemodulestatus/{moduleId}",
                                                         content, token)
                                              .ConfigureAwait(false))
                {
                    // We're not doing anything with the response, but we need to dispose of it.
                }
            }
            catch
            {
                Console.WriteLine($"Unable to send status from module {moduleId}");
            }
        }

        /// <summary>
        /// Logs a message to the CodeProject.AI Server.
        /// </summary>
        /// <param name="message">The Message.</param>
        /// <param name="category">The log category</param>
        /// <param name="logLevel">The log level.</param>
        /// <param name="label">The label</param>
        /// <returns>True if added to the logging message queue, false if dropped.</returns>
        public bool LogToServer(string message, string category,
                                      LogLevel logLevel, string label)
        {
            return _loggingQueue.Writer.TryWrite(new LoggingData(message, category, logLevel, label));
        }

        /// <summary>
        /// Called to process the logging data pulled off a queue by a background task. See the
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
                using (var response = await _httpSendResponseClient!.PostAsync($"v1/log", form, token)
                                     .ConfigureAwait(false))
                {
                       // We're not doing anything with the response, but we need to dispose of it.
                }
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
                        Debug.Write("Error processing logging queue: " + e.Message);
                    }
                }
            }

            _loggingQueue.Writer.Complete();
        }
    }
}