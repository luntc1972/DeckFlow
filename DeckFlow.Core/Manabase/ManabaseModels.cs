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

    /// <summary>
    /// How much mana this source makes per activation (MQ-02): Sol Ring / Ancient Tomb = 2,
    /// Gilded Lotus = 3, a normal land = 1. Defaults to 1. Feeds ONLY the castability simulator's
    /// affordability/curve math — it never changes the Karsten color-SOURCE count (a 2-mana rock is
    /// still ONE source of its color). Conditional/granted sources stay 1.
    /// </summary>
    public int ManaAmount { get; init; } = 1;

    /// <summary>
    /// MQ-03 (70-03b): explicit ramp deploy cost (the turn-cost to bring this non-land source online).
    /// <see langword="null"/> (default) means "resolve it the old way" — the simulator looks the cost up
    /// from the matching mana-source spell. Set only for sources that have no <c>IsManaSource</c> spell
    /// row to key off, namely modeled land-ramp spells (Cultivate etc.), where it is the spell's mana
    /// value. Lands ignore it.
    /// </summary>
    public int? DeployCost { get; init; }

    /// <summary>
    /// True only for ENABLER-CONDITIONAL sources whose production depends on a separate permanent
    /// staying alive — the any-color sources granted by Cryptolith Rite / Relic of Legends et al.
    /// (see <c>AddGrantedSources</c>). These are genuinely speculative (the granter must resolve AND
    /// the granted creature must survive), so the simulator keeps a per-trial Bernoulli activation at
    /// <see cref="Weight"/> for them. Deployable ramp (rocks, dorks, MDFC backs, fast mana) is NOT
    /// conditional: it is a card you draw and play, and the simulator already models that friction
    /// explicitly via deploy cost + summoning-sickness timing, so its analytic weight must NOT be
    /// re-applied as activation (that double-discounts). Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsConditional { get; init; }

    /// <summary>
    /// True for a source contributed by a command-zone card (a mana-producing commander, or the
    /// commander as a granted any-color source). Such a source still counts toward color supply
    /// (a commander mana source is reliably castable) but is NOT drawn into the simulated library —
    /// the commander starts in the command zone, not the 99. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IsCommander { get; init; }
}

/// <summary>
/// Broad spell categories used to match type-scoped cost reducers (e.g. "instant and
/// sorcery spells you cast cost {1} less"). A card may carry more than one kind.
/// </summary>
[Flags]
public enum SpellKinds
{
    /// <summary>No recognized kind.</summary>
    None = 0,

    /// <summary>Creature spell.</summary>
    Creature = 1,

    /// <summary>Artifact spell.</summary>
    Artifact = 2,

    /// <summary>Instant spell.</summary>
    Instant = 4,

    /// <summary>Sorcery spell.</summary>
    Sorcery = 8,

    /// <summary>Any other spell type (enchantment, planeswalker, battle, ...).</summary>
    Other = 16,
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

    /// <summary>
    /// True when the card is itself a mana rock / dork (a non-land partial source). Such cards are
    /// excluded from the castability rows but still feed the mana and color probability pools.
    /// </summary>
    public bool IsManaSource { get; init; }

    /// <summary>The card's broad spell kinds, used to match type-scoped cost reducers.</summary>
    public SpellKinds Kinds { get; init; } = SpellKinds.None;

    /// <summary>True when this requirement is the deck's commander (or a partner/background).</summary>
    public bool IsCommander { get; init; }

    /// <summary>True when a user override / detected alt cost replaced this spell's printed cost.</summary>
    public bool IsCostOverridden { get; init; }
}

/// <summary>
/// A single spell's on-curve castability estimate: the chance it can be cast on its
/// effective turn, going first but drawing every turn (Commander is multiplayer, so the
/// starting player draws their first turn), with a 7-card opener. The product of P(enough mana) and
/// P(enough colored sources) — an approximate ranking metric (see <see cref="LimitingFactor"/>),
/// biased slightly optimistic because both factors draw on the same physical sources.
/// </summary>
public sealed record CardCastability
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>The spell's printed mana value.</summary>
    public required int ManaValue { get; init; }

    /// <summary>
    /// The effective on-curve turn after any applicable cost reducers. Equal to
    /// <see cref="ManaValue"/> when no reducer applies.
    /// </summary>
    public required int OnCurveTurn { get; init; }

    /// <summary>Estimated cast chance on the effective turn, 0–100.</summary>
    public required int CastPercent { get; init; }

    /// <summary>
    /// The bottleneck: <c>"mana"</c>, <c>"color:&lt;X&gt;"</c>, or <c>"both"</c> when the
    /// mana-quantity and color-access probabilities are within a few points.
    /// </summary>
    public required string LimitingFactor { get; init; }

    /// <summary>True when this row is the deck's commander (pinned to the top of the list).</summary>
    public bool IsCommander { get; init; }

    /// <summary>True when a user override / detected alt cost set this row's effective cost.</summary>
    public bool IsCostOverridden { get; init; }

    /// <summary>
    /// Mean turns LATE the spell first becomes castable, averaged over all trials:
    /// <c>mean(max(0, firstCastableTurn − onCurveTurn))</c>. 0 when (near-)always on curve; rises as a
    /// color- or mana-starved spell slips later. A trial that never gets there within the grace window
    /// is capped at <c>lastSimulatedTurn + 1</c>, so the metric is bounded. Supporting context only.
    /// </summary>
    public double AverageDelay { get; init; }

    /// <summary>
    /// TAP-02: count of simulated trials (out of the simulator's trial budget) in which the player
    /// had at least one mana source available to spend on turn 1. Additive — safe default 0 so
    /// existing construction/serialization is unaffected. Divided by the trial budget and averaged
    /// across non-commander rows to produce <see cref="ManabaseTapAnalysis.Turn1UntappedPercent"/>.
    /// </summary>
    public int Turn1UntappedTrials { get; init; }

    /// <summary>
    /// MULLIGAN-01: count of simulated trials (out of the trial budget) that were kept per the sim's
    /// own London-mulligan keep rule — a 7 or a 6, never the forced final 5. Additive/pure-observation
    /// (mirrors <see cref="Turn1UntappedTrials"/>); safe default 0. Equals
    /// <see cref="Kept7Trials"/> + <see cref="MulliganTo6Trials"/>.
    /// </summary>
    public int KeepableTrials { get; init; }

    /// <summary>
    /// Trials that kept a first/free 7 — bucketed by the keep VALUE <c>LondonMulligan</c> RETURNS,
    /// never the mulligan-depth index. A singleton (Commander) deck's depth-1 free mulligan is a
    /// fresh 7 (not a mull-to-6), so it counts here. Additive/pure-observation; safe default 0.
    /// </summary>
    public int Kept7Trials { get; init; }

    /// <summary>Trials that mulliganed to 6. Additive/pure-observation; safe default 0.</summary>
    public int MulliganTo6Trials { get; init; }

    /// <summary>
    /// Trials that mulliganed to 5 (the forced final keep). Additive/pure-observation; safe default 0.
    /// </summary>
    public int MulliganTo5Trials { get; init; }

    /// <summary>
    /// Up to 3 representative opening hands observed across this row's trials — one per distinct kept
    /// size (7/6/5) actually seen — each attributed to THIS row's tracked spell. Additive; empty
    /// default so existing construction/serialization is unaffected.
    /// </summary>
    public IReadOnlyList<OpeningHandSample> RepresentativeOpeners { get; init; } = Array.Empty<OpeningHandSample>();
}

