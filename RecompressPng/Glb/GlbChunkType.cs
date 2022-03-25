namespace RecompressPng.Glb
{
    /// <summary>
    /// Glb chunk type values.
    /// </summary>
    public enum GlbChunkType : uint
    {
        /// <summary>
        /// Represents this chunk is json.
        /// </summary>
        Json = 0x4E4F534A,
        /// <summary>
        /// Represents this chunk is binary.
        /// </summary>
        Binary = 0x004E4942
    }
}
