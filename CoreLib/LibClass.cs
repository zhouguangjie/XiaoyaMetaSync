namespace XiaoyaMetaSync.CoreLib
{
    public class CommonLogger
    {
        private static readonly string LOG_DIR = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XiaoyaMetaSync", "Log");
        public static string? LOG_FILE { get; private set; }
        public static void LogLine(string line, bool writeConsole = false)
        {
            if (!string.IsNullOrWhiteSpace(LOG_FILE)) File.AppendAllLines(LOG_FILE, [line]);
            if (writeConsole) Console.WriteLine(line);
        }

        public static void NewLog()
        {
            if (!Directory.Exists(LOG_DIR))
                Directory.CreateDirectory(LOG_DIR);
            LOG_FILE = Path.Combine(LOG_DIR, DateTime.Now.ToString("yyyyMMddHHmmss") + ".log");
        }

        public static void ClearLog()
        {
            if (Directory.Exists(LOG_DIR))
            {
                var files = Directory.GetFiles(LOG_DIR, "*.log");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
    }

    public class CommonUtility
    {
        public static string AdaptWindowsFileName(string filename)
        {
            filename = filename.Replace('|', '_')
                .Replace('"', '_')
                .Replace('*', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('?', '_')
                .Replace(':', '_')
                .Trim();

            var pathElements = filename.Split(['/', '\\']);
            var path = filename.StartsWith('/') ? "/" : (filename.StartsWith('\\') ? "\\" : "");
            foreach (var item in pathElements)
            {
                var trimmed = item.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    path = Path.Combine(path, trimmed);
                }
            }
            return path;
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
        public static ProcessStrmReport ProcessStrm(string dir, bool recursive)
        {
            return ProcessStrm(dir, recursive, null);
        }

        public static ProcessStrmReport ProcessStrm(string dir, bool recursive, IEnumerable<KeyValuePair<string, string>>? replacements)
        {
            var report = new ProcessStrmReport();
            report.StartTime = DateTime.Now;
            RecursiveProcessStrm(dir, recursive, replacements, report);
            report.EndTime = DateTime.Now;
            return report;
        }

        private static void RecursiveProcessStrm(string path, bool recursive, IEnumerable<KeyValuePair<string, string>>? replacements, ProcessStrmReport report)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.GetFiles(path, "*.strm");
                foreach (var file in files)
                {
                    report.MatchFiles++;
                    if (ProcessStrmFileAsync(file, replacements))
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
                        RecursiveProcessStrm(dir, true, replacements, report);
                    }
                }
            }
        }


        public static bool ProcessStrmFile(string filePath, IEnumerable<KeyValuePair<string, string>>? replacements)
        {
            var content = File.ReadAllText(filePath);
            if (ProcesStrmFileContent(content, replacements, out string newContent))
            {
                File.WriteAllText(filePath, newContent);
                return true;
            }
            return false;
        }


        public static bool ProcessStrmFileAsync(string filePath, IEnumerable<KeyValuePair<string, string>>? replacements)
        {
            var content = File.ReadAllText(filePath);
            if (ProcesStrmFileContent(content, replacements, out string newContent))
            {
                File.WriteAllTextAsync(filePath, newContent);
                return true;
            }
            return false;
        }

        public static bool ProcesStrmFileContent(string content, IEnumerable<KeyValuePair<string, string>>? replacements, out string newContent)
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
            ///
            /*
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
            }*/
            return newContent != content;
        }
    }

    #region XiaoyaMetaZipStream
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
    #endregion
}