/// <summary>
/// MULLIGAN-01..04: a single representative opening hand captured as PURE OBSERVATION from the
/// existing London-mulligan trial loop inside <see cref="CastabilitySimulator.Simulate"/> — no
/// second simulation. The simulator abstracts library cards to kinds (land/ramp/filler), so this
/// Core output DTO carries composition COUNTS plus the tracked spell's on-curve context, never the
/// opener's individual card names.
/// </summary>
public sealed record OpeningHandSample
{
    /// <summary>Lands in the kept hand.</summary>
    public int Lands { get; init; }

    /// <summary>Distinct colors the kept hand's lands can tap for (0-5).</summary>
    public int Colors { get; init; }

    /// <summary>Ramp pieces (mana rocks/dorks) in the kept hand.</summary>
    public int RampPieces { get; init; }

    /// <summary>Non-land, non-ramp cards in the kept hand.</summary>
    public int OtherCards { get; init; }

    /// <summary>The kept hand size (7, 6, or 5) — the same bucketing key as the row's keep-size counters.</summary>
    public int KeptCards { get; init; }

    /// <summary>The London-mulligan decision label ("keep 7", "mulligan to 6", "mulligan to 5").</summary>
    public string Decision { get; init; } = string.Empty;

    /// <summary>
    /// Name of the tracked spell whose per-row <see cref="CastabilitySimulator.Simulate"/> pass
    /// produced this sample. <see cref="OnCurveCastable"/> and <see cref="HasPlan"/> describe ONLY
    /// this spell's early play from this hand — never a generic claim.
    /// </summary>
    public string TrackedSpellName { get; init; } = string.Empty;

    /// <summary>The tracked spell's effective on-curve turn.</summary>
    public int TrackedOnCurveTurn { get; init; }

    /// <summary>
    /// True when the tracked spell first became castable on or before <see cref="TrackedOnCurveTurn"/>
    /// from this hand.
    /// </summary>
    public bool OnCurveCastable { get; init; }

    /// <summary>
    /// True ("workable line") only when this hand holds &gt;= 2 lands, the kept lands' distinct colors
    /// cover the deck's color-keep target, AND the tracked early play is actually castable on curve —
    /// never merely "a non-land card is present."
    /// </summary>
    public bool HasPlan { get; init; }
}

/// <summary>The kinds of spell an always-on cost reducer applies to.</summary>
public enum ReductionScope
{
    /// <summary>Reduces the cost of every spell ("spells you cast cost {N} less").</summary>
    All,

    /// <summary>Reduces instant and sorcery spells (e.g. Goblin Electromancer).</summary>
    InstantSorcery,

    /// <summary>Reduces creature spells.</summary>
    Creature,

    /// <summary>Reduces artifact spells.</summary>
    Artifact,
}

/// <summary>
/// An always-on static generic cost reducer the deck runs ("&lt;Type&gt;? spells you cast cost
/// {N} less"). Shifts a matching spell's effective cast turn earlier in the castability math.
/// </summary>
public sealed record CostReducer
{
    /// <summary>Generic mana removed from a matching spell's cost.</summary>
    public required int GenericReduction { get; init; }

    /// <summary>Which spells the reduction applies to.</summary>
    public required ReductionScope Scope { get; init; }

    /// <summary>
    /// The reducer's own mana value. A reducer only counts against a spell whose mana value
    /// exceeds this (the reducer must be deployable first).
    /// </summary>
    public int SourceManaValue { get; init; }
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

    /// <summary>Names of the ≤2 MV ramp/draw cards credited above, de-duplicated, in deck order.</summary>
    public IReadOnlyList<string> RampAndDrawNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Advisory-only ramp-piece count over all nonland cards. Cards that qualify as both ramp and
    /// draw count as 0.5 here and 0.5 in <see cref="DrawPieceCount"/>. Never feeds land target,
    /// color counts, castability, or health.
    /// </summary>
    public double RampPieceCount { get; init; }

    /// <summary>
    /// Advisory-only draw-piece count over all nonland cards. Cards that qualify as both draw and
    /// ramp count as 0.5 here and 0.5 in <see cref="RampPieceCount"/>. Never feeds land target,
    /// color counts, castability, or health.
    /// </summary>
    public double DrawPieceCount { get; init; }

    /// <summary>
    /// Advisory-only overlap count: cards that qualify as both ramp and draw before the 0.5/0.5
    /// split is applied to <see cref="RampPieceCount"/> and <see cref="DrawPieceCount"/>.
    /// Never feeds land target, color counts, castability, or health.
    /// </summary>
    public int RampDrawBothCount { get; init; }

    /// <summary>Count of non-mythic land/spell MDFCs (each ≈ 0.74 land off the target).</summary>
    public int MdfcCommon { get; init; }

    /// <summary>Count of mythic land/spell MDFCs (each ≈ 0.38 land off the target).</summary>
    public int MdfcMythic { get; init; }

