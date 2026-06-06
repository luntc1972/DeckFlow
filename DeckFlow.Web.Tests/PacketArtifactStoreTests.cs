using System.IO.Compression;
using System.Text.Json;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for <see cref="PacketArtifactStore"/> covering build-and-load zip round-trip,
/// rejection of zips with no recognized response JSON, and path-traversal entry rejection.
/// </summary>
public sealed class PacketArtifactStoreTests
{
    [Fact]
    public void BuildZip_then_LoadFromZip_round_trips_response_json()
    {
        var request = new DeckAnalysisRequest
        {
            DeckProfileJson = "{\"deck_profile\":{\"format\":\"Commander\"}}",
            SetUpgradeResponseJson = "{\"set_upgrade_report\":{\"sets\":[]}}"
        };

        var bytes = PacketArtifactStore.BuildZip(
            request,
            commanderName: "Atraxa",
            inputSummary: "summary",
            requestContextText: "context",
            referenceText: null,
            analysisPromptText: "analysis prompt",
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: "upgrade prompt");

        var loaded = new DeckAnalysisRequest();
        using var memoryStream = new MemoryStream(bytes);
        PacketArtifactStore.LoadFromZip(memoryStream, loaded);

        Assert.Contains("deck_profile", loaded.DeckProfileJson);
        Assert.Contains("set_upgrade_report", loaded.SetUpgradeResponseJson);
        Assert.Equal(5, loaded.WorkflowStep);
    }

    [Fact]
    public void LoadFromZip_throws_when_no_response_json_present()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("00-input-summary.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("noise only");
        }

        memoryStream.Position = 0;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PacketArtifactStore.LoadFromZip(memoryStream, new DeckAnalysisRequest()));
        Assert.Equal("Imported zip did not contain a recognized DeckFlow session — expected 01-request-context.txt, 40-deck-profile.json, or 51-set-upgrade-response.json.", exception.Message);
    }

    [Fact]
    public void LoadFromZip_rejects_directory_traversal_entries()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../escape.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("{}");
        }

        memoryStream.Position = 0;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PacketArtifactStore.LoadFromZip(memoryStream, new DeckAnalysisRequest()));
        Assert.Contains("invalid entry path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildZip_with_expert_context_round_trips_into_request()
    {
        var expertContext = JsonSerializer.Serialize(new List<ContentKbExcerpt>
        {
            new()
            {
                Source = "EDHRECast",
                Title = "Clip One",
                VideoUrl = "https://www.youtube.com/watch?v=abc123&t=134s",
                TimestampLabel = "02:14",
                Excerpt = "First excerpt.",
                HarvestDate = new DateTimeOffset(2026, 6, 5, 12, 34, 56, TimeSpan.Zero),
                Score = 2.75
            },
            new()
            {
                Source = "The Command Zone",
                Title = "Clip Two",
                VideoUrl = "https://www.youtube.com/watch?v=xyz789&t=305s",
                TimestampLabel = "05:05",
                Excerpt = "Second excerpt.",
                HarvestDate = new DateTimeOffset(2026, 6, 6, 1, 2, 3, TimeSpan.Zero),
                Score = 3.25
            }
        });

        var bytes = PacketArtifactStore.BuildZip(
            new DeckAnalysisRequest
            {
                DeckProfileJson = "{\"deck_profile\":{\"format\":\"Commander\"}}"
            },
            commanderName: "Atraxa",
            inputSummary: "summary",
            requestContextText: "context",
            referenceText: null,
            analysisPromptText: "analysis prompt",
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            expertContextJson: expertContext);

        var loaded = new DeckAnalysisRequest();
        using var memoryStream = new MemoryStream(bytes);
        PacketArtifactStore.LoadFromZip(memoryStream, loaded);

        var roundTripped = JsonSerializer.Deserialize<List<ContentKbExcerpt>>(loaded.ExpertContextJson);

        Assert.NotNull(roundTripped);
        Assert.Equal(2, roundTripped.Count);
        Assert.Equal("EDHRECast", roundTripped[0].Source);
        Assert.Equal("05:05", roundTripped[1].TimestampLabel);
        Assert.Equal("Second excerpt.", roundTripped[1].Excerpt);
        Assert.Equal(3.25, roundTripped[1].Score);
    }

    [Fact]
    public void BuildZip_with_null_expert_context_omits_entry_and_loads_empty_request_field()
    {
        var bytes = PacketArtifactStore.BuildZip(
            new DeckAnalysisRequest
            {
                DeckProfileJson = "{\"deck_profile\":{\"format\":\"Commander\"}}"
            },
            commanderName: "Atraxa",
            inputSummary: "summary",
            requestContextText: "context",
            referenceText: null,
            analysisPromptText: "analysis prompt",
            deckProfileSchemaJson: "{}",
            setUpgradePromptText: null,
            expertContextJson: null);

        using (var archiveStream = new MemoryStream(bytes))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false))
        {
            Assert.DoesNotContain(archive.Entries, entry => string.Equals(entry.FullName, "32-expert-context.json", StringComparison.OrdinalIgnoreCase));
        }

        var loaded = new DeckAnalysisRequest();
        using var memoryStream = new MemoryStream(bytes);
        PacketArtifactStore.LoadFromZip(memoryStream, loaded);

        Assert.Equal(string.Empty, loaded.ExpertContextJson);
    }

    [Fact]
    public void LoadFromZip_allows_32_expert_context_entry()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var contextEntry = archive.CreateEntry("01-request-context.txt");
            using (var contextWriter = new StreamWriter(contextEntry.Open()))
            {
                contextWriter.Write("format: Commander");
            }

            var expertEntry = archive.CreateEntry("32-expert-context.json");
            using var expertWriter = new StreamWriter(expertEntry.Open());
            expertWriter.Write("[]");
        }

        memoryStream.Position = 0;

        var loaded = new DeckAnalysisRequest();
        PacketArtifactStore.LoadFromZip(memoryStream, loaded);

        Assert.Equal("[]", loaded.ExpertContextJson);
    }
}
