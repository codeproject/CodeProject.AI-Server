using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

using SkiaSharp.Views.Desktop;
using CodeProject.AI.SDK;
using CodeProject.AI.SDK.Utils;

namespace CodeProject.AI.Modules.PortraitFilter
{
    class PortraitResponse : BackendSuccessResponse
    {
        public byte[]? filtered_image { get; set; }
    }

    /// <summary>
    /// Implements the ModuleWorkerBase for Portrait mode inference
    /// </summary>
    public class PortraitFilterWorker : ModuleWorkerBase
    {
        private const string _modelPath = "Lib\\deeplabv3_mnv2_pascal_train_aug.onnx";

        private DeepPersonLab? _deepPersonLab;

        /// <summary>
        /// Initializes a new instance of the PortraitFilterWorker.
        /// </summary>
        /// <param name="logger">The Logger.</param>
        /// <param name="deepPersonLab">The deep Person Lab.</param>
        /// <param name="configuration">The app configuration values.</param>
        public PortraitFilterWorker(ILogger<PortraitFilterWorker> logger,
                                    IConfiguration configuration)
            : base(logger, configuration)
        {
            string modelPath = _modelPath.Replace('\\', Path.DirectorySeparatorChar);

            // if the support is not available for the Execution Provider DeepPersonLab will throw
            // So we try, then fall back
            try
            {
                var sessionOptions = GetSessionOptions();
                _deepPersonLab = new DeepPersonLab(modelPath, sessionOptions);
            }
            catch
            {
                // use the defaults
                _deepPersonLab = new DeepPersonLab(modelPath);
                ExecutionProvider = "CPU";
                HardwareType      = "CPU";
            }
        }

        /// <summary>
        /// The work happens here.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        protected override BackendResponseBase ProcessRequest(BackendRequest request)
        {
            if (_deepPersonLab == null)
                return new BackendErrorResponse($"{ModuleName} missing _deepPersonLab object.");

            // ignoring the file name
            var file        = request.payload?.files?.FirstOrDefault();
            var strengthStr = request.payload?.GetValue("strength", "0.5");

            if (!float.TryParse(strengthStr, out var strength))
                strength = 0.5f;

            if (file?.data is null)
                return new BackendErrorResponse("Portrait Filter File or file data is null.");

            Logger.LogInformation($"Processing {file.filename}");

            Stopwatch sw = Stopwatch.StartNew();

            // dummy result
            byte[]? result = null;

            try
            {
                var portraitModeFilter = new PortraitModeFilter(strength);

                byte[]? imageData = file.data;
                Bitmap? image     = ImageUtils.GetImage(imageData)?.ToBitmap();

                if (image is null)
                    return new BackendErrorResponse("Portrait Filter unable to get image from file data.");

                Stopwatch stopWatch = Stopwatch.StartNew();
                Bitmap mask = _deepPersonLab.Fit(image);
                stopWatch.Stop();

                if (mask is not null)
                {
                    Bitmap? filteredImage = portraitModeFilter.Apply(image, mask);
                    result = ImageToByteArray(filteredImage);
                }
            }
            catch (Exception ex)
            {
                return new BackendErrorResponse($"Portrait Filter Error for {file.filename}: {ex.Message}.");
            }

            if (result is null)
                return new BackendErrorResponse("Portrait Filter returned null.");
            
            return new PortraitResponse { 
                filtered_image = result,
                inferenceMs    = sw.ElapsedMilliseconds
            };
        }

        private SessionOptions GetSessionOptions()
        {
            var sessionOpts = new SessionOptions();

            string[]? providers = null;
            try
            {
                providers = OrtEnv.Instance().GetAvailableProviders();
            }
            catch
            {
            }

            // foreach (var providerName in providers ?? Array.Empty<string>())
            //    _logger.LogDebug($"PortraitFilter provider: {providerName}");

            // Note on CanUseGPU: if !SupportGPU then we aren't actually going to attempt to load
            // the provider, so we won't really know if we can truly use the GPU. 
            // If SupportGPU = true then we set CanUseGPU true for the first provider that works,
            // even if subsequent providers fail to load

            // Enable CUDA  -------------------
            if (providers?.Any(p => p.StartsWithIgnoreCase("CUDA")) ?? false)
            {
                if (SupportGPU)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_CUDA();

                        ExecutionProvider = "CUDA";
                        HardwareType      = "GPU";
                        CanUseGPU         = true;
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }
                else
                    CanUseGPU = true;
            }
            
            // Enable OpenVINO -------------------
            if (providers?.Any(p => p.StartsWithIgnoreCase("OpenVINO")) ?? false)
            {
                if (SupportGPU)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_OpenVINO("AUTO:GPU,CPU");
                        //sessionOpts.EnableMemoryPattern = false;
                        //sessionOpts.ExecutionMode = ExecutionMode.ORT_PARALLEL;

                        ExecutionProvider = "OpenVINO";
                        HardwareType      = "GPU";
                        CanUseGPU         = true;
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }
                else
                    CanUseGPU = true;
            }

            // Enable DirectML -------------------
            if (providers?.Any(p => p.StartsWithIgnoreCase("DML")) ?? false)
            {
                if (SupportGPU)
                {
                    try
                    {
                        sessionOpts.AppendExecutionProvider_DML();
                        sessionOpts.EnableMemoryPattern = false;

                        ExecutionProvider = "DirectML";
                        HardwareType      = "GPU";
                        CanUseGPU         = true;
                    }
                    catch
                    {
                        // do nothing, the provider didn't work so keep going
                    }
                }
                else
                    CanUseGPU = true;
            }

            sessionOpts.AppendExecutionProvider_CPU();
            return sessionOpts;
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