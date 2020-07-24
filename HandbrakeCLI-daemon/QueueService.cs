using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HandbrakeCLI_daemon
{
    public class HBQueueItem
    {
        public Watch WatchInstance;
        private bool Processing;
        private string fPath;

        public HBQueueItem(Watch watchInstance, bool processing, string fname)
        {
            WatchInstance = watchInstance;
            Processing = processing;
            this.fPath = fname;
        }

        public string FilePath
        {
            get { return fPath; }
        }

    }

    /// <summary>
    /// Scan watching dirs
    /// </summary>
    public class QueueService : IQueueService
    {
        private LoggingService logger;
        private Queue<HBQueueItem> HBQueue;
        public QueueService(LoggingService loggingService)
        {
            logger = loggingService;
            HBQueue = new Queue<HBQueueItem>();
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
    }

    public interface IQueueService
    {
        public void Add(HBQueueItem item);
        public void Remove(Watch watch, string fname);
    }
}
