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
            try
            {
                //SyncXiaoyaMetaTooSlow(zipPath, extractPath, strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                //SyncXiaoyaMetaSlow(zipPath, extractPath, strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                SyncXiaoyaMetaFast(zipPath, extractPath, strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
            }
            catch (Exception ex)
            {
                LogLine(ex.Message);
                LogLine(ex.ToString());
            }

        }

        private static void LogLine(string line)
        {
            File.AppendAllLines(LOG_FILE, [line]);
        }

        private static void SyncXiaoyaMetaTooSlow(string metaZipPath, string xiaoyaMetaOutputPath, string? strmXiaoyaUrlHost, string? replaceStrmXiaoyaUrlHost)
        {
            var replaceStrmUrlHost = !string.IsNullOrWhiteSpace(strmXiaoyaUrlHost) && !string.IsNullOrWhiteSpace(replaceStrmXiaoyaUrlHost);

            var options = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 },
                DisableCheckIncomplete = true,
                LeaveStreamOpen = true,
            };
            var cnt = 0;
            var fileCnt = 0;
            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = SevenZipArchive.Open(xystrm, options))
            using (var writeFileStream = new MemoryStream())
            {
                var total = archive.Entries.Count;
                foreach (var entry in archive.Entries)
                {
                    cnt++;
                    if (!entry.IsDirectory && entry.Size > 0)
                    {
                        fileCnt++;
#if DEBUG
                        //if (fileCnt > 100) return;
#endif
                        var relativeFileName = entry.Key.Replace(" \\", "\\").Replace(" /", "/").Replace("\\ ", "\\").Replace("/ ", "/").Trim();
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            Console.WriteLine($"[{cnt}/{total}]Skipped Exists:{relativeFileName}");
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            Console.WriteLine($"[{cnt}/{total}]New File:{relativeFileName}");
                            writeFileStream.Seek(0, SeekOrigin.Begin);
                            var startDate = DateTime.Now;
                            entry.WriteTo(writeFileStream);
                            var duration = (int)(DateTime.Now - startDate).TotalSeconds;
                            Console.WriteLine($"[{cnt}/{total}]Expanded({duration}s):{relativeFileName}");
                            if (replaceStrmUrlHost && relativeFileName.EndsWith(".strm"))
                            {
                                var strmContent = Encoding.UTF8.GetString(writeFileStream.GetBuffer(), 0, (int)entry.Size).Replace(strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                                File.WriteAllText(extractedFilePath, strmContent);
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                File.WriteAllBytes(extractedFilePath, buf);
                                //entry.WriteToFile(extractedFilePath);
                            }
                            Console.WriteLine($"[{cnt}/{total}]Stored:{relativeFileName}");
                            LogLine($"[New]{relativeFileName}");

                        }
                    }

                }
            }
        }

        private static void SyncXiaoyaMetaSlow(string metaZipPath, string xiaoyaMetaOutputPath, string? strmXiaoyaUrlHost, string? replaceStrmXiaoyaUrlHost)
        {
            var replaceStrmUrlHost = !string.IsNullOrWhiteSpace(strmXiaoyaUrlHost) && !string.IsNullOrWhiteSpace(replaceStrmXiaoyaUrlHost);
            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll", "7z.dll");
            SevenZip.SevenZipExtractor.SetLibraryPath(libPath);

            var cnt = 0;
            var fileCnt = 0;
            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = new SevenZip.SevenZipExtractor(xystrm, true, SevenZip.InArchiveFormat.SevenZip))
            using (var writeFileStream = new MemoryStream())
            {

                var total = archive.FilesCount;
                foreach (var entry in archive.ArchiveFileData)
                {
                    cnt++;
                    if (!entry.IsDirectory && entry.Size > 0)
                    {
                        fileCnt++;
#if DEBUG
                        //if (fileCnt > 100) return;
#endif
                        var relativeFileName = entry.FileName.Replace(" \\", "\\").Replace(" /", "/").Replace("\\ ", "\\").Replace("/ ", "/").Trim();
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            Console.WriteLine($"[{cnt}/{total}]Skipped Exists:{relativeFileName}");
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            Console.WriteLine($"[{cnt}/{total}]New File:{relativeFileName}");
                            writeFileStream.Seek(0, SeekOrigin.Begin);
                            var startDate = DateTime.Now;
                            archive.ExtractFile(entry.Index, writeFileStream);
                            var duration = (int)(DateTime.Now - startDate).TotalSeconds;
                            Console.WriteLine($"[{cnt}/{total}]Expanded({duration}s):{relativeFileName}");
                            if (replaceStrmUrlHost && relativeFileName.EndsWith(".strm"))
                            {
                                var strmContent = Encoding.UTF8.GetString(writeFileStream.GetBuffer(), 0, (int)entry.Size).Replace(strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                                File.WriteAllText(extractedFilePath, strmContent);
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                File.WriteAllBytes(extractedFilePath, buf);
                                //entry.WriteToFile(extractedFilePath);
                            }
                            Console.WriteLine($"[{cnt}/{total}]Stored:{relativeFileName}");
                            LogLine($"[New]{relativeFileName}");

                        }
                    }

                }
            }
        }

        private static void SyncXiaoyaMetaFast(string metaZipPath, string xiaoyaMetaOutputPath, string? strmXiaoyaUrlHost, string? replaceStrmXiaoyaUrlHost)
        {

            var replaceStrmUrlHost = !string.IsNullOrWhiteSpace(strmXiaoyaUrlHost) && !string.IsNullOrWhiteSpace(replaceStrmXiaoyaUrlHost);
            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll", "7z.dll");

            var cnt = 0;
            var fileCnt = 0;
            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = new SevenZipExtractor.ArchiveFile(xystrm, SevenZipExtractor.SevenZipFormat.SevenZip, libPath))
            using (var writeFileStream = new MemoryStream())
            {

                var total = archive.Entries.Count;
                foreach (var entry in archive.Entries)
                {
                    cnt++;
                    if (!entry.IsFolder && entry.Size > 0)
                    {
                        fileCnt++;
#if DEBUG
                        //if (fileCnt > 100) return;
#endif
                        var relativeFileName = entry.FileName.Replace(" \\", "\\").Replace(" /", "/").Replace("\\ ", "\\").Replace("/ ", "/").Trim();
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            Console.WriteLine($"[{cnt}/{total}]Skipped Exists:{relativeFileName}");
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            Console.WriteLine($"[{cnt}/{total}]New File:{relativeFileName}");
                            writeFileStream.Seek(0, SeekOrigin.Begin);
                            var startDate = DateTime.Now;
                            entry.Extract(writeFileStream);
                            var duration = (int)(DateTime.Now - startDate).TotalSeconds;
                            Console.WriteLine($"[{cnt}/{total}]Expanded({duration}s):{relativeFileName}");
                            if (replaceStrmUrlHost && relativeFileName.EndsWith(".strm"))
                            {
                                var strmContent = Encoding.UTF8.GetString(writeFileStream.GetBuffer(), 0, (int)entry.Size).Replace(strmXiaoyaUrlHost, replaceStrmXiaoyaUrlHost);
                                File.WriteAllText(extractedFilePath, strmContent);
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                File.WriteAllBytes(extractedFilePath, buf);
                                //entry.WriteToFile(extractedFilePath);
                            }
                            Console.WriteLine($"[{cnt}/{total}]Stored:{relativeFileName}");
                            LogLine($"[New]{relativeFileName}");

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

        public override bool CanWrite => false;

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
            //fsInput.Flush();
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
            //fsInput.SetLength(value + fileStartIndex);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            //fsInput.Write(buffer, offset, count);
        }
    }
}
