using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ContentKbRelevanceService.GetMergedClipsAsync"/>.
/// </summary>
public sealed class ContentKbMergedClipsTests
{
    [Fact]
    public async Task Tier1_PinsInjectedFirst_InDocumentOrder()
    {
        var pin1 = CreateRow(1, "pin-1.md", "Source A", ["lands"], [], videoId: "pin-1");
        var pin2 = CreateRow(2, "pin-2.md", "Source B", ["lands"], [], videoId: "pin-2");
        var pin3 = CreateRow(3, "pin-3.md", "Source C", ["lands"], [], videoId: "pin-3");
        var auto1 = CreateRow(4, "auto-1.md", "Auto A", ["combo"], ["cEDH"], videoId: "auto-1");
        var auto2 = CreateRow(5, "auto-2.md", "Auto B", ["combo"], ["cEDH"], videoId: "auto-2");
        var sut = CreateService(
            [pin1, pin2, pin3, auto1, auto2],
            new Dictionary<string, string>
            {
                [pin1.ArtifactPath] = BuildArtifact(pin1.VideoUrl, "2026-06-05T12:34:56Z", "Pin one.", ("00:10", "No commander match here.")),
                [pin2.ArtifactPath] = BuildArtifact(pin2.VideoUrl, "2026-06-05T12:34:56Z", "Pin two.", ("00:20", "Still no commander match here.")),
                [pin3.ArtifactPath] = BuildArtifact(pin3.VideoUrl, "2026-06-05T12:34:56Z", "Pin three.", ("00:30", "Also not auto qualified.")),
                [auto1.ArtifactPath] = BuildArtifact(auto1.VideoUrl, "2026-06-05T12:34:56Z", "Auto one.", ("01:10", "Tymna the Weaver supports the combo turn.")),
                [auto2.ArtifactPath] = BuildArtifact(auto2.VideoUrl, "2026-06-05T12:34:56Z", "Auto two.", ("01:20", "Tymna the Weaver closes the combo line."))
            });

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection(["pin-3", "pin-1", "pin-2"], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            "Tymna the Weaver",
            "cEDH",
            new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal(5, result!.Count);
        Assert.Equal(new[] { "Artifact 3", "Artifact 1", "Artifact 2" }, result.Take(3).Select(clip => clip.Title));
        Assert.All(result.Take(3), clip => Assert.Equal("pinned", clip.ClipOrigin));
    }

    [Fact]
    public async Task Tier2_FollowedCreator_GateRelaxedToOneDimension()
    {
        var followed = CreateRow(1, "followed.md", "Followed Cast", ["value-engine"], [], videoId: "followed-1");
        var other = CreateRow(2, "other.md", "Other Cast", ["value-engine"], [], videoId: "other-1");
        var sut = CreateService(
            [followed, other],
            new Dictionary<string, string>
            {
                [followed.ArtifactPath] = BuildArtifact(followed.VideoUrl, "2026-06-05T12:34:56Z", "One dimension only.", ("00:10", "Tymna the Weaver appears here.")),
                [other.ArtifactPath] = BuildArtifact(other.VideoUrl, "2026-06-05T12:34:56Z", "No match.", ("00:20", "No relevant terms."))
            });

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection([], new HashSet<string>(["Followed Cast"], StringComparer.OrdinalIgnoreCase)),
            "Tymna the Weaver",
            bracket: null,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var clip = Assert.Single(result!);
        Assert.Equal("followed", clip.ClipOrigin);
        Assert.Equal(followed.Title, clip.Title);
    }

    [Fact]
    public async Task Tier3_AutoUnchanged()
    {
        var first = CreateRow(1, "first.md", "Source A", ["combo"], ["cEDH"], videoId: "first-1");
        var second = CreateRow(2, "second.md", "Source B", ["combo"], [], videoId: "second-1");
        var sut = CreateService(
            [first, second],
            new Dictionary<string, string>
            {
                [first.ArtifactPath] = BuildArtifact(first.VideoUrl, "2026-06-05T12:34:56Z", "First summary.", ("00:10", "Tymna the Weaver starts the combo.")),
                [second.ArtifactPath] = BuildArtifact(second.VideoUrl, "2026-06-05T12:34:56Z", "Second summary.", ("00:20", "Tymna the Weaver protects the combo."))
            });

        var auto = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));
        var merged = await sut.GetMergedClipsAsync(
            new ExpertSelection([], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            "Tymna the Weaver",
            "cEDH",
            new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(auto);
        Assert.NotNull(merged);
        Assert.Equal(auto!.Count, merged!.Count);
        Assert.Equal(auto.Select(clip => clip.Title), merged.Select(clip => clip.Title));
        Assert.Equal(auto.Select(clip => clip.TimestampLabel), merged.Select(clip => clip.TimestampLabel));
        Assert.All(merged, clip => Assert.Equal("auto", clip.ClipOrigin));
    }

    [Fact]
    public async Task Tier4_EvergreenFills_MaxOneClip()
    {
        var auto = CreateRow(1, "auto.md", "Auto Cast", ["combo"], ["cEDH"], videoId: "auto-1");
        var evergreen = CreateRow(2, "evergreen.md", "Evergreen Cast", ["lands"], [], isEvergreen: true, videoId: "evergreen-1");
        var sut = CreateService(
            [auto, evergreen],
            new Dictionary<string, string>
            {
                [auto.ArtifactPath] = BuildArtifact(
                    auto.VideoUrl,
                    "2026-06-05T12:34:56Z",
                    "Auto summary.",
                    ("00:10", "Tymna the Weaver starts the combo."),
                    ("00:20", "Tymna the Weaver finds the combo."),
                    ("00:30", "Tymna the Weaver protects the combo."),
                    ("00:40", "Tymna the Weaver wins the combo turn.")),
                [evergreen.ArtifactPath] = BuildArtifact(
                    evergreen.VideoUrl,
                    "2026-06-05T12:34:56Z",
                    "Evergreen summary.",
                    ("01:10", "Generic mulligan advice."),
                    ("01:20", "Generic sequencing advice."))
            });

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection([], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            "Tymna the Weaver",
            "cEDH",
            new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal(5, result!.Count);
        Assert.Single(result, clip => clip.ClipOrigin == "evergreen");
        Assert.Equal("evergreen", result[^1].ClipOrigin);
    }

    [Fact]
    public async Task TrimOrder_Tier4ThenTier3ThenTier2ThenTier1()
    {
        var pinned = CreateRow(1, "pinned.md", "Pinned Cast", ["lands"], [], videoId: "pin-1");
        var followed = CreateRow(2, "followed.md", "Followed Cast", ["lands"], [], videoId: "followed-1");
        var auto = CreateRow(3, "auto.md", "Auto Cast", ["combo"], ["cEDH"], videoId: "auto-1");
        var evergreen = CreateRow(4, "evergreen.md", "Evergreen Cast", ["lands"], [], isEvergreen: true, videoId: "evergreen-1");
        var sut = CreateService(
            [pinned, followed, auto, evergreen],
            new Dictionary<string, string>
            {
                [pinned.ArtifactPath] = BuildArtifact(pinned.VideoUrl, "2026-06-05T12:34:56Z", "Pinned.", ("00:10", "Pinned clip text is deliberately long.")),
                [followed.ArtifactPath] = BuildArtifact(followed.VideoUrl, "2026-06-05T12:34:56Z", "Followed.", ("00:20", "Tymna the Weaver followed clip text is deliberately long.")),
                [auto.ArtifactPath] = BuildArtifact(auto.VideoUrl, "2026-06-05T12:34:56Z", "Auto.", ("00:30", "Tymna auto clip text is deliberately long.")),
                [evergreen.ArtifactPath] = BuildArtifact(evergreen.VideoUrl, "2026-06-05T12:34:56Z", "Evergreen.", ("00:40", "Evergreen clip text is deliberately long."))
            });

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection(["pin-1"], new HashSet<string>(["Followed Cast"], StringComparer.OrdinalIgnoreCase)),
            "Tymna the Weaver",
            "cEDH",
            new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase),
            maxRenderedChars: 360);

        Assert.NotNull(result);
        Assert.Equal(new[] { "pinned", "followed" }, result!.Select(clip => clip.ClipOrigin));
    }

