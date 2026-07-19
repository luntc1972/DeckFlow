using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services.CreatorStyle;
using System.Diagnostics;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="CreatorProfileDeckCrawler"/>.
/// </summary>
public sealed class CreatorProfileDeckCrawlerTests
{
    [Fact]
    public async Task CrawlAsync_HappyPath_ResolvesListsImportsAndMapsFolderWeight()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "archidekt",
            ProfileUsername = "snail",
            FolderWeights = new Dictionary<int, double> { [42] = 0.25 },
            UpdatedUtc = now
        });

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckSummaries =
            [
                new ArchidektDeckSummary
                {
                    Id = "deck-1",
                    Name = "Deck One",
                    Size = 100,
                    ParentFolderId = 42,
                    ParentFolderName = "Budget"
                }
            ]
        };
        var importer = new CountingDeckImporter();
        importer.Decks["deck-1"] = [MainboardEntry("Sol Ring"), CommanderEntry("Atraxa, Praetors' Voice")];
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        var samples = await sut.CrawlAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("deck-1", sample.DeckId);
        Assert.Equal(100, sample.CardCount);
        Assert.Equal(42, sample.FolderId);
        Assert.Equal("Budget", sample.FolderName);
        Assert.Equal(0.25, sample.FolderWeight);
        Assert.Equal("ok", sample.ConfidenceMarker);
        Assert.Equal(2, sample.Entries.Count);
        Assert.Equal(1, ownerClient.ResolveUsernameCalls);
        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(1, importer.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_DropsOversizedDecksBeforeImport()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now);

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckSummaries =
            [
                new ArchidektDeckSummary
                {
                    Id = "too-big",
                    Name = "Too Big",
                    Size = 106,
                    ParentFolderId = 1,
                    ParentFolderName = "Current"
                },
                new ArchidektDeckSummary
                {
                    Id = "good",
                    Name = "Good",
                    Size = 100,
                    ParentFolderId = 1,
                    ParentFolderName = "Current"
                }
            ]
        };
        var importer = new CountingDeckImporter();
        importer.Decks["good"] = [MainboardEntry("Arcane Signet")];
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        var samples = await sut.CrawlAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("good", sample.DeckId);
        Assert.Equal(1, importer.ImportCalls);
        Assert.DoesNotContain("too-big", importer.ImportedDeckIds);
    }

    [Fact]
    public async Task CrawlAsync_WarmCacheWithinWindow_ReturnsFullyPopulatedSamplesWithZeroArchidektCalls()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "archidekt",
            ProfileUsername = "snail",
            FolderWeights = new Dictionary<int, double> { [5] = 0.5 },
            LastCrawledUtc = now.AddMinutes(-5),
            UpdatedUtc = now
        });
        await harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = "cached-1",
            ContentHash = "hash-1",
            FolderId = 5,
            FolderName = "Budget",
            Size = 100,
            ConfidenceMarker = "cached",
            Entries = [MainboardEntry("Command Tower"), MainboardEntry("Sol Ring")],
            CachedUtc = now.AddMinutes(-10)
        });

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail"
        };
        var importer = new CountingDeckImporter();
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        var samples = await sut.CrawlAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("cached-1", sample.DeckId);
        Assert.Equal("Budget", sample.FolderName);
        Assert.Equal(0.5, sample.FolderWeight);
        Assert.Equal("cached", sample.ConfidenceMarker);
        Assert.Equal(2, sample.Entries.Count);
        Assert.Equal("Command Tower", sample.Entries[0].Name);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls);
        Assert.Equal(0, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(0, importer.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_ExpiredWindow_ReenumeratesButReusesPerDeckCacheAndRestampsFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "archidekt",
            ProfileUsername = "snail",
            FolderWeights = new Dictionary<int, double> { [1] = 1.0 },
            LastCrawledUtc = now.AddDays(-2),
            UpdatedUtc = now.AddDays(-2)
        });
        await harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = "cached-1",
            ContentHash = "hash-1",
            FolderId = 1,
            FolderName = "Current",
            Size = 100,
            ConfidenceMarker = "cached",
            Entries = [MainboardEntry("Swords to Plowshares")],
            CachedUtc = now.AddDays(-2)
        });

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckSummaries =
            [
                new ArchidektDeckSummary
                {
                    Id = "cached-1",
                    Name = "Cached",
                    Size = 100,
                    ParentFolderId = 1,
                    ParentFolderName = "Current"
                }
            ]
        };
        var importer = new CountingDeckImporter();
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        var samples = await sut.CrawlAsync("snail");
        var refreshed = await harness.ProfileStore.GetBySlugAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("cached-1", sample.DeckId);
        Assert.Equal(1, ownerClient.ResolveUsernameCalls);
        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(0, importer.ImportCalls);
        Assert.Equal(now, refreshed?.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_ForceRefreshBypassesWarmCache()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "archidekt",
            ProfileUsername = "snail",
            LastCrawledUtc = now.AddMinutes(-5),
            UpdatedUtc = now
        });
        await harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = "cached-1",
            ContentHash = "hash-1",
            FolderId = 1,
            FolderName = "Current",
            Size = 100,
            ConfidenceMarker = "cached",
            Entries = [MainboardEntry("Mystic Remora")],
            CachedUtc = now
        });

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckSummaries =
            [
                new ArchidektDeckSummary
                {
                    Id = "cached-1",
                    Name = "Cached",
                    Size = 100,
                    ParentFolderId = 1,
                    ParentFolderName = "Current"
                }
            ]
        };
        var importer = new CountingDeckImporter();
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        await sut.CrawlAsync("snail", forceRefresh: true);

        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
    }

    [Fact]
    public async Task CrawlAsync_UsesManualUrlFallbackWhenResolveReturnsNull()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "archidekt",
            ProfileUsername = "not-found",
            ProfileUrl = "https://archidekt.com/u/fallback-user/",
            UpdatedUtc = now
        });

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = null,
            DeckSummaries =
            [
                new ArchidektDeckSummary
                {
                    Id = "deck-1",
                    Name = "Deck One",
                    Size = 100,
                    ParentFolderId = 1,
                    ParentFolderName = "Current"
                }
            ]
        };
        var importer = new CountingDeckImporter();
        importer.Decks["deck-1"] = [MainboardEntry("Counterspell")];
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        var samples = await sut.CrawlAsync("snail");

        Assert.Single(samples);
        Assert.Equal("fallback-user", ownerClient.LastListUsername);
        Assert.Equal(1, ownerClient.ResolveUsernameCalls);
        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(1, importer.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldPlatform_UsesMoxfieldClientAndImporterOnly()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "moxfield",
            ProfileUsername = "snail",
            UpdatedUtc = now
        });

        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient
        {
            DeckSummaries =
            [
                new MoxfieldDeckSummary("public-1", "Deck One", "commander", "public")
            ]
        };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["public-1"] = [MainboardEntry("Sol Ring")];
        var sut = CreateCrawler(harness, ownerClient, importer, moxfieldOwnerClient, moxfieldImporter, now);

        var samples = await sut.CrawlAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("public-1", sample.DeckId);
        Assert.Null(sample.FolderId);
        Assert.Null(sample.FolderName);
        Assert.Equal(1, sample.CardCount);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls);
        Assert.Equal(0, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(0, importer.ImportCalls);
        Assert.Equal(1, moxfieldOwnerClient.ListDeckSummariesCalls);
        Assert.Equal("snail", moxfieldOwnerClient.LastUsername);
        Assert.Equal(1, moxfieldImporter.ImportCalls);
        Assert.Equal("public-1", Assert.Single(moxfieldImporter.ImportedDeckIds));
    }

    [Fact]
    public async Task CrawlAsync_UnknownPlatform_DefaultsToArchidektPath()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "unknown-platform",
            ProfileUsername = "snail",
            UpdatedUtc = now
        });

        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckSummaries =
            [
                new ArchidektDeckSummary
                {
                    Id = "deck-1",
                    Name = "Deck One",
                    Size = 100
                }
            ]
        };
        var importer = new CountingDeckImporter();
        importer.Decks["deck-1"] = [MainboardEntry("Arcane Signet")];
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        var sut = CreateCrawler(harness, ownerClient, importer, moxfieldOwnerClient, moxfieldImporter, now);

        var samples = await sut.CrawlAsync("snail");

        Assert.Single(samples);
        Assert.Equal(1, ownerClient.ResolveUsernameCalls);
        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(1, importer.ImportCalls);
        Assert.Equal(0, moxfieldOwnerClient.ListDeckSummariesCalls);
        Assert.Equal(0, moxfieldImporter.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldWarmCacheWithinWindow_SkipsListAndImport()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "moxfield",
            ProfileUsername = "snail",
            LastCrawledUtc = now.AddMinutes(-5),
            UpdatedUtc = now
        });
        await harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = "public-1",
            ContentHash = "hash-1",
            Size = 100,
            ConfidenceMarker = "cached",
            Entries = [MainboardEntry("Command Tower")],
            CachedUtc = now.AddMinutes(-10)
        });

        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        var sut = CreateCrawler(harness, ownerClient, importer, moxfieldOwnerClient, moxfieldImporter, now);

        var samples = await sut.CrawlAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("public-1", sample.DeckId);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls);
        Assert.Equal(0, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(0, importer.ImportCalls);
        Assert.Equal(0, moxfieldOwnerClient.ListDeckSummariesCalls);
        Assert.Equal(0, moxfieldImporter.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldOversizedImportedDeck_IsCachedButExcludedFromReturnedSamples()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "moxfield",
            ProfileUsername = "snail",
            UpdatedUtc = now
        });

        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient
        {
            DeckSummaries =
            [
                new MoxfieldDeckSummary("public-oversized", "Too Big", "commander", "public")
            ]
        };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["public-oversized"] = Enumerable.Range(0, 106)
            .Select(index => MainboardEntry($"Card {index}"))
            .ToList();
        var sut = CreateCrawler(harness, ownerClient, importer, moxfieldOwnerClient, moxfieldImporter, now);

        var samples = await sut.CrawlAsync("snail");
        var cacheEntries = await harness.CacheStore.GetByCreatorAsync("snail");

        Assert.Empty(samples);
        var cacheEntry = Assert.Single(cacheEntries);
        Assert.Equal("public-oversized", cacheEntry.DeckId);
        Assert.Equal(106, cacheEntry.Size);
        Assert.Equal(1, moxfieldImporter.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldReenumeration_AllCacheHits_DoesNotPayImportDelay()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await harness.ProfileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "moxfield",
            ProfileUsername = "snail",
            LastCrawledUtc = now.AddDays(-2),
            UpdatedUtc = now.AddDays(-2)
        });
        await harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = "public-1",
            ContentHash = "hash-1",
            Size = 100,
            ConfidenceMarker = "cached-1",
            Entries = [MainboardEntry("Command Tower")],
            CachedUtc = now.AddDays(-2)
        });
        await harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = "public-2",
            ContentHash = "hash-2",
            Size = 100,
            ConfidenceMarker = "cached-2",
            Entries = [MainboardEntry("Sol Ring")],
            CachedUtc = now.AddDays(-2)
        });

        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient
        {
            DeckSummaries =
            [
                new MoxfieldDeckSummary("public-1", "Deck One", "commander", "public"),
                new MoxfieldDeckSummary("public-2", "Deck Two", "commander", "public")
            ]
        };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        var sut = CreateCrawler(harness, ownerClient, importer, moxfieldOwnerClient, moxfieldImporter, now);
        var stopwatch = Stopwatch.StartNew();

        var samples = await sut.CrawlAsync("snail");

        stopwatch.Stop();
        Assert.Equal(2, samples.Count);
        Assert.Equal(0, moxfieldImporter.ImportCalls);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromMilliseconds(300),
            $"Expected cached crawl to avoid the 500ms import delay, but elapsed {stopwatch.Elapsed.TotalMilliseconds:F1}ms.");
    }

    private static CreatorProfileDeckCrawler CreateCrawler(
        TestHarness harness,
        CountingOwnerClient ownerClient,
        CountingDeckImporter importer,
        CountingMoxfieldOwnerClient? moxfieldOwnerClient,
        CountingMoxfieldDeckImporter? moxfieldImporter,
        DateTimeOffset now)
    {
        return new CreatorProfileDeckCrawler(
            ownerClient,
            importer,
            moxfieldOwnerClient ?? new CountingMoxfieldOwnerClient(),
            moxfieldImporter ?? new CountingMoxfieldDeckImporter(),
            harness.ProfileStore,
            harness.CacheStore,
            freshnessWindow: TimeSpan.FromHours(1),
            nowUtc: () => now);
    }

    private static CreatorProfileDeckCrawler CreateCrawler(
        TestHarness harness,
        CountingOwnerClient ownerClient,
        CountingDeckImporter importer,
        DateTimeOffset now)
        => CreateCrawler(harness, ownerClient, importer, null, null, now);

    private static async Task SeedSourceAsync(ICreatorProfileSourceStore profileStore, DateTimeOffset now)
    {
        await profileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = "archidekt",
            ProfileUsername = "snail",
            FolderWeights = new Dictionary<int, double> { [1] = 1.0 },
            UpdatedUtc = now
        });
    }

    private static DeckEntry MainboardEntry(string name)
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = "mainboard"
        };
    }

    private static DeckEntry CommanderEntry(string name)
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = 1,
            Board = "commander"
        };
    }

    private static Task<TestHarness> CreateHarnessAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "deckflow-95-06-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "creator-style.sqlite");
        return Task.FromResult(new TestHarness(
            directory,
            new CreatorProfileSourceStore(databasePath),
            new CreatorDeckCacheStore(databasePath)));
    }

    private sealed class CountingOwnerClient : IArchidektOwnerClient
    {
        public string? ResolvedUsername { get; init; }

        public IReadOnlyList<ArchidektDeckSummary> DeckSummaries { get; init; } = Array.Empty<ArchidektDeckSummary>();

        public int ResolveUsernameCalls { get; private set; }

        public int ListDeckSummariesCalls { get; private set; }

        public string? LastListUsername { get; private set; }

        public Task<string?> ResolveUsernameAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
        {
            ResolveUsernameCalls++;
            return Task.FromResult(ResolvedUsername);
        }

        public Task<IReadOnlyList<ArchidektDeckSummary>> ListDeckSummariesAsync(string ownerUsername, CancellationToken cancellationToken = default)
        {
            ListDeckSummariesCalls++;
            LastListUsername = ownerUsername;
            return Task.FromResult(DeckSummaries);
        }
    }

    private sealed class CountingDeckImporter : IArchidektDeckImporter
    {
        public Dictionary<string, List<DeckEntry>> Decks { get; } = new(StringComparer.Ordinal);

        public List<string> ImportedDeckIds { get; } = [];

        public int ImportCalls => ImportedDeckIds.Count;

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken ct = default)
        {
            ImportedDeckIds.Add(urlOrDeckId);
            if (!Decks.TryGetValue(urlOrDeckId, out var deck))
            {
                throw new InvalidOperationException($"Missing fake deck: {urlOrDeckId}");
            }

            return Task.FromResult(deck);
        }
    }

    private sealed class CountingMoxfieldOwnerClient : IMoxfieldOwnerClient
    {
        public IReadOnlyList<MoxfieldDeckSummary> DeckSummaries { get; init; } = Array.Empty<MoxfieldDeckSummary>();

        public int ListDeckSummariesCalls { get; private set; }

        public string? LastUsername { get; private set; }

        public Task<IReadOnlyList<MoxfieldDeckSummary>> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default)
        {
            ListDeckSummariesCalls++;
            LastUsername = username;
            return Task.FromResult(DeckSummaries);
        }
    }

    private sealed class CountingMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Dictionary<string, List<DeckEntry>> Decks { get; } = new(StringComparer.Ordinal);

        public List<string> ImportedDeckIds { get; } = [];

        public int ImportCalls => ImportedDeckIds.Count;

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            ImportedDeckIds.Add(urlOrDeckId);
            if (!Decks.TryGetValue(urlOrDeckId, out var deck))
            {
                throw new InvalidOperationException($"Missing fake Moxfield deck: {urlOrDeckId}");
            }

            return Task.FromResult(deck);
        }
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        public TestHarness(string directory, CreatorProfileSourceStore profileStore, CreatorDeckCacheStore cacheStore)
        {
            Directory = directory;
            ProfileStore = profileStore;
            CacheStore = cacheStore;
        }

        public string Directory { get; }

        public CreatorProfileSourceStore ProfileStore { get; }

        public CreatorDeckCacheStore CacheStore { get; }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (System.IO.Directory.Exists(Directory))
                {
                    System.IO.Directory.Delete(Directory, recursive: true);
                }
            }
            catch
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
