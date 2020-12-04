using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;


namespace RecompressPng
{
    /// <summary>
    /// <para>Option value for zopflipng.</para>
    /// <para>This structure is used to interact with zopflipng.dll.</para>
    /// </summary>
    /// <seealso cref="ZopfliPng.UnsafeNativeMethods.CZopfliPNGSetDefaults(ref CZopfliPNGOptions)"/>
    /// <seealso cref="ZopfliPng.UnsafeNativeMethods.CZopfliPNGOptimize(byte[], UIntPtr, CZopfliPNGOptions, bool, out IntPtr, out UIntPtr)"/>
    public struct CZopfliPNGOptions : IDisposable
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
        public IntPtr FilterStrategiesPointer { get; set; }
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
        public IntPtr KeepChunksPointer { get; set; }
        /// <summary>
        /// How many entries in keepchunks.
        /// </summary>
        public int NumKeepChunks { get; set; }
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
#pragma warning disable IDE0051
        /// <summary>
        /// Unused, left for backwards compatiblity.
        /// </summary>
        private readonly int _blockSplitStrategy;
#pragma warning restore IDE0051


        /// <summary>
        /// Create option instance for zopflipng with default parameters.
        /// </summary>
        /// <param name="lossyTransparent">Allow altering hidden colors of fully transparent pixels.</param>
        /// <param name="lossy8bit">Convert 16-bit per channel images to 8-bit per channel.</param>
        /// <param name="autoFilterStrategy">Automatically choose filter strategy using less good compression.</param>
        /// <param name="useZopfli">Use Zopfli deflate compression.</param>
        /// <param name="numIterations">Zopfli number of iterations.</param>
        /// <param name="numIterationsLarge">Zopfli number of iterations on large images.</param>
        public CZopfliPNGOptions(
            bool lossyTransparent = false,
            bool lossy8bit = false,
            bool autoFilterStrategy = true,
            bool useZopfli = true,
            int numIterations = 15,
            int numIterationsLarge = 5)
        {
            LossyTransparent = lossyTransparent;
            Lossy8bit = lossy8bit;
            FilterStrategiesPointer = IntPtr.Zero;
            NumFilterStrategies = 0;
            AutoFilterStrategy = autoFilterStrategy;
            KeepChunksPointer = IntPtr.Zero;
            NumKeepChunks = 0;
            UseZopfli = useZopfli;
            NumIterations = numIterations;
            NumIterationsLarge = numIterationsLarge;
            _blockSplitStrategy = 0;
        }

        /// <summary>
        /// Create option instance from <see cref="ZopfliPNGOptions"/>.
        /// </summary>
        /// <param name="pngOptions">Allow altering hidden colors of fully transparent pixels.</param>
        public CZopfliPNGOptions(ZopfliPNGOptions pngOptions)
            : this(
                  pngOptions.LossyTransparent,
                  pngOptions.Lossy8bit,
                  pngOptions.AutoFilterStrategy,
                  pngOptions.UseZopfli,
                  pngOptions.NumIterations,
                  pngOptions.NumIterationsLarge)
        {
            var filterStrategies = pngOptions.FilterStrategies;
            var filterStrategiesCount = filterStrategies.Count;
            FilterStrategiesPointer = Marshal.AllocCoTaskMem(sizeof(ZopfliPNGFilterStrategy) * filterStrategiesCount);
            NumFilterStrategies = filterStrategies.Count;

            unsafe
            {
                var p = (ZopfliPNGFilterStrategy*)FilterStrategiesPointer;
                for (int i = 0; i < filterStrategiesCount; i++)
                {
                    p[i] = filterStrategies[i];
                }
            }

            var keepChunks = pngOptions.KeepChunks;
            var totalKeepChunkMemorySize = keepChunks.Aggregate(0, (acc, keepChunk) => acc + (keepChunk.Length + 1) * sizeof(char));
            unsafe
            {
                // Memory:
                //   p_i: Pointer to c_i.
                //   c_i: Null-terminate string.
                //   [p_0][p_1][p_2]...[p_n][c_1][c_2][c_3]...[c_n]
                KeepChunksPointer = Marshal.AllocCoTaskMem(keepChunks.Count * sizeof(IntPtr) + totalKeepChunkMemorySize);
                var keepChunksCount = keepChunks.Count;
                var p = (byte**)KeepChunksPointer;
                var q = (byte*)(p + keepChunksCount);

                for (int i = 0; i < keepChunksCount; i++)
                {
                    p[i] = q;
                    foreach (var c in Encoding.ASCII.GetBytes(keepChunks[i]))
                    {
                        *q++ = c;
                    }
                    *q++ = 0;  // Null-terminate
                }
            }
            NumKeepChunks = keepChunks.Count;
        }

        /// <summary>
        /// Get default option value.
        /// </summary>
        /// <returns>Default option value.</returns>
        public static CZopfliPNGOptions GetDefault()
        {
            var cPngOptions = new CZopfliPNGOptions();
            ZopfliPng.UnsafeNativeMethods.CZopfliPNGSetDefaults(ref cPngOptions);
            return cPngOptions;
        }

        /// <summary>
        /// Dispose resource of <see cref="FilterStrategiesPointer"/>.
        /// </summary>
        public void Dispose()
        {
            if (FilterStrategiesPointer != null)
            {
                Marshal.FreeCoTaskMem(FilterStrategiesPointer);
            }
            if (KeepChunksPointer != null)
            {
                Marshal.FreeCoTaskMem(KeepChunksPointer);
            }
        }
    }
}
