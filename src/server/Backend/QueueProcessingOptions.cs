using System;

namespace CodeProject.AI.Server.Backend
{
    /// <summary>
    /// Options for Queue Processing
    /// </summary>
    public class QueueProcessingOptions
    {
        /// <summary>
        /// Get or set the max time to get a response.
        /// </summary>
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Get or set the max time for a command to process.
        /// </summary>
        public TimeSpan CommandDequeueTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Get or set the max number of requests that can be queued.
        /// </summary>
        public int MaxQueueLength { get; set; } = 32;

        /* Currently unused, but we'll keep the code for the future just in case
        public string ImageTempDir { get; set; } = "CodeProject.AI.TempImages";
        */
    }
}
