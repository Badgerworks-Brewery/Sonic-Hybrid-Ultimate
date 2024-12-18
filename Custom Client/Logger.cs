using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace SonicHybridUltimate.CustomClient
{
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error
    }

    public interface ILogger
    {
        void Log(LogLevel level, string message);
        void LogError(string message, Exception? exception = null);
    }

    public class Logger : ILogger
    {
        private readonly string _name;
        private readonly Action<string> _logAction;
        private readonly string _logFile;
        private static readonly object _lock = new object();

        public Logger(string name, Action<string> logAction)
        {
            _name = name;
            _logAction = logAction;
            _logFile = "hybrid_client.log";

            // Create or clear log file
            File.WriteAllText(_logFile, string.Empty);
        }

        public void Log(LogLevel level, string message)
        {
            var logMessage = FormatMessage(level, message);
            WriteLog(logMessage);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(FormatMessage(LogLevel.Error, message));
            
            if (exception != null)
            {
                sb.AppendLine($"Exception: {exception.GetType().Name}");
                sb.AppendLine($"Message: {exception.Message}");
                sb.AppendLine($"Stack Trace: {exception.StackTrace}");
            }

            WriteLog(sb.ToString());
        }

        private string FormatMessage(LogLevel level, string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{_name}] {message}";
        }

        private void WriteLog(string message)
        {
            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFile, message + Environment.NewLine);
                    _logAction(message);
                    Trace.WriteLine(message);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"Failed to write to log: {ex.Message}");
                }
            }
        }
    }

    public class LoggerProvider
    {
        private readonly Action<string> _logAction;

        public LoggerProvider(Action<string> logAction)
        {
            _logAction = logAction;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new Logger(categoryName, _logAction);
        }
    }
}
