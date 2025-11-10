using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MicrosoftILogger = Microsoft.Extensions.Logging.ILogger;

namespace SonicHybridUltimate.Core
{
    /// <summary>
    /// Logger provider that can be configured with a log action after creation.
    /// This allows it to be registered in DI before the UI is created.
    /// </summary>
    public class UILoggerProvider : ILoggerProvider
    {
        private Action<string>? _logAction;
        private readonly List<UILogger> _loggers = new List<UILogger>();
        private readonly object _lock = new object();

        public void SetLogAction(Action<string> logAction)
        {
            lock (_lock)
            {
                _logAction = logAction;
                // Update all existing loggers
                foreach (var logger in _loggers)
                {
                    logger.SetLogAction(logAction);
                }
            }
        }

        public MicrosoftILogger CreateLogger(string categoryName)
        {
            lock (_lock)
            {
                var logger = new UILogger(categoryName, _logAction);
                _loggers.Add(logger);
                return logger;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _loggers.Clear();
            }
        }

        private class UILogger : MicrosoftILogger
        {
            private readonly string _categoryName;
            private Action<string>? _logAction;

            public UILogger(string categoryName, Action<string>? logAction)
            {
                _categoryName = categoryName;
                _logAction = logAction;
            }

            public void SetLogAction(Action<string> logAction)
            {
                _logAction = logAction;
            }

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = $"{logLevel.ToString().ToLower()}: {_categoryName}[{eventId.Id}]{Environment.NewLine}      {formatter(state, exception)}";
                if (exception != null)
                {
                    message += $"{Environment.NewLine}      {exception}";
                }
                
                _logAction?.Invoke(message);
            }

            private class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new NullScope();
                public void Dispose() { }
            }
        }
    }
}
