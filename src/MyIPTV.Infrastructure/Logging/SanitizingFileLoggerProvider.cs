using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyIPTV.Core.Abstractions;

namespace MyIPTV.Infrastructure.Logging;

[ProviderAlias("MyIPTVFile")]
public sealed class SanitizingFileLoggerProvider : ILoggerProvider
{
    private readonly IApplicationPaths _paths;
    private readonly Lock _gate = new();
    private StreamWriter? _writer;
    private DateOnly _writerDate;
    private bool _disposed;

    public SanitizingFileLoggerProvider(IApplicationPaths paths) => _paths = paths;

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private void Write(LogLevel level, EventId eventId, string category, string message, Exception? exception)
    {
        if (_disposed) return;
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (_writer is null || today != _writerDate)
                {
                    _writer?.Dispose();
                    _paths.EnsureDirectoriesExist();
                    string path = Path.Combine(_paths.LogsDirectory, $"myiptv-{today:yyyyMMdd}.log");
                    _writer = new StreamWriter(path, append: true, new UTF8Encoding(false)) { AutoFlush = true };
                    _writerDate = today;
                }

                string record = JsonSerializer.Serialize(new
                {
                    timestampUtc = DateTimeOffset.UtcNow,
                    level = level.ToString(),
                    eventId = eventId.Id,
                    category = LogSanitizer.Sanitize(category),
                    message = LogSanitizer.Sanitize(message),
                    exceptionType = exception?.GetType().FullName,
                    exception = exception is null ? null : LogSanitizer.Sanitize(exception.ToString()),
                });
                _writer.WriteLine(record);
            }
        }
        catch (Exception writeFailure) when (writeFailure is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Debug.WriteLine($"MyIPTV file logging unavailable: {writeFailure.GetType().Name}");
        }
    }

    private sealed class FileLogger(SanitizingFileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            ArgumentNullException.ThrowIfNull(formatter);
            provider.Write(logLevel, eventId, category, formatter(state, exception), exception);
        }
    }
}