    [Fact]
    public async Task PinSurvivesTrim_LastTier1ClipKept()
    {
        var pinned = CreateRow(1, "pinned.md", "Pinned Cast", ["lands"], [], videoId: "pin-1");
        var sut = CreateService(
            [pinned],
            new Dictionary<string, string>
            {
                [pinned.ArtifactPath] = BuildArtifact(
                    pinned.VideoUrl,
                    "2026-06-05T12:34:56Z",
                    "Pinned summary.",
                    ("00:10", "Pinned clip text is very long and should force trimming."),
                    ("00:20", "Another pinned clip that should be trimmed away first."),
                    ("00:30", "Third pinned clip that should also be trimmed away."))
            });

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection(["pin-1"], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            commanderName: null,
            bracket: null,
            deckArchetypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            maxRenderedChars: 150);

        Assert.NotNull(result);
        Assert.True(result!.Count >= 1);
        Assert.All(result, clip => Assert.Equal("pinned", clip.ClipOrigin));
    }

    [Fact]
    public async Task PinCap_MaxThreePinnedVideos_Enforced()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(index => CreateRow(index, $"pin-{index}.md", $"Source {index}", ["lands"], [], videoId: $"pin-{index}"))
            .ToList();
        var artifacts = rows.ToDictionary(
            row => row.ArtifactPath,
            row => BuildArtifact(row.VideoUrl, "2026-06-05T12:34:56Z", "Pinned summary.", ("00:10", $"Pinned clip {row.Id}.")));
        var sut = CreateService(rows, artifacts);

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection(["pin-1", "pin-2", "pin-3", "pin-4", "pin-5"], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            commanderName: null,
            bracket: null,
            deckArchetypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal(3, result!.Count);
        Assert.All(result, clip => Assert.Equal("pinned", clip.ClipOrigin));
        Assert.Equal(new[] { "Artifact 1", "Artifact 2", "Artifact 3" }, result.Select(clip => clip.Title));
    }

