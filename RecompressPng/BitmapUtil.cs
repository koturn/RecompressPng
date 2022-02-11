using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;


namespace RecompressPng
{
    /// <summary>
    /// Utility class which provides methods about <see cref="Bitmap"/>.
    /// </summary>
    public static class BitmapUtil
    {
        /// <summary>
        /// Memory comparator.
        /// </summary>
        private static MemoryComparator _memoryComparator;


        /// <summary>
        /// Initialize <see cref="_memoryComparator"/>.
        /// </summary>
        static BitmapUtil()
        {
            _memoryComparator = new MemoryComparator();
        }


        /// <summary>
        /// Compare and determine two image data is same or not.
        /// </summary>
        /// <param name="imgData1">First image data.</param>
        /// <param name="imgData2">Second image data.</param>
        /// <returns>True if two image data are same, otherwise false.</returns>
        public static CompareResult CompareImage(byte[] imgData1, ReadOnlySpan<byte> imgData2)
        {
            // The only way the two PNG data will be the same is
            // if they are recompressed with the same parameters.
            if (_memoryComparator.CompareMemory(imgData1, imgData2))
            {
                return new CompareResult(CompareResultType.Same);
            }

            using var bmp1 = CreateBitmap(imgData1);
            using var bmp2 = CreateBitmap(imgData2);

            return CompareImage(bmp1, bmp2);
        }

        /// <summary>
        /// Compare and determine two image data is same or not.
        /// </summary>
        /// <param name="imgData1">First image data.</param>
        /// <param name="imgData2">Second image data.</param>
        /// <returns>True if two image data are same, otherwise false.</returns>
        public static CompareResult CompareImage(ReadOnlySpan<byte> imgData1, ReadOnlySpan<byte> imgData2)
        {
            // The only way the two PNG data will be the same is
            // if they are recompressed with the same parameters.
            if (_memoryComparator.CompareMemory(imgData1, imgData2))
            {
                return new CompareResult(CompareResultType.Same);
            }

            using var bmp1 = CreateBitmap(imgData1);
            using var bmp2 = CreateBitmap(imgData2);

            return CompareImage(bmp1, bmp2);
        }

        /// <summary>
        /// Compare and determine two image data is same or not.
        /// </summary>
        /// <param name="img1">First image data.</param>
        /// <param name="img2">Second image data.</param>
        /// <returns>True if two image data are same, otherwise false.</returns>
        public static CompareResult CompareImage(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width)
            {
                return new CompareResult(CompareResultType.DifferentWidth, $"{img1.Width} -> {img2.Width}");
            }
            if (img1.Height != img2.Height)
            {
                return new CompareResult(CompareResultType.DifferentHeight, $"{img1.Height} -> {img2.Height}");
            }
            if (img1.PixelFormat != img2.PixelFormat)
            {
                return CompareImageByPixel(img1, img2)
                    ? new CompareResult(CompareResultType.SameButDifferentPixelFormat, $"{img1.PixelFormat} -> {img2.PixelFormat}")
                    : new CompareResult(CompareResultType.DifferentImageData);
            }
            if ((img1.PixelFormat & PixelFormat.Indexed) != 0)
            {
                return new CompareResult(CompareImageByPixel(img1, img2) ? CompareResultType.Same : CompareResultType.DifferentImageData);
            }

            var bd1 = img1.LockBits(
                new Rectangle(0, 0, img1.Width, img1.Height),
                ImageLockMode.ReadOnly,
                img1.PixelFormat);
            var bd2 = img2.LockBits(
                new Rectangle(0, 0, img2.Width, img2.Height),
                ImageLockMode.ReadOnly,
                img2.PixelFormat);

            if (bd1.Stride != bd2.Stride)
            {
                img2.UnlockBits(bd2);
                img1.UnlockBits(bd1);
                return new CompareResult(CompareResultType.DifferentStride, $"{bd1.Stride} -> {bd2.Stride}");
            }

            var isSameImageData = _memoryComparator.CompareMemory(bd1.Scan0, bd2.Scan0, bd1.Height * bd1.Stride);

            img2.UnlockBits(bd2);
            img1.UnlockBits(bd1);

            return new CompareResult(isSameImageData ? CompareResultType.Same : CompareResultType.DifferentImageData);
        }

        /// <summary>
        /// Compare the two images pixel by pixel.
        /// </summary>
        /// <param name="img1">First image data.</param>
        /// <param name="img2">Second image data.</param>
        /// <returns><c>true</c> if two images are same, otherwise <c>false</c>.</returns>
        public static bool CompareImageByPixel(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width)
            {
                return false;
            }
            if (img1.Height != img2.Height)
            {
                return false;
            }

            var height = img1.Height;
            var width = img1.Width;
            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    if (img1.GetPixel(j, i) != img2.GetPixel(j, i))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        public static Bitmap CreateBitmap(byte[] imgData)
        {
            return CreateBitmap(imgData, imgData.LongLength);
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <param name="imgDataLength">Byte length of <paramref name="imgData"/>.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        public static Bitmap CreateBitmap(byte[] imgData, long imgDataLength)
        {
            using var ms = new MemoryStream(imgData, 0, (int)imgDataLength, false, false);
            return (Bitmap)Image.FromStream(ms);
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        public static unsafe Bitmap CreateBitmap(ReadOnlySpan<byte> imgData)
        {
            fixed (byte* p = imgData)
            {
                using var ms = new UnmanagedMemoryStream(p, imgData.Length);
                return (Bitmap)Image.FromStream(ms);
            }
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        public static Bitmap CreateBitmap(SafeBuffer imgData)
        {
            return CreateBitmap(imgData, (long)imgData.ByteLength);
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <param name="imgDataLength">Byte length of <paramref name="imgData"/>.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        private static Bitmap CreateBitmap(SafeBuffer imgData, long imgDataLength)
        {
            using var ms = new UnmanagedMemoryStream(imgData, 0, (int)imgDataLength);
            return (Bitmap)Image.FromStream(ms);
        }
    }
}
