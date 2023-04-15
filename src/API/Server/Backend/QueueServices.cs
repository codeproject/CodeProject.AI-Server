

using Microsoft.Extensions.Options;

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using CodeProject.AI.SDK;
using Microsoft.Extensions.Logging;

namespace CodeProject.AI.API.Server.Backend
{
    /// <summary>
    /// Manages the Concurrent Queues
    /// </summary>
    public class QueueServices
    {
        private readonly QueueProcessingOptions _settings;
        private readonly ILogger _logger;

        // Keeping track of the queues being used.  Will be created as needed.
        private readonly ConcurrentDictionary<string, Channel<BackendRequestBase>> _queues =
                            new ConcurrentDictionary<string, Channel<BackendRequestBase>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> _pendingResponses =
                            new ConcurrentDictionary<string, TaskCompletionSource<string?>>();

        /// <summary>
        /// Creates a new instance of the <cref="QueueServices" /> object.
        /// </summary>
        /// <param name="options">The queue processing options.</param>
        public QueueServices(IOptions<QueueProcessingOptions> options,
                             ILogger<QueueServices> logger)
        {
            _settings = options.Value;
            _logger   = logger;
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
        /// <remarks>We have to be super careful here. We're passing in a BackendRequestBase object
        /// which may be another type, such as BackendRequest. On the other end of this queue we'll
        /// pop this object and pass it to the QueueController, and it will be the original object.
        /// However, if we choose to use a queue mechanism that doesn't maintain the object (eg
        /// converts to Json) then we may not be able to simply cast this object. It is probably
        /// best to always pass a BackendRequest rather than BackendRequestBase, and accept that
        /// unless we're going to be fancy at the other end, the only things we can rely on are
        /// what's in the BackendRequest object.</remarks>
        public async ValueTask<object> SendRequestAsync(string queueName,
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
                string msg = $"Unable to add pending response id {request.reqid} to queue '{queueName}'.";
                return new BackendErrorResponse(msg);
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
                    _logger.LogTrace($"Client request '{request.reqtype}' in queue '{queueName}' (#reqid {request.reqid})");
                }
                catch (OperationCanceledException)
                {
                    if (timeoutToken.IsCancellationRequested)
                        return new BackendErrorResponse($"Request queue '{queueName}' is full (#reqid {request.reqid})");

                    return new BackendErrorResponse($"The request in '{queueName}' was canceled by caller (#reqid {request.reqid})");
                }

                var jsonString = await completion.Task;

                if (jsonString is null)
                    return new BackendErrorResponse($"null json returned from backend (#reqid {request.reqid})");

                    return jsonString;
                }
            catch (OperationCanceledException)
            {
                if (timeoutToken.IsCancellationRequested)
                    return new BackendErrorResponse($"The request timed out (#reqid {request.reqid})");

                    return new BackendErrorResponse($"The request was canceled by caller (#reqid {request.reqid})");
            }
            catch (JsonException)
            {
                return new BackendErrorResponse($"Invalid JSON response from backend (#reqid {request.reqid})");
            }
            catch (Exception ex)
            {
                return new BackendErrorResponse(ex.Message + $" (#reqid {request.reqid})");
            }
            finally
            {
                _pendingResponses.TryRemove(request.reqid, out completion);
            }
        }

        private Channel<BackendRequestBase> GetOrCreateQueue(string queueName)
        {
            return _queues.GetOrAdd(queueName.ToLower(),
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
            
            var response = JsonSerializer.Deserialize<JsonObject>(responseString ?? "");
            if (response?["message"] is not null)
                _logger.LogTrace($"Response received (#reqid {req_id}): {response["message"]}");
            else
                _logger.LogTrace($"Response received (#reqid {req_id})");

            return true;
        }

        /// <summary>
        /// Get a request from the queue.
        /// </summary>
        /// <param name="queueName">The name of the queue.</param>
        /// <returns>A request if available. null if the queue does not exist or empty.</returns>
        public BackendRequestBase? DequeueRequest(string queueName)
        {
            if(!_queues.TryGetValue(queueName.ToLower(), out Channel<BackendRequestBase>? queue))
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
        /// <returns>A request if available. null if the queue does not exist or is cancelled 
        /// waiting for a value.</returns>
        public async ValueTask<BackendRequestBase?> DequeueRequestAsync(string queueName, 
                                                                        CancellationToken token = default)
        {
            if (!_queues.TryGetValue(queueName.ToLower(), out Channel<BackendRequestBase>? queue))
                return null;

            BackendRequestBase? request = null;
            do
            {
                // setup a request timeout.
                using var cancellationSource = new CancellationTokenSource(_settings.CommandDequeueTimeout);
                var timeoutToken = cancellationSource.Token;
                var theToken = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken).Token;

                // NOTE FOR VS CODE users: In debug, you may want to uncheck "All Exceptions" under the
                // breakpoints section (the bottom section) of the Run and Debug tab.

                if (token.IsCancellationRequested || timeoutToken.IsCancellationRequested)
                    return null;

                try
                {
                    request = await queue.Reader.ReadAsync(theToken).ConfigureAwait(false);
                    if (request != null)
                        _logger.LogTrace($"Request '{request.reqtype}' dequeued from '{queueName}' (#reqid {request.reqid})");
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
