using System.Text;
using System.Text.RegularExpressions;

namespace DeckFlow.Web.Services;

/// <summary>
/// Parses the YAML-like <c>01-request-context.txt</c> payload emitted by
/// <see cref="DeckAnalysisPacketService.BuildRequestContextText(DeckFlow.Web.Models.DeckAnalysisRequest, string?)" />.
/// </summary>
/// <remarks>
/// The writer is not general YAML. It emits unindented <c>key: value</c> scalars, unindented
/// <c>- item</c> lists, and raw multi-line blocks for <c>strategy_notes</c>, <c>meta_notes</c>,
/// and <c>deck_source</c>. This parser intentionally mirrors that exact contract and returns
/// defaults for null, empty, or malformed input instead of throwing.
/// </remarks>
internal static partial class RequestContextParser
{
    private static readonly Regex TopLevelKeyRegex = TopLevelKeyPattern();
    private static readonly HashSet<string> MultiLineBlockKeys = new(StringComparer.Ordinal)
    {
        "strategy_notes",
        "meta_notes",
        "deck_source"
    };

    /// <summary>
    /// Parses a request-context text payload into the fields needed to rehydrate a saved upload.
    /// </summary>
    public static ParsedRequestContext Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ParsedRequestContext.Empty;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');

        string? format = null;
        string? deckName = null;
        string? commander = null;
        string? targetCommanderBracket = null;
        string? targetAiPlatform = null;
        string? deckAName = null;
        string? deckBName = null;
        string? deckABracket = null;
        string? deckBBracket = null;
        bool? includeSideboardInAnalysis = null;
        bool? includeMaybeboardInAnalysis = null;
        List<string> cardSpecificQuestionCardNames = [];
        string? budgetUpgradeAmount = null;
        List<string> selectedAnalysisQuestions = [];
        List<string> selectedSetCodes = [];
        string? strategyNotes = null;
        string? metaNotes = null;
        string? deckSource = null;
        string? timePeriod = null;
        string? sortBy = null;
        int? minEventSize = null;
        int? maxStanding = null;
        List<int> selectedReferenceIndexes = [];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var keyMatch = TopLevelKeyRegex.Match(line);
            if (!keyMatch.Success)
            {
                continue;
            }

            var key = keyMatch.Groups[1].Value;
            var inlineValue = keyMatch.Groups[2].Value;
            if (!string.IsNullOrEmpty(inlineValue))
            {
                switch (key)
                {
                    case "format":
                        format = inlineValue.Trim();
                        break;
                    case "deck_name":
                        deckName = inlineValue.Trim();
                        break;
                    case "commander":
                        commander = inlineValue.Trim();
                        break;
                    case "target_commander_bracket":
                        targetCommanderBracket = inlineValue.Trim();
                        break;
                    case "target_ai_platform":
                        targetAiPlatform = inlineValue.Trim();
                        break;
                    case "deck_a_name":
                        deckAName = inlineValue.Trim();
                        break;
                    case "deck_b_name":
                        deckBName = inlineValue.Trim();
                        break;
                    case "deck_a_bracket":
                        deckABracket = inlineValue.Trim();
                        break;
                    case "deck_b_bracket":
                        deckBBracket = inlineValue.Trim();
                        break;
                    case "include_sideboard_in_analysis":
                        includeSideboardInAnalysis = ParseBool(inlineValue);
                        break;
                    case "include_maybeboard_in_analysis":
                        includeMaybeboardInAnalysis = ParseBool(inlineValue);
                        break;
                    case "budget_upgrade_amount":
                        budgetUpgradeAmount = inlineValue.Trim();
                        break;
                    case "time_period":
                        timePeriod = inlineValue.Trim();
                        break;
                    case "sort_by":
                        sortBy = inlineValue.Trim();
                        break;
                    case "min_event_size":
                        if (int.TryParse(inlineValue.Trim(), out var minEventSizeValue))
                        {
                            minEventSize = minEventSizeValue;
                        }
                        break;
                    case "max_standing":
                        if (int.TryParse(inlineValue.Trim(), out var maxStandingValue))
                        {
                            maxStanding = maxStandingValue;
                        }
                        break;
                }

                continue;
            }

