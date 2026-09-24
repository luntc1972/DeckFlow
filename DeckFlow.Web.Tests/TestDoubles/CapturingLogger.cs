using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Tests.TestDoubles;

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<IReadOnlyList<KeyValuePair<string, object?>>> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> values)
        {
            Entries.Add(values.ToArray());
        }
    }
}
