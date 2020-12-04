﻿using System;
using System.Collections.Generic;


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
        /// <para>PNG chunks to keep.</para>
        /// <para>Chunks to literally copy over from the original PNG to the resulting one.</para>
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
        /// Create option instance for zopflipng.
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
            : this(lossyTransparent, lossy8bit, null, autoFilterStrategy, null, useZopfli, numIterations, numIterationsLarge)
        {
        }

        /// <summary>
        /// Create option instance for zopflipng.
        /// </summary>
        /// <param name="lossyTransparent">Allow altering hidden colors of fully transparent pixels.</param>
        /// <param name="lossy8bit">Convert 16-bit per channel images to 8-bit per channel.</param>
        /// <param name="filterStrategies">Filter strategies to try.</param>
        /// <param name="autoFilterStrategy">Automatically choose filter strategy using less good compression.</param>
        /// <param name="keepChunks">PNG chunks to keep.</param>
        /// <param name="useZopfli">Use Zopfli deflate compression.</param>
        /// <param name="numIterations">Zopfli number of iterations.</param>
        /// <param name="numIterationsLarge">Zopfli number of iterations on large images.</param>
        public ZopfliPNGOptions(
            bool lossyTransparent = false,
            bool lossy8bit = false,
            List<ZopfliPNGFilterStrategy> filterStrategies = null,
            bool autoFilterStrategy = true,
            List<string> keepChunks = null,
            bool useZopfli = true,
            int numIterations = 15,
            int numIterationsLarge = 5)
        {
            LossyTransparent = lossyTransparent;
            Lossy8bit = lossy8bit;
            FilterStrategies = filterStrategies;
            AutoFilterStrategy = autoFilterStrategy;
            KeepChunks = keepChunks;
            UseZopfli = useZopfli;
            NumIterations = numIterations;
            NumIterationsLarge = numIterationsLarge;
        }

        /// <summary>
        /// Create option instance from <see cref="CZopfliPNGOptions"/>.
        /// </summary>
        /// <param name="cPngOptions">Allow altering hidden colors of fully transparent pixels.</param>
        public ZopfliPNGOptions(in CZopfliPNGOptions cPngOptions)
            : this(
                cPngOptions.LossyTransparent,
                cPngOptions.Lossy8bit,
                CreateFilterStrategies(cPngOptions.FilterStrategiesPointer, cPngOptions.NumFilterStrategies),
                cPngOptions.AutoFilterStrategy,
                CreateKeepChunks(cPngOptions.KeepChunksPointer, cPngOptions.NumKeepChunks),
                cPngOptions.UseZopfli,
                cPngOptions.NumIterations,
                cPngOptions.NumIterationsLarge)
        {
        }

        /// <summary>
        /// Get default option value from zopflipng.dll.
        /// </summary>
        /// <returns>Default option value.</returns>
        public static ZopfliPNGOptions GetDefault()
        {
            using (var cPngOptions = CZopfliPNGOptions.GetDefault())
            {
                return new ZopfliPNGOptions(cPngOptions);
            }
        }

        /// <summary>
        /// Create filter strategy list from unmanaged memory in <see cref="CZopfliPNGOptions"/>.
        /// </summary>
        /// <param name="filterStrategiesPointer">Unmanaged memory pointer to filter strategies. (<see cref="CZopfliPNGOptions.FilterStrategiesPointer"/>)</param>
        /// <param name="numFilterStrategies">Number of filter strategies. (Unmanaged memory pointer of <see cref="CZopfliPNGOptions.NumFilterStrategies"/>)</param>
        /// <returns>Created filter strategy list.</returns>
        private static List<ZopfliPNGFilterStrategy> CreateFilterStrategies(IntPtr filterStrategiesPointer, int numFilterStrategies)
        {
            if (filterStrategiesPointer == IntPtr.Zero || numFilterStrategies == 0)
            {
                return new List<ZopfliPNGFilterStrategy>();
            }

            var filterStrategies = new List<ZopfliPNGFilterStrategy>(numFilterStrategies);
            unsafe
            {
                var pFilterStrategies = (ZopfliPNGFilterStrategy*)filterStrategiesPointer;
                for (int i = 0; i < numFilterStrategies; i++)
                {
                    filterStrategies.Add(pFilterStrategies[i]);
                }
            }

            return filterStrategies;
        }

        /// <summary>
        /// Create keep chunks list from unmanaged memory in <see cref="CZopfliPNGOptions"/>.
        /// </summary>
        /// <param name="keepChunksPointer">Unmanaged memory pointer to keep chunks. (<see cref="CZopfliPNGOptions.KeepChunksPointer"/>)</param>
        /// <param name="numKeepChunks">Number of keep chunks. (<see cref="CZopfliPNGOptions.NumKeepChunks"/>)</param>
        /// <returns>Created keep chunks list.</returns>
        private static List<string> CreateKeepChunks(IntPtr keepChunksPointer, int numKeepChunks)
        {
            if (keepChunksPointer == IntPtr.Zero || numKeepChunks == 0)
            {
                return new List<string>();
            }

            var keepChunks = new List<string>(numKeepChunks);
            unsafe
            {
                var pKeepChunks = (sbyte**)keepChunksPointer;
                for (int i = 0; i < numKeepChunks; i++)
                {
                    keepChunks.Add(new string(pKeepChunks[i]));
                }
            }

            return keepChunks;
        }
    }
}
