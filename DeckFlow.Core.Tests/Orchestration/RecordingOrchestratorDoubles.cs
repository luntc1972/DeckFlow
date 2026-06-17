using Microsoft.Extensions.Logging;

namespace DeckFlow.Core.Tests;

internal sealed class RecordingOrchestratorProgress : DeckFlow.Core.Orchestration.IOrchestratorProgress
{
    public List<string> Messages { get; } = [];

    public void Report(string message)
    {
        Messages.Add(message);
    }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<RecordedLogEntry> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel)
        => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add(new RecordedLogEntry(logLevel, formatter(state, exception), exception));
    }

    internal sealed record RecordedLogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
