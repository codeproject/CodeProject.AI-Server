using System.Drawing;
using System.Drawing.Imaging;

namespace CodeProject.AI.Modules.PortraitFilter
{
    /// <summary>
    /// Uses for onnx transformations.
    /// </summary>
    public static class Onnx
    {
        #region Tensor
        /// <summary>
        /// Converts a Bitmap to an RGB tensor array.
        /// </summary>
        /// <param name="Data">Bitmap</param>
        /// <returns>RGB tensor array</returns>
        public static byte[] ToTensor(this Bitmap Data)
        {
            BitmapData bmData = Onnx.Lock24bpp(Data);
            byte[] rgb = Onnx.ToTensor(bmData);
            Onnx.Unlock(Data, bmData);
            return rgb;
        }
        /// <summary>
        /// Converts a Bitmap to an RGB tensor array.
        /// </summary>
        /// <param name="bmData">Bitmap data</param>
        /// <returns>RGB tensor array</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability",
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public unsafe static byte[] ToTensor(this BitmapData bmData)
        {
            // params
            int width = bmData.Width, height = bmData.Height, stride = bmData.Stride;
            byte* p = (byte*)bmData.Scan0.ToPointer();
            byte[] t = new byte[3 * height * width];
            int pos = 0;

            // do job
            for (int j = 0; j < height; j++)
            {
                int k, jstride = j * stride;

                for (int i = 0; i < width; i++)
                {
                    k = jstride + i * 3;

                    t[pos++] = p[k + 2];
                    t[pos++] = p[k + 1];
                    t[pos++] = p[k + 0];
                }
            }

            return t;
        }
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
        public unsafe static Bitmap FromTensor(this byte[] tensor, int width, int height)
        {
            Bitmap bitmap = new Bitmap(width, height);
            FromTensor(tensor, width, height, bitmap);
            return bitmap;
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
        public unsafe static void FromTensor(this byte[] tensor, int width, int height, BitmapData bmData)
        {
            // params
            int stride = bmData.Stride;
            byte* p = (byte*)bmData.Scan0.ToPointer();
            int pos = 0;

            // do job
            for (int j = 0; j < height; j++)
            {
                int k, jstride = j * stride;

                for (int i = 0; i < width; i++)
                {
                    k = jstride + i * 3;

                    // rgb
                    p[k + 2] = tensor[pos++];
                    p[k + 1] = tensor[pos++];
                    p[k + 0] = tensor[pos++];
                }
            }

            return;
        }
        /// <summary>
        /// Converts an RGB tensor array to a color image.
        /// </summary>
        /// <param name="tensor">RGBA tensor array</param>
        /// <param name="width">Bitmap width</param>
        /// <param name="height">Bitmap height</param>
        /// <param name="Data">Bitmap</param>
        public static void FromTensor(this byte[] tensor, int width, int height, Bitmap Data)
        {
            BitmapData bmData = Onnx.Lock24bpp(Data);
            FromTensor(tensor, width, height, bmData);
            Onnx.Unlock(Data, bmData);
            return;
        }
        #endregion

        #region BitmapData voids
        /// <summary>
        /// Blocks Bitmap in system memory.
        /// </summary>
        /// <param name="b">Bitmap</param>
        /// <returns>Bitmap data</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability",
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public static BitmapData Lock24bpp(this Bitmap b)
        {
            return b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        }
        /// <summary>
        /// Unblocks Bitmap in system memory.
        /// </summary>
        /// <param name="b">Bitmap</param>
        /// <param name="bmData">Bitmap data</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability",
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public static void Unlock(this Bitmap b, BitmapData bmData)
        {
            b.UnlockBits(bmData);
            return;
        }
        #endregion
    }
}