    /// <summary>Count of 0-cost mana artifacts (Lotus, Moxen). Each substitutes ~1 land.</summary>
    public int FastMana { get; init; }

    /// <summary>True for a singleton/Commander deck (uses the 99-card formula); false for 60-card.</summary>
    public bool IsSingleton { get; init; } = true;

    /// <summary>Always-on static cost reducers the deck runs (empty when none).</summary>
    public IReadOnlyList<CostReducer> CostReduction { get; init; } = Array.Empty<CostReducer>();

    /// <summary>
    /// Auto-detected alternative / reduced-cost suggestions (free/pitch spells, board-scaling
    /// self-reducers, evoke/suspend). These pre-populate the user's override box; they do NOT
    /// change the analysis on their own — only an applied override does. Empty when none found.
    /// </summary>
    public IReadOnlyList<CostSuggestion> CostSuggestions { get; init; } = Array.Empty<CostSuggestion>();

    /// <summary>
    /// Cards whose mana requirement the analysis cannot fully model (X/variable costs that are
    /// skipped from castability; hybrid/Phyrexian pips that are approximated). Surfaced so the
    /// verdict discloses what it approximates instead of silently absorbing it. Empty when none.
    /// </summary>
    public IReadOnlyList<UnsupportedInteraction> UnsupportedInteractions { get; init; } = Array.Empty<UnsupportedInteraction>();
}

/// <summary>
/// A card the mana-base analysis cannot fully model — surfaced to the user so the verdict is honest
/// about what it approximates or skips rather than silently absorbing it.
/// </summary>
public sealed record UnsupportedInteraction
{
    /// <summary>Card name.</summary>
    public required string Name { get; init; }

    /// <summary>Short human-readable reason (e.g. "Variable (X) cost", "Hybrid/Phyrexian pips").</summary>
    public required string Reason { get; init; }
}

/// <summary>
/// A detected alternative / reduced effective cost for a single card — a suggestion the user can
/// accept or edit in the override box. <see cref="EffectiveCost"/> is a canonical braced mana
/// cost (e.g. <c>"0"</c>, <c>"{R}"</c>, <c>"{1}{B}"</c>) parseable by <see cref="ManaCostParser"/>.
/// </summary>
public sealed record CostSuggestion
{
    /// <summary>The card's display name (the override key).</summary>
    public required string Name { get; init; }

    /// <summary>The suggested effective mana cost, in canonical braced form.</summary>
    public required string EffectiveCost { get; init; }

    /// <summary>Short human reason for the suggestion (e.g. "free / alternative cost").</summary>
    public required string Reason { get; init; }
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

    /// <summary>The spell that drove the requirement (the worst single-spell deficit).</summary>
    public required string DrivingSpell { get; init; }

    /// <summary>How many of this color's spells fall below the mode's castability threshold.</summary>
    public int UnderSupportedCount { get; init; }

    /// <summary>
    /// Subset of <see cref="UnderSupportedCount"/> whose shortfall involves color access (not a pure
    /// mana/curve limit). Only this count drives the health verdict toward NeedsWork — an expensive
    /// card the base already supports color-wise is a curve problem the mana base cannot fix.
    /// </summary>
    public int ColorLimitedUnderSupportedCount { get; init; }

    /// <summary>Mean castability (0–100) across every spell demanding this color.</summary>
    public double AverageCastPercent { get; init; }

    /// <summary>The lowest single-spell castability (0–100) among this color's spells.</summary>
    public double WorstSpellCastPercent { get; init; }

    /// <summary>The name of the spell with the lowest castability for this color.</summary>
    public string WorstSpell { get; init; } = string.Empty;

    /// <summary>
    /// Display-only composition of <see cref="ActualSources"/>: weight from sources that make ONLY
    /// this color (basics, mono dorks/rocks, mono lands). The reliable, dedicated core.
    /// </summary>
    public double DirectSources { get; init; }

    /// <summary>
    /// Display-only composition of <see cref="ActualSources"/>: weight from non-conditional sources
    /// that make this color AND at least one other (dual/triome lands, any-color Moxen, Birds). Real,
    /// but shared with the deck's other colors — one such card makes one mana per turn.
    /// </summary>
    public double SharedSources { get; init; }

    /// <summary>
    /// Display-only composition of <see cref="ActualSources"/>: weight from conditional "granted"
    /// sources (a creature handed a mana ability by Cryptolith Rite / Elven Chorus). Speculative —
    /// the simulator only fires these in ~weight of games (needs the granter online and the creature
    /// alive). The three breakdown fields sum (within rounding) to <see cref="ActualSources"/>.
    /// </summary>
    public double ConditionalSources { get; init; }

    /// <summary>
    /// TAP-01: raw (un-rounded) weight of this color's sources that enter untapped, from
    /// <c>EffectiveSources(color, untappedOnly: true)</c>. Additive — safe default 0.0. The numerator
    /// for the per-color untapped fraction; the denominator is the raw total (NOT the rounded
    /// <see cref="ActualSources"/> display field).
    /// </summary>
    public double UntappedSources { get; init; }

    /// <summary>Required minus actual; positive means under-supported.</summary>
    public double Deficit => RequiredSources - ActualSources;

    /// <summary>True if the deck meets the requirement for this color.</summary>
    public bool IsAdequate => Deficit <= 0;

    /// <summary>
    /// True when adding sources of this color would actually help: it carries a color-limited
    /// shortfall (<see cref="ColorLimitedUnderSupportedCount"/> &gt; 0) or a raw source deficit. A
    /// color that is merely the weakest by tail risk but otherwise well-supported (its only late
    /// cards are curve-limited) returns false. Drives the "weakest color" alarm accent so the view
    /// does not embed the domain rule.
    /// </summary>
    public bool NeedsMoreSources => ColorLimitedUnderSupportedCount > 0 || !IsAdequate;

    /// <summary>
    /// True when this color is under-supported by the tail-risk composite (the same signal that
    /// orders <see cref="ManabaseReport.ColorFindings"/>): any spell below its mode threshold
    /// (<see cref="UnderSupportedCount"/> &gt; 0) OR a raw source deficit. Used by
    /// <see cref="ManabaseReport.WeakestColor"/> / <see cref="ManabaseReport.IsHealthy"/> so the
    /// verdict never reverts to raw deficit and drops a composite-worst color.
    /// </summary>
    public bool IsCompositeProblem => UnderSupportedCount > 0 || Deficit > 0;
}

