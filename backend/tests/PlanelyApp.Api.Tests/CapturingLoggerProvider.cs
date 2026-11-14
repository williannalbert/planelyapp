using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PlanelyApp.Api.Tests;

/// <summary>One line written through <see cref="ILogger"/>: what it said, its structured values and the scopes open at that moment.</summary>
internal sealed record CapturedLog(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyDictionary<string, object?> Scope,
    Exception? Exception);

/// <summary>Keeps every log line in memory, so a test can read what the Api wrote and under which scope.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyList<CapturedLog> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => provider._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>();

            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var (key, value) in values)
                {
                    properties[key] = value;
                }
            }

            var scope = new Dictionary<string, object?>();

            provider._scopes.ForEachScope(
                (item, target) =>
                {
                    if (item is IEnumerable<KeyValuePair<string, object>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            target[key] = value;
                        }
                    }
                },
                scope);

            provider._entries.Enqueue(
                new CapturedLog(category, logLevel, formatter(state, exception), properties, scope, exception));
        }
    }
}