namespace RecompressPng
{
    /// <summary>
    /// Pair of image index and image name.
    /// </summary>
    /// <remarks>
    /// Primary ctpr: Initialize all properties.
    /// </remarks>
    /// <param name="index">Index of image.</param>
    /// <param name="imageName">Name of image.</param>
    public struct ImageIndexNamePair(int index, string imageName)
    {
        /// <summary>
        /// Index of image.
        /// </summary>
        public int Index { get; set; } = index;
        /// <summary>
        /// Name of image.
        /// </summary>
        public string ImageName { get; set; } = imageName;

        /// <summary>
        /// Store <see cref="Index"/> and <see cref="ImageName"/> to specified variable.
        /// </summary>
        /// <param name="index">A variable where <see cref="Index"/> is stored.</param>
        /// <param name="imageName">A variable where <see cref="ImageName"/> is stored.</param>
        public readonly void Deconstruct(out int index, out string imageName)
        {
            index = Index;
            imageName = ImageName;
        }
    }
}
