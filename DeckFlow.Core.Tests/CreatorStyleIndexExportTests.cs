using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Storage-to-file-to-record round-trip coverage for the <c>creator-style-index-export</c> CLI
/// runner against a temporary SQLite content KB database.
/// </summary>
public sealed class CreatorStyleIndexExportTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"creator-style-index-export-{Guid.NewGuid():N}.db");
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), $"creator-style-index-export-out-{Guid.NewGuid():N}");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task Export_OneProfileWithFusedTargetsAndTwoDeckCacheRows_ReturnsZeroAndRoundTripsBothFiles()
    {
        const string slug = "exportcreator";
        var profileStore = new CreatorStyleProfileStore(_dbPath);
        var deckCacheStore = new CreatorDeckCacheStore(_dbPath);

        var seededProfile = new CreatorStyleProfile
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 12,
            InsufficientSample = false,
            MeasuredMetrics =
            [
                new MeasuredMetric
                {
                    Metric = "category_ratio:ramp",
                    Value = 10.5,
                    NumDecks = 12,
                },
            ],
            StatedRules =
            [
                new StatedRule
                {
                    Category = "deckbuilding",
                    TargetMetric = "ramp",
                    TargetValueMin = 8,
                    TargetValueMax = 12,
                    Comparator = "range",
                    SourceClip = "Run about 10 ramp pieces.",
                    Confidence = 0.8,
                    VideoDateUtc = DateTimeOffset.Parse("2026-07-05T00:00:00Z"),
                },
            ],
            FusedTargets =
            [
                new FusedTarget
                {
                    Metric = "ramp",
                    Value = 10.5,
                    Weight = 1.0,
                    Source = "measured",
                    Verdict = "agree",
                },
            ],
            UpdatedUtc = DateTimeOffset.UtcNow,
        };
        await profileStore.UpsertAsync(seededProfile);

        var deckOne = new CreatorDeckCacheEntry
        {
            CreatorSlug = slug,
            DeckId = "deck-1",
            ContentHash = "hash-1",
            Size = 100,
            ConfidenceMarker = "high",
            Entries =
            [
                new DeckEntry
                {
                    Name = "Sol Ring",
                    NormalizedName = "sol ring",
                    Quantity = 1,
                    Board = "mainboard",
                },
            ],
            CachedUtc = DateTimeOffset.UtcNow,
        };
        var deckTwo = new CreatorDeckCacheEntry
        {
            CreatorSlug = slug,
            DeckId = "deck-2",
            ContentHash = "hash-2",
            Size = 99,
            ConfidenceMarker = "high",
            Entries =
            [
                new DeckEntry
                {
                    Name = "Arcane Signet",
                    NormalizedName = "arcane signet",
                    Quantity = 1,
                    Board = "mainboard",
                },
            ],
            CachedUtc = DateTimeOffset.UtcNow,
        };
        await deckCacheStore.UpsertAsync(deckOne);
        await deckCacheStore.UpsertAsync(deckTwo);

        var profilesOutputPath = Path.Combine(_outputDir, "creator-style-profiles.json");
        var deckCacheOutputPath = Path.Combine(_outputDir, "creator-deck-cache.json");

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(
            new FileInfo(_dbPath),
            new FileInfo(profilesOutputPath),
            new FileInfo(deckCacheOutputPath));

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(profilesOutputPath));
        Assert.True(File.Exists(deckCacheOutputPath));

        await using (var profilesStream = File.OpenRead(profilesOutputPath))
        {
            var writtenProfiles = await System.Text.Json.JsonSerializer
                .DeserializeAsync<CreatorStyleProfile[]>(profilesStream, CreatorStyleSeedJson.Options);
            Assert.NotNull(writtenProfiles);
            var writtenProfile = Assert.Single(writtenProfiles!, p => p.Slug == slug);
            Assert.NotEmpty(writtenProfile.FusedTargets);
            Assert.NotEmpty(writtenProfile.MeasuredMetrics);
            Assert.NotEmpty(writtenProfile.StatedRules);
            Assert.Equal("ramp", writtenProfile.FusedTargets[0].Metric);
        }

        await using (var deckCacheStream = File.OpenRead(deckCacheOutputPath))
        {
            var writtenEntries = await System.Text.Json.JsonSerializer
                .DeserializeAsync<CreatorDeckCacheEntry[]>(deckCacheStream, CreatorStyleSeedJson.Options);
            Assert.NotNull(writtenEntries);
            Assert.Equal(2, writtenEntries!.Length);
            var writtenDeckOne = Assert.Single(writtenEntries, e => e.DeckId == "deck-1");
            Assert.NotEmpty(writtenDeckOne.Entries);
            Assert.Equal("Sol Ring", writtenDeckOne.Entries[0].Name);
            var writtenDeckTwo = Assert.Single(writtenEntries, e => e.DeckId == "deck-2");
            Assert.NotEmpty(writtenDeckTwo.Entries);
        }
    }

    [Fact]
    public async Task Export_EmptyDatabase_ReturnsTwoAndWritesNoFile()
    {
        var profilesOutputPath = Path.Combine(_outputDir, "creator-style-profiles.json");
        var deckCacheOutputPath = Path.Combine(_outputDir, "creator-deck-cache.json");

        var exitCode = await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(
            new FileInfo(_dbPath),
            new FileInfo(profilesOutputPath),
            new FileInfo(deckCacheOutputPath));

        Assert.Equal(2, exitCode);
        Assert.False(File.Exists(profilesOutputPath));
        Assert.False(File.Exists(deckCacheOutputPath));
    }

    [Fact]
    public async Task Export_ProfileWithEmptyFusedTargets_StillExportsButWarns()
    {
        const string slug = "no-fused-targets";
        var profileStore = new CreatorStyleProfileStore(_dbPath);
        await profileStore.UpsertAsync(new CreatorStyleProfile
        {
            Slug = slug,
            Platform = "youtube",
            MinDecks = 6,
            InsufficientSample = false,
            MeasuredMetrics =
            [
                new MeasuredMetric
                {
                    Metric = "category_ratio:draw",
                    Value = 8.0,
                    NumDecks = 6,
                },
            ],
            FusedTargets = [],
            UpdatedUtc = DateTimeOffset.UtcNow,
        });

        var profilesOutputPath = Path.Combine(_outputDir, "creator-style-profiles.json");
        var deckCacheOutputPath = Path.Combine(_outputDir, "creator-deck-cache.json");

        var originalError = Console.Error;
        using var errorWriter = new StringWriter();
        Console.SetError(errorWriter);
        int exitCode;
        try
        {
            exitCode = await CreatorStyleCommandRunners.RunCreatorStyleIndexExportAsync(
                new FileInfo(_dbPath),
                new FileInfo(profilesOutputPath),
                new FileInfo(deckCacheOutputPath));
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(profilesOutputPath));
        var warningOutput = errorWriter.ToString();
        Assert.Contains(slug, warningOutput, StringComparison.Ordinal);
        Assert.Contains("fuse-profile", warningOutput, StringComparison.Ordinal);
    }
}
