using System.Text.RegularExpressions;
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
/// Tests for <see cref="ContentKbRelevanceService"/>.
/// </summary>
public sealed class ContentKbRelevanceServiceTests
{
    [Fact]
    public async Task GetRelevantClipsAsync_FlagDisabled_ReturnsNullWithoutTouchingStores()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], ["cEDH"]);
        var store = new TrackingContentSiteIndexStore([row]);
        var categoryStore = new TrackingCategoryKnowledgeStore();
        var flags = new TrackingFeatureFlagCache(new Dictionary<string, bool>
        {
            ["content.kb.enabled"] = false
        });
        var archetypeDeriver = new ContentKbArchetypeDeriver(categoryStore);
        var sut = CreateService(store, flags, archetypeDeriver, new Dictionary<string, string>());

        var result = await sut.GetRelevantClipsAsync("Tymna the Weaver", "cEDH");

        Assert.Null(result);
        Assert.Equal(0, store.PublishedRowsQueryCount);
        Assert.Equal(0, categoryStore.CommanderQueryCount);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_CommanderFoundOnlyInClipText_WithArchetypeOverlap_ReturnsClip()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var categoryStore = new TrackingCategoryKnowledgeStore
        {
            CommanderRows =
            [
                new CategoryKnowledgeRow("tutor", "Demonic Tutor", 8, 4),
                new CategoryKnowledgeRow("counter", "Counterspell", 7, 4),
            ]
        };
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary with no commander mention.",
            ("02:14", "Tymna the Weaver keeps the combo line compact and protected."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(categoryStore),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync("Tymna the Weaver / Kraum, Ludevic's Opus", bracket: null);

        var clip = Assert.Single(result!);
        Assert.Equal("02:14", clip.TimestampLabel);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_SingleDimensionMatch_ReturnsNull()
    {
        var row = CreateRow(1, "artifact-a.md", ["value-engine"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "A summary about Tymna the Weaver.",
            ("02:14", "No second dimension here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync("Tymna the Weaver", bracket: null, deckArchetypes: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_CommanderAndBracketMatch_QualifyWithoutArchetypeOverlap()
    {
        var row = CreateRow(1, "artifact-a.md", ["value-engine"], ["cEDH"]);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Kraum, Ludevic's Opus is the engine that closes the game."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver / Kraum, Ludevic's Opus",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Single(result!);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_MoreThanFiveQualifyingClips_ReturnsBestArtifactFirstInDocumentOrder()
    {
        var rows = new[]
        {
            CreateRow(1, "artifact-top.md", ["combo"], ["cEDH"]) with { Title = "Top Row" },
            CreateRow(2, "artifact-next.md", ["combo"], []) with { Title = "Next Row" },
            CreateRow(3, "artifact-third.md", ["combo"], []) with { Title = "Third Row" },
            CreateRow(4, "artifact-fourth.md", ["combo"], []) with { Title = "Fourth Row" },
            CreateRow(5, "artifact-fifth.md", ["combo"], []) with { Title = "Fifth Row" },
            CreateRow(6, "artifact-sixth.md", ["combo"], []) with { Title = "Sixth Row" }
        };
        var store = new TrackingContentSiteIndexStore(rows);
        var artifacts = new Dictionary<string, string>
        {
            [rows[0].ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=top123",
                "2026-06-05T12:34:56Z",
                "Strong summary.",
                ("00:10", "Tymna the Weaver opens the line."),
                ("00:20", "Second top clip for Tymna the Weaver."),
                ("00:30", "Third top clip for Tymna the Weaver."),
                ("00:40", "Fourth top clip for Tymna the Weaver.")),
            [rows[1].ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=next123",
                "2026-06-05T12:34:56Z",
                "Backup summary.",
                ("01:10", "Tymna the Weaver appears in this lower-ranked artifact."),
                ("01:20", "Second lower-ranked clip for Tymna the Weaver.")),
            [rows[2].ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=third123",
                "2026-06-05T12:34:56Z",
                "Third summary.",
                ("02:10", "Tymna the Weaver keeps this third video relevant.")),
            [rows[3].ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=fourth123",
                "2026-06-05T12:34:56Z",
                "Fourth summary.",
                ("03:10", "Tymna the Weaver keeps this fourth video relevant.")),
            [rows[4].ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=fifth123",
                "2026-06-05T12:34:56Z",
                "Fifth summary.",
                ("04:10", "Tymna the Weaver keeps this fifth video relevant.")),
            [rows[5].ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=sixth123",
                "2026-06-05T12:34:56Z",
                "Sixth summary.",
                ("05:10", "Tymna the Weaver keeps this sixth video relevant."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal(5, result!.Count);
        Assert.Equal(5, result.Select(clip => clip.Title).Distinct(StringComparer.Ordinal).Count());
        Assert.Single(result, clip => clip.Title == rows[0].Title);
        Assert.Equal("00:10", result[0].TimestampLabel);
        Assert.DoesNotContain(result, clip => clip.Title == rows[5].Title);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_Spike001AtraxaScenario_DiverseTopicalNoCommanderLeakage()
    {
        var glassCannon = CreateRow(1, "glass-cannon.md", ["midrange", "combo", "value-engine", "ramp", "aggro"], ["Upgraded"]) with
        {
            Source = "Salubrious Snail",
            Title = "The Problem with Glass Cannon Commanders"
        };
        var tooMuchRamp = CreateRow(2, "too-much-ramp.md", ["ramp", "midrange"], []) with
        {
            Source = "Salubrious Snail",
            Title = "You Might Have Too Much Ramp"
        };
        var deckbuildingMistakes = CreateRow(3, "mistakes.md", ["control", "value-engine", "midrange"], []) with
        {
            Source = "Salubrious Snail",
            Title = "5 Most Common Deckbuilding Mistakes"
        };
        var store = new TrackingContentSiteIndexStore([glassCannon, tooMuchRamp, deckbuildingMistakes]);
        var artifacts = new Dictionary<string, string>
        {
            [glassCannon.ArtifactPath] = BuildArtifact(
                glassCannon.VideoUrl,
                "2026-06-05T12:34:56Z",
                "A broad survey of explosive glass cannon commanders and why they stumble.",
                ("00:00", "Take Kaalia and Animar as examples of explosive commanders that demand narrow deckbuilding."),
                ("00:30", "Isshin doubles attack triggers while Zur the Enchanter is a pure enabler."),
                ("01:10", "Atraxa gives proliferates, but the larger point is how fragile these commanders can be.")),
            [tooMuchRamp.ArtifactPath] = BuildArtifact(
                tooMuchRamp.VideoUrl,
                "2026-06-05T12:34:56Z",
                "Ramp is good until it crowds out removal, protection, and payoffs in a midrange shell.",
                ("00:15", "If your ramp count is bloated, your control deck stops drawing enough removal and protection."),
                ("01:05", "Midrange value-engine decks want ramp, but they still need payoffs and interaction.")),
            [deckbuildingMistakes.ArtifactPath] = BuildArtifact(
                deckbuildingMistakes.VideoUrl,
                "2026-06-05T12:34:56Z",
                "Focused decks win more often because every slot reinforces the same plan.",
                ("00:45", "Value-engine control decks lose percentage points when they split between too many plans."),
                ("01:30", "Trim distractions so your ramp, removal, and proliferate payoffs actually work together."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Atraxa, Praetors' Voice",
            "Upgraded",
            deckArchetypes: new HashSet<string>(["ramp", "control", "value-engine", "midrange"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.True(result!.Select(clip => clip.Title).Distinct(StringComparer.Ordinal).Count() >= 2);
        Assert.Contains(result, clip => clip.Title == tooMuchRamp.Title);
        Assert.Contains(result, clip => clip.Title == deckbuildingMistakes.Title);
        Assert.True(result.Count(clip => clip.Title == glassCannon.Title) <= 1);
        Assert.DoesNotContain(result, clip => clip.Excerpt.Contains("Kaalia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result, clip => clip.Excerpt.Contains("Animar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetRelevantClipsAsync_OtherRowsExist_TopArtifactContributesOneClipMaximum()
    {
        var monopolyRow = CreateRow(1, "artifact-monopoly.md", ["combo"], ["cEDH"]) with { Title = "Monopoly Row" };
        var supportingRow = CreateRow(2, "artifact-supporting.md", ["combo"], ["cEDH"]) with { Title = "Supporting Row" };
        var store = new TrackingContentSiteIndexStore([monopolyRow, supportingRow]);
        var artifacts = new Dictionary<string, string>
        {
            [monopolyRow.ArtifactPath] = BuildArtifact(
                monopolyRow.VideoUrl,
                "2026-06-05T12:34:56Z",
                "Monopoly summary.",
                ("00:10", "Tymna the Weaver opens the line."),
                ("00:20", "Tymna the Weaver protects the line."),
                ("00:30", "Tymna the Weaver reloads the line."),
                ("00:40", "Tymna the Weaver closes the line.")),
            [supportingRow.ArtifactPath] = BuildArtifact(
                supportingRow.VideoUrl,
                "2026-06-05T12:34:56Z",
                "Supporting summary.",
                ("01:10", "Tymna the Weaver still appears in the next relevant video."),
                ("01:20", "A second supporting clip exists but should not be needed."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Single(result!, clip => clip.Title == monopolyRow.Title);
        Assert.Contains(result, clip => clip.Title == supportingRow.Title);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_GeneralAdviceWithoutCommander_QualifiesOnArchetypeAndContentOverlap()
    {
        var adviceRow = CreateRow(1, "artifact-advice.md", ["ramp", "midrange"], []) with { Title = "You Might Have Too Much Ramp" };
        var store = new TrackingContentSiteIndexStore([adviceRow]);
        var artifactText = BuildArtifact(
            adviceRow.VideoUrl,
            "2026-06-05T12:34:56Z",
            "Ramp-heavy decks often cut too much removal, protection, and card flow for extra mana rocks.",
            ("00:15", "Midrange value-engine decks need ramp, but they also need removal and protection."),
            ("00:45", "Your proliferate payoffs do not matter if all your nonland slots are just more ramp."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [adviceRow.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Atraxa, Praetors' Voice",
            bracket: null,
            deckArchetypes: new HashSet<string>(["ramp", "control", "value-engine", "midrange"], StringComparer.OrdinalIgnoreCase));

        var clip = Assert.Single(result!);
        Assert.Equal(adviceRow.Title, clip.Title);
    }

    [Fact]
    public void ScoreArtifact_AtraxaOwnCommanderMention_BeatsForeignCommanderBreadth()
    {
        var deckArchetypes = new HashSet<string>(["ramp", "control", "value-engine", "midrange"], StringComparer.OrdinalIgnoreCase);
        var normalizedCommander = new ContentKbRelevanceService.NormalizedCommander(
            "atraxa, praetors' voice",
            ["atraxa"]);
        var ownCommanderScore = ContentKbRelevanceService.ScoreArtifact(
            CreateScoreInput(
                CreateRow(1, "artifact-own.md", ["control"], ["Upgraded"]),
                "Atraxa, Praetors' Voice keeps the proliferate deck focused on removal, protection, and counters."),
            normalizedCommander,
            "Upgraded",
            deckArchetypes);
        var foreignCommanderScore = ContentKbRelevanceService.ScoreArtifact(
            CreateScoreInput(
                CreateRow(2, "artifact-foreign.md", ["ramp", "control", "value-engine", "midrange", "aggro"], ["Upgraded"]),
                "Kaalia wants angels and dragons while Animar wants creature storm turns."),
            normalizedCommander,
            "Upgraded",
            deckArchetypes);

        Assert.True(ownCommanderScore > foreignCommanderScore);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_NoRowsClearRelevanceFloor_ReturnsNull()
    {
        var kaaliaRow = CreateRow(1, "artifact-kaalia.md", ["ramp", "midrange"], ["Upgraded"]) with { Title = "Kaalia Spotlight" };
        var kinnanRow = CreateRow(2, "artifact-kinnan.md", ["control", "value-engine"], ["Upgraded"]) with { Title = "Kinnan Spotlight" };
        var store = new TrackingContentSiteIndexStore([kaaliaRow, kinnanRow]);
        var artifacts = new Dictionary<string, string>
        {
            [kaaliaRow.ArtifactPath] = BuildArtifact(
                kaaliaRow.VideoUrl,
                "2026-06-05T12:34:56Z",
                "Kaalia wants massive flying threats and combat shortcuts.",
                ("00:10", "Kaalia cheats angels, demons, and dragons into play.")),
            [kinnanRow.ArtifactPath] = BuildArtifact(
                kinnanRow.VideoUrl,
                "2026-06-05T12:34:56Z",
                "Kinnan turns sea-monster haymakers into splashy highlight turns.",
                ("00:20", "Kinnan rewards giant creature reveals and flashy activated abilities."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Atraxa, Praetors' Voice",
            "Upgraded",
            deckArchetypes: new HashSet<string>(["ramp", "control", "value-engine", "midrange"], StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_PartnerAwareCommanderMatch_AllowsEitherPartnerToQualify()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Kraum, Ludevic's Opus carries the wheel plan here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver / Kraum, Ludevic's Opus",
            bracket: null,
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Single(result!);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_FullCommanderName_MatchesShortNameMentionBeforeComma()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Kinnan powers the artifact combo turn here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Kinnan, Bonder Prodigy",
            bracket: null,
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Single(result!);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_PreCommaTokenShorterThanFourChars_DoesNotCreateCommanderHit()
    {
        var row = CreateRow(1, "artifact-a.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([row]);
        var artifactText = BuildArtifact(
            "https://www.youtube.com/watch?v=abc123",
            "2026-06-05T12:34:56Z",
            "Neutral summary.",
            ("02:14", "Rin carries the combo turn here."));
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string> { [row.ArtifactPath] = artifactText });

        var result = await sut.GetRelevantClipsAsync(
            "Rin, Test Commander",
            bracket: null,
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_TightBudget_TrimsLowestScoringClipsFirst()
    {
        var highRow = CreateRow(1, "artifact-high.md", ["combo"], ["cEDH"]);
        var lowRow = CreateRow(2, "artifact-low.md", ["combo"], []);
        var store = new TrackingContentSiteIndexStore([highRow, lowRow]);
        var artifacts = new Dictionary<string, string>
        {
            [highRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=high123",
                "2026-06-05T12:34:56Z",
                "High summary.",
                ("00:10", "Tymna the Weaver top clip alpha."),
                ("00:20", "Tymna the Weaver top clip beta."),
                ("00:30", "Tymna the Weaver top clip gamma.")),
            [lowRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=low123",
                "2026-06-05T12:34:56Z",
                "Low summary.",
                ("01:10", "Tymna the Weaver lower clip alpha."),
                ("01:20", "Tymna the Weaver lower clip beta."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase),
            maxRenderedChars: 300);

        Assert.NotNull(result);
        var clip = Assert.Single(result!);
        Assert.Equal(highRow.Title, clip.Title);
    }

    [Fact]
    public async Task ScoreAllAsync_ReturnsEveryVisibleRowIncludingZeroScores()
    {
        var matchRow = CreateRow(1, "artifact-match.md", ["combo"], ["cEDH"]);
        var zeroRow = CreateRow(2, "artifact-zero.md", ["lands"], []);
        var store = new TrackingContentSiteIndexStore([matchRow, zeroRow]);
        var artifacts = new Dictionary<string, string>
        {
            [matchRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=match123",
                "2026-06-05T12:34:56Z",
                "Summary.",
                ("00:10", "Tymna the Weaver is named here.")),
            [zeroRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=zero123",
                "2026-06-05T12:34:56Z",
                "Summary.",
                ("00:10", "No relevant commander text."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts);

        var scored = await sut.ScoreAllAsync(
            "Tymna the Weaver",
            "cEDH",
            CancellationToken.None);

        Assert.Equal(2, scored.Count);
        Assert.Equal(matchRow.Id, scored[0].Row.Id);
        Assert.Contains(scored, item => item.Row.Id == zeroRow.Id && item.Score == 0d);
    }

    [Fact]
    public async Task GetRelevantClipsAsync_FileReadFailureOnOneArtifact_ContinuesSelection()
    {
        var badRow = CreateRow(1, "artifact-bad.md", ["combo"], ["cEDH"]);
        var goodRow = CreateRow(2, "artifact-good.md", ["combo"], ["cEDH"]);
        var store = new TrackingContentSiteIndexStore([badRow, goodRow]);
        var artifacts = new Dictionary<string, string>
        {
            [goodRow.ArtifactPath] = BuildArtifact(
                "https://www.youtube.com/watch?v=good123",
                "2026-06-05T12:34:56Z",
                "Summary.",
                ("00:10", "Tymna the Weaver still qualifies here."))
        };
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            artifacts,
            throwOnRead: "artifact-bad.md");

        var result = await sut.GetRelevantClipsAsync(
            "Tymna the Weaver",
            "cEDH",
            deckArchetypes: new HashSet<string>(["combo"], StringComparer.OrdinalIgnoreCase));

        var clip = Assert.Single(result!);
        Assert.Equal(goodRow.Title, clip.Title);
    }

    [Fact]
    public async Task ResolvePinTitlesAsync_MapsKnownIdsToTitles_SkipsUnknown()
    {
        var knownRow = CreateRow(1, "artifact-known.md", ["combo"], ["cEDH"]) with
        {
            YoutubeVideoId = "known-id",
            Title = "Known Title"
        };
        var otherRow = CreateRow(2, "artifact-other.md", ["combo"], ["cEDH"]) with
        {
            YoutubeVideoId = "known-other",
            Title = "Other Title"
        };
        var store = new TrackingContentSiteIndexStore([knownRow, otherRow]);
        var sut = CreateService(
            store,
            new TrackingFeatureFlagCache(),
            new ContentKbArchetypeDeriver(new TrackingCategoryKnowledgeStore()),
            new Dictionary<string, string>());

        var resolved = await sut.ResolvePinTitlesAsync(["known-id", "missing-id"]);
        var emptyResolved = await sut.ResolvePinTitlesAsync([]);

        Assert.Equal("Known Title", resolved["known-id"]);
        Assert.DoesNotContain("missing-id", resolved.Keys);
        Assert.Empty(emptyResolved);
    }

    private static ContentKbRelevanceService CreateService(
        TrackingContentSiteIndexStore store,
        IFeatureFlagCache flagCache,
        ContentKbArchetypeDeriver archetypeDeriver,
        IReadOnlyDictionary<string, string> artifacts,
        string? throwOnRead = null)
    {
        return new ContentKbRelevanceService(
            store,
            artifactPath => artifactPath,
            flagCache,
            archetypeDeriver,
            logger: null,
            readArtifactAsync: (artifactPath, cancellationToken) =>
            {
                if (string.Equals(artifactPath, throwOnRead, StringComparison.Ordinal))
                {
                    throw new IOException("boom");
                }

                if (!artifacts.TryGetValue(artifactPath, out var text))
                {
                    throw new FileNotFoundException("missing test artifact", artifactPath);
                }

                return Task.FromResult(text);
            });
    }

    private static ContentSiteIndexRow CreateRow(long id, string artifactPath, IReadOnlyList<string> archetypeTags, IReadOnlyList<string> bracketTags)
    {
        return new ContentSiteIndexRow
        {
            Id = id,
            Source = "EDHRECast",
            Title = $"Artifact {id}",
            VideoUrl = $"https://www.youtube.com/watch?v=video{id}",
            ArtifactPath = artifactPath,
            PublishedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IndexedUtc = new DateTimeOffset(2026, 6, 5, 0, 0, 0, TimeSpan.Zero),
            IsVisible = true,
            ArchetypeTags = archetypeTags,
            BracketTags = bracketTags,
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = $"video{id}",
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

    private static ContentKbRelevanceService.ScoreInput CreateScoreInput(
        ContentSiteIndexRow row,
        string searchText)
    {
        var normalizedSearchText = Regex.Replace(searchText, @"\s+", " ").Trim();

        return new ContentKbRelevanceService.ScoreInput(
            row,
            row.Title,
            normalizedSearchText,
            normalizedSearchText,
            row.VideoUrl,
            row.ArchetypeTags,
            row.BracketTags,
            [(string.Empty, normalizedSearchText)],
            row.PublishedUtc ?? row.IndexedUtc,
            normalizedSearchText);
    }

    private sealed class TrackingFeatureFlagCache : IFeatureFlagCache
    {
        private readonly Dictionary<string, bool> _flags;

        public TrackingFeatureFlagCache(Dictionary<string, bool>? flags = null)
        {
            _flags = flags ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsEnabled(string key) => !_flags.TryGetValue(key, out var enabled) || enabled;

        public IReadOnlyDictionary<string, bool> Snapshot() => _flags;

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TrackingContentSiteIndexStore : IContentSiteIndexStore
    {
        private readonly IReadOnlyList<ContentSiteIndexRow> _rows;

        public TrackingContentSiteIndexStore(IReadOnlyList<ContentSiteIndexRow> rows)
        {
            _rows = rows;
        }

        public int PublishedRowsQueryCount { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentSiteIndexRow?>(null);

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
        {
            PublishedRowsQueryCount++;
            return Task.FromResult(_rows);
        }

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_rows);

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(_rows.FirstOrDefault(row => row.Id == id));

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TrackingCategoryKnowledgeStore : ICategoryKnowledgeStore
    {
        public IReadOnlyList<CategoryKnowledgeRow> CommanderRows { get; init; } = Array.Empty<CategoryKnowledgeRow>();

        public int CommanderQueryCount { get; private set; }

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsAsync(string cardName, string? boardFilter = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CategoryKnowledgeRow>>(Array.Empty<CategoryKnowledgeRow>());

        public Task<IReadOnlyList<CategoryKnowledgeRow>> GetCategoryRowsForCommanderAsync(string commanderName, CancellationToken cancellationToken = default)
        {
            CommanderQueryCount++;
            return Task.FromResult(CommanderRows);
        }

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
