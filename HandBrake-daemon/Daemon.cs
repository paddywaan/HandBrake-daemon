using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace HandBrake_daemon
{

    class Daemon
    {

        private const string SERVICENAME = "HandBrake-daemon";
        public static void Main(string[] args)
        {
            
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
                    System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<QueueService>();
                    services.AddSingleton<IHostedService, WatcherService>();
                    //services.AddSerilogServices();
                }).ConfigureLogging((hostingContext, logging) =>
                {
                    if (!System.Diagnostics.EventLog.SourceExists(SERVICENAME))
                    {
                        System.Diagnostics.EventLog.CreateEventSource(
                            SERVICENAME, SERVICENAME + ".log");
                    }
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                    logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings()
                    {
                        SourceName = SERVICENAME,
                        LogName = SERVICENAME+".log",
                    }).SetMinimumLevel(LogLevel.Debug);
                })
            ;
    }
    //public static class ServiceExtention
    //{
    //    public static IServiceCollection AddSerilogServices(this IServiceCollection services)
    //    {
    //        return services.AddSerilogServices(
    //            new LoggerConfiguration()
    //                .MinimumLevel.Verbose()
    //                 .WriteTo. File("log.txt")
    //                //.WriteTo.File(new CompactJsonFormatter(), "log.txt")
    //                .WriteTo.Console());
    //    }
    //}
}
