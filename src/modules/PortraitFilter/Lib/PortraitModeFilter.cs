using System;
using System.Drawing;
using UMapx.Core;
using UMapx.Imaging;

namespace CodeProject.AI.Modules.PortraitFilter
{
    /// <summary>
    /// Defines "portrait mode" filter.
    /// </summary>
    public class PortraitModeFilter
    {
        #region Private data
        BoxBlur _boxBlur;
        AlphaChannel _alphaChannelFilter;
        Merge _merge;
        float _strength;
        #endregion

        #region Class components
        /// <summary>
        /// Initializes "portrait mode" filter.
        /// </summary>
        /// <param name="strength">Strength</param>
        public PortraitModeFilter(float strength)
        {
            _boxBlur            = new BoxBlur();
            _alphaChannelFilter = new AlphaChannel();
            _merge              = new Merge(0, 0, 255);
            _strength           = strength;
        }
        /// <summary>
        /// Gets or sets strength.
        /// </summary>
        public float Strength
        {
            get
            {
                return _strength;
            }
            set
            {
                _strength = Maths.Float(value);
            }
        }


        /// <summary>
        /// Applies filter to image.
        /// </summary>
        /// <param name="image">Input image</param>
        /// <param name="mask">Segmentation mask</param>
        /// <returns>Portrait image</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", 
            "CA1416:Validate platform compatibility",
            Justification = "System.Drawing.EnableUnixSupport is enabled in the runtimeconfig.template.json. A true fix will be made soon.")]
        public Bitmap Apply(Bitmap image, Bitmap mask)
        {
            // time
            int tic = Environment.TickCount;
            Console.WriteLine("Applying portrait mode filter...");

            // deep person lab
            Bitmap alphaMask         = (Bitmap)image.Clone();
            Bitmap portrait          = (Bitmap)image.Clone();
            Bitmap segmentantionMask = (Bitmap)mask.Clone();

            // radius calculation
            int radius = (int)(_strength * 3 * (( Math.Max(image.Height, image.Width) / 100 ) + 1));
            Console.WriteLine($"Blur radius --> {radius}");

            // gaussian blur approximation
            _boxBlur.Size = new SizeInt(radius, radius);
            _boxBlur.Apply(portrait);
            _boxBlur.Apply(segmentantionMask);

            _boxBlur.Size = new SizeInt(radius / 2, radius / 2);
            _boxBlur.Apply(portrait);
            _boxBlur.Apply(segmentantionMask);

            // merging images
            _alphaChannelFilter.Apply(alphaMask, segmentantionMask);
            _merge.Apply(portrait, alphaMask);
            alphaMask.Dispose();
            segmentantionMask.Dispose();
            Console.WriteLine($"Portrait mode filter was applied in {Environment.TickCount - tic} mls.");

            return portrait;
        }
        #endregion
    }
}
