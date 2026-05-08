using System.IO.Compression;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ChatGptPacketArtifactStoreRoundTripTests
{
    private static readonly string FixtureDirectory = "/tmp/arna-test";

    [Fact]
    public void LoadFromZip_AlsoRestoresUserInputs_FromArnaFixture()
    {
        using var stream = BuildFixtureZip();

        var request = new ChatGptDeckRequest();
        ChatGptPacketArtifactStore.LoadFromZip(stream, request);

        Assert.True(request.DeckText.Length > 1000);
        Assert.Contains("Arna Kennerüd, Skycaptain", request.DeckText, StringComparison.Ordinal);
        Assert.Contains("SIDEBOARD:", request.DeckText, StringComparison.Ordinal);
        Assert.Equal(4, request.SelectedAnalysisQuestions.Count);
        Assert.Contains("cuts-for-strength", request.SelectedAnalysisQuestions);
        Assert.Contains("faster-competitive", request.SelectedAnalysisQuestions);
        Assert.Contains("resilience-to-wipes", request.SelectedAnalysisQuestions);
        Assert.Contains("strengths-weaknesses", request.SelectedAnalysisQuestions);
        Assert.Equal("Upgraded", request.TargetCommanderBracket);
        Assert.Single(request.SelectedSetCodes);
        Assert.Contains("sos", request.SelectedSetCodes);
        Assert.False(request.IncludeSideboardInAnalysis);
        Assert.False(request.IncludeMaybeboardInAnalysis);
        Assert.Equal("Commander", request.Format);
        Assert.True(request.StrategyNotes.Length > 1000);
        Assert.Contains("Arna Kennerüd Aura Engine", request.StrategyNotes, StringComparison.Ordinal);
        Assert.NotEmpty(request.DeckProfileJson);
        Assert.NotEmpty(request.SetUpgradeResponseJson);
        Assert.Equal(5, request.WorkflowStep);
    }

    private static MemoryStream BuildFixtureZip()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "00-input-summary.txt");
            WriteEntry(archive, "01-request-context.txt");
            WriteEntry(archive, "40-deck-profile.json");
            WriteEntry(archive, "51-set-upgrade-response.json");
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string fileName)
    {
        var entry = archive.CreateEntry(fileName);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(File.ReadAllText(Path.Combine(FixtureDirectory, fileName)));
    }
}
