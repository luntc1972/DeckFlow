using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// View model for the <c>/Admin/CreatorProfile</c> page: the crawl-and-measure form, an optional
/// operator-facing error message, and — once a run has completed — the measured profile and its
/// deck tendencies report.
/// </summary>
public sealed class AdminCreatorProfileViewModel : AdminCreatorProfileInputModel
{
    /// <summary>Action-boundary error surfaced back to the operator.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Persisted measured style profile returned by the builder.</summary>
    public CreatorStyleProfile? Profile { get; init; }

    /// <summary>Deterministic deck-tendencies report for the crawled sample.</summary>
    public DeckTendenciesReport? Report { get; init; }
}
