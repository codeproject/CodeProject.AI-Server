using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace CodeProject.AI.Modules.PortraitFilter
{
    /// <summary>
    /// Defines deep person lab.
    /// </summary>
    public class DeepPersonLab
    {
        #region Private data
        private const int _person = 15;
        private const int _size = 513;
        private InferenceSession _session;
        #endregion

        #region Class components
        /// <summary>
        /// Initializes deep person lab.
        /// </summary>
        /// <param name="modelPath">Model path</param>
        public DeepPersonLab(string modelPath, SessionOptions? sessionOptions = null)
        {
            sessionOptions = sessionOptions ?? new SessionOptions();

            var tickCount = Environment.TickCount;
            // Console.WriteLine("Starting inference session...");
            _session = new InferenceSession(modelPath, sessionOptions);
            // Console.WriteLine($"Session started in {Environment.TickCount - tickCount} ms.");
        }
        /// <summary>
        /// Returns segmentation mask.
        /// </summary>
        /// <param name="image">Input image</param>
        /// <returns>Segmentation mask</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability",
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public Bitmap Fit(Bitmap image)
        {
            // scaling image
            var width  = image.Width;
            var height = image.Height;
            var ratio  = 1.0f * _size / Math.Max(width, height);
            var size   = new Size(
                (int)(ratio * width),
                (int)(ratio * height));
            var resized = new Bitmap(image, size);

            // creating tensor
            // Console.WriteLine("Creating image tensor...");
            var tickCount  = Environment.TickCount;
            var inputMeta  = _session.InputMetadata;
            var name       = inputMeta.Keys.ToArray()[0];
            var dimentions = new int[] { 1, size.Height, size.Width, 3 };
            var inputData  = Onnx.ToTensor(resized);
            resized.Dispose();
            // Console.WriteLine($"Tensor was created in {Environment.TickCount - tickCount} ms.");

            // prediction
            // Console.WriteLine("Creating segmentation mask...");
            tickCount   = Environment.TickCount;
            var t1      = new DenseTensor<byte>(inputData, dimentions);
            var inputs  = new List<NamedOnnxValue>() { NamedOnnxValue.CreateFromTensor(name, t1) };
            var results = _session.Run(inputs).ToArray();
            var map     = results[0].AsTensor<long>().ToArray();
            var mask    = DeepPersonLab.FromSegmentationMap(map, size.Width, size.Height);
            // Console.WriteLine($"Segmentation was created in {Environment.TickCount - tickCount} ms.");

            // return mask
            return new Bitmap(mask, width, height);
        }
        #endregion

        #region Static methods
        /// <summary>
        /// Converts an RGB tensor array to a color image.
        /// </summary>
        /// <param name="tensor">RGB tensor array</param>
        /// <param name="width">Bitmap width</param>
        /// <param name="height">Bitmap height</param>
        /// <returns>Bitmap</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability",
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public unsafe static Bitmap FromSegmentationMap(long[] tensor, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);
            FromSegmentationMap(tensor, width, height, bitmap);
            return bitmap;
        }
        /// <summary>
        /// Converts an RGB tensor array to a color image.
        /// </summary>
        /// <param name="tensor">RGBA tensor array</param>
        /// <param name="width">Bitmap width</param>
        /// <param name="height">Bitmap height</param>
        /// <param name="Data">Bitmap</param>
        public static void FromSegmentationMap(long[] tensor, int width, int height, Bitmap Data)
        {
            BitmapData bmData = Onnx.Lock24bpp(Data);
            FromSegmentationMap(tensor, width, height, bmData);
            Onnx.Unlock(Data, bmData);
            return;
        }
        /// <summary>
        /// Converts an RGB tensor array to a color image.
        /// </summary>
        /// <param name="tensor">RGBA tensor array</param>
        /// <param name="width">Bitmap width</param>
        /// <param name="height">Bitmap height</param>
        /// <param name="bmData">Bitmap data</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability",
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public unsafe static void FromSegmentationMap(long[] tensor, int width, int height, BitmapData bmData)
        {
            // params
            int stride = bmData.Stride;
            byte* p = (byte*)bmData.Scan0.ToPointer();
            int pos = 0;

            // do job
            for (int j = 0; j < height; j++)
            {
                int k, jstride = j * stride;

                for (int i = 0; i < width; i++, pos++)
                {
                    k = jstride + i * 3;

                    var z = (tensor[pos] == _person)
                        ? (byte)255 : (byte)0;

                    // rgb
                    p[k + 2] = z;
                    p[k + 1] = z;
                    p[k + 0] = z;
                }
            }

            return;
        }
        #endregion
    }
}
