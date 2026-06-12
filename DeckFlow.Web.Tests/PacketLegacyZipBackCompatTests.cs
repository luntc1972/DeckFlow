using System.IO.Compression;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies pre-retire analysis packet zips carrying legacy expert entries still load.
/// </summary>
public sealed class PacketLegacyZipBackCompatTests
{
    [Fact]
    public void LoadFromZip_with_32_expert_context_and_33_expert_selection_entries_does_not_throw()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "01-request-context.txt", "format: Commander");
            WriteEntry(archive, "40-deck-profile.json", "{\"deck_profile\":{\"format\":\"Commander\"}}");
            WriteEntry(archive, "32-expert-context.json", "[]");
            WriteEntry(archive, "33-expert-selection.json", "{\"pinnedVideoIds\":[\"abc123\"],\"followedCreators\":[\"EDHRECast\"]}");
        }

        memoryStream.Position = 0;

        var request = new DeckAnalysisRequest();
        var exception = Record.Exception(() => PacketArtifactStore.LoadFromZip(memoryStream, request));

        Assert.Null(exception);
        Assert.Contains("deck_profile", request.DeckProfileJson, StringComparison.Ordinal);
        Assert.Equal(3, request.WorkflowStep);
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }
}
