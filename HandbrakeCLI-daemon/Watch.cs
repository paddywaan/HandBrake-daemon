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
        public Watch(string source, string destination, bool postDeletion, string profile)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.PostDeletion = postDeletion;
            this.ProfilePath = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public string Source { private set; get; }
        public string Destination { private set; get; }
        public bool PostDeletion { private set; get; }
        public string ProfilePath { private set; get; }
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
        public void AddWatch(string source, string destination, bool postDeletion, string profile);
        public void RemoveWatch(Watch watch);
    }

    public class WatcherService : IWatcherService
    {
        private readonly LoggingService logger;
        private List<Watch> Watching = new List<Watch>();
        private readonly List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        private IQueueService _QueueService;
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

        private void ScanWatchDirs()
        {
            foreach(var watch in Watching)
            {
                foreach(var file in Directory.GetFiles(watch.Source))
                {
                    _QueueService.Add(new HBQueueItem(watch, false, file, file.Replace(watch.Source, String.Empty)));
                }
            }
        }

        FileSystemWatcher CreateWatcher(Watch instance)
        {
            FileSystemWatcher watcher = new FileSystemWatcher(instance.Source)
            {
                Path = instance.Source
            };
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

        public void AddWatch(string source, string destination, bool postDeletion, string profile)
        {
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            else if (!Directory.Exists(destination)) throw new DirectoryNotFoundException(destination);
            else if (!File.Exists(profile)) throw new FileNotFoundException(profile);
            else Watching.Add(new Watch(source, destination, postDeletion, profile));
            Serialize();
        }
        public void RemoveWatch(Watch watch)
        {
            if (Watching.Contains(watch)) Watching.Remove(watch);
        }

        private void Watcher_FileDeleted(object sender, FileSystemEventArgs e, Watch instance)
        {
            logger.Log($"Deleted: {e.FullPath}", LogSeverity.Info);
            _QueueService.Remove(instance, e.FullPath);
        }

        private void Watcher_FileCreated(object sender, FileSystemEventArgs e, Watch instance)
        {
            logger.Log($"Created: {e.FullPath}", LogSeverity.Info);
            _QueueService.Add(new HBQueueItem(instance, false, e.FullPath, e.Name));
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
