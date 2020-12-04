using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ArgumentParserNetStd;


namespace RecompressPng
{
    /// <summary>
    /// PNG re-compression using "zopfli" algorithm.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Default capacity for <see cref="MemoryStream"/> to read zip archive entries.
        /// </summary>
        const int DefaultReadCapacitySize = 4 * 1024 * 1024;

        /// <summary>
        /// Setup DLL search path.
        /// </summary>
        static Program()
        {
            UnsafeNativeMethods.SetDllDirectory(
                Path.Combine(
                    Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                    Environment.Is64BitProcess ? "x64" : "x86"));
        }

        /// <summary>
        /// An entry point of this program.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Status code.</returns>
        private static int Main(string[] args)
        {
            var (target, pngOptions, nThreads, isOverwrite) = ParseCommadLineArguments(args);
            ShowCompressOptions(pngOptions);

            if (File.Exists(target))
            {
                if (IsZipFile(target))
                {
                    RecompressPngInZipArchive(target, null, pngOptions, nThreads, isOverwrite);
                }
                else
                {
                    Console.Error.WriteLine("Specified file is not zip archive");
                    return 1;
                }
            }
            else if (Directory.Exists(target))
            {
                RecompressPngInDirectory(target, null, pngOptions, nThreads, isOverwrite);
            }
            else
            {
                Console.Error.WriteLine("Specified file doesn't exist");
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Parse command line arguments and retrieve the result.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <returns>Parse result tuple.</returns>
        private static (string Target, ZopfliPNGOptions PngOptions, int NumberOfThreads, bool IsOverwrite) ParseCommadLineArguments(string[] args)
        {
            var ap = new ArgumentParser()
            {
                Description = "<<< PNG Re-compressor using zopflipng >>>"
            };
            ap.Add('i', "num-iteration", OptionType.RequiredArgument, "Number of iteration.", "NUM", 15);
            ap.Add('I', "num-iteration-large", OptionType.RequiredArgument, "Number of iterations on large images.", "NUM", 5);
            ap.Add('s', "strategies", OptionType.RequiredArgument,
                "Filter strategies to try\n"
                + ap.IndentString + ap.IndentString + "0: Give all scanlines PNG filter type 0\n"
                + ap.IndentString + ap.IndentString + "1: Give all scanlines PNG filter type 1\n"
                + ap.IndentString + ap.IndentString + "2: Give all scanlines PNG filter type 2\n"
                + ap.IndentString + ap.IndentString + "3: Give all scanlines PNG filter type 3\n"
                + ap.IndentString + ap.IndentString + "4: Give all scanlines PNG filter type 4\n"
                + ap.IndentString + ap.IndentString + "5: Minimum sum\n"
                + ap.IndentString + ap.IndentString + "6: Entropy\n"
                + ap.IndentString + ap.IndentString + "7: Predefined (keep from input, this likely overlaps another strategy)\n"
                + ap.IndentString + ap.IndentString + "8: Brute force (experimental)"
                , "[0|1|2|3|4|5|6|7|8],...");
            ap.Add('n', "num-thread", OptionType.RequiredArgument, "Number of threads for re-compressing. -1 means unlimited.", "N", -1);
            ap.Add("lossy-transparent", "Remove colors behind alpha channel 0. No visual difference, removes hidden information.");
            ap.Add("lossy-8bit", "Convert 16-bit per channel images to 8-bit per channel.");
            ap.Add("overwrite", "Overwrite original files.");
            ap.Add("no-auto-filter-strategy", "Automatically choose filter strategy using less good compression.");
            ap.Add("no-use-zopfli", "Use Zopfli deflate compression.");
            ap.AddHelp();

            ap.Parse(args);

            if (ap.Get<bool>('h'))
            {
                ap.ShowUsage();
                Environment.Exit(0);
            }

            var targets = ap.Arguments;
            if (targets.Count == 0)
            {
                Console.Error.WriteLine("Please specify one zip file or directory.");
                Environment.Exit(0);
            }
            else if (targets.Count > 1)
            {
                Console.Error.WriteLine("Target zip file or directory must be one.");
                Console.Error.WriteLine("Proccess first argument: " + targets[0]);
            }

            var zo = ZopfliPNGOptions.GetDefault();
            zo.NumIterations = ap.Get<int>('i');
            zo.NumIterationsLarge = ap.Get<int>('I');
            zo.LossyTransparent = ap.Get<bool>("lossy-transparent");
            zo.Lossy8bit = ap.Get<bool>("lossy-8bit");
            zo.AutoFilterStrategy = !ap.Get<bool>("no-auto-filter-strategy");
            zo.UseZopfli = !ap.Get<bool>("no-use-zopfli");

            if (ap.Exists('s'))
            {
                zo.FilterStrategies = ap.Get('s')
                    .Split(',')
                    .Select(token => (ZopfliPNGFilterStrategy)int.Parse(token))
                    .ToArray();
                zo.NumFilterStrategies = zo.FilterStrategies.Length;
            }

            return (targets[0], zo, ap.Get<int>('n'), ap.Get<bool>("overwrite"));
        }

        /// <summary>
        /// Output options for zopflipng to stdout.
        /// </summary>
        /// <param name="pngOptions">Options for zopflipng</param>
        private static void ShowCompressOptions(ZopfliPNGOptions pngOptions)
        {
            var strategies = pngOptions.FilterStrategies == null ? "" : string.Join(", ", pngOptions.FilterStrategies);
            var keepChunks = pngOptions.KeepChunks == null ? "" : string.Join(", ", pngOptions.KeepChunks);

            Console.WriteLine("- - - ZopfliPNG Parameters - - -");
            Console.WriteLine($"Lossy Transparent: {pngOptions.LossyTransparent}");
            Console.WriteLine($"Lossy 8bit: {pngOptions.Lossy8bit}");
            Console.WriteLine($"ZopfliPNG Filter Strategies: {strategies}");
            Console.WriteLine($"Auto Filter Strategy: {pngOptions.AutoFilterStrategy}");
            Console.WriteLine($"Keep Chunks: {keepChunks}");
            Console.WriteLine($"Use Zopfli: {pngOptions.UseZopfli}");
            Console.WriteLine($"Number of Iterations: {pngOptions.NumIterations}");
            Console.WriteLine($"Number of Iterations on Large Images: {pngOptions.NumIterationsLarge}");
            Console.WriteLine("- - -");
        }

        /// <summary>
        /// Re-compress all PNG files in zip archive using "zopfli" algorithm.
        /// </summary>
        /// <param name="srcZipFilePath">Source zip archive file.</param>
        /// <param name="dstZipFilePath">Destination zip archive file.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="nThreads">Number of threads for re-compressing.</param>
        /// <param name="isOverwrite">Overwrite PNG files in the zip archive file.</param>
        private static void RecompressPngInZipArchive(string srcZipFilePath, string dstZipFilePath, ZopfliPNGOptions pngOptions, int nThreads, bool isOverwrite = false)
        {
            if (dstZipFilePath == null)
            {
                dstZipFilePath = srcZipFilePath + ".zopfli";
            }

            if (!isOverwrite && File.Exists(dstZipFilePath))
            {
                File.Delete(dstZipFilePath);
            }

            int nProcPngFiles = 0;
            int nSameImages = 0;
            var totalSw = Stopwatch.StartNew();
            var srcFileSize = new FileInfo(srcZipFilePath).Length;

            using (var srcArchive = ZipFile.Open(srcZipFilePath, ZipArchiveMode.Update))
            using (var dstArchive = isOverwrite ? null : ZipFile.Open(dstZipFilePath, ZipArchiveMode.Update))
            {
                var srcLock = new object();
                var dstLock = isOverwrite ? null : new object();
                Parallel.ForEach(
                    srcArchive.Entries,
                    new ParallelOptions() { MaxDegreeOfParallelism = nThreads },
                    srcEntry =>
                    {
                        if (!srcEntry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!isOverwrite)
                            {
                                return;
                            }
                            using (var ms = new MemoryStream((int)srcEntry.Length))
                            {
                                lock (srcLock)
                                {
                                    using (var srcZs = srcEntry.Open())
                                    {
                                        srcZs.CopyTo(ms);
                                    }
                                }
                                lock (dstLock)
                                {
                                    var dstEntry = dstArchive.CreateEntry(srcEntry.FullName, CompressionLevel.Optimal);
                                    using (var dstZs = dstEntry.Open())
                                    {
                                        ms.CopyTo(dstZs);
                                    }
                                }
                            }
                            return;
                        }

                        var sw = Stopwatch.StartNew();

                        var threadId = Thread.CurrentThread.ManagedThreadId;
                        Console.WriteLine($"[{threadId}] Compress {srcEntry.FullName} ...");

                        byte[] data;
                        long dataLength;
                        using (var ms = new MemoryStream((int)srcEntry.Length))
                        {
                            lock (srcLock)
                            {
                                using (var srcZs = srcEntry.Open())
                                {
                                    srcZs.CopyTo(ms);
                                }
                            }
                            data = ms.GetBuffer();
                            dataLength = ms.Length;
                        }

                        // Take a long time
                        var compressedData = ZopfliPng.OptimizePng(
                            data,
                            dataLength,
                            pngOptions);

                        if (isOverwrite)
                        {
                            var lastWriteTime = srcEntry.LastWriteTime;
                            lock (srcLock)
                            {
                                var newSrcEntry = dstArchive.CreateEntry(srcEntry.FullName, CompressionLevel.Optimal);
                                srcEntry.Delete();
                                srcEntry = newSrcEntry;
                                using (var srcZs = srcEntry.Open())
                                {
                                    srcZs.Write(compressedData, 0, compressedData.Length);
                                }
                                // Keep original timestamp
                                srcEntry.LastWriteTime = lastWriteTime;
                                nProcPngFiles++;
                            }
                        }
                        else
                        {
                            lock (dstLock)
                            {
                                var dstEntry = dstArchive.CreateEntry(srcEntry.FullName, CompressionLevel.Optimal);
                                using (var dstZs = dstEntry.Open())
                                {
                                    dstZs.Write(compressedData, 0, compressedData.Length);
                                }
                                // Keep original timestamp
                                dstEntry.LastWriteTime = srcEntry.LastWriteTime;
                                nProcPngFiles++;
                            }
                        }

                        // The comparison of image data is considered to take a little longer.
                        // Therefore, atomically incremented nSameImages outside of the lock statement.
                        var isSameImage = CompareImage(data, dataLength, compressedData, compressedData.LongLength);
                        if (isSameImage)
                        {
                            Interlocked.Increment(ref nSameImages);
                        }

                        var verifyResultMsg = isSameImage ? "same image" : "different image";
                        Console.WriteLine($"[{threadId}] Compress {srcEntry.FullName} done: {sw.ElapsedMilliseconds / 1000.0:F3} seconds, {ToMiB(dataLength):F3} MiB -> {ToMiB(compressedData.Length):F3} MiB ({verifyResultMsg}) (deflated {CalcDeflatedRate(dataLength, compressedData.Length) * 100.0:F2}%)");
                    });
            }

            var dstFileSize = new FileInfo(isOverwrite ? srcZipFilePath : dstZipFilePath).Length;

            Console.WriteLine("- - -");
            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} seconds, {ToMiB(srcFileSize):F3} MiB -> {ToMiB(dstFileSize):F3} MiB (deflated {CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0:F2}%)");
            if (nProcPngFiles == nSameImages)
            {
                Console.WriteLine("All the image data before and after re-compressing are the same.");
            }
            else
            {
                Console.WriteLine($"{nProcPngFiles - nSameImages} / {nProcPngFiles} PNG files are different image.");
            }

            if (!isOverwrite)
            {
                File.Replace(
                    dstZipFilePath,
                    srcZipFilePath,
                    Path.Combine(
                        Path.GetDirectoryName(srcZipFilePath),
                        Path.GetFileNameWithoutExtension(srcZipFilePath) + ".old.zip"));
            }
        }

