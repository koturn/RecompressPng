using System;
using System.Drawing;


namespace RecompressPng
{
    /// <summary>
    /// Result of image comparison methods, <see cref="BitmapUtil.CompareImage(byte[], ReadOnlySpan{byte})"/>
    /// and <see cref="BitmapUtil.CompareImage(Bitmap, Bitmap)"/>
    /// and <see cref="BitmapUtil.CompareImage(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>.
    /// </summary>
    public struct CompareResult
    {
        /// <summary>
        /// Type of comparison result.
        /// </summary>
        public CompareResultType Type { get; set; }
        /// <summary>
        /// Optional message of comparison.
        /// </summary>
        public string? OptionalMessage { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="type">Type of comparison result.</param>
        public CompareResult(CompareResultType type)
            : this(type, null)
        {
        }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="type">Type of comparison result.</param>
        /// <param name="optionalMessage">Optional message of comparison.</param>
        public CompareResult(CompareResultType type, string? optionalMessage)
        {
            Type = type;
            OptionalMessage = optionalMessage;
        }

        /// <summary>
        /// Return message of result.
        /// </summary>
        /// <returns>Message of result</returns>
        public override string ToString()
        {
            var message = GetMassage(Type);
            return string.IsNullOrEmpty(OptionalMessage) ? message : (message + $" ({OptionalMessage})");
        }

        /// <summary>
        /// Get a message according to the value of <see cref="CompareResultType"/>.
        /// </summary>
        /// <param name="type">Comparison result type.</param>
        /// <returns>A message according to the <paramref name="type"/>.</returns>
        public static string GetMassage(CompareResultType type)
        {
            return type switch
            {
                CompareResultType.Same => "same image",
                CompareResultType.DifferentWidth => "different width",
                CompareResultType.DifferentHeight => "different height",
                CompareResultType.SameButDifferentPixelFormat => "different pixel format",
                CompareResultType.DifferentStride => "different stride",
                CompareResultType.DifferentImageData => "different image data",
                _ => "unknown result",
            };
        }
    }

    /// <summary>
    /// Result type of image comparison.
    /// </summary>
    public enum CompareResultType
    {
        /// <summary>
        /// Two images are same
        /// </summary>
        Same,
        /// <summary>
        /// Two images have different widths.
        /// </summary>
        DifferentWidth,
        /// <summary>
        /// Two images have different heights.
        /// </summary>
        DifferentHeight,
        /// <summary>
        /// Two images have different pixel formats.
        /// </summary>
        SameButDifferentPixelFormat,
        /// <summary>
        /// Two images have different strides.
        /// </summary>
        DifferentStride,
        /// <summary>
        /// data of the two images are different.
        /// </summary>
        DifferentImageData
    };
}
