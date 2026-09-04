using DeckFlow.Core.Analysis;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Research;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.Manabase;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure bracket- and play-experience-derived default floor rules for the nine Cut Lab roles.</summary>
public static class CutLabFloorDefaults
{
    // Why: these generalize the design spec's worked examples (combo raises tutoring and protection;
    // combat raises the wincon creature count) across all twelve strategies. Values stay deliberately
    // small because bracket bands and commander p25 remain the base; the omitted land role is resolved
    // through ResolveLandsDefault's separate Phase 2 baseline chain.
    internal static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PlanFloorDeltas =
        new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
        {
            ["combo"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["protection"] = 1, ["wincons"] = 1 },
            ["aristocrats"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["engines"] = 1, ["payoffs"] = 1 },
            ["voltron"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["protection"] = 2 },
            ["tokens"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["payoffs"] = 1 },
            ["spellslinger"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["draw"] = 1 },
            ["stax"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["interaction-mass"] = 1, ["protection"] = 1 },
            ["reanimator"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["engines"] = 1 },
            ["landfall"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["ramp"] = 1 },
            ["lifegain"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["payoffs"] = 1 },
            ["counters"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["payoffs"] = 1 },
            ["combat"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["wincons"] = 1 },
            ["control"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["interaction-targeted"] = 1, ["interaction-mass"] = 1 },
        };

    /// <summary>[ASSUMED] Fallback lands floor when no baseline row is available.</summary>
    public const int FallbackLands = 36;

    /// <summary>Resolves the effective floor bracket from the declared bracket and play experience.</summary>
    /// <param name="declaredBracket">User-declared bracket, when present.</param>
    /// <param name="playExperience">User-declared play-experience string.</param>
    /// <param name="wasFallback">True when the returned bracket was normalized or inferred instead of taken directly.</param>
    /// <returns>The effective bracket used to derive floors.</returns>
    public static int ResolveBracket(int? declaredBracket, string playExperience, out bool wasFallback)
    {
        ArgumentNullException.ThrowIfNull(playExperience);

        if (declaredBracket is >= 2 and <= 5)
        {
            wasFallback = false;
            return declaredBracket.Value;
        }

        if (declaredBracket == 1)
        {
            wasFallback = true;
            return 2;
        }

        wasFallback = true;
        return playExperience switch
        {
            _ when string.Equals(playExperience, "cEDH", StringComparison.OrdinalIgnoreCase) => 5,
            _ when string.Equals(playExperience, "Focused", StringComparison.OrdinalIgnoreCase) => 3,
            _ => 2,
        };
    }

    /// <summary>Resolves all nine Cut Lab role floors in stable role order, merging in any user overrides.</summary>
    /// <param name="declaredBracket">User-declared bracket, when present.</param>
    /// <param name="playExperience">User-declared play-experience string.</param>
    /// <param name="commanderManaValue">Resolved commander mana value used for the ramp/draw split.</param>
    /// <param name="commanderNames">Resolved commander names for optional cEDH lands baseline lookup.</param>
    /// <param name="baseline">Optional bundled per-bracket baseline provider.</param>
    /// <param name="cedhBaseline">Optional commander-keyed cEDH lands baseline provider.</param>
    /// <param name="roleFloorBaseline">Optional commander-keyed role-floor baseline provider.</param>
    /// <param name="priorFloors">Previously persisted user floor entries from the working session.</param>
    /// <param name="planProfile">Optional checked generic strategies that can raise their named role floors.</param>
    /// <returns>One resolved floor row per Cut Lab role.</returns>
    public static IReadOnlyList<CutLabResolvedFloor> ResolveDefaults(
        int? declaredBracket,
        string playExperience,
        double commanderManaValue,
        IReadOnlyList<string> commanderNames,
        IManabaseBaselineProvider? baseline,
        ICedhLandBaselineProvider? cedhBaseline,
        IRoleFloorBaselineProvider? roleFloorBaseline,
        IReadOnlyList<CutLabRoleFloor> priorFloors,
        CutLabPlanProfile? planProfile = null)
    {
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);
        ArgumentNullException.ThrowIfNull(priorFloors);

        IReadOnlyDictionary<string, int> planDeltas = ResolvePlanDeltas(planProfile);