/// <summary>
/// How heavily to weight the commander's colors in the analysis. Orthogonal to
/// <see cref="ManabaseMode"/>: it only tightens (or relaxes) the commander-color support
/// evaluation and summary weighting; it never changes the land target.
/// </summary>
public enum CommanderImportance
{
    /// <summary>
    /// "Must cast ASAP, every game" — commander colors are held to a stricter (cEDH-style)
    /// threshold, prefer untapped early sources, and may override the weakest-color ranking
    /// when below their commander-specific threshold.
    /// </summary>
    Central,

    /// <summary>Default — elevated worst-driver candidate and pinned in the list, but a worse non-commander color can still win.</summary>
    Standard,

    /// <summary>"Optional / situational / late value" — commander treated as a normal spell (still pinned for visibility).</summary>
    Low,
}

/// <summary>
/// FORMULA-01: the additive term-by-term breakdown of the Karsten land-target regression for THIS
/// deck, so the view can "show the work" — each input value and the contribution it makes to the
/// final target. All contributions sum (with their signs) to <see cref="FinalTarget"/>; the view
/// renders them as <c>scale·(19.59 + 1.90·avgMV + 0.27·cmdrs) − 0.28·ramp − fastMana −
/// 0.74·mdfcCommon − 0.38·mdfcMythic − 1.35</c>, plus the cEDH adjustment when applicable.
/// </summary>
public sealed record ManabaseLandTargetBreakdown
{
    /// <summary>The deck's mean non-land mana value (the <c>1.90·avgMV</c> regression input).</summary>
    public required double AverageManaValue { get; init; }

    /// <summary>Count of ramp/card-draw spells of mana value 2 or less (the <c>−0.28·ramp</c> input).</summary>
    public required int RampAndDrawUnderThree { get; init; }

    /// <summary>Count of 0-cost mana artifacts credited 1 land each (the <c>−fastMana</c> input).</summary>
    public required int FastMana { get; init; }

    /// <summary>Count of non-mythic land/spell MDFCs (the <c>−0.74·mdfcCommon</c> input).</summary>
    public required int MdfcCommon { get; init; }

    /// <summary>Count of mythic land/spell MDFCs (the <c>−0.38·mdfcMythic</c> input).</summary>
    public required int MdfcMythic { get; init; }

    /// <summary>Number of commanders credited (the <c>0.27·cmdrs</c> input).</summary>
    public required int CommanderCount { get; init; }

    /// <summary>Library size (deck minus commanders); drives the <c>scale = librarySize / 60</c> factor.</summary>
    public required int LibrarySize { get; init; }

    /// <summary>
    /// The singleton regression result BEFORE any cEDH adjustment — equal to
    /// <see cref="FinalTarget"/> in Casual mode.
    /// </summary>
    public required double BaseTarget { get; init; }

    /// <summary>
    /// The cEDH adjustment actually applied (the signed delta from <see cref="BaseTarget"/> to
    /// <see cref="FinalTarget"/>, after the 28-land floor). 0 in Casual mode.
    /// </summary>
    public required double CedhAdjustment { get; init; }

    /// <summary>The land target the report reports (equal to <see cref="ManabaseReport.TargetLands"/>).</summary>
    public required double FinalTarget { get; init; }
}

/// <summary>
/// Graded mana-base health on a four-tier scale (worst to best: NeedsWork, Workable, Functional,
/// Healthy). The display names the tiers Needs work / Workable / Solid / Excellent (see
/// <c>ManabaseDisplay.HealthLabel</c>). Only a real, broad mana shortage reads NeedsWork; a single
/// contained color issue is Workable; minor notes (slightly land-light, curve-limited demanding
/// cards) are Functional; a clean base is Healthy.
/// </summary>
public enum ManabaseHealth
{
    /// <summary>Display "Excellent" — land-adequate and no color has any shortfall at all.</summary>
    Healthy,

    /// <summary>
    /// Display "Solid" — the base works with only minor notes: within ~1-2 lands of target, or a few
    /// demanding cards that are curve-limited (mana, not color). No color the base can fix.
    /// </summary>
    Functional,

    /// <summary>
    /// Display "Workable" — exactly one contained color problem the base can fix: a single color short
    /// by 1-2 sources, or one color color-starved beyond tolerance. A couple of targeted swaps.
    /// </summary>
    Workable,

    /// <summary>
    /// Display "Needs work" — a real, broad shortage: lands 2+ short, a color short by more than 2
    /// sources, or two or more colors with a fixable problem.
    /// </summary>
    NeedsWork,
}

/// <summary>A demanding card surfaced by the verdict: a spell below its color's castability bar.</summary>
public sealed record DemandingCard
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>The spell's estimated on-curve cast chance, 0–100.</summary>
    public required int CastPercent { get; init; }
}

/// <summary>
/// Which single action best improves the mana base, chosen so the "biggest fix" callout never
/// contradicts the land/health line (e.g. recommending you add sources to a color that already
/// has a surplus). See <see cref="ManabaseReport.PrimaryFix"/>.
/// </summary>
public enum ManabaseFixKind
{
    /// <summary>Nothing actionable — lands and every color are adequately supported.</summary>
    None,

    /// <summary>A color has a real raw source deficit; add more sources of that color.</summary>
    ColorSources,

    /// <summary>No color is raw-short, but the land count itself is below target; add lands.</summary>
    Lands,

    /// <summary>Lands and colored sources are adequate, but some demanding spells still cast late.</summary>
    DemandingCards,
}

/// <summary>
/// The single most actionable fix for a mana base, derived from <see cref="ManabaseReport"/>. Exists
/// so the view renders a fix that agrees with the land/health stats: when the weakest color is only
/// a <i>composite</i> problem (under-supported demanding cards) rather than raw-short, the fix points
/// at lands or top-end, never at adding sources to an already-oversupplied color.
/// </summary>
public sealed record ManabasePrimaryFix
{
    /// <summary>Which kind of fix this is; drives which message the view shows.</summary>
    public required ManabaseFixKind Kind { get; init; }

