using System.Net;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Data.Sqlite;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests.Services.CreatorStyle;

/// <summary>
/// Automated fixture coverage for the measured-style extractor.
/// The reduced Snail seed corpus below is automated and deterministic; the live 39-deck crawl is manual-only.
/// </summary>
public sealed class MeasuredStyleProfileBuilderTests
{
    [Fact]
    public async Task BuildAsync_PersistsProfile_RoundTripsMetricsAndHandlesNullComboGracefully()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            "builder-general",
            "builder-general",
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();

        var comboService = new FakeCommanderSpellbookService(new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)
        {
            ["builder-general-current-1"] = new CommanderSpellbookResult(
                [new SpellbookCombo(["Viscera Seer"], ["Loop"], "Loop line"), new SpellbookCombo(["Skullclamp"], ["Cards"], "Clamp line")],
                []),
            ["builder-general-current-2"] = null
        });
        var builder = harness.CreateBuilder(comboService);

        var profile = await builder.BuildAsync("builder-general", SnailSeedCorpusFixture.Platform);
        var stored = await harness.ProfileStore.GetBySlugAsync("builder-general");

        Assert.NotNull(stored);
        Assert.Equal(profile.Slug, stored!.Slug);
        Assert.Equal(profile.Platform, stored.Platform);
        Assert.Equal(profile.MinDecks, stored.MinDecks);
        Assert.Equal(profile.InsufficientSample, stored.InsufficientSample);
        Assert.Equal(profile.UpdatedUtc, stored.UpdatedUtc);
        Assert.True(profile.MeasuredMetrics.SequenceEqual(stored.MeasuredMetrics));
        Assert.Equal(SnailSeedCorpusFixture.Samples.Count, stored.MinDecks);
        Assert.False(stored.InsufficientSample);
        Assert.NotEmpty(stored.MeasuredMetrics);
        Assert.All(stored.MeasuredMetrics.Where(metric => !metric.Metric.StartsWith("lift:", StringComparison.Ordinal)), metric =>
        {
            Assert.Equal(SnailSeedCorpusFixture.Samples.Count, metric.NumDecks);
            Assert.NotNull(metric.Distribution);
            Assert.NotNull(metric.Distribution!.EffectiveSampleSize);
            Assert.Equal(5.0, metric.Distribution.EffectiveSampleSize!.Value);
        });

        var comboMetric = Assert.Single(stored.MeasuredMetrics, metric => metric.Metric == "combo_density:included_per_deck");
        // 0.4 is weighted by FolderWeight with a weight sum of 5.0, not 2 / 6.
        Assert.Equal(0.4, comboMetric.Value, 6);

        var liftMetric = stored.MeasuredMetrics.FirstOrDefault(metric => metric.Metric.StartsWith("lift:", StringComparison.Ordinal));
        Assert.NotNull(liftMetric);
        Assert.Null(liftMetric.Distribution);
        Assert.Contains(stored.MeasuredMetrics, metric => metric.Metric == "category_ratio:ramp");
    }

    [Fact]
    public async Task BuildAsync_WeightsUncuratedFlagIsTrue_ForcesAllSampleWeightsToOneRegardlessOfFolderWeights()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples,
            weightsUncurated: true);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();

        var builder = harness.CreateBuilder(new FakeCommanderSpellbookService(
            new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)));

        var profile = await builder.BuildAsync(SnailSeedCorpusFixture.CreatorSlug, SnailSeedCorpusFixture.Platform);

        Assert.All(profile.MeasuredMetrics.Where(metric => !metric.Metric.StartsWith("lift:", StringComparison.Ordinal)), metric =>
            Assert.Equal((double)SnailSeedCorpusFixture.Samples.Count, metric.Distribution!.EffectiveSampleSize));
    }

    [Fact]
    public async Task BuildAsync_RangeStatedRule_PreservesBandBoundsInFusedTarget()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            "builder-fused",
            "builder-fused",
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();
        var statedRule = new StatedRule
        {
            Category = "mana",
            TargetMetric = "land_count",
            TargetValue = null,
            TargetValueMin = 13,
            TargetValueMax = 18,
            Comparator = "range",
            SourceClip = "Play between thirteen and eighteen lands.",
            Confidence = 0.90,
            VideoDateUtc = now.AddDays(-1)
        };
        await harness.ProfileStore.UpsertAsync(new CreatorStyleProfile
        {
            Slug = "builder-fused",
            Platform = SnailSeedCorpusFixture.Platform,
            MinDecks = 0,
            StatedRules = [statedRule],
            MeasuredMetrics = Array.Empty<MeasuredMetric>(),
            FusedTargets = Array.Empty<FusedTarget>(),
            UpdatedUtc = now
        });

        CreatorStyleProfile profile = await harness.CreateBuilder(new FakeCommanderSpellbookService(
            new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal))).BuildAsync(
            "builder-fused",
            SnailSeedCorpusFixture.Platform);

        Assert.Equal([statedRule], profile.StatedRules);
        FusedTarget target = Assert.Single(profile.FusedTargets);
        Assert.Equal("land_count", target.Metric);
        Assert.Equal(13, target.StatedMin);
        Assert.Equal(18, target.StatedMax);
    }

    [Fact]
    public async Task BuildAsync_StatedRulesWithDifferentVideoDates_UsesNewestRuleAsActiveTarget()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            "builder-recency",
            "builder-recency",
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();
        var older = new StatedRule
        {
            Category = "mana",
            TargetMetric = "land_count",
            TargetValue = 30,
            Comparator = "gte",
            SourceClip = "Older advice.",
            Confidence = 0.90,
            VideoDateUtc = now.AddDays(-2)
        };
        var newer = new StatedRule
        {
            Category = "mana",
            TargetMetric = "land_count",
            TargetValue = 35,
            Comparator = "gte",
            SourceClip = "Newer advice.",
            Confidence = 0.90,
            VideoDateUtc = now.AddDays(-1)
        };
        await harness.ProfileStore.UpsertAsync(new CreatorStyleProfile
        {
            Slug = "builder-recency",
            Platform = SnailSeedCorpusFixture.Platform,
            MinDecks = 0,
            StatedRules = [older, newer],
            MeasuredMetrics = Array.Empty<MeasuredMetric>(),
            FusedTargets = Array.Empty<FusedTarget>(),
            UpdatedUtc = now
        });

        CreatorStyleProfile profile = await harness.CreateBuilder(new FakeCommanderSpellbookService(
            new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal))).BuildAsync(
            "builder-recency", SnailSeedCorpusFixture.Platform);

        FusedTarget active = Assert.Single(profile.FusedTargets, target => target.Source != "stated-superseded");
        Assert.Equal(35, active.StatedMin);
        Assert.Equal(newer.VideoDateUtc, active.VideoDateUtc);
        Assert.Contains(profile.FusedTargets, target => target.Source == "stated-superseded" && target.StatedMin == 30);
    }

    [Fact]
    public async Task BuildAsync_MultipleDecks_BoundsConcurrentComboLookups()
    {
        // Why (WR-09): BuildComboDensityMetricAsync used to Task.WhenAll every deck's combo lookup
        // with no cap, so a large creator could fan out hundreds of concurrent Scryfall-backed
        // calls. This pins the MaxConcurrentDeckAnalyses bound against the 6-deck Snail corpus.
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.Username,
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();

        var comboService = new ConcurrencyTrackingCommanderSpellbookService();
        var builder = harness.CreateBuilder(comboService);

        await builder.BuildAsync(SnailSeedCorpusFixture.CreatorSlug, SnailSeedCorpusFixture.Platform);

        Assert.True(
            comboService.PeakConcurrency <= 4,
            $"Expected combo lookups to be bounded to 4 concurrent callers but observed {comboService.PeakConcurrency}.");
        Assert.True(comboService.PeakConcurrency > 1, "Expected some overlap so the assertion is not trivially true.");
    }

    [Fact]
    public async Task MeasuredStyleProfileBuilder_SnailSeedCorpus_ExtractorInvariants()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.Username,
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedSourceAsync(
            "snail-seed-thin",
            "snail-seed-thin",
            SnailSeedCorpusFixture.DeckSummaries.Take(4).ToArray(),
            SnailSeedCorpusFixture.BelowMinFloorSubset);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();

        var builder = harness.CreateBuilder(new FakeCommanderSpellbookService(new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)
        {
            ["snail-seed-current-1"] = new CommanderSpellbookResult(
                [new SpellbookCombo(["Viscera Seer"], ["Loop"], "Loop line")],
                []),
            ["snail-seed-budget-1"] = null
        }));

        var strippedSamples = StapleStripper.StripStaples(
            StapleStripper.FlagNearPrecons(StapleStripper.FilterOversized(SnailSeedCorpusFixture.Samples)),
            StapleStripper.ComputePersonalStaples(SnailSeedCorpusFixture.Samples));
        var profile = await builder.BuildAsync(SnailSeedCorpusFixture.CreatorSlug, SnailSeedCorpusFixture.Platform);
        var thinProfile = await builder.BuildAsync("snail-seed-thin", SnailSeedCorpusFixture.Platform);

        Assert.All(strippedSamples, sample =>
        {
            Assert.DoesNotContain(sample.Entries, entry => string.Equals(entry.Name, "Sol Ring", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(sample.Entries, entry => string.Equals(entry.Name, "Command Tower", StringComparison.OrdinalIgnoreCase));
        });

        Assert.All(profile.MeasuredMetrics.Where(metric => !metric.Metric.StartsWith("lift:", StringComparison.Ordinal)), metric =>
        {
            Assert.Equal(SnailSeedCorpusFixture.Samples.Count, metric.NumDecks);
            Assert.NotNull(metric.Distribution);
            Assert.NotNull(metric.Distribution!.EffectiveSampleSize);
        });

        double staplePairLift = Assert.Single(profile.MeasuredMetrics, metric => metric.Metric == "lift:draw|ramp").Value;
        double discriminatingPairLift = Assert.Single(profile.MeasuredMetrics, metric => metric.Metric == "lift:blink|tokens").Value;
        Assert.True(discriminatingPairLift > staplePairLift);

        Assert.All(profile.MeasuredMetrics.Where(metric => !metric.Metric.StartsWith("lift:", StringComparison.Ordinal)), metric => Assert.Equal(5.0, metric.Distribution!.EffectiveSampleSize));
        Assert.True(thinProfile.InsufficientSample);
    }

    [Fact]
    public async Task BuildAsync_DelegatesToBuildDetailedAsync_ReturningTheSameProfile()
    {
        // Why: BuildAsync must be a one-line delegation to BuildDetailedAsync so the two can never
        // drift. Seed two slugs with byte-identical corpora and compare the profile each entry
        // point produces for its own slug (ignoring the Slug field itself).
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync("delegate-direct", "delegate-direct", SnailSeedCorpusFixture.DeckSummaries, SnailSeedCorpusFixture.Samples);
        await harness.SeedSourceAsync("delegate-detailed", "delegate-detailed", SnailSeedCorpusFixture.DeckSummaries, SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();
        var builder = harness.CreateBuilder(new FakeCommanderSpellbookService(new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)));

        CreatorStyleProfile viaBuildAsync = await builder.BuildAsync("delegate-direct", SnailSeedCorpusFixture.Platform);
        MeasuredStyleBuildResult viaDetailed = await builder.BuildDetailedAsync("delegate-detailed", SnailSeedCorpusFixture.Platform);

        Assert.Equal(viaBuildAsync.Platform, viaDetailed.Profile.Platform);
        Assert.Equal(viaBuildAsync.MinDecks, viaDetailed.Profile.MinDecks);
        Assert.Equal(viaBuildAsync.InsufficientSample, viaDetailed.Profile.InsufficientSample);
        Assert.Equal(viaBuildAsync.UpdatedUtc, viaDetailed.Profile.UpdatedUtc);
        Assert.True(viaBuildAsync.MeasuredMetrics.SequenceEqual(viaDetailed.Profile.MeasuredMetrics));
    }

    [Fact]
    public async Task BuildDetailedAsync_ExposesFilteredSamplesAndResolvedCardCategoryMap()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.Username,
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();
        var builder = harness.CreateBuilder(new FakeCommanderSpellbookService(new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)));

        MeasuredStyleBuildResult result = await builder.BuildDetailedAsync(SnailSeedCorpusFixture.CreatorSlug, SnailSeedCorpusFixture.Platform);

        // FilterOversized-only output: none of the fixture decks exceed the 105-card cap, so every
        // sample survives, and curated staples (Sol Ring) are still present because staple
        // stripping happens after this stage, not before it.
        // Note: SeedSourceAsync rewrites deck ids by substituting "snail" with the slug, so the
        // stored/returned ids carry the slug's own name ("snail-seed-current-1", not
        // "snail-current-1").
        Assert.Equal(SnailSeedCorpusFixture.Samples.Count, result.Samples.Count);
        var expectedDeckIds = SnailSeedCorpusFixture.Samples
            .Select(sample => sample.DeckId.Replace("snail", SnailSeedCorpusFixture.CreatorSlug, StringComparison.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal);
        Assert.Equal(expectedDeckIds, result.Samples.Select(sample => sample.DeckId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(result.Samples, sample =>
            Assert.Contains(sample.Entries, entry => string.Equals(entry.Name, "Sol Ring", StringComparison.OrdinalIgnoreCase)));

        Assert.True(result.CardCategories.TryGetValue("Arcane Signet", out var arcaneSignetCategories));
        Assert.Contains("ramp", arcaneSignetCategories!, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildDetailedAsync_ExposesTheExactLoadedBaselineInstance()
    {
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.Username,
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();
        var builder = harness.CreateBuilder(new FakeCommanderSpellbookService(new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)));

        MeasuredStyleBuildResult result = await builder.BuildDetailedAsync(SnailSeedCorpusFixture.CreatorSlug, SnailSeedCorpusFixture.Platform);

        // Distinctive values pinned to the SeedBaselineAsync fixture (10 processed decks; "ramp"
        // appears on baseline-1/2/3/4/7 = 5 of them) so this fails if the field is wired to a
        // default/empty baseline or to some other query's result instead of the real load.
        Assert.Equal(10, result.Baseline.TotalDecks);
        Assert.True(result.Baseline.DecksWithCategory.TryGetValue("ramp", out var rampDeckCount));
        Assert.Equal(5, rampDeckCount);
    }

    [Fact]
    public async Task BuildDetailedAsync_RunsComboDensityThenKarstenMetricsSequentially_NeverConcurrently()
    {
        // Why (WR-03 regression guard): asserts an OBSERVABLE sequencing fact via blocking/tracking
        // test doubles, not just the WR-03 comment's presence — a Task.WhenAll-style concurrent
        // start over both metric builds must fail this fact even if the comment text survives.
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync(
            SnailSeedCorpusFixture.CreatorSlug,
            SnailSeedCorpusFixture.Username,
            SnailSeedCorpusFixture.DeckSummaries,
            SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();
        await harness.SeedBaselineAsync();

        var log = new List<string>();
        var lockObject = new object();
        var comboService = new SequencingTrackingCommanderSpellbookService(log, lockObject);
        var scryfallResolver = new SequencingTrackingScryfallCardResolver(log, lockObject, TestHarness.BuildScryfallCardMap());
        var builder = harness.CreateBuilder(comboService, scryfallResolverOverride: scryfallResolver);

        await builder.BuildDetailedAsync(SnailSeedCorpusFixture.CreatorSlug, SnailSeedCorpusFixture.Platform);

        Assert.Contains("combo", log);
        Assert.Contains("karsten", log);
        int lastComboIndex = log.LastIndexOf("combo");
        int firstKarstenIndex = log.IndexOf("karsten");
        Assert.True(
            firstKarstenIndex > lastComboIndex,
            $"Expected every combo-density call to complete before the first Karsten call started, " +
            $"but observed order [{string.Join(", ", log)}] (last combo at {lastComboIndex}, first karsten at {firstKarstenIndex}).");
    }

    [Fact]
    public async Task BuildAsync_AndBuildDetailedAsync_WhenBaselineLoadThrows_PropagateSameExceptionAndPersistNoProfile()
    {
        // Why: a reintroduced catch-and-continue path around the baseline load (substituting an
        // empty/default baseline, as the source branch did) must fail this fact.
        var now = new DateTimeOffset(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);
        await using var harness = await TestHarness.CreateAsync(now);
        await harness.SeedSourceAsync("throws-direct", "throws-direct", SnailSeedCorpusFixture.DeckSummaries, SnailSeedCorpusFixture.Samples);
        await harness.SeedSourceAsync("throws-detailed", "throws-detailed", SnailSeedCorpusFixture.DeckSummaries, SnailSeedCorpusFixture.Samples);
        await harness.SeedCategoriesAsync();

        // Deliberately corrupt (not merely missing) so this fails the same way in every
        // environment, regardless of filesystem permissions: SQLite refuses to open a non-empty
        // file that isn't a valid database, independent of OS write access.
        string corruptDatabasePath = Path.Combine(harness.Directory, "corrupt-baseline.sqlite");
        await File.WriteAllTextAsync(corruptDatabasePath, "deliberately not a sqlite database");
        var throwingBaselineRepository = new CategoryKnowledgeRepository(corruptDatabasePath);

        var builder = harness.CreateBuilder(
            new FakeCommanderSpellbookService(new Dictionary<string, CommanderSpellbookResult?>(StringComparer.Ordinal)),
            baselineRepositoryOverride: throwingBaselineRepository);

        await Assert.ThrowsAnyAsync<Exception>(() => builder.BuildAsync("throws-direct", SnailSeedCorpusFixture.Platform));
        await Assert.ThrowsAnyAsync<Exception>(() => builder.BuildDetailedAsync("throws-detailed", SnailSeedCorpusFixture.Platform));

        Assert.Null(await harness.ProfileStore.GetBySlugAsync("throws-direct"));
        Assert.Null(await harness.ProfileStore.GetBySlugAsync("throws-detailed"));
    }

    private sealed class SequencingTrackingCommanderSpellbookService : ICommanderSpellbookService
    {
        private readonly List<string> _log;
        private readonly object _lock;

        public SequencingTrackingCommanderSpellbookService(List<string> log, object lockObject)
        {
            _log = log;
            _lock = lockObject;
        }

        public async Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _log.Add("combo");
            }

            // Why: a short delay widens the window in which a wrongly-concurrent implementation
            // would interleave a "karsten" log entry before every "combo" entry completes.
            await Task.Delay(TimeSpan.FromMilliseconds(15), cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private sealed class SequencingTrackingScryfallCardResolver : IScryfallCardResolver
    {
        private readonly List<string> _log;
        private readonly object _lock;
        private readonly IReadOnlyDictionary<string, ScryfallCard> _cardsByName;

        public SequencingTrackingScryfallCardResolver(List<string> log, object lockObject, IReadOnlyDictionary<string, ScryfallCard> cardsByName)
        {
            _log = log;
            _lock = lockObject;
            _cardsByName = cardsByName;
        }

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _log.Add("karsten");
            }

            return Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(_cardsByName.Values.ToList(), null)
            });
        }

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(_cardsByName.TryGetValue(cardName, out var card) ? card : null);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);
    }

    private sealed class TestHarness : IAsyncDisposable
    {
        private readonly Dictionary<string, IReadOnlyList<ArchidektDeckSummary>> _deckSummariesByUsername = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<DeckEntry>> _decksById = new(StringComparer.Ordinal);

        private TestHarness(
            string directory,
            CreatorProfileSourceStore sourceStore,
            CreatorDeckCacheStore cacheStore,
            CreatorStyleProfileStore profileStore,
            CategoryKnowledgeRepository categoryKnowledgeRepository,
            DateTimeOffset now)
        {
            Directory = directory;
            SourceStore = sourceStore;
            CacheStore = cacheStore;
            ProfileStore = profileStore;
            CategoryKnowledgeRepository = categoryKnowledgeRepository;
            Now = now;
        }

        public string Directory { get; }

        public DateTimeOffset Now { get; }

        public CreatorProfileSourceStore SourceStore { get; }

        public CreatorDeckCacheStore CacheStore { get; }

        public CreatorStyleProfileStore ProfileStore { get; }

        public CategoryKnowledgeRepository CategoryKnowledgeRepository { get; }

        public static Task<TestHarness> CreateAsync(DateTimeOffset now)
        {
            var directory = Path.Combine(Path.GetTempPath(), "deckflow-95-07-tests", Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var creatorDb = Path.Combine(directory, "creator-style.sqlite");
            var knowledgeDb = Path.Combine(directory, "category-knowledge.sqlite");
            return Task.FromResult(new TestHarness(
                directory,
                new CreatorProfileSourceStore(creatorDb),
                new CreatorDeckCacheStore(creatorDb),
                new CreatorStyleProfileStore(creatorDb),
                new CategoryKnowledgeRepository(RelationalDatabaseConnection.FromSqlitePath(knowledgeDb)),
                now));
        }

        public async Task SeedSourceAsync(
            string slug,
            string username,
            IReadOnlyList<ArchidektDeckSummary> summaries,
            IReadOnlyList<CreatorDeckSample> samples,
            bool weightsUncurated = false)
        {
            _deckSummariesByUsername[username] = summaries
                .Select(summary => summary with { Id = summary.Id.Replace("snail", slug, StringComparison.Ordinal) })
                .ToArray();

            foreach (CreatorDeckSample sample in samples)
            {
                string deckId = sample.DeckId.Replace("snail", slug, StringComparison.Ordinal);
                _decksById[deckId] = sample.Entries.ToList();
            }

            await SourceStore.UpsertAsync(new CreatorProfileSource
            {
                Slug = slug,
                Platform = SnailSeedCorpusFixture.Platform,
                ProfileUsername = username,
                FolderWeights = SnailSeedCorpusFixture.FolderWeights,
                WeightsUncurated = weightsUncurated,
                UpdatedUtc = Now
            });
        }

        public async Task SeedCategoriesAsync()
        {
            foreach ((string cardName, IReadOnlyList<string> categories) in CardCategoryMap)
            {
                await CategoryKnowledgeRepository.PersistObservedCategoriesAsync("fixture-categories", cardName, categories);
            }
        }

        public async Task SeedBaselineAsync()
        {
            await SeedProcessedDeckAsync("baseline-1", "Commander One", ["ramp", "draw"]);
            await SeedProcessedDeckAsync("baseline-2", "Commander Two", ["ramp", "draw"]);
            await SeedProcessedDeckAsync("baseline-3", "Commander Three", ["ramp", "removal"]);
            await SeedProcessedDeckAsync("baseline-4", "Commander Four", ["ramp", "removal"]);
            await SeedProcessedDeckAsync("baseline-5", "Commander Five", ["draw", "removal"]);
            await SeedProcessedDeckAsync("baseline-6", "Commander Six", ["blink", "tokens"]);
            await SeedProcessedDeckAsync("baseline-7", "Commander Seven", ["ramp"]);
            await SeedProcessedDeckAsync("baseline-8", "Commander Eight", ["draw"]);
            await SeedProcessedDeckAsync("baseline-9", "Commander Nine", ["blink"]);
            await SeedProcessedDeckAsync("baseline-10", "Commander Ten", ["tokens"]);
        }

        public MeasuredStyleProfileBuilder CreateBuilder(
            ICommanderSpellbookService comboService,
            CategoryKnowledgeRepository? baselineRepositoryOverride = null,
            IScryfallCardResolver? scryfallResolverOverride = null)
        {
            var ownerClient = new FakeOwnerClient(_deckSummariesByUsername);
            var importer = new FakeDeckImporter(_decksById);
            var crawler = new CreatorProfileDeckCrawler(
                ownerClient,
                importer,
                SourceStore,
                CacheStore,
                freshnessWindow: TimeSpan.Zero,
                nowUtc: () => Now);
            var tagger = new FakeTaggerLookupService(new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Bident of Thassa"] = ["draw"],
                ["Reconnaissance Mission"] = ["draw"],
                ["Young Pyromancer"] = ["tokens"]
            });
            var resolver = new CreatorDeckCategoryResolver(CategoryKnowledgeRepository, tagger);
            var scryfallResolver = scryfallResolverOverride ?? new FakeScryfallCardResolver(BuildScryfallCardMap());

            return new MeasuredStyleProfileBuilder(
                crawler,
                resolver,
                baselineRepositoryOverride ?? CategoryKnowledgeRepository,
                comboService,
                scryfallResolver,
                ProfileStore,
                SourceStore,
                nowUtc: () => Now,
                logger: null);
        }

        public async ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
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

            await ValueTask.CompletedTask;
        }

        private async Task SeedProcessedDeckAsync(string deckId, string commanderName, IReadOnlyList<string> categories)
        {
            await CategoryKnowledgeRepository.AddDeckIdsAsync([deckId]);
            await CategoryKnowledgeRepository.MarkDeckProcessedAsync(deckId, commanderName);
            for (var index = 0; index < categories.Count; index++)
            {
                string category = categories[index];
                await CategoryKnowledgeRepository.PersistObservedCategoriesAsync(
                    $"archidekt_live:{deckId}",
                    $"{deckId}-card-{index}",
                    [category]);
            }
        }

        internal static Dictionary<string, ScryfallCard> BuildScryfallCardMap()
        {
            var cards = new Dictionary<string, ScryfallCard>(StringComparer.OrdinalIgnoreCase);
            foreach (CreatorDeckSample sample in SnailSeedCorpusFixture.Samples)
            {
                foreach (DeckEntry entry in sample.Entries)
                {
                    if (!cards.ContainsKey(entry.Name))
                    {
                        cards[entry.Name] = CreateScryfallCard(entry.Name);
                    }
                }
            }

            return cards;
        }
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CardCategoryMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Arcane Signet"] = ["ramp"],
            ["Skullclamp"] = ["draw"],
            ["Viscera Seer"] = ["sacrifice"],
            ["Lingering Souls"] = ["tokens"],
            ["Swords to Plowshares"] = ["removal"],
            ["Rhystic Study"] = ["draw"],
            ["Restoration Angel"] = ["blink"],
            ["Ephemerate"] = ["blink"],
            ["Eternal Witness"] = ["value"],
            ["Satyr Wayfinder"] = ["value"],
            ["Cultivate"] = ["ramp"],
            ["Village Rites"] = ["draw", "sacrifice"],
            ["Fellwar Stone"] = ["ramp"],
            ["Defiant Strike"] = ["draw"],
            ["Young Pyromancer"] = ["tokens"],
            ["Bident of Thassa"] = ["draw"],
            ["Reconnaissance Mission"] = ["draw"]
        };

    private static ScryfallCard CreateScryfallCard(string name)
    {
        bool isLand = name is "Command Tower" or "Plains" or "Swamp" or "Island" or "Forest" or "Mountain";
        string manaCost = name switch
        {
            "Sol Ring" => "{1}",
            "Arcane Signet" => "{2}",
            "Skullclamp" => "{1}",
            "Viscera Seer" => "{B}",
            "Lingering Souls" => "{2}{W}",
            "Swords to Plowshares" => "{W}",
            "Rhystic Study" => "{2}{U}",
            "Restoration Angel" => "{3}{W}",
            "Ephemerate" => "{W}",
            "Eternal Witness" => "{1}{G}{G}",
            "Satyr Wayfinder" => "{1}{G}",
            "Cultivate" => "{2}{G}",
            "Village Rites" => "{B}",
            "Fellwar Stone" => "{2}",
            "Defiant Strike" => "{W}",
            "Young Pyromancer" => "{1}{R}",
            "Bident of Thassa" => "{2}{U}{U}",
            "Reconnaissance Mission" => "{2}{U}{U}",
            _ when isLand => null!,
            _ => "{3}"
        };

        string typeLine = isLand
            ? "Basic Land"
            : name switch
            {
                "Sol Ring" or "Arcane Signet" or "Skullclamp" or "Fellwar Stone" or "Bident of Thassa" => "Artifact",
                "Rhystic Study" or "Reconnaissance Mission" or "Lingering Souls" => "Enchantment",
                "Restoration Angel" or "Eternal Witness" or "Satyr Wayfinder" or "Young Pyromancer" or "Viscera Seer" => "Creature",
                _ => "Instant"
            };

        return new ScryfallCard(
            Name: name,
            ManaCost: isLand ? null : manaCost,
            TypeLine: typeLine,
            OracleText: isLand ? "{T}: Add mana." : "Fixture text.",
            Power: null,
            Toughness: null,
            Keywords: null,
            ColorIdentity: null,
            SetCode: "tst",
            SetName: "Test Set",
            CollectorNumber: Math.Abs(name.GetHashCode(StringComparison.Ordinal)).ToString(),
            CardFaces: null,
            Id: null,
            Layout: null,
            ReleasedAt: null,
            Cmc: isLand ? 0 : CountManaValue(manaCost),
            ProducedMana: isLand ? ["W"] : null,
            Rarity: "common");
    }

    private static double CountManaValue(string manaCost)
    {
        return manaCost.Count(character => character == '{');
    }

    private sealed class FakeOwnerClient : IArchidektOwnerClient
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<ArchidektDeckSummary>> _summariesByUsername;

        public FakeOwnerClient(IReadOnlyDictionary<string, IReadOnlyList<ArchidektDeckSummary>> summariesByUsername)
        {
            _summariesByUsername = summariesByUsername;
        }

        public Task<string?> ResolveUsernameAsync(string usernameOrUrl, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(usernameOrUrl);

        public Task<ArchidektDeckListResult> ListDeckSummariesAsync(string ownerUsername, CancellationToken cancellationToken = default)
            => Task.FromResult(new ArchidektDeckListResult
            {
                Decks = _summariesByUsername.TryGetValue(ownerUsername, out var summaries)
                    ? summaries
                    : Array.Empty<ArchidektDeckSummary>(),
                HasUpstreamFailure = false
            });
    }

    private sealed class FakeDeckImporter : IArchidektDeckImporter
    {
        private readonly IReadOnlyDictionary<string, List<DeckEntry>> _decksById;

        public FakeDeckImporter(IReadOnlyDictionary<string, List<DeckEntry>> decksById)
        {
            _decksById = decksById;
        }

        public Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken ct = default)
        {
            if (!_decksById.TryGetValue(urlOrDeckId, out var entries))
            {
                throw new InvalidOperationException($"Missing fake deck for {urlOrDeckId}.");
            }

            return Task.FromResult(entries);
        }
    }

    private sealed class FakeTaggerLookupService : IScryfallTaggerLookupService
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _tagsByCardName;

        public FakeTaggerLookupService(IReadOnlyDictionary<string, IReadOnlyList<string>> tagsByCardName)
        {
            _tagsByCardName = tagsByCardName;
        }

        public Task<IReadOnlyList<string>> LookupOracleTagsAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult(_tagsByCardName.TryGetValue(cardName, out var tags) ? tags : Array.Empty<string>());
    }

    private sealed class FakeCommanderSpellbookService : ICommanderSpellbookService
    {
        private readonly IReadOnlyDictionary<string, CommanderSpellbookResult?> _resultsByDeckId;

        public FakeCommanderSpellbookService(IReadOnlyDictionary<string, CommanderSpellbookResult?> resultsByDeckId)
        {
            _resultsByDeckId = resultsByDeckId;
        }

        public Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
        {
            string deckId = entries.First(entry => entry.Board == "commander").Name switch
            {
                "Teysa Karlov" when entries.Any(entry => entry.Name == "Village Rites") => "snail-seed-secondary-2",
                "Teysa Karlov" => "builder-general-current-1",
                "Brago, King Eternal" => "builder-general-current-2",
                "Feather, the Redeemed" => "snail-seed-budget-1",
                _ => entries.First(entry => entry.Board == "commander").Name
            };

            return Task.FromResult(_resultsByDeckId.TryGetValue(deckId, out var result) ? result : null);
        }
    }

    private sealed class ConcurrencyTrackingCommanderSpellbookService : ICommanderSpellbookService
    {
        private readonly object _lock = new();
        private int _current;
        private int _peak;

        public int PeakConcurrency
        {
            get
            {
                lock (_lock)
                {
                    return _peak;
                }
            }
        }

        public async Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _current++;
                if (_current > _peak)
                {
                    _peak = _current;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
                return null;
            }
            finally
            {
                lock (_lock)
                {
                    _current--;
                }
            }
        }
    }

    private sealed class FakeScryfallCardResolver : IScryfallCardResolver
    {
        private readonly IReadOnlyDictionary<string, ScryfallCard> _cardsByName;

        public FakeScryfallCardResolver(IReadOnlyDictionary<string, ScryfallCard> cardsByName)
        {
            _cardsByName = cardsByName;
        }

        public Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
            {
                StatusCode = HttpStatusCode.OK,
                Data = new ScryfallCollectionResponse(_cardsByName.Values.ToList(), null)
            });

        public Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => Task.FromResult(_cardsByName.TryGetValue(cardName, out var card) ? card : null);

        public Task<ScryfallCard?> SearchPrintingFallbackCardAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);

        public Task<ScryfallCard?> ResolveSingleAsync(string cardName, CancellationToken cancellationToken)
            => SearchFallbackCardAsync(cardName, cancellationToken);
    }
}