        int resolvedBracket = ResolveBracket(declaredBracket, playExperience, out bool bracketWasFallback);
        int landsDefault = ResolveLandsDefault(resolvedBracket, commanderNames, baseline, cedhBaseline);
        int rampDefault = ManabaseRampDrawBudgetCalculator.CalculateTargetRamp(commanderManaValue);
        // Why: 24 - rampDefault remains the bracket-derived draw component, mirroring
        // ManabaseRampDrawBudgetCalculator's fixed split. After commander-aware max() resolution,
        // ramp and draw are independent minimums and may sum past 24 because floors are minimums,
        // not a budget.
        int drawDefault = 24 - rampDefault;

        Dictionary<string, CutLabRoleFloor> userOverrides = GetUserOverrides(priorFloors);
        List<CutLabResolvedFloor> resolved = [];

        foreach (string role in CutLabFloorRules.RoleKeys)
        {
            int bracketValue = role switch
            {
                "lands" => landsDefault,
                "ramp" => rampDefault,
                "draw" => drawDefault,
                _ => GetBracketBand(role, resolvedBracket),
            };
            int? commanderValue = null;
            // Why: lands is deliberately excluded because the Phase 2 Postgres arm measured distinct
            // land names rather than land count, and interaction-mass/protection are out of scope for
            // insufficient breadth. RoleFloorBaseline.AdoptedRoleKeys is the single source of the six
            // commander-aware GO roles.
            if (roleFloorBaseline is not null
                && RoleFloorBaseline.AdoptedRoleKeys.Contains(role, StringComparer.OrdinalIgnoreCase)
                && roleFloorBaseline.TryGetRoleFloor(commanderNames, role, out int commanderFloor))
            {
                commanderValue = commanderFloor;
            }

            // Why: the bracket bands are prescriptive product opinion, while commander p25 is
            // descriptive of what people actually build; max() is the reconciliation. At brackets 4-5,
            // all 124 of 124 adopting payoffs commanders sit below the band, so a literal priority
            // chain would delete that guardrail outright. Both numbers stay visible because they answer
            // different questions.
            int effectiveDefault = Math.Max(bracketValue, commanderValue ?? 0);
            int planDelta = planDeltas[role];
            effectiveDefault = Math.Clamp(effectiveDefault + planDelta, 0, CutLabFloorRules.MaxFloor);

            bool isUserSet = userOverrides.TryGetValue(role, out CutLabRoleFloor? overrideFloor);
            resolved.Add(new CutLabResolvedFloor
            {
                Role = role,
                Floor = isUserSet ? overrideFloor!.Floor : effectiveDefault,
                IsUserSet = isUserSet,
                DefaultValue = effectiveDefault,
                BracketValue = bracketValue,
                CommanderValue = commanderValue,
                ResolvedBracket = resolvedBracket,
                BracketWasFallback = bracketWasFallback,
                PlanDelta = planDelta,
            });
        }

