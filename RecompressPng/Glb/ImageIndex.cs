namespace RecompressPng.Glb
{
    /// <summary>
    /// Element of an array of "images" node.
    /// </summary>
    public struct ImageIndex
    {
        /// <summary>
        /// Index of buffer view.
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// Name of an image.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// MIME type string.
        /// </summary>
        public string MimeType { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="index">Index of buffer view.</param>
        /// <param name="name">Name of an image.</param>
        /// <param name="mimeType">MIME type string.</param>
        public ImageIndex(int index, string name, string mimeType)
        {
            Index = index;
            Name = name;
            MimeType = mimeType;
        }
    }
}