    /// <summary>The color the fix concerns (ColorSources/DemandingCards), or null.</summary>
    public ManaColor? Color { get; init; }

    /// <summary>Sources to add (ColorSources) or lands to add (Lands); 0 otherwise.</summary>
    public int Amount { get; init; }

    /// <summary>Effective sources currently in the deck for <see cref="Color"/> (ColorSources only).</summary>
    public double ActualSources { get; init; }

    /// <summary>Sources required for <see cref="Color"/> (ColorSources only).</summary>
    public int RequiredSources { get; init; }

    /// <summary>The spell to cite: the driving spell (ColorSources) or worst spell (DemandingCards).</summary>
    public string Spell { get; init; } = string.Empty;

    /// <summary>How many demanding cards of <see cref="Color"/> still cast late (DemandingCards only).</summary>
    public int DemandingCount { get; init; }
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

    /// <summary>
    /// Per-color source findings, ordered by the tail-risk composite: under-supported colors
    /// first, then ascending worst-spell castability, then ascending mean castability, then
    /// descending raw deficit. So <c>ColorFindings[0]</c> is the deck's most fragile color.
    /// </summary>
    public required IReadOnlyList<ColorSourceFinding> ColorFindings { get; init; }

    /// <summary>
    /// The weakest color by the tail-risk composite, or null if every color is adequately
    /// supported. Keys off <see cref="ColorSourceFinding.IsCompositeProblem"/> (NOT raw deficit)
    /// so a composite-worst color is never dropped from the verdict.
    /// </summary>
    public ColorSourceFinding? WeakestColor =>
        ColorFindings.Count > 0 && ColorFindings[0].IsCompositeProblem ? ColorFindings[0] : null;

    /// <summary>
    /// Four-tier graded verdict. Only a REAL mana shortage the base can fix moves the needle; a land
    /// surplus, a sub-source rounding deficit, and expensive late-casting (mana-limited) bombs never
    /// do — the verdict measures the mana base, not the curve. A color "issue" is short by more than a
    /// whole source (<see cref="ColorSourceFinding.Deficit"/> &gt; 1) OR color-starved beyond
    /// <c>max(1, ceil(colorCards·0.15))</c> (counting only <see cref="ColorSourceFinding.ColorLimitedUnderSupportedCount"/>).
    /// <list type="bullet">
    /// <item><b>NeedsWork</b> ("Needs work"): a color short by more than 2 sources, OR two or more
    /// colors with an issue, OR <see cref="LandDelta"/> &lt;= -2 (lands 2+ short) <i>and</i> the sim
    /// corroborates it (a color issue or broad under-support). A raw land-count shortfall alone never
    /// forces this tier — the regression under-credits cheap ramp, so a ramp-saturated deck that the
    /// sim casts cleanly stays out of "needs work" despite a paper land deficit.</item>
    /// <item><b>Workable</b>: exactly one color with an issue (and no NeedsWork condition).</item>
    /// <item><b>Healthy</b> ("Excellent"): land-adequate (within one of target) and no color has any
    /// shortfall at all.</item>
    /// <item><b>Functional</b> ("Solid"): otherwise — works, but slightly land-light or only
    /// curve-limited demanding cards.</item>
    /// </list>
    /// </summary>
    public ManabaseHealth Health
    {
        get
        {
            ColorSignals s = ComputeColorSignals();

            // Needs work: a real, broad shortage the base can fix. A raw Karsten land-count
            // shortfall NEVER forces the worst tier on its own — the regression's ramp credit
            // under-weights cheap explosive ramp (Sol Ring, rituals, Spirit Guides), so a
            // ramp-saturated deck can read 2+ lands "short" while the castability sim shows every
            // spell casting fine. The land delta only escalates to "needs work" when the sim
            // corroborates it: a real color issue or broad under-support rides alongside.
            bool landShort = LandDelta <= -2;
            bool simFunctions =
                   UseHealthBandHeadlineFloor
                && AvgOnCurvePercent >= 85
                && WorstColorCastPercent >= 50
                && !s.AnySevereColorDeficit
                && !s.BroadColorUnderSupport;

            if (s.AnySevereColorDeficit || s.ColorsWithIssue >= 2)
            {
                return ManabaseHealth.NeedsWork;
            }

            if (landShort && (s.ColorsWithIssue >= 1 || s.BroadUnderSupport))
            {
                return (simFunctions && s.ColorsWithIssue == 1)
                    ? ManabaseHealth.Workable
                    : ManabaseHealth.NeedsWork;
            }

            // Workable: a single contained color problem the base can fix.
            if (s.ColorsWithIssue == 1)
            {
                return ManabaseHealth.Workable;
            }

            if (LandDelta >= -1 && s.EveryColorClear)
            {
                return ManabaseHealth.Healthy;
            }

            return ManabaseHealth.Functional;
        }
    }

    /// <summary>
    /// True when the deck is below the Karsten land target (<see cref="LandDelta"/> &lt; -1) yet the
    /// simulation finds no real shortage — no color has an issue and there is no broad under-support.
    /// The deck's cheap ramp (which the land-count regression under-credits) covers the paper land
    /// gap, so "add ~N lands" would contradict the Solid/Excellent verdict. The land-advice surfaces
    /// (header copy + <see cref="PrimaryFix"/>) read this so they never recommend lands the sim says
    /// are unnecessary. Shares the exact corroboration signal the <see cref="Health"/> verdict uses,
    /// so the two never disagree.
    /// </summary>
    public bool LandShortfallCoveredByRamp
    {
        get
        {
            if (LandDelta >= -1)
            {
                return false;
            }

            ColorSignals s = ComputeColorSignals();
            bool simFunctions =
                   UseHealthBandHeadlineFloor
                && AvgOnCurvePercent >= 85
                && WorstColorCastPercent >= 50
                && !s.AnySevereColorDeficit
                && !s.BroadColorUnderSupport;

            return (s.ColorsWithIssue == 0 && !s.BroadUnderSupport && !s.AnySevereColorDeficit)
                   || (simFunctions && s.ColorsWithIssue == 1);
        }
    }

