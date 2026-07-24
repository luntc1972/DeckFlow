using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;

namespace DeckFlow.Web.Models.Api;

/// <summary>Server-authored live UI patch for the Cut Lab workspace.</summary>
public sealed record CutLabUiPatchDto
{
    /// <summary>Re-serialized working-session state after the latest server mutation.</summary>
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Total card count in the current derived working list.</summary>
    public int CurrentCount { get; init; }

    /// <summary>Cards still remaining to cut in order to reach 100 cards.</summary>
    public int CardsRemaining { get; init; }

    /// <summary>True when the current working list is eligible for export.</summary>
    public bool CanBuildExport { get; init; }

    /// <summary>The next proposal to render, or a terminal marker when nothing remains to cut.</summary>
    public CutLabDecideNextProposalDto NextProposal { get; init; } = new();

    /// <summary>Metric deltas for the current next proposal, when one exists.</summary>
    public CutLabDecideProposalDeltasDto? ProposalDeltas { get; init; }

    /// <summary>Non-blocking floor warnings for the rendered proposal.</summary>
    public IReadOnlyList<CutLabDecideFloorWarningDto> FloorWarnings { get; init; } = [];

    /// <summary>Accepted cuts shown in the restore list.</summary>
    public IReadOnlyList<CutLabDecideCutRecordDto> CutsMade { get; init; } = [];

    /// <summary>Server-grouped structural findings for the updated working list.</summary>
    public IReadOnlyList<CutLabDecideFindingGroupDto> StructuralFindings { get; init; } = [];

    /// <summary>Per-card combo badge state and context keyed by normalized card name.</summary>
    public IReadOnlyDictionary<string, CutLabDecideComboBadgeDto> ComboBadgeByCardName { get; init; } =
        new Dictionary<string, CutLabDecideComboBadgeDto>(CutLabCardNames.Comparer);

    /// <summary>True when combo-backed findings were computed for this response.</summary>
    public bool ComboDataAvailable { get; init; }

    /// <summary>True when category-backed findings were computed for this response.</summary>
    public bool CategoryDataAvailable { get; init; }

    /// <summary>Working-list card options eligible to be swapped out.</summary>
    public IReadOnlyList<string> WhatifCardOutOptions { get; init; } = [];

    /// <summary>Cut-pile card options eligible to be swapped in.</summary>
    public IReadOnlyList<string> WhatifCardInOptions { get; init; } = [];

    /// <summary>Adjustment-derived working-list rows eligible for inline quantity tuning.</summary>
    public IReadOnlyList<CutLabQuantityTunerRowDto> QuantityTuners { get; init; } = [];

    /// <summary>Known basic lands not currently present in the derived working list.</summary>
    public IReadOnlyList<string> AddableBasics { get; init; } = [];
}

/// <summary>Server-authored combo badge state and disclosure context for one card.</summary>
public sealed record CutLabDecideComboBadgeDto
{
    /// <summary>Badge state for the card's combo membership.</summary>
    public ComboBadgeState BadgeState { get; init; }

    /// <summary>Required combo-context string for disclosure refresh on patch application.</summary>
    public string Context { get; init; } = string.Empty;
}

/// <summary>Server-authored quantity tuner row for one visible working-list card.</summary>
public sealed record CutLabQuantityTunerRowDto
{
    /// <summary>Display card name.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>Current quantity from the adjustment-derived working list.</summary>
    public int CurrentQuantity { get; init; }

    /// <summary>Legal upper bound for the row's quantity.</summary>
    public int LegalMax { get; init; }

    /// <summary>True when the remove action should be disabled for the current quantity.</summary>
    public bool RemoveDisabled { get; init; }

    /// <summary>True when the add action should be disabled for the current quantity.</summary>
    public bool AddDisabled { get; init; }

    /// <summary>True when the row represents a protected or commander card.</summary>
    public bool IsLockedOrCommander { get; init; }

    /// <summary>True when the row should remain visible in the live quantity tuner.</summary>
    public bool IsVisible { get; init; }

    /// <summary>User-facing role label string for the row.</summary>
    public string RoleLabel { get; init; } = string.Empty;

    /// <summary>True when the card can legally appear in multiple copies.</summary>
    public bool IsLegalMultiple { get; init; }

    /// <summary>True when this row was materialized from an added-basic adjustment.</summary>
    public bool IsAddedBasic { get; init; }
}
