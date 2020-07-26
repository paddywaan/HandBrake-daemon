using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace HandbrakeCLI_daemon
{
    public class Watch : IComparable
    {
        public Watch(string source, string destination, bool postDeletion, string profilePath, List<string> extentions)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.PostDeletion = postDeletion;
            this.ProfilePath = profilePath ?? throw new ArgumentNullException(nameof(profilePath));
            this.Extentions = extentions;
        }

        public string Source { private set; get; }
        public string Destination { private set; get; }
        public bool PostDeletion { private set; get; }
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
        public void AddWatch(string source, string destination, bool postDeletion, string profile, List<string> ext = null);
        public void RemoveWatch(Watch watch);
    }

    public class WatcherService : IWatcherService
    {
        private readonly LoggingService logger;
        private List<Watch> Watching = new List<Watch>();
        private readonly List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        private readonly IQueueService _QueueService;
        private readonly string WatchPath;

        public WatcherService(string path, LoggingService loggingService, IQueueService queueService)
        {
            logger = loggingService;
            _QueueService = queueService;
            WatchPath = path;
            if (!Directory.Exists(Daemon.ConfDir)) Directory.CreateDirectory(Daemon.ConfDir);
            if (!File.Exists(WatchPath)) File.Create(WatchPath).Dispose();
            LoadWatchlist();
            ScanWatchDirs();
        }

        private void AddQueueItem(Watch watch, string filePath)
        {

            if (watch.Extentions.Contains(Path.GetExtension(filePath)))
                _QueueService.Add(new HBQueueItem(watch, filePath, Path.GetFileName(filePath)));
            logger.Log($"SCANNER=> Media found: {filePath}", LogSeverity.Info);
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
            foreach(var watcher in Watchers)
            {
                 watcher.EnableRaisingEvents = state ?? !watcher.EnableRaisingEvents;
            }
        }

        public void AddWatch(string source, string destination, bool postDeletion, string profile, List<string> ext = null)
        {
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            else if (!Directory.Exists(destination)) throw new DirectoryNotFoundException(destination);
            else if (!File.Exists(profile)) throw new FileNotFoundException(profile);
            else Watching.Add(new Watch(source, destination, postDeletion, profile, ext ?? new List<string> { ".mp4",".mkv","avi"}));
            Serialize();
        }
        public void RemoveWatch(Watch watch)
        {
            if (Watching.Contains(watch)) Watching.Remove(watch);
        }

        private void Watcher_FileDeleted(object sender, FileSystemEventArgs e, Watch instance)
        {
            logger.Log($"WATCHER=> File deleted: {e.FullPath}", LogSeverity.Info);
            _QueueService.Remove(instance, e.FullPath);
        }

        private void Watcher_FileCreated(object sender, FileSystemEventArgs e, Watch instance)
        {
            logger.Log($"WATCHER=> File created: {e.FullPath}", LogSeverity.Info);
            /*if (instance.Extentions.Contains(Path.GetExtension(e.FullPath)))
                _QueueService.Add(new HBQueueItem(instance, false, e.FullPath, e.Name));*/
            AddQueueItem(instance, e.FullPath);
        }

        private void Serialize()
        {
            var data = JsonConvert.SerializeObject(Watching);
            using StreamWriter sw = new StreamWriter(WatchPath);
            sw.WriteLine(data);
        }
        private List<Watch> Deserialize()
        {
            using StreamReader sr = new StreamReader(WatchPath);
            var data = sr.ReadToEnd();
            return JsonConvert.DeserializeObject<List<Watch>>(data);
        }
        private void LoadWatchlist()
        {
            Watching = Deserialize() ?? new List<Watch>();
            foreach (var instance in Watching)
            {
                Watchers.Add(CreateWatcher(instance));
            }
        }
    }
}
