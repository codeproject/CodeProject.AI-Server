using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using CodeProject.AI.SDK;
using CodeProject.AI.SDK.API;

namespace CodeProject.AI.Server.Backend
{
    /// <summary>
    /// Manages the Concurrent Queues
    /// </summary>
    public class QueueServices
    {
        private readonly string[] _doNotLogCommands = { "list-custom" };

        private readonly QueueProcessingOptions _settings;
        private readonly ILogger _logger;

        // Keeping track of the queues being used.  Will be created as needed.
        private readonly ConcurrentDictionary<string, Channel<BackendRequestBase>> _queues =
                            new ConcurrentDictionary<string, Channel<BackendRequestBase>>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string?>> _pendingResponses =
                            new ConcurrentDictionary<string, TaskCompletionSource<string?>>();

        /// <summary>
        /// Creates a new instance of the <see cref="QueueServices" /> object.
        /// </summary>
        /// <param name="options">The queue processing options.</param>
        /// <param name="logger">The logger</param>
        public QueueServices(IOptions<QueueProcessingOptions> options,
                             ILogger<QueueServices> logger)
        {
            _settings = options.Value;
            _logger   = logger;
        }

        /// <summary>
        /// Checks that a queue exists, and creates it if it doesn't
        /// </summary>
        /// <param name="queueName"></param>
        /// <returns></returns>
        public bool EnsureQueueExists(string queueName)
        {
            return GetOrCreateQueue(queueName) != null;
        }

        /// <summary>
        /// Pushes a request onto a named queue. The request will be handled by a backend process.
        /// </summary>
        /// <param name="queueName">The name of the queue.</param>
        /// <param name="request">The Request to be processed.</param>
        /// <param name="token">The cancellation token</param>
        /// <returns>The response.</returns>
        /// <remarks>We have to be super careful here. We're passing in a BackendRequestBase object
        /// which may be another type, such as BackendRequest. On the other end of this queue we'll
        /// pop this object and pass it to the QueueController, and it will be the original object.
        /// However, if we choose to use a queue mechanism that doesn't maintain the object (eg
        /// converts to JSON) then we may not be able to simply cast this object. It is probably
        /// best to always pass a BackendRequest rather than BackendRequestBase, and accept that
        /// unless we're going to be fancy at the other end, the only things we can rely on are
        /// what's in the BackendRequest object.</remarks>
        public async ValueTask<object> SendRequestAsync(string queueName,
                                                        BackendRequestBase request,
                                                        CancellationToken token = default)
        {
            Channel<BackendRequestBase> queue = GetOrCreateQueue(queueName);

            // the backend process will return a JSON string as a response.
            var completion = new TaskCompletionSource<string?>();

            // when the request is dequeued will have to check that the pending response exists and
            // that the task is not completed.
            if (!_pendingResponses.TryAdd(request.reqid, completion))
            {
                string msg = $"Unable to add pending response id {request.reqid} to queue '{queueName}'.";
                return new ServerErrorResponse(msg);
            }

            // setup a request timeout.
            using var cancellationSource = new CancellationTokenSource(_settings.ResponseTimeout);
            var timeoutToken = cancellationSource.Token;

            try
            {
                // setup the timeout callback.
                using var linkedCTS        = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken);
                CancellationToken theToken = linkedCTS.Token;
                theToken.Register(() => { completion.TrySetCanceled(); });

                try
                {
                    // Add the request onto the queue (by writing to it). The request will be pulled
                    // from the queue by the backend module's request for a command. The backend
                    // module will send the command result back and this will be used to set the
                    // TaskCompletionResult result.
                    await queue.Writer.WriteAsync(request, theToken).ConfigureAwait(false);
                    if (!_doNotLogCommands.Contains(request.reqtype))
                        _logger.LogTrace($"Client request '{request.reqtype}' in queue '{queueName}' (#reqid {request.reqid})");
                }
                catch (OperationCanceledException)
                {
                    if (timeoutToken.IsCancellationRequested)
                        return new ServerErrorResponse($"Request queue '{queueName}' is full (#reqid {request.reqid})");

                    return new ServerErrorResponse($"The request in '{queueName}' was canceled by caller (#reqid {request.reqid})");
                }

                // Await the result of the TaskCompletion for the command that was put on the queue
                var jsonString = await completion.Task.ConfigureAwait(false);

                if (jsonString is null)
                    return new ServerErrorResponse($"null json returned from backend (#reqid {request.reqid})");

                return jsonString;
            }
            catch (OperationCanceledException)
            {
                if (timeoutToken.IsCancellationRequested)
                    return new ServerErrorResponse($"The request timed out (#reqid {request.reqid})");

                return new ServerErrorResponse($"The request was canceled by caller (#reqid {request.reqid})");
            }
            catch (JsonException)
            {
                return new ServerErrorResponse($"Invalid JSON response from backend (#reqid {request.reqid})");
            }
            catch (Exception ex)
            {
                return new ServerErrorResponse(ex.Message + $" (#reqid {request.reqid})");
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
            // Gets the TaskCompletionSource associated with the command and sets the
            // TaskCompletionSource's result to the response returned from the analysis module,
            // thus completing the client's request.
            if (!_pendingResponses.TryGetValue(req_id, out TaskCompletionSource<string?>? completion))
                return false;

            try
            {
                completion.SetResult(responseString);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error setting completion result: " + e.Message);
            }

            var response = JsonSerializer.Deserialize<JsonObject>(responseString ?? "");
            string command = response?["command"]?.ToString() ?? "(unknown)";
            if (!_doNotLogCommands.Contains(command))
            {
                if (response?["message"] is not null)
                    _logger.LogTrace($"Response received (#reqid {req_id} for command {command}): {response["message"]}");
                else
                    _logger.LogTrace($"Response received (#reqid {req_id} for command {command})");
            }

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
        /// <param name="token">The cancellation token</param>
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
                using var cancellationSource            = new CancellationTokenSource(_settings.CommandDequeueTimeout);
                var timeoutToken                        = cancellationSource.Token;
                using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutToken);
                var theToken                            = linkedCancellationTokenSource.Token;

                // NOTE FOR VS CODE users: In debug, you may want to uncheck "All Exceptions" under the
                // breakpoints section (the bottom section) of the Run and Debug tab.

                if (token.IsCancellationRequested || timeoutToken.IsCancellationRequested)
                    return null;

                try
                {
                    request = await queue.Reader.ReadAsync(theToken).ConfigureAwait(false);
                    if (request != null && !_doNotLogCommands.Contains(request.reqtype))
                        _logger.LogTrace($"Request '{request.reqtype}' dequeued from '{queueName}' (#reqid {request.reqid})");
                }
                catch (OperationCanceledException)
                {
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error DequeueRequestAsync: " + e.Message);
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
