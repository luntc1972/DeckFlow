namespace DeckFlow.Web.Models.CutLab;

/// <summary>
/// Serializable working-session envelope for an imported Cut Lab pool, its package/lock state,
/// and the declared deck intent that travels with the session.
/// </summary>
public sealed record CutLabState
{
    /// <summary>Resolved commander name for the working session, or empty when unknown.</summary>
    public string Commander { get; init; } = string.Empty;

    /// <summary>
    /// Imported pool cards, including commander identity and lock/package assignment state. Pool is
    /// immutable for the session: cuts are recorded in <see cref="Decisions"/>, never by removing a
    /// pool entry, and the working list is derived via CutLabWorkingList.Derive so restore is lossless.
    /// </summary>
    public IReadOnlyList<CutLabPoolCard> Pool { get; init; } = [];

    /// <summary>Named packages that can lock or unlock their assigned member cards as a unit.</summary>
    public IReadOnlyList<CutLabPackage> Packages { get; init; } = [];

    /// <summary>
    /// Persisted decision log for accepted, rejected, and deferred cut outcomes. Bounded on
    /// deserialize, and the empty initializer keeps pre-103 JSON blobs deserializing cleanly.
    /// </summary>
    public IReadOnlyList<CutLabDecision> Decisions { get; init; } = [];

    /// <summary>
    /// Original-pool compact numeric baseline snapshot computed once at pool intake and persisted
    /// with the working session, or <see langword="null"/> when not yet captured.
    /// </summary>
    public CutLabMetricSnapshot? BaselineSnapshot { get; init; }

    /// <summary>
    /// User-adjusted role floors plus their user-set flags. Derived defaults are recomputed per POST,
    /// never persisted, and the empty initializer keeps pre-102 JSON blobs deserializing cleanly.
    /// </summary>
    public IReadOnlyList<CutLabRoleFloor> RoleFloors { get; init; } = [];

    /// <summary>Declared target intent for the finished 100-card deck.</summary>
    public CutLabIntent Intent { get; init; } = new();
}

/// <summary>Persisted decision kind for one recorded cut-round outcome.</summary>
public enum CutLabDecisionKind
{
    /// <summary>The proposed cut was accepted.</summary>
    Accepted,

    /// <summary>The proposed cut was rejected.</summary>
    Rejected,

    /// <summary>The proposed cut was deferred for a later loop-around pass.</summary>
    Deferred,
}

/// <summary>Compact persisted decision log entry for one card outcome in one round.</summary>
public sealed record CutLabDecision
{
    /// <summary>Display card name this decision applies to.</summary>
    public string CardName { get; init; } = string.Empty;

    /// <summary>The recorded decision outcome.</summary>
    public CutLabDecisionKind Kind { get; init; }

    /// <summary>Stable round key or display name where the decision was recorded.</summary>
    public string Round { get; init; } = string.Empty;

    /// <summary>Monotonic decision order used for restore-any and most-recent evaluation.</summary>
    public int Ordinal { get; init; }
}

/// <summary>Serializable pool card entry tracked in the Cut Lab working session.</summary>
public sealed record CutLabPoolCard
{
    /// <summary>Display card name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Quantity of this card in the imported pool.</summary>
    public int Quantity { get; init; }

    /// <summary>Resolved type line used for bulk role-group lock rules.</summary>
    public string TypeLine { get; init; } = string.Empty;

    /// <summary>True when this card is the resolved commander for the imported pool.</summary>
    public bool IsCommander { get; init; }

    /// <summary>True when this card is protected from future cuts.</summary>
    public bool IsLocked { get; init; }

    /// <summary>Optional package identifier grouping this card with other protected cards.</summary>
    public string? PackageId { get; init; }
}

/// <summary>Serializable named package that can lock or unlock its assigned member cards together.</summary>
public sealed record CutLabPackage
{
    /// <summary>Stable package identifier referenced by assigned pool cards.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>User-facing package name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>True when the package is currently locked as a unit.</summary>
    public bool Locked { get; init; }
}

/// <summary>Serializable user floor override for one stable Cut Lab role key.</summary>
public sealed record CutLabRoleFloor
{
    /// <summary>Stable serialized role key: lands, ramp, draw, interaction, protection, engines, payoffs, or wincons.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Minimum allowed count for the role in the finished deck.</summary>
    public int Floor { get; init; }

    /// <summary>True when the user explicitly adjusted this floor away from the derived default.</summary>
    public bool IsUserSet { get; init; }
}

/// <summary>Serializable declared intent for the finished 100-card deck.</summary>
public sealed record CutLabIntent
{
    private bool _includeSideboard;
    private bool _includeMaybeboard;

    /// <summary>Required primary plan for the intended finished deck.</summary>
    public string PrimaryPlan { get; init; } = string.Empty;

    /// <summary>Optional secondary plan supporting the primary plan.</summary>
    public string? SecondaryPlan { get; init; }

    /// <summary>Optional target Commander bracket for the finished deck.</summary>
    public int? Bracket { get; init; }

    /// <summary>Desired play experience for the finished deck.</summary>
    public string PlayExperience { get; init; } = string.Empty;

    /// <summary>When true, includes the deck's sideboard cards in the Cut Lab pool as trim candidates.</summary>
    public bool IncludeSideboard
    {
        get => _includeSideboard;
        init => _includeSideboard = value || IncludeSideboardAndMaybeboard;
    }

    /// <summary>
    /// When true, includes the deck's considering or maybeboard cards in the Cut Lab pool as trim candidates.
    /// </summary>
    public bool IncludeMaybeboard
    {
        get => _includeMaybeboard;
        init => _includeMaybeboard = value || IncludeSideboardAndMaybeboard;
    }

    /// <summary>Legacy combined board flag bridge for session JSON created before the split-toggle refinement.</summary>
    public bool IncludeSideboardAndMaybeboard
    {
        get => IncludeSideboard && IncludeMaybeboard;
        init
        {
            if (!value)
            {
                return;
            }

            _includeSideboard = true;
            _includeMaybeboard = true;
        }
    }
}
