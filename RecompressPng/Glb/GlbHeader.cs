using System.IO;
using System.Text;


namespace RecompressPng.Glb
{
    /// <summary>
    /// Header of GLB.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Initialize all properties.
    /// </remarks>
    /// <param name="magic">Magic number of GLB.</param>
    /// <param name="version">Version number of GLB.</param>
    /// <param name="length">Size of GLB file.</param>
    public class GlbHeader(uint magic, uint version, int length)
    {
        /// <summary>
        /// Magic number of GLB.
        /// </summary>
        public uint Magic { get; set; } = magic;
        /// <summary>
        /// Version number of GLB.
        /// </summary>
        public uint Version { get; set; } = version;
        /// <summary>
        /// Size of GLB file.
        /// </summary>
        public int Length { get; set; } = length;

        /// <summary>
        /// Write all properties to specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Destination stream</param>
        public void WriteTo(Stream stream)
        {
            using var bw = new BinaryWriter(stream, Encoding.Default, true);
            bw.Write(Magic);
            bw.Write(Version);
            bw.Write(Length);
        }
    }
}
