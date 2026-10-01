using DeckFlow.Web.Services.CutLab;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CommanderStapleProviderTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public CommanderStapleProviderTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"commander-staples-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "latest.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void GetStapleCardNames_KnownCommander_ReturnsNormalizedStaples()
    {
        WriteFile("""{ "commanders": [{ "name": "Atraxa, Praetors' Voice", "staples": ["Sol Ring", "Arcane Signet"] }] }""");

        IReadOnlySet<string> staples = CreateProvider().GetStapleCardNames(["atraxa praetors voice"]);

        Assert.Equal(["arcane signet", "sol ring"], staples.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void GetStapleCardNames_TwoCommanders_ReturnsEmpty()
    {
        WriteFile("""{ "commanders": [{ "name": "Atraxa", "staples": ["Sol Ring"] }] }""");

        Assert.Empty(CreateProvider().GetStapleCardNames(["Atraxa", "Thrasios"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetStapleCardNames_MissingOrInvalidFile_ReturnsEmptyWithoutThrowing(bool writeInvalidJson)
    {
        if (writeInvalidJson)
        {
            WriteFile("{ invalid json");
        }

        Assert.Empty(CreateProvider().GetStapleCardNames(["Atraxa"]));
    }

    [Fact]
    public void GetStapleCardNames_StructurallyInvalidEntries_SkipsBadEntriesAndLoadsValidCommander()
    {
        WriteFile("""
            {
              "commanders": [
                null,
                { "name": null, "staples": ["Bad"] },
                { "name": "No Staples", "staples": null },
                { "name": "Valid Commander", "staples": [null, "Sol Ring"] }
              ]
            }
            """);

        IReadOnlySet<string> staples = CreateProvider().GetStapleCardNames(["valid commander"]);

        Assert.Equal(["sol ring"], staples);
        Assert.Empty(CreateProvider().GetStapleCardNames(["No Staples"]));
    }

    [Fact]
    public void GetStapleCardNames_NullCommanders_ReturnsEmptyWithoutThrowing()
    {
        WriteFile("""{ "commanders": null }""");

        Assert.Empty(CreateProvider().GetStapleCardNames(["Atraxa"]));
    }

    [Fact]
    public void GetStapleCardNames_UnexpectedLoadFailure_FailsOpenAndLogsOnce()
    {
        var logger = new CountingLogger();
        CommanderStapleProvider provider = new(_ => throw new InvalidOperationException("boom"), _path, logger);

        Assert.Empty(provider.GetStapleCardNames(["Atraxa"]));
        Assert.Empty(provider.GetStapleCardNames(["Atraxa"]));
        Assert.Equal(1, logger.WarningCount);
    }

    [Fact]
    public void GetStapleCardNames_ReturnsReadOnlySets()
    {
        WriteFile("""{ "commanders": [{ "name": "Atraxa", "staples": ["Sol Ring"] }] }""");

        Assert.True(((ICollection<string>)CreateProvider().GetStapleCardNames(["Atraxa"])).IsReadOnly);
        Assert.True(((ICollection<string>)CreateProvider().GetStapleCardNames(["Unknown"])).IsReadOnly);
        Assert.True(((ICollection<string>)NullCommanderStapleProvider.Instance.GetStapleCardNames(["Atraxa"])).IsReadOnly);
    }

    private sealed class CountingLogger : ILogger
    {
        public int WarningCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                WarningCount++;
            }
        }
    }

    private CommanderStapleProvider CreateProvider() => new(_path);

    private void WriteFile(string json) => File.WriteAllText(_path, json);
}