            if (TryReadList(lines, ref i, key, out var listValues))
            {
                switch (key)
                {
                    case "card_specific_question_card_names":
                        cardSpecificQuestionCardNames = listValues;
                        break;
                    case "selected_analysis_questions":
                        selectedAnalysisQuestions = listValues;
                        break;
                    case "selected_set_codes":
                        selectedSetCodes = listValues;
                        break;
                    case "selected_reference_indexes":
                        selectedReferenceIndexes = listValues
                            .Select(value => int.TryParse(value, out var index) ? (int?)index : null)
                            .Where(index => index.HasValue)
                            .Select(index => index!.Value)
                            .ToList();
                        break;
                }

                continue;
            }

            if (!MultiLineBlockKeys.Contains(key))
            {
                continue;
            }

            var blockValue = ReadBlock(lines, ref i);
            switch (key)
            {
                case "strategy_notes":
                    strategyNotes = blockValue;
                    break;
                case "meta_notes":
                    metaNotes = blockValue;
                    break;
                case "deck_source":
                    deckSource = blockValue;
                    break;
            }
        }

        return new ParsedRequestContext
        {
            Format = string.IsNullOrEmpty(format) ? null : format,
            DeckName = string.IsNullOrEmpty(deckName) ? null : deckName,
            Commander = string.IsNullOrEmpty(commander) ? null : commander,
            TargetCommanderBracket = string.IsNullOrEmpty(targetCommanderBracket) ? null : targetCommanderBracket,
            IncludeSideboardInAnalysis = includeSideboardInAnalysis,
            IncludeMaybeboardInAnalysis = includeMaybeboardInAnalysis,
            CardSpecificQuestionCardNames = cardSpecificQuestionCardNames,
            BudgetUpgradeAmount = string.IsNullOrEmpty(budgetUpgradeAmount) ? null : budgetUpgradeAmount,
            SelectedAnalysisQuestions = selectedAnalysisQuestions,
            SelectedSetCodes = selectedSetCodes,
            StrategyNotes = string.IsNullOrEmpty(strategyNotes) ? null : strategyNotes,
            MetaNotes = string.IsNullOrEmpty(metaNotes) ? null : metaNotes,
            DeckSource = string.IsNullOrEmpty(deckSource) ? null : deckSource,
            TargetAiPlatform = string.IsNullOrEmpty(targetAiPlatform) ? null : targetAiPlatform,
            DeckAName = string.IsNullOrEmpty(deckAName) ? null : deckAName,
            DeckBName = string.IsNullOrEmpty(deckBName) ? null : deckBName,
            DeckABracket = string.IsNullOrEmpty(deckABracket) ? null : deckABracket,
            DeckBBracket = string.IsNullOrEmpty(deckBBracket) ? null : deckBBracket,
            TimePeriod = string.IsNullOrEmpty(timePeriod) ? null : timePeriod,
            SortBy = string.IsNullOrEmpty(sortBy) ? null : sortBy,
            MinEventSize = minEventSize,
            MaxStanding = maxStanding,
            SelectedReferenceIndexes = selectedReferenceIndexes
        };
    }

    private static bool TryReadList(string[] lines, ref int index, string key, out List<string> values)
    {
        values = [];
        if (!IsListKey(key))
        {
            return false;
        }

        var nextIndex = index + 1;
        while (nextIndex < lines.Length && string.IsNullOrWhiteSpace(lines[nextIndex]))
        {
            nextIndex++;
        }

        if (nextIndex >= lines.Length || !lines[nextIndex].StartsWith("- ", StringComparison.Ordinal))
        {
            return true;
        }

        while (nextIndex < lines.Length && lines[nextIndex].StartsWith("- ", StringComparison.Ordinal))
        {
            var value = lines[nextIndex][2..].Trim();
            if (!string.IsNullOrEmpty(value))
            {
                values.Add(value);
            }

            nextIndex++;
        }

        index = nextIndex - 1;
        return true;
    }

    private static bool IsListKey(string key)
        => key is "card_specific_question_card_names"
            or "selected_analysis_questions"
            or "selected_set_codes"
            or "selected_reference_indexes";

    private static string? ReadBlock(string[] lines, ref int index)
    {
        var builder = new StringBuilder();
        for (var i = index + 1; i < lines.Length; i++)
        {
            if (TopLevelKeyRegex.IsMatch(lines[i]))
            {
                index = i - 1;
                return TrimTrailingBlankLines(builder);
            }

            builder.Append(lines[i]);
            if (i < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        index = lines.Length;
        return TrimTrailingBlankLines(builder);
    }

    private static string? TrimTrailingBlankLines(StringBuilder builder)
    {
        var value = builder.ToString().TrimEnd('\n');
        while (value.Length > 0)
        {
            var lastNewLine = value.LastIndexOf('\n');
            var segmentStart = lastNewLine >= 0 ? lastNewLine + 1 : 0;
            if (!string.IsNullOrWhiteSpace(value[segmentStart..]))
            {
                break;
            }

            value = lastNewLine >= 0 ? value[..lastNewLine] : string.Empty;
        }

        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static bool? ParseBool(string raw)
        => raw.Trim() switch
        {
            "True" or "true" or "1" => true,
            "False" or "false" or "0" => false,
            _ => null
        };

    [GeneratedRegex("^([a-z_][a-z_0-9]*):\\s?(.*)$", RegexOptions.Compiled)]
    private static partial Regex TopLevelKeyPattern();
}

/// <summary>
/// Parsed request-context values restored from <c>01-request-context.txt</c>.
/// </summary>
internal sealed record ParsedRequestContext
{
    public static ParsedRequestContext Empty { get; } = new();

    public string? Format { get; init; }

    public string? DeckName { get; init; }

    public string? Commander { get; init; }

    public string? TargetCommanderBracket { get; init; }

    public bool? IncludeSideboardInAnalysis { get; init; }

    public bool? IncludeMaybeboardInAnalysis { get; init; }

    public IReadOnlyList<string> CardSpecificQuestionCardNames { get; init; } = Array.Empty<string>();

    public string? BudgetUpgradeAmount { get; init; }

    public IReadOnlyList<string> SelectedAnalysisQuestions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> SelectedSetCodes { get; init; } = Array.Empty<string>();

    public string? StrategyNotes { get; init; }

    public string? MetaNotes { get; init; }

    public string? DeckSource { get; init; }

    /// <summary>
    /// The AI platform from the request context, if present.
    /// Null means absent in zip (legacy zip — caller defaults to "ChatGPT").
    /// </summary>
    public string? TargetAiPlatform { get; init; }

    /// <summary>Comparison-page deck A user-entered name, if present.</summary>
    public string? DeckAName { get; init; }

    /// <summary>Comparison-page deck B user-entered name, if present.</summary>
    public string? DeckBName { get; init; }

    /// <summary>Comparison-page deck A bracket selection, if present.</summary>
    public string? DeckABracket { get; init; }

    /// <summary>Comparison-page deck B bracket selection, if present.</summary>
    public string? DeckBBracket { get; init; }

    /// <summary>cEDH Step 1 filter — time period enum string, e.g. "ONE_YEAR".</summary>
    public string? TimePeriod { get; init; }

    /// <summary>cEDH Step 1 filter — sort-by enum string, e.g. "TOP".</summary>
    public string? SortBy { get; init; }

    /// <summary>cEDH Step 1 filter — minimum event size threshold.</summary>
    public int? MinEventSize { get; init; }

    /// <summary>cEDH Step 1 filter — maximum standing cutoff (null = no cap).</summary>
    public int? MaxStanding { get; init; }

    /// <summary>cEDH Step 2 user picks — positional indexes into the round-tripped FetchedEntries list.</summary>
    public IReadOnlyList<int> SelectedReferenceIndexes { get; init; } = Array.Empty<int>();
}
