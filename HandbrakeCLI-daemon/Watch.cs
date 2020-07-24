using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace HandbrakeCLI_daemon
{
    public class Watch : IComparable
    {
        public Watch(string source, string destination, bool postDeletion)
        {
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.Destination = destination ?? throw new ArgumentNullException(nameof(destination));
            this.PostDeletion = postDeletion;
        }

        public string Source { private set; get; }
        public string Destination { private set; get; }
        public bool PostDeletion { private set; get; }

        public int CompareTo(object obj)
        {
            var watch = obj as Watch;
           return (watch.Source.CompareTo(this.Source) == 0) ? watch.Destination.CompareTo(this.Destination) : watch.Source.CompareTo(this.Source);
        }
    }

    public interface IWatcherService
    {
        public void ToggleWatchers(bool? state = null);
        public void AddWatch(string source, string destination, bool postDeletion);
        public void RemoveWatch(Watch watch);
    }

    public class WatcherService : IWatcherService
    {
        private LoggingService LoggingService;
        private List<Watch> Watching = new List<Watch>();
        private readonly List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        private readonly string WatchPath;

        public WatcherService(string path, LoggingService loggingService)
        {
            LoggingService = loggingService;
            WatchPath = path;
            if (!Directory.Exists(Daemon.ConfDir)) Directory.CreateDirectory(Daemon.ConfDir);
            if (!File.Exists(WatchPath)) File.Create(WatchPath).Dispose();
            LoadWatchlist();
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

        public void AddWatch(string source, string destination, bool postDeletion)
        {
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            else if (!Directory.Exists(destination)) throw new DirectoryNotFoundException(destination);
            else Watching.Add(new Watch(source, destination, postDeletion));
            Serialize();
        }
        public void RemoveWatch(Watch watch)
        {
            if (Watching.Contains(watch)) Watching.Remove(watch);
        }

        private void Watcher_FileDeleted(object sender, FileSystemEventArgs e, Watch instance)
        {
            LoggingService.Log($"Deleted: {e.FullPath}", LogSeverity.Info);
            //throw new NotImplementedException();
        }

        private void Watcher_FileCreated(object sender, FileSystemEventArgs e, Watch instance)
        {
            LoggingService.Log($"Created: {e.FullPath}", LogSeverity.Info);
            //throw new NotImplementedException();
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
