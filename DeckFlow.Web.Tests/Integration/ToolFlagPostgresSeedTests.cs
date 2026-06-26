using DeckFlow.Core.Storage;
using DeckFlow.Web.Services.FeatureFlags;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Proves the Postgres feature-flag seed path includes the new tool keys.
/// </summary>
public sealed class ToolFlagPostgresSeedTests : IClassFixture<PostgresContainerFixture>
{
    private static readonly string[] ExpectedToolKeys =
    [
        "tool.deck-analysis.enabled",
        "tool.deck-comparison.enabled",
        "tool.cedh-meta-gap.enabled",
        "tool.deck-sync.enabled",
        "tool.convert.enabled",
        "tool.deck-primer.enabled",
        "tool.card-lookup.enabled",
        "tool.mechanic-lookup.enabled",
        "tool.judge-questions.enabled",
        "tool.commander-categories.enabled",
    ];

    private readonly PostgresContainerFixture _fixture;

    public ToolFlagPostgresSeedTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task EnsureSchemaAsync_SeedsAllNewToolFlags_DefaultOn()
    {
        var connectionString = await _fixture.GetConnectionStringOrSkipAsync();
        var connection = new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, connectionString);
        var store = new FeatureFlagStore(connection);

        await store.EnsureSchemaAsync();

        var flags = await store.GetAllAsync();
        Assert.All(ExpectedToolKeys, key =>
        {
            Assert.True(flags.TryGetValue(key, out var enabled), $"Missing Postgres-seeded key '{key}'.");
            Assert.True(enabled, $"Postgres-seeded key '{key}' should default to enabled.");
        });
    }
}