    /// <summary>
    /// The sim's read on whether the base has a real, base-fixable problem. <see cref="Health"/>,
    /// <see cref="LandShortfallCoveredByRamp"/>, and <see cref="PrimaryFix"/> all consume this single
    /// computation so the verdict, the land-advice copy, and the "biggest fix" callout can never
    /// disagree about whether a paper land shortfall is genuine.
    /// </summary>
    private ColorSignals ComputeColorSignals()
    {
        int colorsWithIssue = 0;
        bool anySevereColorDeficit = false;
        bool everyColorClear = true;
        bool broadUnderSupport = false;
        bool broadColorUnderSupport = false;
        var issueFindings = new List<ColorSourceFinding>();

        // Mirror ManabaseAnalyzer's support thresholds: Casual = 80, cEDH = 88.
        // Why: the health-band castability path (UseHealthBandCastability) must gate on the same
        // per-mode bar that BuildColorFindings uses, so the sim verdict and the band agree.
        int supportThreshold = Mode == ManabaseMode.Cedh ? 88 : 80;

        // The composite-weakest color is ColorFindings[0] when it IsCompositeProblem. Only that
        // color is eligible for the sim-weakest path — a good color that just happens to have a
        // low worst-spell % is not a mana-base problem; the weakest IS because the sim already
        // ranked it composite-worst.
        ColorSourceFinding? compositeProblemWorst =
            ColorFindings.Count > 0 && ColorFindings[0].IsCompositeProblem ? ColorFindings[0] : null;

        foreach (ColorSourceFinding f in ColorFindings)
        {
            if (f.UnderSupportedCount != 0 || f.Deficit > 0)
            {
                everyColorClear = false;
            }

            int colorCards = ColorSpellCounts.TryGetValue(f.Color, out int count) ? count : 0;
            int tolerance = Math.Max(1, (int)Math.Ceiling(colorCards * 0.15));

            // A whole-source-plus deficit is a real shortage; a sub-source one is rounding noise.
            // Only COLOR-limited under-support counts — mana-limited (curve) cards are not a
            // mana-base fault. (UnderSupportedCount, all late cards, still gates Excellent below.)
            bool sourceShort = f.Deficit > 1;
            bool colorStarved = f.ColorLimitedUnderSupportedCount > tolerance;

            // MQ-health-band: the sim's composite-worst color counts as an issue when its
            // worst spell casts below the mode threshold AND at least one of those slow spells
            // is genuinely COLOR-access-limited (not merely a mana-cost curve bomb that the
            // base can't ramp into). The ColorLimitedUnderSupportedCount >= 1 guard is the
            // key: it separates Avatar/White (Suki color:White → 1) from Meren/Green or
            // graveyard-fungus/Green (Old Gnawbone/Protean Hulk are mana-limited curve
            // bombs → ColorLimitedUnderSupportedCount = 0) so those decks stay Solid.
            // Only the composite-weakest color (ColorFindings[0] when IsCompositeProblem)
            // can trigger this, so a merely sub-par color never inflates the count.
            bool simWeakestProblem = UseHealthBandCastability
                && f.Color == compositeProblemWorst?.Color
                && f.ColorLimitedUnderSupportedCount >= 1
                && f.WorstSpellCastPercent < supportThreshold;

            if (sourceShort || colorStarved || simWeakestProblem)
            {
                colorsWithIssue++;
                issueFindings.Add(f);
            }

            // The simulation's verdict that the base actually fails a meaningful slice of the
            // deck — counting ALL under-supported cards (mana- or color-limited), since a base
            // genuinely too thin shows up as widespread mana-limited misses. Used only to
            // corroborate a raw land-count shortfall.
            if (f.UnderSupportedCount > tolerance)
            {
                broadUnderSupport = true;
            }

            if (f.ColorLimitedUnderSupportedCount > tolerance)
            {
                broadColorUnderSupport = true;
            }

            if (f.Deficit > 2)
            {
                anySevereColorDeficit = true;
            }
        }

        return new ColorSignals(colorsWithIssue, anySevereColorDeficit, everyColorClear, broadUnderSupport, broadColorUnderSupport, issueFindings);
    }

    /// <summary>
    /// The per-color findings the health band counts as real issues — source-short
    /// (Deficit &gt; 1), color-starved, or the sim-weakest composite path — via the SAME
    /// <see cref="ComputeColorSignals"/> predicate the <see cref="Health"/> getter uses. The
    /// plain-language verdict consumes this (efficacy R2 finding H4) so it can never report
    /// "no changes needed" while the health chip shows Workable/Needs work. In
    /// <see cref="ColorFindings"/> order (composite-worst first).
    /// </summary>
    public IReadOnlyList<ColorSourceFinding> ColorIssueFindings => ComputeColorSignals().IssueFindings;

    /// <summary>Shared per-color corroboration signals computed once by <see cref="ComputeColorSignals"/>.</summary>
    private readonly record struct ColorSignals(
        int ColorsWithIssue,
        bool AnySevereColorDeficit,
        bool EveryColorClear,
        bool BroadUnderSupport,
        bool BroadColorUnderSupport,
        IReadOnlyList<ColorSourceFinding> IssueFindings);

    /// <summary>True only when fully <see cref="ManabaseHealth.Healthy"/>. Retained for back-compat.</summary>
    public bool IsHealthy => Health == ManabaseHealth.Healthy;

    /// <summary>
    /// The demanding cards behind a non-Healthy verdict — spells below their color's castability bar,
    /// worst-first. Empty when Healthy. Additive; lets the view show "Functional — 1 demanding card:
    /// Grand Abolisher (77%)".
    /// </summary>
    public IReadOnlyList<DemandingCard> DemandingCards { get; init; } = Array.Empty<DemandingCard>();

    /// <summary>The analysis mode this report was produced under.</summary>
    public ManabaseMode Mode { get; init; } = ManabaseMode.Casual;

    /// <summary>
    /// MQ-health-band flag. When true, the composite-weakest color's worst-spell cast % feeds
    /// <see cref="Health"/>: a color that is the deck's composite-worst AND casts its worst spell
    /// below the mode's support threshold counts as a color issue, tipping the band from Functional
    /// ("Solid") to Workable. Only the composite-weakest color can trigger this path
    /// (<see cref="ColorSourceFinding.IsCompositeProblem"/> must be true and it must be
    /// <c>ColorFindings[0]</c>). When false (default), behavior is byte-identical to before.
    /// </summary>
    public bool UseHealthBandCastability { get; init; }

