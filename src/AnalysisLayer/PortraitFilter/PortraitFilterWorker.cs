using CodeProject.SenseAI.AnalysisLayer.SDK;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using System.Drawing;
using System.Net.Http.Json;

namespace CodeProject.SenseAI.AnalysisLayer.PortraitFilter
{
    class PortraitResponse : BackendSuccessResponse
    {
        public byte[]? filtered_image { get; set; }
    }

    public class PortraitFilterWorker : BackgroundService
    {
        private const string _modelPath = "Lib\\deeplabv3_mnv2_pascal_train_aug.onnx";
        private string _queueName       = "portraitfilter_queue";
        private string _moduleId        = "portrait-mode";

        private int _parallelism        = 1; // 4 also seems to be good on my machine.

        private readonly ILogger<PortraitFilterWorker> _logger;
        private readonly SenseAIClient _senseAI;
        private DeepPersonLab _deepPersonLab;

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="configuration">The app configuration values.</param>
        public PortraitFilterWorker(ILogger<PortraitFilterWorker> logger,
                             IConfiguration configuration)
        {
            _logger = logger;

            int port = configuration.GetValue<int>("PORT");
            if (port == default)
                port = 5000;

            // TODO: It would be really nice to have the server tell the module the name of the
            // queue that they should be processing. The queue name is in the RouteMap, with a
            // different queue for each route. While this provides flexibility, it means we have to
            // hardcode the queue into the analysis module and ensure it is always the same as the
            // value in the modulesettings file. Maybe, for now, have the queue name be defined at
            // the module level in modulesettings, so it's shared among all routes. If a module
            // requires more than one queue then it's probably breaking the Single Resonsibility
            // principle.
            //
            // Because the Modules are not always started by the Server, for debugging and mesh, we
            // would need the Module to register their route info, including the queue, at startup.
            // The frontend can still start any modules it discovers, but the registration should
            // be done by the Module.
            //
            // Notes:
            //  ModuleId: This needs to be unique across Modules, but the same for all instances of
            //      same Module type. Because we want one Queue per Module type, this could effectively
            //      be used as the Queue selector.  ModuleIds could become a GUID.
            //
            // TODO: Move the Queue name up to the Module level.

            // Note that looking up MODULE_QUEUE will currently always return null. It's here as an
            // annoying reminder.
            _queueName = configuration.GetValue<string>("MODULE_QUEUE");
            if (_queueName == default)
                _queueName = "portraitfilter_queue";

            _moduleId = configuration.GetValue<string>("MODULE_ID");
            if (_moduleId == default)
                _moduleId = "PortraitFilter";

            _senseAI = new SenseAIClient($"http://localhost:{port}/"
#if DEBUG
                , TimeSpan.FromMinutes(1)
#endif
            );

             _deepPersonLab = new DeepPersonLab(_modelPath.Replace('\\', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);

            _logger.LogInformation("Background Portrait Filter Task Started.");
            await _senseAI.LogToServer("SenseAI Portrait Filter module started.", token);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < _parallelism; i++)
                tasks.Add(ProcessQueue(token));

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task ProcessQueue(CancellationToken token)
        {
            PortraitModeFilter portraitModeFilter = new PortraitModeFilter(0.0f);
            while (!token.IsCancellationRequested)
            {
                // _logger.LogInformation("Checking Portrait Filter queue.");

                BackendResponseBase response;
                BackendRequest? request = null;
                try
                {
                    request = await _senseAI.GetRequest(_queueName, _moduleId, token);
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, "Portrait Filter Exception");
                    continue;
                }

                if (request is null)
                    continue;

                // ignore the command as only one command

                // ignoring the file name
                var file        = request.payload?.files?.FirstOrDefault();
                var strengthStr = request.payload?.values?
                                                  .FirstOrDefault(x => x.Key == "strength")
                                                  .Value?[0] ?? "0.5";

                if (!float.TryParse(strengthStr, out var strength))
                    strength = 0.5f;

                if (file?.data is null)
                {
                    await _senseAI.LogToServer("Portrait Filter File or file data is null.", token);
                    response = new BackendErrorResponse(-1, "Portrait Filter Invalid File.");
                }
                else
                {
                    _logger.LogInformation($"Processing {file.filename}");
                    // Do the processing here

                    // dummy result
                    byte[]? result = null;

                    try
                    {
                        var imageData = file.data;
                        var image     = GetImage(imageData);

                        if (image is not null)
                        {
                            var mask = _deepPersonLab.Fit(image);
                            if (mask is not null)
                            {
                                portraitModeFilter.Strength = strength;
                                var filteredImage = portraitModeFilter.Apply(image, mask);
                                result = ImageToByteArray(filteredImage);
                            }

                        }

                        // yoloResult = _objectDetector.Predict(imageData);
                    }
                    catch (Exception ex)
                    {
                        await _senseAI.LogToServer($"Portrait Filter Error for {file.filename}.", token);
                        _logger.LogError(ex, "Portrait Filter Exception");
                        result = null;
                    }

                    if (result is null)
                    {
                        response = new BackendErrorResponse(-1, "Portrait Filter returned null.");
                    }
                    else
                    {
                        response = new PortraitResponse
                        {
                            filtered_image = result
                        };
                    }
                }

                HttpContent? content = null;
                if (response is PortraitResponse portraitResponse)
                    content = JsonContent.Create(portraitResponse);
                else
                    content = JsonContent.Create(response as BackendErrorResponse);

                await _senseAI.SendResponse(request.reqid, _moduleId, content, token);
            }
        }

        /// <summary>
        /// Stop the process. Does nothing.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation("Background Portrait Filter Task is stopping.");

            await base.StopAsync(token);
        }

        // Using SkiaSharp as it handles more formats.
        private static Bitmap? GetImage(byte[] imageData)
        {
            if (imageData == null)
                return null;

            var skiaImage = SKImage.FromEncodedData(imageData);
            if (skiaImage is null)
                return null;

            return skiaImage.ToBitmap();
        }

        public static byte[]? ImageToByteArray(Image img)
        {
            if (img is null)
                return null;

            using var stream = new MemoryStream();

            // We'll disabled the warnings around the cross platform issues with System.Drawing.
            // We have enabled System.Drawing.EnableUnixSupport in the runtimeconfig.template.json
            // file, but understand that in .NET7 that option won't be available. We will port to
            // a different libary in the future. For more info see
            // https://github.com/dotnet/designs/blob/main/accepted/2021/system-drawing-win-only/system-drawing-win-only.md
            #pragma warning disable CA1416 // Validate platform compatibility
            img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            #pragma warning restore CA1416 // Validate platform compatibility

            return stream.ToArray();
        }
    }
}