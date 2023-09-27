using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.SDK;

namespace CodeProject.AI.Server.Backend
{
    /// <summary>
    /// Creates and Dispatches commands appropriately.
    /// </summary>
    public class CommandDispatcher
    {
        private readonly QueueServices _queueServices;

        /// <summary>
        /// Initializes a new instance of the CommandDispatcher class.
        /// </summary>
        /// <param name="queueServices">The Queue Services.</param>
        public CommandDispatcher(QueueServices queueServices)
        {
            _queueServices = queueServices;
        }

        /// <summary>
        /// Queues a request
        /// </summary>
        /// <param name="queueName">The name of the queue</param>
        /// <param name="payload">The payload to place on the queue</param>
        /// <param name="token">The cancellation token</param>
        /// <returns></returns>
        public async Task<object> QueueRequest(string queueName, RequestPayload payload,
                                               CancellationToken token = default)
        {
            var response = await _queueServices.SendRequestAsync(queueName.ToLower(), 
                                                                 new BackendRequest(payload), token)
                                               .ConfigureAwait(false);
            return response;
        }
    }
}