    /// <summary>
    /// MQ-health-band headline-floor flag. When true, the headline average castability can narrowly
    /// promote a land-short NeedsWork verdict to Workable when exactly one soft color issue exists,
    /// no broad under-support is present, and no hard-fail color deficit exists.
    /// </summary>
    public bool UseHealthBandHeadlineFloor { get; init; }

    /// <summary>
    /// Per-spell castability rows, commander(s) pinned first then ascending by cast %. Excludes
    /// rocks/dorks (they feed the probability pools but are not real payoff spells).
    /// </summary>
    public IReadOnlyList<CardCastability> Castability { get; init; } = Array.Empty<CardCastability>();

    /// <summary>
    /// Deck-level "avg on-curve" cast rate: the rounded mean of
    /// <see cref="CardCastability.CastPercent"/> across tracked NON-commander castability rows
    /// (efficacy R2 M9). The commander is guaranteed available from the command zone and is shown in
    /// its own callout, so its (often low, high-MV) on-curve rate must not drag the deck-quality
    /// metric — and this one number is consumed by the results lens, the verdict, and the health
    /// band, so they can never disagree. Falls back to all rows if every tracked row is a commander;
    /// returns 0 for an empty set.
    /// </summary>
    public int AvgOnCurvePercent
    {
        get
        {
            long sum = 0;
            int count = 0;
            foreach (CardCastability row in Castability)
            {
                if (row.IsCommander)
                {
                    continue;
                }

                sum += row.CastPercent;
                count++;
            }

            if (count == 0)
            {
                // Degenerate: only commander rows tracked — fall back to the full set rather than 0.
                foreach (CardCastability row in Castability)
                {
                    sum += row.CastPercent;
                    count++;
                }
            }

            return count == 0 ? 0 : (int)Math.Round((double)sum / count);
        }
    }

    /// <summary>
    /// The lowest per-color worst-spell castability. Returns 100 when there are no color findings so
    /// colorless decks are not treated as catastrophic color failures.
    /// </summary>
    public double WorstColorCastPercent =>
        ColorFindings.Count == 0 ? 100 : ColorFindings.Min(f => f.WorstSpellCastPercent);

    /// <summary>
    /// How many spells demand each color (the population denominator behind COLOR-AGG's
    /// "N of M under-supported"). Additive display aid; empty when not computed.
    /// </summary>
    public IReadOnlyDictionary<ManaColor, int> ColorSpellCounts { get; init; } =
        new Dictionary<ManaColor, int>();

    /// <summary>
    /// The deck's commander color identity (union across all commanders/backgrounds), so the view
    /// can flag which color findings are the deck's identity. Additive; empty when no commander.
    /// </summary>
    public IReadOnlyList<ManaColor> CommanderColors { get; init; } = Array.Empty<ManaColor>();

    /// <summary>
    /// FORMULA-01: the additive term-by-term breakdown of the land-target regression for this deck,
    /// or null when not computed. Additive — defaults null so existing serialization/tests are
    /// unaffected. Populated by <see cref="ManabaseAnalyzer"/>.
    /// </summary>
    public ManabaseLandTargetBreakdown? LandTarget { get; init; }

    /// <summary>
    /// TAP-01/TAP-02: tap-quality metrics (untapped-source composition + turn-1 untapped
    /// availability), or null when not computed. Additive — defaults null so existing
    /// serialization/tests are unaffected. Populated by <see cref="ManabaseAnalyzer"/> when the
    /// tap-analyzer flag is on.
    /// </summary>
    public ManabaseTapAnalysis? TapAnalysis { get; init; }

    /// <summary>
    /// MULLIGAN-01..05: opening-hand / mulligan evaluation (keepable-hand band, keep-size
    /// distribution, representative openers with spell-attributed on-curve reads), or null when not
    /// computed. Additive — defaults null so existing serialization/tests are unaffected. Populated by
    /// <see cref="ManabaseAnalyzer"/> from the ALREADY-computed castability rows (no second
    /// simulation); the Web layer flag-gates display.
    /// </summary>
    public ManabaseMulliganEvaluation? MulliganEvaluation { get; init; }

    /// <summary>
    /// Count of non-land mana sources in the deck — mana rocks and dorks (artifacts/creatures that
    /// produce mana, no land face). The deck's at-a-glance ramp/acceleration piece count.
    /// </summary>
    public int RampSourceCount { get; init; }

