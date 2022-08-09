using System.Drawing;
using System.Net.Http.Json;
using CodeProject.AI.AnalysisLayer.SDK;

using Microsoft.ML.OnnxRuntime;

using SkiaSharp;
using SkiaSharp.Views.Desktop;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace CodeProject.AI.AnalysisLayer.PortraitFilter
{
    class PortraitResponse : BackendSuccessResponse
    {
        public byte[]? filtered_image { get; set; }
    }

    /// <summary>
    /// TODO: Derive this from CommandQueueWorker
    /// </summary>
    public class PortraitFilterWorker : BackgroundService
    {
        private const string _modelPath = "Lib\\deeplabv3_mnv2_pascal_train_aug.onnx";
        private string _queueName       = "portraitfilter_queue";
        private string _moduleId        = "portrait-mode";

        private int _parallelism        = 1; // 4 also seems to be good on my machine.

        private readonly ILogger<PortraitFilterWorker> _logger;
        private readonly BackendClient _codeprojectAI;
        private DeepPersonLab? _deepPersonLab;

        /// <summary>
        /// Gets or sets the name of the hardware acceleration execution provider
        /// </summary>
        public string? ExecutionProvider { get; set; } = "CPU";

        /// <summary>
        /// Gets or sets the hardware accelerator ID that's in use
        /// </summary>
        public string? HardwareId { get; set; } = "CPU";

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="configuration">The app configuration values.</param>
        public PortraitFilterWorker(ILogger<PortraitFilterWorker> logger,
                                    IConfiguration configuration)
        {
            _logger = logger;

            int port = configuration.GetValue<int>("CPAI_PORT");
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
            _queueName = configuration.GetValue<string>("CPAI_MODULE_QUEUE");
            if (_queueName == default)
                _queueName = "portraitfilter_queue";

            _moduleId = configuration.GetValue<string>("CPAI_MODULE_ID");
            if (_moduleId == default)
                _moduleId = "PortraitFilter";

            _codeprojectAI = new BackendClient($"http://localhost:{port}/"
#if DEBUG
                , TimeSpan.FromMinutes(1)
#endif
            );

            var sessionOptions = GetHardwareInfo();

            try // if the support is not available for the Execution Provider DeepPersonLab will throw
            {
                _deepPersonLab = new DeepPersonLab(_modelPath.Replace('\\', Path.DirectorySeparatorChar), sessionOptions);
            }
            catch
            {
                // use the defaults
                _deepPersonLab = new DeepPersonLab(_modelPath.Replace('\\', Path.DirectorySeparatorChar));
                ExecutionProvider = "CPU";
                HardwareId        = "CPU";
            }
        }

        private SessionOptions GetHardwareInfo()
        {
            var sessionOpts = new SessionOptions();

            bool useGPU = (Environment.GetEnvironmentVariable("CUDA_MODE") ?? "False").ToLower() == "true";

            if (useGPU)
            {
                ///* -- work in progress
                var onnxRuntimeEnv = OrtEnv.Instance();
                var providers = onnxRuntimeEnv.GetAvailableProviders();

                // Enable CUDA  -------------------
                if (providers?.Any(p => p.StartsWith("CUDA", StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_CUDA();

                        ExecutionProvider = "CUDA";
                        HardwareId        = "GPU";
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }

                // Enable OpenVINO -------------------
                if (providers?.Any(p => p.StartsWith("OpenVINO", StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_OpenVINO("AUTO:GPU,CPU");
                        //sessionOpts.EnableMemoryPattern = false;
                        //sessionOpts.ExecutionMode = ExecutionMode.ORT_PARALLEL;

                        ExecutionProvider = "OpenVINO";
                        HardwareId        = "GPU";
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }

                // Enable DirectML -------------------
                if (providers?.Any(p => p.StartsWith("DML", StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_DML();
                        sessionOpts.EnableMemoryPattern = false;

                        ExecutionProvider = "DirectML";
                        HardwareId        = "GPU";
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }
            }

            // ------------------------------------------------
            //*/

            sessionOpts.AppendExecutionProvider_CPU();
            return sessionOpts;
        }

        /// <summary>
        /// Start the process.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Delay(1_000, token).ConfigureAwait(false);

            _logger.LogTrace("Background Portrait Filter Task Started.");
            await _codeprojectAI.LogToServer("CodeProject.AI Portrait Filter module started.",
                                             "PortraitFilterWorker", LogLevel.Information,
                                             string.Empty, token);

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
                if (_deepPersonLab == null)
                    continue;

                BackendResponseBase response;
                BackendRequest? request = null;
                try
                {
                    request = await _codeprojectAI.GetRequest(_queueName, _moduleId, token,
                                                              ExecutionProvider);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Portrait Filter Exception");
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
                    await _codeprojectAI.LogToServer("Portrait Filter File or file data is null.",
                                                     "PortraitFilterWorker", LogLevel.Error,
                                                     string.Empty, token);
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
                        await _codeprojectAI.LogToServer($"Portrait Filter Error for {file.filename}.",
                                                         "PortraitFilterWorker", LogLevel.Error,
                                                         string.Empty, token);
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

                await _codeprojectAI.SendResponse(request.reqid, _moduleId, content, token,
                                                  executionProvider: ExecutionProvider);
            }
        }

        /// <summary>
        /// Stop the process. Does nothing.
        /// </summary>
        /// <param name="token">The stopping cancellation token.</param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken token)
        {
            _logger.LogTrace("Background Portrait Filter Task is stopping.");

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