using System.IO;
using DeckFlow.Core.Content;
using DeckFlow.Core.Models;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CreatorDeckCacheStoreTests : IDisposable
{
    private static readonly DateTimeOffset CachedUtc = DateTimeOffset.Parse("2026-07-11T18:05:00Z");
    private readonly string _dbPath;
    private readonly CreatorDeckCacheStore _store;

    public CreatorDeckCacheStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"creator-deck-cache-test-{Guid.NewGuid():N}.db");
        _store = new CreatorDeckCacheStore(_dbPath);
    }

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
    public async Task UpsertAsync_ThenGetByCreator_RoundTripsMultiDeckSet()
    {
        var expected = CreateDeckSet("snail");

        foreach (var entry in expected)
        {
            await _store.UpsertAsync(entry);
        }

        var actual = await _store.GetByCreatorAsync("snail");

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            AssertEntriesEqual(expected[i], actual[i]);
        }
    }

    [Fact]
    public async Task GetByCreatorAsync_EntriesJson_RoundTripsFieldForField()
    {
        var expected = CreateEntry(
            "entries-fidelity",
            "deck-1",
            "hash-a",
            901,
            "Current",
            100,
            "measured",
            CachedUtc,
            CreateEntries());

        await _store.UpsertAsync(expected);

        var actual = Assert.Single(await _store.GetByCreatorAsync(expected.CreatorSlug));

        AssertEntriesEqual(expected, actual);
        AssertDeckEntriesEqual(expected.Entries, actual.Entries);
    }

    [Fact]
    public async Task GetContentHashAsync_ReturnsStoredHashForHitAndNullForMiss()
    {
        var expected = CreateEntry(
            "hash-check",
            "deck-42",
            "canonical-hash-42",
            303,
            "Secondary",
            99,
            "deduped",
            CachedUtc,
            CreateEntries());

        await _store.UpsertAsync(expected);

        var hit = await _store.GetContentHashAsync(expected.CreatorSlug, expected.DeckId);
        var miss = await _store.GetContentHashAsync(expected.CreatorSlug, "missing-deck");

        Assert.Equal(expected.ContentHash, hit);
        Assert.Null(miss);
    }

    [Fact]
    public async Task GetByCreatorAsync_ScopesRowsByCreatorSlug()
    {
        var creatorA = CreateEntry(
            "creator-a",
            "deck-1",
            "hash-a",
            null,
            null,
            100,
            "measured",
            CachedUtc,
            CreateEntries());
        var creatorB = CreateEntry(
            "creator-b",
            "deck-2",
            "hash-b",
            777,
            "Budget",
            101,
            "uncertain",
            CachedUtc.AddMinutes(3),
            CreateEntries());

        await _store.UpsertAsync(creatorA);
        await _store.UpsertAsync(creatorB);

        var actual = await _store.GetByCreatorAsync("creator-a");

        var entry = Assert.Single(actual);
        Assert.Equal("creator-a", entry.CreatorSlug);
        Assert.Equal("deck-1", entry.DeckId);
    }

    private static List<CreatorDeckCacheEntry> CreateDeckSet(string creatorSlug)
        =>
        [
            CreateEntry(
                    creatorSlug,
                    "deck-001",
                    "hash-001",
                    10,
                    "Current",
                    100,
                    "measured",
                    CachedUtc,
                    CreateEntries()),
                CreateEntry(
                    creatorSlug,
                    "deck-002",
                    "hash-002",
                    20,
                    "Budget",
                    103,
                    "near-precon",
                    CachedUtc.AddMinutes(7),
                    new[]
                    {
                        new DeckEntry
                        {
                            Name = "Sakura-Tribe Elder",
                            NormalizedName = "sakura-tribe elder",
                            Quantity = 1,
                            Board = "mainboard",
                            SetCode = "ima",
                            CollectorNumber = "185",
                            Category = "Ramp,Creature",
                            IsFoil = false
                        },
                        new DeckEntry
                        {
                            Name = "Command Tower",
                            NormalizedName = "command tower",
                            Quantity = 1,
                            Board = "mainboard",
                            SetCode = "clb",
                            CollectorNumber = "350",
                            Category = "Land",
                            IsFoil = true
                        }
                    })
        ];

    private static IReadOnlyList<DeckEntry> CreateEntries()
        =>
        [
            new DeckEntry
                {
                    Name = "Mystic Remora",
                    NormalizedName = "mystic remora",
                    Quantity = 1,
                    Board = "mainboard",
                    SetCode = "ice",
                    CollectorNumber = "87",
                    Category = "Draw",
                    IsFoil = false
                },
                new DeckEntry
                {
                    Name = "Tymna the Weaver",
                    NormalizedName = "tymna the weaver",
                    Quantity = 1,
                    Board = "commander",
                    SetCode = "c16",
                    CollectorNumber = "45",
                    Category = "Commander,Draw",
                    IsFoil = true
                },
                new DeckEntry
                {
                    Name = "Arid Mesa",
                    NormalizedName = "arid mesa",
                    Quantity = 1,
                    Board = "mainboard",
                    SetCode = "mh2",
                    CollectorNumber = "244",
                    Category = "Land",
                    IsFoil = false
                }
        ];

    private static CreatorDeckCacheEntry CreateEntry(
        string creatorSlug,
        string deckId,
        string contentHash,
        int? folderId,
        string? folderName,
        int size,
        string confidenceMarker,
        DateTimeOffset cachedUtc,
        IReadOnlyList<DeckEntry> entries)
        => new()
        {
            CreatorSlug = creatorSlug,
            DeckId = deckId,
            ContentHash = contentHash,
            FolderId = folderId,
            FolderName = folderName,
            Size = size,
            ConfidenceMarker = confidenceMarker,
            Entries = entries,
            CachedUtc = cachedUtc
        };

    private static void AssertEntriesEqual(CreatorDeckCacheEntry expected, CreatorDeckCacheEntry actual)
    {
        Assert.Equal(expected.CreatorSlug, actual.CreatorSlug);
        Assert.Equal(expected.DeckId, actual.DeckId);
        Assert.Equal(expected.ContentHash, actual.ContentHash);
        Assert.Equal(expected.FolderId, actual.FolderId);
        Assert.Equal(expected.FolderName, actual.FolderName);
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.ConfidenceMarker, actual.ConfidenceMarker);
        AssertDeckEntriesEqual(expected.Entries, actual.Entries);
        AssertCloseTo(expected.CachedUtc, actual.CachedUtc);
    }

    private static void AssertDeckEntriesEqual(IReadOnlyList<DeckEntry> expected, IReadOnlyList<DeckEntry> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Name, actual[i].Name);
            Assert.Equal(expected[i].NormalizedName, actual[i].NormalizedName);
            Assert.Equal(expected[i].Quantity, actual[i].Quantity);
            Assert.Equal(expected[i].Board, actual[i].Board);
            Assert.Equal(expected[i].SetCode, actual[i].SetCode);
            Assert.Equal(expected[i].CollectorNumber, actual[i].CollectorNumber);
            Assert.Equal(expected[i].Category, actual[i].Category);
            Assert.Equal(expected[i].IsFoil, actual[i].IsFoil);
        }
    }

    private static void AssertCloseTo(DateTimeOffset expected, DateTimeOffset actual)
    {
        var delta = (expected - actual).Duration();
        Assert.True(
            delta <= TimeSpan.FromSeconds(1),
            $"Expected timestamp within 1 second. Expected {expected:o}, actual {actual:o}.");
    }
}
