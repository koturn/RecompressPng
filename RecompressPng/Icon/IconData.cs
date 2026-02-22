using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace RecompressPng.Icon
{
    /// <summary>
    /// Icon file data.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Create instance with specified resource type.
    /// </remarks>
    /// <param name="resourceType">Resource type.</param>
    public class IconData(IconResourceType resourceType)
    {
        /// <summary>
        /// Icon file header.
        /// </summary>
        private IconDirectory _iconDirectory = new(resourceType, 0);
        /// <summary>
        /// Icon image entry list.
        /// </summary>
        public List<IconImageEntry> IconImageEntryList { get; } = [];


        /// <summary>
        /// Create instance as ICO file data.
        /// </summary>
        public IconData()
            : this(IconResourceType.Icon)
        {
        }


        /// <summary>
        /// Save icon data to the specified file.
        /// </summary>
        /// <param name="filePath">Path to the icon file to save.</param>
        public void Save(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920);
            Save(fs);
        }

        /// <summary>
        /// Write icon file data to the specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream"><see cref="Stream"/> to write icon file data.</param>
        public void Save(Stream stream)
        {
            UpdateInfo();

            var basePosition = stream.Position;
            using var bw = new BinaryWriter(stream);

            bw.Write((ushort)0);
            bw.Write((ushort)_iconDirectory.ResourceType);
            bw.Write(_iconDirectory.ResourceCount);

            foreach (var iconImageEntry in IconImageEntryList)
            {
                var dirEntry = iconImageEntry.DirectoryEntry;
                bw.Write(dirEntry.Width);
                bw.Write(dirEntry.Height);
                bw.Write(dirEntry.ColorCount);
                bw.Write(dirEntry.Reserved);
                bw.Write(dirEntry.ColorPlaneOrHotSpotX);
                bw.Write(dirEntry.BitPerPixelOrHotSpotY);
                bw.Write(dirEntry.ImageDataSize);
                bw.Write(dirEntry.ImageDataOffset);
            }

            foreach (var iconImageEntry in IconImageEntryList)
            {
                var dirEntry = iconImageEntry.DirectoryEntry;
                stream.Position = basePosition + dirEntry.ImageDataOffset;
                stream.Write(iconImageEntry.ImageData, 0, iconImageEntry.ImageData.Length);
            }
        }

        /// <summary>
        /// Update headers.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the number of images exceeds 65535.</exception>
        public void UpdateInfo()
        {
            if (IconImageEntryList.Count > ushort.MaxValue)
            {
                throw new InvalidOperationException($"Number of entries in icon data exceeds {ushort.MaxValue}.");
            }
            _iconDirectory.ResourceCount = (ushort)IconImageEntryList.Count;

            var offset = (uint)(IconDirectory.Size + IconDirectoryEntry.Size * IconImageEntryList.Count);
            foreach (var iconImageEntry in IconImageEntryList)
            {
                var imageDataSize = (uint)iconImageEntry.ImageData.LongLength;
                var dirEntry = iconImageEntry.DirectoryEntry;
                dirEntry.ImageDataSize = imageDataSize;
                dirEntry.ImageDataOffset = offset;
                offset += imageDataSize;
            }
        }

        /// <summary>
        /// Load icon data from specified file.
        /// </summary>
        /// <param name="filePath">Path to the icon file.</param>
        /// <returns>Loaded icon data.</returns>
        public static IconData Load(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920);
            return Load(fs);
        }

        /// <summary>
        /// Load icon data from specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">A <see cref="Stream"/> of the icon file.</param>
        /// <returns>Loaded icon data.</returns>
        public static IconData Load(Stream stream)
        {
            var basePosition = stream.Position;
            using var br = new BinaryReader(stream, Encoding.ASCII, true);

            var iconData = new IconData()
            {
                _iconDirectory = new IconDirectory(
                    br.ReadUInt16(),
                    (IconResourceType)br.ReadUInt16(),
                    br.ReadUInt16())
            };

            var resourceCount = iconData._iconDirectory.ResourceCount;
            for (var i = 0; i < resourceCount; i++)
            {
                var iconDirEntry = new IconDirectoryEntry(
                    br.ReadByte(),
                    br.ReadByte(),
                    br.ReadByte(),
                    br.ReadByte(),
                    br.ReadUInt16(),
                    br.ReadUInt16(),
                    br.ReadUInt32(),
                    br.ReadUInt32());

                var imageData = new byte[iconDirEntry.ImageDataSize];
                var current = stream.Position;
                stream.Position = basePosition + iconDirEntry.ImageDataOffset;
#if NET7_0_OR_GREATER
                stream.ReadExactly(imageData);
#else
                var readCount = stream.Read(imageData, 0, imageData.Length);
                if (readCount < imageData.Length)
                {
                    throw new EndOfStreamException("Reached EOF while reading icon image data.");
                }
#endif  // NET7_0_OR_GREATER
                stream.Position = current;

                iconData.IconImageEntryList.Add(new IconImageEntry(iconDirEntry, imageData));
            }

            return iconData;
        }
    }
}
