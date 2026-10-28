using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services.CreatorStyle;
using DeckFlow.Web.Tests;
using DeckFlow.Web.Tests.TestDoubles;
using RestSharp;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

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
        importer.Decks["cached-1"] = [MainboardEntry("Rhystic Study")];
        var sut = CreateCrawler(harness, ownerClient, importer, now);

        var samples = await sut.CrawlAsync("snail", forceRefresh: true);

        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(1, importer.ImportCalls);
        Assert.Equal("rhystic study", samples.Single().Entries.Single().NormalizedName);
    }

    [Fact]
    public async Task CrawlAsync_ForceRefreshWithUnchangedHash_ReusesCachedRowWithoutUpsert()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now);
        var entries = new List<DeckEntry> { MainboardEntry("Mystic Remora") };
        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckSummaries = [new() { Id = "cached-1", Name = "Cached", Size = 100 }]
        };
        var importer = new CountingDeckImporter();
        importer.Decks["cached-1"] = entries;

        await CreateCrawler(harness, ownerClient, importer, now).CrawlAsync("snail", forceRefresh: true);
        var firstCached = (await harness.CacheStore.GetByCreatorAsync("snail")).Single();
        importer.Decks["cached-1"] = [.. firstCached.Entries];
        await CreateCrawler(harness, ownerClient, importer, now.AddHours(1)).CrawlAsync("snail", forceRefresh: true);

        var cached = (await harness.CacheStore.GetByCreatorAsync("snail")).Single();
        Assert.Equal(2, importer.ImportCalls);
        Assert.Equal(now, cached.CachedUtc);
    }

    [Fact]
    public async Task CrawlAsync_ListDeckSummariesFails_DoesNotRestampFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now);
        var ownerClient = new CountingOwnerClient
        {
            ResolvedUsername = "snail",
            DeckListResult = new ArchidektDeckListResult
            {
                Decks = [],
                HasUpstreamFailure = true
            }
        };

        await CreateCrawler(harness, ownerClient, new CountingDeckImporter(), now).CrawlAsync("snail");

        var source = await harness.ProfileStore.GetBySlugAsync("snail");
        Assert.Null(source!.LastCrawledUtc);
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

    [Theory]
    [InlineData("moxfield")]
    [InlineData("Moxfield")]
    [InlineData(" MOXFIELD ")]
    public async Task CrawlAsync_MoxfieldPlatform_RoutesToMoxfieldClientsWithZeroArchidektCalls(string platform)
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, platform);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-1")] };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["mox-1"] = [MainboardEntry("Sol Ring"), CommanderEntry("Atraxa, Praetors' Voice")];
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail" };
        var importer = new CountingDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        var sample = Assert.Single(samples);
        Assert.Equal("mox-1", sample.DeckId);
        Assert.Equal(2, sample.CardCount);
        Assert.Null(sample.FolderId);
        Assert.Null(sample.FolderName);
        Assert.Equal(1.0, sample.FolderWeight);
        Assert.Equal("ok", sample.ConfidenceMarker);
        Assert.Equal(1, moxfieldOwnerClient.ListCalls);
        Assert.Equal("snail", moxfieldOwnerClient.LastListUsername);
        Assert.Equal(["mox-1"], moxfieldImporter.ImportedDeckIds);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls);
        Assert.Equal(0, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(0, importer.ImportCalls);
        Assert.Equal(now, (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Theory]
    [InlineData("archidekt")]
    [InlineData("Archidekt")]
    public async Task CrawlAsync_ArchidektPlatform_RoutesToArchidektClientsWithZeroMoxfieldCalls(string platform)
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, platform);
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail", DeckSummaries = [new ArchidektDeckSummary { Id = "deck-1", Name = "Deck", Size = 2 }] };
        var importer = new CountingDeckImporter();
        importer.Decks["deck-1"] = [MainboardEntry("Sol Ring"), CommanderEntry("Atraxa")];
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        Assert.Single(samples);
        Assert.Equal(1, ownerClient.ResolveUsernameCalls);
        Assert.Equal(1, ownerClient.ListDeckSummariesCalls);
        Assert.Equal(1, importer.ImportCalls);
        Assert.Equal(0, moxfieldOwnerClient.ListCalls);
        Assert.Empty(moxfieldImporter.ImportedDeckIds);
    }

    [Fact]
    public async Task CrawlAsync_UnsupportedPlatform_LogsWarningAndReturnsEmptyWithoutUpstreamCalls()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "deckstats");
        var logger = new CapturingLogger<CreatorProfileDeckCrawler>();
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail" };
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter, logger: logger).CrawlAsync("snail");

        Assert.Empty(samples);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls + moxfieldOwnerClient.ListCalls + moxfieldImporter.ImportedDeckIds.Count);
        Assert.Null((await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
        Assert.Contains(logger.Entries, entry => HasState(entry, "Platform", "deckstats") && HasState(entry, "CreatorSlug", "snail"));
    }

    [Fact]
    public async Task CrawlAsync_UnsupportedPlatformWithFreshCrawlStamp_WarnsAndReturnsEmptyWithoutServingCache()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "deckstats", now.AddMinutes(-5));
        await SeedCacheAsync(harness, "mox-timeout", now);
        await SeedCacheAsync(harness, "stale", now);
        var logger = new CapturingLogger<CreatorProfileDeckCrawler>();
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail" };
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter, logger: logger).CrawlAsync("snail");

        Assert.Empty(samples);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls + moxfieldOwnerClient.ListCalls + moxfieldImporter.ImportedDeckIds.Count);
        Assert.Equal(now.AddMinutes(-5), (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
        Assert.Contains(logger.Entries, entry => HasState(entry, "Platform", "deckstats") && HasState(entry, "CreatorSlug", "snail"));
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldWarmCacheWithinWindow_ServesCacheWithZeroUpstreamCalls()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield", now.AddMinutes(-5));
        await SeedCacheAsync(harness, "mox-1", now);
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail" };
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        Assert.Equal("mox-1", Assert.Single(samples).DeckId);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls + moxfieldOwnerClient.ListCalls + moxfieldImporter.ImportedDeckIds.Count);
        Assert.Equal(now.AddMinutes(-5), (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldListingFails_ServesCacheAndDoesNotRestampFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        await SeedCacheAsync(harness, "cached-1", now);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Result = new MoxfieldDeckListResult { Decks = [], HasUpstreamFailure = true } };
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail" };
        var importer = new CountingDeckImporter();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        Assert.Equal("cached-1", Assert.Single(samples).DeckId);
        Assert.Empty(moxfieldImporter.ImportedDeckIds);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
        Assert.Null((await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldListingInvalidShape_ServesCacheAndDoesNotRestampFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield", now.AddDays(-2));
        await SeedCacheAsync(harness, "cached-1", now);
        var handler = new StubTimestampingListingHandler("{\"totalPages\":1}");
        var pacer = new MoxfieldRequestPacer(TimeSpan.Zero);
        var moxfieldOwnerClient = CreateMoxfieldOwnerClient(handler, pacer);
        var ownerClient = new CountingOwnerClient { ResolvedUsername = "snail" };
        var importer = new CountingDeckImporter();
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter, pacer).CrawlAsync("snail");

        Assert.Equal("cached-1", Assert.Single(samples).DeckId);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(moxfieldImporter.ImportedDeckIds);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
        Assert.Equal(now.AddDays(-2), (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldCachedDeck_ReusesCacheWithoutImport()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        await SeedCacheAsync(harness, "mox-1", now);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-1")] };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        Assert.Equal("mox-1", Assert.Single(samples).DeckId);
        Assert.Equal(1, moxfieldOwnerClient.ListCalls);
        Assert.Empty(moxfieldImporter.ImportedDeckIds);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldImportTimeout_ServesCacheAndDoesNotRestampFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var lastCrawledUtc = now.AddDays(-2);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield", lastCrawledUtc);
        await SeedCacheAsync(harness, "mox-timeout", now);
        await SeedCacheAsync(harness, "stale", now);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-timeout")] };
        var samples = await CreateCrawler(
                harness,
                new CountingOwnerClient(),
                new CountingDeckImporter(),
                now,
                moxfieldOwnerClient,
                new NeverCompletingMoxfieldDeckImporter(),
                moxfieldImportTimeout: TimeSpan.FromMilliseconds(20))
            .CrawlAsync("snail", forceRefresh: true)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("mox-timeout", Assert.Single(samples).DeckId);
        Assert.Equal(2, (await harness.CacheStore.GetByCreatorAsync("snail")).Count);
        Assert.Equal(lastCrawledUtc, (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldSpellbookFallback_ServesFilteredCacheAndDoesNotRestampFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var lastCrawledUtc = now.AddDays(-2);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield", lastCrawledUtc);
        await SeedCacheAsync(harness, "mox-fallback", now);
        await SeedCacheAsync(harness, "stale", now);
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["mox-fallback"] = [MainboardEntry("Sol Ring")];
        moxfieldImporter.FallbackDeckIds.Add("mox-fallback");
        var samples = await CreateCrawler(
                harness,
                new CountingOwnerClient(),
                new CountingDeckImporter(),
                now,
                new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-fallback")] },
                moxfieldImporter)
            .CrawlAsync("snail", forceRefresh: true);

        Assert.Equal("mox-fallback", Assert.Single(samples).DeckId);
        Assert.Equal(2, (await harness.CacheStore.GetByCreatorAsync("snail")).Count);
        Assert.Equal(lastCrawledUtc, (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldHttpCancellationAfterImportTimeout_ServesFilteredCacheWithoutRestamp()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        var lastCrawledUtc = now.AddDays(-2);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield", lastCrawledUtc);
        await SeedCacheAsync(harness, "mox-timeout", now);
        await SeedCacheAsync(harness, "stale", now);
        var ownerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-timeout")] };

        var samples = await CreateCrawler(
                harness,
                new CountingOwnerClient(),
                new CountingDeckImporter(),
                now,
                ownerClient,
                new HttpCancellationMoxfieldDeckImporter(),
                moxfieldImportTimeout: TimeSpan.FromMilliseconds(20))
            .CrawlAsync("snail", forceRefresh: true)
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("mox-timeout", Assert.Single(samples).DeckId);
        Assert.Equal(2, (await harness.CacheStore.GetByCreatorAsync("snail")).Count);
        Assert.Equal(lastCrawledUtc, (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldOversizedDeck_IsSkippedAndNotCached()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        await SeedCacheAsync(harness, "mox-big", now);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-big"), MoxfieldSummary("mox-1")] };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["mox-big"] = [new DeckEntry { Name = "Rat Colony", NormalizedName = "rat colony", Quantity = 106, Board = "mainboard" }];
        moxfieldImporter.Decks["mox-1"] = [MainboardEntry("Sol Ring"), CommanderEntry("Atraxa")];
        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail", forceRefresh: true);

        Assert.Equal(1, moxfieldOwnerClient.ListCalls);
        Assert.Equal(["mox-big", "mox-1"], moxfieldImporter.ImportedDeckIds);
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
        Assert.Equal("mox-1", Assert.Single(samples).DeckId);
        var cacheEntries = await harness.CacheStore.GetByCreatorAsync("snail");
        Assert.DoesNotContain(cacheEntries, entry => entry.DeckId == "mox-big");
        Assert.Contains(cacheEntries, entry => entry.DeckId == "mox-1");
        Assert.Equal(now, (await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldCompleteListing_PrunesUnlistedCacheRows()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        await SeedCacheAsync(harness, "mox-1", now);
        await SeedCacheAsync(harness, "stale", now);
        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-1")] };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        Assert.Equal(["mox-1"], samples.Select(sample => sample.DeckId));
        Assert.Equal(["mox-1"], (await harness.CacheStore.GetByCreatorAsync("snail")).Select(entry => entry.DeckId));
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldTruncatedListing_DoesNotPruneUnlistedCacheRows()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        await SeedCacheAsync(harness, "mox-1", now);
        await SeedCacheAsync(harness, "stale", now);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient
        {
            Result = new MoxfieldDeckListResult { Decks = [MoxfieldSummary("mox-1")], HasUpstreamFailure = false, IsTruncated = true },
        };

        var samples = await CreateCrawler(harness, new CountingOwnerClient(), new CountingDeckImporter(), now, moxfieldOwnerClient, new CountingMoxfieldDeckImporter())
            .CrawlAsync("snail");

        Assert.Equal(["mox-1"], samples.Select(sample => sample.DeckId));
        Assert.Equal(["mox-1", "stale"], (await harness.CacheStore.GetByCreatorAsync("snail")).Select(entry => entry.DeckId).Order());
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldDeckFetchBlocked_StopsCrawlServesCacheAndDoesNotRestampFreshness()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        await SeedCacheAsync(harness, "old-1", now);
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-1"), MoxfieldSummary("mox-2"), MoxfieldSummary("mox-3")] };
        var moxfieldImporter = new CountingMoxfieldDeckImporter { FallbackDeckIds = ["mox-2"] };
        moxfieldImporter.Decks["mox-1"] = [MainboardEntry("Sol Ring")];
        moxfieldImporter.Decks["mox-2"] = [MainboardEntry("Arcane Signet")];
        moxfieldImporter.Decks["mox-3"] = [MainboardEntry("Mana Crypt")];
        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();

        var samples = await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter).CrawlAsync("snail");

        Assert.Equal(["mox-1", "mox-2"], moxfieldImporter.ImportedDeckIds);
        Assert.Equal(["mox-1"], samples.Select(sample => sample.DeckId));
        var cacheEntries = await harness.CacheStore.GetByCreatorAsync("snail");
        Assert.Contains(cacheEntries, entry => entry.DeckId == "mox-1");
        Assert.Contains(cacheEntries, entry => entry.DeckId == "old-1");
        Assert.DoesNotContain(cacheEntries, entry => entry.DeckId is "mox-2" or "mox-3");
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
        Assert.Null((await harness.ProfileStore.GetBySlugAsync("snail"))!.LastCrawledUtc);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldImports_ArePacedThroughSharedPacer()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        var moxfieldOwnerClient = new CountingMoxfieldOwnerClient { Decks = [MoxfieldSummary("mox-1"), MoxfieldSummary("mox-2")] };
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["mox-1"] = [MainboardEntry("Sol Ring")];
        moxfieldImporter.Decks["mox-2"] = [MainboardEntry("Mana Crypt")];
        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();

        await CreateCrawler(harness, ownerClient, importer, now, moxfieldOwnerClient, moxfieldImporter, new MoxfieldRequestPacer(TimeSpan.FromMilliseconds(150))).CrawlAsync("snail");

        Assert.Equal(2, moxfieldImporter.ImportTimestamps.Count);
        Assert.True(moxfieldImporter.ImportTimestamps[1] - moxfieldImporter.ImportTimestamps[0] >= TimeSpan.FromMilliseconds(120));
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
    }

    [Fact]
    public async Task CrawlAsync_MoxfieldListingThenImport_SharesOnePacerAcrossClientAndCrawler()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await CreateHarnessAsync();
        await SeedSourceAsync(harness.ProfileStore, now, "moxfield");
        var pacer = new MoxfieldRequestPacer(TimeSpan.FromMilliseconds(150));
        var handler = new StubTimestampingListingHandler("{\"totalPages\":1,\"data\":[{\"publicId\":\"mox-1\",\"name\":\"Deck\",\"format\":\"commander\",\"visibility\":\"public\"}]}");
        var moxfieldImporter = new CountingMoxfieldDeckImporter();
        moxfieldImporter.Decks["mox-1"] = [MainboardEntry("Sol Ring")];
        var ownerClient = new CountingOwnerClient();
        var importer = new CountingDeckImporter();

        await CreateCrawler(harness, ownerClient, importer, now, CreateMoxfieldOwnerClient(handler, pacer), moxfieldImporter, pacer)
            .CrawlAsync("snail")
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Single(moxfieldImporter.ImportTimestamps);
        Assert.True(moxfieldImporter.ImportTimestamps[0] - handler.ResponseTimestamps.Single() >= TimeSpan.FromMilliseconds(120));
        Assert.Equal(0, ownerClient.ResolveUsernameCalls + ownerClient.ListDeckSummariesCalls + importer.ImportCalls);
    }

    private static CreatorProfileDeckCrawler CreateCrawler(
        TestHarness harness,
        CountingOwnerClient ownerClient,
        CountingDeckImporter importer,
        DateTimeOffset now,
        IMoxfieldOwnerClient? moxfieldOwnerClient = null,
        IMoxfieldDeckImporter? moxfieldImporter = null,
        MoxfieldRequestPacer? moxfieldPacer = null,
        CapturingLogger<CreatorProfileDeckCrawler>? logger = null,
        TimeSpan? moxfieldImportTimeout = null)
    {
        return new CreatorProfileDeckCrawler(
            ownerClient,
            importer,
            moxfieldOwnerClient ?? new EmptyMoxfieldOwnerClient(),
            moxfieldImporter ?? new EmptyMoxfieldDeckImporter(),
            moxfieldPacer ?? new MoxfieldRequestPacer(TimeSpan.Zero),
            harness.ProfileStore,
            harness.CacheStore,
            logger,
            freshnessWindow: TimeSpan.FromHours(1),
            nowUtc: () => now,
            moxfieldImportTimeout: moxfieldImportTimeout ?? TimeSpan.FromSeconds(60));
    }

    private static async Task SeedSourceAsync(
        ICreatorProfileSourceStore profileStore,
        DateTimeOffset now,
        string platform = "archidekt",
        DateTimeOffset? lastCrawledUtc = null)
    {
        await profileStore.UpsertAsync(new CreatorProfileSource
        {
            Slug = "snail",
            Platform = platform,
            ProfileUsername = "snail",
            FolderWeights = new Dictionary<int, double> { [1] = 1.0 },
            LastCrawledUtc = lastCrawledUtc,
            UpdatedUtc = now
        });
    }

    private static Task SeedCacheAsync(TestHarness harness, string deckId, DateTimeOffset now)
    {
        return harness.CacheStore.UpsertAsync(new CreatorDeckCacheEntry
        {
            CreatorSlug = "snail",
            DeckId = deckId,
            ContentHash = $"hash-{deckId}",
            Size = 1,
            ConfidenceMarker = "cached",
            Entries = [MainboardEntry("Sol Ring")],
            CachedUtc = now
        });
    }

    private static MoxfieldDeckSummary MoxfieldSummary(string publicId)
    {
        return new MoxfieldDeckSummary
        {
            PublicId = publicId,
            Name = publicId,
            Format = "commander",
            Visibility = "public"
        };
    }

    private static bool HasState(IReadOnlyList<KeyValuePair<string, object?>> entry, string key, string value)
    {
        return entry.Any(pair => pair.Key == key && string.Equals(pair.Value?.ToString(), value, StringComparison.Ordinal));
    }

    private static MoxfieldOwnerClient CreateMoxfieldOwnerClient(StubTimestampingListingHandler handler, MoxfieldRequestPacer pacer)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api2.moxfield.com/") };
        return new MoxfieldOwnerClient(new FakeResiliencePipelineProvider(), new RestClient(httpClient), pacer);
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

    private sealed class EmptyMoxfieldOwnerClient : IMoxfieldOwnerClient
    {
        public Task<MoxfieldDeckListResult> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(new MoxfieldDeckListResult
            {
                Decks = [],
                HasUpstreamFailure = false,
            });
    }

    private sealed class EmptyMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult<List<DeckEntry>>([]);

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
            => Task.FromResult(new MoxfieldImportResult([], MoxfieldImportSource.Direct));
    }

    private sealed class CountingMoxfieldOwnerClient : IMoxfieldOwnerClient
    {
        public IReadOnlyList<MoxfieldDeckSummary> Decks { get; init; } = [];

        public MoxfieldDeckListResult? Result { get; init; }

        public int ListCalls { get; private set; }

        public string? LastListUsername { get; private set; }

        public Task<MoxfieldDeckListResult> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            LastListUsername = username;
            return Task.FromResult(Result ?? new MoxfieldDeckListResult { Decks = Decks, HasUpstreamFailure = false });
        }
    }

    private sealed class CountingMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Dictionary<string, List<DeckEntry>> Decks { get; } = new(StringComparer.Ordinal);

        public HashSet<string> FallbackDeckIds { get; init; } = new(StringComparer.Ordinal);

        public List<string> ImportedDeckIds { get; } = [];

        public List<TimeSpan> ImportTimestamps { get; } = [];

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Import(urlOrDeckId).Entries);
        }

        public Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Import(urlOrDeckId));
        }

        private MoxfieldImportResult Import(string deckId)
        {
            ImportedDeckIds.Add(deckId);
            ImportTimestamps.Add(TimeSpan.FromTicks(Stopwatch.GetTimestamp() * TimeSpan.TicksPerSecond / Stopwatch.Frequency));
            if (!Decks.TryGetValue(deckId, out var entries))
            {
                throw new InvalidOperationException($"Unknown Moxfield deck {deckId}.");
            }

            return new MoxfieldImportResult(entries, FallbackDeckIds.Contains(deckId) ? MoxfieldImportSource.CommanderSpellbookFallback : MoxfieldImportSource.Direct);
        }
    }

    private sealed class NeverCompletingMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ContinueWith(_ => new List<DeckEntry>(), TaskScheduler.Default);

        public async Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Cancellation must interrupt the import.");
        }
    }

    private sealed class HttpCancellationMoxfieldDeckImporter : IMoxfieldDeckImporter
    {
        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default) => Task.FromResult<List<DeckEntry>>([]);

        public async Task<MoxfieldImportResult> ImportWithSourceAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The real RestSharp transport surfaces header-stall cancellation as HttpRequestException.
            }
            throw new HttpRequestException("Moxfield request cancelled after a header stall.");
        }
    }

    private sealed class StubTimestampingListingHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StubTimestampingListingHandler(string body)
        {
            _body = body;
        }

        public int CallCount { get; private set; }

        public List<TimeSpan> ResponseTimestamps { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            ResponseTimestamps.Add(TimeSpan.FromTicks(Stopwatch.GetTimestamp() * TimeSpan.TicksPerSecond / Stopwatch.Frequency));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_body) });
        }
    }

    private sealed class CountingOwnerClient : IArchidektOwnerClient
    {
        public string? ResolvedUsername { get; init; }

        public IReadOnlyList<ArchidektDeckSummary> DeckSummaries { get; init; } = [];

        public ArchidektDeckListResult? DeckListResult { get; init; }

        public int ResolveUsernameCalls { get; private set; }

        public int ListDeckSummariesCalls { get; private set; }

        public string? LastListUsername { get; private set; }

        public Task<string?> ResolveUsernameAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
        {
            ResolveUsernameCalls++;
            return Task.FromResult(ResolvedUsername);
        }

        public Task<ArchidektDeckListResult> ListDeckSummariesAsync(string ownerUsername, CancellationToken cancellationToken = default)
        {
            ListDeckSummariesCalls++;
            LastListUsername = ownerUsername;
            return Task.FromResult(DeckListResult ?? new ArchidektDeckListResult
            {
                Decks = DeckSummaries,
                HasUpstreamFailure = false
            });
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

    private sealed class TestHarness(string directory, CreatorProfileSourceStore profileStore, CreatorDeckCacheStore cacheStore) : IAsyncDisposable
    {
        public string Directory { get; } = directory;

        public CreatorProfileSourceStore ProfileStore { get; } = profileStore;

        public CreatorDeckCacheStore CacheStore { get; } = cacheStore;

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
