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


namespace HandBrake_daemon
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

    public class TestService : BackgroundService, IDisposable
    {
        private readonly ILogger<TestService> Logger;
        public TestService(ILogger<TestService> logger)
        {
            Logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
                Logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
        }
    }

    public class Worker : BackgroundService, IDisposable
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
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
        private static bool debug = false;
        private readonly IHostApplicationLifetime _appLifeTime;

        public QueueService(ILogger<QueueService> logService, IHostApplicationLifetime appLifeTime)
        {
            logger = logService;
            HBQueue = new Queue<HBQueueItem>();
            Debug.Assert(debug = true);
            _appLifeTime = appLifeTime;
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
            logger.LogInformation($"QUEUE=> Added: {item.FileName}");
            logger.LogDebug($"Queue is now: " + QueueString);
        }
        public void Remove(string fPath)
        {
            HBQueue = new Queue<HBQueueItem>(HBQueue.Where(x => x.FilePath != fPath));
            logger.LogInformation($"Removed item from queue: {Path.GetFileName(fPath)}");
            logger.LogDebug($"Queue is now: " + QueueString);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _appLifeTime.ApplicationStopped.Register(Dispose);
            return RunAsync(stoppingToken);
        }

        protected async Task RunAsync(CancellationToken stoppingToken)
        {
            //if (!File.Exists(HBProc)) throw new FileNotFoundException($"HandBrakeCLI could not be found in location: {HBProc}");
            while (!stoppingToken.IsCancellationRequested)
            {
                if (HBQueue.Count > 0 && (HBService==null || HBService.HasExited))
                {
                    if (IsFileReady(HBQueue.Peek().FilePath))
                    {
                        logger.LogDebug($"File: {HBQueue.Peek().FilePath} is not locked. Processing...");
                        var poppedQueue = HBQueue.Dequeue();

                        //logger.LogInformation($"Removed item from queue: {poppedQueue.FileName}");
                        //logger.LogDebug($"Queue is now: " + QueueString);
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

                        string destDir = poppedQueue.WatchInstance.Destination; ;
                        if (poppedQueue.WatchInstance.IsShow)
                        {
                            var DirName = new DirectoryInfo(Path.GetDirectoryName(poppedQueue.FilePath)).Name;
                            var matchReg = new Regex(@"(.*?).(s|season)\ ?(\d{1,2})", RegexOptions.IgnoreCase);

                            if (matchReg.IsMatch(DirName))
                            {
                                destDir = poppedQueue.WatchInstance.Destination + Path.DirectorySeparatorChar + matchReg.Match(DirName).Groups[1]
                                    + Path.DirectorySeparatorChar + "Season " + matchReg.Match(DirName).Groups[3];
                                Directory.CreateDirectory(destDir);
                            }
                        }

                        argsSB.Append($" -o \"{destDir + Path.DirectorySeparatorChar + poppedQueue.FileName}\"");


                        logger.LogInformation($"Encoding {poppedQueue.FileName}");
                        logger.LogDebug($"Used Args: {argsSB}");

                        HBService = new Process
                        {
                            StartInfo = new ProcessStartInfo(HBProc, argsSB.ToString())
                        };
                        HBService.StartInfo.UseShellExecute = false;
                        HBService.StartInfo.RedirectStandardOutput = false;
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) HBService.StartInfo.RedirectStandardError = true;
                        HBService.StartInfo.CreateNoWindow = true;
                        stoppingToken.Register(() => HBService?.Kill()); //Errors here are likely due to HandBrakeCLI exec not being found/started.
                        HBService.EnableRaisingEvents = true;
                        HBService.Exited += new EventHandler((sender, e) => HBService_Exited(sender, e, poppedQueue)); //prevents continuation?
                        logger.LogDebug($"Starting HBCLI");
                        HBService.Start();
                        logger.LogDebug($"HBCLI Started: {HBService.StartTime}");
                        HBService.PriorityClass = ProcessPriorityClass.BelowNormal;
                    }
                    else logger.LogWarning($"File {HBQueue.Peek().FilePath} is locked");
                }
                await Task.Delay(SleepDelay, stoppingToken);
            }
        }

        private void HBService_Exited(object sender, EventArgs e, HBQueueItem poppedQueue)
        {
            logger.LogInformation("Encode completed.");
            if (!debug)
            {
                logger.LogDebug($"Origin: {poppedQueue.WatchInstance.Origin}, FilePath: {poppedQueue.FilePath}");
                if (String.IsNullOrEmpty(poppedQueue.WatchInstance.Origin)) File.Delete(poppedQueue.FilePath);
                else File.Move(poppedQueue.FilePath, poppedQueue.WatchInstance.Origin);
                logger.LogDebug("Original file has been moved/deleted.");
            }
        }

        public static Tuple<List<string>,List<string>> GetSubs(string fPath)
        {
            var tempsrtPATH = new List<string>();
            var tempLangs = new List<string>();
            var mediaRoot = Path.GetDirectoryName(fPath);
            foreach (var file in Directory.GetFiles(mediaRoot))
            {
                if (Path.GetExtension(file).Equals(".srt") && Path.GetFileNameWithoutExtension(file).Contains(Path.GetFileNameWithoutExtension(fPath), StringComparison.OrdinalIgnoreCase))
                {
                    //Console.WriteLine($"Compared {Path.GetFileNameWithoutExtension(file)} contains?: {Path.GetFileNameWithoutExtension(fPath)} MediaRoot:{fPath}");
                    tempsrtPATH.Add(file);
                    if (Path.GetFileNameWithoutExtension(file).Equals(Path.GetFileNameWithoutExtension(fPath), StringComparison.OrdinalIgnoreCase))
                        tempLangs.Add("und");
                    else
                        tempLangs.Add(GetSubLang(file));
                }
            }

            //We need to ignore invariants due to Directory.Exists ignoring on NTFS but not Ext4.
            try
            {
                var SubDir = new DirectoryInfo(mediaRoot + Path.DirectorySeparatorChar).GetDirectories().First(x => x.Name.Equals("subs", StringComparison.OrdinalIgnoreCase)).Name;
                //if (!(logger is null)) logger.LogDebug($"Ext: {SubDir}");
                if (Directory.Exists(mediaRoot + Path.DirectorySeparatorChar + SubDir))
                {
                    foreach (var file in Directory.GetFiles(mediaRoot + Path.DirectorySeparatorChar + SubDir))
                    {
                        //if (!(logger is null)) logger.LogDebug($"Ext: {Path.GetExtension(file)}");
                        if (Path.GetExtension(file).Equals(".srt"))
                        {
                            //if (!(logger is null)) logger.LogDebug($"Compared {Path.GetFileNameWithoutExtension(file)} contains?: {Path.GetFileNameWithoutExtension(fPath)} MediaRoot:{fPath}");
                            var startsReg = new Regex(@"\d{1,2}_");
                            if (Path.GetFileNameWithoutExtension(file).Equals(Path.GetFileNameWithoutExtension(fPath), StringComparison.OrdinalIgnoreCase))
                            {
                                tempsrtPATH.Add(file);
                                tempLangs.Add("und");
                            }
                            else if (Path.GetFileNameWithoutExtension(file).Contains(Path.GetFileNameWithoutExtension(fPath), StringComparison.OrdinalIgnoreCase) || startsReg.IsMatch(file))
                            {
                                tempsrtPATH.Add(file);
                                tempLangs.Add(GetSubLang(file));
                            }
                        }
                    }
                }
            }
            catch (InvalidOperationException) { }
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
            if (HBService != null)
            {
                HBService.Dispose();
            }
            base.Dispose();
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
