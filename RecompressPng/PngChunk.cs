using System;
using System.IO;
using System.Text;


namespace RecompressPng
{
    /// <summary>
    /// A class that represents a PNG chunk.
    /// </summary>
    public class PngChunk
    {
        /// <summary>
        /// Chunk type string.
        /// </summary>
        public string Type { get; }
        /// <summary>
        /// Chunk data.
        /// </summary>
        public byte[] Data { get; }
        /// <summary>
        /// CRC-32 value of this chunk.
        /// </summary>
        public uint Crc32 { get; private set; }

        /// <summary>
        /// Initialize all properties with specified values.
        /// </summary>
        /// <param name="type">Chunk type string</param>
        /// <param name="data">Chunk data.</param>
        /// <param name="crc32">CRC-32 value of this chunk.</param>
        /// <param name="verifyCrc32">If true, check <paramref name="crc32"/> is valid or not.</param>
        /// <exception cref="InvalidDataException">Throw when <paramref name="verifyCrc32"/> is true and <paramref name="crc32"/> is invalid.</exception>
        public PngChunk(string type, byte[] data, uint crc32, bool verifyCrc32 = false)
        {
            if (verifyCrc32)
            {
                VerifyCrc32(type, data, crc32);
            }

            Type = type;
            Data = data;
            Crc32 = crc32;
        }

        /// <summary>
        /// <para>Initialize <see cref="Type"/> and <see cref="Data"/> with specified value.</para>
        /// <para><see cref="Crc32"/> is initialized with comnputed CRC-32 value from <paramref name="type"/> and <see cref="data"/>.</para>
        /// </summary>
        /// <param name="type">Chunk type string</param>
        /// <param name="data">Chunk data.</param>
        public PngChunk(string type, byte[] data)
        {
            Type = type;
            Data = data;
            Crc32 = ComputeCrc32(type, data);
        }

        /// <summary>
        /// Update <see cref="Crc32"/>.
        /// </summary>
        public void UpdateCrc32()
        {
            Crc32 = ComputeCrc32(Type, Data);
        }

        /// <summary>
        /// Write this chunk data to specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="s">Destination <see cref="Stream"/></param>
        public void WriteTo(Stream s)
        {
            var data = Data;

            // Length
            WriteAsBigEndian(s, data.Length);

            // Chunk Type
            Span<byte> buf = stackalloc byte[4];
            Encoding.ASCII.GetBytes(Type, buf);
            s.Write(buf);

            // Data
            s.Write(data);

            // CRC-32
            WriteAsBigEndian(s, Crc32);
        }

        /// <summary>
        /// Read a chunk from specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="s">Source <see cref="Stream"/>.</param>
        /// <param name="verifyCrc32">If true, check CRC-32 value which is read is valid or not.</param>
        /// <returns>New instance of <see cref="PngChunk"/>.</returns>
        public static PngChunk ReadOneChunk(Stream s, bool verifyCrc32 = false)
        {
            Span<byte> buf = stackalloc byte[4];

            if (s.Read(buf) < buf.Length)
            {
                throw new InvalidDataException("Failed to read data length.");
            }
            var length = (buf[0] << 24) | (buf[1] << 16) | (buf[2] << 8) | buf[3];

            if (s.Read(buf) < buf.Length)
            {
                throw new InvalidDataException("Failed to read chunk type.");
            }
            var type = Encoding.ASCII.GetString(buf);

            var data = new byte[length];
            if (s.Read(data) < data.Length)
            {
                throw new InvalidDataException($"Failed to data type at {type} chunk");
            }

            Span<byte> crc32Buf = stackalloc byte[4];
            if (s.Read(crc32Buf) < crc32Buf.Length)
            {
                throw new InvalidDataException($"Failed to read CRC-32 at {type} chunk.");
            }
            var crc32 = (uint)((crc32Buf[0] << 24) | (crc32Buf[1] << 16) | (crc32Buf[2] << 8) | crc32Buf[3]);

            if (verifyCrc32)
            {
                VerifyCrc32(buf, data, crc32, type);
            }

            return new PngChunk(type, data, crc32);
        }

        /// <summary>
        /// Write tEXt chunk.
        /// </summary>
        /// <param name="s">Destination <see cref="Stream"/> of PNG.</param>
        /// <param name="key">Key of tEXt chunk.</param>
        /// <param name="value">Value of tEXt chunk.</param>
        public static void WriteTextChunk(Stream s, string key, string value)
        {
            var keyData = Encoding.ASCII.GetBytes(key);
            var valueData = Encoding.ASCII.GetBytes(value);

            WriteAsBigEndian(s, keyData.Length + 1 + valueData.Length);

            var textChunkTypeData = Encoding.ASCII.GetBytes("tEXt");
            s.Write(textChunkTypeData);

            s.Write(keyData);
            s.WriteByte((byte)0);
            s.Write(valueData);

            var crc = Crc32Calculator.Update(textChunkTypeData);
            crc = Crc32Calculator.Update(keyData, crc);
            crc = Crc32Calculator.Update((byte)0, crc);
            crc = Crc32Calculator.Update(valueData, crc);

            WriteAsBigEndian(s, Crc32Calculator.Finalize(crc));
        }

