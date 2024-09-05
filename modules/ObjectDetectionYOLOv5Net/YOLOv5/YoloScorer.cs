using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

using CodeProject.AI.SDK.Utils;

using SkiaSharp;
using Yolov5Net.Scorer.Models.Abstract;

namespace Yolov5Net.Scorer
{
    /// <summary>
    /// Yolov5 scorer.
    /// </summary>
    public class YoloScorer<T> : IDisposable where T : YoloModel
    {
        // for pixel ectract
        static readonly uint           _blueBits       = (uint)0xFF << SkiaSharp.SKImageInfo.PlatformColorBlueShift;
        static readonly uint           _greenBits      = (uint)0xFF << SkiaSharp.SKImageInfo.PlatformColorGreenShift;
        static readonly uint           _redBits        = (uint)0xFF << SkiaSharp.SKImageInfo.PlatformColorRedShift;
        static readonly Vector<uint>   _blueMask       = new Vector<uint>(_blueBits);
        static readonly Vector<uint>   _greenMask      = new Vector<uint>(_greenBits);
        static readonly Vector<uint>   _redMask        = new Vector<uint>(_redBits);
        static readonly Vector<Single> _scalingDivisor = new Vector<Single>(255.0F);

        private readonly T _model;

        private ObjectPool<DenseTensor<float>> _tensorPool;
        private ObjectPool<ConcurrentBag<YoloPrediction>> _predictionListPool;
        private bool disposedValue;

        // To scale up we will need to create multiple InferenceSessions per model
        // as the InferenceSession instance is not thread safe.
        private readonly InferenceSession? _inferenceSession;

        /// <summary>
        /// Gets or sets the model FilePath.
        /// </summary>
        public string FilePath { get; } = "N/A";

        /// <summary>
        /// Outputs value between 0 and 1.
        /// </summary>
        private float Sigmoid(float value)
        {
            return 1 / (1 + (float)Math.Exp(-value));
        }

        /// <summary>
        /// Converts xywh bbox format to xyxy.
        /// </summary>
        private float[] Xywh2xyxy(float[] source)
        {
            var result = new float[4];

            result[0] = source[0] - source[2] / 2f;
            result[1] = source[1] - source[3] / 2f;
            result[2] = source[0] + source[2] / 2f;
            result[3] = source[1] + source[3] / 2f;

            return result;
        }

        /// <summary>
        /// Returns value clamped to the inclusive range of min and max.
        /// </summary>
        public float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        /// <summary>
        /// Resizes image keeping ratio to fit model input size. Make sure to dispose of the returned
        /// image!
        /// </summary>
        public SKImage ResizeImage(SKImage image, SKFilterQuality quality = SKFilterQuality.None)
        {
            if (_model.Width <= 0 || _model.Height <= 0)
                return image;

            var (w, h)           = (image.Width, image.Height); // image width and height
            var (xRatio, yRatio) = (_model.Width / (float)w, _model.Height / (float)h); // x, y ratios
            var ratio            = Math.Min(xRatio, yRatio);   // ratio = resized / original
            var (width, height)  = ((int)(w * ratio), (int)(h * ratio)); // roi width and height
            var (x, y)           = ((_model.Width / 2) - (width / 2), (_model.Height / 2) - (height / 2)); // roi x and y coordinates

            // SKImage version
            var destRect  = new SKRectI(x, y, x + width, y + height); // region of interest
            var imageInfo = new SKImageInfo(_model.Width, _model.Height, image.ColorType, image.AlphaType);

            try
            {
                using var surface = SKSurface.Create(imageInfo);

                using var paint     = new SKPaint();
                paint.IsAntialias   = true;
                paint.FilterQuality = quality;

                surface.Canvas.DrawImage(image, destRect, paint);
                surface.Canvas.Flush();

                return surface.Snapshot();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return image;
            }
        }

