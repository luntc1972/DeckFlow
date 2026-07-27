using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="RoleFloorProvenance"/>.
/// </summary>
public sealed class RoleFloorProvenanceTests
{
    [Fact]
    public void ResolveConnectionString_FlagOnly_ReturnsFlagValue()
    {
        string? result = RoleFloorProvenance.ResolveConnectionString("Host=flag", null);

        Assert.Equal("Host=flag", result);
    }

    [Fact]
    public void ResolveConnectionString_EnvironmentOnly_ReturnsEnvironmentValue()
    {
        string? result = RoleFloorProvenance.ResolveConnectionString(null, "Host=environment");

        Assert.Equal("Host=environment", result);
    }

    [Fact]
    public void ResolveConnectionString_BothSet_PrefersFlagValue()
    {
        string? result = RoleFloorProvenance.ResolveConnectionString("Host=flag", "Host=environment");

        Assert.Equal("Host=flag", result);
    }

    [Fact]
    public void ResolveConnectionString_NeitherSet_ReturnsNull()
    {
        string? result = RoleFloorProvenance.ResolveConnectionString(null, null);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveConnectionString_WhitespaceFlagWithEnvironment_ReturnsEnvironmentValue(string flagValue)
    {
        string? result = RoleFloorProvenance.ResolveConnectionString(flagValue, "Host=environment");

        Assert.Equal("Host=environment", result);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("", "   ")]
    [InlineData("   ", "   ")]
    public void ResolveConnectionString_BothEmptyOrWhitespace_ReturnsNull(string? flagValue, string? environmentValue)
    {
        string? result = RoleFloorProvenance.ResolveConnectionString(flagValue, environmentValue);

        Assert.Null(result);
    }

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
    public void DescribeDatabaseHost_ConnectionStringWithPwdAlias_DoesNotLeakCredentials()
    {
        const string password = "AliasSecret!2026";
        const string username = "alias_user";
        string result = RoleFloorProvenance.DescribeDatabaseHost(
            $"Host=db.example.com;Port=5432;Database=deckflow;Username={username};Pwd={password};SSL Mode=Require");

        Assert.Equal("db.example.com:5432/deckflow", result);
        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(username, result, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeDatabaseHost_UnixSocketHost_ReturnsExpectedRenderingWithoutLeakingCredentials()
    {
        const string password = "SocketSecret!2026";
        const string username = "socket_user";
        string result = RoleFloorProvenance.DescribeDatabaseHost(
            $"Host=/var/run/postgresql;Port=5432;Database=cutlab;Username={username};Password={password}");

        Assert.Equal("/var/run/postgresql:5432/cutlab", result);
        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(username, result, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeDatabaseHost_QuotedPasswordWithSemicolon_DoesNotLeakCredentials()
    {
        const string password = "\"semi;colon secret\"";
        const string username = "quoted_user";
        string result = RoleFloorProvenance.DescribeDatabaseHost(
            $"Host=db.example.com;Port=5432;Database=deckflow;Username={username};Password={password};SSL Mode=Require");

        Assert.Equal("db.example.com:5432/deckflow", result);
        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain("semi;colon", result, StringComparison.Ordinal);
        Assert.DoesNotContain(username, result, StringComparison.Ordinal);
    }

    [Fact]
    public void DescribeDatabaseHost_TrailingUnknownKey_ReturnsUnavailableWithoutLeakingCredentials()
    {
        const string password = "UnknownKeySecret!2026";
        const string username = "unknown_key_user";
        string result = RoleFloorProvenance.DescribeDatabaseHost(
            $"Host=db.example.com;Port=5432;Database=deckflow;Username={username};Password={password};ExtraKey=still-here");

        Assert.Equal("unavailable", result);
        Assert.DoesNotContain(password, result, StringComparison.Ordinal);
        Assert.DoesNotContain(username, result, StringComparison.Ordinal);
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
    public void DescribeDatabaseHost_WhitespaceOnlyInput_ReturnsUnavailable()
    {
        // Why: whitespace-only input carries no credential to leak, so a DoesNotContain assertion
        // here would be tautological. The credential-absence guarantee is covered by the cases that
        // actually feed a secret in; this case pins only the degraded-value contract.
        string result = RoleFloorProvenance.DescribeDatabaseHost("   ");

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
    public void BuildProvenanceWarnings_HostUnavailableWithoutDeckCounts_ReturnsSingleNoCorpusWarning()
    {
        IReadOnlyList<string> result = RoleFloorProvenance.BuildProvenanceWarnings("unavailable", "abc123", 0, 0);

        string warning = Assert.Single(result);
        Assert.Contains("did not reach any deck rows", warning, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reached the corpus", warning, StringComparison.OrdinalIgnoreCase);
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
