using System.IO;
using System.Text;


namespace RecompressPng.Glb
{
    /// <summary>
    /// Header of GLB.
    /// </summary>
    public class GlbHeader
    {
        /// <summary>
        /// Magic number of GLB.
        /// </summary>
        public uint Magic { get; set; }
        /// <summary>
        /// Version number of GLB.
        /// </summary>
        public uint Version { get; set; }
        /// <summary>
        /// Size of GLB file.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="magic">Magic number of GLB.</param>
        /// <param name="version">Version number of GLB.</param>
        /// <param name="length">Size of GLB file.</param>
        public GlbHeader(uint magic, uint version, int length)
        {
            Magic = magic;
            Version = version;
            Length = length;
        }

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
