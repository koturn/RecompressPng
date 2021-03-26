using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ArgumentParserSharp;
using ArgumentParserSharp.Exceptions;
using NLog;
using ZopfliSharp;


namespace RecompressPng
{
    /// <summary>
    /// PNG re-compression using "zopfli" algorithm.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger;
        /// <summary>
        /// Signature of PNG file.
        /// </summary>
        private static readonly byte[] PngSignature;
        /// <summary>
        /// Memory comparator.
        /// </summary>
        private static MemoryComparator _memoryComparator;


        /// <summary>
        /// Setup DLL search path.
        /// </summary>
        static Program()
        {
            UnsafeNativeMethods.SetDllDirectory(
                Path.Combine(
                    AppContext.BaseDirectory,
                    Environment.Is64BitProcess ? "x64" : "x86"));
            _logger = LogManager.GetCurrentClassLogger();
            PngSignature = new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a };
        }

        /// <summary>
        /// An entry point of this program.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Status code.</returns>
        private static int Main(string[] args)
        {
            try
            {
                var (target, pngOptions, execOptions) = ParseCommadLineArguments(args);
                ShowParameters(pngOptions, execOptions);

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
                        _logger.Fatal("Specified file is not zip archive: ", target);
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
                    _logger.Fatal("Specified file or directory doesn't exist: {0}", target);
                    return 1;
                }

                return 0;
            }
            catch (ArgumentParserException ex)
            {
                _logger.Fatal(ex, "Failed to parse command-line arguments:");
                return 64;
            }
            catch (AggregateException exs)
            {
                foreach (var ex in exs.Flatten().InnerExceptions)
                {
                    _logger.Fatal(ex, "AggregateException:");
                }
                return 1;
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "An exception occured:");
                return 1;
            }
            finally
            {
                _memoryComparator?.Dispose();
            }
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
                "Filter strategies to try:\n"
                + indent3 + "0-4: give all scanlines PNG filter type 0-4\n"
                + indent3 + "m: Minimum sum\n"
                + indent3 + "e: Entropy\n"
                + indent3 + "p: Predefined (keep from input, this likely overlaps another strategy)\n"
                + indent3 + "b: Brute force (experimental)\n"
                + indent2 + "By default, if this argument is not given, one that is most likely the best for this image is chosen by trying faster compression with each type.\n"
                + indent2 + "If this argument is used, all given filter types are tried with slow compression and the best result retained.\n"
                + indent2 + "A good set of filters to try is -s 0me.",
                "0|1|2|3|4|m|e|p|b...");
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

            if (ap.GetValue<bool>('h'))
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
            zo.NumIterations = ap.GetValue<int>('i');
            zo.NumIterationsLarge = ap.GetValue<int>('I');
            zo.LossyTransparent = ap.GetValue<bool>("lossy-transparent");
            zo.Lossy8bit = ap.GetValue<bool>("lossy-8bit");
            zo.AutoFilterStrategy = !ap.GetValue<bool>("no-auto-filter-strategy");
            zo.UseZopfli = !ap.GetValue<bool>("no-use-zopfli");

            if (ap.HasValue('s'))
            {
                zo.FilterStrategies.AddRange(ParseFilterStrategiesString(ap.GetValue('s')));
            }
            if (ap.HasValue("keep-chunks"))
            {
                zo.KeepChunks.AddRange(ap.GetValue("keep-chunks").Split(','));
            }

            return (
                targets[0],
                zo,
                new ExecuteOptions(
                    ap.GetValue<int>('n'),
                    !ap.GetValue<bool>("no-overwrite"),
                    ap.GetValue<bool>('r'),
                    !ap.GetValue<bool>("no-keep-timestamp"),
                    ap.GetValue<bool>('d'),
                    ap.GetValue<bool>('c'),
                    ap.GetValue<bool>('v'),
                    !ap.GetValue<bool>("no-verify-image")));
        }

        /// <summary>
        /// Parse option value for "-s" or "--strategies".
        /// </summary>
        /// <param name="filterStrategiesString">Option value for "-s" or "--strategies".</param>
        /// <returns>Enumeration of <see cref="ZopfliPNGFilterStrategy"/>.</returns>
        private static IEnumerable<ZopfliPNGFilterStrategy> ParseFilterStrategiesString(string filterStrategiesString)
        {
            foreach (var c in filterStrategiesString)
            {
                switch (c)
                {
                    case '0':
                        yield return ZopfliPNGFilterStrategy.Zero;
                        break;
                    case '1':
                        yield return ZopfliPNGFilterStrategy.One;
                        break;
                    case '2':
                        yield return ZopfliPNGFilterStrategy.Two;
                        break;
                    case '3':
                        yield return ZopfliPNGFilterStrategy.Three;
                        break;
                    case '4':
                        yield return ZopfliPNGFilterStrategy.Four;
                        break;
                    case 'm':
                        yield return ZopfliPNGFilterStrategy.MinSum;
                        break;
                    case 'e':
                        yield return ZopfliPNGFilterStrategy.Entropy;
                        break;
                    case 'p':
                        yield return ZopfliPNGFilterStrategy.Predefined;
                        break;
                    case 'b':
                        yield return ZopfliPNGFilterStrategy.BruteForce;
                        break;
                }
            }
        }

        /// <summary>
        /// Output options for zopflipng to stdout.
        /// </summary>
        /// <param name="pngOptions">Options for zopflipng</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void ShowParameters(ZopfliPNGOptions pngOptions, ExecuteOptions execOptions)
        {
            var strategies = pngOptions.FilterStrategies is null ? "" : string.Join(", ", pngOptions.FilterStrategies);
            var keepChunks = pngOptions.KeepChunks is null ? "" : string.Join(", ", pngOptions.KeepChunks);

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
            var srcFileSize = new FileInfo(srcZipFilePath).Length;
            var diffImageList = new List<string>();
            var errorImageList = new List<string>();
            var totalSw = Stopwatch.StartNew();

            using (var srcArchive = ZipFile.OpenRead(srcZipFilePath))
            using (var dstArchive = execOptions.IsDryRun ? null : ZipFile.Open(dstZipFilePath, ZipArchiveMode.Create))
            {
                var srcLock = new object();
                var dstLock = execOptions.IsDryRun ? null : new object();

                void CopyZipEntry(ZipArchiveEntry srcEntry, int procIndex, Stopwatch sw)
                {
                    if (execOptions.IsDryRun)
                    {
                        return;
                    }
                    _logger.Info("[{0}] Copy {1} ...", procIndex, srcEntry.FullName);
                    try
                    {
                        CreateEntryAndWriteData(
                            dstArchive,
                            srcEntry.FullName,
                            ReadAllBytes(srcEntry, srcLock),
                            dstLock,
                            execOptions.IsKeepTimestamp ? (DateTimeOffset?)srcEntry.LastWriteTime : null);
                        _logger.Info(
                            "[{0}] Copy {1} done: {2:F3} seconds",
                            procIndex,
                            srcEntry.FullName,
                            sw.ElapsedMilliseconds / 1000.0);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[{0}] Copy {1} failed: ", procIndex, srcEntry.FullName);
                    }
                }

                Parallel.ForEach(
                    srcArchive.Entries,
                    new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                    srcEntry =>
                    {
                        var sw = Stopwatch.StartNew();
                        var procIndex = Interlocked.Increment(ref nProcPngFiles);

                        if (!srcEntry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            CopyZipEntry(srcEntry, procIndex, sw);
                            return;
                        }

                        _logger.Info("[{0}] Compress {1} ...", procIndex, srcEntry.FullName);
                        try
                        {
                            var data = ReadAllBytes(srcEntry, srcLock);
                            if (!HasPngSignature(data))
                            {
                                _logger.Error("[{0}] Compress {1} failed, invalid PNG signature");
                                CopyZipEntry(srcEntry, procIndex, sw);
                                return;
                            }

                            // Take a long time
                            var compressedData = ZopfliPng.OptimizePng(
                                data,
                                pngOptions,
                                execOptions.Verbose);

                            if (!execOptions.IsDryRun)
                            {
                                CreateEntryAndWriteData(
                                    dstArchive,
                                    srcEntry.FullName,
                                    (compressedData.LongLength < data.LongLength || execOptions.IsReplaceForce) ? compressedData : data,
                                    dstLock,
                                    execOptions.IsKeepTimestamp ? (DateTimeOffset?)srcEntry.LastWriteTime : null);
                            }

                            var logLevel = LogLevel.Info;
                            var verifyResultMsg = "";
                            if (execOptions.IsVerifyImage)
                            {
                                if (CompareImage(data, data.LongLength, compressedData, compressedData.LongLength))
                                {
                                    verifyResultMsg = " (same image)";
                                }
                                else
                                {
                                    verifyResultMsg = " (different image)";
                                    logLevel = LogLevel.Warn;
                                    lock (((ICollection)diffImageList).SyncRoot)
                                    {
                                        diffImageList.Add(srcEntry.FullName);
                                    }
                                }
                            }
                            _logger.Log(
                                logLevel,
                                "[{0}] Compress {1} done: {2:F3} seconds, {3:F3} MiB -> {4:F3} MiB{5} (deflated {6:F2}%)",
                                procIndex,
                                srcEntry.FullName,
                                sw.ElapsedMilliseconds / 1000.0,
                                ToMiB(data.LongLength),
                                ToMiB(compressedData.LongLength),
                                verifyResultMsg,
                                CalcDeflatedRate(data.LongLength, compressedData.LongLength) * 100.0);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                ex,
                                "[{0}] Compress {1} failed:",
                                procIndex,
                                srcEntry.FullName);
                            CopyZipEntry(srcEntry, procIndex, sw);
                            lock (((ICollection)errorImageList).SyncRoot)
                            {
                                errorImageList.Add(srcEntry.FullName);
                            }
                        }
                    });
            }

            if (execOptions.IsOverwrite)
            {
                File.Delete(srcZipFilePath);
                File.Move(dstZipFilePath, srcZipFilePath);
            }

            Console.WriteLine("- - -");
            if (nProcPngFiles == 0)
            {
                _logger.Info("No PNG file were processed.");
                return;
            }
            _logger.Info("All PNG files were proccessed ({0} files).", nProcPngFiles);
            _logger.Info("Elapsed time: {0:F3} seconds.", totalSw.ElapsedMilliseconds / 1000.0);
            if (!execOptions.IsDryRun)
            {
                var dstFileSize = new FileInfo(execOptions.IsOverwrite ? srcZipFilePath : dstZipFilePath).Length;
                _logger.Info(
                    "{0:F3} MiB -> {1:F3} MiB (deflated {2:F2}%)",
                    ToMiB(srcFileSize),
                    ToMiB(dstFileSize),
                    CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0);
            }
            if (execOptions.IsVerifyImage)
            {
                if (diffImageList.Count == 0)
                {
                    _logger.Info("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    _logger.Warn("{0} / {1} PNG files are different image.", diffImageList.Count, nProcPngFiles);
                    int cnt = 1;
                    foreach (var fullname in diffImageList)
                    {
                        Console.WriteLine($"Different image [{cnt}]: {fullname}");
                        cnt++;
                    }
                }
            }
            if (errorImageList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageList.Count);
                int cnt = 1;
                foreach (var fullname in diffImageList)
                {
                    Console.WriteLine($"Error image [{cnt}]: {fullname}");
                    cnt++;
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
            var diffImageList = new List<string>();
            var errorImageList = new List<string>();
            var totalSw = Stopwatch.StartNew();

            Parallel.ForEach(
                Directory.EnumerateFiles(srcDirPath, "*.png", SearchOption.AllDirectories),
                new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                srcFilePath =>
                {
                    var sw = Stopwatch.StartNew();
                    var procIndex = Interlocked.Increment(ref nProcPngFiles);
                    var srcRelPath = ToRelativePath(srcFilePath, srcBaseDirFullPath);
                    var dstFilePath = execOptions.IsOverwrite ? srcFilePath : Path.Combine(
                        dstBaseDirFullPath,
                        new StringBuilder(Path.GetFullPath(srcFilePath))
                            .Replace(srcBaseDirFullPath + @"\", "", 0, srcBaseDirFullPath.Length + 1)
                            .ToString());

                    _logger.Info("[{0}] Compress {1} ...", procIndex, srcRelPath);
                    try
                    {
                        var data = File.ReadAllBytes(srcFilePath);
                        if (!HasPngSignature(data))
                        {
                            _logger.Error("[{0}] Compress {1} failed, invalid PNG signature");
                            return;
                        }
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

                        var logLevel = LogLevel.Info;
                        var verifyResultMsg = "";
                        if (execOptions.IsVerifyImage)
                        {
                            if (CompareImage(data, compressedData))
                            {
                                verifyResultMsg = " (same image)";
                            }
                            else
                            {
                                logLevel = LogLevel.Warn;
                                verifyResultMsg = " (different image)";
                                lock (((ICollection)diffImageList).SyncRoot)
                                {
                                    diffImageList.Add(srcRelPath);
                                }
                            }
                        }
                        _logger.Log(
                            logLevel,
                            "[{0}] Compress {1} done: {2:F3} seconds, {3:F3} MiB -> {4:F3} MiB{5} (deflated {6:F2}%)",
                            procIndex,
                            srcRelPath,
                            sw.ElapsedMilliseconds / 1000.0,
                            ToMiB(data.LongLength),
                            ToMiB(compressedData.LongLength),
                            verifyResultMsg,
                            CalcDeflatedRate(data.LongLength, compressedData.LongLength) * 100.0);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[{0}] Compress {1} failed", procIndex, srcRelPath);
                        lock (((ICollection)errorImageList).SyncRoot)
                        {
                            errorImageList.Add(srcRelPath);
                        }
                    }
                });

            Console.WriteLine("- - -");
            if (nProcPngFiles == 0)
            {
                _logger.Info("No PNG file were processed.");
                return;
            }
            _logger.Info("All PNG files were proccessed ({0} files).", nProcPngFiles);
            _logger.Info("Elapsed time: {0:F3} seconds.", totalSw.ElapsedMilliseconds / 1000.0);
            _logger.Info(
                "{0:F3} MiB -> {1:F3} MiB (deflated {2:F2}%)",
                ToMiB(srcTotalFileSize),
                ToMiB(dstTotalFileSize),
                CalcDeflatedRate(srcTotalFileSize, dstTotalFileSize) * 100.0);
            if (execOptions.IsVerifyImage)
            {
                if (diffImageList.Count == 0)
                {
                    _logger.Info("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    _logger.Warn("{0} / {1} PNG files are different image.", diffImageList.Count, nProcPngFiles);
                    int cnt = 1;
                    foreach (var relPath in diffImageList)
                    {
                        Console.WriteLine($"Different image [{cnt}]: {relPath}");
                        cnt++;
                    }
                }
            }
            if (errorImageList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageList.Count);
                int cnt = 1;
                foreach (var relPath in diffImageList)
                {
                    Console.WriteLine($"Error image [{cnt}]: {relPath}");
                    cnt++;
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
        /// <para>Just determine if the first four bytes are 'P', 'K', 0x03 and 0x04.</para>
        /// </summary>
        /// <param name="zipFilePath">Target zip file path,</param>
        /// <returns>True if specified file is a zip archive file, otherwise false.</returns>
        private static bool IsZipFile(string zipFilePath)
        {
            var buffer = new byte[4];
            using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Read(buffer, 0, buffer.Length) < buffer.Length)
                {
                    return false;
                }
            }
            return HasZipSignature(buffer);
        }

        /// <summary>
        /// <para>Identify the specified binary data has a zip signature or not.</para>
        /// <para>Just determine if the first four bytes are 'P', 'K', 0x03 and 0x04.</para>
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a zip signature, otherwise false.</returns>
        private static bool HasZipSignature(byte[] data)
        {
            return data.Length >= 4
                && data[0] == 'P'
                && data[1] == 'K'
                && data[2] == '\x03'
                && data[3] == '\x04';
        }

        /// <summary>
        /// Identify the specified binary data has a PNG signature or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a PNG signature, otherwise false.</returns>
        private static bool HasPngSignature(byte[] data)
        {
            if (data.Length < PngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (data[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
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
        private static Bitmap CreateBitmapFromByteArray(byte[] imgData)
        {
            return CreateBitmapFromByteArray(imgData, imgData.LongLength);
        }

        /// <summary>
        /// Convert <see cref="Bitmap"/> instance from image data.
        /// </summary>
        /// <param name="imgData">Image data.</param>
        /// <param name="imgDataLength">Byte length of <paramref name="imgData"/>.</param>
        /// <returns><see cref="Bitmap"/> instance.</returns>
        private static Bitmap CreateBitmapFromByteArray(byte[] imgData, long imgDataLength)
        {
            using (var ms = new MemoryStream(imgData, 0, (int)imgDataLength, false, false))
            {
                return (Bitmap)Image.FromStream(ms);
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
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [SuppressUnmanagedCodeSecurity]
            public static extern bool SetDllDirectory([In] string path);
        }
    }
}
