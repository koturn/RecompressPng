#if NET7_0_OR_GREATER
#    define SUPPORT_LIBRARY_IMPORT
#endif  // NET7_0_OR_GREATER

using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
#endif  // NETCOREAPP3_0_OR_GREATER
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Koturn.CommandLine;
using Koturn.CommandLine.Exceptions;
using Koturn.Zopfli;
using Koturn.Zopfli.Checksums;
using Koturn.Zopfli.Enums;
using RecompressPng.Glb;
using RecompressPng.Internals;

#if !NET9_0_OR_GREATER
using Lock = object;
#endif  // NET9_0_OR_GREATER


namespace RecompressPng
{
    /// <summary>
    /// PNG re-compression using "zopfli" algorithm.
    /// </summary>
#if SUPPORT_LIBRARY_IMPORT
    static partial class Program
#else
    static class Program
#endif  // SUPPORT_LIBRARY_IMPORT
    {
        /// <summary>
        /// Chunk type string of IDAT chunk.
        /// </summary>
        private const string ChunkTypeIdat = "IDAT";
        /// <summary>
        /// Chunk type string of IEND chunk.
        /// </summary>
        private const string ChunkTypeIend = "IEND";
        /// <summary>
        /// Chunk type string of tEXt chunk.
        /// </summary>
        private const string ChunkNameText = "tEXt";
        /// <summary>
        /// Chunk type string of tIME chunk.
        /// </summary>
        private const string ChunkNameTime = "tIME";
        /// <summary>
        /// Predefined keyword of tEXt chunk for time of original image creation.
        /// </summary>
        private const string TextChunkKeyCreationTime = "Creation Time";

        /// <summary>
        /// Logging instance.
        /// </summary>
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// Signature of PNG file.
        /// </summary>
        private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0d, 0x0a, 0x1a, 0x0a];
        /// <summary>
        /// Magic number byte sequence of glTF file.
        /// </summary>
        private static readonly byte[] GltfMagicBytes = [(byte)'g', (byte)'l', (byte)'T', (byte)'F'];
        /// <summary>
        /// All chunks to keep when "--keep-all-chunks" specified.
        /// </summary>
        private static readonly string[] AllChunks = ["acTL", "bKGD", "cHRM", "eXIf", "fcTL", "fdAT", "gAMA", "hIST", "iCCP", "iTXt", "pHYs", "sBIT", "sPLT", "sRGB", "tEXt", "tIME", "zTXt"];


        /// <summary>
        /// Setup DLL search paths and logging instance.
        /// </summary>
        static Program()
        {
            var dllDir = Path.Combine(
                AppContext.BaseDirectory,
                Environment.Is64BitProcess ? "x64" : "x86");
            // Current directory will be searched previously.
            // Paths that are added later have a higher priority in the search.
            //   1. Current Directory
            //   2. dllDir + @"\avx2"
            //   3. dllDir
            UnsafeNativeMethods.AddDllDirectory(dllDir);
#if NETCOREAPP3_0_OR_GREATER
            if (Avx2.IsSupported)
            {
                UnsafeNativeMethods.AddDllDirectory(Path.Combine(dllDir, "avx2"));
            }
#endif  // NETCOREAPP3_0_OR_GREATER
            UnsafeNativeMethods.SetDefaultDllDirectories(LoadLibrarySearchFlags.DefaultDirs);
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

                if (File.Exists(target))
                {
                    if (IsZipFile(target))
                    {
                        if (execOptions.IsCountOnly)
                        {
                            CountPngInZipArchive(target);
                        }
                        else if (execOptions.IsCopyAndShrinkZip)
                        {
                            RecompressPngInZipArchiveCopyAndShrink(target, null, pngOptions, execOptions);
                        }
                        else
                        {
                            RecompressPngInZipArchive(target, null, pngOptions, execOptions);
                        }
                    }
                    else if (IsGltfFile(target))
                    {
                        if (execOptions.IsCountOnly)
                        {
                            CountPngInGlb(target);
                        }
                        else
                        {
                            RecompressPngInGlb(target, null, pngOptions, execOptions);
                        }
                    }
                    else
                    {
                        _logger.Fatal("Specified file is neither zip archive nor glTF file: {0}", target);
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
        }

        /// <summary>
        /// Parse command line arguments and retrieve the result.
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <returns>Parse result tuple.</returns>
        private static (string Target, ZopfliPngOptions PngOptions, ExecuteOptions ExecOptions) ParseCommadLineArguments(string[] args)
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
            ap.Add('f', "filters", OptionType.RequiredArgument,
                "Filter strategies to try:\n"
                + indent3 + "0-4: give all scanlines PNG filter type 0-4\n"
                + indent3 + "m: Minimum sum\n"
                + indent3 + "e: Entropy\n"
                + indent3 + "p: Predefined (keep from input, this likely overlaps another strategy)\n"
                + indent3 + "b: Brute force (experimental)\n"
                + indent2 + "By default, if this argument is not given, one that is most likely the best for this image is chosen by trying faster compression with each type.\n"
                + indent2 + "If this argument is used, all given filter types are tried with slow compression and the best result retained.\n"
                + indent2 + "A good set of filters to try is -f 0me.",
                "0|1|2|3|4|m|e|p|b...");
            ap.AddHelp();
            ap.Add('i', "num-iteration", OptionType.RequiredArgument, "Number of iteration.", "NUM", ZopfliPngOptions.DefaultNumIterations);
            ap.Add('I', "num-iteration-large", OptionType.RequiredArgument, "Number of iterations on large images.", "NUM", ZopfliPngOptions.DefaultNumIterationsLarge);
            ap.Add('n', "num-thread", OptionType.RequiredArgument, "Number of threads for re-compressing. -1 means unlimited.", "N", ExecuteOptions.DefaultNumberOfThreads);
            ap.Add('q', "no-use-zopfli",
                "Use quick, but not very good, compression.\n"
                + indent2 + "(e.g. for only trying the PNG filter and color types)");
            ap.Add('r', "replace-force", "Do the replacement even if the size of the recompressed data is larger than the size of the original data.");
            ap.Add('v', "verbose", "Allow to output to stdout from zopflipng.dll.");
            ap.Add("add-text-creation-time",
                "Add tEXt chunk whose key is \"Creation Time\" and value is last update time of PNG file.\n"
                + indent2 + "The data time format can be specified with \"--creation-time-format\".");
            ap.Add("creation-time-format", OptionType.RequiredArgument,
                "Specify Creation Time format.\n"
                + indent2 + "Format string is same as DateTime or DateTimeOffset of .NET\n"
                + indent2 + "\"r\", RFC-1123 section 5.2.14 is recommended in PNG specification.\n"
                + indent2 + "\"yyyy:MM:dd HH:mm:ss.fff\" can be recognize explorer.exe of Windows.",
                "FORMAT",
                "yyyy:MM:dd HH:mm:ss.fff");
            ap.Add("add-time", "Add tIME chunk whose value is last update time of PNG file in UTC.");
            ap.Add("idat-size", OptionType.RequiredArgument,
                "Specify chunk data size in IDAT.\n"
                + indent2 + "The IDAT is splitted so that the data part of the IDAT becomes the specified size.\n"
                + indent2 + "0 or negative value means no splitting",
                "SIZE", -1);
            ap.Add("ignore-single-idat-size", OptionType.RequiredArgument,
                "Don't process PNG files with a single IDAT chunk which IDAT chunk size is larger than the specified size.\n"
                + indent2 + "Negative values mean that PNG files with a single IDAT of any size will be processed.",
                "SIZE",
                -1L);
            ap.Add("keep-chunks", OptionType.RequiredArgument,
                "Keep metadata chunks with these names that would normally be removed,\n"
                + indent3 + "e.g. tEXt,zTXt,iTXt,gAMA, ... \n"
                + indent2 + "Due to adding extra data, this increases the result size.\n"
                + indent2 + "By default ZopfliPNG only keeps the following chunks because they are essential:\n"
                + indent3 + "IHDR, PLTE, tRNS, IDAT and IEND.",
                "NAME,NAME...");
            ap.Add("keep-all-chunks",
                "Keep all metadata chunks.\n"
                + indent2 + "This option is equivalent to the following.\n"
#if NETCOREAPP2_0_OR_GREATER
                + indent3 + $"--keep-chunks={string.Join(',', AllChunks)}");
#else
                + indent3 + $"--keep-chunks={string.Join(",", AllChunks)}");
#endif  // NETCOREAPP2_0_OR_GREATER
            ap.Add("lossy-transparent", "Remove colors behind alpha channel 0. No visual difference, removes hidden information.");
            ap.Add("lossy-8bit", "Convert 16-bit per channel images to 8-bit per channel.");
            ap.Add("zip-copy-and-shrink",
                "Copy source zip file at first, then open the copied file and recompress PNG files.\n"
                + indent2 + "Since this method leaves entries other than PNG files untouched, it may result in smaller zip files when targeting highly compressed zip files created with 7-zip or other efficient methods.");
            ap.Add("no-auto-filter-strategy", "Automatically choose filter strategy using less good compression.");
            ap.Add("no-keep-timestamp", "Don't keep timestamp.");
            ap.Add("no-overwrite", "Don't overwrite PNG files and create images to new zip archive file or directory.");
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

            // Check DateTime format of Creation Time
            var isAddCt = ap.GetValue<bool>("add-text-creation-time");
            string? ctFormat = null;
            if (isAddCt)
            {
                ctFormat = ap.GetValue("creation-time-format");
                if (ctFormat == "")
                {
                    Console.Error.WriteLine("Date time format for Creation Time is empty string");
                    Environment.Exit(1);
                }
                DateTime.Now.ToString(ctFormat);
            }

            var zo = ZopfliPngOptions.GetDefault();
            zo.NumIterations = ap.GetValue<int>('i');
            zo.NumIterationsLarge = ap.GetValue<int>('I');
            zo.LossyTransparent = ap.GetValue<bool>("lossy-transparent");
            zo.Lossy8bit = ap.GetValue<bool>("lossy-8bit");
            zo.AutoFilterStrategy = !ap.GetValue<bool>("no-auto-filter-strategy");
            zo.UseZopfli = !ap.GetValue<bool>('q');

            if (ap.HasValue('f'))
            {
                zo.FilterStrategies.AddRange(ParseFilterStrategiesString(ap.GetValue('f')!));
            }
            if (ap.HasValue("keep-chunks"))
            {
                zo.KeepChunks.AddRange(ap.GetValue("keep-chunks")!.Split(','));
            }
            if (ap.GetValue<bool>("keep-all-chunks"))
            {
                zo.KeepChunks.AddRange(AllChunks);
            }

            return (
                targets[0],
                zo,
                new ExecuteOptions(
                    ap.GetValue<int>('n'),
                    !ap.GetValue<bool>("no-overwrite"),
                    ap.GetValue<bool>('r'),
                    !ap.GetValue<bool>("no-keep-timestamp"),
                    ap.GetValue<long>("ignore-single-idat-size"),
                    ap.GetValue<int>("idat-size"),
                    isAddCt ? ctFormat : "",
                    ap.GetValue<bool>("add-time"),
                    ap.GetValue<bool>("zip-copy-and-shrink"),
                    ap.GetValue<bool>('d'),
                    ap.GetValue<bool>('c'),
                    ap.GetValue<bool>('v'),
                    !ap.GetValue<bool>("no-verify-image")));
        }

