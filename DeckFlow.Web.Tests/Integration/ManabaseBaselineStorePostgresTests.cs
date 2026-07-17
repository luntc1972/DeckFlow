using DeckFlow.Core.Manabase;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests.Integration;

/// <summary>
/// Integration tests for the Postgres-backed <see cref="ManabaseBaselineStore"/> path,
/// gated behind <see cref="PostgresFactAttribute"/> and backed by <see cref="PostgresContainerFixture"/>.
/// </summary>
public sealed class ManabaseBaselineStorePostgresTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    /// <summary>
    /// Initializes the shared Postgres test fixture.
    /// </summary>
    /// <param name="fixture">Container fixture providing the Postgres connection string.</param>
    public ManabaseBaselineStorePostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task Upsert_then_Get_roundtrips_with_utc_kind()
    {
        var store = await CreateStoreAsync();
        var slug = $"pg-baseline-{Guid.NewGuid():N}";
        var expected = CreateRow(
            commanderSlug: slug,
            bracket: 3,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 35.9,
            avgRamp: 10.4,
            avgDraw: 8.2,
            deckCount: 42,
            computedUtc: new DateTime(2026, 07, 17, 12, 34, 56, DateTimeKind.Utc));

        await store.UpsertAsync(expected);

        var actual = Assert.Single(await store.GetAsync(slug, 3));
        Assert.Equal(DateTimeKind.Utc, actual.ComputedUtc.Kind);
        AssertRowsEqual(expected, actual);
    }

    [PostgresFact]
    public async Task Upsert_same_key_updates_in_place()
    {
        var store = await CreateStoreAsync();
        var slug = $"pg-baseline-{Guid.NewGuid():N}";

        await store.UpsertAsync(CreateRow(
            commanderSlug: slug,
            bracket: 4,
            source: ManabaseBaselineSources.Edhrec,
            avgLands: 34.7,
            avgRamp: 9.8,
            avgDraw: 7.4,
            deckCount: 15,
            computedUtc: new DateTime(2026, 07, 17, 11, 00, 00, DateTimeKind.Utc)));

        var expected = CreateRow(
            commanderSlug: slug,
            bracket: 4,
            source: ManabaseBaselineSources.Edhrec,
            avgLands: 36.6,
            avgRamp: 11.1,
            avgDraw: 8.8,
            deckCount: 23,
            computedUtc: new DateTime(2026, 07, 17, 12, 00, 00, DateTimeKind.Utc));

        await store.UpsertAsync(expected);

        var actual = Assert.Single(await store.GetAsync(slug, 4));
        Assert.Equal(DateTimeKind.Utc, actual.ComputedUtc.Kind);
        AssertRowsEqual(expected, actual);
    }

    private async Task<ManabaseBaselineStore> CreateStoreAsync()
        => new(new RelationalDatabaseConnection(RelationalDatabaseProvider.Postgres, await _fixture.GetConnectionStringOrSkipAsync()));

    private static void AssertRowsEqual(ManabaseBaselineRow expected, ManabaseBaselineRow actual)
    {
        Assert.Equal(expected.CommanderSlug, actual.CommanderSlug);
        Assert.Equal(expected.Bracket, actual.Bracket);
        Assert.Equal(expected.Source, actual.Source);
        Assert.Equal(expected.AvgLands, actual.AvgLands);
        Assert.Equal(expected.AvgRamp, actual.AvgRamp);
        Assert.Equal(expected.AvgDraw, actual.AvgDraw);
        Assert.Equal(expected.DeckCount, actual.DeckCount);
        Assert.Equal(expected.ComputedUtc, actual.ComputedUtc);
    }

    private static ManabaseBaselineRow CreateRow(
        string commanderSlug,
        int bracket,
        string source,
        double avgLands,
        double avgRamp,
        double avgDraw,
        int deckCount,
        DateTime computedUtc) =>
        new()
        {
            CommanderSlug = commanderSlug,
            Bracket = bracket,
            Source = source,
            AvgLands = avgLands,
            AvgRamp = avgRamp,
            AvgDraw = avgDraw,
            DeckCount = deckCount,
            ComputedUtc = computedUtc,
        };
}