        /// <summary>
        /// Write tIME chunk.
        /// </summary>
        /// <param name="s">Destination <see cref="Stream"/> of PNG.</param>
        /// <param name="dt"><see cref="DateTime"/> value for tIME chunk.</param>
        public static void WriteTimeChunk(Stream s, in DateTime dt)
        {
            var dtUtc = dt.ToUniversalTime();
            Span<byte> dtData = stackalloc byte[] {
                (byte)((dtUtc.Year & 0xff00) >> 8),
                (byte)(dtUtc.Year & 0xff),
                (byte)dtUtc.Month,
                (byte)dtUtc.Day,
                (byte)dtUtc.Hour,
                (byte)dtUtc.Minute,
                (byte)dtUtc.Second,
            };

            WriteAsBigEndian(s, dtData.Length);

            var textChunkTypeData = Encoding.ASCII.GetBytes("tIME");

            s.Write(textChunkTypeData);
            s.Write(dtData);

            var crc = Crc32Calculator.Update(textChunkTypeData);
            crc = Crc32Calculator.Update(dtData, crc);
            WriteAsBigEndian(s, Crc32Calculator.Finalize(crc));
        }

        /// <summary>
        /// Write <see cref="int"/> data as big endian.
        /// </summary>
        /// <param name="s">Destination <see cref="Stream"/>.</param>
        /// <param name="data"><see cref="int"/> data.</param>
        private static void WriteAsBigEndian(Stream s, int data)
        {
            WriteAsBigEndian(s, (uint)data);
        }

        /// <summary>
        /// Write <see cref="uint"/> data as big endian.
        /// </summary>
        /// <param name="s">Destination <see cref="Stream"/>.</param>
        /// <param name="data"><see cref="uint"/> data.</param>
        private static void WriteAsBigEndian(Stream s, uint data)
        {
            Span<byte> buf = stackalloc byte[]
            {
                (byte)(data >> 24),
                (byte)(data >> 16),
                (byte)(data >> 8),
                (byte)data
            };
            s.Write(buf);
        }

        /// <summary>
        /// Check specified CRC-32 value is valid or not.
        /// </summary>
        /// <param name="type">Chunk type string.</param>
        /// <param name="data">Chunk data.</param>
        /// <param name="crc32">CRC-32 value.</param>
        private static void VerifyCrc32(string type, Span<byte> data, uint crc32)
        {
            VerifyCrc32(Encoding.ASCII.GetBytes(type), data, crc32, type);
        }

        /// <summary>
        /// Check specified CRC-32 value is valid or not.
        /// </summary>
        /// <param name="typeData">ASCII byte data of Chunk type string.</param>
        /// <param name="data">Chunk data.</param>
        /// <param name="crc32">CRC-32 value.</param>
        /// <param name="type">Chunk type string.</param>
        private static void VerifyCrc32(Span<byte> typeData, Span<byte> data, uint crc32, string type)
        {
            var expectedCrc32 = ComputeCrc32(typeData, data);
            if (expectedCrc32 != crc32)
            {
                throw new InvalidDataException($"Invalid CRC-32 is detected at {type} chunk. Actual = {crc32:X8}, Expected = 0x{expectedCrc32:X8}");
            }
        }

        /// <summary>
        /// Compute CRC-32 value with specified chunk type and chunk data.
        /// </summary>
        /// <param name="type">Chunk type string.</param>
        /// <param name="data">Chunk data.</param>
        /// <returns>Computed CRC-32 value.</returns>
        private static uint ComputeCrc32(string type, Span<byte> data)
        {
            return ComputeCrc32(Encoding.ASCII.GetBytes(type), data);
        }

        /// <summary>
        /// Compute CRC-32 value with specified chunk type and chunk data.
        /// </summary>
        /// <param name="typeData">ASCII byte data of Chunk type string.</param>
        /// <param name="data">Chunk data.</param>
        /// <returns>Computed CRC-32 value.</returns>
        private static uint ComputeCrc32(Span<byte> typeData, Span<byte> data)
        {
            return Crc32Calculator.Finalize(Crc32Calculator.Update(data, Crc32Calculator.Update(typeData)));
        }
    }
}
