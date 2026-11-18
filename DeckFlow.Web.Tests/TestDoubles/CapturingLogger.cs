using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Tests.TestDoubles;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public ConcurrentQueue<IReadOnlyList<KeyValuePair<string, object?>>> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            Entries.Enqueue(values.ToArray());
        }
    }
}
