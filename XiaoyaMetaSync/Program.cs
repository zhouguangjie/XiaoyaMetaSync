﻿using XiaoyaMetaSync.CoreLib;
namespace XiaoyaMetaSync
{
    internal class Program
    {
        private static void PrintHelp()
        {
            PrintHelpSync();
            PrintHelpStrm();
            PrintHelpGenStrm();
            PrintHelpClearLog();
            PrintHelpRemoveExpiredMeta();
        }

        private static void PrintHelpRemoveExpiredMeta()
        {
            Console.WriteLine("Usage: --remove_expired_meta <xiaoya meta zip> <output path>");
        }

        private static void PrintHelpClearLog()
        {
            Console.WriteLine("Usage: --clear_log");
        }
        private static void PrintHelpSync()
        {
            Console.WriteLine("Usage: --sync <xiaoya meta zip> <output path> [--ignore <ignore relative path config file>] [<--replace|-R> <strm file old string> <strm file new string>]...");
        }
        private static void PrintHelpStrm()
        {
            Console.WriteLine("Usage: --strm <dir> [<--replace|-R> <strm file old string> <strm file new string>]...");
        }

        private static void PrintHelpGenStrm()
        {
            Console.WriteLine("Usage: --genstrm <media path> <url prefix> <output> [--only_strm] [--rewrite_meta] [--rewrite_strm] [--encode_url]");
        }

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
                    case "--genstrm": CmdGetStrm(args); break;
                    case "--clear_log": CmdClearLog(args); break;
                    case "--remove_expired_meta": CmdRemoveExpiredMeta(args); break;
                    default: PrintHelp(); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        private static void CmdClearLog(string[] args)
        {
            CommonLogger.ClearLog();
            Console.WriteLine("Clear Logs Finished");
        }

        private static void CmdGetStrm(string[] args)
        {
            if (args.Length < 4)
            {
                PrintHelpGenStrm();
                return;
            }
            var startDate = DateTime.Now;
            var mediaRootPath = args[1];
            var urlPrefix = args[2];
            var outputPath = args[3];
            CommonLogger.NewLog();
            CommonLogger.LogLine($"GenStrm Start:{DateTime.Now}", true);


            new XiaoYaMetaSync().StartRecursiveSyncMediaToStrm(mediaRootPath,
                urlPrefix,
                outputPath,
                args.Contains("--only_strm"),
                args.Contains("--rewrite_meta"),
                args.Contains("--rewrite_strm"),
                args.Contains("--encode_url"),
                args.Contains("--strm_keep_filetype"),
                GetOutputPathRegexReplacement(args));

            var duration = DateTime.Now - startDate;
            CommonLogger.LogLine($"GenStrm Finish:{startDate} --> {DateTime.Now}, Duration: {duration}", true);
        }

        private static KeyValuePair<string, string>[] GetOutputPathRegexReplacement(string[] args)
        {
            var res = new List<KeyValuePair<string, string>>();
            try
            {
                var renameConf = GetFollowArgs(args, "--path_remap", 1)[0];
                var lines = File.ReadAllLines(renameConf);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var kv = line.Split('>');
                    if (kv.Length > 1)
                    {
                        res.Add(new KeyValuePair<string, string>(kv[0], kv[1]));
                    }
                    else if (kv.Length > 0)
                    {
                        res.Add(new KeyValuePair<string, string>(kv[0], ""));
                    }
                }
            }
            catch (Exception)
            {

            }
            return res.Count > 0 ? res.ToArray() : null;
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
            var report = StrmFileHelper.ProcessStrm(args[1], true, replacements);
            CommonLogger.LogLine($"Strm Matched:{report.MatchFiles}, Processed:{report.Replaced}", true);
            CommonLogger.LogLine($"Process Finish:{report.StartTime} --> {report.EndTime}, Duration: {report.Duration}", true);
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

        private static List<string> GetFollowArgs(string[] args, string matches, int follows)
        {
            return GetFollowArgs(args, 0, new string[] { matches }, follows);
        }

        private static List<string> GetFollowArgs(string[] args, int start, string matches, int follows)
        {
            return GetFollowArgs(args, start, new string[] { matches }, follows);
        }

        private static List<string> GetFollowArgs(string[] args, int start, string[] matches, int follows)
        {
            var res = new List<string>();
            for (int i = start; i < args.Length; i++)
            {
                var cur = args[i];
                if (matches.Contains(cur))
                {
                    if (args.Length > i + follows)
                    {
                        for (int j = 0; j < follows; j++)
                        {
                            res.Add(args[i + 1 + j]);
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"Required follow arguments[{follows}]: --{string.Join('|', matches)}");
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
                CommonLogger.LogLine($"Sync Start:{DateTime.Now}", true);
                var sync = new XiaoYaMetaSync();
                if (TryGetIgnoreConfigFile(args, out string file)) sync.AddIgnoreRelativePathsFromFile(file);
                sync.Sync(zipPath, extractPath, replacments);
                var duration = DateTime.Now - startDate;
                CommonLogger.LogLine($"Sync Finish:{startDate} --> {DateTime.Now}, Duration: {duration}", true);
            }
            catch (Exception ex)
            {
                CommonLogger.LogLine(ex.Message, true);
                CommonLogger.LogLine(ex.ToString(), true);
            }
        }

        private static bool TryGetIgnoreConfigFile(string[] args, out string file)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--ignore")
                {
                    if (i + 1 < args.Length)
                    {
                        file = args[i + 1];
                        if (File.Exists(file))
                        {
                            return true;
                        }
                        else
                        {
                            throw new ArgumentException($"Specific File Not Exists: --ignore {file}");
                        }
                    }
                    else
                    {
                        throw new ArgumentException("Required argument: --ignore <ignore relative path config file>");
                    }
                }
            }
            file = null;
            return false;
        }

        private static void CmdRemoveExpiredMeta(string[] args)
        {
            if (args.Length < 3)
            {
                PrintHelpRemoveExpiredMeta();
                return;
            }

            var zipPath = args[1];
            var extractPath = args[2];
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
                CommonLogger.LogLine($"Remove Expired Meta Start:{DateTime.Now}", true);
                new XiaoYaMetaSync().RemoveExpiredMeta(zipPath, extractPath);
                var duration = DateTime.Now - startDate;
                CommonLogger.LogLine($"Remove Expired Meta Finish:{startDate} --> {DateTime.Now}, Duration: {duration}", true);
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

}