        /// <summary>
        /// Re-compress all PNG files in directory using "zopfli" algorithm.
        /// </summary>
        /// <param name="srcDirPath">Source directory.</param>
        /// <param name="dstDirPath">Destination directory.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="nThreads">Number of threads for re-compressing.</param>
        /// <param name="isOverwrite">Overwrite PNG files in the directory.</param>
        private static void RecompressPngInDirectory(string srcDirPath, string dstDirPath, ZopfliPNGOptions pngOptions, int nThreads, bool isOverwrite = false)
        {
            if (dstDirPath == null)
            {
                dstDirPath = srcDirPath + ".zopfli";
            }

            var srcBaseDirFullPath = Path.GetFullPath(srcDirPath);
            var dstBaseDirFullPath = Path.GetFullPath(dstDirPath);

            var srcTotalFileSize = 0L;
            var dstTotalFileSize = 0L;

            int nProcPngFiles = 0;
            int nSameImages = 0;
            var totalSw = Stopwatch.StartNew();

            Parallel.ForEach(
                Directory.EnumerateFiles(srcDirPath, "*.png", SearchOption.AllDirectories),
                new ParallelOptions() { MaxDegreeOfParallelism = nThreads },
                srcFilePath =>
                {
                    var sw = Stopwatch.StartNew();

                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    var srcRelPath = ToRelativePath(srcFilePath, srcBaseDirFullPath);
                    Console.WriteLine($"[{threadId}] Compress {srcRelPath} ...");

                    var dstFilePath = isOverwrite ? srcFilePath : Path.Combine(
                        dstBaseDirFullPath,
                        new StringBuilder(Path.GetFullPath(srcFilePath))
                            .Replace(srcBaseDirFullPath + @"\", "", 0, srcBaseDirFullPath.Length + 1)
                            .ToString());

                    Directory.CreateDirectory(Path.GetDirectoryName(dstFilePath));

                    var data = File.ReadAllBytes(srcFilePath);
                    var originalTimestamp = new FileInfo(srcFilePath).LastWriteTime;

                    // Take a long time
                    var compressedData = ZopfliPng.OptimizePng(data, pngOptions);

                    File.WriteAllBytes(dstFilePath, compressedData);

                    // Keep original timestamp
                    new FileInfo(dstFilePath).LastWriteTime = originalTimestamp;

                    Interlocked.Add(ref srcTotalFileSize, data.Length);
                    Interlocked.Add(ref dstTotalFileSize, compressedData.Length);
                    Interlocked.Increment(ref nProcPngFiles);

                    var isSameImage = CompareImage(data, compressedData);
                    if (isSameImage)
                    {
                        Interlocked.Increment(ref nSameImages);
                    }

                    var verifyResultMsg = isSameImage ? "same image" : "different image";
                    Console.WriteLine($"[{threadId}] Compress {srcRelPath} done: {sw.ElapsedMilliseconds / 1000.0:F3} seconds, {ToMiB(data.Length):F3} MiB -> {ToMiB(compressedData.Length):F3} MiB ({verifyResultMsg}) (deflated {CalcDeflatedRate(data.Length, compressedData.Length) * 100.0:F2}%)");
                });

            Console.WriteLine("- - -");
            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} seconds, {ToMiB(srcTotalFileSize):F3} MiB -> {ToMiB(dstTotalFileSize):F3} MiB (deflated {CalcDeflatedRate(srcTotalFileSize, dstTotalFileSize) * 100.0:F2}%)");
            if (nProcPngFiles == nSameImages)
            {
                Console.WriteLine("All the image data before and after re-compressing are the same.");
            }
            else
            {
                Console.WriteLine($"{nProcPngFiles - nSameImages} / {nProcPngFiles} PNG files are different image.");
            }

            if (!isOverwrite)
            {
                ReplaceDirectory(
                    dstDirPath,
                    srcDirPath,
                    Path.Combine(Path.GetDirectoryName(srcDirPath), Path.GetFileNameWithoutExtension(srcDirPath) + ".old"));
            }
        }

