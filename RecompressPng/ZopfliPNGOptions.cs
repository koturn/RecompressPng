using System.Runtime.InteropServices;


namespace RecompressPng
{
    /// <summary>
    /// Enum forzopflipng strategy.
    /// </summary>
    public enum ZopfliPNGFilterStrategy
    {
        StrategyZero = 0,
        StrategyOne = 1,
        StrategyTwo = 2,
        StrategyThree = 3,
        StrategyFour = 4,
        StrategyMinSum = 5,
        StrategyEntropy = 6,
        StrategyPredefined = 7,
        StrategyBruteForce = 8
    };

    /// <summary>
    /// Structure of options for zopflipng.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ZopfliPNGOptions
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
        public ZopfliPNGFilterStrategy[] FilterStrategies { get; set; }
        /// <summary>
        /// How many strategies to try.
        /// </summary>
        public int NumFilterStrategies { get; set; }
        /// <summary>
        /// Automatically choose filter strategy using less good compression.
        /// </summary>
        public bool AutoFilterStrategy { get; set; }
        /// <summary>
        /// <para>PNG chunks to keep</para>
        /// <para>chunks to literally copy over from the original PNG to the resulting one.</para>
        /// </summary>
        public string[] KeepChunks { get; set; }
        /// <summary>
        /// How many entries in keepchunks.
        /// </summary>
        int NumKeepChunks { get; set; }
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
        /// Unused, left for backwards compatiblity.
        /// </summary>
        private int _blockSplitStrategy { get; set; }

        /// <summary>
        /// Get default option value.
        /// </summary>
        /// <returns>Default option value</returns>
        public static ZopfliPNGOptions GetDefault()
        {
            var pngOptions = new ZopfliPNGOptions();
            ZopfliPng.UnsafeNativeMethods.CZopfliPNGSetDefaults(ref pngOptions);
            return pngOptions;
        }
    }
}
