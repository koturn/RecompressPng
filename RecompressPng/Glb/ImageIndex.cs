namespace RecompressPng.Glb
{
    /// <summary>
    /// Element of an array of "images" node.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Initialize all properties.
    /// </remarks>
    /// <param name="index">Index of buffer view.</param>
    /// <param name="name">Name of an image.</param>
    /// <param name="mimeType">MIME type string.</param>
    public struct ImageIndex(int index, string name, string mimeType)
    {
        /// <summary>
        /// Index of buffer view.
        /// </summary>
        public int Index { get; set; } = index;
        /// <summary>
        /// Name of an image.
        /// </summary>
        public string Name { get; set; } = name;
        /// <summary>
        /// MIME type string.
        /// </summary>
        public string MimeType { get; set; } = mimeType;
    }
}
