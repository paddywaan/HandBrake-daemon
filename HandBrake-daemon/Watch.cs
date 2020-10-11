using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using IniParser;
using System.Reflection;

namespace HandBrake_daemon
{
    public class Watch : IComparable
    {
        public Watch(string source, string destination, string origin, string profilePath, List<string> extentions, bool show)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.ProfilePath = profilePath ?? throw new ArgumentNullException(nameof(profilePath));
            this.Extentions = extentions ?? new List<string>{ "mp4", "mkv", "avi" };
            this.Origin = origin;
            this.IsShow = show;
        }

        public string Source { private set; get; }
        public string Destination { private set; get; }
        public string Origin { private set; get; }
        public string ProfilePath { private set; get; }
        public List<string> Extentions { private set; get; }
        public bool IsShow { private set; get; }
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
        private const string CONFNAME = "handbrake-daemon.conf";
        public WatcherService(ILogger<WatcherService> loggingService, IHostApplicationLifetime hostApp /* IServiceProvider _provider*/)
        {
            logger = loggingService;
            logger.LogInformation("Loading watchers");
            ConfPath = (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ? "/etc/"+ CONFNAME : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + CONFNAME;
            if (!File.Exists(ConfPath))
            {
                var defConf = Assembly.GetExecutingAssembly().GetManifestResourceStream("HandBrake_daemon.default.conf");
                using var sr = new StreamReader(defConf);
                File.WriteAllText(ConfPath, sr.ReadToEnd());
            }
            HostApp = hostApp;
            _QueueService = QueueService.Instance;
            LoadWatchlist();
            CheckPermissions();
            ScanWatchDirs();
        }

        private void CheckPermissions()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                //string serviceFile = "/etc/systemd/system/handbrake-daemon.service";
                //if (!File.Exists(serviceFile)) throw new Exception("SystemD service unit for handbrake-daemon does not exist. Please install the service before proceeding.");
                //var userReg = @"^[#?]User=(.*)$";
                //var groupReg = @"^[#?]Group=(.*)$";
                //string gid, uid;
                //var txt = string.Join(Environment.NewLine, File.ReadAllLines(serviceFile));
                //logger.LogDebug($"{txt}");
                ////if (userMatch.IsMatch(txt) && groupMatch.IsMatch(txt))
                //var umatch = Regex.Match(txt, userReg, RegexOptions.Multiline);
                //var gmatch = Regex.Match(txt, groupReg, RegexOptions.Multiline);
                ////logger.LogDebug($"{ umatch.Groups[1]}");
                ////logger.LogDebug($"{ gmatch.Groups[1]}");
                //if (umatch.Success && gmatch.Success)
                //{
                    
                    //uid = umatch.Groups[1].Value;
                    //gid = gmatch.Groups[1].Value;
                    foreach (var watch in Watching)
                    {
                        if (!(HasDirectoryAuth(watch.Source) && HasDirectoryAuth(watch.Destination)))
                        {
                            logger.LogError($"Invalid permissions to: {(!HasDirectoryAuth(watch.Source) ? watch.Source : watch.Destination)}");
                        }
                    }
                //}
                //else logger.LogDebug("Unable to match user and group from service file.");
            }
        }

        /// <summary>
        /// naive OS Agnostic method of returning directory permissions
        /// </summary>
        /// <param name="path">Directory path to check permissions</param>
        /// <param name="user">The user ID of the current process</param>
        /// <param name="group">The group ID of the current process</param>
        /// <returns>true if the current process has RWX permissions and is owner.</returns>
        private bool HasDirectoryAuth(string path)
        {
            try
            {
                File.Create(path + Path.DirectorySeparatorChar + "readwritetestfile.txt").Dispose();
                File.Delete(path + Path.DirectorySeparatorChar + "readwritetestfile.txt");
                return true;
            }
            catch (IOException) {
                return false;
            }
        }

        private void AddQueueItem(Watch watch, string filePath)
        {
            if (watch.Extentions.Contains(Path.GetExtension(filePath).Replace(".",string.Empty)))
            {
                logger.LogInformation($"WATCHER=> Queuing: {filePath}");
                _QueueService.Add(new MediaItem(watch, filePath, Path.GetFileName(filePath)));
            }
            else logger.LogDebug($"WATCHER=> Skipping: {filePath}");
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
            foreach (var dir in Directory.GetDirectories(scanPath).OrderBy(x => x).ToArray())
            {
                ScanDir(watch, dir);
            }
            foreach (var file in Directory.GetFiles(scanPath).OrderBy(x => x).ToArray())
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
            watcher.Deleted += (sender, e) => Watcher_FileDeleted(sender, e);
            return watcher;
        }
        public void ToggleWatchers(bool? state = null)
        {
            if (Watchers.Count == 0)
            {
                //logger.LogWarning("No watchers detected, now terminating. Add watchers to the service before starting it.");
                throw new Exception("No watchers detected, now terminating. Add watchers to the service before starting it.");
                //HostApp.StopApplication();
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
        private void Watcher_FileDeleted(object _, FileSystemEventArgs e)
        {
            logger.LogInformation($"WATCHER=> File deleted: {e.FullPath}");
            _QueueService.Remove(e.FullPath);
        }
        private void Watcher_FileCreated(object _, FileSystemEventArgs e, Watch instance)
        {
            logger.LogDebug($"WATCHER=> File Created: { e.FullPath}");
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
            Watching = ReadConfd(ConfPath) ?? new List<Watch>();
            logger.LogInformation($"WATCHER=> Loaded {Watching.Count} watches.");
            foreach (var instance in Watching)
            {
                logger.LogDebug($"WATCHER=> Watching: {instance.Source}");
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
        }
        public static List<Watch>ReadConfd(string fPath)
        {
            List<Watch> tempWatchers = new List<Watch>();
            FileIniDataParser parser = new FileIniDataParser();
            var x = parser.ReadFile(fPath);
            foreach(var section in x.Sections)
            {
                foreach(var key in section.Keys)
                {
                    if (key.Value == null && (key.KeyName != "origin" || key.KeyName != "isShow")) throw new Exception($"Missing config parameter for: {section}:{key}");
                }
                
                var tWatch = new Watch(section.Keys["source"], section.Keys["destination"], section.Keys["origin"], section.Keys["profilePath"], section.Keys["extensions"]?.Split(",").ToList(), Convert.ToBoolean(section.Keys["isShow"]));
                if (!Directory.Exists(tWatch.Source)) throw new Exception($"Config references a directory which does not exist: {tWatch.Source}");
                if (!Directory.Exists(tWatch.Destination)) throw new Exception($"Config references a directory which does not exist: {tWatch.Destination}");
                if (!string.IsNullOrEmpty(tWatch.Origin) && !Directory.Exists(tWatch.Origin)) throw new Exception($"Config file references a directory which does not exist: {tWatch.Origin}");
                if (!File.Exists(tWatch.ProfilePath)) throw new Exception($"Config references a file which does not exist: {tWatch.ProfilePath}");
                tempWatchers.Add(tWatch);
            }
            return tempWatchers;
        }
    }
}