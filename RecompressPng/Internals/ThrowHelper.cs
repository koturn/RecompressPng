#if !NETCOREAPP3_0_OR_GREATER

using System;
using System.Diagnostics.CodeAnalysis;


namespace RecompressPng.Internals
{
    /// <summary>
    /// Provides exception throwing methods.
    /// </summary>
    internal static class ThrowHelper
    {
        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/>.
        /// </summary>
        /// <param name="objectName">The name of the disposed object.</param>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <exception cref="ObjectDisposedException">Always thrown.</exception>
        [DoesNotReturn]
        public static void ThrowObjectDisposedException(string objectName, string message)
        {
            throw new ObjectDisposedException(objectName, message);
        }
    }
}


#endif  // QNETCOREAPP3_0_OR_GREATER
