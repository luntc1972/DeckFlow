namespace DeckFlow.Core.Manabase;

/// <summary>
/// A land (or partial mana source) and the colors it can produce. Weight allows
/// discounting fragile or conditional sources per Karsten's counting rules
/// (mana dork ≈ 0.5, Signet ≈ 0.75, choice-fetch in 3+ colors ≈ 0.67).
/// </summary>
public sealed record ManaSource
{
    /// <summary>Display name (for findings/diagnostics).</summary>
    public required string Name { get; init; }

    /// <summary>Colors this source can tap for.</summary>
    public required IReadOnlyList<ManaColor> Produces { get; init; }

    /// <summary>Effective source weight (1.0 for a normal land). Defaults to a full source.</summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// True if this source occupies a land slot (counts toward the land-drop total), even when
    /// its color weight is discounted (e.g. a basic-fetch at 0.67). Partial non-land sources
    /// — mana dorks, rocks, MDFC spell-backs — are <see langword="false"/>.
    /// </summary>
    public bool IsLand { get; init; } = true;

    /// <summary>True if it can produce mana the turn it is played (matters only for turn-1 pips).</summary>
    public bool EntersUntapped { get; init; } = true;
}

/// <summary>
/// A spell's colored requirement: how many pips of each color it needs and when it is
/// first castable on curve (its mana value).
/// </summary>
public sealed record SpellRequirement
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Total mana value — the turn the spell is cast on curve.</summary>
    public required int ManaValue { get; init; }

    /// <summary>Colored pip counts by color (omit colors with zero pips).</summary>
    public required IReadOnlyDictionary<ManaColor, int> Pips { get; init; }

    /// <summary>True if the card needs more than one color (gold) and both colors are consistency-critical.</summary>
    public bool IsGold { get; init; }
}

/// <summary>
/// A fully classified deck ready for mana-base analysis: its lands, its colored spells,
/// and the aggregate numbers the land-count formula needs.
/// </summary>
public sealed record ManabaseDeck
{
    /// <summary>Total cards in the deck including commanders (typically 100 for Commander, 60 for constructed).</summary>
    public required int TotalCards { get; init; }

    /// <summary>Number of commanders sitting in the command zone (0 for 60-card formats).</summary>
    public int CommanderCount { get; init; }

    /// <summary>All lands / mana sources in the deck.</summary>
    public required IReadOnlyList<ManaSource> Sources { get; init; }

    /// <summary>Colored spells whose castability we want to check.</summary>
    public required IReadOnlyList<SpellRequirement> Spells { get; init; }

    /// <summary>Mean mana value of the non-land cards.</summary>
    public required double AverageManaValue { get; init; }

    /// <summary>Count of ramp/card-draw spells of mana value 2 or less.</summary>
    public int RampAndDrawUnderThree { get; init; }

    /// <summary>Count of non-mythic land/spell MDFCs (each ≈ 0.74 land off the target).</summary>
    public int MdfcCommon { get; init; }

    /// <summary>Count of mythic land/spell MDFCs (each ≈ 0.38 land off the target).</summary>
    public int MdfcMythic { get; init; }

    /// <summary>Count of 0-cost mana artifacts (Lotus, Moxen). Each substitutes ~1 land.</summary>
    public int FastMana { get; init; }

    /// <summary>True for a singleton/Commander deck (uses the 99-card formula); false for 60-card.</summary>
    public bool IsSingleton { get; init; } = true;
}

/// <summary>One color's source supply versus its toughest requirement in the deck.</summary>
public sealed record ColorSourceFinding
{
    /// <summary>The color examined.</summary>
    public required ManaColor Color { get; init; }

    /// <summary>Effective sources of this color currently in the deck (weighted).</summary>
    public required double ActualSources { get; init; }

    /// <summary>Sources required by the most demanding spell of this color (Karsten threshold).</summary>
    public required int RequiredSources { get; init; }

    /// <summary>The spell that drove the requirement.</summary>
    public required string DrivingSpell { get; init; }

    /// <summary>Required minus actual; positive means under-supported.</summary>
    public double Deficit => RequiredSources - ActualSources;

    /// <summary>True if the deck meets the requirement for this color.</summary>
    public bool IsAdequate => Deficit <= 0;
}

/// <summary>The §6 mana-base report: land count, ramp, per-color sources, and a verdict.</summary>
public sealed record ManabaseReport
{
    /// <summary>Lands actually in the deck.</summary>
    public required int ActualLands { get; init; }

    /// <summary>Karsten-recommended land count for the curve.</summary>
    public required double TargetLands { get; init; }

    /// <summary>Actual minus target; negative means too few lands.</summary>
    public double LandDelta => ActualLands - TargetLands;

    /// <summary>Per-color source findings, ordered worst-deficit first.</summary>
    public required IReadOnlyList<ColorSourceFinding> ColorFindings { get; init; }

    /// <summary>The color with the largest source deficit, or null if every color is adequate.</summary>
    public ColorSourceFinding? WeakestColor =>
        ColorFindings.Count > 0 && ColorFindings[0].Deficit > 0 ? ColorFindings[0] : null;

    /// <summary>True if land count is within one of target and every color is adequate.</summary>
    public bool IsHealthy => LandDelta >= -1 && WeakestColor is null;

    /// <summary>Short human-readable verdict.</summary>
    public required string Summary { get; init; }
}
