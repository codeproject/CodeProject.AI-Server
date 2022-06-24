// No longer used

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using CodeProject.AI.AnalysisLayer.SDK;

namespace CodeProject.AI.API.Server.Backend
{
    /// <summary>
    /// Creates and Dispatches commands appropriately.
    /// </summary>
    public class CommandDispatcher
    {
        private readonly QueueServices _queueServices;
        private readonly QueueProcessingOptions _settings;

        /// <summary>
        /// Initializes a new instance of the CommandDispatcher class.
        /// </summary>
        /// <param name="queueServices">The Queue Services.</param>
        /// <param name="options">The Backend Options.</param>
        public CommandDispatcher(QueueServices queueServices, IOptions<QueueProcessingOptions> options)
        {
            _queueServices = queueServices;
            _settings      = options.Value;
        }

        /* Currently unused, but we'll keep the code for the future just in case
        /// <summary>
        /// Saves a Form File to the temp directory.
        /// </summary>
        /// <param name="image">The Form File.</param>
        /// <returns>The saved file name.</returns>
        public async Task<string?> SaveFileToTempAsync(IFormFile image)
        {
            string filename = $"{Guid.NewGuid():B}{Path.GetExtension(image.FileName)}";
            string tempDir  = Path.GetTempPath();
            string dirPath  = Path.Combine(tempDir, _settings.ImageTempDir);

            var directoryInfo = new DirectoryInfo(dirPath);
            if (!directoryInfo.Exists)
                directoryInfo.Create();

            var filePath = Path.Combine(tempDir, _settings.ImageTempDir, filename);
            var fileInfo = new FileInfo(filePath);

            try
            {
                using var imageStream = image.OpenReadStream();
                using var fileStream  = fileInfo.OpenWrite();
                await imageStream.CopyToAsync(fileStream).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }

            return filePath;
        }
        */

        public async Task<Object> QueueRequest(string queueName, string command,
                                               RequestPayload payload,
                                               CancellationToken token = default)
        {
            var response = await _queueServices.SendRequestAsync(queueName, 
                                                                 new BackendRequest (command, payload),
                                                                 token);
            return response;
        }
    }
}
