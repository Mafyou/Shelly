namespace ShellyBLE;

/// <summary>Journalise dans logcat sous le tag « ShellyBLE » (adb logcat -s ShellyBLE).</summary>
public sealed class LogcatLoggerProvider : ILoggerProvider
{
    const string Tag = "ShellyBLE";

    public ILogger CreateLogger(string categoryName) => new LogcatLogger(categoryName);

    public void Dispose()
    {
    }

    sealed class LogcatLogger(string category) : ILogger
    {
        readonly string shortCategory = category[(category.LastIndexOf('.') + 1)..];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = exception is null
                ? $"{shortCategory}: {formatter(state, exception)}"
                : $"{shortCategory}: {formatter(state, exception)}{Environment.NewLine}{exception}";

            switch (logLevel)
            {
                case LogLevel.Trace or LogLevel.Debug: global::Android.Util.Log.Debug(Tag, message); break;
                case LogLevel.Information: global::Android.Util.Log.Info(Tag, message); break;
                case LogLevel.Warning: global::Android.Util.Log.Warn(Tag, message); break;
                default: global::Android.Util.Log.Error(Tag, message); break;
            }
        }
    }
}