        /// <summary>
        /// <para>Identify zip archive file or not.</para>
        /// <para>Just determine if the first two bytes are 'P' and 'K'.</para>
        /// </summary>
        /// <param name="zipFilePath">Target zip file path,</param>
        /// <returns>True if specified file is a zip archive file, otherwise false.</returns>
        private static bool IsZipFile(string zipFilePath)
        {
            var buffer = new byte[2];
            using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Read(buffer, 0, buffer.Length);
            }
            return buffer[0] == 'P' && buffer[1] == 'K';
        }

        /// <summary>
        /// Convert path to relative path.
        /// </summary>
        /// <param name="targetPath">Target path.</param>
        /// <param name="basePath">Base path.</param>
        /// <returns>Relative path of <paramref name="targetPath"/>.</returns>
        private static string ToRelativePath(string targetPath, string basePath)
        {
            return HttpUtility.UrlDecode(new Uri(Path.GetFullPath(basePath))
                .MakeRelativeUri(new Uri(Path.GetFullPath(targetPath))).ToString())
                .Replace('/', '\\');
        }

        /// <summary>
        /// Replace the directory and create backup directory.
        /// </summary>
        /// <param name="srcDirPath">Source directory path.</param>
        /// <param name="dstDirPath">Destination directory path.</param>
        /// <param name="backupDirPath">Backup directory path.</param>
        private static void ReplaceDirectory(string srcDirPath, string dstDirPath, string backupDirPath)
        {
            if (Directory.Exists(backupDirPath))
            {
                Directory.Delete(backupDirPath, true);
            }
            Directory.Move(dstDirPath, backupDirPath);
            Directory.Move(srcDirPath, dstDirPath);
        }

        /// <summary>
        /// Converts a number in bytes to a number in MiB.
        /// </summary>
        /// <param name="byteSize">A number in bytes.</param>
        /// <returns>A number in MiB.</returns>
        private static double ToMiB(long byteSize)
        {
            return byteSize / 1024.0 / 1024.0;
        }

        /// <summary>
        /// Calculate deflated rate.
        /// </summary>
        /// <param name="originalSize">Original size.</param>
        /// <param name="compressedSize">Compressed size.</param>
        /// <returns>Deflated rete.</returns>
        private static double CalcDeflatedRate(long originalSize, long compressedSize)
        {
            return 1.0 - (double)compressedSize / originalSize;
        }

        /// <summary>
        /// Compare and determine two image data is same or not.
        /// </summary>
        /// <param name="imgData1">First image data.</param>
        /// <param name="imgData2">Second image data.</param>
        /// <returns>True if two image data are same, otherwise false.</returns>
        private static bool CompareImage(byte[] imgData1, byte[] imgData2)
        {
            return CompareImage(
                CreateBitmapFromByteArray(imgData1),
                CreateBitmapFromByteArray(imgData2));
        }

        /// <summary>
        /// Compare and determine two image data is same or not.
        /// </summary>
        /// <param name="imgData1">First image data.</param>
        /// <param name="imgDataLength1">Byte length of <paramref name="imgData1"/>.</param>
        /// <param name="imgData2">Second image data.</param>
        /// <param name="imgDataLength2">Byte length of <paramref name="imgData2"/>.</param>
        /// <returns>True if two image data are same, otherwise false.</returns>
        private static bool CompareImage(byte[] imgData1, long imgDataLength1, byte[] imgData2, long imgDataLength2)
        {
            return CompareImage(
                CreateBitmapFromByteArray(imgData1, imgDataLength1),
                CreateBitmapFromByteArray(imgData2, imgDataLength2));
        }

        /// <summary>
        /// Compare and determine two image data is same or not.
        /// </summary>
        /// <param name="img1">First image data.</param>
        /// <param name="img2">Second image data.</param>
        /// <returns>True if two image data are same, otherwise false.</returns>
        private static bool CompareImage(Bitmap img1, Bitmap img2)
        {
            if (img1.Width != img2.Width)
            {
                return false;
            }
            if (img1.Height != img2.Height)
            {
                return false;
            }
            if (img1.PixelFormat != img2.PixelFormat)
            {
                return false;
            }

            var bd1 = img1.LockBits(
                new Rectangle(0, 0, img1.Width, img1.Height),
                ImageLockMode.ReadWrite,
                img1.PixelFormat);
            var bd2 = img2.LockBits(
                new Rectangle(0, 0, img2.Width, img2.Height),
                ImageLockMode.ReadWrite,
                img2.PixelFormat);

            if (bd1.Stride != bd2.Stride)
            {
                return false;
            }

            var isSameImageData = CompareMemory(bd1.Scan0, bd2.Scan0, bd1.Stride * bd1.Height);

            img2.UnlockBits(bd2);
            img1.UnlockBits(bd1);

            return isSameImageData;
        }

        /// <summary>
        /// Compare two byte data.
        /// </summary>
        /// <param name="data1">First byte data array.</param>
        /// <param name="data2">Second byte data array.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        private static bool CompareMemory(byte[] data1, byte[] data2)
        {
            if (data1.Length != data2.Length)
            {
                return false;
            }
            for (int i = 0; i < data1.Length; i++)
            {
                if (data1[i] != data2[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Compare two byte data.
        /// </summary>
        /// <param name="pData1">First byte data array.</param>
        /// <param name="pData2">Second byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        private static unsafe bool CompareMemory(IntPtr pData1, IntPtr pData2, long dataLength)
        {
            return CompareMemory((byte*)pData1, (byte*)pData2, dataLength);
        }

        /// <summary>
        /// Compare two byte data.
        /// </summary>
        /// <param name="pData1">First byte data array.</param>
        /// <param name="pData2">Second byte data array.</param>
        /// <param name="dataLength">Data length of <paramref name="pData1"/> and <paramref name="pData2"/>.</param>
        /// <returns>True if two byte data is same, otherwise false.</returns>
        private static unsafe bool CompareMemory(byte* pData1, byte* pData2, long dataLength)
        {
            for (long i = 0; i < dataLength; i++)
            {
                if (pData1[i] != pData2[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        private static unsafe Bitmap CreateBitmapFromByteArray(byte[] imgData)
        {
            using (var ms = new MemoryStream(imgData))
            {
                return (Bitmap)Image.FromStream(ms);
            }
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <param name="imgDataLength">Byte length of <paramref name="imgData"/>.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        private static unsafe Bitmap CreateBitmapFromByteArray(byte[] imgData, long imgDataLength)
        {
            fixed (byte* pImgData = imgData)
            {
                using (var ums = new UnmanagedMemoryStream(pImgData, imgDataLength))
                {
                    return (Bitmap)Image.FromStream(ums);
                }
            }
        }

        /// <summary>
        /// Native methods.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class UnsafeNativeMethods
        {
            /// <summary>
            /// Adds a directory to the search path used to locate DLLs for the application.
            /// </summary>
            /// <param name="path">Path to DLL directory.</param>
            /// <returns>True if success to set directory, otherwise false.</returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern bool SetDllDirectory(string path);
        }
    }
}
