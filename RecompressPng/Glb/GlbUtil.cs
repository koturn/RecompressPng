using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Json;
using System.Linq;


namespace RecompressPng.Glb
{
    /// <summary>
    /// Utility class of VRM/GLB.
    /// </summary>
    public static class GlbUtil
    {
        /// <summary>
        /// Split header and chunks in GLB file.
        /// </summary>
        /// <param name="filePath">Path to GLB file.</param>
        /// <returns>Header of GLB and GLB chunks.</returns>
        public static (GlbHeader GlbHeader, List<GlbChunk> GlbChunks) ParseChunk(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ParseChunk(fs);
        }

        /// <summary>
        /// Parse GLB chunks.
        /// </summary>
        /// <param name="s">Stream of GLB data.</param>
        /// <returns>Header of GLB and GLB chunks.</returns>
        public static (GlbHeader GlbHeader, List<GlbChunk> GlbChunks) ParseChunk(Stream s)
        {
            using var br = new BinaryReader(s, Encoding.Default, true);
            var header = new GlbHeader(
                br.ReadUInt32(),
                br.ReadUInt32(),
                br.ReadInt32());
            var chunks = new List<GlbChunk>(2);
            for (int i = 0, capacity = chunks.Capacity; i < capacity; i++)
            {
                var length = br.ReadInt32();
                var chunkType = (GlbChunkType)br.ReadInt32();
                chunks.Add(new GlbChunk(length, chunkType, br.ReadBytes(length)));
            }
            return (header, chunks);
        }

        /// <summary>
        /// Parse json of glTF.
        /// </summary>
        /// <returns><see cref="ValueTuple"/> of glTF json, list of buffers and imageIndexes.</returns>
        public static (JsonValue GltfJson, List<byte[]> BinaryBuffers, List<ImageIndex> ImageIndexes) ParseGltf(List<GlbChunk> glbChunks)
        {
            if (glbChunks[0].ChunkType != GlbChunkType.Json)
            {
                throw new InvalidDataException($"Fitst GLB chunk type is not JSON. expected = 0x{(uint)GlbChunkType.Json:X8}; actual = 0x{glbChunks[0].ChunkType:X8}");
            }
            if (glbChunks[1].ChunkType != GlbChunkType.Binary)
            {
                throw new InvalidDataException($"Second GLB chunk type is not Binary. expected = 0x{(uint)GlbChunkType.Binary:X8}; actual = 0x{glbChunks[1].ChunkType:X8}");
            }

            var data0 = glbChunks[0].Data;
            if (data0 == null)
            {
                throw new ArgumentNullException(nameof(data0), "First GLB chunk data is null.");
            }
            var gltfJson = LoadJson(data0);
            var bufferViews = (JsonArray)gltfJson["bufferViews"];

            var data1 = glbChunks[1].Data;
            if (data1 == null)
            {
                throw new ArgumentNullException(nameof(data1), "Second GLB chunk data is null.");
            }
            var binaryBuffers = SplitBuffer(data1, bufferViews);

            var images = (JsonArray)gltfJson["images"];
            var imageIndexes = new List<ImageIndex>(images.Count);
            imageIndexes.AddRange(images.Select(image => new ImageIndex(image["bufferView"], image["name"], image["mimeType"])));

            return (gltfJson, binaryBuffers, imageIndexes);
        }


        /// <summary>
        /// Load json data.
        /// </summary>
        /// <param name="jsonData"><see cref="byte"/> array of json.</param>
        /// <returns>A <see cref="JsonValue"/> instance.</returns>
        private static JsonValue LoadJson(byte[] jsonData)
        {
            using var ms = new MemoryStream(jsonData);
            return JsonValue.Load(ms);
        }

        /// <summary>
        /// Split second chunk data of GLB.
        /// </summary>
        /// <param name="wholeBuffer">Data of second chunk.</param>
        /// <param name="bufferViews">JSON value of "bufferViews".</param>
        /// <returns>List of split buffers.</returns>
        private static List<byte[]> SplitBuffer(byte[] wholeBuffer, JsonArray bufferViews)
        {
            var binaryBuffers = new List<byte[]>(bufferViews.Count);
            binaryBuffers.AddRange(bufferViews.Select(bv =>
            {
                var buffer = new byte[bv["byteLength"]];
                Buffer.BlockCopy(wholeBuffer, bv["byteOffset"], buffer, 0, buffer.Length);
                return buffer;
            }));
            return binaryBuffers;
        }
    }
}
