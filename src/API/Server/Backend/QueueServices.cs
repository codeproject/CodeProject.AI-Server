

using Microsoft.Extensions.Options;

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using CodeProject.SenseAI.AnalysisLayer.SDK;
using CodeProject.SenseAI.API.Common;

namespace CodeProject.SenseAI.API.Server.Backend
{
    /// <summary>
    /// Manages the Concurrent Queues
    /// </summary>
    public class QueueServices
    {
        private readonly QueueProcessingOptions _settings;

        // Keeping track of the queues being used.  Will be created as needed.
        private readonly ConcurrentDictionary<string, Channel<BackendRequestBase>> _queues =
                            new ConcurrentDictionary<string, Channel<BackendRequestBase>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> _pendingResponses =
                            new ConcurrentDictionary<string, TaskCompletionSource<string?>>();

        /// <summary>
        /// Creates a new instance of the <cref="QueueServices" /> object.
        /// </summary>
        /// <param name="options">The queue processing options.</param>
        public QueueServices(IOptions<QueueProcessingOptions> options)
        {
            _settings = options.Value;
        }

        public bool EnsureQueueExists(string queueName)
        {
            return GetOrCreateQueue(queueName) != null;
        }

        /// <summary>
        /// Pushes a request onto a named queue.  The request will be handled by a backened process.
        /// </summary>
        /// <param name="queueName">The name of the queue.</param>
        /// <param name="request">The Request to be processed.</param>
        /// <returns>The response.</returns>
        public async ValueTask<Object> SendRequestAsync(string queueName,
                                                        BackendRequestBase request,
                                                        CancellationToken token = default)
        {
            Channel<BackendRequestBase> queue = GetOrCreateQueue(queueName);

            // the backend process will return a json string as a response.
            var completion = new TaskCompletionSource<string?>();

            // when the request is dequeued will have to check that the pending response exists and
            // that the task is not completed.
            if (!_pendingResponses.TryAdd(request.reqid, completion))
            {
                return new BackendErrorResponse(-3, $"Unable to add pending response id {request.reqid}.");
            }

            // setup a request timeout.
            using var cancelationSource = new CancellationTokenSource(_settings.ResponseTimeout);
            var timeoutToken = cancelationSource.Token;

            try
            {
                CancellationToken theToken =
                    CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken).Token;

                // setup the timeout callback.
                theToken.Register(() => { completion.TrySetCanceled(); });

                try
                {
                    await queue.Writer.WriteAsync(request, theToken).ConfigureAwait(false);

                    if (request != null)
                        Logger.Log($"Queued: '{request.reqtype}' request, id {request.reqid}");
                }
                catch (OperationCanceledException)
                {
                    if (timeoutToken.IsCancellationRequested)
                        return new BackendErrorResponse(-1, "request queue is full.");
                    else
                        return new BackendErrorResponse(-6, "the request was canceled by caller.");
                }

                var jsonString = await completion.Task;

                if (jsonString is null)
                {
                    return new BackendErrorResponse(-5, "null json returned from backend.");
                }
                else
                {
                    // var response = JsonSerializer.Deserialize<ResponseType>(jsonString);
                    return jsonString;
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutToken.IsCancellationRequested)
                    return new BackendErrorResponse(-1, "The request timed out.");
                else
                    return new BackendErrorResponse(-6, "the request was canceled by caller.");
            }
            catch (JsonException)
            {
                return new BackendErrorResponse(-4, "Invalid JSON response from backend.");
            }
            catch (Exception ex)
            {
                return new BackendErrorResponse(-2, ex.Message);
            }
            finally
            {
                _pendingResponses.TryRemove(request.reqid, out completion);
            }
        }

        private Channel<BackendRequestBase> GetOrCreateQueue(string queueName)
        {
            return _queues.GetOrAdd(queueName,
                            Channel.CreateBounded<BackendRequestBase>(_settings.MaxQueueLength));
        }

        /// <summary>
        /// Set the result for the request.
        /// </summary>
        /// <param name="req_id">The Id of the Request.</param>
        /// <param name="responseString">The response value.</param>
        /// <returns></returns>
        public bool SetResult(string req_id, string? responseString)
        {
            if (!_pendingResponses.TryGetValue(req_id, out TaskCompletionSource<string?>? completion))
                return false;

            completion.SetResult(responseString);
            Logger.Log($"Response received: id {req_id}");

            return true;
        }

        /// <summary>
        /// Get a request from the queue.
        /// </summary>
        /// <param name="queueName">The name of the queue.</param>
        /// <returns>A request if available. null if the queue does not exist or empty.</returns>
        public BackendRequestBase? DequeueRequest(string queueName)
        {
            if(!_queues.TryGetValue(queueName, out Channel<BackendRequestBase>? queue))
                return null;

            BackendRequestBase? request;
            do
            {
                if (!queue.Reader.TryRead(out request))
                    return null;

            } while (!ValidateRequest(request));

            return request;
        }

        /// <summary>
        /// Get a request from the queue.
        /// </summary>
        /// <param name="queueName">The name of the queue.</param>
        /// <returns>A request if available. null if the queue does not exist 
        ///     or is cancelled waiting for a value.</returns>
        public async ValueTask<BackendRequestBase?> DequeueRequestAsync(string queueName, 
                                                                        CancellationToken token = default)
        {
            if (!_queues.TryGetValue(queueName, out Channel<BackendRequestBase>? queue))
                return null;

            BackendRequestBase? request = null;
            do
            {

                // setup a request timeout.
                using var cancelationSource = new CancellationTokenSource(_settings.CommandDequeueTimeout);
                var timeoutToken = cancelationSource.Token;
                var theToken = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken).Token;

                // NOTE FOR VS CODE users: In debug, you may want to uncheck "All Exceptions" under the
                // breakpoints section (the bottom section) of the Run and Debug tab.

                try
                {
                    request = await queue.Reader.ReadAsync(theToken).ConfigureAwait(false);
                    if (request != null)
                        Logger.Log($"Dequeued: '{request.reqtype}' request, id {request.reqid}");
                }
                catch
                {
                    return null;
                }
            }
            while (request == null || !ValidateRequest(request));  

            return request;
        }

        private bool ValidateRequest(BackendRequestBase request)
        {
            var reqId = request.reqid;
            return _pendingResponses.TryGetValue(reqId, out var completion)
                && completion != null
                && !completion.Task.IsCompleted;
        }
    }
}
