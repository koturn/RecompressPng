using System;
using System.Runtime.InteropServices;
using System.Security;


namespace RecompressPng
{
    /// <summary>
    /// Utility class of zopflipng.dll
    /// </summary>
    public static class ZopfliPng
    {
        /// <summary>
        /// Native methods.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal static class UnsafeNativeMethods
        {
            /// <summary>
            /// Get default parameter of <see cref="ZopfliPNGOptions"/>.
            /// </summary>
            /// <param name="pngOptions">Options struct for zopflipng.</param>
            [DllImport("zopflipng.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern void CZopfliPNGSetDefaults(ref ZopfliPNGOptions pngOptions);

            /// <summary>
            /// Re-compress deflated data in PNG with zopfli algorithm.
            /// </summary>
            /// <param name="origPng">Source PNG binary.</param>
            /// <param name="origpngSize">Size of PNG binary.</param>
            /// <param name="pngOptions">Options for zopflipng.</param>
            /// <param name="verbose">Output verbose message to stdout using printf() or not from zopflipng.dll.</param>
            /// <param name="resultPng">Result PNG binary. This data is allocated with malloc in zopflipng.dll and caller of this method have to free the memory.</param>
            /// <param name="resultpngSize">Result PNG binary size.</param>
            /// <returns>Status code. 0 means success, otherwise it means failure.</returns>
            [DllImport("zopflipng.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern unsafe int CZopfliPNGOptimize(
                byte[] origPng,
                UIntPtr origpngSize,
                ref ZopfliPNGOptions pngOptions,
                bool verbose,
                out IntPtr resultPng,
                out UIntPtr resultpngSize);
        }

        /// <summary>
        /// Re-compress deflated data in PNG with zopfli algorithm.
        /// </summary>
        /// <param name="pngData">Source PNG binary.</param>
        /// <param name="verbose">Output verbose message to stdout using printf() or not from zopflipng.dll.</param>
        /// <returns>Result PNG binary.</returns>
        public static byte[] OptimizePng(byte[] pngData, bool verbose = false)
        {
            return OptimizePng(pngData, pngData.LongLength, ZopfliPNGOptions.GetDefault(), verbose);
        }

        /// <summary>
        /// Re-compress deflated data in PNG with zopfli algorithm.
        /// </summary>
        /// <param name="pngData">Source PNG binary.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="verbose">Output verbose message to stdout using printf() or not from zopflipng.dll.</param>
        /// <returns>Result PNG binary.</returns>
        public static byte[] OptimizePng(byte[] pngData, ZopfliPNGOptions pngOptions, bool verbose = false)
        {
            return OptimizePng(pngData, pngData.LongLength, pngOptions, verbose);
        }

        /// <summary>
        /// Re-compress deflated data in PNG with zopfli algorithm.
        /// </summary>
        /// <param name="pngData">Source PNG binary.</param>
        /// <param name="pngDataLength">Byte length of <paramref name="pngData"/>.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="verbose">Output verbose message to stdout using printf() or not from zopflipng.dll.</param>
        /// <returns>Result PNG binary.</returns>
        public static byte[] OptimizePng(byte[] pngData, long pngDataLength, ZopfliPNGOptions pngOptions, bool verbose = false)
        {
            var error = UnsafeNativeMethods.CZopfliPNGOptimize(
                pngData,
                (UIntPtr)pngDataLength,
                ref pngOptions,
                verbose,
                out var pResultPng,
                out var resultPngSize);

            if (error != 0)
            {
                return null;
            }

            var resultPng = new byte[(ulong)resultPngSize];
            Marshal.Copy(pResultPng, resultPng, 0, resultPng.Length);

            // Free malloc() memory from C library
            Marshal.FreeCoTaskMem(pResultPng);

            return resultPng;
        }
    }
}