        /// <summary>
        /// Extracts pixels into tensor for net input.
        /// </summary>
        public DenseTensor<float> ExtractPixels(SKImage image)
        {
            var tensor = _tensorPool.Get();

            var bitmap = SKBitmap.FromImage(image);
            try
            {
                if (bitmap is null) return tensor;

                int bytesPerPixel = bitmap.BytesPerPixel;
                int stride = bytesPerPixel * image.Width;
                {
                    Parallel.For(0, image.Height, (y) =>
                    {
                        ReadOnlySpan<byte> pixels = bitmap.GetPixelSpan().Slice(y * stride, stride);

                        var vectorCount = Vector<uint>.Count;
                        // This will result in doing it the 'slow' way if no SIMD support.
                        var remainder   = Vector.IsHardwareAccelerated ? image.Width % vectorCount : image.Width;

                        var redBuffer   = tensor.Buffer.Span[(2 * image.Height * image.Width + (y * image.Width))..];
                        var greenBuffer = tensor.Buffer.Span[(1 * image.Height * image.Width + (y * image.Width))..];
                        var blueBuffer  = tensor.Buffer.Span[(0 * image.Height * image.Width + (y * image.Width))..];

                        for (int x = 0; x < image.Width - remainder; x += vectorCount)
                        {
                            // Red
                            var v1 = (new Vector<UInt32>(pixels[(x * bytesPerPixel)..]) & _redMask);
                            v1 = Vector.ShiftRightLogical(v1, SkiaSharp.SKImageInfo.PlatformColorRedShift);
                            var v2 = Vector.ConvertToSingle(v1);
                            v2 = v2 / _scalingDivisor;
                            v2.CopyTo(redBuffer[x..]);

                            // Green
                            v1 = (new Vector<UInt32>(pixels[(x * bytesPerPixel)..]) & _greenMask);
                            v1 = Vector.ShiftRightLogical(v1, SkiaSharp.SKImageInfo.PlatformColorGreenShift);
                            v2 = Vector.ConvertToSingle(v1);
                            v2 = v2 / _scalingDivisor;
                            v2.CopyTo(greenBuffer[x..]);

                            // Blue
                            v1 = (new Vector<UInt32>(pixels[(x * bytesPerPixel)..]) & _blueMask);
                            v1 = Vector.ShiftRightLogical(v1, SkiaSharp.SKImageInfo.PlatformColorBlueShift);
                            v2 = Vector.ConvertToSingle(v1);
                            v2 = v2 / _scalingDivisor;
                            v2.CopyTo(blueBuffer[x..]);
                        }

                        // the slow way for the remainder.
                        for (var x = image.Width - remainder; x < image.Width; x++)
                        {
                            var redPixel   = (pixels[x] & _redBits >> SkiaSharp.SKImageInfo.PlatformColorRedShift) / 255.0f;
                            redBuffer[x]   = redPixel;
                            var greenPixel = (pixels[x] & _greenBits >> SkiaSharp.SKImageInfo.PlatformColorGreenShift) / 255.0f;
                            greenBuffer[x] = greenPixel;
                            var bluePixel  = (pixels[x] & _blueBits >> SkiaSharp.SKImageInfo.PlatformColorBlueShift) / 255.0f;
                            blueBuffer[x]  = bluePixel;
                        }
                    });
                }

                return tensor;
            }
            finally
            {
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// Runs inference session.
        /// </summary>
        /// <param name="image">The input image</param>
        /// <returns>A dense tensor containing the image pixels</returns>
        private IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? Inference(SKImage image)
        {
            if (_inferenceSession is null || image is null)
                return null;

            // ExtractPixels uses a Tensor from an Object Pool
            var inputTensor = ExtractPixels(image);
            var inputs = new List<NamedOnnxValue> // add image as onnx input
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue>? result = null;
            try
            {
                // A minor potential perf improvement: We lock on the InferenceSession as it is the
                // InferenceSession that is not thread safe, the GPU and ONNX library is fine.
                // Multiple InferenceSessions can run on the GPU in different threads, but each
                // InferenceSession can not run multiple times concurrently as the
                // InferenceSesssion instances are not thread safe. This means the detection on
                // different models CAN run concurrently.
                // Also locking on the session means we can scale up by creating multiple
                // InferenceSessions for each model.
                lock (_inferenceSession)
                {
                    result = _inferenceSession.Run(inputs); // run inference
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Inference Ex:{ex.Message}");
            }
            finally
            {
                // make sure we return the Tensor to the Object Pool.
                _tensorPool.Release(inputTensor);
            }

            return result;
        }

        /// <summary>
        /// Parses net output (detect) to predictions.
        /// </summary>
        /// <param name="output">The first output from an inference call</param>
        /// <param name="image">The original input image</param>
        /// <returns>A list of Yolo predictions</returns>
        private List<YoloPrediction> ParseDetect(DenseTensor<float> output, SKImage image, float minConfidence)
        {
            var result = _predictionListPool.Get();

            try
            {
                var (w, h) = (image.Width, image.Height); // image w and h
                var (xGain, yGain) = (_model.Width / (float)w, _model.Height / (float)h); // x, y gains
                var gain = Math.Min(xGain, yGain); // gain = resized / original

                var (xPad, yPad) = ((_model.Width - w * gain) / 2, (_model.Height - h * gain) / 2); // left, right pads
                int numBoxes = output.Dimensions[1];
                int boxInfoLength = output.Dimensions[2];

                Parallel.For(0, numBoxes, (i) =>
                {
                    var buffer = output.Buffer.Slice(i * boxInfoLength, boxInfoLength).Span;
                    float boxConfidence = buffer[4];

                    if (boxConfidence <= minConfidence) return; // skip low obj_conf results
                    var (cx, cy) = (buffer[0], buffer[1]);
                    var (offsetX, offsetY) = (buffer[2] / 2f, buffer[3] / 2f);

                    float xMin = (cx - offsetX - xPad) / gain; // unpad bbox tlx to original
                    float yMin = (cy - offsetY - yPad) / gain; // unpad bbox tly to original
                    float xMax = (cx + offsetX - xPad) / gain; // unpad bbox brx to original
                    float yMax = (cy + offsetY - yPad) / gain; // unpad bbox bry to original

                    xMin = Math.Clamp(xMin, 0, w - 0); // clip bbox tlx to boundaries
                    yMin = Math.Clamp(yMin, 0, h - 0); // clip bbox tly to boundaries
                    xMax = Math.Clamp(xMax, 0, w - 1); // clip bbox brx to boundaries
                    yMax = Math.Clamp(yMax, 0, h - 1); // clip bbox bry to boundaries

                    var requiredConfidence = minConfidence / boxConfidence;

                    int   maxIndex      = -1;
                    float maxConfidence = 0f;
                    for (int k = 5; k < boxInfoLength; k++)
                    {
                        // find max
                        if (buffer[k] > maxConfidence)
                        {
                            maxConfidence = buffer[k];
                            maxIndex = k;
                        }
                    }

                    if (maxIndex < 0 || maxConfidence <= requiredConfidence) return; // skip low mul_conf results

                    YoloLabel label = _model.Labels[maxIndex - 5];

                    var prediction = new YoloPrediction(label, maxConfidence * boxConfidence)
                    {
                        Rectangle = new SKRect(xMin, yMin, xMax, yMax)
                    };

                    result.Add(prediction);
                });

                return result.ToList();
            }
            finally
            {
                _predictionListPool.Release(result);
            }
        }

        /// <summary>
        /// Parses net outputs (sigmoid) to predictions.
        /// </summary>
        /// <param name="output">All outputs from an inference call</param>
        /// <param name="image">The original input image</param>
        /// <returns>A list of Yolo predictions</returns>
        private List<YoloPrediction> ParseSigmoid(IList<DenseTensor<float>> output, SKImage image)
        {
            var result = new ConcurrentBag<YoloPrediction>();

            var (w, h) = (image.Width, image.Height); // image w and h
            var (xGain, yGain) = (_model.Width / (float)w, _model.Height / (float)h); // x, y gains
            var gain = Math.Min(xGain, yGain); // gain = resized / original

            var (xPad, yPad) = ((_model.Width - w * gain) / 2, (_model.Height - h * gain) / 2); // left, right pads

            Parallel.For(0, output.Count, (i) => // iterate model outputs
            {
                int shapes = _model.Shapes[i]; // shapes per output

                Parallel.For(0, _model.Anchors[0].Length, (a) => // iterate anchors
                {
                    Parallel.For(0, shapes, (y) => // iterate shapes (rows)
                    {
                        Parallel.For(0, shapes, (x) => // iterate shapes (columns)
                        {
                            int offset = (shapes * shapes * a + shapes * y + x) * _model.Dimensions;

                            float[] buffer = output[i].Skip(offset).Take(_model.Dimensions).Select(Sigmoid).ToArray();

                            if (buffer[4] <= _model.Confidence) return; // skip low obj_conf results

                            List<float> scores = buffer.Skip(5).Select(b => b * buffer[4]).ToList(); // mul_conf = obj_conf * cls_conf

                            float mulConfidence = scores.Max(); // max confidence score

                            if (mulConfidence <= _model.MulConfidence) return; // skip low mul_conf results

                            float rawX = (buffer[0] * 2 - 0.5f + x) * _model.Strides[i]; // predicted bbox x (center)
                            float rawY = (buffer[1] * 2 - 0.5f + y) * _model.Strides[i]; // predicted bbox y (center)

                            float rawW = (float)Math.Pow(buffer[2] * 2, 2) * _model.Anchors[i][a][0]; // predicted bbox w
                            float rawH = (float)Math.Pow(buffer[3] * 2, 2) * _model.Anchors[i][a][1]; // predicted bbox h

                            float[] xyxy = Xywh2xyxy(new float[] { rawX, rawY, rawW, rawH });

                            float xMin = Math.Clamp((xyxy[0] - xPad) / gain, 0, w - 0); // unpad, clip tlx
                            float yMin = Math.Clamp((xyxy[1] - yPad) / gain, 0, h - 0); // unpad, clip tly
                            float xMax = Math.Clamp((xyxy[2] - xPad) / gain, 0, w - 1); // unpad, clip brx
                            float yMax = Math.Clamp((xyxy[3] - yPad) / gain, 0, h - 1); // unpad, clip bry

                            YoloLabel label = _model.Labels[scores.IndexOf(mulConfidence)];

                            var prediction = new YoloPrediction(label, mulConfidence)
                            {
                                Rectangle = new SKRect(xMin, yMin, xMax, yMax)
                            };

                            result.Add(prediction);
                        });
                    });
                });
            });

            return result.ToList();
        }

        /// <summary>
        /// Parses net outputs (sigmoid or detect layer) to predictions.
        /// </summary>
        /// <param name="output">The output from an inference call</param>
        /// <param name="image">The original input image</param>
        /// <returns>A list of Yolo predictions</returns>
        private List<YoloPrediction> ParseOutput(DenseTensor<float>[] output, SKImage image, float minConfidence)
        {
            return _model.UseDetect 
                   ? ParseDetect(output[0], image, minConfidence) 
                   : ParseSigmoid(output, image);
        }

        /// <summary>
        /// Removes overlaped duplicates (nms).
        /// </summary>
        private List<YoloPrediction> Supress(List<YoloPrediction> items)
        {
            var result = new List<YoloPrediction>(items);

            foreach (YoloPrediction item in items) // iterate every prediction
            {
                foreach (var current in result.ToList()) // make a copy for each iteration
                {
                    try
                    {
                        if (current == item) continue;

                        var (rect1, rect2) = (item!.Rectangle, current.Rectangle);

                        var intersection = SKRect.Intersect(rect1, rect2);

                        float intArea   = intersection.Area(); // intersection area
                        float unionArea = rect1.Area() + rect2.Area() - intArea; // union area
                        float overlap   = intArea / unionArea; // overlap ratio

                        if (overlap >= _model.Overlap)
                        {
                            if (item.Score >= current.Score)
                            {
                                result.Remove(current);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Item is {(item is null ? "" : "not ")}null");
                        Debug.WriteLine(ex);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Runs object detection on an image
        /// </summary>
        /// <param name="image">The input image</param>
        /// <returns>A list of predictions</returns>
        public List<YoloPrediction> Predict(SKImage image, SKFilterQuality quality = SKFilterQuality.High, float minConfidence = 0.45f)
        {
            SKImage? resized = null;
            List<YoloPrediction>? results = null;
            try
            {
                if (image.Width != _model.Width || image.Height != _model.Height)
                {
                    resized = ResizeImage(image, quality); // fit image size to specified input size
                }
                var output = new List<DenseTensor<float>>();

                using var inferenceResults = Inference(resized ?? image);
                {
                    if (inferenceResults != null)
                    {
                        foreach (var item in _model.Outputs) // add outputs for processing
                        {
                            if (item is not null)
                            {
                                var firstResult = inferenceResults.First(x => x.Name == item).Value as DenseTensor<float>;
                                if (firstResult is not null)
                                    output.Add(firstResult);
                            }
                        }

                        List<YoloPrediction> predictions = ParseOutput(output.ToArray(), image, minConfidence);
                        results = Supress(predictions);
                    }
                }

                return results ?? new List<YoloPrediction>();
            }
            finally
            {
                if (resized != null)
                    resized.Dispose();
            }
        }

        /// <summary>
        /// Creates new instance of YoloScorer.
        /// </summary>
        public YoloScorer()
        {
            _model              = Activator.CreateInstance<T>();

            _tensorPool         = new ObjectPool<DenseTensor<float>>(8,
                                    () => new DenseTensor<float>(new[] { 1, 3, _model.Height, _model.Width }));

            _predictionListPool = new ObjectPool<ConcurrentBag<YoloPrediction>>(8,
                                    () => new ConcurrentBag<YoloPrediction>(),
                                    (x) => x.Clear());
        }

        /// <summary>
        /// Creates new instance of YoloScorer with weights path and options.
        /// </summary>
        public YoloScorer(string weights, SessionOptions? opts = null) : this()
        {
            FilePath = weights;

            // Breaking this up so we can debug timing.
            var bytes   = File.ReadAllBytes(weights);
            var options = opts ?? new SessionOptions();

            _inferenceSession = new InferenceSession(bytes, options);

            SetModelPropetiesFromMetadata();
        }

        /// <summary>
        /// Creates new instance of YoloScorer with weights stream and options.
        /// </summary>
        public YoloScorer(Stream weights, SessionOptions? opts = null) : this()
        {
            using (var reader = new BinaryReader(weights))
            {
                _inferenceSession = new InferenceSession(reader.ReadBytes((int)weights.Length), opts ?? new SessionOptions());
                SetModelPropetiesFromMetadata();
            }
        }

        /// <summary>
        /// Creates new instance of YoloScorer with weights bytes and options.
        /// </summary>
        public YoloScorer(byte[] weights, SessionOptions? opts = null) : this()
        {
            _inferenceSession = new InferenceSession(weights, opts ?? new SessionOptions());
            SetModelPropetiesFromMetadata();
        }

        private void SetModelPropetiesFromMetadata()
        {
            if (_inferenceSession is null)
                return;

            var inputMetadata = _inferenceSession.InputMetadata;
            if (inputMetadata is not null)
            {
                var inputName     = inputMetadata.Keys.First();
                var imageMetadata = inputMetadata[inputName];
                
                if (imageMetadata.Dimensions[1] > 0)
                    _model.Depth  = imageMetadata.Dimensions[1];
                if (imageMetadata.Dimensions[2] > 0)
                    _model.Height = imageMetadata.Dimensions[2];
                if (imageMetadata.Dimensions[3] > 0)
                    _model.Width  = imageMetadata.Dimensions[3];
            }

            // Get the output tensor name
            IReadOnlyDictionary<string, NodeMetadata> outputMetadata = _inferenceSession.OutputMetadata;
            if (outputMetadata is null)
                return;

            string[] outputs = outputMetadata.Keys.ToArray();
            _model.Outputs   = outputs;
            var metaOutput   = outputMetadata[outputs.First()];
            if (metaOutput.Dimensions[2] > 0)
                _model.Dimensions = metaOutput.Dimensions[2];

            // get the labels for the model classes. Defaults to COCO labels if not included
            if (_inferenceSession.ModelMetadata.CustomMetadataMap.TryGetValue("names", out string? labelsStr))
            {
                MatchCollection matches = Regex.Matches(labelsStr, "\'([^\']*)\'");
                var labels = matches.Select(m => m.Groups[1].Value);

                _model.Labels = labels.Select((x, i) => new YoloLabel() { Name = x, Id = i }).ToList();
            }
        }

        /// <summary>
        /// Disposes YoloScorer instance.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _inferenceSession?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~YoloScorer()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        /// <summary>
        /// Disposes YoloScorer instance.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
