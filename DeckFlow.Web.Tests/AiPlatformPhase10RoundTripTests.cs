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
public sealed class AiPlatformPhase10RoundTripTests
{
    // ---- Comparison zip round-trip ----

    [Fact]
    public void BuildComparisonZip_writes_request_context_entry_when_provided()
    {
        var request = new DeckComparisonRequest
        {
            ComparisonResponseJson = "{\"deck_a\":{},\"deck_b\":{}}"
        };

        var bytes = PacketArtifactStore.BuildComparisonZip(
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
        var request = new DeckComparisonRequest
        {
            ComparisonResponseJson = "{\"deck_a\":{},\"deck_b\":{}}"
        };

        var bytes = PacketArtifactStore.BuildComparisonZip(
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

        var loaded = new DeckComparisonRequest { TargetAiPlatform = "ChatGPT" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_does_not_modify_target_ai_platform_when_request_context_missing()
    {
        var bytes = BuildComparisonZipWithoutRequestContext();

        var loaded = new DeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_ignores_blank_request_context_entry()
    {
        var bytes = BuildComparisonZipWithRequestContext("   \n");

        var loaded = new DeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_ignores_request_context_without_target_ai_platform_key()
    {
        var bytes = BuildComparisonZipWithRequestContext("deck_a_name: My Deck\nworkflow_step: 2\n");

        var loaded = new DeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadComparisonFromZip_normalizes_invalid_target_ai_platform_to_chatgpt()
    {
        // A crafted zip with an out-of-set platform value must not leave the
        // request holding an invalid string (which would render the AI selector
        // with no radio checked). The model setter normalizes via Phase 10 hardening.
        var bytes = BuildComparisonZipWithRequestContext("target_ai_platform: SomethingInvalid\n");

        var loaded = new DeckComparisonRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        var artifacts = PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new MetaGapRequest();
        using var stream = new MemoryStream(bytes);
        var artifacts = PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        var exception = Assert.Throws<InvalidOperationException>(
            () => PacketArtifactStore.LoadComparisonFromZip(stream, loaded));
        Assert.Contains("recognized DeckFlow comparison session", exception.Message);
    }

    // ---- CedhMetaGap zip round-trip ----

    [Fact]
    public void BuildCedhMetaGapZip_writes_request_context_entry_when_provided()
    {
        var request = new MetaGapRequest
        {
            MetaGapResponseJson = "{\"meta_gap\":{}}"
        };

        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
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
        var request = new MetaGapRequest
        {
            MetaGapResponseJson = "{\"meta_gap\":{}}"
        };

        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
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

        var loaded = new MetaGapRequest { TargetAiPlatform = "ChatGPT" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_does_not_modify_target_ai_platform_when_request_context_missing()
    {
        var bytes = BuildCedhMetaGapZipWithoutRequestContext();

        var loaded = new MetaGapRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_ignores_blank_request_context_entry()
    {
        var bytes = BuildCedhMetaGapZipWithRequestContext("   \n");

        var loaded = new MetaGapRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Claude", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_normalizes_invalid_target_ai_platform_to_chatgpt()
    {
        var bytes = BuildCedhMetaGapZipWithRequestContext("target_ai_platform: BogusValue\n");

        var loaded = new MetaGapRequest { TargetAiPlatform = "Claude" };
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

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

        var loaded = new MetaGapRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

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

        var loaded = new MetaGapRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Equal("Yuriko, the Tiger's Shadow", loaded.CommanderName);
        Assert.Equal("Gemini", loaded.TargetAiPlatform);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_throws_when_zip_has_no_recognized_entries()
    {
        // Empty zip: passes the allowlist gate (no unsupported entries) but has
        // none of the recognized files either, so the partial-zip throw fires.
        var bytes = BuildRawZip(new Dictionary<string, string>());

        var loaded = new MetaGapRequest();
        using var stream = new MemoryStream(bytes);
        var exception = Assert.Throws<InvalidOperationException>(
            () => PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded));
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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

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

        var loaded = new MetaGapRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

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

        var loaded = new MetaGapRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadCedhMetaGapFromZip(stream, loaded);

        Assert.Contains("Commander", loaded.DeckSource);
        Assert.Contains("1 Sol Ring", loaded.DeckSource);
    }

    [Fact]
    public void OriginalDeckTextOrNull_ReturnsNull_ForMoxfieldAndArchidektUrls()
    {
        Assert.Null(PacketArtifactStore.OriginalDeckTextOrNull("https://www.moxfield.com/decks/abc"));
        Assert.Null(PacketArtifactStore.OriginalDeckTextOrNull("https://archidekt.com/decks/123"));
    }

    [Fact]
    public void OriginalDeckTextOrNull_ReturnsTextUnchanged_ForRawDeckText()
    {
        Assert.Equal("1 Sol Ring\n1 Mana Crypt", PacketArtifactStore.OriginalDeckTextOrNull("1 Sol Ring\n1 Mana Crypt"));
        Assert.Equal("Commander\n1 Atraxa", PacketArtifactStore.OriginalDeckTextOrNull("Commander\n1 Atraxa"));
    }

    [Fact]
    public void OriginalDeckTextOrNull_ReturnsTextUnchanged_ForOtherHosts()
    {
        // Unsupported hosts (Pastebin, GitHub Gist, etc.) should preserve original text —
        // they'll fall through to the text parser at deck-load time, so the original
        // is the more user-recognizable source.
        Assert.Equal("https://pastebin.com/raw/abc", PacketArtifactStore.OriginalDeckTextOrNull("https://pastebin.com/raw/abc"));
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

        var loaded = new DeckAnalysisRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadFromZip(stream, loaded);

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

        var loaded = new DeckAnalysisRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadFromZip(stream, loaded);

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

        var loaded = new DeckAnalysisRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadFromZip(stream, loaded);

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

        var loaded = new DeckComparisonRequest();
        using var stream = new MemoryStream(bytes);
        PacketArtifactStore.LoadComparisonFromZip(stream, loaded);

        Assert.Equal(2, loaded.WorkflowStep);
    }

    // ---- Comparison BuildRequestContextText writer ----

    [Fact]
    public void Comparison_BuildRequestContextText_emits_all_expected_keys()
    {
        var request = new DeckComparisonRequest
        {
            WorkflowStep = 2,
            DeckAName = "My Atraxa",
            DeckBName = "Their Kraum",
            DeckABracket = "Cedh",
            DeckBBracket = "Optimized",
            TargetAiPlatform = "Claude"
        };

        var text = DeckComparisonService.BuildRequestContextText(request);

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
        var request = new DeckComparisonRequest();
        var text = DeckComparisonService.BuildRequestContextText(request);
        Assert.Contains("target_ai_platform: ChatGPT", text);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_normalizes_newlines_in_field_values()
    {
        var request = new DeckComparisonRequest
        {
            DeckAName = "Multi\nline\rDeck Name"
        };
        var text = DeckComparisonService.BuildRequestContextText(request);
        Assert.Contains("deck_a_name: Multi line Deck Name", text);
        Assert.DoesNotContain("Multi\nline", text);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_round_trips_target_ai_platform_through_parser()
    {
        var request = new DeckComparisonRequest
        {
            TargetAiPlatform = "Gemini",
            DeckAName = "A",
            DeckBName = "B"
        };
        var text = DeckComparisonService.BuildRequestContextText(request);
        var parsed = RequestContextParser.Parse(text);
        Assert.Equal("Gemini", parsed.TargetAiPlatform);
    }

    [Fact]
    public void Comparison_BuildRequestContextText_throws_on_null_request()
    {
        Assert.Throws<ArgumentNullException>(() => DeckComparisonService.BuildRequestContextText(null!));
    }

    // ---- CedhMetaGap BuildRequestContextText writer ----

    [Fact]
    public void CedhMetaGap_BuildRequestContextText_emits_all_expected_keys()
    {
        var request = new MetaGapRequest
        {
            WorkflowStep = 3,
            CommanderName = "Kraum, Ludevic's Opus",
            TargetAiPlatform = "Claude"
        };
        var text = MetaGapService.BuildRequestContextText(request);
        Assert.Contains("workflow_step: 3", text);
        Assert.Contains("commander: Kraum, Ludevic's Opus", text);
        Assert.Contains("target_ai_platform: Claude", text);
    }

    [Fact]
    public void CedhMetaGap_BuildRequestContextText_round_trips_target_ai_platform_through_parser()
    {
        var request = new MetaGapRequest
        {
            CommanderName = "Atraxa",
            TargetAiPlatform = "Gemini"
        };
        var text = MetaGapService.BuildRequestContextText(request);
        var parsed = RequestContextParser.Parse(text);
        Assert.Equal("Gemini", parsed.TargetAiPlatform);
    }

    [Fact]
    public void CedhMetaGap_BuildRequestContextText_throws_on_null_request()
    {
        Assert.Throws<ArgumentNullException>(() => MetaGapService.BuildRequestContextText(null!));
    }

    // ---- Filename suggestion (AI name embedded in download filename) ----

    public static IEnumerable<object[]> AllPlatforms()
    {
        foreach (var platform in AiPlatform.All)
        {
            yield return new object[] { platform.Key };
        }
    }

    [Theory]
    [MemberData(nameof(AllPlatforms))]
    public void SuggestPacketZipFileName_includes_lowercased_ai_name(string platform)
    {
        var expectedSegment = platform.ToLowerInvariant();
        var fileName = PacketArtifactStore.SuggestPacketZipFileName("Atraxa", platform);
        Assert.Contains($"-{expectedSegment}-", fileName);
        Assert.Contains("atraxa", fileName);
        Assert.EndsWith(".zip", fileName);
    }

    [Theory]
    [MemberData(nameof(AllPlatforms))]
    public void SuggestComparisonZipFileName_includes_lowercased_ai_name(string platform)
    {
        var expectedSegment = platform.ToLowerInvariant();
        var fileName = PacketArtifactStore.SuggestComparisonZipFileName("Atraxa", platform);
        Assert.Contains($"-{expectedSegment}-", fileName);
        Assert.StartsWith("atraxa-comparison-", fileName);
    }

    [Theory]
    [MemberData(nameof(AllPlatforms))]
    public void SuggestCedhMetaGapZipFileName_includes_lowercased_ai_name(string platform)
    {
        var expectedSegment = platform.ToLowerInvariant();
        var fileName = PacketArtifactStore.SuggestCedhMetaGapZipFileName("Atraxa", platform);
        Assert.Contains($"-{expectedSegment}-", fileName);
    }

    [Fact]
    public void SuggestPacketZipFileName_falls_back_to_chatgpt_when_platform_null()
    {
        var fileName = PacketArtifactStore.SuggestPacketZipFileName("Atraxa", null);
        Assert.Contains("-chatgpt-", fileName);
    }

    [Fact]
    public void SuggestPacketZipFileName_filenames_are_distinct_per_ai_platform()
    {
        // Two different platforms must produce filenames distinguishable by AI
        // segment regardless of the timestamp suffix matching exactly.
        var claudeName = PacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Claude");
        var geminiName = PacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Gemini");
        Assert.Contains("-claude-", claudeName);
        Assert.Contains("-gemini-", geminiName);
        Assert.DoesNotContain("-claude-", geminiName);
        Assert.DoesNotContain("-gemini-", claudeName);
    }

    // ---- Page-identity segment in zip filenames ----

    [Fact]
    public void SuggestPacketZipFileName_includes_analysis_page_segment()
    {
        var fileName = PacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Claude");
        Assert.Contains("-analysis-", fileName);
        Assert.DoesNotContain("-compare2-", fileName);
        Assert.DoesNotContain("-cedh-", fileName);
    }

    [Fact]
    public void SuggestComparisonZipFileName_includes_compare2_page_segment()
    {
        var fileName = PacketArtifactStore.SuggestComparisonZipFileName("Atraxa", "Gemini");
        Assert.Contains("-compare2-", fileName);
        Assert.DoesNotContain("-analysis-", fileName);
        Assert.DoesNotContain("-cedh-", fileName);
    }

    [Fact]
    public void SuggestCedhMetaGapZipFileName_includes_cedh_page_segment()
    {
        var fileName = PacketArtifactStore.SuggestCedhMetaGapZipFileName("Atraxa", "ChatGPT");
        Assert.Contains("-cedh-", fileName);
        Assert.DoesNotContain("-analysis-", fileName);
        Assert.DoesNotContain("-compare2-", fileName);
    }

    [Fact]
    public void SuggestZipFileNames_page_segments_are_distinct_across_pages()
    {
        var packet = PacketArtifactStore.SuggestPacketZipFileName("Atraxa", "Claude");
        var comparison = PacketArtifactStore.SuggestComparisonZipFileName("Atraxa", "Claude");
        var metaGap = PacketArtifactStore.SuggestCedhMetaGapZipFileName("Atraxa", "Claude");
        Assert.NotEqual(packet, comparison);
        Assert.NotEqual(packet, metaGap);
        Assert.NotEqual(comparison, metaGap);
    }

    // ---- helpers ----

    private static byte[] BuildComparisonZipWithRequestContext(string requestContextText)
    {
        var request = new DeckComparisonRequest
        {
            ComparisonResponseJson = "{\"deck_a\":{},\"deck_b\":{}}"
        };

        return PacketArtifactStore.BuildComparisonZip(
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
        var request = new MetaGapRequest
        {
            MetaGapResponseJson = "{\"meta_gap\":{}}"
        };

        return PacketArtifactStore.BuildCedhMetaGapZip(
            request,
            inputSummary: "summary",
            promptText: "prompt",
            schemaJson: "{}",
            requestContextText: requestContextText);
    }

    private static byte[] BuildCedhMetaGapZipWithoutRequestContext()
        => BuildCedhMetaGapZipWithRequestContext(null!);

    // ---- Phase 10-05: cEDH Step 1 round-trip — zip artifact write/read ----

    [Fact]
    public void BuildCedhMetaGapZip_includes_fetched_entries_artifact_when_provided()
    {
        var request = new MetaGapRequest { CommanderName = "Atraxa" };
        var entries = new List<EdhTop16Entry>
        {
            new()
            {
                Standing = 1,
                Wins = 6,
                Losses = 0,
                Draws = 1,
                PlayerName = "Alice",
                DecklistUrl = "https://example.com/a",
                TournamentName = "Test Cup",
                TournamentId = "tc1",
                TournamentDate = new DateOnly(2026, 4, 1),
                TournamentSize = 64,
                MainDeck = new List<EdhTop16Card> { new() { Name = "Sol Ring", Type = "Artifact" } }
            }
        };
        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
            request,
            inputSummary: "summary",
            promptText: "prompt text",
            schemaJson: "{}",
            requestContextText: "workflow_step: 2",
            canonicalDeckListText: null,
            originalDeckText: null,
            fetchedEntries: entries);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entry = archive.GetEntry("20-edh-top16-references.json");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        var json = reader.ReadToEnd();
        var roundTripped = System.Text.Json.JsonSerializer.Deserialize<List<EdhTop16Entry>>(
            json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(roundTripped);
        Assert.Single(roundTripped!);
        Assert.Equal("Alice", roundTripped![0].PlayerName);
        Assert.Equal("Sol Ring", roundTripped[0].MainDeck[0].Name);
    }

    [Fact]
    public void BuildCedhMetaGapZip_omits_fetched_entries_artifact_when_empty()
    {
        var request = new MetaGapRequest { CommanderName = "Atraxa" };
        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
            request,
            inputSummary: "summary",
            promptText: "prompt text",
            schemaJson: "{}",
            requestContextText: "workflow_step: 2",
            canonicalDeckListText: null,
            originalDeckText: null,
            fetchedEntries: Array.Empty<EdhTop16Entry>());

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.Null(archive.GetEntry("20-edh-top16-references.json"));
    }

    // ---- Phase 10-05: cEDH Step 1 round-trip — loader ----

    [Fact]
    public void LoadCedhMetaGapFromZip_restores_fetched_entries_from_artifact()
    {
        var request = new MetaGapRequest { CommanderName = "Atraxa" };
        var entries = new List<EdhTop16Entry>
        {
            new()
            {
                Standing = 1,
                PlayerName = "Bob",
                DecklistUrl = "https://example.com/b",
                TournamentName = "Cup",
                TournamentId = "c1",
                TournamentSize = 32,
                MainDeck = new List<EdhTop16Card> { new() { Name = "Mana Crypt", Type = "Artifact" } }
            }
        };
        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
            request, "summary", "prompt", "{}", "workflow_step: 2", null, null, entries);

        var loaded = new MetaGapRequest();
        var restored = PacketArtifactStore.LoadCedhMetaGapFromZip(new MemoryStream(bytes), loaded);

        Assert.Single(restored.FetchedEntries);
        Assert.Equal("Bob", restored.FetchedEntries[0].PlayerName);
        Assert.Equal("Mana Crypt", restored.FetchedEntries[0].MainDeck[0].Name);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_restores_filter_scalars_and_selected_indexes()
    {
        var request = new MetaGapRequest
        {
            WorkflowStep = 2,
            CommanderName = "Atraxa",
            TimePeriod = CedhMetaTimePeriod.SIX_MONTHS,
            SortBy = CedhMetaSortBy.NEW,
            MinEventSize = 30,
            MaxStanding = 4,
            SelectedReferenceIndexes = new List<int> { 0, 2 }
        };
        var contextText = MetaGapService.BuildRequestContextText(request);
        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
            request, "summary", "prompt", "{}", contextText, null, null, fetchedEntries: null);

        var loaded = new MetaGapRequest();
        PacketArtifactStore.LoadCedhMetaGapFromZip(new MemoryStream(bytes), loaded);

        Assert.Equal(CedhMetaTimePeriod.SIX_MONTHS, loaded.TimePeriod);
        Assert.Equal(CedhMetaSortBy.NEW, loaded.SortBy);
        Assert.Equal(30, loaded.MinEventSize);
        Assert.Equal(4, loaded.MaxStanding);
        Assert.Equal(new[] { 0, 2 }, loaded.SelectedReferenceIndexes);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_lands_on_step_2_when_entries_present_and_no_response()
    {
        var request = new MetaGapRequest { CommanderName = "Atraxa" };
        var entries = new List<EdhTop16Entry>
        {
            new()
            {
                Standing = 1,
                PlayerName = "Pilot",
                DecklistUrl = "u",
                TournamentName = "t",
                TournamentId = "id",
                TournamentSize = 1,
                MainDeck = new List<EdhTop16Card> { new() { Name = "Sol Ring", Type = "Artifact" } }
            }
        };
        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
            request, "summary", "prompt", "{}", "workflow_step: 2\ncommander: Atraxa", null, null, fetchedEntries: entries);

        var loaded = new MetaGapRequest();
        PacketArtifactStore.LoadCedhMetaGapFromZip(new MemoryStream(bytes), loaded);

        Assert.Equal(2, loaded.WorkflowStep);
    }

    [Fact]
    public void LoadCedhMetaGapFromZip_returns_empty_entries_for_legacy_zip()
    {
        var request = new MetaGapRequest
        {
            CommanderName = "Atraxa",
            MetaGapResponseJson = "{\"meta_gap\":{\"commander\":\"Atraxa\"}}"
        };
        var bytes = PacketArtifactStore.BuildCedhMetaGapZip(
            request, "summary", "prompt", "{}",
            "workflow_step: 3\ncommander: Atraxa\ntarget_ai_platform: ChatGPT",
            null, null, fetchedEntries: null);

        var loaded = new MetaGapRequest();
        var restored = PacketArtifactStore.LoadCedhMetaGapFromZip(new MemoryStream(bytes), loaded);

        Assert.Empty(restored.FetchedEntries);
        Assert.Empty(loaded.SelectedReferenceIndexes);
        Assert.Equal(CedhMetaTimePeriod.ONE_YEAR, loaded.TimePeriod);
    }

    // ---- Phase 10-05: cEDH Step 1 round-trip — request-context parser ----

    [Fact]
    public void Parse_extracts_filter_scalars_and_selected_reference_indexes_list()
    {
        const string text = """
            workflow_step: 2
            commander: Atraxa
            target_ai_platform: Claude
            time_period: ONE_YEAR
            sort_by: TOP
            min_event_size: 50
            max_standing: 16
            selected_reference_indexes:
            - 0
            - 2
            - 5
            """;

        var parsed = RequestContextParser.Parse(text);

        Assert.Equal("ONE_YEAR", parsed.TimePeriod);
        Assert.Equal("TOP", parsed.SortBy);
        Assert.Equal(50, parsed.MinEventSize);
        Assert.Equal(16, parsed.MaxStanding);
        Assert.Equal(new[] { 0, 2, 5 }, parsed.SelectedReferenceIndexes);
    }

    [Fact]
    public void Parse_returns_null_filter_scalars_when_keys_absent()
    {
        const string text = """
            workflow_step: 1
            commander: Atraxa
            """;

        var parsed = RequestContextParser.Parse(text);

        Assert.Null(parsed.TimePeriod);
        Assert.Null(parsed.SortBy);
        Assert.Null(parsed.MinEventSize);
        Assert.Null(parsed.MaxStanding);
        Assert.Empty(parsed.SelectedReferenceIndexes);
    }

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
