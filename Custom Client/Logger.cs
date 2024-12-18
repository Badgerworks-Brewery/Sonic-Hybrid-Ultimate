using System.Text;

namespace SonicHybridUltimate.CustomClient
{
    public static class Logger
    {
        private static readonly string LogFile = "hybrid_client.log";
        private static bool enableDebugLogging = false;
        private static readonly object lockObj = new object();

        public static void Initialize(bool enableDebug)
        {
            enableDebugLogging = enableDebug;
            // Clear log file on startup
            if (File.Exists(LogFile))
                File.WriteAllText(LogFile, string.Empty);
            
            Log("Logger initialized");
        }

        public static void Log(string message, bool isDebug = false)
        {
            if (isDebug && !enableDebugLogging)
                return;

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {(isDebug ? "[DEBUG] " : "")}{message}";
            
            lock (lockObj)
            {
                try
                {
                    File.AppendAllText(LogFile, logMessage + Environment.NewLine);
                    Debug.WriteLine(logMessage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ERROR] {message}");
            
            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.GetType().Name}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace: {ex.StackTrace}");
            }

            Log(sb.ToString());
        }

        public static string[] GetRecentLogs(int lines = 100)
        {
            try
            {
                if (!File.Exists(LogFile))
                    return Array.Empty<string>();

                return File.ReadAllLines(LogFile)
                    .Reverse()
                    .Take(lines)
                    .Reverse()
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
