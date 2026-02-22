namespace RecompressPng.Icon
{
    /// <summary>
    /// Icon file header.
    /// </summary>
    public class IconDirectory
    {
        /// <summary>
        /// Size of this class.
        /// </summary>
        public const int Size = 6;

        /// <summary>
        /// Reserved, always zero.
        /// </summary>
        private readonly ushort _reserved;
        /// <summary>
        /// Icon resource type.
        /// </summary>
        public IconResourceType ResourceType { get; set; }
        /// <summary>
        /// Number of images in the icon file.
        /// </summary>
        public ushort ResourceCount { get; set; }

        /// <summary>
        /// Create instance with specified arguments.
        /// </summary>
        /// <param name="resourceType">Icon resource type.</param>
        /// <param name="resourceCount">Number of images in the icon file.</param>
        public IconDirectory(IconResourceType resourceType, ushort resourceCount)
            : this(0, resourceType, resourceCount)
        {
        }

        /// <summary>
        /// Create instance with specified arguments.
        /// </summary>
        /// <param name="reserved">Reserved, always zero.</param>
        /// <param name="resourceType">Icon resource type.</param>
        /// <param name="resourceCount">Number of images in the icon file.</param>
        internal IconDirectory(ushort reserved, IconResourceType resourceType, ushort resourceCount)
        {
            _reserved = reserved;
            ResourceType = resourceType;
            ResourceCount = resourceCount;
        }
    }
}
