namespace RecompressPng.Icon
{
    /// <summary>
    /// Icon image header.
    /// </summary>
    public class IconDirectoryEntry
    {
        /// <summary>
        /// Size of this class.
        /// </summary>
        public const int Size = 16;

        /// <summary>
        /// Width of the image.
        /// For 256 pixels, the value is zero.
        /// </summary>
        public byte Width { get; set; }
        /// <summary>
        /// Height of the image.
        /// For 256 pixels, the value is zero.
        /// </summary>
        public byte Height { get; set; }
        /// <summary>
        /// Number of colors.
        /// </summary>
        public byte ColorCount { get; set; }
        /// <summary>
        /// Reserved, always zero.
        /// </summary>
        public byte Reserved { get; }
        /// <summary>
        /// Number of color planes for ICO format.
        /// X-coordinate of the hot-spot for CUR format.
        /// </summary>
        public ushort ColorPlaneOrHotSpotX { get; set; }
        /// <summary>
        /// Number of bits per pixel for ICO format.
        /// Y-coordinate of the hot-spot for CUR format.
        /// </summary>
        public ushort BitPerPixelOrHotSpotY { get; set; }
        /// <summary>
        /// Image data size.
        /// </summary>
        public uint ImageDataSize { get; set; }
        /// <summary>
        /// File offset to the corresponding bitmap data.
        /// </summary>
        public uint ImageDataOffset { get; set; }

        /// <summary>
        /// Create instance with specified arguments and set <see cref="Reserved"/> to 0.
        /// </summary>
        /// <param name="width">Width of the image.</param>
        /// <param name="height">Height of the image.</param>
        /// <param name="colorCount">Number of colors.</param>
        /// <param name="colorPlaneOrHotSpotX">Number of color planes for ICO format. X-coordinate of the hot-spot for CUR format.</param>
        /// <param name="bitPerPixelOrHotSpotY">Number of bits per pixel for ICO format. Y-coordinate of the hot-spot for CUR format.</param>
        /// <param name="imageDataSize">Image data size.</param>
        /// <param name="imageDataOffset">File offset to the corresponding bitmap data.</param>
        public IconDirectoryEntry(byte width, byte height, byte colorCount, ushort colorPlaneOrHotSpotX, ushort bitPerPixelOrHotSpotY, uint imageDataSize, uint imageDataOffset)
            : this(width, height, colorCount, 0, colorPlaneOrHotSpotX, bitPerPixelOrHotSpotY, imageDataSize, imageDataOffset)
        {
        }

        /// <summary>
        /// Create instance with specified arguments and set <see cref="Reserved"/> to 0.
        /// </summary>
        /// <param name="width">Width of the image.</param>
        /// <param name="height">Height of the image.</param>
        /// <param name="colorCount">Number of colors.</param>
        /// <param name="reserved">Reserved, always zero.</param>
        /// <param name="colorPlaneOrHotSpotX">Number of color planes for ICO format. X-coordinate of the hot-spot for CUR format.</param>
        /// <param name="bitPerPixelOrHotSpotY">Number of bits per pixel for ICO format. Y-coordinate of the hot-spot for CUR format.</param>
        /// <param name="imageDataSize">Image data size.</param>
        /// <param name="imageDataOffset">File offset to the corresponding bitmap data.</param>
        internal IconDirectoryEntry(byte width, byte height, byte colorCount, byte reserved, ushort colorPlaneOrHotSpotX, ushort bitPerPixelOrHotSpotY, uint imageDataSize, uint imageDataOffset)
        {
            Width = width;
            Height = height;
            ColorCount = colorCount;
            Reserved = reserved;
            ColorPlaneOrHotSpotX = colorPlaneOrHotSpotX;
            BitPerPixelOrHotSpotY = bitPerPixelOrHotSpotY;
            ImageDataSize = imageDataSize;
            ImageDataOffset = imageDataOffset;
        }
    }
}
