using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;

namespace HandBrake_daemon
{
    class Daemon
    {
        private const string SERVICENAME = "HandBrake-daemon";
        private static bool debug = false;
        public static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
            } catch (Exception ex)
            {
                Console.WriteLine(debug ? $"{ex}" : $"{ex.Message}");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .UseWindowsService()
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    if (hostingContext.Configuration.GetValue<string>("Logging:LogLevel:Default") == "Debug") debug = true;
                    logging.AddConsole();

                    //Add logging via EventLog only for Windows platforms
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        if (!System.Diagnostics.EventLog.SourceExists(SERVICENAME))
                        {
                            System.Diagnostics.EventLog.CreateEventSource(
                                SERVICENAME, SERVICENAME + ".log");
                        }
                        logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings()
                        {
                            SourceName = SERVICENAME,
                            LogName = SERVICENAME + ".log",
                        });
                    }
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    System.IO.Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<QueueService>();
                    services.AddSingleton<IHostedService, WatcherService>();
                });
    }
}
