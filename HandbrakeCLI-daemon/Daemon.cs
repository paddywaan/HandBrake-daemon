using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;

namespace HandbrakeCLI_daemon
{
    class Daemon
    {
        /// <summary>
        /// reccursive filestructure
        /// dynamic subtitles (folder)?
        /// temp dir output
        /// </summary>

        private static IServiceProvider Services;
        private static IWatcherService _WatcherService;
        private static IQueueService _QueueService;
        private static LoggingService _LoggingService;
        private static readonly string APPDATA = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public static char Slash = Path.DirectorySeparatorChar;
        private static readonly string AppName = "HandbrakeCLI-daemon";
        public static string ConfDir = APPDATA + Slash + AppName + Slash;
        private static readonly string Watchpath = ConfDir + "WatchList.json";
        static void Main(string[] args)
        {
            if (args == null) args = new string[] {"help"};
            _LoggingService = new LoggingService();
            _QueueService = new QueueService(_LoggingService);
            _WatcherService = new WatcherService(Watchpath, _LoggingService, _QueueService);
            
            switch (args[0])
            {
                case "add":
                    _WatcherService.AddWatch(args[1], args[2], bool.Parse(args[3]), args[4]);
                    break;
                case "remove":
                    break;
                case "start":
                    new Daemon().MainAsync().GetAwaiter().GetResult();
                        break;
                default:
                    if (new List<string>{ "help", "--help", "/h", "-h"}.Contains(args[0]))
                    {

                    }
                    break;
            }
            
            ConfigureServices();
        }
        public async Task MainAsync()
        {
            _WatcherService.ToggleWatchers();
            _LoggingService.Log("Watchers have been started.", LogSeverity.Info);
            await Task.Delay(-1);
        }
        private static void ConfigureServices()
        {
            Services = new ServiceCollection()
                .AddSingleton(_LoggingService)
                .AddSingleton(_QueueService)
                .AddSingleton(_WatcherService)
                .BuildServiceProvider();
        }
        public static IServiceProvider GetServices()
        {
            return Services;
        }
    }
}
