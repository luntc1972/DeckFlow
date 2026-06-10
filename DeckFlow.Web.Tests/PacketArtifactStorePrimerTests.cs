using System.IO.Compression;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Regression coverage for deck-primer packet zip build/load behaviors.
/// </summary>
public sealed class PacketArtifactStorePrimerTests
{
    [Fact]
    public void BuildPrimerZip_then_LoadPrimerFromZip_round_trips_request_context()
    {
        var request = new DeckPrimerRequest
        {
            TargetCommanderBracket = "cEDH",
            TargetAiPlatform = "ChatGPT",
            SelectedSectionIds = ["core-identity", "mulligan", "matchups"]
        };

        var bytes = PacketArtifactStore.BuildPrimerZip(
            request,
            inputSummary: "primer input summary",
            requestContextText:
            """
            workflow_step: 2
            format: Commander
            deck_name: Kinnan Primer
            commander: Kinnan, Bonder Prodigy
            target_commander_bracket: cEDH
            target_ai_platform: ChatGPT
            selected_section_ids:
            - core-identity
            - mulligan
            - matchups
            deck_source:
            1 Kinnan, Bonder Prodigy
            """,
            chatGptPromptText: "chatgpt prompt",
            claudePromptText: "claude prompt",
            geminiPromptText: "gemini prompt",
            canonicalDeckListText: "1 Kinnan, Bonder Prodigy",
            originalDeckText: "1 Kinnan, Bonder Prodigy");

        var loaded = new DeckPrimerRequest();
        using var memoryStream = new MemoryStream(bytes);
        PacketArtifactStore.LoadPrimerFromZip(memoryStream, loaded);

        Assert.Equal("cEDH", loaded.TargetCommanderBracket);
        Assert.Equal("ChatGPT", loaded.TargetAiPlatform);
        Assert.Equal(["core-identity", "mulligan", "matchups"], loaded.SelectedSectionIds);
    }

    [Fact]
    public void BuildPrimerZip_writes_only_present_prompt_variants()
    {
        var request = new DeckPrimerRequest
        {
            TargetCommanderBracket = "cEDH",
            TargetAiPlatform = "Claude",
            SelectedSectionIds = ["core-identity", "mulligan"]
        };

        var bytes = PacketArtifactStore.BuildPrimerZip(
            request,
            inputSummary: "primer input summary",
            requestContextText:
            """
            target_commander_bracket: cEDH
            target_ai_platform: Claude
            selected_section_ids:
            - core-identity
            - mulligan
            """,
            chatGptPromptText: "chatgpt prompt",
            claudePromptText: "claude prompt",
            geminiPromptText: null,
            canonicalDeckListText: "1 Kinnan, Bonder Prodigy");

        using (var archiveStream = new MemoryStream(bytes))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false))
        {
            Assert.Contains(archive.Entries, entry => string.Equals(entry.FullName, "30-primer-chatgpt-prompt.txt", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(archive.Entries, entry => string.Equals(entry.FullName, "30-primer-claude-prompt.txt", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(archive.Entries, entry => string.Equals(entry.FullName, "30-primer-gemini-prompt.txt", StringComparison.OrdinalIgnoreCase));
        }

        var loaded = new DeckPrimerRequest();
        using var memoryStream = new MemoryStream(bytes);
        PacketArtifactStore.LoadPrimerFromZip(memoryStream, loaded);

        Assert.Equal("cEDH", loaded.TargetCommanderBracket);
        Assert.Equal("Claude", loaded.TargetAiPlatform);
        Assert.Equal(["core-identity", "mulligan"], loaded.SelectedSectionIds);
    }

    [Fact]
    public void BuildPrimerZip_writes_only_allowlisted_entries()
    {
        var request = new DeckPrimerRequest
        {
            TargetCommanderBracket = "Bracket 3",
            TargetAiPlatform = "Gemini",
            SelectedSectionIds = ["summary"]
        };

        var bytes = PacketArtifactStore.BuildPrimerZip(
            request,
            inputSummary: "primer input summary",
            requestContextText:
            """
            target_commander_bracket: Bracket 3
            target_ai_platform: Gemini
            selected_section_ids:
            - summary
            """,
            chatGptPromptText: "chatgpt prompt",
            claudePromptText: "claude prompt",
            geminiPromptText: "gemini prompt",
            canonicalDeckListText: "1 Atraxa, Praetors' Voice");

        using var archiveStream = new MemoryStream(bytes);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);
        var allowedNames = typeof(PacketArtifactStore)
            .GetField("PrimerAllowedNames", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?
            .GetValue(null) as IEnumerable<string>;

        Assert.NotNull(allowedNames);

        Assert.All(
            archive.Entries,
            entry => Assert.Contains(entry.FullName, allowedNames!, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void LoadPrimerFromZip_rejects_non_primer_entry_names()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("31-analysis-prompt.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("analysis prompt");
        }

        memoryStream.Position = 0;

        Assert.Throws<InvalidOperationException>(() =>
            PacketArtifactStore.LoadPrimerFromZip(memoryStream, new DeckPrimerRequest()));
    }
}
