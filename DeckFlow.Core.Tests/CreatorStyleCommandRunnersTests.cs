using System.Text.Json;
using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
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
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-style-command-runners-{Guid.NewGuid():N}.db");
    private readonly string _seedPath = Path.Combine(Path.GetTempPath(), $"creator-stated-rules-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        if (File.Exists(_seedPath))
        {
            File.Delete(_seedPath);
        }
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
}
