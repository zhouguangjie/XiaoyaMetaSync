﻿using SevenZipExtractor;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using WebDav;
namespace XiaoyaMetaSync.CoreLib
{
    public class XiaoYaMetaSync
    {
        private const string DIR_SYNC_CONF_PATH_NAME = ".xymetasync";
        private const string DIR_SYNC_ATTR_FILE_IGNORE = ".ignore";


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

        public void StartRecursiveSyncMediaToStrm(string mediaRootPath, string urlPrefix, string outpuPath, bool generateStrmOnly, bool rewriteMetaFiles, bool rewriteStrm, bool encodeStrmUrl, bool strmKeepFileExtension, KeyValuePair<string, string>[] outputPathRegexReplacements)
        {
            urlPrefix = urlPrefix.Trim("/\\".ToArray());
            RecursiveSyncMediaToStrm(mediaRootPath, mediaRootPath, urlPrefix, outpuPath, generateStrmOnly, rewriteMetaFiles, rewriteStrm, encodeStrmUrl, strmKeepFileExtension, outputPathRegexReplacements);
        }

        public static IWebDavClient _client = CreateWebDavClient();

        private static IWebDavClient CreateWebDavClient()
        {
            var accessToken = Environment.GetEnvironmentVariable("XY_SYNC_WEBDAV_TOKEN");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                return new WebDavClient(httpClient);
            }

