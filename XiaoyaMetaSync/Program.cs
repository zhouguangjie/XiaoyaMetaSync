using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using System.Text;

namespace XiaoyaMetaSync
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var zipPath = args[0];
            var extractPath = args[1];
            var strmXiaoyaUrlHost = args.Length > 2 ? args[2] : null;
            var replaceStrmXiaoyaUrlHost = args.Length > 3 ? args[3] : null;
            Console.WriteLine($"CurrentPath:{Directory.GetCurrentDirectory()}");
            Console.WriteLine($"ZipPath:{zipPath}");
            Console.WriteLine($"ZipPath:{extractPath}");

            if (!File.Exists(zipPath))
            {
                Console.WriteLine($"Zip File Not Exists:{zipPath}");
                return;
            }
            SyncXiaoyaMetaWithMetaZip(zipPath, extractPath, strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);

        }
        private static void SyncXiaoyaMetaWithMetaZip(string metaZipPath, string xiaoyaMetaOutputPath, string? strmXiaoyaUrlHost, string? replaceStrmXiaoyaUrlHost)
        {
            var replaceStrmUrlHost = !string.IsNullOrWhiteSpace(strmXiaoyaUrlHost) && !string.IsNullOrWhiteSpace(replaceStrmXiaoyaUrlHost);
            MemoryStream tmpStrmBuf = null;
            if (replaceStrmUrlHost) tmpStrmBuf = new MemoryStream();
            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = SevenZipArchive.Open(xystrm))
            {
                foreach (var entry in archive.Entries)
                {
                    Console.WriteLine(entry.Key);
                    string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, entry.Key);
                    if (entry.IsDirectory)
                    {
                        if (!Directory.Exists(extractedFilePath))
                            Directory.CreateDirectory(extractedFilePath);
                    }
                    else if (!File.Exists(extractedFilePath))
                    {
                        if (replaceStrmUrlHost && entry.Key.EndsWith(".strm"))
                        {
                            tmpStrmBuf.Seek(0, SeekOrigin.Begin);
                            entry.WriteTo(tmpStrmBuf);
                            var strmBytes = new byte[entry.Size];
                            tmpStrmBuf.Seek(0, SeekOrigin.Begin);
                            tmpStrmBuf.Read(strmBytes, 0, strmBytes.Length);
                            var strmContent = Encoding.UTF8.GetString(strmBytes, 0, strmBytes.Length).Replace(strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                            File.WriteAllText(extractedFilePath, strmContent);
                        }
                        else
                        {
                            entry.WriteToFile(extractedFilePath);
                        }
                    }

                }
            }
        }

    }
    class XiaoyaMetaZipStream : Stream
    {
        private FileStream fsInput;
        private long fileStartIndex;
        public override bool CanRead => fsInput.CanRead;

        public override bool CanSeek => fsInput.CanSeek;

        public override bool CanWrite => fsInput.CanWrite;

        public override long Length => fsInput.Length - fileStartIndex;

        public override long Position { get => fsInput.Position - fileStartIndex; set => fsInput.Seek(value + fileStartIndex, SeekOrigin.Begin); }

        public XiaoyaMetaZipStream(FileStream fileStream)
        {
            var buf = new byte[6];
            var pattern = new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
            while (fileStream.Read(buf, 0, 6) == 6)
            {
                if (Enumerable.SequenceEqual(buf, pattern))
                {
                    fileStream.Seek(-6, SeekOrigin.Current);
                    fsInput = fileStream;
                    fileStartIndex = fileStream.Position;
                    Console.WriteLine($"7z File Start:{fileStartIndex}");
                    return;
                }
                else
                {
                    fileStream.Seek(-5, SeekOrigin.Current);
                }
            }
            throw new Exception("Not a 7z file stream");
        }

        public override void Flush()
        {
            fsInput.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return fsInput.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    fsInput.Seek(fileStartIndex + offset, origin);
                    break;
                case SeekOrigin.Current:
                    fsInput.Seek(offset, origin);
                    break;
                case SeekOrigin.End:
                    fsInput.Seek(offset, origin);
                    break;
                default:
                    break;
            }
            return Position;
        }

        public override void SetLength(long value)
        {
            fsInput.SetLength(value + fileStartIndex);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            fsInput.Write(buffer, offset, count);
        }
    }
}
