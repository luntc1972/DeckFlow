using System.IO;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Integration tests for <see cref="ManabaseBaselineStore"/> covering SQLite persistence,
/// upsert semantics, and UTC timestamp round-trips against a temporary database.
/// </summary>
public sealed class ManabaseBaselineStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ManabaseBaselineStore _store;

    /// <summary>
    /// Initializes a test store backed by a unique temporary SQLite database.
    /// </summary>
    public ManabaseBaselineStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"manabase-baseline-test-{Guid.NewGuid():N}.db");
        _store = new ManabaseBaselineStore(_dbPath);
    }

    /// <summary>
    /// Releases SQLite file handles and deletes the temporary database.
    /// </summary>
    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task Upsert_then_Get_returns_row()
    {
        var computedUtc = TrimToSecondUtc(DateTime.UtcNow);
        var row = CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 3,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 35.9,
            avgRamp: 10.4,
            avgDraw: 8.2,
            deckCount: 42,
            computedUtc: computedUtc);

        await _store.UpsertAsync(row);

        var fetched = await _store.GetAsync("smeagol-helpful-guide", 3);
        var actual = Assert.Single(fetched);
        AssertRowsEqual(row, actual);
    }

    [Fact]
    public async Task Upsert_same_key_updates_in_place()
    {
        await _store.UpsertAsync(CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 3,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 34.1,
            avgRamp: 9.2,
            avgDraw: 7.8,
            deckCount: 18,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow.AddMinutes(-5))));

        var updated = CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 3,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 36.4,
            avgRamp: 11.0,
            avgDraw: 8.9,
            deckCount: 27,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow));

        await _store.UpsertAsync(updated);

        var fetched = await _store.GetAsync("smeagol-helpful-guide", 3);
        var actual = Assert.Single(fetched);
        AssertRowsEqual(updated, actual);
    }

    [Fact]
    public async Task Get_returns_all_sources_for_cell()
    {
        var corpus = CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 3,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 35.1,
            avgRamp: 10.2,
            avgDraw: 8.3,
            deckCount: 30,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow.AddMinutes(-2)));
        var edhrec = CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 3,
            source: ManabaseBaselineSources.Edhrec,
            avgLands: 36.2,
            avgRamp: 9.9,
            avgDraw: 7.7,
            deckCount: 50,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow));

        await _store.UpsertAsync(corpus);
        await _store.UpsertAsync(edhrec);

        var fetched = await _store.GetAsync("smeagol-helpful-guide", 3);
        Assert.Equal(2, fetched.Count);
        Assert.Contains(fetched, row => row.Source == ManabaseBaselineSources.Corpus);
        Assert.Contains(fetched, row => row.Source == ManabaseBaselineSources.Edhrec);
    }

    [Fact]
    public async Task Get_unknown_returns_empty()
    {
        var fetched = await _store.GetAsync("nobody", 1);
        Assert.NotNull(fetched);
        Assert.Empty(fetched);
    }

    [Fact]
    public async Task Global_row_roundtrips()
    {
        var row = CreateRow(
            commanderSlug: ManabaseBaselineSources.GlobalCommanderSlug,
            bracket: 2,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 37.0,
            avgRamp: 9.5,
            avgDraw: 8.0,
            deckCount: 100,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow));

        await _store.UpsertAsync(row);

        var fetched = await _store.GetAsync(ManabaseBaselineSources.GlobalCommanderSlug, 2);
        var actual = Assert.Single(fetched);
        AssertRowsEqual(row, actual);
    }

    [Fact]
    public async Task ComputedUtc_roundtrips_utc()
    {
        var computedUtc = new DateTime(2026, 07, 17, 12, 34, 56, DateTimeKind.Utc);
        var row = CreateRow(
            commanderSlug: "time-proof-commander",
            bracket: 4,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 35.9,
            avgRamp: 10.0,
            avgDraw: 7.0,
            deckCount: 12,
            computedUtc: computedUtc);

        await _store.UpsertAsync(row);

        var actual = Assert.Single(await _store.GetAsync("time-proof-commander", 4));
        Assert.Equal(DateTimeKind.Utc, actual.ComputedUtc.Kind);
        Assert.Equal(computedUtc, TrimToSecondUtc(actual.ComputedUtc));
    }

    [Fact]
    public async Task UpsertRange_persists_all()
    {
        var rows = new[]
        {
            CreateRow(
                commanderSlug: "aragorn-the-uniter",
                bracket: 3,
                source: ManabaseBaselineSources.Corpus,
                avgLands: 36.1,
                avgRamp: 10.8,
                avgDraw: 8.6,
                deckCount: 25,
                computedUtc: TrimToSecondUtc(DateTime.UtcNow.AddMinutes(-3))),
            CreateRow(
                commanderSlug: "aragorn-the-uniter",
                bracket: 4,
                source: ManabaseBaselineSources.Corpus,
                avgLands: 35.0,
                avgRamp: 11.4,
                avgDraw: 9.1,
                deckCount: 31,
                computedUtc: TrimToSecondUtc(DateTime.UtcNow.AddMinutes(-2))),
            CreateRow(
                commanderSlug: "eowyn-shieldmaiden",
                bracket: 3,
                source: ManabaseBaselineSources.Edhrec,
                avgLands: 37.3,
                avgRamp: 9.2,
                avgDraw: 7.5,
                deckCount: 19,
                computedUtc: TrimToSecondUtc(DateTime.UtcNow.AddMinutes(-1))),
        };

        await _store.UpsertRangeAsync(rows);
        await _store.UpsertRangeAsync(Array.Empty<ManabaseBaselineRow>());

        var aragornThree = Assert.Single(await _store.GetAsync("aragorn-the-uniter", 3));
        var aragornFour = Assert.Single(await _store.GetAsync("aragorn-the-uniter", 4));
        var eowynThree = Assert.Single(await _store.GetAsync("eowyn-shieldmaiden", 3));
        AssertRowsEqual(rows[0], aragornThree);
        AssertRowsEqual(rows[1], aragornFour);
        AssertRowsEqual(rows[2], eowynThree);
    }

    [Fact]
    public async Task Get_scopes_by_bracket()
    {
        var bracketThree = CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 3,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 35.0,
            avgRamp: 10.0,
            avgDraw: 8.0,
            deckCount: 20,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow.AddMinutes(-2)));
        var bracketFour = CreateRow(
            commanderSlug: "smeagol-helpful-guide",
            bracket: 4,
            source: ManabaseBaselineSources.Corpus,
            avgLands: 34.0,
            avgRamp: 11.0,
            avgDraw: 9.0,
            deckCount: 22,
            computedUtc: TrimToSecondUtc(DateTime.UtcNow));

        await _store.UpsertRangeAsync(new[] { bracketThree, bracketFour });

        var fetched = await _store.GetAsync("smeagol-helpful-guide", 3);
        var actual = Assert.Single(fetched);
        AssertRowsEqual(bracketThree, actual);
    }

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

    private static DateTime TrimToSecondUtc(DateTime value)
    {
        var utc = value.ToUniversalTime();
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Utc);
    }
}
