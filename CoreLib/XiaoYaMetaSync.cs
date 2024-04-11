using SevenZipExtractor;
using System.Text;
namespace XiaoyaMetaSync.CoreLib
{
    public class XiaoYaMetaSync
    {
        private const string DIR_SYNC_CONF_PATH_NAME = ".xymetasync";
        private const string DIR_SYNC_ATTR_FILE_IGNORE = ".ignore";
        private const string MEDIA_FILE_LIST = ".mp4|.mkv|.avi|.ts|.asf|.wmv|.wm|.wmp|.m4v|.m4b|.m4r|.m4p|.mpeg4|.mov|.flv|.f4v|.swf|.hlv|.rm|.ram|.rmvb|.rp|.rpm|.rt|.smil|.scm|.mpg|.mpe|.mpeg(*)|.dat|.tsv|.mts|.m2t|.m2ts|.tp|.tpr|.pva|.pss|.m1v|.m2v|.m2p|.mp2v|.mpv2|.3gp|.3gpp|.3g2|.3gp2|.ifo|.vob|.amv|.csf|.mts|.mod|.evo|.pmp|.webm|.mxf|.vp6|.bik|.ogm|.ogv|.ogx|.xlmv|.divx|.qt";

        private int totalEntry = 0;
        private int cnt = 0;
        private int fileCnt = 0;
        private int newFileCnt = 0;
        public bool WriteFileAsync { get; set; } = true;
        public void Sync(string metaZipPath, string xiaoyaMetaOutputPath, IEnumerable<KeyValuePair<string, string>> replacements)
        {
            SyncFast(metaZipPath, xiaoyaMetaOutputPath, replacements);
        }

        private void SyncFast(string metaZipPath, string xiaoyaMetaOutputPath, IEnumerable<KeyValuePair<string, string>> replacements)
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
                            
                            if (IsIgnoreFile(xiaoyaMetaOutputPath, relativeFileName))
                            {
                                Console.WriteLine($"[{cnt}/{totalEntry}]Ignored Path Files:{relativeFileName}");
                                continue;
                            }
                            
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

                                if (StrmFileHelper.ProcesStrmFileContent(strmContent, replacements, out string newContent))
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

        public void AddIgnoreRelativePaths(IEnumerable<string> paths)
        {
            foreach (var item in paths)
            {
                _ignorePaths.Add(AdaptIgnoreFileName(item));
            }
        }

        public void AddIgnoreRelativePathsFromFile(string configFile)
        {
            var lines = File.ReadAllLines(configFile);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                _ignorePaths.Add(AdaptIgnoreFileName(line));
            }
        }

        private static string AdaptIgnoreFileName(string filePath)
        {
            return CommonUtility.AdaptWindowsFileName(filePath).Replace("\\", "/").Trim('/');
        }

        private HashSet<string> _ignorePaths = new HashSet<string>();
        private bool IsIgnoreFile(string outputDir, string relativeFileName)
        {
            foreach (var path in _ignorePaths)
            {
                if (AdaptIgnoreFileName(relativeFileName).StartsWith(path)) return true;
            }
            var curDir = Path.GetDirectoryName(relativeFileName);
            while (curDir != null)
            {
                if (File.Exists(Path.Combine(outputDir, curDir, DIR_SYNC_CONF_PATH_NAME, DIR_SYNC_ATTR_FILE_IGNORE)))
                {
                    _ignorePaths.Add(AdaptIgnoreFileName(curDir));
                    return true;
                }
                curDir = Path.GetDirectoryName(curDir);
            }
            return false;
        }

        public void StartRecursiveSyncMediaToStrm(string mediaRootPath, string urlPrefix, string outpuPath, bool generateStrmOnly, bool rewriteMetaFiles, bool rewriteStrm, bool encodeStrmUrl, bool strmKeepFileExtension)
        {
            urlPrefix = urlPrefix.Trim("/\\".ToArray());
            RecursiveSyncMediaToStrm(mediaRootPath, mediaRootPath, urlPrefix, outpuPath, generateStrmOnly, rewriteMetaFiles, rewriteStrm, encodeStrmUrl, strmKeepFileExtension);
        }

        private void RecursiveSyncMediaToStrm(string mediaRootPath, string currentPath, string urlPrefix, string outpuPath, bool generateStrmOnly, bool rewriteMetaFiles, bool rewriteStrm, bool encodeStrmUrl, bool strmKeepFileExtension)
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

        private void GenerateStrm(string urlPrefix, string outputFile, string relativeFile, bool encodeStrmUrl)
        {
            var url = $"{urlPrefix}/{relativeFile}".Replace("\\", "/");
            if (encodeStrmUrl) url = Uri.EscapeUriString(url);
            Directory.CreateDirectory(Directory.GetParent(outputFile).FullName);
            if (WriteFileAsync)
                File.WriteAllTextAsync(outputFile, url);
            else
                File.WriteAllText(outputFile, url);

        }

        public void RemoveExpiredMeta(string metaZipPath, string extractPath)
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

        private void RecursiveRemoveExpiredMeta(HashSet<string> metaFolders, string extractPath, string currentPath)
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

}