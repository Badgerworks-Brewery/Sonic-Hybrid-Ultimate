using System;
using System.IO;

namespace SonicHybridUltimate.Core
{
    public class FileLogger : ILogger
    {
        private readonly string logPath;
        private readonly object lockObj = new object();

        public FileLogger(string logPath)
        {
            this.logPath = logPath;
        }

        public void Log(string message)
        {
            WriteToLog("INFO", message);
        }

        public void LogError(string message)
        {
            WriteToLog("ERROR", message);
        }

        public void LogWarning(string message)
        {
            WriteToLog("WARNING", message);
        }

        private void WriteToLog(string level, string message)
        {
            lock (lockObj)
            {
                try
                {
                    string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
                    File.AppendAllText(logPath, logMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // If we can't write to the log file, write to console as fallback
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                    Console.WriteLine($"Original message: {message}");
                }
            }
        }
    }
}
