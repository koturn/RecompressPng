#if NET7_0_OR_GREATER
#    define SUPPORT_GENERATED_REGEX
#endif  // NET7_0_OR_GREATER
#if NET9_0_OR_GREATER
#    define SUPPORT_GENERATED_REGEX_PROPERTY
#endif  // NET9_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;


namespace RecompressPng.Internals
{
    /// <summary>
    /// Provides some <see cref="Regex"/> instances.
    /// </summary>
#if SUPPORT_GENERATED_REGEX
    internal static partial class RegexHelper
#else
    internal static class RegexHelper
#endif  // SUPPORT_GENERATED_REGEX
    {
        /// <summary>
        /// Options for <see cref="Regex"/> instances.
        /// </summary>
        private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;

        /// <summary>
        /// <see cref="Regex"/> pattern <see cref="string"/> that performs substitutions to minify JSON.
        /// </summary>
        [StringSyntax(StringSyntaxAttribute.Regex)]
        internal const string MinifyJsonPattern = "(\"(?:[^\"\\\\]|\\\\.)*\")|\\s+";

        /// <summary>
        /// <see cref="Regex"/> instance that performs substitutions to minify JSON.
        /// </summary>
#if !SUPPORT_GENERATED_REGEX
        public static Regex MinifyJsonPatternRegex => _minifyJsonPatternRegex ??= new Regex(MinifyJsonPattern, Options);
        /// <summary>
        /// Cache field of <see cref="MinifyJsonPatternRegex"/>.
        /// </summary>
        private static Regex? _minifyJsonPatternRegex;
#elif SUPPORT_GENERATED_REGEX_PROPERTY
        [GeneratedRegex(MinifyJsonPattern, Options)]
        public static partial Regex MinifyJsonPatternRegex { get; }
#else
        public static Regex MinifyJsonPatternRegex => GetMinifyJsonPatternRegex();
        /// <summary>
        /// Underlying method of <see cref="MinifyJsonPatternRegex"/>.
        /// </summary>
        /// <returns><see cref="Regex"/> instance that performs substitutions to minify JSON.</returns>
        [GeneratedRegex(MinifyJsonPattern, Options)]
        private static partial Regex GetMinifyJsonPatternRegex();
#endif  // !SUPPORT_GENERATED_REGEX
    }
}
