using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.Manabase;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Pure bracket- and play-experience-derived default floor rules for the eight Cut Lab roles.</summary>
public static class CutLabFloorDefaults
{
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

    /// <summary>Resolves all eight Cut Lab role floors in stable role order, merging in any user overrides.</summary>
    /// <param name="declaredBracket">User-declared bracket, when present.</param>
    /// <param name="playExperience">User-declared play-experience string.</param>
    /// <param name="commanderManaValue">Resolved commander mana value used for the ramp/draw split.</param>
    /// <param name="commanderNames">Resolved commander names for optional cEDH lands baseline lookup.</param>
    /// <param name="baseline">Optional bundled per-bracket baseline provider.</param>
    /// <param name="cedhBaseline">Optional commander-keyed cEDH lands baseline provider.</param>
    /// <param name="priorFloors">Previously persisted user floor entries from the working session.</param>
    /// <returns>One resolved floor row per Cut Lab role.</returns>
    public static IReadOnlyList<CutLabResolvedFloor> ResolveDefaults(
        int? declaredBracket,
        string playExperience,
        double commanderManaValue,
        IReadOnlyList<string> commanderNames,
        IManabaseBaselineProvider? baseline,
        ICedhLandBaselineProvider? cedhBaseline,
        IReadOnlyList<CutLabRoleFloor> priorFloors)
    {
        ArgumentNullException.ThrowIfNull(playExperience);
        ArgumentNullException.ThrowIfNull(commanderNames);
        ArgumentNullException.ThrowIfNull(priorFloors);

        int resolvedBracket = ResolveBracket(declaredBracket, playExperience, out bool bracketWasFallback);
        int landsDefault = ResolveLandsDefault(resolvedBracket, commanderNames, baseline, cedhBaseline);
        int rampDefault = ManabaseRampDrawBudgetCalculator.CalculateTargetRamp(commanderManaValue);
        // Mirror ManabaseRampDrawBudgetCalculator's fixed 24-slot split: draw gets whatever ramp does not.
        int drawDefault = 24 - rampDefault;

        Dictionary<string, CutLabRoleFloor> userOverrides = GetUserOverrides(priorFloors);
        List<CutLabResolvedFloor> resolved = [];

        foreach (string role in CutLabFloorRules.RoleKeys)
        {
            int defaultValue = role switch
            {
                "lands" => landsDefault,
                "ramp" => rampDefault,
                "draw" => drawDefault,
                _ => GetBracketBand(role, resolvedBracket),
            };

            bool isUserSet = userOverrides.TryGetValue(role, out CutLabRoleFloor? overrideFloor);
            resolved.Add(new CutLabResolvedFloor
            {
                Role = role,
                Floor = isUserSet ? overrideFloor!.Floor : defaultValue,
                IsUserSet = isUserSet,
                DefaultValue = defaultValue,
                ResolvedBracket = resolvedBracket,
                BracketWasFallback = bracketWasFallback,
            });
        }

        return resolved;
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
            // Why: higher brackets expect denser answers, so interaction climbs the fastest.
            "interaction" => normalizedBracket switch { 2 => 6, 3 => 8, 4 => 10, _ => 12 },
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

    /// <summary>Freshly derived default before any user override merge.</summary>
    public int DefaultValue { get; init; }

    /// <summary>Bracket that actually drove the resolved default values.</summary>
    public int ResolvedBracket { get; init; }

    /// <summary>True when the bracket was derived or normalized instead of taken directly from a 2-5 declaration.</summary>
    public bool BracketWasFallback { get; init; }
}
