#nullable enable
using System;
using Microsoft.Extensions.Logging;

namespace DebugTests
{
    // https://github.com/MarshallMoorman/Extensions.Logging.NUnit/blob/master/NUnitLogger.cs
    internal class NUnitLogger : ILogger
    {
        private readonly Func<string, LogLevel, bool>? _filter;
        private readonly string _name;

        public NUnitLogger()
        {
            _name = nameof(NUnitLogger);
        }

        public NUnitLogger(string name, Func<string, LogLevel, bool> filter)
        {
            _name = string.IsNullOrEmpty(name) ? nameof(NUnitLogger) : name;
            _filter = filter;
        }

        public IDisposable BeginScope<TState>(TState state) => new ScopeHandler();

        public bool IsEnabled(LogLevel logLevel) 
            => RunningInNUnitContext()
               && logLevel != LogLevel.None
               && (_filter is null || _filter(_name, logLevel));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            if (formatter is null)
                throw new ArgumentNullException(nameof(formatter));

            var message = formatter(state, exception);

            if (string.IsNullOrEmpty(message))
                return;

            message = $"[{DateTime.Now:T}][{logLevel}]: {message}";

            if (exception is { }) 
                message += Environment.NewLine + exception;

            WriteMessage(message);
        }

        private static bool RunningInNUnitContext() => NUnit.Framework.TestContext.Progress is { };

        private static void WriteMessage(string message) => NUnit.Framework.TestContext.Progress.WriteLine(message);

        private class ScopeHandler : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
