namespace RecompressPng.Icon
{
    /// <summary>
    /// Pair of <see cref="IconDirectoryEntry"/> and image data.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Create instance with specified icon image data header and image data.
    /// </remarks>
    /// <param name="directoryEntry">Icon image data header.</param>
    /// <param name="imageData">Icon image data.</param>
    public class IconImageEntry(IconDirectoryEntry directoryEntry, byte[] imageData)
    {
        /// <summary>
        /// Icon image data header.
        /// </summary>
        public IconDirectoryEntry DirectoryEntry { get; set; } = directoryEntry;
        /// <summary>
        /// Icon image data.
        /// </summary>
        public byte[] ImageData { get; set; } = imageData;
    }
}
