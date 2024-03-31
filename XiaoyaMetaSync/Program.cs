using XiaoyaMetaSync.CoreLib;
namespace XiaoyaMetaSync
{
    internal class Program
    {
        private static void PrintHelp()
        {
            PrintHelpSync();
            PrintHelpStrm();
            PrintHelpGenStrm();
        }

        private static void PrintHelpSync()
        {
            Console.WriteLine("Usage: --sync <xiaoya meta zip> <output path> [--kodi] [<--replace|-R> <strm file old string> <strm file new string>]...");
        }
        private static void PrintHelpStrm()
        {
            Console.WriteLine("Usage: --strm <dir> [--kodi] [<--replace|-R> <strm file old string> <strm file new string>]...");
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
                    default: PrintHelp(); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void CmdGetStrm(string[] args)
        {
            if (args.Length < 4)
            {
                PrintHelpGenStrm();
                return;
            }
            var mediaRootPath = args[1];
            var urlPrefix = args[2];
            var outputPath = args[3];
            XiaoYaMetaSync.RecursiveSyncMediaToStrm(mediaRootPath, mediaRootPath, urlPrefix, outputPath, args.Contains("--only_strm"), args.Contains("--rewrite_meta"), args.Contains("--rewrite_strm"), args.Contains("--encode_url"));
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

}
