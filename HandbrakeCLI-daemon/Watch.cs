using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HandbrakeCLI_daemon
{
    public class Watch : IComparable
    {
        public Watch(string source, string destination, string origin, string profilePath, List<string> extentions)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.ProfilePath = profilePath ?? throw new ArgumentNullException(nameof(profilePath));
            this.Extentions = extentions;
            this.Origin = origin;
        }

        public string Source { private set; get; }
        public string Destination { private set; get; }
        public string Origin { private set; get; }
        public string ProfilePath { private set; get; }
        public List<string> Extentions { private set; get; }
        public string ProfileName
        {
            get
            {
                return Path.GetFileName(ProfilePath).Replace(".json",String.Empty);
            }
        }

        public int CompareTo(object obj)
        {
            var watch = obj as Watch;
            return (watch.Source.CompareTo(this.Source) == 0) ? watch.Destination.CompareTo(this.Destination) : watch.Source.CompareTo(this.Source);
        }
    }

    public interface IWatcherService
    {
        public void ToggleWatchers(bool? state = null);
        //public void AddWatch(string source, string destination, string postDeletion, string profile, List<string> ext = null);
        public void RemoveWatch(Watch watch);
    }

    public class WatcherService : IWatcherService, IHostedService, IDisposable
    {
        private List<Watch> Watching = new List<Watch>();
        readonly ILogger<WatcherService> logger;
        private readonly List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        private readonly QueueService _QueueService;
        private static string ConfPath;
        private static IHostApplicationLifetime HostApp;

        public WatcherService(ILogger<WatcherService> loggingService, IHostApplicationLifetime hostApp /* IServiceProvider _provider*/)
        {
            logger = loggingService;
            logger.LogInformation("Loading watchers");
            ConfPath = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ? "/etc/HandBrakeDaemon.conf" : "HandBrakeDaemon.conf";
            if (!File.Exists(ConfPath)) File.WriteAllText(ConfPath, "#Use double quotes for filepaths with spaces. Providing a null value in the argument for `Origin` directory will delete the original file post-transcoding. Providing a directory will move the file instead." + Environment.NewLine + 
                "#\"source\" \"destination\" \"origin\" \"profile  path\" ext1,ext2,ext3..." + Environment.NewLine +
                "#/mnt/media/source /mnt/media/destination  \"\" /mnt/media/profile.json \"\"");
            HostApp = hostApp;
            _QueueService = QueueService.Instance;
            LoadWatchlist();
            ScanWatchDirs();
        }

        private void AddQueueItem(Watch watch, string filePath)
        {
            //is directory?
            logger.LogInformation($"{string.Join(",", watch.Extentions)} vs FileExt: {Path.GetExtension(filePath).Replace(".", string.Empty)}");
            if (watch.Extentions.Contains(Path.GetExtension(filePath).Replace(".",string.Empty)))
            {
                _QueueService.Add(new HBQueueItem(watch, filePath, Path.GetFileName(filePath)));
                logger.LogInformation($"SCANNER=> Media found: {filePath}");
            }
            else logger.LogDebug($"Scanner=> Skipping: {filePath}");
        }

        private void ScanWatchDirs()
        {
            foreach(var watch in Watching)
            {
                ScanDir(watch, watch.Source);
            }
        }

        private void ScanDir(Watch watch, string scanPath)
        {
            foreach (var dir in Directory.GetDirectories(scanPath))
            {
                ScanDir(watch, dir);
            }
            foreach (var file in Directory.GetFiles(scanPath))
            {
                AddQueueItem(watch, file);
            }
        }

        FileSystemWatcher CreateWatcher(Watch instance)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(instance.Source)
            {
                Path = instance.Source
            };
            watcher.IncludeSubdirectories = true;
            watcher.Created += (sender, e) => Watcher_FileCreated(sender, e, instance);
            watcher.Deleted += (sender, e) => Watcher_FileDeleted(sender, e, instance);
            return watcher;
        }

        public void ToggleWatchers(bool? state = null)
        {
            if (Watchers.Count == 0)
            {
                logger.LogWarning("No watchers detected. Add watchers to the service before starting it.");
                HostApp.StopApplication();
                //Environment.Exit(0);
            }
            foreach(var watcher in Watchers)
            {
                 watcher.EnableRaisingEvents = state ?? !watcher.EnableRaisingEvents;
            }
        }

        public void RemoveWatch(Watch watch)
        {
            if (Watching.Contains(watch))
            {
                Watching.Remove(watch);
            }
        }

        private void Watcher_FileDeleted(object _, FileSystemEventArgs e, Watch instance)
        {
            logger.LogInformation($"WATCHER=> File deleted: {e.FullPath}");
            _QueueService.Remove(instance, e.FullPath);
        }

        private void Watcher_FileCreated(object _, FileSystemEventArgs e, Watch instance)
        {
            if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory))
            {
                ScanDir(instance, e.FullPath);
            }
            else
            {
                AddQueueItem(instance, e.FullPath);
            }
        }

        private void LoadWatchlist()
        {
            Watching = ReadConf(ConfPath) ?? new List<Watch>();
            logger.LogInformation($"Loaded {Watching.Count} items to watchers.");
            foreach (var instance in Watching)
            {
                Watchers.Add(CreateWatcher(instance));
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            ToggleWatchers();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ToggleWatchers();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            foreach(var watch in Watchers)
            {
                watch.Dispose();
            }
            //throw new NotImplementedException();
        }
        public static List<Watch> ReadConf(string fPath)
        {
            using StreamReader sr = new StreamReader(fPath);
            string line;
            var temp = new List<Watch>();
            var splitReg = new Regex(@"[ ](?=(?:[^""]*""[^""]*"")*[^""]*$)");
            while ((line = sr.ReadLine()) != null)
            {
                if (line.ToCharArray()[0] != '#')
                {
                    var args = splitReg.Split(line);
                    var exts = (args[4] == "\"\"") ? new List<string> { "mp4", "mkv", "avi" } : args[4].Split(",").ToList();
                    for(int i=0;i<2;i++)
                    {
                        if (!Directory.Exists(args[i])) throw new Exception($"Config references a directory which does not exist: {args[i]}");
                    }
                    if (!File.Exists(args[3])) throw new Exception($"Config references a filepath which does not exist: {args[3]}");
                    var toAdd = new Watch(args[0], args[1], (args[2] == "\"\"") ? string.Empty : args[2], args[3], exts);
                    temp.Add(toAdd);
                }
            }
            if (temp.Count == 0) return null;
            return temp;
        }
    }
}