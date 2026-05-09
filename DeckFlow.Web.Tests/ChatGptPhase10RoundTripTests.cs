using System.IO.Compression;
using System.Text;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Phase 10 (10-03) round-trip coverage for the new <c>01-request-context.txt</c>
/// envelope on Comparison + CedhMetaGap zips, plus the per-service
/// <c>BuildRequestContextText</c> writers and their parser-symmetric reads.
/// </summary>
public sealed class ChatGptPhase10RoundTripTests
{
    // ---- Comparison zip round-trip ----

    [Fact]
    public void BuildComparisonZip_writes_request_context_entry_when_provided()
    {
        var request = new ChatGptDeckComparisonRequest
        {
            ComparisonResponseJson = "{\"deck_a\":{},\"deck_b\":{}}"
        };

        var bytes = ChatGptPacketArtifactStore.BuildComparisonZip(
            request,
            inputSummary: "summary",
            deckAListText: "deck a list",
            deckBListText: "deck b list",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonPromptText: "prompt",
            followUpPromptText: "follow up",
            comparisonSchemaJson: "{}",
            requestContextText: "target_ai_platform: Claude\n");

        var entries = ReadZipEntries(bytes);
        Assert.True(entries.ContainsKey("01-request-context.txt"));
        Assert.Contains("target_ai_platform: Claude", entries["01-request-context.txt"]);
    }

    [Fact]
    public void BuildComparisonZip_omits_request_context_entry_when_null()
    {
        var request = new ChatGptDeckComparisonRequest
        {
            ComparisonResponseJson = "{\"deck_a\":{},\"deck_b\":{}}"
        };

        var bytes = ChatGptPacketArtifactStore.BuildComparisonZip(
            request,
            inputSummary: "summary",
            deckAListText: "deck a",
            deckBListText: "deck b",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonPromptText: "prompt",
            followUpPromptText: "follow up",
            comparisonSchemaJson: "{}",
            requestContextText: null);

        var entries = ReadZipEntries(bytes);
        Assert.False(entries.ContainsKey("01-request-context.txt"));
    }