        /// <summary>
        /// Parse option value for "-s" or "--strategies".
        /// </summary>
        /// <param name="filterStrategiesString">Option value for "-s" or "--strategies".</param>
        /// <returns>Enumeration of <see cref="ZopfliPngFilterStrategy"/>.</returns>
        private static IEnumerable<ZopfliPngFilterStrategy> ParseFilterStrategiesString(string filterStrategiesString)
        {
            foreach (var c in filterStrategiesString)
            {
                yield return c switch
                {
                    '0' => ZopfliPngFilterStrategy.Zero,
                    '1' => ZopfliPngFilterStrategy.One,
                    '2' => ZopfliPngFilterStrategy.Two,
                    '3' => ZopfliPngFilterStrategy.Three,
                    '4' => ZopfliPngFilterStrategy.Four,
                    'm' => ZopfliPngFilterStrategy.MinSum,
                    'e' => ZopfliPngFilterStrategy.Entropy,
                    'p' => ZopfliPngFilterStrategy.Predefined,
                    'b' => ZopfliPngFilterStrategy.BruteForce,
                    _ => throw new ArgumentException($"Invalid filter strategy character is specified: {c}", nameof(filterStrategiesString))
                };
            }
        }

        /// <summary>
        /// Output options for zopflipng to stdout.
        /// </summary>
        /// <param name="pngOptions">Options for zopflipng</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void ShowParameters(ZopfliPngOptions pngOptions, ExecuteOptions execOptions)
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
            Console.WriteLine($"Ignore Single IDAT Size: {execOptions.IgnoreSingleIdatSize}");
            Console.WriteLine($"Dry Run: {execOptions.IsDryRun}");
            Console.WriteLine($"Verbose: {execOptions.Verbose}");
            Console.WriteLine($"Verify Image: {execOptions.IsVerifyImage}");

