using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

            var srcZipFile = args[0];
            var dstZipFile = srcZipFile + ".zopfli";

            if (File.Exists(dstZipFile))
            {
                File.Delete(dstZipFile);
            }

            int nProcPngFiles = 0;
            var totalSw = Stopwatch.StartNew();
            using (var srcArchive = ZipFile.OpenRead(srcZipFile))
            using (var dstArchive = ZipFile.Open(dstZipFile, ZipArchiveMode.Update))
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

                        Console.WriteLine($"[{threadId}] Compress {srcEntry.FullName} done: {sw.ElapsedMilliseconds / 1000.0:F3} ms {ToMiB(data.Length):F3} MB -> {ToMiB(compressedData.Length):F3} MB (deflated {CalcDeflatedRate(data.Length, compressedData.Length) * 100.0:F2}%)");
                    });
            }

            var srcFileSize = new FileInfo(srcZipFile).Length;
            var dstFileSize = new FileInfo(dstZipFile).Length;

            Console.WriteLine($"All PNG files were proccessed ({nProcPngFiles} files).");
            Console.WriteLine($"Elapsed time: {totalSw.ElapsedMilliseconds / 1000.0:F3} ms, {ToMiB(srcFileSize):F3} MB -> {ToMiB(dstFileSize):F3} MB (deflated {CalcDeflatedRate(srcFileSize, dstFileSize) * 100.0:F2}%)");

            MoveFileForce(
                srcZipFile,
                Path.Combine(Path.GetDirectoryName(srcZipFile), Path.GetFileNameWithoutExtension(srcZipFile) + ".old.zip"));
            MoveFileForce(
                dstZipFile,
                Path.Combine(Path.GetDirectoryName(dstZipFile), Path.GetFileNameWithoutExtension(dstZipFile)));

            return 0;
        }

        private static void MoveFileForce(string srcFileName, string dstFileName)
        {
            if (File.Exists(dstFileName))
            {
                File.Delete(dstFileName);
            }
            File.Move(srcFileName, dstFileName);
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
