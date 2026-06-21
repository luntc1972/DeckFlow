using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Implements the §6 mana-base scoring recipe: compare a deck's land count to the
/// Karsten curve target, count effective colored sources, and flag the weakest color.
/// Pure CPU; takes a pre-classified <see cref="ManabaseDeck"/> so it has no Scryfall
/// or HTTP dependency and is fully unit-testable.
/// </summary>
public static class ManabaseAnalyzer
{
    // A spell whose castability falls below this is "under-supported" for COLOR-AGG. Casual uses
    // a mid bar; cEDH and a Central commander hold their colors to a stricter bar.
    private const int CasualSupportThreshold = 80;
    private const int CedhSupportThreshold = 88;

    /// <summary>Run the full analysis in the default Casual / Standard-commander profile.</summary>
    public static ManabaseReport Analyze(ManabaseDeck deck)
        => Analyze(deck, ManabaseMode.Casual);

    /// <summary>Run the full analysis for a given mode (commander importance defaults to Standard).</summary>
    public static ManabaseReport Analyze(ManabaseDeck deck, ManabaseMode mode)
        => Analyze(deck, mode, CommanderImportance.Standard);

    /// <summary>Run the full analysis and produce a <see cref="ManabaseReport"/>.</summary>
    /// <param name="deck">The classified deck.</param>
    /// <param name="mode">Global profile — sets the land-target baseline and default thresholds.</param>
    /// <param name="importance">
    /// How heavily to weight the commander's colors. Orthogonal to <paramref name="mode"/>: it never
    /// changes the land target, only the commander-color support evaluation and summary weighting.
    /// </param>
    public static ManabaseReport Analyze(ManabaseDeck deck, ManabaseMode mode, CommanderImportance importance = CommanderImportance.Standard)
    {
        ArgumentNullException.ThrowIfNull(deck);

        // A source occupies a land slot when flagged IsLand (even discounted fetches);
        // partial sources (dorks, rocks, MDFC backs) count toward color supply only.
        int actualLands = deck.Sources.Count(s => s.IsLand);

        double targetLands = ComputeTargetLands(deck, mode, out ManabaseLandTargetBreakdown landTarget);

        // Library size excludes commanders (they start in the command zone, not the deck).
        int librarySize = deck.TotalCards - deck.CommanderCount;

        // Per-spell castability comes FIRST; the color findings then consume these rows so the
        // table and the color verdict never drift apart.
        var castabilityByName = new Dictionary<string, CardCastability>(StringComparer.Ordinal);
        IReadOnlyList<CardCastability> castability = BuildCastability(deck, librarySize, actualLands, castabilityByName);

        var colorSpellCounts = new Dictionary<ManaColor, int>();
        var findings = BuildColorFindings(deck, librarySize, actualLands, castabilityByName, mode, importance, colorSpellCounts);

        string summary = BuildSummary(actualLands, targetLands, findings, castability, colorSpellCounts, mode, importance);

        return new ManabaseReport
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            ColorFindings = findings,
            Mode = mode,
            Castability = castability,
            ColorSpellCounts = colorSpellCounts,
            CommanderColors = CommanderColors(deck).ToArray(),
            LandTarget = landTarget,
            Summary = summary,
        };
    }

    // FORMULA-01: returns the land target AND the additive term breakdown the "show the work" panel
    // renders. The returned target is byte-for-byte what KarstenManabase produces — the breakdown
    // only surfaces the inputs and the (already-applied) cEDH adjustment; it never recomputes.
    private static double ComputeTargetLands(ManabaseDeck deck, ManabaseMode mode, out ManabaseLandTargetBreakdown breakdown)
    {
        if (!deck.IsSingleton)
        {
            double sixty = KarstenManabase.SixtyCardLandTarget(
                deck.AverageManaValue,
                deck.RampAndDrawUnderThree,
                deck.FastMana,
                deck.MdfcCommon,
                deck.MdfcMythic);
            breakdown = BuildBreakdown(deck, commanders: 0, librarySize: deck.TotalCards, baseTarget: sixty, finalTarget: sixty);
            return sixty;
        }

        int commanderCount = Math.Max(1, deck.CommanderCount);
        int librarySize = deck.TotalCards - commanderCount;

        double singleton = KarstenManabase.SingletonLandTarget(
            deck.TotalCards,
            commanderCount,
            deck.AverageManaValue,
            deck.RampAndDrawUnderThree,
            deck.FastMana,
            deck.MdfcCommon,
            deck.MdfcMythic);

        double finalTarget = mode == ManabaseMode.Cedh
            ? KarstenManabase.CedhLandTarget(
                deck.TotalCards,
                commanderCount,
                deck.AverageManaValue,
                deck.RampAndDrawUnderThree,
                deck.FastMana,
                deck.MdfcCommon,
                deck.MdfcMythic)
            : singleton;

        breakdown = BuildBreakdown(deck, commanderCount, librarySize, baseTarget: singleton, finalTarget: finalTarget);
        return finalTarget;
    }

    private static ManabaseLandTargetBreakdown BuildBreakdown(
        ManabaseDeck deck,
        int commanders,
        int librarySize,
        double baseTarget,
        double finalTarget)
        => new()
        {
            AverageManaValue = deck.AverageManaValue,
            RampAndDrawUnderThree = deck.RampAndDrawUnderThree,
            FastMana = deck.FastMana,
            MdfcCommon = deck.MdfcCommon,
            MdfcMythic = deck.MdfcMythic,
            CommanderCount = commanders,
            LibrarySize = librarySize,
            BaseTarget = baseTarget,
            // The cEDH adjustment is the signed delta after the 28-floor (so the floor is honored),
            // 0 when no adjustment was applied.
            CedhAdjustment = finalTarget - baseTarget,
            FinalTarget = finalTarget,
        };

    // CAST-01/02/04 + COMMANDER-01: build the per-spell castability rows. Rocks/dorks are excluded
    // (but counted in the pools) UNLESS the row is the commander. Commanders pin to the top.
    private static IReadOnlyList<CardCastability> BuildCastability(
        ManabaseDeck deck,
        int librarySize,
        int totalLands,
        Dictionary<string, CardCastability> byName)
    {
        var rows = new List<CardCastability>();

        foreach (SpellRequirement spell in deck.Spells)
        {
            // Never hide a commander row, even if it has a mana ability; otherwise skip sources.
            if (spell.IsManaSource && !spell.IsCommander)
            {
                continue;
            }

            // FINDING-3: cast % comes from a seeded Monte-Carlo simulation that models the JOINT
            // "enough mana incl. colors by turn T" event plus a London mulligan — replacing the old
            // pessimistic P_mana × P_color independence product (it ran ~30 pts under Salubrious
            // Snail because the same lands feed both factors and we skipped the mulligan).
            int genericReduction = GenericReduction(spell, deck.CostReduction);
            int onCurveTurn = EffectiveTurn(spell, deck.CostReduction);

            CardCastability row = CastabilitySimulator.Simulate(
                deck, librarySize, spell, onCurveTurn, genericReduction);
            rows.Add(row);
            byName[spell.Name] = row;
        }

        // Commanders pinned to the top (in declaration order); everything else worst-first.
        return rows
            .OrderByDescending(r => r.IsCommander)
            .ThenBy(r => r.IsCommander ? 0 : r.CastPercent)
            .ToList();
    }

    /// <summary>
    /// REDUCE-01: the spell's effective on-curve turn after applicable always-on reducers. Never
    /// drops below the spell's total colored pips, and caps the total generic reduction at 2.
    /// A reducer only applies when its own mana value is below the spell's (deployable first).
    /// </summary>
    private static int EffectiveTurn(SpellRequirement spell, IReadOnlyList<CostReducer> reducers)
    {
        int totalPips = spell.Pips.Where(p => p.Key != ManaColor.Colorless).Sum(p => Math.Max(0, p.Value));
        int floor = Math.Max(1, totalPips);
        int applicable = GenericReduction(spell, reducers);
        return Math.Max(floor, spell.ManaValue - applicable);
    }

    /// <summary>
    /// REDUCE-01: the total generic mana shaved off this spell by applicable always-on reducers,
    /// capped at 2. A reducer applies only when its scope matches the spell and its own mana value
    /// is below the spell's (it must be deployable first). Shared by <see cref="EffectiveTurn"/> and
    /// the castability simulator so the table and the color verdict use the same reduction.
    /// </summary>
    private static int GenericReduction(SpellRequirement spell, IReadOnlyList<CostReducer> reducers)
    {
        if (reducers.Count == 0)
        {
            return 0;
        }

        int applicable = 0;
        foreach (CostReducer reducer in reducers)
        {
            if (reducer.SourceManaValue >= spell.ManaValue)
            {
                continue;
            }

            if (!ScopeMatches(reducer.Scope, spell.Kinds))
            {
                continue;
            }

            applicable += reducer.GenericReduction;
        }

        return Math.Min(2, applicable);
    }

    private static bool ScopeMatches(ReductionScope scope, SpellKinds kinds) => scope switch
    {
        ReductionScope.All => true,
        ReductionScope.InstantSorcery => (kinds & (SpellKinds.Instant | SpellKinds.Sorcery)) != 0,
        ReductionScope.Creature => (kinds & SpellKinds.Creature) != 0,
        ReductionScope.Artifact => (kinds & SpellKinds.Artifact) != 0,
        _ => false,
    };

    // COLOR-AGG-01 + COMMANDER-01/02: each color's finding keeps the worst single-spell driver
    // (so a lone bomb still shows its real requirement) AND aggregates the whole population
    // (mean cast %, under-supported count). WeakestColor ranks tail-risk first.
    private static IReadOnlyList<ColorSourceFinding> BuildColorFindings(
        ManabaseDeck deck,
        int librarySize,
        int totalLands,
        IReadOnlyDictionary<string, CardCastability> castabilityByName,
        ManabaseMode mode,
        CommanderImportance importance,
        Dictionary<ManaColor, int> colorSpellCounts)
    {
        var findings = new List<ColorSourceFinding>();
        var commanderColors = CommanderColors(deck);

        foreach (ManaColor color in EnumerateUsedColors(deck))
        {
            double allSources = EffectiveSources(deck, color, untappedOnly: false);
            double untappedSources = EffectiveSources(deck, color, untappedOnly: true);

            int required = 0;
            string driver = "(none)";
            double worstDeficit = double.NegativeInfinity;

            int underSupported = 0;
            double castSum = 0;
            int castCount = 0;
            double worstCast = double.PositiveInfinity;
            string worstSpell = "(none)";

            bool colorIsCommander = commanderColors.Contains(color);
            int threshold = ColorThreshold(color, mode, importance, commanderColors);

            foreach (SpellRequirement spell in deck.Spells)
            {
                if (spell.IsManaSource && !spell.IsCommander)
                {
                    continue;
                }

                if (!spell.Pips.TryGetValue(color, out int pips) || pips <= 0)
                {
                    continue;
                }

                // HIGH-2: evaluate availability AND required-sources at the spell's effective
                // on-curve turn (after cost reduction), not its printed mana value, so the color
                // verdict matches the castability table. Fall back to ManaValue when the spell has
                // no castability row (e.g. an excluded mana source).
                int onCurveTurn = castabilityByName.TryGetValue(spell.Name, out CardCastability? curveRow)
                    ? curveRow.OnCurveTurn
                    : spell.ManaValue;

                double available = onCurveTurn <= 1 ? untappedSources : allSources;

                // Gold cards bump each color's requirement by one (need all colors present).
                int goldBump = spell.IsGold ? 1 : 0;
                int need = KarstenManabase.SourcesNeeded(librarySize, totalLands, pips, onCurveTurn) + goldBump;
                double deficit = need - available;

                // COMMANDER: its colors always use the worst-driver value (never averaged away),
                // so a commander pip is a mandatory candidate to set the color's required count.
                bool commanderDriver = spell.IsCommander && importance != CommanderImportance.Low;
                if (deficit > worstDeficit || (commanderDriver && driver == "(none)"))
                {
                    worstDeficit = deficit;
                    required = need;
                    driver = spell.Name;
                }

                int castPercent = castabilityByName.TryGetValue(spell.Name, out CardCastability? row)
                    ? row.CastPercent
                    : 0;
                castSum += castPercent;
                castCount++;
                if (castPercent < worstCast)
                {
                    worstCast = castPercent;
                    worstSpell = spell.Name;
                }

                if (castPercent < threshold)
                {
                    underSupported++;
                }
            }

            if (driver == "(none)")
            {
                continue;
            }

            colorSpellCounts[color] = castCount;

            findings.Add(new ColorSourceFinding
            {
                Color = color,
                // MEDIUM-4: ActualSources is the color's full weighted source count (all sources).
                // The worst-driver's turn-specific untapped supply lives in the driver/required pair.
                ActualSources = Math.Round(allSources, 1),
                RequiredSources = required,
                DrivingSpell = driver,
                UnderSupportedCount = underSupported,
                AverageCastPercent = castCount > 0 ? Math.Round(castSum / castCount, 1) : 0,
                WorstSpellCastPercent = double.IsPositiveInfinity(worstCast) ? 0 : worstCast,
                WorstSpell = worstSpell,
            });
        }

        return OrderFindings(findings, deck, mode, importance, commanderColors);
    }

    // WeakestColor / ordering = tail-risk-first composite (NOT mean alone): any under-supported
    // first, then worst single-spell cast %, then mean cast %, then deficit. A Central commander
    // color below its threshold is promoted ahead of the composite.
    private static IReadOnlyList<ColorSourceFinding> OrderFindings(
        List<ColorSourceFinding> findings,
        ManabaseDeck deck,
        ManabaseMode mode,
        CommanderImportance importance,
        IReadOnlySet<ManaColor> commanderColors)
    {
        bool central = importance == CommanderImportance.Central;
        return findings
            .OrderByDescending(f => central && commanderColors.Contains(f.Color)
                && f.WorstSpellCastPercent < ColorThreshold(f.Color, mode, importance, commanderColors))
            .ThenByDescending(f => f.UnderSupportedCount > 0)
            .ThenBy(f => f.WorstSpellCastPercent)
            .ThenBy(f => f.AverageCastPercent)
            .ThenByDescending(f => f.Deficit)
            .ToList();
    }

    private static int ColorThreshold(
        ManaColor color,
        ManabaseMode mode,
        CommanderImportance importance,
        IReadOnlySet<ManaColor> commanderColors)
    {
        // Base bar comes from the mode; a Central commander tightens ITS colors only (orthogonal
        // to mode — it does not move the land target). A Low commander gets no elevation.
        int baseThreshold = mode == ManabaseMode.Cedh ? CedhSupportThreshold : CasualSupportThreshold;
        if (importance == CommanderImportance.Central && commanderColors.Contains(color))
        {
            return Math.Max(baseThreshold, CedhSupportThreshold);
        }

        return baseThreshold;
    }

    private static IReadOnlySet<ManaColor> CommanderColors(ManabaseDeck deck)
    {
        var colors = new HashSet<ManaColor>();
        foreach (SpellRequirement spell in deck.Spells)
        {
            if (!spell.IsCommander)
            {
                continue;
            }

            foreach (KeyValuePair<ManaColor, int> pip in spell.Pips)
            {
                if (pip.Value > 0 && pip.Key != ManaColor.Colorless)
                {
                    colors.Add(pip.Key);
                }
            }
        }

        return colors;
    }

    private static IEnumerable<ManaColor> EnumerateUsedColors(ManabaseDeck deck)
    {
        var colors = new HashSet<ManaColor>();
        foreach (SpellRequirement spell in deck.Spells)
        {
            foreach (KeyValuePair<ManaColor, int> pip in spell.Pips)
            {
                if (pip.Value > 0 && pip.Key != ManaColor.Colorless)
                {
                    colors.Add(pip.Key);
                }
            }
        }

        return colors;
    }

    // Sum weighted sources of a color. When untappedOnly, exclude tapped lands — a turn-1
    // one-drop can only be cast off mana available the turn the land is played.
    private static double EffectiveSources(ManabaseDeck deck, ManaColor color, bool untappedOnly)
    {
        double total = 0.0;
        foreach (ManaSource source in deck.Sources)
        {
            if (!source.Produces.Contains(color))
            {
                continue;
            }

            if (untappedOnly && !source.EntersUntapped)
            {
                continue;
            }

            total += source.Weight;
        }

        return total;
    }

    private static string BuildSummary(
        int actualLands,
        double targetLands,
        IReadOnlyList<ColorSourceFinding> findings,
        IReadOnlyList<CardCastability> castability,
        IReadOnlyDictionary<ManaColor, int> colorSpellCounts,
        ManabaseMode mode,
        CommanderImportance importance)
    {
        var sb = new StringBuilder();

        sb.Append(CultureInfo.InvariantCulture, $"Mode: {ModeLabel(mode)} — ");
        double delta = actualLands - targetLands;
        sb.Append(CultureInfo.InvariantCulture, $"Lands: {actualLands} vs ~{targetLands:F1} target ");
        if (delta >= -1)
        {
            sb.Append("(land count OK). ");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"(add ~{Math.Ceiling(-delta):F0} land(s)). ");
        }

        // HIGH-1: gate on the tail-risk composite (same signal that orders findings), not raw
        // deficit, so a composite-worst color is not dropped from the verdict copy.
        ColorSourceFinding? weakest = findings.Count > 0 && findings[0].IsCompositeProblem ? findings[0] : null;
        if (weakest is null)
        {
            sb.Append("Colors: every color adequately supported.");
        }
        else
        {
            // A composite-worst color may still meet the raw source bar (deficit <= 0) — only
            // suggest adding sources when there is an actual shortfall.
            int addSources = (int)Math.Ceiling(Math.Max(0, weakest.Deficit));
            string addClause = addSources > 0 ? $" (add ~{addSources})" : string.Empty;
            sb.Append(CultureInfo.InvariantCulture,
                $"Weakest color: {weakest.Color} — {weakest.ActualSources:F1} sources vs {weakest.RequiredSources} needed for {weakest.DrivingSpell}{addClause}. ");
            int total = colorSpellCounts.TryGetValue(weakest.Color, out int count) ? count : Math.Max(weakest.UnderSupportedCount, 1);
            sb.Append(CultureInfo.InvariantCulture,
                $"{weakest.UnderSupportedCount} of {total} {weakest.Color} cards under-supported; worst cast: {weakest.WorstSpell} (~{weakest.WorstSpellCastPercent:F0}%).");
        }

        // Surface the deck's single hardest payoff to cast (commander weighted heaviest at Central).
        CardCastability? hardest = SelectHeadlineSpell(castability, importance);
        if (hardest is not null)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $" Hardest to cast on curve: {hardest.Name} (~{hardest.CastPercent}%).");
        }

        return sb.ToString();
    }

    private static CardCastability? SelectHeadlineSpell(IReadOnlyList<CardCastability> castability, CommanderImportance importance)
    {
        if (castability.Count == 0)
        {
            return null;
        }

        if (importance == CommanderImportance.Central)
        {
            CardCastability? commander = castability.FirstOrDefault(c => c.IsCommander);
            if (commander is not null)
            {
                return commander;
            }
        }

        // Otherwise the worst non-commander payoff (or worst overall if all are commanders).
        return castability
            .OrderBy(c => c.IsCommander)
            .ThenBy(c => c.CastPercent)
            .First();
    }

    private static string ModeLabel(ManabaseMode mode) => mode == ManabaseMode.Cedh ? "cEDH" : "Casual";
}
