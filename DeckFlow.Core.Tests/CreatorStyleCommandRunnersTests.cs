using System.Text.Json;
using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.ProfileFusion;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// End-to-end tests for the <c>creator-style-import-stated</c> and <c>fuse-profile</c> CLI runners
/// against a temporary SQLite content KB database.
/// </summary>
public sealed class CreatorStyleCommandRunnersTests : IDisposable
{
    private static void ClearPool(string path) => SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(path)}"));
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-style-command-runners-{Guid.NewGuid():N}.db");
    private readonly string _seedPath = Path.Combine(Path.GetTempPath(), $"creator-stated-rules-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        ClearPool(_dbPath);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        File.Delete(_dbPath);
        File.Delete(_seedPath);
    }

    [Fact]
    public async Task RunCreatorStyleImportStatedAsync_IdentityAliasSuppressed_SkipsSlug()
    {
        const string slug = "creator-slug";
        var stores = CreatorStyleCommandRunners.CreateStoresForDatabase(_dbPath);
        var source = new ContentSourceStore(_dbPath, stores.SuppressionStore);
        await source.InsertSourceAsync(slug, "Suppressed Display", ContentSourceType.Podcast, "https://example.test/source");
        await stores.SuppressionStore.SuppressAsync("Suppressed Display", [], "test", DateTimeOffset.UtcNow, null);
        await File.WriteAllTextAsync(_seedPath, JsonSerializer.Serialize(new Dictionary<string, List<StatedRuleCandidate>>
        {
            [slug] = [new StatedRuleCandidate { Category = "deckbuilding", Metric = "ramp", Value = 1, Comparator = "gte", SourceClip = "test", Confidence = 1, VideoDateUtc = DateTimeOffset.UtcNow }],
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var originalError = Console.Error;
        using var error = new StringWriter();
        try
        {
            Console.SetError(error);
            var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(new FileInfo(_seedPath), new FileInfo(_dbPath));

            Assert.Equal(2, exitCode);
            Assert.Contains("Skipping suppressed creator 'creator-slug'.", error.ToString());
            Assert.Empty(await new CreatorStyleStatedRuleStore(_dbPath).GetBySlugAsync(slug));
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public async Task RunCreatorStyleIndexExportAsync_IdentityAliasSuppressed_ExcludesProfile()
    {
        const string slug = "creator-slug";
        var stores = CreatorStyleCommandRunners.CreateStoresForDatabase(_dbPath);
        var source = new ContentSourceStore(_dbPath, stores.SuppressionStore);
        await source.InsertSourceAsync(slug, "Suppressed Display", ContentSourceType.Podcast, "https://example.test/source");
        await stores.SuppressionStore.SuppressAsync("Suppressed Display", [], "test", DateTimeOffset.UtcNow, null);
        await stores.ProfileStore.UpsertAsync(new CreatorStyleProfile { Slug = slug, Platform = "youtube", MinDecks = 1, UpdatedUtc = DateTimeOffset.UtcNow });
        var output = Path.Combine(Path.GetTempPath(), $"profiles-{Guid.NewGuid():N}.json");

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(new FileInfo(_dbPath), new FileInfo(output), new FileInfo(Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}.json")));

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain(slug, await File.ReadAllTextAsync(output));
        File.Delete(output);
    }

    [Fact]
    public async Task ImportThenFuse_OneStatedRule_ReachesPersistedFusedTarget()
    {
        const string slug = "tracercreator";

        var profileStore = new CreatorStyleProfileStore(_dbPath);
        await profileStore.UpsertAsync(new CreatorStyleProfile
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 39,
            InsufficientSample = false,
            MeasuredMetrics =
            [
                new MeasuredMetric
                {
                    Metric = "category_ratio:board-wipe",
                    Value = 1.2,
                    NumDecks = 39,
                    Distribution = new MetricDistribution
                    {
                        Mean = 1.2,
                        Min = 1.2,
                        Max = 1.2,
                        StdDev = 0.1,
                        EffectiveSampleSize = 10.5,
                    },
                },
            ],
            UpdatedUtc = DateTimeOffset.UtcNow,
        });

        var seed = new Dictionary<string, List<StatedRuleCandidate>>
        {
            [slug] =
            [
                new StatedRuleCandidate
                {
                    Category = "deckbuilding",
                    Metric = "board-wipe",
                    Value = 5,
                    ValueMin = 3,
                    ValueMax = 5,
                    Comparator = "lte",
                    SourceClip = "Keep board wipes lean, not a full sweep every game.",
                    Confidence = 0.8,
                    VideoDateUtc = DateTimeOffset.Parse("2026-07-05T00:00:00Z"),
                    Provenance = "hand-authored",
                },
            ],
        };
        await File.WriteAllTextAsync(
            _seedPath,
            JsonSerializer.Serialize(seed, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var importExitCode = await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(
            new FileInfo(_seedPath),
            new FileInfo(_dbPath));
        Assert.Equal(0, importExitCode);

        var statedRuleStore = new CreatorStyleStatedRuleStore(_dbPath);
        var storedRules = await statedRuleStore.GetBySlugAsync(slug);
        Assert.Single(storedRules);

        var fuseExitCode = await CreatorStyleCommandRunners.RunFuseProfileAsync(slug, new FileInfo(_dbPath));
        Assert.Equal(0, fuseExitCode);

        var fusedProfile = await profileStore.GetBySlugAsync(slug);
        Assert.NotNull(fusedProfile);
        Assert.NotEmpty(fusedProfile!.FusedTargets);
        Assert.NotEmpty(fusedProfile.StatedRules);

        var boardWipeTarget = Assert.Single(fusedProfile.FusedTargets, target => target.Metric == "board-wipe");
        Assert.Equal("agree", boardWipeTarget.Verdict);
        Assert.Null(boardWipeTarget.Conflict);
    }

    [Fact]
    public async Task RunCreatorStyleImportStatedAsync_SlugWithEmptyRuleList_ReturnsTwoAndWritesNoRows()
    {
        var seed = new Dictionary<string, List<StatedRuleCandidate>> { ["emptyslug"] = [] };
        await File.WriteAllTextAsync(
            _seedPath,
            JsonSerializer.Serialize(seed, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(
            new FileInfo(_seedPath),
            new FileInfo(_dbPath));

        Assert.Equal(2, exitCode);
        var store = new CreatorStyleStatedRuleStore(_dbPath);
        Assert.Empty(await store.GetBySlugAsync("emptyslug"));
    }

    [Fact]
    public async Task RunCreatorStyleImportStatedAsync_MissingSeedFile_ReturnsOne()
    {
        var missingSeedPath = Path.Combine(Path.GetTempPath(), $"missing-seed-{Guid.NewGuid():N}.json");

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleImportStatedAsync(
            new FileInfo(missingSeedPath),
            new FileInfo(_dbPath));

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task RunFuseProfileAsync_MeasuredProfileWithZeroStatedRules_ReturnsTwo()
    {
        const string slug = "no-stated-rules";
        var profileStore = new CreatorStyleProfileStore(_dbPath);
        await profileStore.UpsertAsync(new CreatorStyleProfile
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 39,
            InsufficientSample = false,
            MeasuredMetrics =
            [
                new MeasuredMetric
                {
                    Metric = "category_ratio:ramp",
                    Value = 12.0,
                    NumDecks = 39,
                    Distribution = new MetricDistribution
                    {
                        Mean = 12.0,
                        Min = 12.0,
                        Max = 12.0,
                        StdDev = 0.1,
                        EffectiveSampleSize = 10.0,
                    },
                },
            ],
            UpdatedUtc = DateTimeOffset.UtcNow,
        });

        var exitCode = await CreatorStyleCommandRunners.RunFuseProfileAsync(slug, new FileInfo(_dbPath));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunFuseProfileAsync_NoProfileForSlug_ReturnsTwo()
    {
        var exitCode = await CreatorStyleCommandRunners.RunFuseProfileAsync("no-such-slug", new FileInfo(_dbPath));

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Fuse_TwoStatedRulesSameMetricConditionAndDate_FirstListedStaysActiveDeterministically()
    {
        // Why: content_stated_rules' (slug, metric, condition) primary key (grounding correction 2)
        // means the persisted store already collapses same-key rows to the last-upserted one on
        // write — proven by CreatorStyleStatedRuleStoreTests's
        // UpsertAsync_ReimportSameUnconditionedRule_StaysOneRowAndUpdatesInPlace, and by
        // 115-PATTERNS.md's own UpsertAsync_DuplicateRule_OverwritesPreviousVersion reference test
        // ("only v2 remains (upsert on primary key)"). Two rows sharing (metric, condition) can
        // therefore never both survive a real creator-style-import-stated run — so
        // StatedRuleRecencyCollapser's date tie-break is pinned directly through
        // ProfileFusionEngine.Fuse here, the same way the condition-scoping coverage exercises Fuse
        // directly rather than through the CLI/store round trip.
        var sameDate = DateTimeOffset.Parse("2026-07-05T00:00:00Z");
        var firstListed = new StatedRuleCandidate
        {
            Category = "deckbuilding",
            Metric = "draw",
            ValueMin = 13,
            ValueMax = 18,
            Comparator = "range",
            SourceClip = "First-listed draw target.",
            Confidence = 0.8,
            VideoDateUtc = sameDate,
        };
        var secondListed = new StatedRuleCandidate
        {
            Category = "deckbuilding",
            Metric = "draw",
            ValueMin = 12,
            ValueMax = 16,
            Comparator = "range",
            SourceClip = "Second-listed draw target, identical date.",
            Confidence = 0.8,
            VideoDateUtc = sameDate,
        };

        var firstRun = ProfileFusionEngine.Fuse([], [firstListed, secondListed]);
        var secondRun = ProfileFusionEngine.Fuse([], [firstListed, secondListed]);

        var active = Assert.Single(firstRun, target => target.Metric == "draw" && target.Source != "stated-superseded");
        var superseded = Assert.Single(firstRun, target => target.Metric == "draw" && target.Source == "stated-superseded");
        Assert.Equal("First-listed draw target.", active.SourceClip);
        Assert.Equal("Second-listed draw target, identical date.", superseded.SourceClip);
        Assert.Equal("superseded", superseded.Verdict);
        Assert.Equal(firstRun, secondRun);
    }
}
