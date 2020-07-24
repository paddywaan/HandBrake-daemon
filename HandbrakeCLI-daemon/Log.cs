using System;
using System.Collections.Generic;
using System.Text;

namespace HandbrakeCLI_daemon
{
    public enum LogSeverity
    {
        Critical,
        Error,
        Warning,
        Info,
        Verbose
    }
    //class LogMessage
    //{
    //    string Message;
    //    LogSeverity Severity;
    //    Exception _Exception;
    //}
    public class LoggingService
    {
        public LoggingService()
        {

        }
        public static void Log(string message, LogSeverity severity, Exception ex = null)
        {
            Console.WriteLine(message);
        }
    }

}
