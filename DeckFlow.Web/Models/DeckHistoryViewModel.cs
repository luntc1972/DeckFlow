using DeckFlow.Core.History;
using DeckFlow.Web.Services;

namespace DeckFlow.Web.Models;

/// <summary>Rendered timeline row for a single saved history version.</summary>
public sealed record TimelineRow
{
    /// <summary>DeckFlow-assigned version id.</summary>
    public int Id { get; init; }

    /// <summary>Version timestamp.</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>Optional short label for the version.</summary>
    public string? Label { get; init; }

    /// <summary>Optional notes explaining the change.</summary>
    public string? Notes { get; init; }

    /// <summary>Total commander plus mainboard card count shown in the timeline.</summary>
    public int CardCount { get; init; }

    /// <summary>Number of cards added versus the prior version.</summary>
    public int AddsCount { get; init; }

    /// <summary>Number of cards cut versus the prior version.</summary>
    public int CutsCount { get; init; }

    /// <summary>Display text for adds, derived from <see cref="AddsCount"/>.</summary>
    public string AddsText => AddsCount > 0 ? $"+{AddsCount}" : string.Empty;

    /// <summary>Display text for cuts, derived from <see cref="CutsCount"/>.</summary>
    public string CutsText => CutsCount > 0 ? $"−{CutsCount}" : string.Empty;

    /// <summary>True when either adds or cuts are non-zero.</summary>
    public bool HasDelta => AddsCount > 0 || CutsCount > 0;
}

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
    public IReadOnlyList<TimelineRow> TimelineRows { get; init; } = [];

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

    /// <summary>Positive append/create notice rendered separately from warnings.</summary>
    public string? SuccessMessage { get; init; }

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
                return new TimelineRow
                {
                    Id = version.Id,
                    Date = version.Date,
                    Label = version.Label,
                    Notes = version.Notes,
                    CardCount = (version.Commander?.Count ?? 0) + (version.Cards?.Sum(card => card.Qty) ?? 0),
                    AddsCount = addsCount,
                    CutsCount = cutsCount,
                };
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
            PromptText = result.PromptText,
            HistoryJson = result.SerializedJson ?? request.HistoryJson,
            SuccessMessage = BuildSuccessMessage(result, file),
            Warnings = result.Warnings,
            HasResult = file is not null,
        };
    }

    private static string? BuildSuccessMessage(DeckHistoryProcessResult result, DeckHistoryFile? file)
    {
        if (!result.Appended || file is null)
        {
            return null;
        }

        return file.Versions.Count == 1
            ? "Started a new history — version 1 saved."
            : $"Version {file.Versions[^1].Id} added.";
    }
}