        return resolved;
    }

    /// <summary>Resolves checked strategy raises, composing overlapping role deltas by maximum.</summary>
    internal static IReadOnlyDictionary<string, int> ResolvePlanDeltas(CutLabPlanProfile? planProfile)
    {
        var resolved = CutLabFloorRules.RoleKeys.ToDictionary(role => role, _ => 0, StringComparer.Ordinal);
        if (planProfile is null)
        {
            return resolved;
        }

        foreach (string slug in planProfile.GenericStrategies)
        {
            if (!DeckPlanStrategyCatalog.TryGetBySlug(slug, out DeckPlanStrategyEntry strategy)
                || !PlanFloorDeltas.TryGetValue(strategy.Slug, out IReadOnlyDictionary<string, int>? deltas))
            {
                continue;
            }

            foreach ((string role, int delta) in deltas)
            {
                // Why: table entries are hand-maintained and must never turn a configuration typo into a render-path 500.
                if (!resolved.ContainsKey(role))
                {
                    continue;
                }

                resolved[role] = Math.Max(resolved[role], delta);
            }
        }

        return resolved;
    }

    /// <summary>Returns the default targeted-interaction floor for the requested bracket.</summary>
    internal static int GetDefaultInteractionTargetedFloor(int bracket)
    {
        int normalizedBracket = bracket switch
        {
            <= 2 => 2,
            3 => 3,
            4 => 4,
            _ => 5,
        };

        return normalizedBracket switch
        {
            2 => 4,
            3 => 5,
            4 => 7,
            _ => 9,
        };
    }

    internal static int GetDefaultInteractionMassFloor(int bracket)
    {
        int normalizedBracket = bracket switch
        {
            <= 2 => 2,
            3 => 3,
            4 => 4,
            _ => 5,
        };

        return normalizedBracket switch
        {
            2 => 2,
            3 => 3,
            4 => 3,
            _ => 3,
        };
    }

    /// <summary>[ASSUMED] Unsigned product constants for non-lands, non-ramp, non-draw role floors.</summary>
    /// <remarks>User-adjustable via FLOOR-02; chosen during planning and awaiting product sign-off.</remarks>
    private static int GetBracketBand(string role, int bracket)
    {
        int normalizedBracket = bracket switch
        {
            <= 2 => 2,
            3 => 3,
            4 => 4,
            _ => 5,
        };

        return role switch
        {
            // Why: the split preserves the shipped interaction budget while biasing higher brackets toward targeted answers.
            "interaction-targeted" => GetDefaultInteractionTargetedFloor(normalizedBracket),
            // Why: sweepers matter at every bracket, but the default demand flattens once decks optimize around narrower answers.
            "interaction-mass" => GetDefaultInteractionMassFloor(normalizedBracket),
            // Why: protection scales with combo pressure but stays below interaction because it is narrower.
            "protection" => normalizedBracket switch { 2 => 2, 3 => 3, 4 => 4, _ => 5 },
            // Why: engine density rises through optimized play, then flattens because engines self-sustain.
            "engines" => normalizedBracket switch { 2 => 4, 3 => 5, 4 => 6, _ => 6 },
            // Why: payoffs track engines so plans have enough closers without overloading dead finishers.
            "payoffs" => normalizedBracket switch { 2 => 4, 3 => 5, 4 => 6, _ => 6 },
            // Why: most decks need only a small number of true wins even as optimization increases.
            "wincons" => normalizedBracket switch { 2 => 2, 3 => 2, 4 => 3, _ => 3 },
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported Cut Lab role key."),
        };
    }

    private static Dictionary<string, CutLabRoleFloor> GetUserOverrides(IReadOnlyList<CutLabRoleFloor> priorFloors)
    {
        Dictionary<string, CutLabRoleFloor> overrides = new(StringComparer.OrdinalIgnoreCase);
        foreach (CutLabRoleFloor floor in priorFloors)
        {
            if (!floor.IsUserSet || !CutLabFloorRules.RoleKeys.Contains(floor.Role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            overrides[floor.Role] = floor;
        }

        return overrides;
    }

    private static int ResolveLandsDefault(
        int resolvedBracket,
        IReadOnlyList<string> commanderNames,
        IManabaseBaselineProvider? baseline,
        ICedhLandBaselineProvider? cedhBaseline)
    {
        if (resolvedBracket == 5
            && cedhBaseline is not null
            && cedhBaseline.TryGetBaseline(commanderNames, out double mean, out _, out _, out _))
        {
            return (int)Math.Round(mean);
        }

        ManabaseBracketBaseline? row = baseline?.TryGetBracketBaseline(resolvedBracket);
        return row is null
            ? FallbackLands
            : (int)Math.Round(row.AvgLands);
    }
}

/// <summary>Resolved floor payload for one Cut Lab role, including default provenance and user override state.</summary>
public sealed record CutLabResolvedFloor
{
    /// <summary>Stable serialized role key for this floor row.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Effective floor after merging the derived default with any user override.</summary>
    public int Floor { get; init; }

    /// <summary>True when the effective floor came from a user-set persisted override.</summary>
    public bool IsUserSet { get; init; }

    /// <summary>Freshly derived effective default before any user override merge; resets restore this max(bracket, commander) value.</summary>
    public int DefaultValue { get; init; }

    /// <summary>Bracket band before commander-aware max() resolution and before the plan delta is applied.</summary>
    public int BracketValue { get; init; }

    /// <summary>Freshly derived commander-specific floor when one matched; otherwise null.</summary>
    public int? CommanderValue { get; init; }

    /// <summary>Bracket that actually drove the resolved default values.</summary>
    public int ResolvedBracket { get; init; }

    /// <summary>True when the bracket was derived or normalized instead of taken directly from a 2-5 declaration.</summary>
    public bool BracketWasFallback { get; init; }

    /// <summary>Strategy-plan raise applied after bracket and commander default resolution.</summary>
    public int PlanDelta { get; init; }
}