            var user = Environment.GetEnvironmentVariable("XY_SYNC_WEBDAV_USER");
            var password = Environment.GetEnvironmentVariable("XY_SYNC_WEBDAV_PASSWORD");
            if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password))
            {
                var clientParams = new WebDavClientParams
                {
                    Credentials = new NetworkCredential(user, password)
                };
                return new WebDavClient(clientParams);
            }
            return new WebDavClient();
        }

        public async Task GenStrmFromWebDavAsync(string webdavUrl, string output, bool rewrite, bool keepFileExt, KeyValuePair<string, string>[] pathRegexReplacements)
        {
            var webDavUri = new Uri(webdavUrl);
            var webDavReqHost = webDavUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

            var result = await _client.Propfind(webdavUrl);

            if (result.IsSuccessful)
            {
                var files = new List<WebDavResource>();
                var dirs = new List<WebDavResource>();

                foreach (var res in result.Resources)
                {
                    if (res.IsCollection)
                    {
                        dirs.Add(res);
                        Trace.WriteLine($"DIR:{res.DisplayName}");
                    }
                    else
                    {
                        files.Add(res);
                        Trace.WriteLine($"FILE:{res.DisplayName}");
                    }
                }

                foreach (var file in files)
                {
                    var replacedOutputPath = CommonUtility.AdaptWindowsFileName(CommonUtility.KVReplace(pathRegexReplacements, output));
                    if (CommonUtility.IsMediaFile(file.DisplayName))
                    {
                        var outputFile = "";
                        if (keepFileExt)
                            outputFile = Path.Combine(replacedOutputPath, $"{file.DisplayName}.strm");
                        else
                            outputFile = Path.Combine(replacedOutputPath, $"{Path.GetFileNameWithoutExtension(file.DisplayName)}.strm");

                        string fileContent = GetWebdavFileUrl(webDavReqHost, file);
                        WriteStrm(outputFile, fileContent, rewrite);

                        Console.WriteLine();
                    }
                }

                for (int i = 1; i < dirs.Count; i++)
                {
                    var dir = dirs[i];
                    await GenStrmFromWebDavAsync($"{webdavUrl}/{dir.DisplayName}", Path.Combine(output, dir.DisplayName), rewrite, keepFileExt, pathRegexReplacements);
                }
            }
            else
            {
                Console.WriteLine($"[Request url error] {webdavUrl}");
            }
        }

        private static string GetWebdavFileUrl(string webDavReqHost, WebDavResource file)
        {
            var uri = file.Uri;
            if (uri.StartsWith("/dav/")) uri = "/d/" + uri.Substring(5);
            var fileContent = $"{webDavReqHost}{uri}";
            return fileContent;
        }

        private void WriteStrm(string filePath, string fileContent, bool rewrite)
        {
            if (rewrite || !File.Exists(filePath))
            {
                Directory.CreateDirectory(Directory.GetParent(filePath).FullName);
                if (WriteFileAsync)
                    File.WriteAllTextAsync(filePath, fileContent);
                else
                    File.WriteAllText(filePath, fileContent);

                CommonLogger.LogLine($"[STRM] {filePath}", true);
            }
            else
            {
                Console.WriteLine($"[SKIP]{filePath}");
            }
        }

        private void RecursiveSyncMediaToStrm(string mediaRootPath, string currentPath, string urlPrefix, string outpuPath, bool generateStrmOnly, bool rewriteMetaFiles, bool rewriteStrm, bool encodeStrmUrl, bool strmKeepFileExtension, KeyValuePair<string, string>[] pathRegexReplacements)
        {

            if (Directory.Exists(currentPath))
            {
                var files = Directory.GetFiles(currentPath);
                foreach (var file in files)
                {
                    var relativeFile = Path.GetRelativePath(mediaRootPath, file);
                    var relativePath = Path.GetDirectoryName(relativeFile);
                    var filename = Path.GetFileName(relativeFile);
                    var outputRelativeFile = CommonUtility.AdaptWindowsFileName(CommonUtility.KVReplace(pathRegexReplacements, relativePath));
                    var outputFile = Path.Combine(outpuPath, outputRelativeFile, filename);

                    if (CommonUtility.IsMediaFile(file))
                    {
                        if (strmKeepFileExtension)
                            outputFile += ".strm";
                        else
                            outputFile = Path.Combine(Directory.GetParent(outputFile).FullName, Path.GetFileNameWithoutExtension(outputFile) + ".strm");


                        if (rewriteStrm || !File.Exists(outputFile))
                        {
                            GenerateStrm(urlPrefix, outputFile, relativeFile, encodeStrmUrl, WriteFileAsync);
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
                    RecursiveSyncMediaToStrm(mediaRootPath, dir, urlPrefix, outpuPath, generateStrmOnly, rewriteMetaFiles, rewriteStrm, encodeStrmUrl, strmKeepFileExtension, pathRegexReplacements);
                }
            }
            else
            {
                Console.WriteLine($"[Path Not Found] {currentPath}");
            }
        }

        public static void GenerateStrm(string urlPrefix, string outputFile, string relativeFile, bool encodeStrmUrl, bool writeAsync)
        {
            var url = $"{urlPrefix}/{relativeFile}".Replace("\\", "/");
            if (encodeStrmUrl) url = Uri.EscapeUriString(url);
            Directory.CreateDirectory(Directory.GetParent(outputFile).FullName);
            if (writeAsync)
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

        public async Task CollectShowsStrmFromWebDavAsync(string webdavUrl, string output, bool rewrite, KeyValuePair<string, string>[] fileNameReplacements, KeyValuePair<string, string>[] showNameReplacements)
        {
            var webDavUri = new Uri(webdavUrl);
            var webDavReqHost = webDavUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);

            var result = await _client.Propfind(webdavUrl);

            if (result.IsSuccessful)
            {
                var files = new List<WebDavResource>();
                var dirs = new List<WebDavResource>();

                foreach (var res in result.Resources)
                {
                    if (res.IsCollection)
                    {
                        dirs.Add(res);
                        Trace.WriteLine($"DIR:{res.DisplayName}");
                    }
                    else if (CommonUtility.IsMediaFile(res.DisplayName))
                    {
                        files.Add(res);
                        Trace.WriteLine($"FILE:{res.DisplayName}");
                    }
                }

                foreach (var file in files)
                {
                    var fn = Path.GetFileNameWithoutExtension(file.DisplayName);

                    fn = CommonUtility.KVReplace(fileNameReplacements, fn);
                    fn = fn.Trim();

                    var show = fn;

                    show = CommonUtility.KVReplace(showNameReplacements, show);
                    show = show.Trim();

                    var outputFile = Path.Combine(output, show, fn + ".strm");

                    string fileContent = GetWebdavFileUrl(webDavReqHost, file);
                    WriteStrm(outputFile, fileContent, rewrite);
                }

                for (int i = 1; i < dirs.Count; i++)
                {
                    var dir = dirs[i];
                    await CollectShowsStrmFromWebDavAsync($"{webdavUrl}/{dir.DisplayName}", output, rewrite, fileNameReplacements, showNameReplacements);
                }
            }
            else
            {
                Console.WriteLine($"[Request url error] {webdavUrl}");
            }
        }

        public void CollectShowsStrm(string path, string urlPrefix, string output, bool rewrite, bool encodeStrmUrl, KeyValuePair<string, string>[] fileNameReplacements, KeyValuePair<string, string>[] showNameReplacements)
        {
            var files = CommonUtility.CollectMediaFiles(path);
            foreach (var file in files)
            {
                var fn = Path.GetFileNameWithoutExtension(file);

                fn = CommonUtility.KVReplace(fileNameReplacements, fn);
                fn = fn.Trim();

                var show = fn;

                show = CommonUtility.KVReplace(showNameReplacements, show);
                show = show.Trim();

                var outputFile = Path.Combine(output, show, fn + ".strm");
                if (rewrite || !File.Exists(outputFile))
                {
                    var relativeFile = Path.GetRelativePath(path, file);
                    GenerateStrm(urlPrefix, outputFile, relativeFile, encodeStrmUrl, WriteFileAsync);
                    Console.WriteLine(outputFile);
                }
            }
        }
    }

}