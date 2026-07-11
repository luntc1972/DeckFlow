using Dapper;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using DeckFlow.Studio.Services;
using Npgsql;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Tests for <see cref="ProdContentReader.ReadFlagAsync"/> — the structurally read-only, fail-closed
/// prod <c>feature_flags</c> accessor (90-04 Task 1 / D-04). Covers: the throwing default-interface-
/// method idiom on a non-overriding double (<see cref="FakeProdContentReader"/>, unmodified per the
/// plan), fail-closed behavior on a connection failure (always runs, no Postgres required), and
/// enabled-true / enabled-false / missing-key round trips against a real Postgres (gated behind
/// <c>DECKFLOW_POSTGRES_TESTS=1</c> + a DEDICATED test-only connection string — never the production
/// <c>DECKFLOW_DATABASE_CONNECTION_STRING</c> — so these tests can never accidentally touch prod).
/// </summary>
public sealed class ProdContentReaderTests
{
    // ── Throwing default interface method (non-overriding double) ───────────

    [Fact]
    public async Task ReadFlagAsync_DefaultInterfaceMethod_ThrowsNotSupported_WhenNotOverridden()
    {
        // FakeProdContentReader (DeckFlow.Studio.Tests/TestDoubles/FakeProdContentReader.cs) is
        // UNMODIFIED by this plan — it does not override ReadFlagAsync, so the interface's throwing
        // default body must fire. This is the compile+runtime proof the throwing-DIM idiom holds.
        IProdContentReader doubleWithoutOverride = new FakeProdContentReader();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => doubleWithoutOverride.ReadFlagAsync("Host=localhost;Database=x", "sync.directpush-gitbody"));
    }

    // ── Fail-closed on connection failure (always runs — no live Postgres needed) ───────

    [Fact]
    public async Task ReadFlagAsync_ConnectionFails_ReturnsFalse_FailClosed()
    {
        // Why: a well-formed but unreachable connection string (RFC 5737 TEST-NET-1, a very short
        // timeout) proves ReadFlagAsync catches the connection failure and fails CLOSED (returns
        // false) rather than throwing or defaulting ON — the D-04 contract for a brand-new, riskier
        // flag Studio cannot confirm.
        var reader = new ProdContentReader();

        var result = await reader.ReadFlagAsync(
            "Host=192.0.2.1;Port=5432;Database=x;Username=x;Password=x;Timeout=1;Command Timeout=1",
            "sync.directpush-gitbody",
            CancellationToken.None);

        Assert.False(result);
    }

    // ── Live Postgres round trips (gated — never touches DECKFLOW_DATABASE_CONNECTION_STRING) ──

    [PostgresFact]
    public async Task ReadFlagAsync_EnabledTrueRow_ReturnsTrue()
    {
        var connectionString = RequireTestConnectionString();
        var key = $"studio.test.readflag.{Guid.NewGuid():N}";
        await SeedFlagRowAsync(connectionString, key, enabled: true);

        var reader = new ProdContentReader();
        var result = await reader.ReadFlagAsync(connectionString, key, CancellationToken.None);

        Assert.True(result);
    }

    [PostgresFact]
    public async Task ReadFlagAsync_EnabledFalseRow_ReturnsFalse()
    {
        var connectionString = RequireTestConnectionString();
        var key = $"studio.test.readflag.{Guid.NewGuid():N}";
        await SeedFlagRowAsync(connectionString, key, enabled: false);

        var reader = new ProdContentReader();
        var result = await reader.ReadFlagAsync(connectionString, key, CancellationToken.None);

        Assert.False(result);
    }

    [PostgresFact]
    public async Task ReadFlagAsync_MissingKey_ReturnsFalse()
    {
        var connectionString = RequireTestConnectionString();
        await EnsureFeatureFlagsTableAsync(connectionString);
        var missingKey = $"studio.test.readflag.missing.{Guid.NewGuid():N}";

        var reader = new ProdContentReader();
        var result = await reader.ReadFlagAsync(connectionString, missingKey, CancellationToken.None);

        Assert.False(result);
    }

    // ── ReadAllAsync body_sha256 + seed_managed round trip (Pitfall 2) ──────────

    [PostgresFact]
    public async Task ReadAllAsync_RoundTripsBodySha256AndSeedManaged_WhenPopulated()
    {
        // Pitfall 2: the reconciler's body-hash-mismatch and seed-drift classes are unbuildable
        // until ReadAllAsync actually selects + maps these two columns instead of leaving them null.
        var connectionString = RequireTestConnectionString();
        var videoId = $"prodreader-{Guid.NewGuid():N}";
        var expectedHash = "a1b2c3d4e5f60718293a4b5c6d7e8f90112233445566778899aabbccddeeff";

        var store = new ContentSiteIndexStore(
            new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, NormalizeForStore(connectionString)));
        await store.UpsertContentColumnsOnlyAsync(new ContentSiteIndexRow
        {
            Id = 0,
            YoutubeVideoId = videoId,
            Source = "Prod Reader Round Trip",
            Title = "Round Trip Row",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/prod-reader/{videoId}.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            ApprovalStatus = "approved",
            BodySha256 = expectedHash,
            SeedManaged = true,
        });

        var reader = new ProdContentReader();
        var rows = await reader.ReadAllAsync(connectionString, CancellationToken.None);

        var row = Assert.Single(rows, r => r.YoutubeVideoId == videoId);
        Assert.Equal(expectedHash, row.BodySha256);
        Assert.True(row.SeedManaged);
    }

    // Why: ContentSiteIndexStore's constructor normalizes internally for its own connection pool, but
    // this test opens the store directly against the SAME test connection string ProdContentReader
    // consumes raw — force SslMode.Require the same way ProdContentReader does so both sides talk to
    // the exact same Render-Postgres-shaped endpoint.
    private static string NormalizeForStore(string connectionString)
    {
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized) { SslMode = SslMode.Require };
        return builder.ConnectionString;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Why: a DEDICATED test-only env var — deliberately NOT DECKFLOW_DATABASE_CONNECTION_STRING
    // (the production connection string per CLAUDE.md). A developer must opt a local/throwaway
    // Postgres instance in explicitly; these tests must never be able to run DDL/DML against prod.
    private const string TestConnectionStringEnvVar = "DECKFLOW_STUDIO_POSTGRES_TEST_CONNECTION_STRING";

    private static string RequireTestConnectionString()
        => Environment.GetEnvironmentVariable(TestConnectionStringEnvVar)
           ?? throw new InvalidOperationException(
               $"{TestConnectionStringEnvVar} must be set when DECKFLOW_POSTGRES_TESTS=1.");

    private static async Task EnsureFeatureFlagsTableAsync(string connectionString)
    {
        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized) { SslMode = SslMode.Require };
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
        await using var connection = await conn.OpenConnectionAsync(CancellationToken.None);

        // Mirrors DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs PostgresCreateTableSql
        // verbatim (D-07 schema) — this test never invents a second flag-table shape.
        await connection.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS feature_flags (
              key        TEXT PRIMARY KEY,
              enabled    BOOLEAN NOT NULL DEFAULT TRUE,
              updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );
            """);
    }

    private static async Task SeedFlagRowAsync(string connectionString, string key, bool enabled)
    {
        await EnsureFeatureFlagsTableAsync(connectionString);

        var normalized = PostgresConnectionStringNormalizer.Normalize(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(normalized) { SslMode = SslMode.Require };
        var conn = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, builder.ConnectionString);
        await using var connection = await conn.OpenConnectionAsync(CancellationToken.None);

        await connection.ExecuteAsync(
            "INSERT INTO feature_flags (key, enabled) VALUES (@key, @enabled) " +
            "ON CONFLICT (key) DO UPDATE SET enabled = @enabled;",
            new { key, enabled });
    }

    /// <summary>
    /// Marks a Fact that runs only when DECKFLOW_POSTGRES_TESTS=1 is set — mirrors
    /// DeckFlow.Web.Tests/Integration/PostgresFactAttribute.cs. Duplicated locally rather than
    /// cross-referenced (DeckFlow.Studio.Tests does not reference DeckFlow.Web.Tests, and this
    /// project carries no Testcontainers dependency — these gated tests target a developer-supplied
    /// local/throwaway Postgres via <see cref="TestConnectionStringEnvVar"/>, never a container).
    /// </summary>
    private sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            var enabled = Environment.GetEnvironmentVariable("DECKFLOW_POSTGRES_TESTS");
            if (!string.Equals(enabled, "1", StringComparison.Ordinal))
            {
                Skip = "Postgres integration tests are disabled. Set DECKFLOW_POSTGRES_TESTS=1 and " +
                       $"{TestConnectionStringEnvVar} (a local/throwaway Postgres — never prod) to enable.";
            }
        }
    }
}
