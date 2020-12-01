using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using LibZopfliSharp;


namespace RecompressPng
{
    class Program
    {
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

            var totalSw = Stopwatch.StartNew();
            using (var srcArchive = ZipFile.OpenRead(srcZipFile))
            using (var dstArchive = ZipFile.Open(dstZipFile, ZipArchiveMode.Update))
            {
                foreach (var srcEntry in srcArchive.Entries)
                {
                    if (!srcEntry.FullName.EndsWith(".png"))
                    {
                        Console.WriteLine($"Non target: {srcEntry.FullName}");
                        continue;
                    }
                    Console.WriteLine($"Compress {srcEntry.FullName} ...");

                    var sw = Stopwatch.StartNew();
                    byte[] data;
                    using (var ms = new MemoryStream(4 * 1024 * 1024))
                    {
                        using (var srcZs = srcEntry.Open())
                        {
                            srcZs.CopyTo(ms);
                        }
                        data = ms.ToArray();
                    }

                    var dstEntry = dstArchive.CreateEntry(srcEntry.FullName);
                    using (var dstZs = dstEntry.Open())
                    using (var zopfliStream = new ZopfliPNGStream(dstZs))
                    {
                        zopfliStream.Write(data, 0, data.Length);
                    }
                    dstEntry.LastWriteTime = srcEntry.LastWriteTime;

                    Console.WriteLine($"Compress {srcEntry.FullName} done: {sw.ElapsedMilliseconds} ms");
                }
            }
            Console.WriteLine($"All PNG file was proccessed. Elapsed time: {totalSw.ElapsedMilliseconds} ms");

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
    }
}
