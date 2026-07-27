using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="RoleFloorProvenance"/>.
/// </summary>
public sealed class RoleFloorProvenanceTests
{
    [Fact]
    public void DescribeDatabaseHost_ConnectionStringWithPassword_DoesNotLeakCredentials()
    {
        const string password = "Sup3rSecret!2026";
        const string username = "deckflow_user";
        string result = RoleFloorProvenance.DescribeDatabaseHost(
            $"Host=db.example.com;Port=5432;Database=deckflow;Username={username};Password={password};SSL Mode=Require");

        Assert.Equal("db.example.com:5432/deckflow", result);
        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(username, result, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", result, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeDatabaseHost_HostPortAndDatabase_ReturnsExpectedRendering()
    {
        string result = RoleFloorProvenance.DescribeDatabaseHost("Host=pg.internal;Port=15432;Database=cutlab");

        Assert.Equal("pg.internal:15432/cutlab", result);
    }

    [Fact]
    public void DescribeDatabaseHost_MalformedString_ReturnsUnavailable()
    {
        string result = RoleFloorProvenance.DescribeDatabaseHost("Host=\u0000bad");

        Assert.Equal("unavailable", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DescribeDatabaseHost_NullOrEmptyInput_ReturnsUnavailable(string? normalizedConnectionString)
    {
        string result = RoleFloorProvenance.DescribeDatabaseHost(normalizedConnectionString);

        Assert.Equal("unavailable", result);
    }

    [Fact]
    public void DescribeDatabaseHost_NoHost_ReturnsUnavailable()
    {
        string result = RoleFloorProvenance.DescribeDatabaseHost("Database=cutlab;Username=deckflow");

        Assert.Equal("unavailable", result);
    }

    [Fact]
    public void FormatCommitSha_ExitZeroAndCleanStatus_ReturnsBareSha()
    {
        string result = RoleFloorProvenance.FormatCommitSha(0, "abc123\n", string.Empty);

        Assert.Equal("abc123", result);
    }

    [Fact]
    public void FormatCommitSha_ExitZeroAndDirtyStatus_AppendsDirtySuffix()
    {
        string result = RoleFloorProvenance.FormatCommitSha(0, "abc123\r\n", " M DeckFlow.CLI/RoleFloorResearchCommandRunner.cs\n");

        Assert.Equal("abc123-dirty", result);
    }

    [Fact]
    public void FormatCommitSha_NonZeroExit_ReturnsUnknown()
    {
        string result = RoleFloorProvenance.FormatCommitSha(1, "abc123", string.Empty);

        Assert.Equal("unknown", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatCommitSha_NullOrWhitespaceStdout_ReturnsUnknown(string? revParseStdout)
    {
        string result = RoleFloorProvenance.FormatCommitSha(0, revParseStdout, string.Empty);

        Assert.Equal("unknown", result);
    }

    [Fact]
    public void BuildProvenanceWarnings_BothFieldsResolved_ReturnsEmptyList()
    {
        IReadOnlyList<string> result = RoleFloorProvenance.BuildProvenanceWarnings("db.example.com", "abc123", 120, 100);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildProvenanceWarnings_HostUnavailableWithDeckCounts_ReportsConnectedContradiction()
    {
        IReadOnlyList<string> result = RoleFloorProvenance.BuildProvenanceWarnings("unavailable", "abc123", 120, 100);

        string warning = Assert.Single(result);
        Assert.Contains("contradiction", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("120 raw decks", warning, StringComparison.Ordinal);
        Assert.Contains("100 deduped decks", warning, StringComparison.Ordinal);
        Assert.Contains("host could not be derived", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildProvenanceWarnings_CommitUnknown_ReportsCodeStateGap()
    {
        IReadOnlyList<string> result = RoleFloorProvenance.BuildProvenanceWarnings("db.example.com", "unknown", 120, 100);

        string warning = Assert.Single(result);
        Assert.Contains("harness revision could not be determined", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("code state", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildProvenanceWarnings_BothDegraded_ReturnsTwoWarnings()
    {
        IReadOnlyList<string> result = RoleFloorProvenance.BuildProvenanceWarnings("unavailable", "unknown", 0, 0);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, warning => warning.Contains("did not reach any deck rows", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, warning => warning.Contains("harness revision could not be determined", StringComparison.OrdinalIgnoreCase));
    }
}
