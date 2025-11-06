using System;
using Microsoft.Extensions.Logging;
using MicrosoftILogger = Microsoft.Extensions.Logging.ILogger;

namespace SonicHybridUltimate.Core
{
    public class LoggerProvider : ILoggerProvider
    {
        private readonly Action<string> _logAction;

        public LoggerProvider(Action<string> logAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        }

        public MicrosoftILogger CreateLogger(string categoryName)
        {
            return new CustomLogger(categoryName, _logAction);
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        private class CustomLogger : MicrosoftILogger, ILogger
        {
            private readonly string _categoryName;
            private readonly Action<string> _logAction;

            public CustomLogger(string categoryName, Action<string> logAction)
            {
                _categoryName = categoryName;
                _logAction = logAction;
            }

            public IDisposable BeginScope<TState>(TState state) => null!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _logAction($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
            }

            // Implement custom ILogger interface
            public void Log(string message)
            {
                _logAction(message);
            }

            public void LogError(string message)
            {
                _logAction($"[ERROR] {message}");
            }

            public void LogWarning(string message)
            {
                _logAction($"[WARNING] {message}");
            }
        }
    }
}
