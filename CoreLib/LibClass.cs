using SevenZipExtractor;
using System.Text;

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
    public class XiaoYaMetaSync
    {
        private const string MEDIA_FILE_LIST = ".mp4|.mkv|.avi|.ts|.asf|.wmv|.wm|.wmp|.m4v|.m4b|.m4r|.m4p|.mpeg4|.mov|.flv|.f4v|.swf|.hlv|.rm|.ram|.rmvb|.rp|.rpm|.rt|.smil|.scm|.mpg|.mpe|.mpeg(*)|.dat|.tsv|.mts|.m2t|.m2ts|.tp|.tpr|.pva|.pss|.m1v|.m2v|.m2p|.mp2v|.mpv2|.3gp|.3gpp|.3g2|.3gp2|.ifo|.vob|.amv|.csf|.mts|.mod|.evo|.pmp|.webm|.mxf|.vp6|.bik|.ogm|.ogv|.ogx|.xlmv|.divx|.qt";
        private static int totalEntry = 0;
        private static int cnt = 0;
        private static int fileCnt = 0;
        private static int newFileCnt = 0;
        public static bool WriteFileAsync { get; set; } = true;
        public static void Sync(string metaZipPath, string xiaoyaMetaOutputPath, bool kodiFix, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            SyncFast(metaZipPath, xiaoyaMetaOutputPath, kodiFix, replacements);
        }

        private static void SyncFast(string metaZipPath, string xiaoyaMetaOutputPath, bool kodiFix, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            var replaceStrm = replacements != null && replacements.Count() > 0;
            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll", "7z.dll");

            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = new ArchiveFile(xystrm, SevenZipFormat.SevenZip, libPath))
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
                                    if (WriteFileAsync)
                                        File.WriteAllTextAsync(extractedFilePath, newContent);
                                    else
                                        File.WriteAllText(extractedFilePath, newContent);
                                }
                                else
                                {
                                    if (WriteFileAsync)
                                        File.WriteAllTextAsync(extractedFilePath, strmContent);
                                    else
                                        File.WriteAllText(extractedFilePath, strmContent);
                                }
                            }
                            else
                            {
                                writeFileStream.Seek(0, SeekOrigin.Begin);
                                var buf = new byte[entry.Size];
                                writeFileStream.Read(buf, 0, buf.Length);
                                if (WriteFileAsync)
                                    File.WriteAllBytesAsync(extractedFilePath, buf);
                                else
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
        public static void StartRecursiveSyncMediaToStrm(string mediaRootPath, string urlPrefix, string outpuPath, bool generateStrmOnly, bool rewriteMetaFiles, bool rewriteStrm, bool encodeStrmUrl, bool strmKeepFileExtension)
        {
            urlPrefix = urlPrefix.Trim("/\\".ToArray());
            RecursiveSyncMediaToStrm(mediaRootPath, mediaRootPath, urlPrefix, outpuPath, generateStrmOnly, rewriteMetaFiles, rewriteStrm, encodeStrmUrl, strmKeepFileExtension);
        }

        private static void RecursiveSyncMediaToStrm(string mediaRootPath, string currentPath, string urlPrefix, string outpuPath, bool generateStrmOnly, bool rewriteMetaFiles, bool rewriteStrm, bool encodeStrmUrl, bool strmKeepFileExtension)
        {
            if (Directory.Exists(currentPath))
            {
                var files = Directory.GetFiles(currentPath);
                foreach (var file in files)
                {
                    var relativeFile = Path.GetRelativePath(mediaRootPath, file);
                    var outputFile = Path.Combine(outpuPath, relativeFile);
                    var fileExtension = Path.GetExtension(file);
                    if (MEDIA_FILE_LIST.Contains(fileExtension))
                    {
                        if (strmKeepFileExtension)
                            outputFile += ".strm";
                        else
                            outputFile = Path.Combine(Directory.GetParent(outputFile).FullName, Path.GetFileNameWithoutExtension(outputFile) + ".strm");


                        if (rewriteStrm || !File.Exists(outputFile))
                        {
                            GenerateStrm(urlPrefix, outputFile, relativeFile, encodeStrmUrl);
                            CommonLogger.LogLine($"[STRM] {outputFile}", true);
                        }
                        else
                        {
                            Console.WriteLine($"[SKIP]{outputFile}");
                        }
                        Console.WriteLine();
                    }
                    else if (!generateStrmOnly)
                    {
                        if (!rewriteMetaFiles && File.Exists(outputFile))
                        {
                            Console.WriteLine($"[SKIP]{outputFile}");
                        }
                        else
                        {
                            Console.WriteLine($"[COPY]{outputFile}");
                            Directory.CreateDirectory(Directory.GetParent(outputFile).FullName);
                            File.Copy(file, outputFile);
                        }
                        Console.WriteLine();
                    }
                }

                var dirs = Directory.GetDirectories(currentPath);
                foreach (var dir in dirs)
                {
                    RecursiveSyncMediaToStrm(mediaRootPath, dir, urlPrefix, outpuPath, generateStrmOnly, rewriteMetaFiles, rewriteStrm, encodeStrmUrl, strmKeepFileExtension);
                }
            }
            else
            {
                Console.WriteLine($"[Path Not Found] {currentPath}");
            }
        }

        private static void GenerateStrm(string urlPrefix, string outputFile, string relativeFile, bool encodeStrmUrl)
        {
            var url = $"{urlPrefix}/{relativeFile}".Replace("\\", "/");
            if (encodeStrmUrl) url = Uri.EscapeUriString(url);
            Directory.CreateDirectory(Directory.GetParent(outputFile).FullName);
            if (WriteFileAsync)
                File.WriteAllTextAsync(outputFile, url);
            else
                File.WriteAllText(outputFile, url);

        }

        public static void RemoveExpiredMeta(string metaZipPath, string extractPath)
        {
            var libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dll", "7z.dll");

            using (var fs = File.OpenRead(metaZipPath))
            using (var xystrm = new XiaoyaMetaZipStream(fs))
            using (var archive = new ArchiveFile(xystrm, SevenZipFormat.SevenZip, libPath))
            {
                Console.WriteLine("Counting Meta Folders......");
                var metaFolders = GetMetaFolders(archive);
                Console.WriteLine($"Counted Meta Folders: {metaFolders.Count}");
                extractPath = Path.GetFullPath(extractPath);
                RecursiveRemoveExpiredMeta(metaFolders, extractPath, extractPath);
            }
        }

        private static HashSet<string> GetMetaFolders(ArchiveFile archive)
        {
            var folders = new HashSet<string>();
            foreach (var item in archive.Entries)
            {
                if (item.IsFolder) folders.Add(CommonUtility.AdaptWindowsFileName(item.FileName));
            }
            return folders;
        }

        private static void RecursiveRemoveExpiredMeta(HashSet<string> metaFolders, string extractPath, string currentPath)
        {
            var dirs = Directory.GetDirectories(currentPath);
            foreach (var dir in dirs)
            {
                if (new DirectoryInfo(dir).Name.StartsWith("."))
                {
                    Console.WriteLine($"[Ignore] {dir}");
                    continue;
                }
                var relativePath = Path.GetRelativePath(extractPath, dir);
                relativePath = CommonUtility.AdaptWindowsFileName(relativePath);
                //Console.WriteLine($"{relativePath}");
                if (metaFolders.Contains(relativePath))
                {
                    RecursiveRemoveExpiredMeta(metaFolders, extractPath, dir);
                }
                else
                {
                    Directory.Delete(dir, true);
                    CommonLogger.LogLine($"[Remove Expired] {relativePath}", true);
                }

            }

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
                if (XiaoYaMetaSync.WriteFileAsync)
                    File.WriteAllTextAsync(filePath, newContent);
                else
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
