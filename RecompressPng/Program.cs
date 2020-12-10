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
        /// Default capacity of <see cref="MemoryStream"/> for reading <see cref="ZipArchiveEntry"/>.
        /// </summary>
        private const int DefaultMemoryStreamCapacity = 4 * 1024 * 1024;
        /// <summary>
        /// Date time format for logging.
        /// </summary>
        private const string LogDateTimeFormat = "yyyy-MM-dd hh:mm:ss.fff";

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
        /// Memory comparator.
        /// </summary>
        private static MemoryComparator _memoryComparator;

        /// <summary>
        /// An entry point of this program.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Status code.</returns>
        private static int Main(string[] args)
        {
            var (target, pngOptions, execOptions) = ParseCommadLineArguments(args);
            ShowParameters(pngOptions, execOptions);

            try
            {
                _memoryComparator = new MemoryComparator();
                if (File.Exists(target))
                {
                    if (IsZipFile(target))
                    {
                        if (execOptions.IsCountOnly)
                        {
                            CountPngInZipArchive(target);
                        }
                        else
                        {
                            RecompressPngInZipArchive(target, null, pngOptions, execOptions);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine("Specified file is not zip archive");
                        return 1;
                    }
                }
                else if (Directory.Exists(target))
                {
                    if (execOptions.IsCountOnly)
                    {
                        CountPngInDirectory(target);
                    }
                    else
                    {
                        RecompressPngInDirectory(target, null, pngOptions, execOptions);
                    }
                }
                else
                {
                    Console.Error.WriteLine("Specified file doesn't exist");
                    return 1;
                }
            }
            finally
            {
                _memoryComparator?.Dispose();
            }

            return 0;
        }

        /// <summary>
        /// Parse command line arguments and retrieve the result.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <returns>Parse result tuple.</returns>
        private static (string Target, ZopfliPNGOptions PngOptions, ExecuteOptions ExecOptions) ParseCommadLineArguments(string[] args)
        {
            var ap = new ArgumentParser()
            {
                Description = "<<< PNG Re-compressor using zopflipng >>>"
            };

            var indent1 = ap.IndentString;
            var indent2 = indent1 + indent1;
            var indent3 = indent2 + indent1;

            ap.Add('c', "count-only", "Show target PNG files and its size. And show the total count and size");
            ap.Add('d', "dry-run", "Don't save any files, just see the console output.");
            ap.AddHelp();
            ap.Add('i', "num-iteration", OptionType.RequiredArgument, "Number of iteration.", "NUM", ZopfliPNGOptions.DefaultNumIterations);
            ap.Add('I', "num-iteration-large", OptionType.RequiredArgument, "Number of iterations on large images.", "NUM", ZopfliPNGOptions.DefaultNumIterationsLarge);
            ap.Add('n', "num-thread", OptionType.RequiredArgument, "Number of threads for re-compressing. -1 means unlimited.", "N", ExecuteOptions.DefaultNumberOfThreads);
            ap.Add('r', "replace-force", "Do the replacement even if the size of the recompressed data is larger than the size of the original data.");
            ap.Add('s', "strategies", OptionType.RequiredArgument,
                "Filter strategies to try\n"
                + indent2 + "0: Give all scanlines PNG filter type 0\n"
                + indent2 + "1: Give all scanlines PNG filter type 1\n"
                + indent2 + "2: Give all scanlines PNG filter type 2\n"
                + indent2 + "3: Give all scanlines PNG filter type 3\n"
                + indent2 + "4: Give all scanlines PNG filter type 4\n"
                + indent2 + "5: Minimum sum\n"
                + indent2 + "6: Entropy\n"
                + indent2 + "7: Predefined (keep from input, this likely overlaps another strategy)\n"
                + indent2 + "8: Brute force (experimental)",
                "[0|1|2|3|4|5|6|7|8],...");
            ap.Add('v', "verbose", "Allow to output to stdout from zopflipng.dll.");
            ap.Add("keep-chunks", OptionType.RequiredArgument,
                "keep metadata chunks with these names that would normally be removed,\n"
                    + indent3 + "e.g. tEXt,zTXt,iTXt,gAMA, ... \n"
                    + indent2 + "Due to adding extra data, this increases the result size.\n"
                    + indent2 + "By default ZopfliPNG only keeps the following chunks because they are essential:\n"
                    + indent3 + "IHDR, PLTE, tRNS, IDAT and IEND.",
                "NAME,NAME...");
            ap.Add("lossy-transparent", "Remove colors behind alpha channel 0. No visual difference, removes hidden information.");
            ap.Add("lossy-8bit", "Convert 16-bit per channel images to 8-bit per channel.");
            ap.Add("no-auto-filter-strategy", "Automatically choose filter strategy using less good compression.");
            ap.Add("no-keep-timestamp", "Don't keep timestamp.");
            ap.Add("no-overwrite", "Don't overwrite PNG files and create images to new zip archive file or directory.");
            ap.Add("no-use-zopfli", "Use Zopfli deflate compression.");
            ap.Add("no-verify-image", "Don't compare two image data.");

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
                zo.FilterStrategies.AddRange(ap.Get('s')
                    .Split(',')
                    .Select(token => (ZopfliPNGFilterStrategy)int.Parse(token)));
            }
            if (ap.Exists("keep-chunks"))
            {
                zo.KeepChunks.AddRange(ap.Get("keep-chunks").Split(','));
            }

            return (
                targets[0],
                zo,
                new ExecuteOptions(
                    ap.Get<int>('n'),
                    !ap.Get<bool>("no-overwrite"),
                    ap.Get<bool>('r'),
                    !ap.Get<bool>("no-keep-timestamp"),
                    ap.Get<bool>('d'),
                    ap.Get<bool>('c'),
                    ap.Get<bool>('v'),
                    !ap.Get<bool>("no-verify-image")));
        }

        /// <summary>
        /// Output options for zopflipng to stdout.
        /// </summary>
        /// <param name="pngOptions">Options for zopflipng</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void ShowParameters(ZopfliPNGOptions pngOptions, ExecuteOptions execOptions)
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
            Console.WriteLine("- - - Execution Parameters - - -");
            Console.WriteLine($"Number of Threads: {execOptions.NumberOfThreads}");
            Console.WriteLine($"Overwrite: {execOptions.IsOverwrite}");
            Console.WriteLine($"Replace Force: {execOptions.IsReplaceForce}");
            Console.WriteLine($"Keep Timestamp: {execOptions.IsKeepTimestamp}");
            Console.WriteLine($"Dry Run: {execOptions.IsDryRun}");
            Console.WriteLine($"Verbose: {execOptions.Verbose}");
            Console.WriteLine($"Verify Image: {execOptions.IsVerifyImage}");
            Console.WriteLine("- - -");
        }

        /// <summary>
        /// Re-compress all PNG files in zip archive using "zopfli" algorithm.
        /// </summary>
        /// <param name="srcZipFilePath">Source zip archive file.</param>
        /// <param name="dstZipFilePath">Destination zip archive file.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPngInZipArchive(string srcZipFilePath, string dstZipFilePath, ZopfliPNGOptions pngOptions, ExecuteOptions execOptions)
        {
            dstZipFilePath = dstZipFilePath
                ?? Path.Combine(
                    Path.GetDirectoryName(srcZipFilePath),
                    Path.GetFileNameWithoutExtension(srcZipFilePath) + ".zopfli.zip");

            if (File.Exists(dstZipFilePath))
            {
                File.Delete(dstZipFilePath);
            }

            int nProcPngFiles = 0;
            int nSameImages = 0;
            var totalSw = Stopwatch.StartNew();
            var srcFileSize = new FileInfo(srcZipFilePath).Length;

            using (var srcArchive = ZipFile.OpenRead(srcZipFilePath))
            using (var dstArchive = execOptions.IsDryRun ? null : ZipFile.Open(dstZipFilePath, ZipArchiveMode.Create))
            {
                var srcLock = new object();
                var dstLock = execOptions.IsDryRun ? null : new object();
                Parallel.ForEach(
                    srcArchive.Entries,
                    new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                    srcEntry =>
                    {
                        if (!srcEntry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!execOptions.IsDryRun)
                            {
                                CreateEntryAndWriteData(
                                    dstArchive,
                                    srcEntry.FullName,
                                    ReadAllBytes(srcEntry, srcLock),
                                    dstLock,
                                    execOptions.IsKeepTimestamp ? (DateTimeOffset?)srcEntry.LastWriteTime : null);
                            }
                            return;
                        }

                        var sw = Stopwatch.StartNew();

                        var procIndex = Interlocked.Increment(ref nProcPngFiles);
                        Console.WriteLine($"{DateTime.Now.ToString(LogDateTimeFormat)}: [{procIndex}] Compress {srcEntry.FullName} ...");

                        var data = ReadAllBytes(srcEntry, srcLock);

                        // Take a long time
                        var compressedData = ZopfliPng.OptimizePng(
                            data,
                            pngOptions,
                            execOptions.Verbose);
                        if (compressedData == null)
                        {
                            Console.Error.WriteLine("Invalid PNG data");
                            return;
                        }

                        if (!execOptions.IsDryRun)
                        {
                            CreateEntryAndWriteData(
                                dstArchive,
                                srcEntry.FullName,
                                (compressedData.LongLength < data.LongLength || execOptions.IsReplaceForce) ? compressedData : data,
                                dstLock,
                                execOptions.IsKeepTimestamp ? (DateTimeOffset?)srcEntry.LastWriteTime : null);
                        }

                        // The comparison of image data is considered to take a little longer.
                        // Therefore, atomically incremented nSameImages outside of the lock statement.
                        var verifyResultMsg = "";
                        if (execOptions.IsVerifyImage)
                        {
                            var isSameImage = CompareImage(data, data.LongLength, compressedData, compressedData.LongLength);
                            if (isSameImage)
                            {
                                Interlocked.Increment(ref nSameImages);
                            }
                            verifyResultMsg = isSameImage ? " (same image)" : " (different image)";
                        }
                        Console.WriteLine($"{DateTime.Now.ToString(LogDateTimeFormat)}: [{procIndex}] Compress {srcEntry.FullName} done: {sw.ElapsedMilliseconds / 1000.0:F3} seconds, {ToMiB(data.LongLength):F3} MiB -> {ToMiB(compressedData.LongLength):F3} MiB{verifyResultMsg} (deflated {CalcDeflatedRate(data.LongLength, compressedData.LongLength) * 100.0:F2}%)");
                    });
            }

            if (execOptions.IsOverwrite)
            {
                File.Delete(srcZipFilePath);
                File.Move(dstZipFilePath, srcZipFilePath);
            }

            Console.WriteLine("- - -");
            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} seconds.");
            if (!execOptions.IsDryRun)
            {
                var dstFileSize = new FileInfo(execOptions.IsOverwrite ? srcZipFilePath : dstZipFilePath).Length;
                Console.WriteLine($"{ToMiB(srcFileSize):F3} MiB -> {ToMiB(dstFileSize):F3} MiB (deflated {CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0:F2}%)");
            }
            if (execOptions.IsVerifyImage)
            {
                if (nProcPngFiles == nSameImages)
                {
                    Console.WriteLine("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    Console.WriteLine($"{nProcPngFiles - nSameImages} / {nProcPngFiles} PNG files are different image.");
                }
            }
        }

        /// <summary>
        /// Re-compress all PNG files in directory using "zopfli" algorithm.
        /// </summary>
        /// <param name="srcDirPath">Source directory.</param>
        /// <param name="dstDirPath">Destination directory.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPngInDirectory(string srcDirPath, string dstDirPath, ZopfliPNGOptions pngOptions, ExecuteOptions execOptions)
        {
            dstDirPath = dstDirPath ?? (srcDirPath + ".zopfli");

            var srcBaseDirFullPath = Path.GetFullPath(srcDirPath);
            var dstBaseDirFullPath = Path.GetFullPath(dstDirPath);

            var srcTotalFileSize = 0L;
            var dstTotalFileSize = 0L;

            int nProcPngFiles = 0;
            int nSameImages = 0;
            var totalSw = Stopwatch.StartNew();

            Parallel.ForEach(
                Directory.EnumerateFiles(srcDirPath, "*.png", SearchOption.AllDirectories),
                new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                srcFilePath =>
                {
                    var sw = Stopwatch.StartNew();

                    var procIndex = Interlocked.Increment(ref nProcPngFiles);
                    var srcRelPath = ToRelativePath(srcFilePath, srcBaseDirFullPath);
                    Console.WriteLine($"{DateTime.Now.ToString(LogDateTimeFormat)}: [{procIndex}] Compress {srcRelPath} ...");

                    var dstFilePath = execOptions.IsOverwrite ? srcFilePath : Path.Combine(
                        dstBaseDirFullPath,
                        new StringBuilder(Path.GetFullPath(srcFilePath))
                            .Replace(srcBaseDirFullPath + @"\", "", 0, srcBaseDirFullPath.Length + 1)
                            .ToString());

                    var data = File.ReadAllBytes(srcFilePath);
                    var originalTimestamp = new FileInfo(srcFilePath).LastWriteTime;

                    // Take a long time
                    var compressedData = ZopfliPng.OptimizePng(data, pngOptions, execOptions.Verbose);

                    if (!execOptions.IsDryRun)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dstFilePath));
                        if (compressedData.Length <= data.Length || execOptions.IsReplaceForce)
                        {
                            File.WriteAllBytes(dstFilePath, compressedData);
                        }
                        else
                        {
                            File.Copy(srcFilePath, dstFilePath, true);
                        }

                        if (execOptions.IsKeepTimestamp)
                        {
                            new FileInfo(dstFilePath).LastWriteTime = originalTimestamp;
                        }
                    }

                    Interlocked.Add(ref srcTotalFileSize, data.Length);
                    Interlocked.Add(ref dstTotalFileSize, compressedData.Length);

                    var verifyResultMsg = "";
                    if (execOptions.IsVerifyImage)
                    {
                        var isSameImage = CompareImage(data, compressedData);
                        if (isSameImage)
                        {
                            Interlocked.Increment(ref nSameImages);
                            verifyResultMsg = " (same image)";
                        }
                        else
                        {
                            verifyResultMsg = " (different image)";
                        }
                    }
                    Console.WriteLine($"{DateTime.Now.ToString(LogDateTimeFormat)}: [{procIndex}] Compress {srcRelPath} done: {sw.ElapsedMilliseconds / 1000.0:F3} seconds, {ToMiB(data.LongLength):F3} MiB -> {ToMiB(compressedData.LongLength):F3} MiB{verifyResultMsg} (deflated {CalcDeflatedRate(data.LongLength, compressedData.LongLength) * 100.0:F2}%)");
                });

            Console.WriteLine("- - -");
            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} seconds.");
            Console.WriteLine($"{ToMiB(srcTotalFileSize):F3} MiB -> {ToMiB(dstTotalFileSize):F3} MiB (deflated {CalcDeflatedRate(srcTotalFileSize, dstTotalFileSize) * 100.0:F2}%)");
            if (execOptions.IsVerifyImage)
            {
                if (nProcPngFiles == nSameImages)
                {
                    Console.WriteLine("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    Console.WriteLine($"{nProcPngFiles - nSameImages} / {nProcPngFiles} PNG files are different image.");
                }
            }
        }

        /// <summary>
        /// Count PNG files and print its full name in the zip archive file.
        /// </summary>
        /// <param name="zipFilePath">Target zip archive file.</param>
        private static void CountPngInZipArchive(string zipFilePath)
        {
            var totalPngFiles = 0;
            var totalPngFileSize = 0L;
            using (var archive = ZipFile.OpenRead(zipFilePath))
            {
                foreach (var entry in archive.Entries.Where(entry => entry.Name.EndsWith(".png")))
                {
                    var fileSize = entry.Length;
                    Console.WriteLine($"{entry.FullName}: {ToMiB(fileSize):F3} MiB");
                    totalPngFiles++;
                    totalPngFileSize += fileSize;
                }
            }
            Console.WriteLine("- - -");
            Console.WriteLine($"The number of target PNG files: {totalPngFiles}");
            Console.WriteLine($"Total target PNG file size: {ToMiB(totalPngFileSize):F3} MiB");
        }

        /// <summary>
        /// Count PNG files and print its full name in the directory.
        /// </summary>
        /// <param name="dirPath">Target directory path.</param>
        private static void CountPngInDirectory(string dirPath)
        {
            var totalPngFiles = 0;
            var totalPngFileSize = 0L;
            var dirFullPath = Path.GetFullPath(dirPath);
            foreach (var filePath in Directory.EnumerateFiles(dirFullPath, "*.png", SearchOption.AllDirectories))
            {
                var fileSize = new FileInfo(filePath).Length;
                Console.WriteLine($"{ToRelativePath(filePath, dirFullPath)}: {ToMiB(fileSize):F3} MiB");
                totalPngFiles++;
                totalPngFileSize += fileSize;
            }
            Console.WriteLine("- - -");
            Console.WriteLine($"The number of target PNG files: {totalPngFiles}");
            Console.WriteLine($"Total target PNG file size: {ToMiB(totalPngFileSize):F3} MiB");
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
        /// Read all data from <see cref="ZipArchiveEntry"/>.
        /// </summary>
        /// <param name="entry">Target <see cref="ZipArchiveEntry"/>.</param>
        /// <param name="lockObj">The object for lock.</param>
        /// <returns>Read data.</returns>
        private static byte[] ReadAllBytes(ZipArchiveEntry entry, object lockObj)
        {
            var data = new byte[entry.Length];
            lock (lockObj)
            {
                using (var zs = entry.Open())
                {
                    zs.Read(data, 0, data.Length);
                }
            }
            return data;
        }

        /// <summary>
        /// Create new entry in <see cref="ZipArchive"/> and write data to it.
        /// </summary>
        /// <param name="archive">Target zip archive.</param>
        /// <param name="entryName">New entry name.</param>
        /// <param name="data">The data to write.</param>
        /// <param name="lockObj">The object for lock.</param>
        /// <param name="timestamp">Timestamp for new entry.</param>
        private static void CreateEntryAndWriteData(ZipArchive archive, string entryName, byte[] data, object lockObj, DateTimeOffset? timestamp = null)
        {
            lock (lockObj)
            {
                var dstEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                if (timestamp.HasValue)
                {
                    dstEntry.LastWriteTime = timestamp.Value;
                }
                using (var dstZs = dstEntry.Open())
                {
                    dstZs.Write(data, 0, data.Length);
                }
            }
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
            using (var bmp1 = CreateBitmapFromByteArray(imgData1))
            using (var bmp2 = CreateBitmapFromByteArray(imgData2))
            {
                return CompareImage(bmp1, bmp2) || _memoryComparator.CompareMemory(imgData1, imgData2);
            }
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
            using (var bmp1 = CreateBitmapFromByteArray(imgData1, imgDataLength1))
            using (var bmp2 = CreateBitmapFromByteArray(imgData2, imgDataLength2))
            {
                return CompareImage(bmp1, bmp2)
                    || (imgData1.LongLength == imgData2.LongLength && _memoryComparator.CompareMemory(imgData1, imgData2, imgData1.Length));
            }
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
                ImageLockMode.ReadOnly,
                img1.PixelFormat);
            var bd2 = img2.LockBits(
                new Rectangle(0, 0, img2.Width, img2.Height),
                ImageLockMode.ReadOnly,
                img2.PixelFormat);

            if (bd1.Stride != bd2.Stride)
            {
                return false;
            }

            var isSameImageData = _memoryComparator.CompareMemory(bd1.Scan0, bd2.Scan0, bd1.Stride * bd1.Height);

            img2.UnlockBits(bd2);
            img1.UnlockBits(bd1);

            return isSameImageData;
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
            public static extern bool SetDllDirectory([In] string path);
        }
    }
}
