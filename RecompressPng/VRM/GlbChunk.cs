using System.IO;
using System.Text;


namespace RecompressPng.VRM
{
    /// <summary>
    /// Chunk of GLB.
    /// </summary>
    public class GlbChunk
    {
        /// <summary>
        /// Length of this chunk.
        /// </summary>
        public int Length { get; set; }
        /// <summary>
        /// Chunk type value.
        /// </summary>
        public int ChunkType { get; set; }
        /// <summary>
        /// Chunk data.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Initialize all properties.
        /// </summary>
        /// <param name="length">Length of this chunk.</param>
        /// <param name="chunkType">Chunk type value.</param>
        /// <param name="data">Chunk data.</param>
        public GlbChunk(int length, int chunkType, byte[] data)
        {
            Length = length;
            ChunkType = chunkType;
            Data = data;
        }

        /// <summary>
        /// Write all properties to specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">Destination stream</param>
        public void WriteTo(Stream stream)
        {
            using var bw = new BinaryWriter(stream, Encoding.Default, true);
            bw.Write(Length);
            bw.Write(ChunkType);
            bw.Write(Data);
        }
    }
}
