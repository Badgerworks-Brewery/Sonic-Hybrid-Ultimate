using System;
using Microsoft.Extensions.Logging;

namespace SonicHybridUltimate.Core
{
    public class LoggerProvider : ILoggerProvider
    {
        private readonly Action<string> _logAction;

        public LoggerProvider(Action<string> logAction)
        {
            _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new CustomLogger(categoryName, _logAction);
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        private class CustomLogger : ILogger
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
        }
    }
}
