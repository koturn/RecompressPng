using System;
using System.Runtime.InteropServices;
using System.Security;


namespace RecompressPng
{
    /// <summary>
    /// Option value for zopflipng.
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
            public static extern void CZopfliPNGSetDefaults(out CZopfliPNGOptions pngOptions);

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
                [In] byte[] origPng,
                [In] UIntPtr origpngSize,
                in CZopfliPNGOptions pngOptions,
                [In] bool verbose,
                out MallocedMemoryHandle resultPng,
                out UIntPtr resultpngSize);
        }

        /// <summary>
        /// <para><see cref="SafeHandle"/> for malloced memory.</para>
        /// <para>Free memory using <see cref="Marshal.FreeCoTaskMem(IntPtr)"/>.</para>
        /// </summary>
        internal sealed class MallocedMemoryHandle : SafeHandle
        {
            /// <summary>
            /// Private ctor
            /// </summary>
            private MallocedMemoryHandle()
                : base(IntPtr.Zero, true)
            {
            }

            /// <summary>
            /// True if the memory is not allocated (null pointer), otherwise false.
            /// </summary>
            public override bool IsInvalid
            {
                get { return handle == IntPtr.Zero; }
            }

            /// <summary>
            /// Free malloced memory using <see cref="Marshal.FreeCoTaskMem(IntPtr)"/>.
            /// </summary>
            /// <returns>True if freeing is successful, otherwise false</returns>
            protected override bool ReleaseHandle()
            {
                Marshal.FreeCoTaskMem(handle);
                return true;
            }
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
            using (var cPngOptions = new CZopfliPNGOptions(pngOptions))
            {
                var error = UnsafeNativeMethods.CZopfliPNGOptimize(
                    pngData,
                    (UIntPtr)pngDataLength,
                    in cPngOptions,
                    verbose,
                    out var pResultPngHandle,
                    out var resultPngSize);

                if (pResultPngHandle.IsInvalid)
                {
                    return null;
                }

                using (pResultPngHandle)
                {
                    if (error != 0)
                    {
                        return null;
                    }
                    var resultPng = new byte[(ulong)resultPngSize];
                    Marshal.Copy(pResultPngHandle.DangerousGetHandle(), resultPng, 0, resultPng.Length);
                    return resultPng;
                }
            }
        }
    }
}
