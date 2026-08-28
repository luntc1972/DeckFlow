using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON response payload for one applied Cut Lab decision.</summary>
public sealed record CutLabDecideApiResponse
{
    /// <summary>Server-authored live UI patch for the post-decision state.</summary>
    public CutLabUiPatchDto Patch { get; init; } = new();

    /// <summary>Re-serialized working-session state after applying the decision.</summary>
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>The next proposal to render, or a terminal marker when nothing remains to cut.</summary>
    public CutLabDecideNextProposalDto NextProposal { get; init; } = new();

    /// <summary>Metric deltas for the current next proposal, when one exists.</summary>
    public CutLabDecideProposalDeltasDto? ProposalDeltas { get; init; }

    /// <summary>Non-blocking floor warnings for the current proposal.</summary>
    public IReadOnlyList<CutLabDecideFloorWarningDto> FloorWarnings { get; init; } = [];

    /// <summary>Cards still remaining to cut in order to reach 100 cards.</summary>
    public int CardsRemaining { get; init; }

    /// <summary>Accepted cuts shown in the restore list.</summary>
    public IReadOnlyList<CutLabDecideCutRecordDto> CutsMade { get; init; } = [];

    /// <summary>Server-grouped structural findings for the updated working list.</summary>
    public IReadOnlyList<CutLabDecideFindingGroupDto> StructuralFindings { get; init; } = [];

    /// <summary>True when combo-backed findings were computed for this response.</summary>
    public bool ComboDataAvailable { get; init; }

    /// <summary>True when category-backed findings were computed for this response.</summary>
    public bool CategoryDataAvailable { get; init; }
}

/// <summary>Represents the next proposal card or a terminal no-more-cuts state.</summary>
public sealed record CutLabDecideNextProposalDto
{
    /// <summary>True when the queue is terminal and there is no next card to propose.</summary>
    public bool IsTerminal { get; init; }

    /// <summary>True when the terminal state means the working list is already at 100 cards.</summary>
    public bool IsAtTarget { get; init; }

    /// <summary>True when nothing is left to propose because all remaining cards are protected.</summary>
    public bool IsNothingToCut { get; init; }

    /// <summary>Display card name to propose next, or empty when terminal.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>Stable round key used for decision logging and restore routing.</summary>
    public string RoundKey { get; init; } = string.Empty;

    /// <summary>Fixed UI round label or banner text.</summary>
    public string RoundLabel { get; init; } = string.Empty;

    /// <summary>Fixed round-banner supporting copy.</summary>
    public string RoundBannerBody { get; init; } = string.Empty;

    /// <summary>Count of discriminating findings attached to the proposed card.</summary>
    public int FindingCount { get; init; }

    /// <summary>Neutral finding chips to render for the proposed card.</summary>
    public IReadOnlyList<string> FindingChips { get; init; } = [];

    /// <summary>Server-authored compact summary for the pinned proposal header.</summary>
    public string GlanceLine { get; init; } = string.Empty;
}

/// <summary>Structured metric delta payload for the next proposed cut.</summary>
public sealed record CutLabDecideProposalDeltasDto
{
    /// <summary>The proposed card name these deltas describe.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>How many metric families changed meaningfully.</summary>
    public int ChangedFamilyCount { get; init; }

    /// <summary>Per-line metric deltas.</summary>
    public IReadOnlyList<CutLabDecideMetricDeltaDto> Deltas { get; init; } = [];
}

/// <summary>One rendered metric delta line for a proposed cut.</summary>
public sealed record CutLabDecideMetricDeltaDto
{
    /// <summary>The granular metric identity.</summary>
    public CutLabMetricKind Kind { get; init; }

    /// <summary>User-facing display label.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Numeric value before the cut.</summary>
    public double Before { get; init; }

    /// <summary>Numeric value after the cut.</summary>
    public double After { get; init; }

    /// <summary>Signed numeric delta.</summary>
    public double Delta { get; init; }

    /// <summary>Display unit used for the delta values.</summary>
    public CutLabMetricUnit Unit { get; init; }

    /// <summary>Display direction for the numeric delta.</summary>
    public CutLabMetricDirection Direction { get; init; }

    /// <summary>True when the delta exceeds the configured noise floor.</summary>
    public bool IsMeaningful { get; init; }
}

/// <summary>One non-blocking floor warning returned with the current proposal.</summary>
public sealed record CutLabDecideFloorWarningDto
{
    /// <summary>Stable role key whose floor would be broken.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Role count after the proposed cut.</summary>
    public int NewCount { get; init; }

    /// <summary>User-visible floor value that would be broken.</summary>
    public int Floor { get; init; }

    /// <summary>Fixed user-facing warning copy.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>Accepted-cut restore-list row returned to the client.</summary>
public sealed record CutLabDecideCutRecordDto
{
    /// <summary>Display card name cut earlier.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>Stable round key where the cut was recorded.</summary>
    public string RoundKey { get; init; } = string.Empty;

    /// <summary>Fixed round label for the restore list row.</summary>
    public string RoundLabel { get; init; } = string.Empty;

    /// <summary>Monotonic decision ordinal for stable restore ordering.</summary>
    public int Ordinal { get; init; }
}

/// <summary>One structural finding rendered for the decide-response live patch.</summary>
public sealed record CutLabDecideFindingDto
{
    /// <summary>Underlying finding kind represented by this item.</summary>
    public CutLabFindingKind Kind { get; init; }

    /// <summary>UI heading for the finding.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>Lead sentence describing the measured issue.</summary>
    public string Lead { get; init; } = string.Empty;

    /// <summary>Preformatted supporting evidence lines for the finding.</summary>
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>Structured role display labels for findings that enumerate roles (e.g. Slot Congestion); empty otherwise.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}

/// <summary>One grouped structural-finding block rendered for the decide-response live patch.</summary>
public sealed record CutLabDecideFindingGroupDto
{
    /// <summary>Underlying finding kind represented by this rendered block.</summary>
    public CutLabFindingKind Kind { get; init; }

    /// <summary>UI heading for the rendered group.</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>One or more findings rendered inside the group.</summary>
    public IReadOnlyList<CutLabDecideFindingDto> Items { get; init; } = [];
}
