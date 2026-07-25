using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.Manabase;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>
/// Assigns each pool card to zero or more of Cut Lab's eight structural role keys using only the
/// existing role and deck-stat classifiers. This taxonomy is wider than <see cref="PlanRole"/>:
/// lands, ramp, and filler draw still matter for slot competition even though
/// <see cref="PlanRoleClassifier"/> deliberately excludes them from plan-presence roles. Multi-role
/// membership is allowed; cutting a card reduces every role count it currently fills.
/// </summary>
public static class CutLabRoleAssigner
{
    private const string LandsRole = "lands";
    private const string RampRole = "ramp";
    private const string DrawRole = "draw";
    private const string InteractionRole = "interaction";
    private const string ProtectionRole = "protection";
    private const string EnginesRole = "engines";
    private const string PayoffsRole = "payoffs";
    private const string WinconsRole = "wincons";
    private const string OtherRole = "other";

    private static readonly string[] RoleKeys =
    [
        LandsRole,
        RampRole,
        DrawRole,
        InteractionRole,
        ProtectionRole,
        EnginesRole,
        PayoffsRole,
        WinconsRole,
    ];

    // Shared primary-type order used both for UI grouping and deterministic service-side ranking.
    internal static readonly string[] TypeGroupOrder =
    [
        "Creature",
        "Planeswalker",
        "Battle",
        "Instant",
        "Sorcery",
        "Artifact",
        "Enchantment",
        "Land",
        "Other",
    ];

    internal static readonly IReadOnlyDictionary<string, string> RoleDisplayLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["lands"] = "Lands",
            ["ramp"] = "Ramp",
            ["draw"] = "Card draw",
            ["interaction"] = "Interaction",
            ["protection"] = "Protection",
            ["engines"] = "Engines",
            ["payoffs"] = "Payoffs",
            ["wincons"] = "Win conditions",
            ["other"] = "Other",
        };

    /// <summary>Maps the Cut Lab play-experience string to the shared classifier mode.</summary>
    /// <param name="playExperience">User-selected play-experience label.</param>
    /// <returns>The matching mode, or <see cref="ManabaseMode.Casual"/> when unspecified or unknown.</returns>
    public static ManabaseMode ResolveMode(string? playExperience)
    {
        if (string.Equals(playExperience, "cEDH", StringComparison.OrdinalIgnoreCase))
        {
            return ManabaseMode.Cedh;
        }

        if (string.Equals(playExperience, "Focused", StringComparison.OrdinalIgnoreCase))
        {
            return ManabaseMode.Focused;
        }

        return ManabaseMode.Casual;
    }

    internal static string DisplayLabelFor(string roleKey)
        => RoleDisplayLabels.TryGetValue(roleKey, out string? label) ? label : roleKey;

    /// <summary>
    /// Assigns the fixed-order Cut Lab role keys for a card using only existing classifier signals.
    /// </summary>
    /// <param name="fact">Resolved card fact.</param>
    /// <param name="categories">Crowd-sourced category tags for the card.</param>
    /// <param name="isComboPiece">Whether Commander Spellbook lists the card in an included combo.</param>
    /// <param name="mode">Classifier mode derived from play experience.</param>
    /// <returns>The subset of role keys the card fills, in canonical order.</returns>
    public static IReadOnlyList<string> AssignRoles(
        CardFact fact,
        IReadOnlyList<string> categories,
        bool isComboPiece,
        ManabaseMode mode)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(categories);

        string typeLine = fact.TypeLine;
        string oracle = fact.FrontFaceOracleText ?? fact.OracleText ?? string.Empty;
        bool isLand = CutLabLockRules.IsLand(typeLine) || fact.HasLandFace;
        PlanRole roles = PlanRoleClassifier.Classify(fact, categories, isComboPiece, mode, out bool interactionMeritPreGate);

        List<string> assigned = new(RoleKeys.Length);

        if (isLand)
        {
            assigned.Add(LandsRole);
        }

        // Why: DeckStatClassifier.IsRampCard first returns true for every type line containing
        // "Land", so without the gate every land would double-count as ramp and inflate downstream
        // role counts. Lands and ramp stay disjoint by construction.
        if (!isLand && DeckStatClassifier.IsRampCard(typeLine, oracle))
        {
            assigned.Add(RampRole);
        }

        if (DeckStatClassifier.IsDrawCard(oracle))
        {
            assigned.Add(DrawRole);
        }

        if (interactionMeritPreGate
            || DeckStatClassifier.IsBoardWipeCard(oracle)
            || DeckStatClassifier.IsTargetedRemovalCard(typeLine, oracle))
        {
            assigned.Add(InteractionRole);
        }

        if (DeckStatClassifier.IsProtectionCard(fact.Name, oracle))
        {
            assigned.Add(ProtectionRole);
        }

        // Why: the shared PlanRoleClassifier keeps Engine on one-shot card advantage for the manabase
        // plan-presence lens (locked 2026-07-09), but Cut Lab's role display wants true repeatable
        // engines. Gate locally on permanent + repeatable draw, mirroring FromHeuristic's Engine rule,
        // so one-shot "draw two" spells tagged "card draw"/"value" no longer flood the engines role.
        if (roles.HasFlag(PlanRole.Engine)
            && !CardTypeLine.IsNonPermanentFront(typeLine)
            && DeckStatClassifier.IsDrawCard(oracle))
        {
            assigned.Add(EnginesRole);
        }

        if (roles.HasFlag(PlanRole.Payoff))
        {
            assigned.Add(PayoffsRole);
        }

        if (DeckStatClassifier.IsClosingPowerCard(typeLine, oracle) || isComboPiece)
        {
            assigned.Add(WinconsRole);
        }

        if (assigned.Count == 0)
        {
            assigned.Add(OtherRole);
        }

        return assigned;
    }
}
