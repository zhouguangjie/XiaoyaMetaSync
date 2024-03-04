using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Text;

namespace XiaoyaMetaSync
{
    internal class Program
    {
        private static readonly string LOG_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiaoyaMetaSync", "Log");

        public static string LOG_FILE { get; private set; }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: command <xiaoya meta zip> <output path> [<strm file host> <new strm file host>]");
                return;
            }


            var zipPath = args[0];
            var extractPath = args[1];
            var strmXiaoyaUrlHost = args.Length > 2 ? args[2] : null;
            var replaceStrmXiaoyaUrlHost = args.Length > 3 ? args[3] : null;

            if (!File.Exists(zipPath))
            {
                Console.WriteLine($"Zip File Not Exists:{zipPath}");
                return;
            }
            if (!Directory.Exists(LOG_DIR))
                Directory.CreateDirectory(LOG_DIR);
            LOG_FILE = Path.Combine(LOG_DIR, DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
            LogLine($"ZipPath:{zipPath}");
            LogLine($"MetaOutput:{extractPath}");
            SyncXiaoyaMetaWithMetaZip(zipPath, extractPath, strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);

        }

        private static void LogLine(string line, bool writeLogFile = true)
        {
            Console.WriteLine(line);
            if (writeLogFile)
            {
                File.AppendAllLines(LOG_FILE, [line]);
            }
        }

        private static void SyncXiaoyaMetaWithMetaZip(string metaZipPath, string xiaoyaMetaOutputPath, string? strmXiaoyaUrlHost, string? replaceStrmXiaoyaUrlHost)
        {
            var replaceStrmUrlHost = !string.IsNullOrWhiteSpace(strmXiaoyaUrlHost) && !string.IsNullOrWhiteSpace(replaceStrmXiaoyaUrlHost);
            MemoryStream writeFileStream = new MemoryStream();
            var options = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding(Encoding.UTF8, Encoding.UTF8),
                LookForHeader = true,
            };
#if DEBUG
            var cnt = 0;
#endif
            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = SevenZipArchive.Open(xystrm))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory && entry.Size > 0)
                    {
#if DEBUG
                        if (cnt++ > 20) return;
#endif
                        var relativeFileName = entry.Key.Replace(" \\", "\\").Replace(" /", "/");
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            LogLine($"Skipped Exists:{relativeFileName}", false);
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);


                            if (replaceStrmUrlHost && relativeFileName.EndsWith(".strm"))
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                entry.WriteTo(writeFileStream);
                                var fileBytes = new byte[entry.Size];
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                writeFileStream.Read(fileBytes, 0, fileBytes.Length);

                                var strmContent = Encoding.UTF8.GetString(fileBytes, 0, fileBytes.Length).Replace(strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                                File.WriteAllText(extractedFilePath, strmContent);
                                LogLine($"Replaced Strm File:{relativeFileName}", false);
                            }
                            else
                            {
                                entry.WriteToFile(extractedFilePath);
                            }
                            LogLine($"New File:{relativeFileName}");
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
                    Console.WriteLine($"Xiaoya Meta Start At:{fileStartIndex}");
                    return;
                }
                else
                {
                    fileStream.Seek(-5, SeekOrigin.Current);
                }
            }
            throw new Exception("Invalid 7z File");
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