            Console.WriteLine("- - - PNG Modification Parameters - - -");
            Console.WriteLine($"Size of data part of IDAT chunk: {(execOptions.IdatSize < 0 ? "No Splitting" : $"{execOptions.IdatSize} Bytes")}");
            var ctFormat = execOptions.TextCreationTimeFormat;
            var isAddCt = !string.IsNullOrEmpty(ctFormat);
            Console.WriteLine($"Add Creation Time of tEXt chunk: {isAddCt}{(isAddCt ? $" ({ctFormat})" : "")}");
            Console.WriteLine($"Add tIME chunk: {execOptions.IsAddTimeChunk}");
            Console.WriteLine("- - -");
        }

        /// <summary>
        /// <para>Re-compress all PNG files in zip archive using "zopfli" algorithm.</para>
        /// <para>Rebuild the zip file from scratch, which means decompress all entries, even non-PNG files,
        /// and then deflate and compress again.</para>
        /// <para>This may cause the compressed size of the non-PNG file to be larger than that of the original zip file,
        /// especially for zip files created with 7-zip or similar.</para>
        /// </summary>
        /// <param name="srcZipFilePath">Source zip archive file.</param>
        /// <param name="dstZipFilePath">Destination zip archive file.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPngInZipArchive(string srcZipFilePath, string? dstZipFilePath, ZopfliPngOptions pngOptions, ExecuteOptions execOptions)
        {
            dstZipFilePath ??= Path.Combine(
                Path.GetDirectoryName(srcZipFilePath) ?? ".",
                Path.GetFileNameWithoutExtension(srcZipFilePath) + ".zopfli" + Path.GetExtension(srcZipFilePath));

            if (File.Exists(dstZipFilePath))
            {
                File.Delete(dstZipFilePath);
            }

            int nProcPngFiles = 0;
            var srcFileSize = new FileInfo(srcZipFilePath).Length;
            var diffImageIndexNameList = new List<ImageIndexNamePair>();
            var errorImageIndexNameList = new List<ImageIndexNamePair>();
            var totalSw = Stopwatch.StartNew();

            using (var srcArchive = ZipFile.OpenRead(srcZipFilePath))
            using (var dstArchive = execOptions.IsDryRun ? null : ZipFile.Open(dstZipFilePath, ZipArchiveMode.Create))
            {
                var srcLock = new Lock();
                var dstLock = execOptions.IsDryRun ? null : new Lock();

                void CopyZipEntry(ZipArchiveEntry srcEntry)
                {
                    // is dry-run.
                    if (dstArchive is null || dstLock is null)
                    {
                        return;
                    }
                    _logger.Info("Copy {0} ...", srcEntry.FullName);
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        CreateEntryAndWriteData(
                            dstArchive,
                            srcEntry.FullName,
                            ReadAllBytes(srcEntry, srcLock),
                            dstLock,
                            execOptions.IsKeepTimestamp ? srcEntry.LastWriteTime : null);
                        _logger.Info(
                            "Copy {0} done: {1:F3} seconds",
                            srcEntry.FullName,
                            sw.ElapsedMilliseconds / 1000.0);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Copy {0} failed: ", srcEntry.FullName);
                    }
                }

                Parallel.ForEach(
                    Partitioner.Create(srcArchive.Entries, true),
                    new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                    srcEntry =>
                    {
                        if (!srcEntry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            CopyZipEntry(srcEntry);
                            return;
                        }

                        var procIndex = Interlocked.Increment(ref nProcPngFiles);
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            var data = ReadAllBytes(srcEntry, srcLock);
                            if (!HasPngSignature(data))
                            {
                                _logger.Error("[{0}] Compress {1} failed, invalid PNG signature", procIndex, srcEntry.FullName);
                                CopyZipEntry(srcEntry);
                                return;
                            }

                            if (execOptions.IgnoreSingleIdatSize >= 0)
                            {
                                var (count, totalIdatSize) = CountAndGetTotalSizeOfIdatChunks(data);
                                if (count == 1 && totalIdatSize >= execOptions.IgnoreSingleIdatSize)
                                {
                                    _logger.Info("[{0}] Compress {1} ... Ignored (Single IDAT chunk)", procIndex, srcEntry.FullName);
                                    return;
                                }
                            }

                            _logger.Info("[{0}] Compress {1} ...", procIndex, srcEntry.FullName);

                            // Take a long time
                            using var compressedData = ZopfliPng.OptimizePngUnmanaged(
                                data,
                                pngOptions,
                                execOptions.Verbose);

                            var pngDataSpan = ((long)compressedData.ByteLength < data.LongLength || execOptions.IsReplaceForce)
                                ? SpanUtil.CreateSpan(compressedData)
                                : data.AsSpan();
                            if (execOptions.IsModifyPng)
                            {
                                pngDataSpan = AddAdditionalChunks(pngDataSpan, execOptions, srcEntry.LastWriteTime.DateTime);
                            }

                            // is not dry-run.
                            if (dstArchive is not null && dstLock is not null)
                            {
                                CreateEntryAndWriteData(
                                    dstArchive,
                                    srcEntry.FullName,
                                    pngDataSpan,
                                    dstLock,
                                    execOptions.IsKeepTimestamp ? srcEntry.LastWriteTime : null);
                            }

                            var logLevel = pngDataSpan.Length < data.Length ? LogLevel.Info : LogLevel.Warn;
                            var verifyResultMsg = "";
                            if (execOptions.IsVerifyImage)
                            {
                                var result = BitmapUtil.CompareImage(data, pngDataSpan);
                                verifyResultMsg = $" ({result})";
                                if (result.Type != CompareResultType.Same)
                                {
                                    logLevel = LogLevel.Warn;
                                    lock (((ICollection)diffImageIndexNameList).SyncRoot)
                                    {
                                        diffImageIndexNameList.Add(new ImageIndexNamePair(procIndex, srcEntry.FullName));
                                    }
                                }
                            }
                            _logger.Log(
                                logLevel,
                                "[{0}] Compress {1} done: {2:F3} MiB -> {3:F3} MiB (deflated {4:F2}%, {5:F3} seconds){6}",
                                procIndex,
                                srcEntry.FullName,
                                ToMiB(data.LongLength),
                                ToMiB(pngDataSpan.Length),
                                CalcDeflatedRate(data.LongLength, pngDataSpan.Length) * 100.0,
                                sw.ElapsedMilliseconds / 1000.0,
                                verifyResultMsg);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                ex,
                                "[{0}] Compress {1} failed:",
                                procIndex,
                                srcEntry.FullName);
                            CopyZipEntry(srcEntry);
                            lock (((ICollection)errorImageIndexNameList).SyncRoot)
                            {
                                errorImageIndexNameList.Add(new ImageIndexNamePair(procIndex, srcEntry.FullName));
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
            if (execOptions.IsDryRun)
            {
                _logger.Info("Elapsed time: {0:F3} seconds.", totalSw.ElapsedMilliseconds / 1000.0);
            }
            else
            {
                var dstFileSize = new FileInfo(execOptions.IsOverwrite ? srcZipFilePath : dstZipFilePath).Length;
                _logger.Info(
                    "{0:F3} MiB -> {1:F3} MiB (deflated {2:F2}%, {3:F3} seconds)",
                    ToMiB(srcFileSize),
                    ToMiB(dstFileSize),
                    CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0,
                    totalSw.ElapsedMilliseconds / 1000.0);
            }
            if (execOptions.IsVerifyImage)
            {
                if (diffImageIndexNameList.Count == 0)
                {
                    _logger.Info("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    _logger.Warn("{0} / {1} PNG files are different image.", diffImageIndexNameList.Count, nProcPngFiles);
                    foreach (var (index, name) in diffImageIndexNameList)
                    {
                        Console.WriteLine($"Different image [{index}]: {name}");
                    }
                }
            }
            if (errorImageIndexNameList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageIndexNameList.Count);
                foreach (var (index, name) in errorImageIndexNameList)
                {
                    Console.WriteLine($"Error image [{index}]: {name}");
                }
            }
        }

        /// <summary>
        /// <para>Re-compress all PNG files in zip archive using "zopfli" algorithm.</para>
        /// <para>Copy source zip file at first, then open the copied file and recompress PNG files.</para>
        /// <para>Since this method leaves entries other than PNG files untouched,
        /// it may result in smaller zip files than <see cref="RecompressPngInZipArchive(string, string, ZopfliPngOptions, ExecuteOptions)"/>
        /// when targeting highly compressed zip files created with 7-zip or other efficient methods.</para>
        /// </summary>
        /// <param name="srcZipFilePath">Source zip archive file.</param>
        /// <param name="dstZipFilePath">Destination zip archive file.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPngInZipArchiveCopyAndShrink(string srcZipFilePath, string? dstZipFilePath, ZopfliPngOptions pngOptions, ExecuteOptions execOptions)
        {
            dstZipFilePath ??= Path.Combine(
                Path.GetDirectoryName(srcZipFilePath) ?? ".",
                Path.GetFileNameWithoutExtension(srcZipFilePath) + ".zopfli" + Path.GetExtension(srcZipFilePath));

            File.Copy(srcZipFilePath, dstZipFilePath, true);

            int nProcPngFiles = 0;
            var srcFileSize = new FileInfo(srcZipFilePath).Length;
            var diffImageIndexNameList = new List<ImageIndexNamePair>();
            var errorImageIndexNameList = new List<ImageIndexNamePair>();
            var totalSw = Stopwatch.StartNew();

            using (var zipArchive = ZipFile.Open(dstZipFilePath, ZipArchiveMode.Update))
            {
                var lockObj = new Lock();
                var deleteEntryList = new List<ZipArchiveEntry>();

                Parallel.ForEach(
                    Partitioner.Create(zipArchive.Entries, true),
                    new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                    srcEntry =>
                    {
                        if (!srcEntry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.Info("Ignore {0}", srcEntry.FullName);
                            return;
                        }

                        var procIndex = Interlocked.Increment(ref nProcPngFiles);
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            var (data, dataLength) = ReadAllBytesRestrict(srcEntry, lockObj);
                            if (!HasPngSignature(data))
                            {
                                _logger.Error("[{0}] Compress {1} failed, invalid PNG signature", procIndex, srcEntry.FullName);
                                return;
                            }

                            if (execOptions.IgnoreSingleIdatSize >= 0)
                            {
                                var (count, totalIdatSize) = CountAndGetTotalSizeOfIdatChunks(data);
                                if (count == 1 && totalIdatSize >= execOptions.IgnoreSingleIdatSize)
                                {
                                    _logger.Info("[{0}] Compress {1} ... Ignored (Single IDAT chunk)", procIndex, srcEntry.FullName);
                                    return;
                                }
                            }

                            _logger.Info("[{0}] Compress {1} ...", procIndex, srcEntry.FullName);

                            // Take a long time
                            using var compressedData = ZopfliPng.OptimizePngUnmanaged(
                                data,
                                0,
                                dataLength,
                                pngOptions,
                                execOptions.Verbose);

                            var isUpdateNeeded = (long)compressedData.ByteLength < dataLength || execOptions.IsReplaceForce;
                            var pngDataSpan = isUpdateNeeded ? SpanUtil.CreateSpan(compressedData) : data.AsSpan(0, dataLength);
                            if (execOptions.IsModifyPng)
                            {
                                isUpdateNeeded = true;
                                pngDataSpan = AddAdditionalChunks(pngDataSpan, execOptions, srcEntry.LastWriteTime.DateTime);
                            }

                            if (!execOptions.IsDryRun && isUpdateNeeded)
                            {
                                CreateEntryAndWriteData(
                                    zipArchive,
                                    srcEntry.FullName,
                                    pngDataSpan,
                                    lockObj,
                                    execOptions.IsKeepTimestamp ? srcEntry.LastWriteTime : null);
                                lock (((ICollection)deleteEntryList).SyncRoot)
                                {
                                    deleteEntryList.Add(srcEntry);
                                }
                            }

                            var logLevel = pngDataSpan.Length < dataLength ? LogLevel.Info : LogLevel.Warn;
                            var verifyResultMsg = "";
                            if (execOptions.IsVerifyImage)
                            {
                                var result = BitmapUtil.CompareImage(data.AsSpan(0, dataLength), pngDataSpan);
                                verifyResultMsg = $" ({result})";
                                if (result.Type != CompareResultType.Same)
                                {
                                    logLevel = LogLevel.Warn;
                                    lock (((ICollection)diffImageIndexNameList).SyncRoot)
                                    {
                                        diffImageIndexNameList.Add(new ImageIndexNamePair(procIndex, srcEntry.FullName));
                                    }
                                }
                            }
                            _logger.Log(
                                logLevel,
                                "[{0}] Compress {1} done: {2:F3} MiB -> {3:F3} MiB (deflated {4:F2}%, {5:F3} seconds){6}",
                                procIndex,
                                srcEntry.FullName,
                                ToMiB(dataLength),
                                ToMiB(pngDataSpan.Length),
                                CalcDeflatedRate(dataLength, pngDataSpan.Length) * 100.0,
                                sw.ElapsedMilliseconds / 1000.0,
                                verifyResultMsg);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(
                                ex,
                                "[{0}] Compress {1} failed:",
                                procIndex,
                                srcEntry.FullName);
                            lock (((ICollection)errorImageIndexNameList).SyncRoot)
                            {
                                errorImageIndexNameList.Add(new ImageIndexNamePair(procIndex, srcEntry.FullName));
                            }
                        }
                    });

                foreach (var entry in deleteEntryList)
                {
                    entry.Delete();
                }
            }

            if (nProcPngFiles == 0)
            {
                File.Delete(dstZipFilePath);
            }
            else if (execOptions.IsOverwrite)
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
            if (execOptions.IsDryRun)
            {
                _logger.Info("Elapsed time: {0:F3} seconds.", totalSw.ElapsedMilliseconds / 1000.0);
                File.Delete(dstZipFilePath);
            }
            else
            {
                var dstFileSize = new FileInfo(execOptions.IsOverwrite ? srcZipFilePath : dstZipFilePath).Length;
                _logger.Info(
                    "{0:F3} MiB -> {1:F3} MiB (deflated {2:F2}%, {3:F3} seconds)",
                    ToMiB(srcFileSize),
                    ToMiB(dstFileSize),
                    CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0,
                    totalSw.ElapsedMilliseconds / 1000.0);
            }
            if (execOptions.IsVerifyImage)
            {
                if (diffImageIndexNameList.Count == 0)
                {
                    _logger.Info("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    _logger.Warn("{0} / {1} PNG files are different image.", diffImageIndexNameList.Count, nProcPngFiles);
                    foreach (var (index, name) in diffImageIndexNameList)
                    {
                        Console.WriteLine($"Different image [{index}]: {name}");
                    }
                }
            }
            if (errorImageIndexNameList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageIndexNameList.Count);
                foreach (var (index, name) in errorImageIndexNameList)
                {
                    Console.WriteLine($"Error image [{index}]: {name}");
                }
            }
        }

        /// <summary>
        /// Re-compress all PNG files in zip archive using "zopfli" algorithm.
        /// </summary>
        /// <param name="srcGlbFilePath">Source GLB file.</param>
        /// <param name="dstGlbFilePath">Destination GLB file.</param>
        /// <param name="pngOptions">Options for zopflipng.</param>
        /// <param name="execOptions">Options for execution.</param>
        private static void RecompressPngInGlb(string srcGlbFilePath, string? dstGlbFilePath, ZopfliPngOptions pngOptions, ExecuteOptions execOptions)
        {
            dstGlbFilePath ??= Path.Combine(
                Path.GetDirectoryName(srcGlbFilePath) ?? ".",
                Path.GetFileNameWithoutExtension(srcGlbFilePath) + ".zopfli" + Path.GetExtension(srcGlbFilePath));

            if (File.Exists(dstGlbFilePath))
            {
                File.Delete(dstGlbFilePath);
            }

            var (glbHeader, glbChunks) = GlbUtil.ParseChunk(srcGlbFilePath);
            var (gltfJson, binaryBuffers, imageIndexes) = GlbUtil.ParseGltf(glbChunks);

            // Validate data length.
            var fileSize = new FileInfo(srcGlbFilePath).Length;
            if (glbHeader.Length != fileSize)
            {
                _logger.Warn($"The size described in the header differs from the actual file size. Expected: {glbHeader.Length} Bytes, Actual: {fileSize} Bytes");
            }
            var buffer0Length = (int)gltfJson["buffers"][0]["byteLength"];
            if (buffer0Length != glbChunks[1].Length)
            {
                _logger.Warn($"The size described in the buffers[0].byteLength in the glTF json differs from the size of data of second chunk. Expected: {buffer0Length} Bytes, Actual: {glbChunks[1].Length} Bytes");
            }

            int nProcPngFiles = 0;
            var srcFileSize = new FileInfo(srcGlbFilePath).Length;
            var diffImageIndexNameList = new List<ImageIndexNamePair>();
            var errorImageIndexNameList = new List<ImageIndexNamePair>();
            var totalSw = Stopwatch.StartNew();

            // Overwrite options.
            // PNG file in GLB file doesn't have its timestamp.
            execOptions = (ExecuteOptions)execOptions.Clone();
            execOptions.TextCreationTimeFormat = null;
            execOptions.IsAddTimeChunk = false;

            Parallel.ForEach(
                Partitioner.Create(imageIndexes, true),
                new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                imageIndex =>
                {
                    var data = binaryBuffers[imageIndex.Index];
                    if (imageIndex.MimeType != "image/png" || !HasPngSignature(data))
                    {
                        return;
                    }

                    var sw = Stopwatch.StartNew();
                    var procIndex = Interlocked.Increment(ref nProcPngFiles);

                    var displayName = $"[{imageIndex.Index}] {imageIndex.Name}";
                    try
                    {
                        if (execOptions.IgnoreSingleIdatSize >= 0)
                        {
                            var (count, totalIdatSize) = CountAndGetTotalSizeOfIdatChunks(data);
                            if (count == 1 && totalIdatSize >= execOptions.IgnoreSingleIdatSize)
                            {
                                _logger.Info("[{0}] Compress {1} ... Ignored (Single IDAT chunk)", procIndex, displayName);
                                return;
                            }
                        }

                        _logger.Info("[{0}] Compress {1} ...", procIndex, displayName);

                        // Take a long time
                        using var compressedData = ZopfliPng.OptimizePngUnmanaged(
                            data,
                            pngOptions,
                            execOptions.Verbose);

                        var pngDataSpan = ((long)compressedData.ByteLength < data.LongLength || execOptions.IsReplaceForce)
                            ? SpanUtil.CreateSpan(compressedData)
                            : data.AsSpan();
                        if (execOptions.IsModifyPng)
                        {
                            pngDataSpan = AddAdditionalChunks(pngDataSpan, execOptions);
                        }

                        if (!execOptions.IsDryRun)
                        {
                            binaryBuffers[imageIndex.Index] = pngDataSpan.ToArray();
                        }

                        var logLevel = pngDataSpan.Length < data.Length ? LogLevel.Info : LogLevel.Warn;
                        var verifyResultMsg = "";
                        if (execOptions.IsVerifyImage)
                        {
                            var result = BitmapUtil.CompareImage(data, pngDataSpan);
                            verifyResultMsg = $" ({result})";
                            if (result.Type != CompareResultType.Same)
                            {
                                logLevel = LogLevel.Warn;
                                lock (((ICollection)diffImageIndexNameList).SyncRoot)
                                {
                                    diffImageIndexNameList.Add(new ImageIndexNamePair(procIndex, displayName));
                                }
                            }
                        }
                        _logger.Log(
                            logLevel,
                            "[{0}] Compress {1} done: {2:F3} MiB -> {3:F3} MiB (deflated {4:F2}%, {5:F3} seconds){6}",
                            procIndex,
                            displayName,
                            ToMiB(data.LongLength),
                            ToMiB(pngDataSpan.Length),
                            CalcDeflatedRate(data.LongLength, pngDataSpan.Length) * 100.0,
                            sw.ElapsedMilliseconds / 1000.0,
                            verifyResultMsg);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            ex,
                            "[{0}] Compress {1} failed:",
                            procIndex,
                            displayName);
                        lock (((ICollection)errorImageIndexNameList).SyncRoot)
                        {
                            errorImageIndexNameList.Add(new ImageIndexNamePair(procIndex, displayName));
                        }
                    }
                });

            if (!execOptions.IsDryRun)
            {
                int nOffset = 0;
                for (int i = 0, cnt = binaryBuffers.Count; i < cnt; i++)
                {
                    var buffer = binaryBuffers[i];
                    var bufferView = gltfJson["bufferViews"][i];

                    // byteOffset must be multiply of 4.
                    bufferView["byteOffset"] = nOffset;
                    bufferView["byteLength"] = buffer.Length;
                    nOffset = RoundUpToMultiplyOf4(nOffset + buffer.Length);
                }

                gltfJson["buffers"][0]["byteLength"] = nOffset;
                var jsonString = MinifyJson(gltfJson.ToString());
                var jsonByteCount = Encoding.UTF8.GetByteCount(jsonString);
                var jsonAlignedByteCount = RoundUpToMultiplyOf4(jsonByteCount);
                var data = new byte[jsonAlignedByteCount];
#if NETCOREAPP2_1_OR_GREATER
                Encoding.UTF8.GetBytes(jsonString, data);
#else
                Encoding.UTF8.GetBytes(jsonString, 0, jsonString.Length, data, 0);
#endif  // NETCOREAPP2_1_OR_GREATER
                for (int i = jsonByteCount; i < jsonAlignedByteCount; i++)
                {
                    data[i] = (byte)' ';
                }

                glbChunks[0].Data = data;
                glbChunks[0].Length = data.Length;

                // Discard original binary data.
                glbChunks[1].Data = null;
                glbChunks[1].Length = nOffset;
                glbHeader.Length = sizeof(uint) * 3 + sizeof(uint) * 2 * 2 + glbChunks[0].Length + glbChunks[1].Length;

                using (var stream = new FileStream(dstGlbFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    // Write header.
                    glbHeader.WriteTo(stream);
                    // Write first chunk.
                    glbChunks[0].WriteTo(stream);
                    // Write second chunk.
                    glbChunks[1].WriteTo(stream);

#if NETCOREAPP2_1_OR_GREATER
                    ReadOnlySpan<byte> padding = stackalloc byte[4];
#else
                    var padding = new byte[4];
#endif  // NETCOREAPP2_1_OR_GREATER
                    nOffset = 0;
                    foreach (var buf in binaryBuffers)
                    {
                        stream.Write(buf, 0, buf.Length);
                        nOffset += buf.Length;
                        var alignedOffset = RoundUpToMultiplyOf4(nOffset);
                        var diff = alignedOffset - nOffset;
                        if (diff > 0)
                        {
#if NETCOREAPP2_1_OR_GREATER
                            stream.Write(padding.Slice(0, diff));
#else
                            stream.Write(padding, 0, diff);
#endif  // NETCOREAPP2_1_OR_GREATER
                        }
                        nOffset = alignedOffset;
                    }
                }

                if (execOptions.IsOverwrite)
                {
                    File.Delete(srcGlbFilePath);
                    File.Move(dstGlbFilePath, srcGlbFilePath);
                }
            }

            Console.WriteLine("- - -");
            if (nProcPngFiles == 0)
            {
                _logger.Info("No PNG file were processed.");
                return;
            }
            _logger.Info("All PNG files were proccessed ({0} files).", nProcPngFiles);
            if (execOptions.IsDryRun)
            {
                _logger.Info("Elapsed time: {0:F3} seconds.", totalSw.ElapsedMilliseconds / 1000.0);
            }
            else
            {
                var dstFileSize = new FileInfo(execOptions.IsOverwrite ? srcGlbFilePath : dstGlbFilePath).Length;
                _logger.Info(
                    "{0:F3} MiB -> {1:F3} MiB (deflated {2:F2}%, {3:F3} seconds)",
                    ToMiB(srcFileSize),
                    ToMiB(dstFileSize),
                    CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0,
                    totalSw.ElapsedMilliseconds / 1000.0);
            }
            if (execOptions.IsVerifyImage)
            {
                if (diffImageIndexNameList.Count == 0)
                {
                    _logger.Info("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    _logger.Warn("{0} / {1} PNG files are different image.", diffImageIndexNameList.Count, nProcPngFiles);
                    foreach (var (index, name) in diffImageIndexNameList)
                    {
                        Console.WriteLine($"Different image [{index}]: {name}");
                    }
                }
            }
            if (errorImageIndexNameList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageIndexNameList.Count);
                foreach (var (index, name) in errorImageIndexNameList)
                {
                    Console.WriteLine($"Error image [{index}]: {name}");
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
        private static void RecompressPngInDirectory(string srcDirPath, string? dstDirPath, ZopfliPngOptions pngOptions, ExecuteOptions execOptions)
        {
            dstDirPath ??= (srcDirPath + ".zopfli");

            var srcBaseDirFullPath = Path.GetFullPath(srcDirPath);
            var dstBaseDirFullPath = Path.GetFullPath(dstDirPath);

            var srcTotalFileSize = 0L;
            var dstTotalFileSize = 0L;

            int nProcPngFiles = 0;
            var diffImageIndexNameList = new List<ImageIndexNamePair>();
            var errorImageIndexNameList = new List<ImageIndexNamePair>();
            var totalSw = Stopwatch.StartNew();

            Parallel.ForEach(
                Partitioner.Create(Directory.EnumerateFiles(srcDirPath, "*.png", SearchOption.AllDirectories)),
                new ParallelOptions() { MaxDegreeOfParallelism = execOptions.NumberOfThreads },
                srcFilePath =>
                {
                    var sw = Stopwatch.StartNew();
                    var procIndex = Interlocked.Increment(ref nProcPngFiles);
                    var srcRelPath = GetRelativePath(srcBaseDirFullPath, srcFilePath);
                    var dstFilePath = execOptions.IsOverwrite ? srcFilePath : Path.Combine(
                        dstBaseDirFullPath,
                        new StringBuilder(Path.GetFullPath(srcFilePath))
                            .Replace(srcBaseDirFullPath + @"\", "", 0, srcBaseDirFullPath.Length + 1)
                            .ToString());

                    try
                    {
                        var data = File.ReadAllBytes(srcFilePath);
                        if (!HasPngSignature(data))
                        {
                            _logger.Error("[{0}] Compress {1} failed, invalid PNG signature", procIndex, srcRelPath);
                            return;
                        }

                        if (execOptions.IgnoreSingleIdatSize >= 0)
                        {
                            var (count, totalIdatSize) = CountAndGetTotalSizeOfIdatChunks(data);
                            if (count == 1 && totalIdatSize >= execOptions.IgnoreSingleIdatSize)
                            {
                                _logger.Info("[{0}] Compress {1} ... Ignored (Single IDAT chunk)", procIndex, srcRelPath);
                                return;
                            }
                        }

                        _logger.Info("[{0}] Compress {1} ...", procIndex, srcRelPath);

                        var originalTimestamp = new FileInfo(srcFilePath).LastWriteTime;

                        // Take a long time
                        using var compressedData = ZopfliPng.OptimizePngUnmanaged(
                            data,
                            pngOptions,
                            execOptions.Verbose);

                        var isReplace = (long)compressedData.ByteLength < data.LongLength || execOptions.IsReplaceForce;
                        var pngDataSpan = isReplace ? SpanUtil.CreateSpan(compressedData) : data.AsSpan();
                        if (execOptions.IsModifyPng)
                        {
                            pngDataSpan = AddAdditionalChunks(pngDataSpan, execOptions, originalTimestamp);
                        }

                        if (!execOptions.IsDryRun)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(dstFilePath) ?? ".");
                            var isWritten = true;
                            if (isReplace || execOptions.IsModifyPng)
                            {
                                using var fs = new FileStream(dstFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
#if NETCOREAPP2_1_OR_GREATER
                                fs.Write(pngDataSpan);
#else
                                var spanData = pngDataSpan.ToArray();
                                fs.Write(spanData, 0, spanData.Length);
#endif  // NETCOREAPP2_1_OR_GREATER
                            }
                            else if (srcFilePath != dstFilePath)
                            {
                                File.Copy(srcFilePath, dstFilePath, true);
                            }
                            else
                            {
                                isWritten = false;
                            }

                            if (execOptions.IsKeepTimestamp && isWritten)
                            {
                                new FileInfo(dstFilePath).LastWriteTime = originalTimestamp;
                            }
                        }

                        Interlocked.Add(ref srcTotalFileSize, data.Length);
                        Interlocked.Add(ref dstTotalFileSize, pngDataSpan.Length);

                        var logLevel = pngDataSpan.Length < data.Length ? LogLevel.Info : LogLevel.Warn;
                        var verifyResultMsg = "";
                        if (execOptions.IsVerifyImage)
                        {
                            var result = BitmapUtil.CompareImage(data, pngDataSpan);
                            verifyResultMsg = $" ({result})";
                            if (result.Type != CompareResultType.Same)
                            {
                                logLevel = LogLevel.Warn;
                                lock (((ICollection)diffImageIndexNameList).SyncRoot)
                                {
                                    diffImageIndexNameList.Add(new ImageIndexNamePair(procIndex, srcRelPath));
                                }
                            }
                        }
                        _logger.Log(
                            logLevel,
                            "[{0}] Compress {1} done: {2:F3} MiB -> {3:F3} MiB (deflated {4:F2}%, {5:F3} seconds){6}",
                            procIndex,
                            srcRelPath,
                            ToMiB(data.LongLength),
                            ToMiB(pngDataSpan.Length),
                            CalcDeflatedRate(data.LongLength, pngDataSpan.Length) * 100.0,
                            sw.ElapsedMilliseconds / 1000.0,
                            verifyResultMsg);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "[{0}] Compress {1} failed", procIndex, srcRelPath);
                        lock (((ICollection)errorImageIndexNameList).SyncRoot)
                        {
                            errorImageIndexNameList.Add(new ImageIndexNamePair(procIndex, srcRelPath));
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
            _logger.Info(
                "{0:F3} MiB -> {1:F3} MiB (deflated {2:F2}%, {3:F3} seconds)",
                ToMiB(srcTotalFileSize),
                ToMiB(dstTotalFileSize),
                CalcDeflatedRate(srcTotalFileSize, dstTotalFileSize) * 100.0,
                totalSw.ElapsedMilliseconds / 1000.0);
            if (execOptions.IsVerifyImage)
            {
                if (diffImageIndexNameList.Count == 0)
                {
                    _logger.Info("All the image data before and after re-compressing are the same.");
                }
                else
                {
                    _logger.Warn("{0} / {1} PNG files are different image.", diffImageIndexNameList.Count, nProcPngFiles);
                    foreach (var (index, name) in diffImageIndexNameList)
                    {
                        Console.WriteLine($"Different image [{index}]: {name}");
                    }
                }
            }
            if (errorImageIndexNameList.Count > 0)
            {
                _logger.Error("There are {0} PNG files that encountered errors during processing.", errorImageIndexNameList.Count);
                foreach (var (index, name) in errorImageIndexNameList)
                {
                    Console.WriteLine($"Error image [{index}]: {name}");
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
        /// Count PNG files and print its full name in the GLB file.
        /// </summary>
        /// <param name="vrmFilePath">Target GLB file.</param>
        private static void CountPngInGlb(string vrmFilePath)
        {
            var (_, glbChunks) = GlbUtil.ParseChunk(vrmFilePath);
            var (_, binaryBuffers, imageIndexes) = GlbUtil.ParseGltf(glbChunks);

            var totalPngFiles = 0;
            var totalPngFileSize = 0L;
            foreach (var imageIndex in imageIndexes)
            {
                var data = binaryBuffers[imageIndex.Index];
                if (imageIndex.MimeType != "image/png" || !HasPngSignature(data))
                {
                    continue;
                }

                Console.WriteLine($"[{imageIndex.Index}] {imageIndex.Name}: {ToMiB(data.Length):F3} MiB");
                totalPngFiles++;
                totalPngFileSize += data.Length;
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
                Console.WriteLine($"{GetRelativePath(dirFullPath, filePath)}: {ToMiB(fileSize):F3} MiB");
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
#if NETCOREAPP2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[4];
#else
            var buffer = new byte[4];
#endif  // NETCOREAPP2_1_OR_GREATER
            using (var fs = File.OpenRead(zipFilePath))
            {
#if NETCOREAPP2_1_OR_GREATER
                if (fs.Read(buffer) < buffer.Length)
#else
                if (fs.Read(buffer, 0, buffer.Length) < buffer.Length)
#endif  // NETCOREAPP2_1_OR_GREATER
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
        private static bool HasZipSignature(ReadOnlySpan<byte> data)
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
            return HasPngSignature(data.AsSpan());
        }

        /// <summary>
        /// Identify the specified binary data has a PNG signature or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a PNG signature, otherwise false.</returns>
        private static bool HasPngSignature(ReadOnlySpan<byte> data)
        {
            var pngSignature = PngSignature;
            if (data.Length < pngSignature.Length)
            {
                return false;
            }

            for (int i = 0; i < pngSignature.Length; i++)
            {
                if (data[i] != pngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// <para>Identify glTF file or not.</para>
        /// <para>Just determine if the first four bytes are 'g', 'l', 'T' and 'F'.</para>
        /// </summary>
        /// <param name="gltfFilePath">Target glTF file path,</param>
        /// <returns>True if specified file is a glTF file, otherwise false.</returns>
        private static bool IsGltfFile(string gltfFilePath)
        {
#if NETCOREAPP2_1_OR_GREATER
            Span<byte> buffer = stackalloc byte[GltfMagicBytes.Length];
#else
            var buffer = new byte[GltfMagicBytes.Length];
#endif  // NETCOREAPP2_1_OR_GREATER

            using (var fs = File.OpenRead(gltfFilePath))
            {
#if NETCOREAPP2_1_OR_GREATER
                if (fs.Read(buffer) < buffer.Length)
#else
                if (fs.Read(buffer, 0, buffer.Length) < buffer.Length)
#endif  // NETCOREAPP2_1_OR_GREATER
                {
                    return false;
                }
            }
            return HasGltfMagic(buffer);
        }

        /// <summary>
        /// Identify the specified binary data has a glTF magic number or not.
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <returns>True if the specified binary has a glTF magic number, otherwise false.</returns>
        private static bool HasGltfMagic(ReadOnlySpan<byte> data)
        {
            var gltfMagicBytes = GltfMagicBytes;
            if (data.Length < gltfMagicBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < gltfMagicBytes.Length; i++)
            {
                if (data[i] != gltfMagicBytes[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Count how many IDAT chunks are in the PNG data.
        /// </summary>
        /// <param name="pngData">Source PNG data.</param>
        /// <returns>Tuple of the number of IDAT chunks in specified PNG data and the total size of IDAT chunks.</returns>
        static (int Count, long TotalIdatSize) CountAndGetTotalSizeOfIdatChunks(ReadOnlySpan<byte> pngData)
        {
            unsafe
            {
                fixed (byte* p = pngData)
                {
                    using var ums = new UnmanagedMemoryStream(p, pngData.Length);
                    return CountIdatChunk(ums);
                }
            }
        }

        /// <summary>
        /// Count how many IDAT chunks are in the PNG stream.
        /// </summary>
        /// <param name="pngStream">Source PNG stream.</param>
        /// <returns>The number of IDAT chunks in specified PNG stream.</returns>
        static (int Count, long TotalIdatSize) CountIdatChunk(Stream pngStream)
        {
#if NETCOREAPP2_1_OR_GREATER
            Span<byte> pngSignature = stackalloc byte[PngSignature.Length];
            if (pngStream.Read(pngSignature) < pngSignature.Length)
#else
            var pngSignature = new byte[PngSignature.Length];
            if (pngStream.Read(pngSignature, 0, pngSignature.Length) < pngSignature.Length)
#endif  // NETCOREAPP2_1_OR_GREATER
            {
                throw new EndOfStreamException("Source PNG file data is too small.");
            }

            if (!HasPngSignature(pngSignature))
            {
                var sb = new StringBuilder();
                foreach (var b in pngSignature)
                {
                    sb.Append($" {b:2X}");
                }
                throw new InvalidDataException($"Invalid PNG signature:{sb}");
            }

            PngChunk pngChunk;
            int cnt = 0;
            long totalIdatSize = 0L;
            do
            {
                pngChunk = PngChunk.ReadOneChunk(pngStream);
                if (pngChunk.Type == ChunkTypeIdat)
                {
                    cnt++;
                    totalIdatSize += pngChunk.Data.LongLength;
                }
            }
            while (pngChunk.Type != ChunkTypeIend);

            return (cnt, totalIdatSize);
        }

        /// <summary>
        /// Split IDAT chunk or add tEXt chunk whose key is "Creation Time".
        /// </summary>
        /// <param name="pngData">Source PNG data.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <returns>Modified PNG data.</returns>
        private static Span<byte> AddAdditionalChunks(ReadOnlySpan<byte> pngData, ExecuteOptions execOptions)
        {
            return AddAdditionalChunks(pngData, execOptions, null);
        }

        /// <summary>
        /// Split IDAT chunk or add tEXt chunk whose key is "Creation Time" or add tIME chunk.
        /// </summary>
        /// <param name="pngData">Source PNG data.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <param name="createTime">Ceation time used for "Creation Time" of tEXt chunk and value of tIME chunk.</param>
        /// <returns>Modified PNG data.</returns>
        private static Span<byte> AddAdditionalChunks(ReadOnlySpan<byte> pngData, ExecuteOptions execOptions, DateTime? createTime)
        {
            using var oms = new MemoryStream(pngData.Length
                + (execOptions.IdatSize > 0 ? (pngData.Length / execOptions.IdatSize - 1) * 12 : 0)
                + (string.IsNullOrEmpty(execOptions.TextCreationTimeFormat) ? 0 : 256)
                + (execOptions.IsAddTimeChunk ? 19 : 0));
            unsafe
            {
                fixed (byte* p = pngData)
                {
                    using var ims = new UnmanagedMemoryStream(p, pngData.Length);
                    AddAdditionalChunks(ims, oms, execOptions, createTime);
                }
            }
            return SpanUtil.CreateSpan(oms);
        }

        /// <summary>
        /// Split IDAT chunk into the specified size.
        /// </summary>
        /// <param name="srcPngStream">Source PNG data stream.</param>
        /// <param name="dstPngStream">Destination data stream.</param>
        /// <param name="execOptions">Options for execution.</param>
        /// <param name="createTime">Ceation time used for "Creation Time" of tEXt chunk and value of tIME chunk.</param>
        private static void AddAdditionalChunks(Stream srcPngStream, Stream dstPngStream, ExecuteOptions execOptions, DateTime? createTime)
        {
#if NETCOREAPP2_1_OR_GREATER
            Span<byte> pngSignature = stackalloc byte[PngSignature.Length];
            if (srcPngStream.Read(pngSignature) < pngSignature.Length)
#else
            var pngSignature = new byte[PngSignature.Length];
            if (srcPngStream.Read(pngSignature, 0, pngSignature.Length) < pngSignature.Length)
#endif  // NETCOREAPP2_1_OR_GREATER
            {
                throw new EndOfStreamException("Source PNG file data is too small.");
            }

            if (!HasPngSignature(pngSignature))
            {
                var sb = new StringBuilder();
                foreach (var b in pngSignature)
                {
                    sb.Append($" {b:2X}");
                }
                throw new InvalidDataException($"Invalid PNG signature:{sb}");
            }

            // Write PNG Signature
            dstPngStream.Write(PngSignature, 0, PngSignature.Length);

            using var bw = new BinaryWriter(dstPngStream, Encoding.ASCII, true);
            PngChunk pngChunk;
            var hasTimeChunk = false;
            var hasTextCreationTime = false;
            do
            {
                pngChunk = PngChunk.ReadOneChunk(srcPngStream);
                if (pngChunk.Type == ChunkTypeIdat && execOptions.IdatSize > 0)
                {
                    using var idatMs = new MemoryStream((int)srcPngStream.Length);
                    do
                    {
                        idatMs.Write(pngChunk.Data, 0, pngChunk.Data.Length);
                        pngChunk = PngChunk.ReadOneChunk(srcPngStream);
                    }
                    while (pngChunk.Type == ChunkTypeIdat);
                    idatMs.Position = 0;

                    var idatData = new byte[Math.Min(execOptions.IdatSize, idatMs.Length)];
                    var chunkTypeDataIdat = Encoding.ASCII.GetBytes(ChunkTypeIdat);
                    for (var nRead = idatMs.Read(idatData, 0, idatData.Length); nRead != 0; nRead = idatMs.Read(idatData, 0, idatData.Length))
                    {
                        bw.Write(BinaryPrimitives.ReverseEndianness(nRead));
                        bw.Write(chunkTypeDataIdat);
                        bw.Write(idatData, 0, nRead);

                        var crc32 = Crc32Util.Update(chunkTypeDataIdat);
                        crc32 = Crc32Util.Update(idatData, 0, nRead, crc32);
                        crc32 = Crc32Util.Finalize(crc32);
                        bw.Write(BinaryPrimitives.ReverseEndianness(crc32));
                    }
                }

                if (pngChunk.Type == ChunkNameText)
                {
                    // May be thrown ArgumentOutOfRangeException if null character is not found.
                    var key = Encoding.ASCII.GetString(
                        pngChunk.Data,
                        0,
                        Array.IndexOf(pngChunk.Data, (byte)0, 0, pngChunk.Data.Length));
                    if (key == TextChunkKeyCreationTime)
                    {
                        hasTextCreationTime = true;
                    }
                }
                else if (pngChunk.Type == ChunkNameTime)
                {
                    hasTimeChunk = true;
                }
                else if (pngChunk.Type == ChunkTypeIend)
                {
                    // Insert tEXt and tIME chunks before IEND.
                    if (createTime.HasValue)
                    {
                        if (!hasTextCreationTime && !string.IsNullOrEmpty(execOptions.TextCreationTimeFormat))
                        {
                            PngChunk.WriteTextChunk(
                                dstPngStream,
                                TextChunkKeyCreationTime,
                                createTime.Value.ToString(execOptions.TextCreationTimeFormat));
                        }
                        if (!hasTimeChunk && execOptions.IsAddTimeChunk)
                        {
                            PngChunk.WriteTimeChunk(dstPngStream, createTime.Value);
                        }
                    }
                }
                pngChunk.WriteTo(dstPngStream);
            }
            while (pngChunk.Type != ChunkTypeIend);
        }

        /// <summary>
        /// Convert path to relative path.
        /// </summary>
        /// <param name="relativeTo">The source path the output should be relative to. This path is always considered to be a directory.</param>
        /// <param name="path">The destination path.</param>
        /// <returns>The relative path or <paramref name="path"/> if the paths don't share the same root.</returns>
        private static string GetRelativePath(string relativeTo, string path)
        {
#if NETCOREAPP2_0_OR_GREATER
            return Path.GetRelativePath(relativeTo, path);
#else
            var fullPath = Path.GetFullPath(path).Replace('/', '\\').TrimEnd('\\');
            var fullRelativeTo = Path.GetFullPath(relativeTo).Replace('/', '\\').TrimEnd('\\');

            var pathParts = fullPath.Split('\\');
            var relToParts = fullRelativeTo.Split('\\');

            int commonPrefixCount = 0;
            for (int i = 0; i < Math.Min(pathParts.Length, relToParts.Length); i++)
            {
                if (!string.Equals(pathParts[i], relToParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                commonPrefixCount++;
            }

            var relativePathParts = new List<string>(Math.Max(0, relToParts.Length - commonPrefixCount) + Math.Max(0, pathParts.Length - commonPrefixCount));
            for (int i = commonPrefixCount; i < relToParts.Length; i++)
            {
                relativePathParts.Add("..");
            }
            for (int i = commonPrefixCount; i < pathParts.Length; i++)
            {
                relativePathParts.Add(pathParts[i]);
            }

            var relativePath = string.Join("\\", relativePathParts);

            return relativePath.Length == 0 ? "." : relativePath;
#endif  // NETCOREAPP2_0_OR_GREATER
        }

        /// <summary>
        /// Read all data from <see cref="ZipArchiveEntry"/>.
        /// </summary>
        /// <param name="entry">Target <see cref="ZipArchiveEntry"/>.</param>
        /// <param name="lockObj">The object for lock.</param>
        /// <returns>Read data.</returns>
        private static byte[] ReadAllBytes(ZipArchiveEntry entry, Lock lockObj)
        {
            using var ms = new MemoryStream((int)entry.Length);
            lock (lockObj)
            {
                using var zs = entry.Open();
                zs.CopyTo(ms);
            }
            return ms.Length == entry.Length ? ms.GetBuffer() : ms.ToArray();
        }

        /// <summary>
        /// <para>Read all data from <see cref="ZipArchiveEntry"/>.</para>
        /// <para>This method does the locking from the time of memory allocation
        /// to prevent access to the Length property while writing to a Zip archive.</para>
        /// </summary>
        /// <param name="entry">Target <see cref="ZipArchiveEntry"/>.</param>
        /// <param name="lockObj">The object for lock.</param>
        /// <returns>Read data.</returns>
        private static (byte[] Data, int Length) ReadAllBytesRestrict(ZipArchiveEntry entry, Lock lockObj)
        {
            using var ms = new MemoryStream(4 * 1024 * 1024);
            lock (lockObj)
            {
                using var zs = entry.Open();
                zs.CopyTo(ms);
            }
            return (ms.GetBuffer(), (int)ms.Length);
        }

        /// <summary>
        /// Create new entry in <see cref="ZipArchive"/> and write data to it.
        /// </summary>
        /// <param name="archive">Target zip archive.</param>
        /// <param name="entryName">New entry name.</param>
        /// <param name="data">The data to write.</param>
        /// <param name="lockObj">The object for lock.</param>
        /// <param name="timestamp">Timestamp for new entry.</param>
        private static void CreateEntryAndWriteData(ZipArchive archive, string entryName, ReadOnlySpan<byte> data, Lock lockObj, DateTimeOffset? timestamp = null)
        {
            lock (lockObj)
            {
                var dstEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                if (timestamp.HasValue)
                {
                    dstEntry.LastWriteTime = timestamp.Value;
                }
                using var dstZs = dstEntry.Open();
#if NETCOREAPP2_1_OR_GREATER
                dstZs.Write(data);
#else
                var dataArray = data.ToArray();
                dstZs.Write(dataArray, 0, dataArray.Length);
#endif  // NETCOREAPP2_1_OR_GREATER
            }
        }

        /// <summary>
        /// Round up to multiply of 4.
        /// </summary>
        /// <param name="n">A value to round up.</param>
        /// <returns>Rounded up value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RoundUpToMultiplyOf4(int n)
        {
            return (n + 0x3) & ~0x3;
        }

        /// <summary>
        /// Minify json, remove unneccessary whitespaces.
        /// </summary>
        /// <param name="json">A json to minify.</param>
        /// <returns>Minified json.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string MinifyJson(string json)
        {
            return RegexHelper.MinifyJsonPatternRegex.Replace(json, "$1");
        }

        /// <summary>
        /// Converts a number in bytes to a number in MiB.
        /// </summary>
        /// <param name="byteSize">A number in bytes.</param>
        /// <returns>A number in MiB.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double CalcDeflatedRate(long originalSize, long compressedSize)
        {
            return originalSize == 0L ? 0.0 : (1.0 - (double)compressedSize / originalSize);
        }

        /// <summary>
        /// Native methods.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
#if SUPPORT_LIBRARY_IMPORT
        internal static partial class UnsafeNativeMethods
#else
        internal static class UnsafeNativeMethods
#endif  // SUPPORT_LIBRARY_IMPORT
        {
            /// <summary>
            /// Adds a directory to the process DLL search path.
            /// </summary>
            /// <param name="path">Path to DLL directory.</param>
            /// <returns>
            /// <para>If the function succeeds, the return value is an opaque pointer that can be passed
            /// to <see href="https://learn.microsoft.com/en-us/windows/desktop/api/libloaderapi/nf-libloaderapi-removedlldirectory">RemoveDllDirectory</see>
            /// to remove the DLL from the process DLL search path.</para>
            /// <para>If the function fails, the return value is zero.
            /// To get extended error information, call <see cref="Marshal.GetLastWin32Error"/>.</para>
            /// </returns>
            /// <remarks>
            /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-adddlldirectory"/>
            /// </remarks>
#if SUPPORT_LIBRARY_IMPORT
            [LibraryImport("kernel32.dll", EntryPoint = nameof(AddDllDirectory), StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
            public static partial IntPtr AddDllDirectory(string path);
#else
            [DllImport("kernel32.dll", EntryPoint = nameof(AddDllDirectory), ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr AddDllDirectory(string path);
#endif  // SUPPORT_LIBRARY_IMPORT
            /// <summary>
            /// Specifies a default set of directories to search when the calling process loads a DLL.
            /// This search path is used when <see href="https://learn.microsoft.com/en-us/windows/desktop/api/libloaderapi/nf-libloaderapi-loadlibraryexa">LoadLibraryEx</see> is called
            /// with no <see cref="LoadLibrarySearchFlags"/> flags.
            /// </summary>
            /// <param name="directoryFlags">The directories to search. This parameter can be any combination of the <see cref="LoadLibrarySearchFlags"/> values.</param>
            /// <returns>
            /// <para>If the function succeeds, the return value is true.</para>
            /// <para>If the function fails, the return value is false. To get extended error information, call <see cref="Marshal.GetLastWin32Error"/>.</para>
            /// </returns>
            /// <remarks>
            /// <see href="https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-setdefaultdlldirectories"/>
            /// </remarks>
#if SUPPORT_LIBRARY_IMPORT
            [LibraryImport("kernel32.dll", EntryPoint = nameof(SetDefaultDllDirectories), SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool SetDefaultDllDirectories(LoadLibrarySearchFlags directoryFlags);
#else
            [DllImport("kernel32.dll", EntryPoint = nameof(SetDefaultDllDirectories), ExactSpelling = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetDefaultDllDirectories(LoadLibrarySearchFlags directoryFlags);
#endif  // SUPPORT_LIBRARY_IMPORT
        }

        /// <summary>
        /// Flag values for <see cref="UnsafeNativeMethods.SetDefaultDllDirectories(LoadLibrarySearchFlags)"/>.
        /// </summary>
        [Flags]
        internal enum LoadLibrarySearchFlags
        {
            /// <summary>
            /// If this value is used, the application's installation directory is searched.
            /// </summary>
            ApplicationDir = 0x00000200,
            /// <summary>
            /// <para>If this value is used, any path explicitly added using the AddDllDirectory or SetDllDirectory function is searched.</para>
            /// <para>If more than one directory has been added, the order in which those directories are searched is unspecified.</para>
            /// </summary>
            UserDirs = 0x00000400,
            /// <summary>
            /// If this value is used, %windows%\system32 is searched.
            /// </summary>
            System32 = 0x00000800,
            /// <summary>
            /// <para>This value is a combination of <see cref="ApplicationDir"/>, <see cref="System32"/>, and <see cref="UserDirs"/>.</para>
            /// <para>This value represents the recommended maximum number of directories an application should include in its DLL search path.</para>
            /// </summary>
            DefaultDirs = 0x00001000
        }
    }
}
