using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Builds a single in-memory .zip of every ChatGPT analysis artifact for the current request,
/// and rehydrates a saved zip back into a request. Pure CPU work, no filesystem access.
/// </summary>
internal static class ChatGptPacketArtifactStore
{
    private const int MaxEntryUncompressedBytes = 2 * 1024 * 1024;
    private const int MaxTotalUncompressedBytes = 10 * 1024 * 1024;

    // Phase 10-05: serialization options for the new 20-edh-top16-references.json
    // artifact. CamelCase property names match the EdhTop16Entry JSON shape used
    // by the upstream edhtop16 client; ignoring nulls keeps the payload compact.
    private static readonly JsonSerializerOptions FetchedEntriesJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly HashSet<string> PacketAllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "00-input-summary.txt",
        "01-request-context.txt",
        "10-deck-list.txt",
        "10b-deck-original.txt",
        "30-reference.txt",
        "31-analysis-prompt.txt",
        "40-deck-profile.json",
        "41-deck-profile-schema.json",
        "50-set-upgrade-prompt.txt",
        "51-set-upgrade-response.json",
        "all-prompts.txt",
        "all-responses.txt"
    };

    private static readonly HashSet<string> ComparisonAllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "00-comparison-input-summary.txt",
        "01-request-context.txt",
        "10-deck-a-list.txt",
        "10b-deck-a-original.txt",
        "11-deck-b-list.txt",
        "11b-deck-b-original.txt",
        "12-deck-a-combos.txt",
        "13-deck-b-combos.txt",
        "20-comparison-context.txt",
        "30-comparison-prompt.txt",
        "31-comparison-schema.json",
        "32-comparison-follow-up-prompt.txt",
        "40-deck-comparison-response.json"
    };

    private static readonly HashSet<string> CedhAllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "00-input-summary.txt",
        "01-request-context.txt",
        "10-deck-list.txt",
        "10b-deck-original.txt",
        "20-edh-top16-references.json",
        "30-meta-gap-prompt.txt",
        "31-meta-gap-schema.json",
        "40-meta-gap-response.json"
    };

    /// <summary>
    /// Returns the source string if it is NOT a supported deck-import URL
    /// (Moxfield or Archidekt). For URL inputs returns null so the writer
    /// skips the original-text artifact — the canonical artifact is the only
    /// faithful record. Mirrors the host-allowlist used by the deck importers.
    /// </summary>
    public static string? OriginalDeckTextOrNull(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) { return null; }
        var trimmed = source.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && uri.Host is not null
            && (uri.Host.Contains("moxfield.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Contains("archidekt.com", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }
        return trimmed;
    }

    public static byte[] BuildZip(
        ChatGptDeckRequest request,
        string? commanderName,
        string inputSummary,
        string? requestContextText,
        string? referenceText,
        string? analysisPromptText,
        string deckProfileSchemaJson,
        string? setUpgradePromptText,
        string? canonicalDeckListText = null,
        string? originalDeckText = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promptSections = NormalizeSections(
        [
            ("00-input-summary.txt", "INPUT SUMMARY", inputSummary),
            ("01-request-context.txt", "REQUEST CONTEXT", requestContextText),
            ("10-deck-list.txt", "DECK LIST", canonicalDeckListText),
            ("10b-deck-original.txt", "DECK ORIGINAL TEXT", originalDeckText),
            ("30-reference.txt", "REFERENCE TEXT", referenceText),
            ("31-analysis-prompt.txt", "ANALYSIS PROMPT", analysisPromptText),
            ("41-deck-profile-schema.json", "DECK PROFILE JSON SCHEMA", deckProfileSchemaJson),
            ("50-set-upgrade-prompt.txt", "SET UPGRADE PROMPT", setUpgradePromptText)
        ]);

        var responseSections = NormalizeSections(
        [
            ("40-deck-profile.json", "DECK PROFILE JSON", string.IsNullOrWhiteSpace(request.DeckProfileJson) ? null : ExtractJsonObject(request.DeckProfileJson)),
            ("51-set-upgrade-response.json", "SET UPGRADE RESPONSE JSON", string.IsNullOrWhiteSpace(request.SetUpgradeResponseJson) ? null : ExtractJsonObject(request.SetUpgradeResponseJson))
        ]);

        return BuildArchive(promptSections, responseSections);
    }

    public static byte[] BuildComparisonZip(
        ChatGptDeckComparisonRequest request,
        string inputSummary,
        string deckAListText,
        string deckBListText,
        string deckAComboText,
        string deckBComboText,
        string comparisonContextText,
        string comparisonPromptText,
        string followUpPromptText,
        string comparisonSchemaJson,
        string? requestContextText,
        string? deckAOriginalText = null,
        string? deckBOriginalText = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sections = NormalizeSections(
        [
            ("00-comparison-input-summary.txt", "COMPARISON INPUT SUMMARY", inputSummary),
            ("01-request-context.txt", "REQUEST CONTEXT", requestContextText),
            ("10-deck-a-list.txt", "DECK A LIST", deckAListText),
            ("10b-deck-a-original.txt", "DECK A ORIGINAL TEXT", deckAOriginalText),
            ("11-deck-b-list.txt", "DECK B LIST", deckBListText),
            ("11b-deck-b-original.txt", "DECK B ORIGINAL TEXT", deckBOriginalText),
            ("12-deck-a-combos.txt", "DECK A COMBOS", deckAComboText),
            ("13-deck-b-combos.txt", "DECK B COMBOS", deckBComboText),
            ("20-comparison-context.txt", "COMPARISON CONTEXT", comparisonContextText),
            ("30-comparison-prompt.txt", "COMPARISON PROMPT", comparisonPromptText),
            ("31-comparison-schema.json", "COMPARISON SCHEMA JSON", comparisonSchemaJson),
            ("32-comparison-follow-up-prompt.txt", "COMPARISON FOLLOW-UP PROMPT", followUpPromptText),
            ("40-deck-comparison-response.json", "DECK COMPARISON RESPONSE JSON", string.IsNullOrWhiteSpace(request.ComparisonResponseJson) ? null : ChatGptJsonTextFormatterService.ExtractJsonPayload(request.ComparisonResponseJson))
        ]);

        return BuildArchive(sections);
    }

    public static byte[] BuildCedhMetaGapZip(
        ChatGptCedhMetaGapRequest request,
        string inputSummary,
        string promptText,
        string schemaJson,
        string? requestContextText,
        string? canonicalDeckListText = null,
        string? originalDeckText = null,
        IReadOnlyList<EdhTop16Entry>? fetchedEntries = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sections = NormalizeSections(
        [
            ("00-input-summary.txt", "INPUT SUMMARY", inputSummary),
            ("01-request-context.txt", "REQUEST CONTEXT", requestContextText),
            ("10-deck-list.txt", "DECK LIST", canonicalDeckListText),
            ("10b-deck-original.txt", "DECK ORIGINAL TEXT", originalDeckText),
            ("20-edh-top16-references.json", "EDH TOP 16 REFERENCES",
                fetchedEntries is { Count: > 0 } ? JsonSerializer.Serialize(fetchedEntries, FetchedEntriesJsonOptions) : null),
            ("30-meta-gap-prompt.txt", "META GAP PROMPT", promptText),
            ("31-meta-gap-schema.json", "META GAP SCHEMA JSON", schemaJson),
            ("40-meta-gap-response.json", "META GAP RESPONSE JSON", string.IsNullOrWhiteSpace(request.MetaGapResponseJson) ? null : ChatGptJsonTextFormatterService.ExtractJsonPayload(request.MetaGapResponseJson))
        ]);

        return BuildArchive(sections);
    }

    /// <summary>
    /// Rehydrates a saved ChatGPT packet zip back into a deck request.
    /// </summary>
    /// <remarks>
    /// At least one of <c>01-request-context.txt</c>, <c>40-deck-profile.json</c>, or
    /// <c>51-set-upgrade-response.json</c> must be present. Partial zips (request context
    /// only, no responses) rehydrate form state and land the user back on Step 1 to re-paste
    /// the deck. Zips with response JSONs land on Step 3 (deck profile) or Step 5 (set upgrade).
    /// </remarks>
    public static void LoadFromZip(Stream zipStream, ChatGptDeckRequest request)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(request);

        var entries = ReadEntries(zipStream, PacketAllowedNames);
        entries.TryGetValue("40-deck-profile.json", out var deckProfile);
        entries.TryGetValue("51-set-upgrade-response.json", out var setUpgrade);
        entries.TryGetValue("01-request-context.txt", out var requestContextText);
        entries.TryGetValue("10-deck-list.txt", out var canonicalDeckList);
        entries.TryGetValue("10b-deck-original.txt", out var originalDeckText);

        if (string.IsNullOrWhiteSpace(deckProfile) &&
            string.IsNullOrWhiteSpace(setUpgrade) &&
            string.IsNullOrWhiteSpace(requestContextText))
        {
            throw new InvalidOperationException("Imported zip did not contain a recognized DeckFlow session — expected 01-request-context.txt, 40-deck-profile.json, or 51-set-upgrade-response.json.");
        }

        request.DeckProfileJson = deckProfile ?? string.Empty;
        request.SetUpgradeResponseJson = setUpgrade ?? string.Empty;
        request.WorkflowStep = !string.IsNullOrWhiteSpace(setUpgrade)
            ? 5
            : !string.IsNullOrWhiteSpace(deckProfile)
                ? 3
                : 1;

        // Precedence: original (user's pasted text) > canonical (DeckFlow-emitted
        // sectioned list) > deck_source key in request_context (legacy). Each
        // step overrides the previous so the most user-recognizable text wins.
        if (!string.IsNullOrWhiteSpace(canonicalDeckList))
        {
            request.DeckText = canonicalDeckList.TrimEnd();
        }
        if (!string.IsNullOrWhiteSpace(originalDeckText))
        {
            request.DeckText = originalDeckText.TrimEnd();
        }

        if (!string.IsNullOrWhiteSpace(requestContextText))
        {
            var parsed = ChatGptRequestContextParser.Parse(requestContextText);
            if (!string.IsNullOrEmpty(parsed.Format))
            {
                request.Format = parsed.Format;
            }

            if (parsed.DeckName is not null)
            {
                request.DeckName = parsed.DeckName;
            }

            if (parsed.TargetCommanderBracket is not null)
            {
                request.TargetCommanderBracket = parsed.TargetCommanderBracket;
            }

            if (parsed.IncludeSideboardInAnalysis is { } includeSideboard)
            {
                request.IncludeSideboardInAnalysis = includeSideboard;
            }

            if (parsed.IncludeMaybeboardInAnalysis is { } includeMaybeboard)
            {
                request.IncludeMaybeboardInAnalysis = includeMaybeboard;
            }

            if (parsed.CardSpecificQuestionCardNames.Count > 0)
            {
                request.CardSpecificQuestionCardNames = parsed.CardSpecificQuestionCardNames.ToList();
            }

            if (parsed.BudgetUpgradeAmount is not null)
            {
                request.BudgetUpgradeAmount = parsed.BudgetUpgradeAmount;
            }

            if (parsed.SelectedAnalysisQuestions.Count > 0)
            {
                request.SelectedAnalysisQuestions = parsed.SelectedAnalysisQuestions.ToList();
            }

            if (parsed.SelectedSetCodes.Count > 0)
            {
                request.SelectedSetCodes = parsed.SelectedSetCodes.ToList();
            }

            if (parsed.StrategyNotes is not null)
            {
                request.StrategyNotes = parsed.StrategyNotes;
            }

            if (parsed.MetaNotes is not null)
            {
                request.MetaNotes = parsed.MetaNotes;
            }

            // Precedence: canonical/original deck artifacts (set above) win
            // over the legacy deck_source block in request_context. Only apply
            // the request_context value when neither artifact populated DeckText.
            if (parsed.DeckSource is not null
                && string.IsNullOrWhiteSpace(canonicalDeckList)
                && string.IsNullOrWhiteSpace(originalDeckText))
            {
                request.DeckSource = parsed.DeckSource;
            }

            if (parsed.TargetAiPlatform is not null)
            {
                request.TargetAiPlatform = parsed.TargetAiPlatform;
            }
        }
    }

    /// <summary>
    /// Rehydrates a saved comparison zip back into a comparison request.
    /// </summary>
    /// <remarks>
    /// At least one of <c>40-deck-comparison-response.json</c>, <c>10-deck-a-list.txt</c>,
    /// <c>11-deck-b-list.txt</c>, or <c>01-request-context.txt</c> must be present. Partial zips
    /// (no response yet) rehydrate whatever state is available and land the user back on Step 2
    /// (decks restored, ready to regenerate the prompt) or Step 1 (re-paste decks).
    /// Deck A and Deck B are restored from the normalized post-Scryfall list entries in the zip,
    /// which is the deck content the comparison workflow actually analyzed.
    /// </remarks>
    public static RestoredComparisonArtifacts LoadComparisonFromZip(Stream zipStream, ChatGptDeckComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(request);

        var entries = ReadEntries(zipStream, ComparisonAllowedNames);
        entries.TryGetValue("40-deck-comparison-response.json", out var responseJson);
        entries.TryGetValue("10-deck-a-list.txt", out var deckAList);
        entries.TryGetValue("10b-deck-a-original.txt", out var deckAOriginal);
        entries.TryGetValue("11-deck-b-list.txt", out var deckBList);
        entries.TryGetValue("11b-deck-b-original.txt", out var deckBOriginal);
        entries.TryGetValue("01-request-context.txt", out var requestContextText);
        entries.TryGetValue("00-comparison-input-summary.txt", out var inputSummary);
        entries.TryGetValue("12-deck-a-combos.txt", out var deckAComboText);
        entries.TryGetValue("13-deck-b-combos.txt", out var deckBComboText);
        entries.TryGetValue("20-comparison-context.txt", out var comparisonContextText);
        entries.TryGetValue("30-comparison-prompt.txt", out var comparisonPromptText);
        entries.TryGetValue("31-comparison-schema.json", out var comparisonSchemaJson);
        entries.TryGetValue("32-comparison-follow-up-prompt.txt", out var followUpPromptText);

        if (string.IsNullOrWhiteSpace(responseJson) &&
            string.IsNullOrWhiteSpace(deckAList) &&
            string.IsNullOrWhiteSpace(deckAOriginal) &&
            string.IsNullOrWhiteSpace(deckBList) &&
            string.IsNullOrWhiteSpace(deckBOriginal) &&
            string.IsNullOrWhiteSpace(requestContextText))
        {
            throw new InvalidOperationException("Imported zip did not contain a recognized DeckFlow comparison session — expected 01-request-context.txt, 10-deck-a-list.txt, 11-deck-b-list.txt, or 40-deck-comparison-response.json.");
        }

        request.ComparisonResponseJson = responseJson ?? string.Empty;
        // Precedence: original (10b-*-original.txt) over canonical (10-*-list.txt).
        // Original is what the user actually pasted; canonical is the
        // alphabetized DeckFlow-emitted version. URL-imported decks have no
        // original artifact so canonical is the only restore source.
        var deckAText = !string.IsNullOrWhiteSpace(deckAOriginal) ? deckAOriginal : deckAList;
        if (!string.IsNullOrWhiteSpace(deckAText))
        {
            request.DeckASource = deckAText.TrimEnd();
        }

        var deckBText = !string.IsNullOrWhiteSpace(deckBOriginal) ? deckBOriginal : deckBList;
        if (!string.IsNullOrWhiteSpace(deckBText))
        {
            request.DeckBSource = deckBText.TrimEnd();
        }

        // Step 3 = response present (full state); Step 2 = both decks present
        // (ready to regenerate prompt); otherwise Step 1 (re-paste decks).
        request.WorkflowStep = !string.IsNullOrWhiteSpace(responseJson)
            ? 3
            : (!string.IsNullOrWhiteSpace(deckAText) && !string.IsNullOrWhiteSpace(deckBText))
                ? 2
                : 1;

        if (!string.IsNullOrWhiteSpace(requestContextText))
        {
            var parsed = ChatGptRequestContextParser.Parse(requestContextText);
            if (parsed.TargetAiPlatform is not null)
            {
                request.TargetAiPlatform = parsed.TargetAiPlatform;
            }
            if (parsed.DeckAName is not null)
            {
                request.DeckAName = parsed.DeckAName;
            }
            if (parsed.DeckBName is not null)
            {
                request.DeckBName = parsed.DeckBName;
            }
            if (parsed.DeckABracket is not null)
            {
                request.DeckABracket = parsed.DeckABracket;
            }
            if (parsed.DeckBBracket is not null)
            {
                request.DeckBBracket = parsed.DeckBBracket;
            }
        }

        return new RestoredComparisonArtifacts
        {
            InputSummary = NullIfBlank(inputSummary),
            DeckAListText = NullIfBlank(deckAList),
            DeckBListText = NullIfBlank(deckBList),
            DeckAComboText = NullIfBlank(deckAComboText),
            DeckBComboText = NullIfBlank(deckBComboText),
            ComparisonContextText = NullIfBlank(comparisonContextText),
            ComparisonPromptText = NullIfBlank(comparisonPromptText),
            ComparisonSchemaJson = NullIfBlank(comparisonSchemaJson),
            FollowUpPromptText = NullIfBlank(followUpPromptText)
        };
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd();

    /// <summary>
    /// Rehydrates a saved cEDH meta-gap zip back into a request.
    /// </summary>
    /// <remarks>
    /// At least one of <c>40-meta-gap-response.json</c> or <c>01-request-context.txt</c> must
    /// be present. Partial zips (no response yet) rehydrate the AI selector and land the user
    /// back on Step 1 to re-paste the deck. The cEDH zip contract does not currently include
    /// deck-source text, so <see cref="ChatGptCedhMetaGapRequest.DeckSource" /> cannot be
    /// restored here. The upload controller restores commander name from the response JSON
    /// (when present) after this method returns.
    /// </remarks>
    public static RestoredCedhMetaGapArtifacts LoadCedhMetaGapFromZip(Stream zipStream, ChatGptCedhMetaGapRequest request)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(request);

        var entries = ReadEntries(zipStream, CedhAllowedNames);
        entries.TryGetValue("40-meta-gap-response.json", out var responseJson);
        entries.TryGetValue("01-request-context.txt", out var requestContextText);
        entries.TryGetValue("00-input-summary.txt", out var inputSummary);
        entries.TryGetValue("30-meta-gap-prompt.txt", out var promptText);
        entries.TryGetValue("31-meta-gap-schema.json", out var schemaJson);
        entries.TryGetValue("10-deck-list.txt", out var canonicalDeckList);
        entries.TryGetValue("10b-deck-original.txt", out var originalDeckText);
        entries.TryGetValue("20-edh-top16-references.json", out var fetchedEntriesJson);

        if (string.IsNullOrWhiteSpace(responseJson) && string.IsNullOrWhiteSpace(requestContextText))
        {
            throw new InvalidOperationException("Imported zip did not contain a recognized DeckFlow meta-gap session — expected 01-request-context.txt or 40-meta-gap-response.json.");
        }

        request.MetaGapResponseJson = responseJson ?? string.Empty;
        request.CommanderName = string.Empty;

        // Precedence: original > canonical for re-rendering the deck text box.
        // Legacy zips have neither; cleared DeckSource here lets the user
        // re-paste. CommanderName is restored from the request_context block
        // below as a fallback when the response JSON isn't present.
        var deckText = !string.IsNullOrWhiteSpace(originalDeckText)
            ? originalDeckText
            : canonicalDeckList;
        request.DeckSource = string.IsNullOrWhiteSpace(deckText) ? string.Empty : deckText.TrimEnd();

        if (!string.IsNullOrWhiteSpace(requestContextText))
        {
            var parsed = ChatGptRequestContextParser.Parse(requestContextText);
            if (parsed.TargetAiPlatform is not null)
            {
                request.TargetAiPlatform = parsed.TargetAiPlatform;
            }
            if (parsed.Commander is not null)
            {
                request.CommanderName = parsed.Commander;
            }
            if (parsed.TimePeriod is not null && Enum.TryParse<CedhMetaTimePeriod>(parsed.TimePeriod, out var tp))
            {
                request.TimePeriod = tp;
            }
            if (parsed.SortBy is not null && Enum.TryParse<CedhMetaSortBy>(parsed.SortBy, out var sb))
            {
                request.SortBy = sb;
            }
            if (parsed.MinEventSize.HasValue)
            {
                request.MinEventSize = parsed.MinEventSize.Value;
            }
            if (parsed.MaxStanding.HasValue)
            {
                request.MaxStanding = parsed.MaxStanding.Value;
            }
            if (parsed.SelectedReferenceIndexes.Count > 0)
            {
                request.SelectedReferenceIndexes = parsed.SelectedReferenceIndexes.ToList();
            }
        }

        var restoredEntries = TryDeserializeFetchedEntries(fetchedEntriesJson);

        // Workflow-step heuristic — must run AFTER restoredEntries is computed so
        // we can distinguish "no response, but Step 1 state was saved" (=> Step 2)
        // from "truly empty session" (=> Step 1).
        request.WorkflowStep = !string.IsNullOrWhiteSpace(responseJson) ? 3
            : restoredEntries.Count > 0 ? 2
            : 1;

        return new RestoredCedhMetaGapArtifacts
        {
            InputSummary = NullIfBlank(inputSummary),
            PromptText = NullIfBlank(promptText),
            SchemaJson = NullIfBlank(schemaJson),
            FetchedEntries = restoredEntries
        };
    }

    private static IReadOnlyList<EdhTop16Entry> TryDeserializeFetchedEntries(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<EdhTop16Entry>();
        }
        try
        {
            var deserialized = JsonSerializer.Deserialize<List<EdhTop16Entry>>(json, FetchedEntriesJsonOptions);
            return deserialized ?? (IReadOnlyList<EdhTop16Entry>)Array.Empty<EdhTop16Entry>();
        }
        catch (JsonException)
        {
            return Array.Empty<EdhTop16Entry>();
        }
    }

    public static string SuggestPacketZipFileName(string? commanderName, string? targetAiPlatform = null)
        => $"{CreateSafePathSegment(commanderName, "deck-analysis")}-analysis-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    public static string SuggestComparisonZipFileName(string? commanderName, string? targetAiPlatform = null)
        => $"{CreateSafePathSegment(commanderName, "deck-comparison")}-comparison-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    public static string SuggestCedhMetaGapZipFileName(string commanderName, string? targetAiPlatform = null)
        => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-cedh-meta-gap-{CreateSafePathSegment(targetAiPlatform, "chatgpt")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    private static byte[] BuildArchive(params IReadOnlyList<(string FileName, string Label, string Content)>[] sectionGroups)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var sectionGroup in sectionGroups)
            {
                foreach (var section in sectionGroup)
                {
                    WriteEntry(archive, section.FileName, section.Content);
                }
            }

            if (sectionGroups.Length == 2)
            {
                var promptText = BuildCombinedArtifactText(sectionGroups[0]);
                if (!string.IsNullOrWhiteSpace(promptText))
                {
                    WriteEntry(archive, "all-prompts.txt", promptText);
                }

                var responseText = BuildCombinedArtifactText(sectionGroups[1]);
                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    WriteEntry(archive, "all-responses.txt", responseText);
                }
            }
        }

        return memoryStream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string fileName, string content)
    {
        var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static IReadOnlyList<(string FileName, string Label, string Content)> NormalizeSections(
        IEnumerable<(string FileName, string Label, string? Content)> sections)
        => sections
            .Where(section => !string.IsNullOrWhiteSpace(section.Content))
            .Select(section => (section.FileName, section.Label, section.Content!.Trim() + Environment.NewLine))
            .ToList();

    private static Dictionary<string, string> ReadEntries(Stream zipStream, HashSet<string> allowedNames)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: false);
        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.Contains('/') || entry.FullName.Contains('\\'))
            {
                throw new InvalidOperationException($"Imported zip contains an invalid entry path: {entry.FullName}");
            }

            if (!allowedNames.Contains(entry.FullName))
            {
                throw new InvalidOperationException($"Imported zip contains an unsupported entry: {entry.FullName}");
            }

            if (entry.Length > MaxEntryUncompressedBytes)
            {
                throw new InvalidOperationException($"Imported zip entry exceeds the 2 MB limit: {entry.FullName}");
            }

            totalBytes += entry.Length;
            if (totalBytes > MaxTotalUncompressedBytes)
            {
                throw new InvalidOperationException("Imported zip exceeds the 10 MB total uncompressed size limit.");
            }

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            entries[entry.FullName] = reader.ReadToEnd();
        }

        return entries;
    }

    private static string BuildCombinedArtifactText(IEnumerable<(string FileName, string Label, string Content)> sections)
    {
        var builder = new StringBuilder();
        foreach (var section in sections)
        {
            builder.AppendLine($"===== {section.Label} ({section.FileName}) =====");
            builder.Append(section.Content);
            builder.AppendLine();
        }

        return builder.Length == 0
            ? string.Empty
            : builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string CreateSafePathSegment(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        // Header-safe + cross-OS sanitizer. Drop anything outside [A-Za-z0-9 _.-]
        // so CR/LF/control chars can never reach an HTTP response header even on
        // Linux, where Path.GetInvalidFileNameChars only rejects NUL + '/'.
        var sanitized = new string(candidate.Select(ch =>
            ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or ' ' or '.' or '_' or '-'
                ? ch
                : '-').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized.Replace(' ', '-').ToLowerInvariant();
    }

    private static string ExtractJsonObject(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }
        }

        return trimmed.Trim();
    }
}

/// <summary>
/// Display-side artifacts restored from a comparison zip on re-upload.
/// Mirrors the analysis output stored alongside form-field state so the
/// view can show Step 2 content (prompt, summary, schema, combos, etc.)
/// without re-running BuildAsync.
/// </summary>
internal sealed record RestoredComparisonArtifacts
{
    public string? InputSummary { get; init; }
    public string? DeckAListText { get; init; }
    public string? DeckBListText { get; init; }
    public string? DeckAComboText { get; init; }
    public string? DeckBComboText { get; init; }
    public string? ComparisonContextText { get; init; }
    public string? ComparisonPromptText { get; init; }
    public string? ComparisonSchemaJson { get; init; }
    public string? FollowUpPromptText { get; init; }
}

/// <summary>
/// Display-side artifacts restored from a cEDH meta-gap zip on re-upload.
/// </summary>
internal sealed record RestoredCedhMetaGapArtifacts
{
    public string? InputSummary { get; init; }
    public string? PromptText { get; init; }
    public string? SchemaJson { get; init; }
    public IReadOnlyList<EdhTop16Entry> FetchedEntries { get; init; } = Array.Empty<EdhTop16Entry>();
}
