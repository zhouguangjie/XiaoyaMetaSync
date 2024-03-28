using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Text;

namespace XiaoyaMetaSync
{
    internal class Program
    {

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintHelp();
                return;
            }

            try
            {
                switch (args[0])
                {
                    case "--sync": CmdSync(args); break;
                    case "--strm": CmdStrm(args); break;
                    default: PrintHelp(); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void PrintHelpSync()
        {
            Console.WriteLine("Usage: --sync <xiaoya meta zip> <output path> [--kodi] [<--replace|-R> <strm file old string> <strm file new string>]...");
        }
        private static void PrintHelpStrm()
        {
            Console.WriteLine("Usage: --strm <dir> [--kodi] [<--replace|-R> <strm file old string> <strm file new string>]...");
        }
        private static void PrintHelp()
        {
            PrintHelpSync();
            PrintHelpStrm();
        }

        private static void CmdStrm(string[] args)
        {
            if (args.Length < 2)
            {
                PrintHelpStrm();
                return;
            }
            CommonLogger.NewLog();
            var replacements = GetReplacements(args);
            var report = StrmFileHelper.ProcessStrm(args[1], true, StrmAdapt2Kodi(args), replacements);
            CommonLogger.LogLine($"Matched:{report.MatchFiles}, Processed:{report.Replaced}", true);
            CommonLogger.LogLine($"Finish:{report.StartTime} --> {report.EndTime}, Duration: {report.Duration}", true);
        }

        private static List<KeyValuePair<string, string>> GetReplacements(string[] args)
        {
            var res = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < args.Length; i++)
            {
                var cur = args[i];
                if (cur.ToLower() == "--replace" || cur == "-R")
                {
                    if (args.Length > i + 2)
                    {
                        res.Add(new KeyValuePair<string, string>(args[i + 1], args[i + 2]));
                    }
                    else
                    {
                        throw new ArgumentException("Required argument: --replace <old string> <new string>");
                    }
                }

            }
            return res;
        }

        private static void CmdSync(string[] args)
        {
            if (args.Length < 3)
            {
                PrintHelpSync();
                return;
            }

            var zipPath = args[1];
            var extractPath = args[2];
            var replacments = GetReplacements(args);

            if (!File.Exists(zipPath))
            {
                Console.WriteLine($"Zip File Not Exists:{zipPath}");
                return;
            }
            CommonLogger.NewLog();
            CommonLogger.LogLine($"ZipPath:{zipPath}", true);
            CommonLogger.LogLine($"MetaOutput:{extractPath}", true);
            try
            {
                var startDate = DateTime.Now;
                CommonLogger.LogLine($"Start:{DateTime.Now}", true);

                XiaoYaMetaSync.Sync(zipPath, extractPath, StrmAdapt2Kodi(args), replacments);
                var duration = DateTime.Now - startDate;
                CommonLogger.LogLine($"Finish:{startDate} --> {DateTime.Now}, Duration: {duration}", true);
            }
            catch (Exception ex)
            {
                CommonLogger.LogLine(ex.Message, true);
                CommonLogger.LogLine(ex.ToString(), true);
            }
        }

        private static bool StrmAdapt2Kodi(string[] args)
        {
            return args.Contains("--kodi");
        }
    }

    class CommonLogger
    {
        private static readonly string LOG_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiaoyaMetaSync", "Log");
        public static string? LOG_FILE { get; private set; }
        public static void LogLine(string line, bool writeConsole = false)
        {
            if (!string.IsNullOrWhiteSpace(LOG_FILE)) File.AppendAllLines(LOG_FILE, [line]);
            if (writeConsole) Console.WriteLine(line);
        }

        internal static void NewLog()
        {
            if (!Directory.Exists(LOG_DIR))
                Directory.CreateDirectory(LOG_DIR);
            LOG_FILE = Path.Combine(LOG_DIR, DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
        }
    }

