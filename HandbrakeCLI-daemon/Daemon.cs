using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace HandbrakeCLI_daemon
{
    class Daemon
    {
        /// <summary>
        /// reccursive filestructure
        /// dynamic subtitles (folder)?
        /// temp dir output
        /// </summary>

        public static char Slash = Path.DirectorySeparatorChar;

        public static void Main(string[] args)
        {
            //CreateHostBuilder(args).RunConsoleAsync();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .UseWindowsService()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<QueueService>();
                    services.AddSingleton<IHostedService, WatcherService>();
                }).ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });
    }
}
