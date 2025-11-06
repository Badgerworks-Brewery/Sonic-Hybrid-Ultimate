using System;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate
{
    public class LoggerProvider : ILoggerProvider
    {
        private readonly Action<string> _logAction;

        public LoggerProvider(Action<string> logAction)
        {
            _logAction = logAction;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomLogger(categoryName, _logAction);
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    public class CustomLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Action<string> _logAction;

        public CustomLogger(string categoryName, Action<string> logAction)
        {
            _categoryName = categoryName;
            _logAction = logAction;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new NoOpDisposable();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}] {message}";

            if (exception != null)
            {
                logMessage += Environment.NewLine + exception.ToString();
            }

            _logAction(logMessage);
        }
    }

    internal class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }
}
