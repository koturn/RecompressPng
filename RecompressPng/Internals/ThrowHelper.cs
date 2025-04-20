#if !NET6_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#endif  // !NET6_0_OR_GREATER


namespace RecompressPng.Internals
{
    /// <summary>
    /// Provides exception throwing methods.
    /// </summary>
    internal static class ThrowHelper
    {
#if !NET6_0_OR_GREATER
        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowArgumentNullExceptionIfNull(object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                ThrowArgumentNullException(paramName);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/>.
        /// </summary>
        /// <param name="paramName">The name of the parameter which is null.</param>
        /// <exception cref="ArgumentNullException">Always thrown.</exception>
        [DoesNotReturn]
        public static void ThrowArgumentNullException(string? paramName)
        {
            throw new ArgumentNullException(paramName);
        }
#endif  // !NET6_0_OR_GREATER
    }
}