    public class XiaoYaMetaSync
    {
        private static int totalEntry = 0;
        private static int cnt = 0;
        private static int fileCnt = 0;
        private static int newFileCnt = 0;
        public static void Sync(string metaZipPath, string xiaoyaMetaOutputPath, bool kodiFix, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            //SyncTooSlow(metaZipPath, xiaoyaMetaOutputPath, kodiFix, replacements);
            //SyncSlow(metaZipPath, xiaoyaMetaOutputPath, kodiFix, replacements);
            SyncFast(metaZipPath, xiaoyaMetaOutputPath, kodiFix, replacements);
        }


        private static void SyncTooSlow(string metaZipPath, string xiaoyaMetaOutputPath, bool kodiFix, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            var replaceStrm = replacements != null && replacements.Count() > 0;

            var options = new ReaderOptions
            {
                ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 },
                DisableCheckIncomplete = true,
                LeaveStreamOpen = true,
            };

            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = SevenZipArchive.Open(xystrm, options))
            using (var writeFileStream = new MemoryStream())
            {
                totalEntry = archive.Entries.Count;
                foreach (var entry in archive.Entries)
                {
                    cnt++;
                    if (!entry.IsDirectory && entry.Size > 0)
                    {
                        fileCnt++;
                        var relativeFileName = CommonUtility.AdaptWindowsFileName(entry.Key);
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            Console.WriteLine($"[{cnt}/{totalEntry}]Skipped Exists:{relativeFileName}");
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            Console.WriteLine($"[{cnt}/{totalEntry}]New File:{relativeFileName}");
                            writeFileStream.Seek(0, SeekOrigin.Begin);
                            var startDate = DateTime.Now;
                            entry.WriteTo(writeFileStream);
                            var duration = (int)(DateTime.Now - startDate).TotalSeconds;
                            Console.WriteLine($"[{cnt}/{totalEntry}]Expanded({duration}s):{relativeFileName}");
                            if (replaceStrm && relativeFileName.EndsWith(".strm"))
                            {
                                var strmContent = Encoding.UTF8.GetString(writeFileStream.GetBuffer(), 0, (int)entry.Size);
                                if (StrmFileHelper.ProcesStrmFileContent(strmContent, kodiFix, replacements, out string newContent))
                                {
                                    File.WriteAllText(extractedFilePath, newContent);
                                }
                                else
                                {
                                    File.WriteAllText(extractedFilePath, strmContent);
                                }
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                File.WriteAllBytes(extractedFilePath, buf);
                            }
                            newFileCnt++;
                            Console.WriteLine($"[{cnt}/{totalEntry}]Stored:{relativeFileName}");
                            CommonLogger.LogLine($"[New]{relativeFileName}");

                        }
                    }

                }
            }

