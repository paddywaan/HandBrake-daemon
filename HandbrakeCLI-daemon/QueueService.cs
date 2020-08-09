using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


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

    public class QueueService : BackgroundService, IDisposable
    {
        public static QueueService Instance { get; private set; }
        private readonly ILogger<QueueService> logger;
        private Queue<HBQueueItem> HBQueue;
        private const string HBProc = "HandBrakeCLI";
        private const int SleepDelay = 5000;
        private Process HBService;
        private object queuelock;
        public QueueService(ILogger<QueueService> logService)
        {
            logger = logService;
            HBQueue = new Queue<HBQueueItem>();
            //Task.Run(() => { OnStart(); });
            Instance = this;
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
            logger.LogInformation($"Enqueued new item: {item.FileName}");
            logger.LogDebug($"Queue is now: " + QueueString);
        }
        public void Remove(Watch watch, string fPath)
        {
            HBQueue = new Queue<HBQueueItem>(HBQueue.Where(x => x.FilePath != fPath));
            logger.LogInformation($"Removed item from queue: {Path.GetFileName(fPath)}");
            logger.LogDebug($"Queue is now: " + QueueString);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (HBQueue.Count > 0)
                {
                    if (IsFileReady(HBQueue.Peek().FilePath))

                    {
                        logger.LogDebug($"File: {HBQueue.Peek().FilePath} is not locked. Processing...");
                        var poppedQueue = HBQueue.Dequeue();

                        logger.LogInformation($"Removed item from queue: {poppedQueue.FileName}");
                        logger.LogDebug($"Queue is now: " + QueueString);
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
                        argsSB.Append($" -o \"{poppedQueue.WatchInstance.Destination + Daemon.Slash + poppedQueue.FileName}\"");
                        logger.LogInformation($"Encoding {poppedQueue.FileName} using: {argsSB}");
                        Process p = new Process
                        {
                            StartInfo = new ProcessStartInfo(HBProc, argsSB.ToString())
                        };
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.RedirectStandardOutput = false;
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) p.StartInfo.RedirectStandardError = true;
                        //p.StandardOutput.
                        p.StartInfo.CreateNoWindow = true;
                        HBService = p;
                        HBService.Start();
                        //string output = p.StandardOutput.ReadToEnd();
                        p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        await p.WaitForExitAsync();
                        try
                        {
                            if (p.ExitCode == 0)
                            {
                                if (poppedQueue.WatchInstance.Origin == String.Empty) File.Delete(poppedQueue.FilePath);
                                else File.Move(poppedQueue.FilePath, poppedQueue.WatchInstance.Origin);
                                logger.LogInformation("Encode completed.");
                            }
                            else logger.LogError($"Error encoding file: {p.StandardOutput.ReadToEnd()}");
                        }
                        catch (IOException)
                        {
                            logger.LogError($"Permission denied to move/delete the source file. Please make sure the service is run as the appropriate group/owner for the source directory: {poppedQueue.FilePath}");
                        }
                    }
                    else logger.LogWarning("File is locked.");
                }
                await Task.Delay(SleepDelay, stoppingToken);
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

        public static bool IsFileReady(string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
                return inputStream.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if(HBService != null)
                HBService.Dispose();
        }
        
    }


    /// <summary>
    /// Taken from Ryan's WaitForExitAsync:
    /// https://stackoverflow.com/questions/470256/process-waitforexit-asynchronously
    /// https://stackoverflow.com/users/2266345/ryan
    /// </summary>
    public static class ProcessExtensions
    {
        public static async Task WaitForExitAsync(this Process process, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void Process_Exited(object sender, EventArgs e)
            {
                tcs.TrySetResult(true);
            }

            process.EnableRaisingEvents = true;
            process.Exited += Process_Exited;

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    await tcs.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                process.Exited -= Process_Exited;
            }
        }
    }

}
