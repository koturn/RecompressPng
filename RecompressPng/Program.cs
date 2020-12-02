using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibZopfliSharp;


namespace RecompressPng
{
    class Program
    {
        const int DefaultReadCapacitySize = 4 * 1024 * 1024;

        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Please specify target zip file");
                return 1;
            }

            if (File.Exists(args[0]))
            {
                if (!IsZipFile(args[0]))
                {
                    Console.Error.WriteLine("Specified file is not zip archive");
                    return 1;
                }
                else
                {
                    RecompressPngInZipArchive(args[0]);
                }
            }
            else if (Directory.Exists(args[0]))
            {
                RecompressPngInDirectory(args[0]);
            }
            else
            {
                Console.Error.WriteLine("Specified file doesn't exist");
                return 1;
            }

            return 0;
        }

        private static void RecompressPngInZipArchive(string srcZipFilePath, string dstZipFilePath = null)
        {
            if (dstZipFilePath == null)
            {
                dstZipFilePath = srcZipFilePath + ".zopfli";
            }

            if (File.Exists(dstZipFilePath))
            {
                File.Delete(dstZipFilePath);
            }


            int nProcPngFiles = 0;
            var totalSw = Stopwatch.StartNew();
            using (var srcArchive = ZipFile.OpenRead(srcZipFilePath))
            using (var dstArchive = ZipFile.Open(dstZipFilePath, ZipArchiveMode.Update))
            {
                var srcLock = new object();
                var dstLock = new object();
                Parallel.ForEach(
                    srcArchive.Entries.Where(entry => entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)),
                    new ParallelOptions() { MaxDegreeOfParallelism = -1 },
                    srcEntry =>
                    {
                        var sw = Stopwatch.StartNew();

                        var threadId = Thread.CurrentThread.ManagedThreadId;
                        Console.WriteLine($"[{threadId}] Compress {srcEntry.FullName} ...");

                        byte[] data;
                        using (var ms = new MemoryStream(DefaultReadCapacitySize))
                        {
                            lock (srcLock)
                            {
                                using (var srcZs = srcEntry.Open())
                                {
                                    srcZs.CopyTo(ms);
                                }
                            }
                            data = ms.ToArray();
                        }

                        // Take a long time
                        var compressedData = ZopfliPNG.compress(data);

                        lock (dstLock)
                        {
                            var dstEntry = dstArchive.CreateEntry(srcEntry.FullName);
                            using (var dstZs = dstEntry.Open())
                            {
                                dstZs.Write(compressedData, 0, compressedData.Length);
                            }
                            // Keep original timestamp
                            dstEntry.LastWriteTime = srcEntry.LastWriteTime;
                            nProcPngFiles++;
                        }

                        Console.WriteLine($"[{threadId}] Compress {srcEntry.FullName} done: {sw.ElapsedMilliseconds / 1000.0:F3} ms, {ToMiB(data.Length):F3} MB -> {ToMiB(compressedData.Length):F3} MB (deflated {CalcDeflatedRate(data.Length, compressedData.Length) * 100.0:F2}%)");
                    });
            }

            var srcFileSize = new FileInfo(srcZipFilePath).Length;
            var dstFileSize = new FileInfo(dstZipFilePath).Length;

            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} ms, {ToMiB(srcFileSize):F3} MB -> {ToMiB(dstFileSize):F3} MB (deflated {CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0:F2}%)");

            MoveFileForce(
                srcZipFilePath,
                Path.Combine(Path.GetDirectoryName(srcZipFilePath), Path.GetFileNameWithoutExtension(srcZipFilePath) + ".old.zip"));
            MoveFileForce(
                dstZipFilePath,
                Path.Combine(Path.GetDirectoryName(dstZipFilePath), Path.GetFileNameWithoutExtension(dstZipFilePath)));
        }


        private static void RecompressPngInDirectory(string srcDirPath, string dstDirPath = null)
        {
            if (dstDirPath == null)
            {
                dstDirPath = srcDirPath + ".zopfli";
            }

            if (File.Exists(dstDirPath))
            {
                File.Delete(dstDirPath);
            }

            var srcBaseDirFullPath = Path.GetFullPath(srcDirPath);
            var dstBaseDirFullPath = Path.GetFullPath(dstDirPath);

            var srcTotalFileSize = 0L;
            var dstTotalFileSize = 0L;

            int nProcPngFiles = 0;
            var totalSw = Stopwatch.StartNew();

            Parallel.ForEach(
                Directory.EnumerateFiles(srcDirPath, "*.png", SearchOption.AllDirectories),
                new ParallelOptions() { MaxDegreeOfParallelism = -1 },
                srcFilePath =>
                {
                    var sw = Stopwatch.StartNew();

                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    Console.WriteLine($"[{threadId}] Compress {srcFilePath} ...");

                    var dstFilePath = Path.Combine(
                        dstBaseDirFullPath,
                        new StringBuilder(Path.GetFullPath(srcFilePath))
                            .Replace(srcBaseDirFullPath + @"\", "", 0, srcBaseDirFullPath.Length + 1)
                            .ToString());

                    Directory.CreateDirectory(Path.GetDirectoryName(dstFilePath));

                    var data = File.ReadAllBytes(srcFilePath);

                    // Take a long time
                    var compressedData = ZopfliPNG.compress(data);

                    File.WriteAllBytes(dstFilePath, compressedData);

                    // Keep original timestamp
                    new FileInfo(dstFilePath).LastWriteTime = new FileInfo(srcFilePath).LastWriteTime;

                    Interlocked.Add(ref srcTotalFileSize, data.Length);
                    Interlocked.Add(ref dstTotalFileSize, compressedData.Length);
                    Interlocked.Increment(ref nProcPngFiles);

                    Console.WriteLine($"[{threadId}] Compress {srcFilePath} done: {sw.ElapsedMilliseconds / 1000.0:F3} ms, {ToMiB(data.Length):F3} MB -> {ToMiB(compressedData.Length):F3} MB (deflated {CalcDeflatedRate(data.Length, compressedData.Length) * 100.0:F2}%)");
                });

            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} ms, {ToMiB(srcTotalFileSize):F3} MB -> {ToMiB(dstTotalFileSize):F3} MB (deflated {CalcDeflatedRate(srcTotalFileSize, dstTotalFileSize) * 100.0:F2}%)");

            MoveDirectoryForce(
                srcDirPath,
                Path.Combine(Path.GetDirectoryName(srcDirPath), Path.GetFileNameWithoutExtension(srcDirPath) + ".old"));
            MoveDirectoryForce(
                dstDirPath,
                Path.Combine(Path.GetDirectoryName(dstDirPath), Path.GetFileNameWithoutExtension(dstDirPath)));
        }

        private static bool IsZipFile(string zipFilePath)
        {
            var buffer = new byte[2];
            using (var fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Read(buffer, 0, buffer.Length);
            }
            return buffer[0] == 'P' && buffer[1] == 'K';
        }


        private static void MoveFileForce(string srcFileName, string dstFileName)
        {
            if (File.Exists(dstFileName))
            {
                File.Delete(dstFileName);
            }
            File.Move(srcFileName, dstFileName);
        }

        private static void MoveDirectoryForce(string srcFileName, string dstFileName)
        {
            if (Directory.Exists(dstFileName))
            {
                Directory.Delete(dstFileName);
            }
            Directory.Move(srcFileName, dstFileName);
        }

        private static double ToMiB(long byteSize)
        {
            return (double)byteSize / 1024.0 / 1024.0;
        }

        private static double CalcDeflatedRate(long originalSize, long compressedSize)
        {
            return 1.0 - (double)compressedSize / (double)originalSize;
        }
    }
}