            CommonLogger.LogLine($"Total:{totalEntry}, Effective File:{fileCnt}, New:{newFileCnt}", true);
        }
        private static void SyncSlow(string metaZipPath, string xiaoyaMetaOutputPath, bool kodiFix, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            var replaceStrm = replacements != null && replacements.Count() > 0;
            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll", "7z.dll");
            SevenZip.SevenZipExtractor.SetLibraryPath(libPath);

            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = new SevenZip.SevenZipExtractor(xystrm, true, SevenZip.InArchiveFormat.SevenZip))
            using (var writeFileStream = new MemoryStream())
            {

                totalEntry = (int)archive.FilesCount;
                foreach (var entry in archive.ArchiveFileData)
                {
                    cnt++;
                    if (!entry.IsDirectory && entry.Size > 0)
                    {
                        fileCnt++;
                        var relativeFileName = CommonUtility.AdaptWindowsFileName(entry.FileName);
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            Console.WriteLine($"[{cnt}/{totalEntry}]Skipped Exists:{relativeFileName}");
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            Console.WriteLine($"[{cnt}/{totalEntry}]New File:{relativeFileName}");
                            writeFileStream.Seek(0, SeekOrigin.Begin);
                            var startDate = DateTime.Now;
                            archive.ExtractFile(entry.Index, writeFileStream);
                            var duration = (int)(DateTime.Now - startDate).TotalSeconds;
                            Console.WriteLine($"[{cnt}/{totalEntry}]Expanded({duration}s):{relativeFileName}");
                            if (replaceStrm && relativeFileName.EndsWith(".strm"))
                            {
                                var strmContent = Encoding.UTF8.GetString(writeFileStream.GetBuffer(), 0, (int)entry.Size);
                                if (StrmFileHelper.ProcesStrmFileContent(strmContent, kodiFix, replacements, out string newContent))
                                {
                                    File.WriteAllText(extractedFilePath, newContent);
                                }
                                else
                                {
                                    File.WriteAllText(extractedFilePath, strmContent);
                                }
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                File.WriteAllBytes(extractedFilePath, buf);
                            }
                            newFileCnt++;
                            Console.WriteLine($"[{cnt}/{totalEntry}]Stored:{relativeFileName}");
                            CommonLogger.LogLine($"[New]{relativeFileName}");

                        }
                    }

                }
            }

            CommonLogger.LogLine($"Total:{totalEntry}, Effective File:{fileCnt}, New:{newFileCnt}", true);
        }
        private static void SyncFast(string metaZipPath, string xiaoyaMetaOutputPath, bool kodiFix, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            var replaceStrm = replacements != null && replacements.Count() > 0;
            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll", "7z.dll");

            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = new SevenZipExtractor.ArchiveFile(xystrm, SevenZipExtractor.SevenZipFormat.SevenZip, libPath))
            using (var writeFileStream = new MemoryStream())
            {

                totalEntry = archive.Entries.Count;
                foreach (var entry in archive.Entries)
                {
                    cnt++;
                    if (!entry.IsFolder && entry.Size > 0)
                    {
                        fileCnt++;
                        var relativeFileName = CommonUtility.AdaptWindowsFileName(entry.FileName);
                        string extractedFilePath = Path.Combine(xiaoyaMetaOutputPath, relativeFileName);
                        if (File.Exists(extractedFilePath))
                        {
                            Console.WriteLine($"[{cnt}/{totalEntry}]Skipped Exists:{relativeFileName}");
                        }
                        else
                        {
                            var dir = Path.GetDirectoryName(extractedFilePath);
                            if (!Directory.Exists(dir))
                                Directory.CreateDirectory(dir);

                            Console.WriteLine($"[{cnt}/{totalEntry}]New File:{relativeFileName}");
                            writeFileStream.Seek(0, SeekOrigin.Begin);
                            var startDate = DateTime.Now;
                            entry.Extract(writeFileStream);
                            var duration = (int)(DateTime.Now - startDate).TotalSeconds;
                            Console.WriteLine($"[{cnt}/{totalEntry}]Expanded({duration}s):{relativeFileName}");
                            if (replaceStrm && relativeFileName.EndsWith(".strm"))
                            {
                                var strmContent = Encoding.UTF8.GetString(writeFileStream.GetBuffer(), 0, (int)entry.Size);

                                if (StrmFileHelper.ProcesStrmFileContent(strmContent, kodiFix, replacements, out string newContent))
                                {
                                    File.WriteAllText(extractedFilePath, newContent);
                                }
                                else
                                {
                                    File.WriteAllText(extractedFilePath, strmContent);
                                }
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                File.WriteAllBytes(extractedFilePath, buf);
                            }
                            newFileCnt++;
                            Console.WriteLine($"[{cnt}/{totalEntry}]Stored:{relativeFileName}");
                            CommonLogger.LogLine($"[New]{relativeFileName}");

                        }
                    }

                }
            }

            CommonLogger.LogLine($"Total:{totalEntry}, Effective File:{fileCnt}, New:{newFileCnt}", true);
        }

    }

    public class CommonUtility
    {
        public static string AdaptWindowsFileName(string filename)
        {
            return filename.Replace('|', '_')
                .Replace('"', '_')
                .Replace('*', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('?', '_')
                .Replace(':', '_')
                .Replace(" \\", "\\")
                .Replace(" /", "/")
                .Replace("\\ ", "\\")
                .Replace("/ ", "/")
                .Trim();
        }
    }

    public class StrmFileHelper
    {

        public class ProcessStrmReport
        {
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public TimeSpan Duration => EndTime - StartTime;
            public int Replaced { get; set; } = 0;
            public int MatchFiles { get; set; } = 0;
        }
        public static ProcessStrmReport ProcessStrm(string dir, bool recursive, bool kodiFix)
        {
            return ProcessStrm(dir, recursive, kodiFix, null);
        }

        public static ProcessStrmReport ProcessStrm(string dir, bool recursive, bool kodiFix, IEnumerable<KeyValuePair<string, string>>? replacements)
        {
            var report = new ProcessStrmReport();
            report.StartTime = DateTime.Now;
            RecursiveProcessStrm(dir, recursive, kodiFix, replacements, report);
            report.EndTime = DateTime.Now;
            return report;
        }

        private static void RecursiveProcessStrm(string path, bool recursive, bool kodiFix, IEnumerable<KeyValuePair<string, string>>? replacements, ProcessStrmReport report)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.strm");
                foreach (var file in files)
                {
                    report.MatchFiles++;
                    if (ProcessStrmFile(file, replacements, kodiFix))
                    {
                        CommonLogger.LogLine($"[Processed] {file}", true);
                        report.Replaced++;
                    }
                    else
                    {
                        Console.WriteLine($"[Ignored] {file}");
                    }
                }

                if (recursive)
                {
                    var dirs = Directory.GetDirectories(path);
                    foreach (var dir in dirs)
                    {
                        RecursiveProcessStrm(dir, true, kodiFix, replacements, report);
                    }
                }
            }
        }


        public static bool ProcessStrmFile(string filePath, IEnumerable<KeyValuePair<string, string>>? replacements, bool kodiFix)
        {
            var content = File.ReadAllText(filePath);
            if (ProcesStrmFileContent(content, kodiFix, replacements, out string newContent))
            {
                File.WriteAllText(filePath, newContent);
                return true;
            }
            return false;
        }

        public static bool ProcesStrmFileContent(string content, bool kodiFix, IEnumerable<KeyValuePair<string, string>>? replacements, out string newContent)
        {
            newContent = content;
            if (replacements != null)
            {
                foreach (var entry in replacements)
                {
                    newContent = newContent.Replace(entry.Key, entry.Value);
                }
            }

            ///适配Kodi Emby插件，示例URL："http://xiaoya.host:5244/folder%20A/subfolder%20B/video%20C.mp4"，会处理成"http://xiaoya.host:5244/folder A/subfolder B/video%20C.mp4"
            ///小雅strm文件的url是经过编码的(但仅仅编码了一些符号)，而Kodi的emby插件也会对url path(不包含文件名)进行一次编码再拼接文件名存储起来，二次编码处理的URL导致小雅Alist找不到文件返回400。
            ///emby插件编码参考：https://github.com/MediaBrowser/plugin.video.emby/blob/next-gen-dev-python3/core/common.py#L183
            ///emby插件不做文件名编码，kodi播放不了带空格文件名的url，所以保留编码了的文件名。
            ///
            ///适配：把小雅strm文件url path还原，文件名不作处理。
            ///处理后的url使用emby网页端可以正常播放，其他客户端可能会不兼容，推荐只使用kodi客户端使用，多客户端建议修改kodi插件来修复该问题。
            if (kodiFix)
            {
                var cut = newContent.LastIndexOf("/");
                var prefix = newContent.Substring(0, cut);
                var newPrefix = Uri.UnescapeDataString(prefix);
                if (prefix != newPrefix)
                {
                    var fileName = newContent.Substring(cut + 1);
                    newContent = $"{newPrefix}/{fileName}";
                }
            }
            return newContent != content;
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