    /// <summary>
    /// Names of the mana rocks/dorks counted by <see cref="RampSourceCount"/> (the exact
    /// <c>!IsLand &amp;&amp; !IsConditional &amp;&amp; Weight &lt;= 0.75</c> predicate), de-duplicated by name in
    /// deck order. Surfaced in the Ramp disclosure so the user can verify what was credited.
    /// </summary>
    public IReadOnlyList<string> RampSourceNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Names of the ≤2 MV ramp/draw cards counted by <see cref="ManabaseLandTargetBreakdown.RampAndDrawUnderThree"/>,
    /// de-duplicated by name in deck order. Surfaced in the Ramp disclosure.
    /// </summary>
    public IReadOnlyList<string> RampAndDrawNames { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Cards the analysis cannot fully model (X/variable costs skipped from castability;
    /// hybrid/Phyrexian pips approximated). Surfaced as a disclosure so the verdict is honest about
    /// what it skips. Empty when none.
    /// </summary>
    public IReadOnlyList<UnsupportedInteraction> UnsupportedInteractions { get; init; } = Array.Empty<UnsupportedInteraction>();

    /// <summary>Short human-readable verdict.</summary>
    public required string Summary { get; init; }

    /// <summary>
    /// The single most actionable fix, chosen land-and-source-truthfully so the "biggest fix" callout
    /// never contradicts the land/health line. Priority: a real raw color deficit, else a short land
    /// count, else demanding cards that cast late, else nothing. A color picked by the composite
    /// signal (under-supported demanding spells) but holding a source <i>surplus</i> never yields an
    /// "add ~N sources" message — that produced the negative "add ~-14" the callout used to show.
    /// </summary>
    public ManabasePrimaryFix PrimaryFix
    {
        get
        {
            // 1. A color short by more than a whole source is the most actionable fix. A sub-source
            //    deficit is rounding noise (it shares the Health verdict's tolerance), so it falls
            //    through to the land / demanding-card guidance instead of "add ~1 source".
            ColorSourceFinding? rawShort = null;
            foreach (ColorSourceFinding f in ColorFindings)
            {
                if (f.Deficit > 1 && (rawShort is null || f.Deficit > rawShort.Deficit))
                {
                    rawShort = f;
                }
            }

            if (rawShort is not null)
            {
                return new ManabasePrimaryFix
                {
                    Kind = ManabaseFixKind.ColorSources,
                    Color = rawShort.Color,
                    Amount = (int)Math.Ceiling(rawShort.Deficit),
                    ActualSources = rawShort.ActualSources,
                    RequiredSources = rawShort.RequiredSources,
                    Spell = rawShort.DrivingSpell,
                };
            }

            // 2. No color is raw-short. A short land count is the real fix — but only when the sim
            //    corroborates the shortage. A ramp-saturated deck reads land-light on the Karsten
            //    count while every spell casts fine; recommending lands there contradicts the Solid
            //    verdict (LandShortfallCoveredByRamp), so fall through to demanding-card / no-op
            //    guidance. Never recommend adding sources to an already-oversupplied color.
            if (LandDelta < -1 && !LandShortfallCoveredByRamp)
            {
                return new ManabasePrimaryFix
                {
                    Kind = ManabaseFixKind.Lands,
                    Amount = (int)Math.Ceiling(-LandDelta),
                };
            }

            // 3. Lands and sources adequate, but the weakest color still has demanding spells that
            //    cast late — point at the top end / early ramp rather than raw source count.
            if (WeakestColor is { } weak && weak.UnderSupportedCount > 0)
            {
                return new ManabasePrimaryFix
                {
                    Kind = ManabaseFixKind.DemandingCards,
                    Color = weak.Color,
                    DemandingCount = weak.UnderSupportedCount,
                    Spell = weak.WorstSpell,
                };
            }

            // 4. Nothing actionable.
            return new ManabasePrimaryFix { Kind = ManabaseFixKind.None };
        }
    }
}

/// <summary>
/// TAP-01/TAP-02: tap-quality metrics for a deck — untapped-source composition (overall and per
/// color) plus the turn-1 untapped availability figure. Derived from the existing castability
/// simulation pass; no second simulation. All fields are additive <c>{ get; init; }</c> with safe
/// defaults so the record round-trips through System.Text.Json without dropping members.
/// </summary>
public sealed record ManabaseTapAnalysis
{
    /// <summary>Overall untapped fraction (0–100) across all weighted colored sources.</summary>
    public int OverallUntappedPercent { get; init; }

    /// <summary>Weighted untapped source count (numerator for <see cref="OverallUntappedPercent"/>).</summary>
    public double UntappedSources { get; init; }

    /// <summary>Total weighted source count (denominator for <see cref="OverallUntappedPercent"/>).</summary>
    public double TotalSources { get; init; }

    /// <summary>
    /// TAP-02 (deck-level): share of simulated games (0–100) where the player had ≥1 mana source
    /// available to spend on turn 1. Averaged across non-commander castability rows (decision D1/D3).
    /// </summary>
    public int Turn1UntappedPercent { get; init; }

    /// <summary>Per-color untapped composition (key = <see cref="ManaColor"/>). Empty, never null.</summary>
    public IReadOnlyDictionary<ManaColor, ColorTapFinding> ColorTap { get; init; }
        = new Dictionary<ManaColor, ColorTapFinding>();
}

/// <summary>One color's untapped-source composition for <see cref="ManabaseTapAnalysis"/>.</summary>
public sealed record ColorTapFinding
{
    /// <summary>Weighted untapped sources of this color.</summary>
    public double UntappedSources { get; init; }

    /// <summary>
    /// Raw weighted total sources of this color (un-rounded EffectiveSources;
    /// <see cref="ColorSourceFinding.ActualSources"/> is the rounded display value).
    /// </summary>
    public double TotalSources { get; init; }

    /// <summary>Rounded untapped fraction (0–100).</summary>
    public int UntappedPercent { get; init; }
}

/// <summary>
/// MULLIGAN-01..05: the deck-level opening-hand / mulligan evaluation — a keepable-hand BAND (not a
/// false-precision percent), the London-mulligan keep-size distribution, and representative openers
/// with spell-attributed on-curve reads. Derived from the ALREADY-computed castability rows (no
/// second simulation); reuses the sim's own London-mulligan + color-keep rule, so the keepable figure
/// can never contradict the manabase tool's own cast-rate numbers. All fields are additive
/// <c>{ get; init; }</c> with safe defaults.
/// </summary>
public sealed record ManabaseMulliganEvaluation
{
    /// <summary>Share of trials (0–100) kept per the sim's own London-mulligan rule — a 7 or a 6.</summary>
    public int KeepableHandPercent { get; init; }

    /// <summary>Coarse band over <see cref="KeepableHandPercent"/>: "high" (&gt;=85), "medium" (70-84), "low" (&lt;70).</summary>
    public string KeepableBand { get; init; } = string.Empty;

    /// <summary>Share of trials (0–100) that kept a first/free 7.</summary>
    public int Kept7Percent { get; init; }

    /// <summary>Share of trials (0–100) that mulliganed to 6.</summary>
    public int MulliganTo6Percent { get; init; }

    /// <summary>Share of trials (0–100) that mulliganed to 5 (the forced final keep).</summary>
    public int MulliganTo5Percent { get; init; }

    /// <summary>Distinct colors the deck demands across its spells (reused from the deck; no new sim).</summary>
    public int ColorCount { get; init; }

    /// <summary>The deck's average mana value (reused from the deck; no new sim).</summary>
    public double AverageManaValue { get; init; }

    /// <summary>
    /// Up to 3 representative openers selected from the EARLIEST (lowest mana-value) non-commander
    /// castability rows — each already carries its own tracked-spell on-curve context, so the surfaced
    /// on-curve read is about a genuine early play, never an arbitrary tracked spell.
    /// </summary>
    public IReadOnlyList<OpeningHandSample> RepresentativeOpeners { get; init; } = Array.Empty<OpeningHandSample>();
}
