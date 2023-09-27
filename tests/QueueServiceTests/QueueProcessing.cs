using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.SDK;
using CodeProject.AI.Server.Backend;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace QueueServiceTests
{
    public class QueueProcessing
    {
        public class TestQueuedRequest : BackendRequestBase
        {
            public string? image_name { get; set; }
        }

        public class TestQueuedResponse : BackendResponseBase
        {
            public string? label { get; set; }
        }

        private const string QueueName = "testQueue";
        private class TestOptions : IOptions<QueueProcessingOptions>
        {
            public TestOptions(QueueProcessingOptions options)
            {
                Value = options;
            }

            public QueueProcessingOptions Value { get; }
        }

        private static readonly QueueProcessingOptions queueOptions = new()
            {
                MaxQueueLength  = 10,
                ResponseTimeout = TimeSpan.FromSeconds(10)
            };
        private QueueServices _queueServices = new QueueServices(new TestOptions(queueOptions),
                                                                 new NullLogger<QueueServices>());

        [Fact]
        public async Task RequestTimesOutIfNotHandled()
        {
            var request = new TestQueuedRequest { image_name = "Bob.jpg" };
            var result  = await _queueServices.SendRequestAsync(QueueName, request)
                                              .ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.IsType<BackendErrorResponse>(result);

            var errorResult = result as BackendErrorResponse;
            Assert.NotNull(errorResult);
            Assert.False(errorResult!.success);
            Assert.StartsWith("The request timed out.", errorResult.error);
        }

        [Fact]
        public async Task CanPullRequestFromQueue()
        {
            var request                = new TestQueuedRequest { image_name = "Bob.jpg" };
            var requestTask            = _queueServices.SendRequestAsync(QueueName, request);
            BackendRequestBase? result = await _queueServices.DequeueRequestAsync(QueueName)
                                                             .ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.IsType<TestQueuedRequest>(result);
            Assert.Same(request, result);
        }

        [Fact]
        public async Task CanPullRequestFromQueuAsynce()
        {
            var request                = new TestQueuedRequest { image_name = "Bob.jpg" };
            var requestTask            = _queueServices.SendRequestAsync(QueueName, request);
            BackendRequestBase? result = await _queueServices.DequeueRequestAsync(QueueName)
                                                             .ConfigureAwait(false);

            Assert.NotNull(result);
            Assert.IsType<TestQueuedRequest>(result);
            Assert.Same(request, result);
        }


        [Fact]
        public void CantPullRequestFronWrongQueue()
        {
            var request                = new TestQueuedRequest { image_name = "Bob.jpg" };
            var requestTask            = _queueServices.SendRequestAsync(QueueName, request);
            BackendRequestBase? result = _queueServices.DequeueRequest(QueueName + "_Wrong");

            Assert.Null(result);
        }

        [Fact]
        public async Task CantPullRequestFronWrongQueueAsync()
        {
            var request                = new TestQueuedRequest { image_name = "Bob.jpg" };
            var requestTask            = _queueServices.SendRequestAsync(QueueName, request);
            BackendRequestBase? result = await _queueServices.DequeueRequestAsync(QueueName + "_Wrong").ConfigureAwait(false);

            Assert.Null(result);
        }


        [Fact]
        public async Task CanCancelPullRequestQueueAsync()
        {
            // make sure the queue exists
            var request                = new TestQueuedRequest { image_name = "Bob.jpg" };
            var requestTask            = _queueServices.SendRequestAsync(QueueName, request);
            BackendRequestBase? result = await _queueServices.DequeueRequestAsync(QueueName)
                                                             .ConfigureAwait(false);

            using CancellationTokenSource cancellationSource = new();

            var token = cancellationSource.Token;

            var task  = _queueServices.DequeueRequestAsync(QueueName, token);
            cancellationSource.Cancel();

            result = await task.ConfigureAwait(false);

            Assert.Null(result);
        }

        [Fact]
        public async Task CanGetResponse()
        {
            var request            = new TestQueuedRequest { image_name = "Bob.jpg" };
            var testResponse       = new TestQueuedResponse() { success = true, label = "Bob" };
            var testResponseString = JsonSerializer.Serialize(testResponse);
            var requestTask        = _queueServices.SendRequestAsync(QueueName, request);
            var pulledRequest      = _queueServices.DequeueRequest(QueueName);
            Assert.NotNull(pulledRequest);

            bool success          = _queueServices.SetResult(pulledRequest!.reqid, testResponseString);
            Assert.True(success);

            var result            = await requestTask.ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.IsType<TestQueuedResponse>(result);
        }

        [Fact]
        public async Task CantAddSameRequestTwice()
        {
            var request           = new TestQueuedRequest { image_name = "Bob.jpg" };
            var firstrequestTask  = _queueServices.SendRequestAsync(QueueName, request);
            var secondRequestTask = _queueServices.SendRequestAsync(QueueName, request);
            var secondResult      = await secondRequestTask.ConfigureAwait(false);
            Assert.NotNull(secondResult);
            Assert.IsType<BackendErrorResponse>(secondResult);

            var errorResult = secondResult as BackendErrorResponse;
            Assert.NotNull(errorResult);
            Assert.False(errorResult!.success);
            Assert.StartsWith("Unable to add pending response id", errorResult.error);
        }

        [Fact]
        public async Task NullResponseReturnsError()
        {
            var request                = new TestQueuedRequest { image_name = "Bob.jpg" };
            string? testResponseString = null;
            var requestTask            = _queueServices.SendRequestAsync(QueueName, request);
            var pulledRequest          = _queueServices.DequeueRequest(QueueName);
            Assert.NotNull(pulledRequest);

            bool success               = _queueServices.SetResult(request.reqid, testResponseString);
            Assert.True(success);

            var result                 = await requestTask.ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.IsType<BackendErrorResponse>(result);
            var errorResult           = result as BackendErrorResponse;
            Assert.NotNull(errorResult);
            Assert.False(errorResult!.success);
            Assert.Equal("null json returned from backend.", errorResult.error);
        }

        [Fact]
        public async Task BadResponseReturnsError()
        {
            var request               = new TestQueuedRequest { image_name = "Bob.jpg" };
            string testResponseString = "This is not JSON";
            var requestTask           = _queueServices.SendRequestAsync(QueueName, request);
            var pulledRequest         = _queueServices.DequeueRequest(QueueName);
            Assert.NotNull(pulledRequest);

            bool success              = _queueServices.SetResult(request.reqid, testResponseString);
            Assert.True(success);

            var result                = await requestTask.ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.IsType<BackendErrorResponse>(result);

            var errorResult          = result as BackendErrorResponse;
            Assert.NotNull(errorResult);
            Assert.False(errorResult!.success);
            Assert.Equal("Invalid JSON response from backend.", errorResult.error);
        }

        [Fact]
        public async Task EmptyResponseReturnsError()
        {
            var request               = new TestQueuedRequest { image_name = "Bob.jpg" };
            string testResponseString = "null";
            var requestTask           = _queueServices.SendRequestAsync(QueueName, request);
            var pulledRequest         = _queueServices.DequeueRequest(QueueName);
            Assert.NotNull(pulledRequest);

            bool success              = _queueServices.SetResult(request.reqid, testResponseString);
            Assert.True(success);

            var result                = await requestTask.ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.IsType<BackendErrorResponse>(result);

            var errorResult           = result as BackendErrorResponse;
            Assert.NotNull(errorResult);
            Assert.False(errorResult!.success);
            Assert.Equal("null object from JSON string.", errorResult.error);
        }

        [Fact]
        public async Task TimeoutRequestDoesntClogQueue()
        {
            var request1     = new TestQueuedRequest { image_name = "Bob.jpg" };
            var request2     = new TestQueuedRequest { image_name = "Alf.jpg" };
            var request1Task = _queueServices.SendRequestAsync(QueueName, request1);

            await Task.Delay(queueOptions.ResponseTimeout + TimeSpan.FromSeconds(5))
                      .ConfigureAwait(false);
            var request2Task = _queueServices.SendRequestAsync(QueueName, request2);

            BackendRequestBase? result = await _queueServices.DequeueRequestAsync(QueueName)
                                                             .ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.IsType<TestQueuedRequest>(result);
            Assert.Equal(request2, result);
        }

        [Fact]
        public async Task RequestQueueHasLimit()
        {
            var tasks = new List<Task<Object>>();
            for (int i = 0; i < queueOptions.MaxQueueLength; i++)
            {
                var request1     = new TestQueuedRequest { image_name = "Bob.jpg" };
                var request1Task = _queueServices.SendRequestAsync(QueueName, request1);
                tasks.Add(request1Task.AsTask());
            }
            var request2     = new TestQueuedRequest { image_name = "Alf.jpg" };
            var request2Task = _queueServices.SendRequestAsync(QueueName, request2);
            tasks.Add(request2Task.AsTask());

            await Task.WhenAll(tasks).ConfigureAwait(false);

            var lastTask = tasks[queueOptions.MaxQueueLength];
            Assert.True(lastTask.IsCompletedSuccessfully);

            var lastResult = lastTask.Result;
            Assert.IsType<BackendErrorResponse>(lastResult);

            var errorResponse = lastResult as BackendErrorResponse;
            Assert.NotNull(errorResponse);
            Assert.Equal("request queue is full.", errorResponse!.error);

            for (int i = 0; i < queueOptions.MaxQueueLength; i++)
            {
                var task = tasks[i];
                Assert.True(task.IsCompletedSuccessfully);

                var result = task.Result;
                Assert.IsType<BackendErrorResponse>(result);

                var errorResponse2 = result as BackendErrorResponse;
                Assert.NotNull(errorResponse2);
                Assert.Equal("The request timed out.", errorResponse2!.error);
            }
        }

        [Fact]
        public async Task RequestCanBeCanceled()
        {
            var cts         = new CancellationTokenSource();
            
            var request     = new TestQueuedRequest { image_name = "Bob.jpg" };
            var requestTask = _queueServices.SendRequestAsync(QueueName, request, cts.Token);
            cts.Cancel();

            var result = await requestTask.ConfigureAwait(false);
            Assert.NotNull(result);
            Assert.IsType<BackendErrorResponse>(result);

            var errorResponse = result as BackendErrorResponse;
            Assert.NotNull(errorResponse);
            Assert.Equal("the request was canceled by caller.", errorResponse!.error);
        }
    }
}