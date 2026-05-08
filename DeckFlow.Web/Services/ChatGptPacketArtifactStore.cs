using System.IO.Compression;
using System.Text;
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

    private static readonly HashSet<string> PacketAllowedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "00-input-summary.txt",
        "01-request-context.txt",
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
        "10-deck-a-list.txt",
        "11-deck-b-list.txt",
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
        "30-meta-gap-prompt.txt",
        "31-meta-gap-schema.json",
        "40-meta-gap-response.json"
    };

    public static byte[] BuildZip(
        ChatGptDeckRequest request,
        string? commanderName,
        string inputSummary,
        string? requestContextText,
        string? referenceText,
        string? analysisPromptText,
        string deckProfileSchemaJson,
        string? setUpgradePromptText)
    {
        ArgumentNullException.ThrowIfNull(request);

        var promptSections = NormalizeSections(
        [
            ("00-input-summary.txt", "INPUT SUMMARY", inputSummary),
            ("01-request-context.txt", "REQUEST CONTEXT", requestContextText),
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
        string comparisonSchemaJson)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sections = NormalizeSections(
        [
            ("00-comparison-input-summary.txt", "COMPARISON INPUT SUMMARY", inputSummary),
            ("10-deck-a-list.txt", "DECK A LIST", deckAListText),
            ("11-deck-b-list.txt", "DECK B LIST", deckBListText),
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
        string schemaJson)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sections = NormalizeSections(
        [
            ("00-input-summary.txt", "INPUT SUMMARY", inputSummary),
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
    /// If present, <c>01-request-context.txt</c> is parsed to restore user-controlled request fields.
    /// Older zips that only contain the response JSON payloads remain valid and silently skip context hydration.
    /// </remarks>
    public static void LoadFromZip(Stream zipStream, ChatGptDeckRequest request)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(request);

        var entries = ReadEntries(zipStream, PacketAllowedNames);
        entries.TryGetValue("40-deck-profile.json", out var deckProfile);
        entries.TryGetValue("51-set-upgrade-response.json", out var setUpgrade);
        entries.TryGetValue("01-request-context.txt", out var requestContextText);

        if (string.IsNullOrWhiteSpace(deckProfile) && string.IsNullOrWhiteSpace(setUpgrade))
        {
            throw new InvalidOperationException("Imported zip did not contain 40-deck-profile.json or 51-set-upgrade-response.json.");
        }

        request.DeckProfileJson = deckProfile ?? string.Empty;
        request.SetUpgradeResponseJson = setUpgrade ?? string.Empty;
        request.WorkflowStep = !string.IsNullOrWhiteSpace(setUpgrade) ? 5 : 3;

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

            if (parsed.DeckSource is not null)
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
    /// Deck A and Deck B are restored from the normalized post-Scryfall list entries in the zip,
    /// which is the deck content the comparison workflow actually analyzed.
    /// </remarks>
    public static void LoadComparisonFromZip(Stream zipStream, ChatGptDeckComparisonRequest request)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(request);

        var entries = ReadEntries(zipStream, ComparisonAllowedNames);
        if (!entries.TryGetValue("40-deck-comparison-response.json", out var responseJson)
            || string.IsNullOrWhiteSpace(responseJson))
        {
            throw new InvalidOperationException("Imported zip did not contain 40-deck-comparison-response.json.");
        }

        request.ComparisonResponseJson = responseJson;
        request.WorkflowStep = 3;
        if (entries.TryGetValue("10-deck-a-list.txt", out var deckAList) && !string.IsNullOrWhiteSpace(deckAList))
        {
            request.DeckASource = deckAList.TrimEnd();
        }

        if (entries.TryGetValue("11-deck-b-list.txt", out var deckBList) && !string.IsNullOrWhiteSpace(deckBList))
        {
            request.DeckBSource = deckBList.TrimEnd();
        }
    }

    /// <summary>
    /// Rehydrates a saved cEDH meta-gap zip back into a request.
    /// </summary>
    /// <remarks>
    /// The cEDH zip contract does not currently include deck-source text, so <see cref="ChatGptCedhMetaGapRequest.DeckSource" />
    /// cannot be restored here. The upload controller restores commander name from the response JSON after this method returns.
    /// </remarks>
    public static void LoadCedhMetaGapFromZip(Stream zipStream, ChatGptCedhMetaGapRequest request)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(request);

        var entries = ReadEntries(zipStream, CedhAllowedNames);
        if (!entries.TryGetValue("40-meta-gap-response.json", out var responseJson)
            || string.IsNullOrWhiteSpace(responseJson))
        {
            throw new InvalidOperationException("Imported zip did not contain 40-meta-gap-response.json.");
        }

        request.MetaGapResponseJson = responseJson;
        request.WorkflowStep = 3;
        request.DeckSource = string.Empty;
        request.CommanderName = string.Empty;
    }

    public static string SuggestPacketZipFileName(string? commanderName)
        => $"{CreateSafePathSegment(commanderName, "deckflow-packet")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    public static string SuggestComparisonZipFileName(string deckAName, string deckBName)
        => $"{CreateSafePathSegment($"{deckAName}-vs-{deckBName}", "deck-comparison")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

    public static string SuggestCedhMetaGapZipFileName(string commanderName)
        => $"{CreateSafePathSegment(commanderName, "cedh-meta-gap")}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

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
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(candidate.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
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
