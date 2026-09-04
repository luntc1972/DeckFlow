using System.Globalization;
using DeckFlow.Core.Research;
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

    /// <summary>Actual lands in the current working-pool simulation, when available.</summary>
    public int? ActualLands { get; init; }

    /// <summary>Target lands in the current working-pool simulation, when available.</summary>
    public double? TargetLands { get; init; }

    /// <summary>Resolved role-floor rows for synchronizing the floor table, or null when not computed on this code path.</summary>
    public IReadOnlyList<CutLabResolvedFloorDto>? ResolvedFloors { get; init; }

    /// <summary>True when the current working list is eligible for export.</summary>
    public bool CanBuildExport { get; init; }

    /// <summary>The next proposal to render, null when no proposal is available, or a terminal marker when nothing remains to cut.</summary>
    public CutLabDecideNextProposalDto? NextProposal { get; init; }

    /// <summary>Metric deltas for the current next proposal, when one exists.</summary>
    public CutLabDecideProposalDeltasDto? ProposalDeltas { get; init; }

    /// <summary>Incremental card-popup data keyed by card name for live modal refreshes.</summary>
    public IReadOnlyDictionary<string, CutLabCardTextView> CardTextByCardName { get; init; } =
        new Dictionary<string, CutLabCardTextView>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Non-blocking floor warnings for the rendered proposal.</summary>
    public IReadOnlyList<CutLabDecideFloorWarningDto> FloorWarnings { get; init; } = [];

    /// <summary>Accepted cuts shown in the restore list.</summary>
    public IReadOnlyList<CutLabDecideCutRecordDto> CutsMade { get; init; } = [];

    /// <summary>Server-grouped structural findings for the updated working list.</summary>
    public IReadOnlyList<CutLabDecideFindingGroupDto> StructuralFindings { get; init; } = [];

    /// <summary>Read-only locked-overshoot advisory for terminal over-target states, when applicable.</summary>
    public CutLabLockedOvershootAdvisoryDto? LockedOvershootAdvisory { get; init; }

    /// <summary>Per-card combo badge state and context keyed by raw rendered pool name.</summary>
    public IReadOnlyDictionary<string, CutLabDecideComboBadgeDto> ComboBadgeByCardName { get; init; } =
        new Dictionary<string, CutLabDecideComboBadgeDto>(StringComparer.Ordinal);

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

/// <summary>API contract for the locked-overshoot advisory.</summary>
public sealed record CutLabLockedOvershootAdvisoryDto
{
    /// <summary>How many cards the pool remains over target.</summary>
    public int CardsOverTarget { get; init; }

    /// <summary>How many ranked cards were omitted after the top-20 cap.</summary>
    public int HiddenCount { get; init; }

    /// <summary>Grouped role buckets for the advisory.</summary>
    public IReadOnlyList<CutLabLockedOvershootGroupDto> Groups { get; init; } = [];
}

/// <summary>One grouped role bucket in the locked-overshoot advisory.</summary>
public sealed record CutLabLockedOvershootGroupDto
{
    /// <summary>User-facing role label.</summary>
    public string RoleLabel { get; init; } = string.Empty;

    /// <summary>Suggested card names in rank order for this role bucket.</summary>
    public IReadOnlyList<string> CardNames { get; init; } = [];
}

/// <summary>API contract for a resolved role-floor row.</summary>
public sealed record CutLabResolvedFloorDto
{
    /// <summary>Creates API floor rows from resolved domain floors and current role counts.</summary>
    public static IReadOnlyList<CutLabResolvedFloorDto> Create(
        IReadOnlyList<CutLabResolvedFloor> resolvedFloors,
        IReadOnlyDictionary<string, int> countsByRole,
        string playExperience)
        => resolvedFloors.Select(floor =>
        {
            int inPoolCount = countsByRole.TryGetValue(floor.Role, out int count) ? count : 0;
            bool supportsCommanderFloor = RoleFloorBaseline.AdoptedRoleKeys.Contains(floor.Role, StringComparer.OrdinalIgnoreCase);
            string commanderDisplay = supportsCommanderFloor
                ? floor.CommanderValue?.ToString(CultureInfo.InvariantCulture)
                    // Why: per D-08 the shipped snapshot cannot distinguish "commander absent from the corpus"
                    // from "commander present, role did not clear the bar", so the UI must not claim to.
                    ?? "—"
                // Why: D-12 requires `n/a` for structurally out-of-scope roles because a bare dash would
                // imply the tool looked and found nothing, when lands was deliberately pulled at the
                // Phase 2 checkpoint and interaction-mass/protection were ruled out for insufficient breadth.
                : "n/a";
            string sourceLabel = floor.CommanderValue is int commander && commander > floor.BracketValue
                // Why: the label names which number actually drove the effective default, so a tie reads as
                // Bracket because the bracket band alone already produced that number.
                ? "Commander"
                : "Bracket";

            return new CutLabResolvedFloorDto
            {
                RoleKey = floor.Role,
                InPoolCount = inPoolCount,
                BracketValue = floor.BracketValue,
                CommanderDisplay = commanderDisplay,
                Floor = floor.Floor,
                DefaultValue = floor.DefaultValue,
                PlanDelta = floor.PlanDelta,
                IsUserSet = floor.IsUserSet,
                SourceLabel = sourceLabel,
                SourceDetail = floor.BracketWasFallback
                    ? $"Default: {floor.DefaultValue} — based on {FallbackSource(playExperience)}"
                    : $"Default for B{floor.ResolvedBracket}: {floor.DefaultValue}",
            };
        }).ToArray();

    /// <summary>Role identifier used by the floor table.</summary>
    public string RoleKey { get; init; } = string.Empty;

    /// <summary>Current number of cards in the role.</summary>
    public int InPoolCount { get; init; }

    /// <summary>Resolved bracket contribution.</summary>
    public int BracketValue { get; init; }

    /// <summary>Formatted commander contribution.</summary>
    public string CommanderDisplay { get; init; } = string.Empty;

    /// <summary>Effective floor.</summary>
    public int Floor { get; init; }

    /// <summary>Default floor before user override.</summary>
    public int DefaultValue { get; init; }

    /// <summary>Plan-profile adjustment to the floor.</summary>
    public int PlanDelta { get; init; }

    /// <summary>Whether the user explicitly set the floor.</summary>
    public bool IsUserSet { get; init; }

    /// <summary>Label identifying the effective floor source.</summary>
    public string SourceLabel { get; init; } = string.Empty;

    /// <summary>Tooltip describing the default floor source.</summary>
    public string SourceDetail { get; init; } = string.Empty;

    private static string FallbackSource(string playExperience)
    {
        if (!string.IsNullOrWhiteSpace(playExperience))
        {
            return playExperience;
        }

        return "your play experience";
    }
}