    [Fact]
    public void LoadComparisonFromZip_restores_target_ai_platform_when_present()
    {
        var bytes = BuildComparisonZipWithRequestContext("target_ai_platform: Gemini\n");

        var loaded = new ChatGptDeckComparisonRequest { TargetAiPlatform = "ChatGPT" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_does_not_modify_target_ai_platform_when_request_context_missing()
    {
        var bytes = BuildComparisonZipWithoutRequestContext();

        var loaded = new ChatGptDeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_ignores_blank_request_context_entry()
    {
        var bytes = BuildComparisonZipWithRequestContext("   \n");

        var loaded = new ChatGptDeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_ignores_request_context_without_target_ai_platform_key()
    {
        var bytes = BuildComparisonZipWithRequestContext("deck_a_name: My Deck\nworkflow_step: 2\n");

        var loaded = new ChatGptDeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_normalizes_invalid_target_ai_platform_to_chatgpt()
    {
        // A crafted zip with an out-of-set platform value must not leave the
        // request holding an invalid string (which would render the AI selector
        // with no radio checked). The model setter normalizes via Phase 10 hardening.
        var bytes = BuildComparisonZipWithRequestContext("target_ai_platform: SomethingInvalid\n");

        var loaded = new ChatGptDeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("ChatGPT", loaded.TargetAiPlatform);
    }

    // ---- CedhMetaGap zip round-trip ----

    [Fact]
    public void BuildCedhMetaGapZip_writes_request_context_entry_when_provided()
    {
        var request = new ChatGptCedhMetaGapRequest
        {
            MetaGapResponseJson = "{\"meta_gap\":{}}"
        };

        var bytes = ChatGptPacketArtifactStore.BuildCedhMetaGapZip(
            request,
            inputSummary: "summary",
            promptText: "prompt",
            schemaJson: "{}",
            requestContextText: "target_ai_platform: Claude\n");

        var entries = ReadZipEntries(bytes);
        Assert.True(entries.ContainsKey("01-request-context.txt"));
        Assert.Contains("target_ai_platform: Claude", entries["01-request-context.txt"]);
    }

    [Fact]
    public void BuildCedhMetaGapZip_omits_request_context_entry_when_null()
    {
        var request = new ChatGptCedhMetaGapRequest
        {
            MetaGapResponseJson = "{\"meta_gap\":{}}"
        };

        var bytes = ChatGptPacketArtifactStore.BuildCedhMetaGapZip(
            request,
            inputSummary: "summary",
            promptText: "prompt",
            schemaJson: "{}",
            requestContextText: null);

        var entries = ReadZipEntries(bytes);
        Assert.False(entries.ContainsKey("01-request-context.txt"));
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_restores_target_ai_platform_when_present()
    {
        var bytes = BuildCedhMetaGapZipWithRequestContext("target_ai_platform: Gemini\n");

        var loaded = new ChatGptCedhMetaGapRequest { TargetAiPlatform = "ChatGPT" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_does_not_modify_target_ai_platform_when_request_context_missing()
    {
        var bytes = BuildCedhMetaGapZipWithoutRequestContext();

        var loaded = new ChatGptCedhMetaGapRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_ignores_blank_request_context_entry()
    {
        var bytes = BuildCedhMetaGapZipWithRequestContext("   \n");

        var loaded = new ChatGptCedhMetaGapRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_normalizes_invalid_target_ai_platform_to_chatgpt()
    {
        var bytes = BuildCedhMetaGapZipWithRequestContext("target_ai_platform: BogusValue\n");

        var loaded = new ChatGptCedhMetaGapRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("ChatGPT", loaded.TargetAiPlatform);
    }

    // ---- Comparison BuildRequestContextText writer ----

    [Fact]
    public void Comparison_BuildRequestContextText_emits_all_expected_keys()
    {
        var request = new ChatGptDeckComparisonRequest
        {
            WorkflowStep = 2,
            DeckAName = "My Atraxa",
            DeckBName = "Their Kraum",
            DeckABracket = "Cedh",
            DeckBBracket = "Optimized",
            TargetAiPlatform = "Claude"
        };

        var text = ChatGptDeckComparisonService.BuildRequestContextText(request);

        Assert.Contains("workflow_step: 2", text);
        Assert.Contains("deck_a_name: My Atraxa", text);
        Assert.Contains("deck_b_name: Their Kraum", text);
        Assert.Contains("deck_a_bracket: Cedh", text);
        Assert.Contains("deck_b_bracket: Optimized", text);
        Assert.Contains("target_ai_platform: Claude", text);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_falls_back_to_chatgpt_when_target_ai_platform_blank()
    {
        // Setter normalizes empty-string to "ChatGPT" already via Phase 9 plumbing,
        // but the writer's own fallback also defends against direct field manipulation.
        var request = new ChatGptDeckComparisonRequest();
        var text = ChatGptDeckComparisonService.BuildRequestContextText(request);
        Assert.Contains("target_ai_platform: ChatGPT", text);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_normalizes_newlines_in_field_values()
    {
        var request = new ChatGptDeckComparisonRequest
        {
            DeckAName = "Multi\nline\rDeck Name"
        };
        var text = ChatGptDeckComparisonService.BuildRequestContextText(request);
        Assert.Contains("deck_a_name: Multi line Deck Name", text);
        Assert.DoesNotContain("Multi\nline", text);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_round_trips_target_ai_platform_through_parser()
    {
        var request = new ChatGptDeckComparisonRequest
        {
            TargetAiPlatform = "Gemini",
            DeckAName = "A",
            DeckBName = "B"
        };
        var text = ChatGptDeckComparisonService.BuildRequestContextText(request);
        var parsed = ChatGptRequestContextParser.Parse(text);
        Assert.Equal("Gemini", parsed.TargetAiPlatform);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_throws_on_null_request()
    {
        Assert.Throws<ArgumentNullException>(() => ChatGptDeckComparisonService.BuildRequestContextText(null!));
    }

    // ---- CedhMetaGap BuildRequestContextText writer ----

    [Fact]
    public void CedhMetaGap_BuildRequestContextText_emits_all_expected_keys()
    {
        var request = new ChatGptCedhMetaGapRequest
        {
            WorkflowStep = 3,
            CommanderName = "Kraum, Ludevic's Opus",
            TargetAiPlatform = "Claude"
        };
        var text = ChatGptCedhMetaGapService.BuildRequestContextText(request);
        Assert.Contains("workflow_step: 3", text);
        Assert.Contains("commander: Kraum, Ludevic's Opus", text);
        Assert.Contains("target_ai_platform: Claude", text);
    }

    [Fact]
    public void CedhMetaGap_BuildRequestContextText_round_trips_target_ai_platform_through_parser()
    {
        var request = new ChatGptCedhMetaGapRequest
        {
            CommanderName = "Atraxa",
            TargetAiPlatform = "Gemini"
        };
        var text = ChatGptCedhMetaGapService.BuildRequestContextText(request);
        var parsed = ChatGptRequestContextParser.Parse(text);
        Assert.Equal("Gemini", parsed.TargetAiPlatform);
    }

    [Fact]
    public void CedhMetaGap_BuildRequestContextText_throws_on_null_request()
    {
        Assert.Throws<ArgumentNullException>(() => ChatGptCedhMetaGapService.BuildRequestContextText(null!));
    }

    // ---- helpers ----

    private static byte[] BuildComparisonZipWithRequestContext(string requestContextText)
    {
        var request = new ChatGptDeckComparisonRequest
        {
            ComparisonResponseJson = "{\"deck_a\":{},\"deck_b\":{}}"
        };

        return ChatGptPacketArtifactStore.BuildComparisonZip(
            request,
            inputSummary: "summary",
            deckAListText: "deck a",
            deckBListText: "deck b",
            deckAComboText: string.Empty,
            deckBComboText: string.Empty,
            comparisonContextText: "context",
            comparisonPromptText: "prompt",
            followUpPromptText: "follow up",
            comparisonSchemaJson: "{}",
            requestContextText: requestContextText);
    }

    private static byte[] BuildComparisonZipWithoutRequestContext()
        => BuildComparisonZipWithRequestContext(null!);

    private static byte[] BuildCedhMetaGapZipWithRequestContext(string requestContextText)
    {
        var request = new ChatGptCedhMetaGapRequest
        {
            MetaGapResponseJson = "{\"meta_gap\":{}}"
        };

        return ChatGptPacketArtifactStore.BuildCedhMetaGapZip(
            request,
            inputSummary: "summary",
            promptText: "prompt",
            schemaJson: "{}",
            requestContextText: requestContextText);
    }

    private static byte[] BuildCedhMetaGapZipWithoutRequestContext()
        => BuildCedhMetaGapZipWithRequestContext(null!);

    private static Dictionary<string, string> ReadZipEntries(byte[] bytes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            result[entry.FullName] = reader.ReadToEnd();
        }
        return result;
    }
}
