namespace RecompressPng
{
    /// <summary>
    /// Pair of image index and image name.
    /// </summary>
    public struct ImageIndexNamePair
    {
        /// <summary>
        /// Index of image.
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// Name of image.
        /// </summary>
        public string ImageName { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="index">Index of image.</param>
        /// <param name="imageName">Name of image.</param>
        public ImageIndexNamePair(int index, string imageName)
        {
            Index = index;
            ImageName = imageName;
        }

        /// <summary>
        /// Store <see cref="Index"/> and <see cref="ImageName"/> to specified variable.
        /// </summary>
        /// <param name="index">A variable where <see cref="Index"/> is stored.</param>
        /// <param name="imageName">A variable where <see cref="ImageName"/> is stored.</param>
        public void Deconstruct(out int index, out string imageName)
        {
            index = Index;
            imageName = ImageName;
        }
    }
}
