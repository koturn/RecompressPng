using System.IO;
using System.Text;


namespace RecompressPng.Glb
{
    /// <summary>
    /// Chunk of GLB.
    /// </summary>
    /// <remarks>
    /// Primary ctor: Initialize all properties.
    /// </remarks>
    /// <param name="length">Length of this chunk.</param>
    /// <param name="chunkType">Chunk type value.</param>
    /// <param name="data">Chunk data.</param>
    public class GlbChunk(int length, GlbChunkType chunkType, byte[] data)
    {
        /// <summary>
        /// Length of this chunk.
        /// </summary>
        public int Length { get; set; } = length;
        /// <summary>
        /// Chunk type value.
        /// </summary>
        public GlbChunkType ChunkType { get; set; } = chunkType;
        /// <summary>
        /// Chunk data.
        /// </summary>
        public byte[]? Data { get; set; } = data;

        /// <summary>
        /// Write all properties to specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Destination stream</param>
        public void WriteTo(Stream stream)
        {
            using var bw = new BinaryWriter(stream, Encoding.Default, true);
            bw.Write(Length);
            bw.Write((uint)ChunkType);
            if (Data != null)
            {
                bw.Write(Data);
            }
        }
    }
}
