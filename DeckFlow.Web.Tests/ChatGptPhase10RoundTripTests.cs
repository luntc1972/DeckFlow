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

    // ---- Comparison partial-zip handling (no response.json) ----

    [Fact]
    public void LoadComparisonFromZip_accepts_partial_zip_with_decks_but_no_response()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-a-list.txt"] = "1 Sol Ring\n",
            ["11-deck-b-list.txt"] = "1 Mana Crypt\n",
            ["01-request-context.txt"] = "target_ai_platform: Gemini\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal(2, loaded.WorkflowStep);
        Assert.Equal("1 Sol Ring", loaded.DeckASource);
        Assert.Equal("1 Mana Crypt", loaded.DeckBSource);
        Assert.Equal(string.Empty, loaded.ComparisonResponseJson);
        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_accepts_request_context_only_zip()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["01-request-context.txt"] = "target_ai_platform: Claude\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal(1, loaded.WorkflowStep);
        Assert.Equal(string.Empty, loaded.ComparisonResponseJson);
        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_restores_deck_names_and_brackets_from_request_context()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-a-list.txt"] = "1 Sol Ring\n",
            ["11-deck-b-list.txt"] = "1 Mana Crypt\n",
            ["01-request-context.txt"] = "deck_a_name: My Atraxa\ndeck_b_name: Their Kraum\ndeck_a_bracket: Cedh\ndeck_b_bracket: Optimized\ntarget_ai_platform: Claude\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("My Atraxa", loaded.DeckAName);
        Assert.Equal("Their Kraum", loaded.DeckBName);
        Assert.Equal("Cedh", loaded.DeckABracket);
        Assert.Equal("Optimized", loaded.DeckBBracket);
        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_returns_display_artifacts_for_view_model()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["00-comparison-input-summary.txt"] = "INPUT SUMMARY BODY\n",
            ["10-deck-a-list.txt"] = "Commander\n1 Atraxa, Praetors' Voice\n\nMainboard\n1 Sol Ring\n",
            ["11-deck-b-list.txt"] = "Commander\n1 Atraxa, Praetors' Voice\n\nMainboard\n1 Counterspell\n",
            ["12-deck-a-combos.txt"] = "DECK A COMBOS BODY\n",
            ["13-deck-b-combos.txt"] = "DECK B COMBOS BODY\n",
            ["20-comparison-context.txt"] = "COMPARISON CONTEXT BODY\n",
            ["30-comparison-prompt.txt"] = "COMPARISON PROMPT BODY\n",
            ["31-comparison-schema.json"] = "{\"comparison\":{}}",
            ["32-comparison-follow-up-prompt.txt"] = "FOLLOW-UP PROMPT BODY\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        var artifacts = ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("INPUT SUMMARY BODY", artifacts.InputSummary);
        Assert.Contains("Atraxa", artifacts.DeckAListText);
        Assert.Contains("Counterspell", artifacts.DeckBListText);
        Assert.Equal("DECK A COMBOS BODY", artifacts.DeckAComboText);
        Assert.Equal("DECK B COMBOS BODY", artifacts.DeckBComboText);
        Assert.Equal("COMPARISON CONTEXT BODY", artifacts.ComparisonContextText);
        Assert.Equal("COMPARISON PROMPT BODY", artifacts.ComparisonPromptText);
        Assert.Equal("{\"comparison\":{}}", artifacts.ComparisonSchemaJson);
        Assert.Equal("FOLLOW-UP PROMPT BODY", artifacts.FollowUpPromptText);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_returns_display_artifacts_for_view_model()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["00-input-summary.txt"] = "META-GAP INPUT SUMMARY\n",
            ["30-meta-gap-prompt.txt"] = "META-GAP PROMPT BODY\n",
            ["31-meta-gap-schema.json"] = "{\"meta_gap\":{}}",
            ["01-request-context.txt"] = "target_ai_platform: Claude\ncommander: Kinnan, Bonder Prodigy\n"
        });

        var loaded = new ChatGptCedhMetaGapRequest();
        using var stream = new MemoryStream(bytes);
        var artifacts = ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("META-GAP INPUT SUMMARY", artifacts.InputSummary);
        Assert.Equal("META-GAP PROMPT BODY", artifacts.PromptText);
        Assert.Equal("{\"meta_gap\":{}}", artifacts.SchemaJson);
    }

    [Fact]
    public void LoadComparisonFromZip_throws_when_zip_has_no_recognized_entries()
    {
        // Empty zip: passes the allowlist gate (no unsupported entries) but has
        // none of the recognized files either, so the partial-zip throw fires.
        var bytes = BuildRawZip(new Dictionary<string, string>());

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        var exception = Assert.Throws<InvalidOperationException>(
            () => ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded));
        Assert.Contains("recognized DeckFlow comparison session", exception.Message);
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

    // ---- CedhMetaGap partial-zip handling (no response.json) ----

    [Fact]
    public void LoadCedhMetaGapFromZip_accepts_request_context_only_zip()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["01-request-context.txt"] = "target_ai_platform: Gemini\n"
        });

        var loaded = new ChatGptCedhMetaGapRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal(1, loaded.WorkflowStep);
        Assert.Equal(string.Empty, loaded.MetaGapResponseJson);
        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_restores_commander_from_request_context()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["01-request-context.txt"] = "commander: Yuriko, the Tiger's Shadow\ntarget_ai_platform: Gemini\n"
        });

        var loaded = new ChatGptCedhMetaGapRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Yuriko, the Tiger's Shadow", loaded.CommanderName);
        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_throws_when_zip_has_no_recognized_entries()
    {
        // Empty zip: passes the allowlist gate (no unsupported entries) but has
        // none of the recognized files either, so the partial-zip throw fires.
        var bytes = BuildRawZip(new Dictionary<string, string>());

        var loaded = new ChatGptCedhMetaGapRequest();
        using var stream = new MemoryStream(bytes);
        var exception = Assert.Throws<InvalidOperationException>(
            () => ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded));
        Assert.Contains("recognized DeckFlow meta-gap session", exception.Message);
    }

    // ---- Hybrid storage: original deck text artifact ----

    [Fact]
    public void Comparison_OriginalDeckText_RoundTrips_WhenPasted()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-a-list.txt"] = "Commander\n1 Atraxa\n\nMainboard\n1 Sol Ring\n",
            ["10b-deck-a-original.txt"] = "1 Atraxa, Praetors' Voice\n1 Sol Ring\n1 Arcane Signet\n",
            ["11-deck-b-list.txt"] = "Commander\n1 Atraxa\n\nMainboard\n1 Counterspell\n",
            ["11b-deck-b-original.txt"] = "1 Atraxa, Praetors' Voice\n1 Counterspell\n1 Cyclonic Rift\n",
            ["01-request-context.txt"] = "target_ai_platform: Gemini\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        // Original-prefers-canonical: form fields get the user's pasted text.
        Assert.Contains("1 Arcane Signet", loaded.DeckASource);
        Assert.DoesNotContain("Commander\n", loaded.DeckASource);
        Assert.Contains("1 Cyclonic Rift", loaded.DeckBSource);
    }

    [Fact]
    public void Comparison_FallsBackToCanonical_WhenOriginalMissing()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-a-list.txt"] = "Commander\n1 Atraxa\n\nMainboard\n1 Sol Ring\n",
            ["11-deck-b-list.txt"] = "Commander\n1 Atraxa\n\nMainboard\n1 Counterspell\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Contains("Commander", loaded.DeckASource);
        Assert.Contains("1 Sol Ring", loaded.DeckASource);
        Assert.Contains("1 Counterspell", loaded.DeckBSource);
    }

    [Fact]
    public void CedhMetaGap_OriginalDeckText_RoundTrips_WhenPasted()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-list.txt"] = "Commander\n1 Kinnan, Bonder Prodigy\n\nMainboard\n1 Sol Ring\n",
            ["10b-deck-original.txt"] = "1 Kinnan, Bonder Prodigy\n1 Sol Ring\n1 Llanowar Elves\n",
            ["01-request-context.txt"] = "target_ai_platform: Claude\ncommander: Kinnan, Bonder Prodigy\n"
        });

        var loaded = new ChatGptCedhMetaGapRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Contains("1 Llanowar Elves", loaded.DeckSource);
        Assert.DoesNotContain("Commander\n", loaded.DeckSource);
    }

    [Fact]
    public void CedhMetaGap_FallsBackToCanonical_WhenOriginalMissing()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-list.txt"] = "Commander\n1 Kinnan, Bonder Prodigy\n\nMainboard\n1 Sol Ring\n",
            ["01-request-context.txt"] = "commander: Kinnan, Bonder Prodigy\n"
        });

        var loaded = new ChatGptCedhMetaGapRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Contains("Commander", loaded.DeckSource);
        Assert.Contains("1 Sol Ring", loaded.DeckSource);
    }

    [Fact]
    public void OriginalDeckTextOrNull_ReturnsNull_ForMoxfieldAndArchidektUrls()
    {
        Assert.Null(ChatGptPacketArtifactStore.OriginalDeckTextOrNull("https://www.moxfield.com/decks/abc"));
        Assert.Null(ChatGptPacketArtifactStore.OriginalDeckTextOrNull("https://archidekt.com/decks/123"));
    }

    [Fact]
    public void OriginalDeckTextOrNull_ReturnsTextUnchanged_ForRawDeckText()
    {
        Assert.Equal("1 Sol Ring\n1 Mana Crypt", ChatGptPacketArtifactStore.OriginalDeckTextOrNull("1 Sol Ring\n1 Mana Crypt"));
        Assert.Equal("Commander\n1 Atraxa", ChatGptPacketArtifactStore.OriginalDeckTextOrNull("Commander\n1 Atraxa"));
    }

    [Fact]
    public void OriginalDeckTextOrNull_ReturnsTextUnchanged_ForOtherHosts()
    {
        // Unsupported hosts (Pastebin, GitHub Gist, etc.) should preserve original text —
        // they'll fall through to the text parser at deck-load time, so the original
        // is the more user-recognizable source.
        Assert.Equal("https://pastebin.com/raw/abc", ChatGptPacketArtifactStore.OriginalDeckTextOrNull("https://pastebin.com/raw/abc"));
    }

    [Fact]
    public void Packets_OriginalDeckText_OverridesCanonicalAndRequestContextDeckSource()
    {
        // Precedence contract: 10b-deck-original.txt (user-pasted) wins over
        // 10-deck-list.txt (canonical) wins over deck_source: block in
        // 01-request-context.txt (legacy path). All three carry distinct text
        // so the loader's winning source is unambiguous.
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10b-deck-original.txt"] = "1 Atraxa, Praetors' Voice\n1 Sol Ring\n",
            ["10-deck-list.txt"] = "Commander\n1 Atraxa, Praetors' Voice\n\nMainboard\n1 Mana Crypt\n",
            ["01-request-context.txt"] = "deck_source:\n1 SOMETHING ELSE\n1 ANOTHER STALE LINE\n",
            ["40-deck-profile.json"] = "{\"commander\":\"Atraxa\"}"
        });

        var loaded = new ChatGptDeckRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadFromZip(stream, loaded);

        Assert.Contains("1 Sol Ring", loaded.DeckText);
        Assert.DoesNotContain("Mana Crypt", loaded.DeckText);
        Assert.DoesNotContain("SOMETHING ELSE", loaded.DeckText);
    }

    [Fact]
    public void Packets_CanonicalDeckList_WinsOver_RequestContextDeckSource_WhenOriginalMissing()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10-deck-list.txt"] = "Commander\n1 Atraxa\n\nMainboard\n1 Mana Crypt\n",
            ["01-request-context.txt"] = "deck_source:\n1 STALE FROM REQUEST CONTEXT\n",
            ["40-deck-profile.json"] = "{\"commander\":\"Atraxa\"}"
        });

        var loaded = new ChatGptDeckRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadFromZip(stream, loaded);

        Assert.Contains("Mana Crypt", loaded.DeckText);
        Assert.DoesNotContain("STALE", loaded.DeckText);
    }

    [Fact]
    public void Packets_RequestContextDeckSource_UsedAsLastResort()
    {
        // Legacy zip path: no canonical or original deck artifact, only
        // deck_source inside request-context. The loader still restores it.
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["01-request-context.txt"] = "deck_source:\n1 Sol Ring\n1 Arcane Signet\n",
            ["40-deck-profile.json"] = "{\"commander\":\"Atraxa\"}"
        });

        var loaded = new ChatGptDeckRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadFromZip(stream, loaded);

        Assert.Contains("Sol Ring", loaded.DeckText);
        Assert.Contains("Arcane Signet", loaded.DeckText);
    }

    [Fact]
    public void Comparison_WorkflowStep_IsTwo_WhenOnlyOriginalsPresent()
    {
        var bytes = BuildRawZip(new Dictionary<string, string>
        {
            ["10b-deck-a-original.txt"] = "1 Atraxa\n1 Sol Ring\n",
            ["11b-deck-b-original.txt"] = "1 Atraxa\n1 Counterspell\n"
        });

        var loaded = new ChatGptDeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        ChatGptPacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal(2, loaded.WorkflowStep);
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

    // ---- Filename suggestion (AI name embedded in download filename) ----

    [Theory]
    [InlineData("ChatGPT", "chatgpt")]
    [InlineData("Claude", "claude")]
    [InlineData("Gemini", "gemini")]
    public void SuggestPacketZipFileName_includes_lowercased_ai_name(string platform, string expectedSegment)
    {
        var fileName = ChatGptPacketArtifactStore.SuggestPacketZipFileName("Atraxa", platform);
        Assert.Contains($"-{expectedSegment}-", fileName);
        Assert.Contains("atraxa", fileName);
        Assert.EndsWith(".zip", fileName);
    }

    [Theory]
    [InlineData("ChatGPT", "chatgpt")]
    [InlineData("Claude", "claude")]
    [InlineData("Gemini", "gemini")]
    public void SuggestComparisonZipFileName_includes_lowercased_ai_name(string platform, string expectedSegment)
    {
        var fileName = ChatGptPacketArtifactStore.SuggestComparisonZipFileName("Atraxa", platform);
        Assert.Contains($"-{expectedSegment}-", fileName);
        Assert.StartsWith("atraxa-compare2-", fileName);
    }

    [Theory]
    [InlineData("ChatGPT", "chatgpt")]
    [InlineData("Claude", "claude")]
    [InlineData("Gemini", "gemini")]
    public void SuggestCedhMetaGapZipFileName_includes_lowercased_ai_name(string platform, string expectedSegment)
    {
        var fileName = ChatGptPacketArtifactStore.SuggestCedhMetaGapZipFileName("Atraxa", platform);
        Assert.Contains($"-{expectedSegment}-", fileName);
    }

    [Fact]
    public void SuggestPacketZipFileName_falls_back_to_chatgpt_when_platform_null()
    {
        var fileName = ChatGptPacketArtifactStore.SuggestPacketZipFileName("Atraxa", null);
        Assert.Contains("-chatgpt-", fileName);
    }

    [Fact]
    public void SuggestPacketZipFileName_filenames_are_distinct_per_ai_platform()
    {
        // Two different platforms must produce filenames distinguishable by AI
        // segment regardless of the timestamp suffix matching exactly.
        var claudeName = ChatGptPacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Claude");
        var geminiName = ChatGptPacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Gemini");
        Assert.Contains("-claude-", claudeName);
        Assert.Contains("-gemini-", geminiName);
        Assert.DoesNotContain("-claude-", geminiName);
        Assert.DoesNotContain("-gemini-", claudeName);
    }

    // ---- Page-identity segment in zip filenames ----

    [Fact]
    public void SuggestPacketZipFileName_includes_analysis_page_segment()
    {
        var fileName = ChatGptPacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Claude");
        Assert.Contains("-analysis-", fileName);
        Assert.DoesNotContain("-compare2-", fileName);
        Assert.DoesNotContain("-cedh-", fileName);
    }

    [Fact]
    public void SuggestComparisonZipFileName_includes_compare2_page_segment()
    {
        var fileName = ChatGptPacketArtifactStore.SuggestComparisonZipFileName("Atraxa", "Gemini");
        Assert.Contains("-compare2-", fileName);
        Assert.DoesNotContain("-analysis-", fileName);
        Assert.DoesNotContain("-cedh-", fileName);
    }

    [Fact]
    public void SuggestCedhMetaGapZipFileName_includes_cedh_page_segment()
    {
        var fileName = ChatGptPacketArtifactStore.SuggestCedhMetaGapZipFileName("Atraxa", "ChatGPT");
        Assert.Contains("-cedh-", fileName);
        Assert.DoesNotContain("-analysis-", fileName);
        Assert.DoesNotContain("-compare2-", fileName);
    }

    [Fact]
    public void SuggestZipFileNames_page_segments_are_distinct_across_pages()
    {
        var packet = ChatGptPacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Claude");
        var comparison = ChatGptPacketArtifactStore.SuggestComparisonZipFileName("Atraxa", "Claude");
        var metaGap = ChatGptPacketArtifactStore.SuggestCedhMetaGapZipFileName("Atraxa", "Claude");
        Assert.NotEqual(packet, comparison);
        Assert.NotEqual(packet, metaGap);
        Assert.NotEqual(comparison, metaGap);
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

    private static byte[] BuildRawZip(IDictionary<string, string> entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, contents) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(contents);
            }
        }
        return memory.ToArray();
    }
}
