using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HandbrakeCLI_daemon
{
    public class HBQueueItem
    {
        public Watch WatchInstance;
        private readonly string fPath;
        private readonly string fName;
        private readonly string fExt;

        public HBQueueItem(Watch watchInstance, string fpath, string fname)
        {
            WatchInstance = watchInstance;
            this.fPath = fpath;
            this.fName = fname;
            this.fExt = Path.GetExtension(fPath); //Regex.Match(fname, "[.][a-zA-Z0-9]{1,4}$").Value;
        }

        public string FilePath
        {
            get { return fPath; }
        }
        public string FileName
        {
            get
            {
                return fName;
            }
        }
        public string Extention
        {
            get
            {
                return fExt;
            }
        }
    }

    public class QueueService : IQueueService
    {
        private readonly LoggingService logger;
        private Queue<HBQueueItem> HBQueue;
        private const string HBProc = "HandbrakeCLI";
        private const int SleepDelay = 5000;
        public QueueService(LoggingService loggingService)
        {
            logger = loggingService;
            HBQueue = new Queue<HBQueueItem>();
            Task.Run(() => { OnStart(); });
        }
        public string QueueString
        {
            get
            { return (HBQueue.Count == 0) ? "Empty" : String.Join(Environment.NewLine, HBQueue.Select(x => x.FileName)); }
        }
        public void Add(HBQueueItem item)
        {
            if (HBQueue.Where(x => x.FilePath == item.FilePath).ToList().Count > 0) throw new Exception(
                $"QueueItem already exists: {item.FilePath}");
            HBQueue.Enqueue(item);
            logger.Log($"Enqueued new item: {item.FileName}", LogSeverity.Info);
            logger.Log($"Queue is now: " + QueueString, LogSeverity.Info);
        }
        public void Remove(Watch watch, string fPath)
        {
            HBQueue = new Queue<HBQueueItem>(HBQueue.Where(x => x.FilePath != fPath));
            logger.Log($"Removed item from queue: {Path.GetFileName(fPath)}", LogSeverity.Info);
            logger.Log($"Queue is now: " + QueueString, LogSeverity.Info);
        }

        private void OnStart()
        {
            //Process p;
            while(true)
            {
                if(HBQueue.Count > 0)
                {
                    if (GetFileProcessName(HBQueue.Peek().FilePath) == null)
                    {
                        var poppedQueue = HBQueue.Dequeue();
                        
                        logger.Log($"Removed item from queue: {poppedQueue.FileName}", LogSeverity.Info);
                        logger.Log($"Queue is now: " + QueueString, LogSeverity.Verbose);
                        var argsSB = new StringBuilder();
                        var baseArgs = $"--preset-import-file \"{poppedQueue.WatchInstance.ProfilePath}\" -Z {poppedQueue.WatchInstance.ProfileName}" +
                            $" -i \"{poppedQueue.FilePath}\"";
                        argsSB.Append(baseArgs);
                        var tup = GetSubs(poppedQueue.FilePath);
                        if (tup.Item1.Count > 0)
                        {
                            argsSB.Append(" --srt-file \"" + String.Join(",", tup.Item1) + "\"");
                            argsSB.Append(" --srt-lang \"" + String.Join(",", tup.Item2) + "\"");
                            argsSB.Append(" --all-subtitles");
                        }
                        argsSB.Append($" -o \"{poppedQueue.WatchInstance.Destination +Daemon.Slash+ poppedQueue.FileName}\"");
                        logger.Log($"Encoding {poppedQueue.FileName} using: {argsSB}", LogSeverity.Info);
                        Process p = new Process
                        {
                            StartInfo = new ProcessStartInfo(HBProc, argsSB.ToString())
                        };
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = false;
                        p.StartInfo.CreateNoWindow = true;
                        p.Start();
                        p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        p.WaitForExit();
                        logger.Log("Encode completed.", LogSeverity.Verbose);
                    }
                }
                Thread.Sleep(SleepDelay);
            }
        }
        private Tuple<List<string>,List<string>> GetSubs(string fPath)
        {
            var tempsrtPATH = new List<string>();
            var tempLangs = new List<string>();
            var mediaRoot = Path.GetDirectoryName(fPath);
            foreach (var file in Directory.GetFiles(mediaRoot))
            {
                if (Path.GetExtension(file).Equals(".srt") && Path.GetFileNameWithoutExtension(mediaRoot)
                    .Contains(Path.GetFileNameWithoutExtension(fPath)))
                {
                    tempsrtPATH.Add(file);
                    tempLangs.Add(GetSubLang(file));
                }
            }
            if (Directory.Exists(mediaRoot + Daemon.Slash + "subs"))
            {
                foreach (var file in Directory.GetFiles(mediaRoot + Daemon.Slash + "subs"))
                {
                    if (Path.GetExtension(file).Equals(".srt"))
                    {
                        tempsrtPATH.Add(file);
                        tempLangs.Add(GetSubLang(file));
                    }
                }
            }
            return new Tuple<List<string>, List<string>>(tempsrtPATH, tempLangs);
        }
        public static string GetSubLang(string mediaSource)
        {
            var name = Path.GetFileName(mediaSource);
            //Match m = Regex.Match(name, @"[a-zA-Z0-9_\(\)]*$");
            Match m = Regex.Match(name, @"[a-zA-Z0-9_\(\)]*.srt$");
            if (m.Success)
            {
                if (m.Value.Equals(".srt")) return "und";
                return Regex.Match(m.Value, "[a-zA-Z]+").Value;
            }
            return "und";
        }
        public static string GetFileProcessName(string filePath)
        {
            Process[] procs = Process.GetProcesses();
            string fileName = Path.GetFileName(filePath);

            foreach (Process proc in procs)
            {
                if (proc.MainWindowHandle != new IntPtr(0) && !proc.HasExited)
                {
                    ProcessModule[] arr = new ProcessModule[proc.Modules.Count];

                    foreach (ProcessModule pm in proc.Modules)
                    {
                        if (pm.ModuleName == fileName)
                            return proc.ProcessName;
                    }
                }
            }

            return null;
        }
    }

    public interface IQueueService
    {
        public void Add(HBQueueItem item);
        public void Remove(Watch watch, string fname);
    }
}
