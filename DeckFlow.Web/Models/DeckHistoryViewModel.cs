using DeckFlow.Core.History;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Models;

/// <summary>
/// View model for the Deck History page, including the current request, any processing error,
/// timeline projections, the selected pair diff, prompt text, and the serialized history JSON.
/// </summary>
public sealed record DeckHistoryViewModel
{
    /// <summary>The active deck tool tab.</summary>
    public DeckPageTab ActiveTab { get; init; }

    /// <summary>The current request values to re-render into the form.</summary>
    public DeckHistoryRequest Request { get; init; } = new();

    /// <summary>User-facing error message for hard failures.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Newest-first timeline rows for the rendered history table.</summary>
    public IReadOnlyList<(int Id, DateTimeOffset Date, string? Label, string? Notes, int CardCount, string AddsSummary, string CutsSummary)> TimelineRows { get; init; } = [];

    /// <summary>The currently selected pairwise diff, when at least two versions exist.</summary>
    public VersionDiff? PairDiff { get; init; }

    /// <summary>The selected older version id for the compare panel.</summary>
    public int? PairOlderId { get; init; }

    /// <summary>The selected newer version id for the compare panel.</summary>
    public int? PairNewerId { get; init; }

    /// <summary>The generated AI prompt describing how the deck evolved.</summary>
    public string PromptText { get; init; } = string.Empty;

    /// <summary>The serialized history JSON used for compare/download round-trips.</summary>
    public string HistoryJson { get; init; } = string.Empty;

    /// <summary>Non-blocking notices and warnings for the current result.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>True when a parsed or appended history result is available to render.</summary>
    public bool HasResult { get; init; }

    /// <summary>Builds the page model from the request plus a processed page-service result.</summary>
    /// <param name="request">The posted request to preserve in the form.</param>
    /// <param name="result">The processed result returned by the page service.</param>
    public static DeckHistoryViewModel From(DeckHistoryRequest request, DeckHistoryProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);

        var file = result.File;
        var timelineRows = file?.Versions
            .OrderByDescending(version => version.Date)
            .ThenByDescending(version => version.Id)
            .Select(version =>
            {
                var addsCount = version.Delta?.Adds.Count ?? 0;
                var cutsCount = version.Delta?.Cuts.Count ?? 0;
                return (
                    version.Id,
                    version.Date,
                    version.Label,
                    version.Notes,
                    (version.Commander?.Count ?? 0) + (version.Cards?.Sum(card => card.Qty) ?? 0),
                    addsCount > 0 ? $"+{addsCount}" : "—",
                    cutsCount > 0 ? $"−{cutsCount}" : "—");
            })
            .ToArray() ?? [];

        return new DeckHistoryViewModel
        {
            ActiveTab = DeckPageTab.DeckHistory,
            Request = request,
            ErrorMessage = result.ErrorMessage,
            TimelineRows = timelineRows,
            PairDiff = result.PairDiff,
            PairOlderId = result.PairOlderId,
            PairNewerId = result.PairNewerId,
            PromptText = result.PromptText ?? string.Empty,
            HistoryJson = result.SerializedJson ?? request.HistoryJson ?? string.Empty,
            Warnings = BuildWarnings(result, file),
            HasResult = file is not null && !string.IsNullOrWhiteSpace(result.SerializedJson),
        };
    }

    private static IReadOnlyList<string> BuildWarnings(DeckHistoryProcessResult result, DeckHistoryFile? file)
    {
        var warnings = new List<string>();
        if (result.Appended && file is not null)
        {
            warnings.Add(file.Versions.Count == 1
                ? "Started a new history — version 1 saved."
                : $"Version {file.Versions[^1].Id} added.");
        }

        warnings.AddRange(result.Warnings);
        return warnings;
    }
}