    [Fact]
    public async Task GetMergedClipsAsync_PinnedRowWithUnparseableArtifact_StillEmitsPinnedClip()
    {
        var pinned = CreateRow(1, "broken-pin.md", "Pinned Cast", ["lands"], [], videoId: "VID1");
        var sut = CreateService(
            [pinned],
            artifacts: new Dictionary<string, string>(),
            throwOnArtifactPaths: new HashSet<string>([pinned.ArtifactPath], StringComparer.Ordinal));

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection(["VID1"], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            commanderName: null,
            bracket: null,
            deckArchetypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var clip = Assert.Single(result!);
        Assert.Equal("pinned", clip.ClipOrigin);
        Assert.Equal(pinned.Title, clip.Title);
        Assert.Equal(pinned.Source, clip.Source);
        Assert.Equal(pinned.VideoUrl, clip.VideoUrl);
    }

    [Fact]
    public async Task GetMergedClipsAsync_UnparseableUnpinnedRow_NotInAutoTier()
    {
        var broken = CreateRow(1, "broken-auto.md", "Auto Cast", ["combo"], ["cEDH"], videoId: "VID1");
        var sut = CreateService(
            [broken],
            artifacts: new Dictionary<string, string>(),
            throwOnArtifactPaths: new HashSet<string>([broken.ArtifactPath], StringComparer.Ordinal));

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection([], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            "Tymna the Weaver",
            "cEDH",
            new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMergedClipsAsync_ParseableCliplessRow_NotEmittedAsAutoClip()
    {
        var clipless = CreateRow(1, "clipless-auto.md", "Auto Cast", ["combo"], ["cEDH"], videoId: "VID1");
        var sut = CreateService(
            [clipless],
            new Dictionary<string, string>
            {
                [clipless.ArtifactPath] = BuildArtifact(
                    clipless.VideoUrl,
                    "2026-06-05T12:34:56Z",
                    "Tag-only score path.",
                    Array.Empty<(string Timestamp, string Excerpt)>())
            });

        var result = await sut.GetMergedClipsAsync(
            new ExpertSelection([], new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            commanderName: null,
            bracket: "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    private static ContentKbRelevanceService CreateService(
        IReadOnlyList<ContentSiteIndexRow> rows,
        IReadOnlyDictionary<string, string> artifacts,
        IReadOnlySet<string>? throwOnArtifactPaths = null)
    {
        var store = new FakeContentSiteIndexStore();
        foreach (var row in rows)
        {
            store.Rows.Add(row);
        }

        return new ContentKbRelevanceService(
            store,
            artifactPath => artifactPath,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            logger: null,
            readArtifactAsync: (artifactPath, cancellationToken) =>
            {
                if (throwOnArtifactPaths?.Contains(artifactPath) == true)
                {
                    throw new InvalidDataException("forced parse failure");
                }

                if (!artifacts.TryGetValue(artifactPath, out var text))
                {
                    throw new FileNotFoundException("missing test artifact", artifactPath);
                }

                return Task.FromResult(text);
            });
    }

    private static ContentSiteIndexRow CreateRow(
        long id,
        string artifactPath,
        string source,
        IReadOnlyList<string> archetypeTags,
        IReadOnlyList<string> bracketTags,
        bool isEvergreen = false,
        string? videoId = null)
    {
        return new ContentSiteIndexRow
        {
            Id = id,
            Source = source,
            Title = $"Artifact {id}",
            VideoUrl = $"https://www.youtube.com/watch?v={videoId ?? $"video{id}"}",
            ArtifactPath = artifactPath,
            PublishedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IndexedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IsVisible = true,
            IsEvergreen = isEvergreen,
            ArchetypeTags = archetypeTags,
            BracketTags = bracketTags,
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId ?? $"video{id}",
            RssGuid = null
        };
    }

    private static string BuildArtifact(string sourceUrl, string generatedUtc, string summary, params (string Timestamp, string Excerpt)[] clips)
    {
        var clipLines = string.Join(
            Environment.NewLine,
            clips.Select(clip => $"- **[{clip.Timestamp}]** {clip.Excerpt}"));

        return $$"""
---
source: "EDHRECast"
title: "Test Artifact"
url: "{{sourceUrl}}"
generated_utc: "{{generatedUtc}}"
---

## Summary

{{summary}}

## Key Clips

{{clipLines}}

## Tags

ignored
""";
    }

    private sealed class TrackingFeatureFlagCache : IFeatureFlagCache
    {
        public bool IsEnabled(string key) => true;

        public IReadOnlyDictionary<string, bool> Snapshot() => new Dictionary<string, bool>();

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TrackingCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<int> GetProcessedDeckCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetCommanderDeckCountAsync(string commanderName, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> RunCacheSweepAsync(ILogger logger, int durationSeconds, CancellationToken cancellationToken = default, IProgress<int>? progress = null) => Task.FromResult(0);

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task PersistObservedCategoriesAsync(string source, string cardName, IReadOnlyList<string> categories, int quantity = 1, string board = "mainboard", int deckCountIncrement = 0, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetTotalProcessedDeckCountSinceAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> GetTotalObservationCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TopCommanderRow>>(Array.Empty<TopCommanderRow>());

        public Task<IReadOnlyList<HarvestedCommanderRow>> GetPagedProcessedCommandersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<HarvestedCommanderRow>>(Array.Empty<HarvestedCommanderRow>());

        public Task<int> GetDistinctProcessedCommanderCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default) => Task.FromResult<long?>(null);

        public Task<CardDeckTotals> GetCardDeckTotalsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CardDeckTotals.Empty);
    }
}
