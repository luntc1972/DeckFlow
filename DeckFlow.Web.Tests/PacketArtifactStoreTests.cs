using System.IO.Compression;
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
        Assert.Equal("Imported zip did not contain 40-deck-profile.json or 51-set-upgrade-response.json.", exception.Message);
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
}
