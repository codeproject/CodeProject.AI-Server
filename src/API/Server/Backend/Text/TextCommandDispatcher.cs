// NO LONGER USED.

using Microsoft.Extensions.Options;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CodeProject.SenseAI.API.Server.Backend
{
    /// <summary>
    /// Creates and Dispatches commands appropriately.
    /// </summary>
    public class TextCommandDispatcher
    {
        private const string SummaryQueueName = "summary_queue";

        private readonly QueueServices  _queueServices;
        private readonly BackendOptions _settings;

        /// <summary>
        /// Initializes a new instance of the CommandDispatcher class.
        /// </summary>
        /// <param name="queueServices">The Queue Services.</param>
        /// <param name="options">The Backend Options.</param>
        public TextCommandDispatcher(QueueServices queueServices, IOptions<BackendOptions> options)
        {
            _queueServices = queueServices;
            _settings      = options.Value;
        }

        /// <summary>
        /// Executes a Summarize Text Command.
        /// </summary>
        /// <param name="text">The text to sumamrise.</param>
        /// <param name="numberOfSentences">The number of sentences to produce.</param>
        /// <param name="token">A Cancellation Token (optional).</param>
        /// <returns>A list of the detected objects or an error response.</returns>
        public async Task<Object> SummarizeText(string? text, int numberOfSentences, 
                                                             CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new BackendErrorResponse(-1, "No text was provided");

            if (numberOfSentences <= 0)
                return new BackendErrorResponse(-1, "Number of sentences to produce is invalid");

            try
            {
                var response = await _queueServices.SendRequestAsync(SummaryQueueName,
                                                    new BackendTextSummaryRequest(text, numberOfSentences),
                                                    token).ConfigureAwait(false);
                return response;
            }
            catch
            {
                return new BackendErrorResponse(-1, "Unable to summarize the text");
            }
        }
    }
}
