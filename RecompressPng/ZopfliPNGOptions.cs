using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


namespace RecompressPng
{
    /// <summary>
    /// Enum forzopflipng strategy.
    /// </summary>
    public enum ZopfliPNGFilterStrategy
    {
        /// <summary>
        /// Give all scanlines PNG filter type 0.
        /// </summary>
        Zero = 0,
        /// <summary>
        /// Give all scanlines PNG filter type 1.
        /// </summary>
        One = 1,
        /// <summary>
        /// Give all scanlines PNG filter type 2.
        /// </summary>
        Two = 2,
        /// <summary>
        /// Give all scanlines PNG filter type 3.
        /// </summary>
        Three = 3,
        /// <summary>
        /// Give all scanlines PNG filter type 4.
        /// </summary>
        Four = 4,
        /// <summary>
        /// Minimum sum.
        /// </summary>
        MinSum = 5,
        /// <summary>
        /// Entropy.
        /// </summary>
        Entropy = 6,
        /// <summary>
        /// Predefined (keep from input, this likely overlaps another strategy).
        /// </summary>
        Predefined = 7,
        /// <summary>
        /// Brute force (experimental).
        /// </summary>
        BruteForce = 8
    };

    /// <summary>
    /// Structure of options for zopflipng.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public class ZopfliPNGOptions
    {
        /// <summary>
        /// Allow altering hidden colors of fully transparent pixels.
        /// </summary>
        public bool LossyTransparent { get; set; }
        /// <summary>
        /// Convert 16-bit per channel images to 8-bit per channel.
        /// </summary>
        public bool Lossy8bit { get; set; }
        /// <summary>
        /// Filter strategies to try.
        /// </summary>
        public List<ZopfliPNGFilterStrategy> FilterStrategies { get; }
        /// <summary>
        /// Automatically choose filter strategy using less good compression.
        /// </summary>
        public bool AutoFilterStrategy { get; set; }
        /// <summary>
        /// <para>PNG chunks to keep</para>
        /// <para>chunks to literally copy over from the original PNG to the resulting one.</para>
        /// </summary>
        public List<string> KeepChunks { get; }
        /// <summary>
        /// Use Zopfli deflate compression.
        /// </summary>
        public bool UseZopfli { get; set; }
        /// <summary>
        /// Zopfli number of iterations.
        /// </summary>
        public int NumIterations { get; set; }
        /// <summary>
        /// Zopfli number of iterations on large images.
        /// </summary>
        public int NumIterationsLarge { get; set; }


        /// <summary>
        /// Create option instance for zopflipng with default parameters.
        /// </summary>
        /// <param name="lossyTransparent">Allow altering hidden colors of fully transparent pixels.</param>
        /// <param name="lossy8bit">Convert 16-bit per channel images to 8-bit per channel.</param>
        /// <param name="autoFilterStrategy">Automatically choose filter strategy using less good compression.</param>
        /// <param name="useZopfli">Use Zopfli deflate compression.</param>
        /// <param name="numIterations">Zopfli number of iterations.</param>
        /// <param name="numIterationsLarge">Zopfli number of iterations on large images.</param>
        public ZopfliPNGOptions(
            bool lossyTransparent = false,
            bool lossy8bit = false,
            bool autoFilterStrategy = true,
            bool useZopfli = true,
            int numIterations = 15,
            int numIterationsLarge = 5)
        {
            LossyTransparent = lossyTransparent;
            Lossy8bit = lossy8bit;
            FilterStrategies = new List<ZopfliPNGFilterStrategy>();
            AutoFilterStrategy = autoFilterStrategy;
            KeepChunks = new List<string>();
            UseZopfli = useZopfli;
            NumIterations = numIterations;
            NumIterationsLarge = numIterationsLarge;
        }

        /// <summary>
        /// Get default option value from zopflipng.dll.
        /// </summary>
        /// <returns>Default option value.</returns>
        public static ZopfliPNGOptions GetDefault()
        {
            var cPngOptions = CZopfliPNGOptions.GetDefault();
            var obj = new ZopfliPNGOptions(
                cPngOptions.LossyTransparent,
                cPngOptions.Lossy8bit,
                cPngOptions.AutoFilterStrategy,
                cPngOptions.UseZopfli,
                cPngOptions.NumIterations,
                cPngOptions.NumIterationsLarge);


            if (cPngOptions.FilterStrategiesPointer != IntPtr.Zero)
            {
                unsafe
                {
                    var pFilterStrategies = (int*)cPngOptions.FilterStrategiesPointer;
                    for (int i = 0, im = cPngOptions.NumFilterStrategies; i < im; i++)
                    {
                        obj.FilterStrategies.Add((ZopfliPNGFilterStrategy)pFilterStrategies[i]);
                    }
                }
            }
            if (cPngOptions.KeepChunks != null)
            {
                obj.KeepChunks.AddRange(cPngOptions.KeepChunks);
            }

            return obj;
        }
    }
}
