using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Tests;

/// <summary>
/// In-memory <see cref="ILogger{TCategoryName}"/> test double that records every logged entry
/// (level + formatted message) so tests can assert a specific warning fired — or did not.
/// </summary>
internal sealed class FakeLogger<T> : ILogger<T>
{
    /// <summary>Every log entry captured, in call order.</summary>
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Entries.Add((logLevel, formatter(state, exception)));
    }
}
