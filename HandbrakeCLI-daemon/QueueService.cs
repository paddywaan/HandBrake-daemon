using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HandbrakeCLI_daemon
{
    public class HBQueueItem
    {
        public Watch WatchInstance;
        private bool Processing;
        private string fPath;
        private string fName;
        private string fExt;

        public HBQueueItem(Watch watchInstance, bool processing, string fpath, string fname)
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

    }

    /// <summary>
    /// Scan watching dirs
    /// </summary>
    public class QueueService : IQueueService
    {
        private LoggingService logger;
        private Queue<HBQueueItem> HBQueue;
        private static string HBProc = "HandbrakeCLI";
        public QueueService(LoggingService loggingService)
        {
            logger = loggingService;
            HBQueue = new Queue<HBQueueItem>();
            Task.Run(() => { OnStart(); });
        }
        public string QueueString
        {
            get
            { return String.Join(Environment.NewLine, HBQueue.Select(x => x.FilePath)); }
        }
        public void Add(HBQueueItem item)
        {
            if (HBQueue.Where(x => x.FilePath == item.FilePath).ToList().Count > 0) throw new Exception(
                $"QueueItem already exists: {item.FilePath}");
            HBQueue.Enqueue(item);
            logger.Log($"Enqueued new item: {item.FilePath}", LogSeverity.Info);
            logger.Log($"Queue is now: {Environment.NewLine}" + QueueString, LogSeverity.Info);
        }
        public void Remove(Watch watch, string fPath)
        {
            HBQueue = new Queue<HBQueueItem>(HBQueue.Where(x => x.FilePath != fPath));
            logger.Log($"Removed item from queue: {fPath}", LogSeverity.Info);
            logger.Log($"Queue is now: {Environment.NewLine}" + QueueString, LogSeverity.Info);
        }

        private void OnStart()
        {
            //Process p;
            while(true)
            {
                if(HBQueue.Count > 0)
                {
                    var instance = HBQueue.Dequeue();
                    Process.Start(new ProcessStartInfo {
                        FileName = HBProc,
                        Arguments = $"--preset-import-file \"{instance.WatchInstance.ProfilePath}\" -Z {instance.WatchInstance.ProfileName}" +
                        $" -i \"{instance.FilePath}\" -o \"{instance.WatchInstance.Destination + Daemon.Slash + instance.FileName}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                        //HandBrakeCLI -Z MyPreset -i inputfile.mpg -o outputfile.mp4
                    }).WaitForExit();
                }
            }
        }
    }

    public interface IQueueService
    {
        public void Add(HBQueueItem item);
        public void Remove(Watch watch, string fname);
    }
}
