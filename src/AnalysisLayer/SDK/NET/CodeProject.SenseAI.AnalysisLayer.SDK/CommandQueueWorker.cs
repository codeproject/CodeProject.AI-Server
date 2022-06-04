using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Net.Http.Json;

namespace CodeProject.SenseAI.AnalysisLayer.SDK
{
    public abstract class CommandQueueWorker : BackgroundService
    {
        private readonly string?       _queueName;
        private readonly string?       _moduleId;

        private readonly int           _parallelism = 1;

        private readonly ILogger       _logger;
        private readonly SenseAIClient _senseAI;

        /// <summary>
        /// Gets or sets the name of this Module
        /// </summary>
        public string? ModuleName { get; set; }

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="configuration">The app configuration values.</param>
        /// <param name="defaultQueueName">The default Queue Name.</param>
        /// <param name="defaultModuleId">The default Module Id.</param>
        public CommandQueueWorker(ILogger logger, IConfiguration configuration,
                                  string moduleName,   
                                  string defaultQueueName, string defaultModuleId)
        {
            _logger    = logger;
            ModuleName = moduleName;
            
            int port = configuration.GetValue<int>("PORT");
            if (port == default)
                port = 5000;

            _queueName = configuration.GetValue<string>("MODULE_QUEUE") ?? defaultQueueName;
            if (string.IsNullOrEmpty(_queueName))
                throw new ArgumentException("QueueName not initialized");

            _moduleId  = configuration.GetValue<string>("MODULE_ID") ?? defaultModuleId;
            if (string.IsNullOrEmpty(_moduleId))
                throw new ArgumentException("ModuleId not initialized");

            _parallelism = configuration.GetValue<int>("MODULE_TASKS", 1);

            _senseAI = new SenseAIClient($"http://localhost:{port}/"
#if DEBUG
                , TimeSpan.FromMinutes(1)
#endif
            );
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);

            _logger.LogInformation($"{ModuleName} Task Started.");
            await _senseAI.LogToServer($"{ModuleName} module started.", token);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < _parallelism; i++)
                tasks.Add(ProcessQueue(token));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ProcessQueue(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // _logger.LogInformation("Checking Portrait Filter queue.");

                BackendRequest? request = null;
                try
                {
                    request = await _senseAI.GetRequest(_queueName!, _moduleId!, token);

                    if (request is null)
                        continue;

                    var response = ProcessRequest(request);

                    HttpContent content = JsonContent.Create(response, response.GetType());
                    await _senseAI.SendResponse(request.reqid, _moduleId!, content, token);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, $"{ModuleName} Exception");
                    continue;
                }
            }
        }

        /// <summary>
        /// Processes the request receive from the server queue.
        /// </summary>
        /// <param name="request">The Request data.</param>
        /// <returns>An object to serialize back to the server.</returns>
        public abstract BackendResponseBase ProcessRequest(BackendRequest request);

        /// <summary>
        /// Stop the process. Does nothing.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation($"{ModuleName} Task is stopping.");

            await base.StopAsync(token);
        }
    }
}
