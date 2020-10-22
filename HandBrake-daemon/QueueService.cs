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
    public class MediaItem
    {
        public Watch WatchInstance;
        /// <summary>
        /// Returns the full source filepath 
        /// </summary>
        public string FilePath { get; }
        /// <summary>
        /// Returns only the FileName, inclusive of extention.
        /// </summary>
        public string FileName { get; }
        /// <summary>
        /// Returns the extention of the file.
        /// </summary>
        public string Extention { get; }

        /// <summary>
        /// Instnatiates a new MediaItem
        /// </summary>
        /// <param name="watchInstance">The watch instance this media item belongs too.</param>
        /// <param name="fpath">The filepath for the source media.</param>
        public MediaItem(Watch watchInstance, string fpath)
        {
            WatchInstance = watchInstance;
            this.FilePath = fpath;
            this.FileName = Path.GetFileName(fpath);
            this.Extention = Path.GetExtension(FilePath); //Regex.Match(fname, "[.][a-zA-Z0-9]{1,4}$").Value;
        }
    }

    public class QueueService : BackgroundService, IDisposable
    {
        public static QueueService Instance { get; private set; }
        private readonly ILogger<QueueService> logger;
        private Queue<MediaItem> HBQueue;
        private const string HBProc = "HandBrakeCLI";
        private const int SleepDelay = 5000;
        private Process HBService;
        private static bool debug = false;
        private readonly IHostApplicationLifetime _appLifeTime;

        public QueueService(ILogger<QueueService> logService, IHostApplicationLifetime appLifeTime)
        {
            logger = logService;
            if (!HBCIsInstalled()) throw new Exception("Please install HandBrakeCLI before starting the service.");
            HBQueue = new Queue<MediaItem>();
            Debug.Assert(debug = true); //set to true if we are built for debug, used for certain logs and non deletion of media post-process.
            _appLifeTime = appLifeTime;
            Instance = this;
        }
        public string QueueString
        {
            get
            { return (HBQueue.Count == 0) ? "Empty" : String.Join(Environment.NewLine, HBQueue.Select(x => x.FileName)); }
        }
        public void Add(MediaItem item)
        {
            if (HBQueue.Where(x => x.FilePath == item.FilePath).ToList().Count > 0) throw new Exception(
                $"QueueItem already exists: {item.FilePath}");
            HBQueue.Enqueue(item);
            logger.LogInformation($"QUEUE=> Added: {item.FileName}");
            logger.LogDebug($"Queue is now: " + QueueString);
        }
        public void Remove(string fPath)
        {
            HBQueue = new Queue<MediaItem>(HBQueue.Where(x => x.FilePath != fPath));
            logger.LogInformation($"Removed item from queue: {Path.GetFileName(fPath)}");
            logger.LogDebug($"Queue is now: " + QueueString);
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _appLifeTime.ApplicationStopped.Register(Dispose);
            return RunAsync(stoppingToken);
        }
        public override void Dispose()
        {
            if (HBService != null)
            {
                HBService.Dispose();
            }
            base.Dispose();
        }
        protected async Task RunAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (HBQueue.Count > 0 && (HBService==null || HBService.HasExited))
                {
                    if (IsFileReady(HBQueue.Peek().FilePath)) //Check that data is not being written to the source media before we process it.
                    {
                        logger.LogDebug($"File: {HBQueue.Peek().FilePath} is not locked. Processing...");
                        var poppedQueue = HBQueue.Dequeue();
                        Process pre = null;
                        if (!string.IsNullOrEmpty(poppedQueue.WatchInstance.PreScript))
                        {
                            var preScriptArgs = new List<string>() { poppedQueue.FilePath.Enquote(), poppedQueue.WatchInstance.Destination.Enquote(), poppedQueue.WatchInstance.Origin.Enquote(), poppedQueue.WatchInstance.IsShow.ToString() };
                            pre = ExecuteScript(poppedQueue.WatchInstance.PreScript, preScriptArgs);
                        }
                        if (string.IsNullOrEmpty(poppedQueue.WatchInstance.PreScript) || pre?.ExitCode == 0)
                        {
                            var argsSB = new StringBuilder();
                            if (!string.IsNullOrEmpty(poppedQueue.WatchInstance.ProfilePath)) argsSB.Append($"--preset-import-file \"{poppedQueue.WatchInstance.ProfilePath}\" ");
                            argsSB.Append($"-Z \"{poppedQueue.WatchInstance.ProfileName}\"" + $" -i \"{poppedQueue.FilePath}\"");

                            //Add all relevant subtitles to the process
                            var tup = GetSubs(poppedQueue.FilePath);
                            if (tup.Item1.Count > 0)
                            {
                                argsSB.Append(" --srt-file \"" + String.Join(",", tup.Item1) + "\"");
                                argsSB.Append(" --srt-lang \"" + String.Join(",", tup.Item2) + "\"");
                                argsSB.Append(" --all-subtitles");
                            }

                            //Determine output directory for convenience
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

                            //Set up the process
                            logger.LogInformation($"Encoding {poppedQueue.FileName}");
                            logger.LogDebug($"Encoding using args: {argsSB}");
                            HBService = new Process
                            {
                                StartInfo = new ProcessStartInfo(HBProc, argsSB.ToString())
                            };
                            HBService.StartInfo.UseShellExecute = false;
                            HBService.StartInfo.RedirectStandardOutput = false;
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) HBService.StartInfo.RedirectStandardError = true; //if we redirect standard error we can capture progress output.
                            HBService.StartInfo.CreateNoWindow = true;
                            stoppingToken.Register(() => HBService?.Kill()); //"No process is associated with this object" errors here are likely due to HandBrakeCLI exec not being found (is PATH correct?),
                                                                             //or to referencing HBservice before it has been started.
                            HBService.EnableRaisingEvents = true; //Allows our stopping token to interrupt the transcode
                            HBService.Exited += new EventHandler((sender, e) => HBService_Exited(sender, e, poppedQueue));
                            HBService.Start();
                            logger.LogDebug($"Started HBCLI @ {HBService.StartTime}");
                            HBService.PriorityClass = ProcessPriorityClass.BelowNormal;
                        } 
                        else
                        {
                            logger.LogWarning($"{poppedQueue.WatchInstance.PreScript} exited with ErrorCode({pre.ExitCode}), skipping processing source media: {poppedQueue.FilePath}");
                        }
                    }
                    else logger.LogWarning($"File {HBQueue.Peek().FilePath} is locked"); //Wait until the file is not busy
                }
                await Task.Delay(SleepDelay, stoppingToken);
            }
        }

        /// <summary>
        /// Called post-encode but before RunAsync returns.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <param name="poppedQueue">The reference to the QueueItem which was processed by HandBrakeCLI</param>
        private void HBService_Exited(object sender, EventArgs e, MediaItem poppedQueue)
        {
            logger.LogInformation("Encode completed.");
            if (!debug) //perform post-processes & media cleanup
            {
                if (HBService?.ExitCode == 0)
                {
                    bool delete = String.IsNullOrEmpty(poppedQueue.WatchInstance.Origin);
                    //logger.LogDebug($"Origin: {poppedQueue.WatchInstance.Origin}, FilePath: {poppedQueue.FilePath}");
                    if (delete) File.Delete(poppedQueue.FilePath);
                    else File.Move(poppedQueue.FilePath, poppedQueue.WatchInstance.Origin + Path.DirectorySeparatorChar + poppedQueue.FileName);
    
                    logger.LogDebug("Original file has been " + (delete ? "deleted." : "moved."));
                    if (!string.IsNullOrEmpty(poppedQueue.WatchInstance.PostScript))
                    {
                        var destination = poppedQueue.WatchInstance.Destination + Path.DirectorySeparatorChar + poppedQueue.FileName;
                        var post = ExecuteScript(poppedQueue.WatchInstance.PostScript, new List<string>() { destination.Enquote(), poppedQueue.WatchInstance.Origin.Enquote(), poppedQueue.WatchInstance.IsShow.ToString()});
                        if (post.ExitCode != 0) logger.LogWarning($"PostScript {poppedQueue.WatchInstance.PostScript} exited with ErrorCode({post.ExitCode}) when processing: {destination}");
                    }
                } else {
                    logger.LogWarning($"Non 0 exitcode for: {poppedQueue.FilePath}, skipping post processing.");
                }
            }
        }

        /// <summary>
        /// Compiles and formats lists of sub-files and their corresponding languages ready for HandBrakeCLI
        /// </summary>
        /// <param name="mediaSource">The file path of the source media</param>
        /// <returns>The first list is a list of relevant subtitle paths for the current media source, the second is the corresponding list of languages for those files.</returns>
        public static Tuple<List<string>,List<string>> GetSubs(string mediaSource)
        {
            var tempsrtPATH = new List<string>();
            var tempLangs = new List<string>();
            var mediaRoot = Path.GetDirectoryName(mediaSource);

            //First check the source media's directory for files of the same prefix
            foreach (var file in Directory.GetFiles(mediaRoot))
            {
                if (Path.GetExtension(file).Equals(".srt") && Path.GetFileNameWithoutExtension(file).Contains(Path.GetFileNameWithoutExtension(mediaSource), StringComparison.OrdinalIgnoreCase))
                {
                    tempsrtPATH.Add(file);
                    if (Path.GetFileNameWithoutExtension(file).Equals(Path.GetFileNameWithoutExtension(mediaSource), StringComparison.OrdinalIgnoreCase))
                        tempLangs.Add("und");
                    else
                        tempLangs.Add(GetSubLang(file));
                }
            }

            //Next we check if a "subs" directory exists, if it does we will also check for names of the same prefix, however we will also check for .srt's that contain a languagecode prefix follow by a language.
            //We need to ignore invariants due to Directory.Exists ignoring on NTFS but not Ext4.
            try
            {
                var SubDir = new DirectoryInfo(mediaRoot + Path.DirectorySeparatorChar).GetDirectories().First(x => x.Name.Equals("subs", StringComparison.OrdinalIgnoreCase)).Name;
                if (Directory.Exists(mediaRoot + Path.DirectorySeparatorChar + SubDir))
                {
                    foreach (var file in Directory.GetFiles(mediaRoot + Path.DirectorySeparatorChar + SubDir))
                    {
                        if (Path.GetExtension(file).Equals(".srt"))
                        {
                            var startsReg = new Regex(@"\d{1,2}_");
                            if (Path.GetFileNameWithoutExtension(file).Equals(Path.GetFileNameWithoutExtension(mediaSource), StringComparison.OrdinalIgnoreCase))
                            {
                                tempsrtPATH.Add(file);
                                tempLangs.Add("und");
                            }
                            else if (Path.GetFileNameWithoutExtension(file).Contains(Path.GetFileNameWithoutExtension(mediaSource), StringComparison.OrdinalIgnoreCase) || startsReg.IsMatch(file))
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

        /// <summary>
        /// Given a filename, will strip any language from a sub-extension within the file. For example test.English.txt
        /// </summary>
        /// <param name="mediaSource">The file name (not path) to extract a language from.</param>
        /// <returns>Returns the sub-extension after parsing and stripping of special characters, or "und" if the regex failed to match any text.</returns>
        public static string GetSubLang(string mediaSource)
        {
            var name = Path.GetFileName(mediaSource);
            Match m = Regex.Match(name, @"[a-zA-Z0-9_\(\)]*.srt$");
            if (m.Success)
            {
                if (m.Value.Equals(".srt")) return "und";
                return Regex.Match(m.Value, "[a-zA-Z]+").Value;
            }
            return "und";
        }

        /// <summary>
        /// Used: https://stackoverflow.com/users/21299/gordon-thompson
        /// To test if the file is currently being written too.
        /// This method only works for NTFS, is ineffectual on Linux. Needs to be addressed at some point.
        /// </summary>
        /// <param name="path">The file path to test if data is currently being written too</param>
        /// <returns>True when a lock is able to be acquired, false otherwise. </returns>
        public static bool IsFileReady(string path)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using FileStream inputStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return inputStream.Length > 0;
            }
            catch (Exception) { return false; }
        }
        /// <summary>
        /// Check that HB-cli is installed before we proceed.
        /// </summary>
        /// <returns>True when one of the $PATH locations contains a HandBrakeCLI/.exe file</returns>
        public bool HBCIsInstalled()
        {
            foreach(var dir in Environment.GetEnvironmentVariable("PATH").Split(MultipathSeperatorCharacter()))
            {
                logger.LogDebug($"Checking for HB-CLI in: {dir + Path.DirectorySeparatorChar + HBProc}");
                if (File.Exists(dir + Path.DirectorySeparatorChar + HBProc)) return true;
                if (File.Exists(dir + Path.DirectorySeparatorChar + HBProc + ".exe")) return true;
            }
            return false;
        }
        /// <summary>
        /// Gets the platform specific multi-path delimiter character.
        /// </summary>
        /// <returns>The platform specific separator character.</returns>
        public static char MultipathSeperatorCharacter()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ';';
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) return ':';
            throw new PlatformNotSupportedException();
        }

        /// <summary>
        /// Shell script executer
        /// </summary>
        /// <param name="path">Path to the shell script</param>
        /// <param name="args">Arguments to pass to the script</param>
        /// <returns>Returns process after completion.</returns>
        public static Process ExecuteScript(string path, List<string> args)
        {
            string argList = string.Join(" ", args);
            var p = Process.Start(
                new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = argList,
                    WorkingDirectory = Path.GetDirectoryName(path)
                });
            p.WaitForExit();
            return p;
        }
    }

    public static class ThreadExtentions
    {
        /// <summary>
        /// Waits asynchronously for the process to exit.
        /// </summary>
        /// <param name="process">The process to wait for cancellation.</param>
        /// <param name="cancellationToken">A cancellation token. If invoked, the task will return 
        /// immediately as canceled.</param>
        /// <returns>A Task representing waiting for the process to end.</returns>
        public static Task WaitForExitAsync(this Process process,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (process.HasExited) return Task.CompletedTask;

            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);
            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(() => tcs.SetCanceled());

            return process.HasExited ? Task.CompletedTask : tcs.Task;
        }
    }
    public static class StringExtentions
    {
        /// <summary>
        /// Enquotes the given string.
        /// </summary>
        /// <param name="text">The string to be encapsulated.</param>
        /// <param name="quoteChar">The character to encapsulate with.</param>
        /// <returns>Returns the encapsulated string</returns>
        public static string Enquote(this string text, char? quoteChar = null)
        {
            var encapsulator = (quoteChar == null) ? '"' : quoteChar.Value;
            return $"{encapsulator}{text}{encapsulator}";
        }
    }
}
