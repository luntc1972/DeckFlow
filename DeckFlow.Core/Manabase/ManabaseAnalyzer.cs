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
    /// <param name="costOverrides">User effective-cost overrides by card name, applied before analysis.</param>
    /// <param name="useManaQuantity">
    /// MQ-02 flag. When true, the castability ROWS credit each source its full mana amount (Sol
    /// Ring = 2, etc.). It is threaded ONLY into the display castability path — the per-color source
    /// REQUIREMENT measurement stays mana-amount-blind, so the Karsten color counts
    /// (EffectiveSources / SimRequiredSources / deficit) are identical whether the flag is on or off.
    /// </param>
    /// <param name="colorAwareMulligan">
    /// MQ-05 flag. When true the castability ROWS mulligan multi-color hands whose opening lands do not
    /// show enough distinct colors (threaded ONLY into the display castability path). The per-color
    /// source REQUIREMENT probe stays count-only, so the Karsten color counts are unchanged.
    /// </param>
    /// <param name="gateRampOnCastable">
    /// P4 gated-ramp flag (tied to land-ramp-sim). When true the castability ROWS only credit a drawn
    /// ramp piece once the board can pay the ramp's OWN colored cost (mirrors 17Lands); when false
    /// (default) ramp deploys as soon as its generic deploy cost is affordable (legacy, byte-identical).
    /// Threaded ONLY into the display castability path — the per-color source requirement probe builds
    /// ramp-free synthetic decks, so it is unaffected.
    /// </param>
    public static ManabaseReport Analyze(
        ManabaseDeck deck,
        ManabaseMode mode,
        CommanderImportance importance = CommanderImportance.Standard,
        IReadOnlyDictionary<string, string>? costOverrides = null,
        bool useManaQuantity = false,
        bool colorAwareMulligan = false,
        bool gateRampOnCastable = false)
    {
        ArgumentNullException.ThrowIfNull(deck);

        // Apply user cost overrides BEFORE anything reads the spell list: substitute each affected
        // spell with an effective requirement (new MV + pips from the override cost). Every
        // downstream consumer — castability rows, the simulator, and the color findings — then
        // reads the substituted spells, so the table and the color verdict stay consistent.
        deck = ApplyCostOverrides(deck, costOverrides);

        // A source occupies a land slot when flagged IsLand (even discounted fetches);
        // partial sources (dorks, rocks, MDFC backs) count toward color supply only.
        int actualLands = deck.Sources.Count(s => s.IsLand);

        double targetLands = ComputeTargetLands(deck, mode, out ManabaseLandTargetBreakdown landTarget);

        // Library size excludes commanders (they start in the command zone, not the deck).
        int librarySize = deck.TotalCards - deck.CommanderCount;

        // Per-spell castability comes FIRST; the color findings then consume these rows so the
        // table and the color verdict never drift apart.
        var castabilityByName = new Dictionary<string, CardCastability>(StringComparer.Ordinal);
        IReadOnlyList<CardCastability> castability = BuildCastability(deck, librarySize, actualLands, castabilityByName, useManaQuantity, colorAwareMulligan, gateRampOnCastable);

        var colorSpellCounts = new Dictionary<ManaColor, int>();
        var demandingByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var findings = BuildColorFindings(deck, librarySize, actualLands, castabilityByName, mode, importance, colorSpellCounts, demandingByName);

        // Demanding cards (below their color's bar) worst-first — surfaced by the two-tier verdict.
        IReadOnlyList<DemandingCard> demandingCards = demandingByName
            .Select(kvp => new DemandingCard { Name = kvp.Key, CastPercent = kvp.Value })
            .OrderBy(d => d.CastPercent)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

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
            DemandingCards = demandingCards,
            // Genuine mana rocks/dorks only: artifacts/creatures that tap for mana (weight 0.5 dork
            // / 0.75 rock). Excludes conditional "granted" creatures (a creature handed a mana
            // ability by Cryptolith Rite / Elven Chorus is not itself a rock or dork) and MDFC
            // land-backs (weight 0.8+, which are lands, not ramp pieces) so the at-a-glance count
            // matches its label instead of over-reporting every non-land source.
            RampSourceCount = deck.Sources.Count(s => !s.IsLand && !s.IsConditional && s.Weight <= 0.75),
            UnsupportedInteractions = deck.UnsupportedInteractions,
            Summary = summary,
        };
    }

    // Substitute each overridden spell with an effective requirement built from the override cost.
    // Keyed by resolved display name (case-insensitive) first, normalized name as a fallback so
    // punctuation / DFC front-face names still match. Deck-level aggregates (land target, average
    // mana value) are intentionally untouched — an alt cost changes castability, not the curve.
    private static ManabaseDeck ApplyCostOverrides(ManabaseDeck deck, IReadOnlyDictionary<string, string>? overrides)
    {
        if (overrides is null || overrides.Count == 0)
        {
            return deck;
        }

        var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byNormalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kvp in overrides)
        {
            exact[kvp.Key] = kvp.Value;
            byNormalized[DeckFlow.Core.Normalization.CardNormalizer.Normalize(kvp.Key)] = kvp.Value;
        }

        var spells = new List<SpellRequirement>(deck.Spells.Count);
        foreach (SpellRequirement spell in deck.Spells)
        {
            string? cost = ResolveOverrideCost(spell.Name, exact, byNormalized);
            spells.Add(cost is null ? spell : ApplyOverride(spell, cost));
        }

        return deck with { Spells = spells };
    }

    private static string? ResolveOverrideCost(
        string name,
        IReadOnlyDictionary<string, string> exact,
        IReadOnlyDictionary<string, string> byNormalized)
    {
        if (exact.TryGetValue(name, out string? hit))
        {
            return hit;
        }

        return byNormalized.TryGetValue(DeckFlow.Core.Normalization.CardNormalizer.Normalize(name), out string? fallback)
            ? fallback
            : null;
    }

    // Build an effective requirement from a (possibly shorthand) cost string. Pips come solely from
    // the parsed cost — no heuristic pip-dropping — so a free "0" clears color while "{R}" keeps it.
    private static SpellRequirement ApplyOverride(SpellRequirement spell, string costString)
    {
        ParsedManaCost cost = ManaCostParser.Parse(ManaCostParser.NormalizeToBraced(costString));
        return spell with
        {
            ManaValue = cost.ManaValue,
            Pips = cost.Pips,
            IsGold = cost.DistinctColors >= 2,
            IsCostOverridden = true,
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
        Dictionary<string, CardCastability> byName,
        bool useManaQuantity,
        bool colorAwareMulligan,
        bool gateRampOnCastable)
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
                deck, librarySize, spell, onCurveTurn, genericReduction,
                useManaQuantity: useManaQuantity, colorAwareMulligan: colorAwareMulligan, gateRampOnCastable: gateRampOnCastable);
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
        Dictionary<ManaColor, int> colorSpellCounts,
        Dictionary<string, int> demandingByName)
    {
        var findings = new List<ColorSourceFinding>();
        var commanderColors = CommanderColors(deck);

        // Cache the mulligan-aware required-source search by (target color + full effective pip
        // signature + on-curve turn + threshold). Ranking every candidate spell on the sim figure
        // (Codex HIGH-2) would otherwise re-run the binary search for identical requirements; most
        // spells share a handful of signatures, so this keeps the extra sims bounded.
        var simRequiredCache = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (ManaColor color in EnumerateUsedColors(deck))
        {
            double allSources = EffectiveSources(deck, color, untappedOnly: false);
            double untappedSources = EffectiveSources(deck, color, untappedOnly: true);

            int required = 0;
            string driver = "(none)";
            double worstDeficit = double.NegativeInfinity;

            int underSupported = 0;
            int colorLimitedUnderSupported = 0;
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

                // Codex HIGH-2: rank the driver on the MULLIGAN-AWARE sim requirement, not the old
                // mulligan-blind hypergeometric. The mono-color sim isolates this color's access;
                // cached per (color, target pips, turn, threshold) so identical requirements reuse it.
                string sig = $"{(int)color}|{pips}|t{onCurveTurn}|th{threshold}";
                if (!simRequiredCache.TryGetValue(sig, out int simNeed))
                {
                    simNeed = SimRequiredSources(
                        librarySize, totalLands, color, pips, onCurveTurn,
                        deck.AverageManaValue, deck.IsSingleton, threshold);
                    simRequiredCache[sig] = simNeed;
                }

                // Codex HIGH-1: a gold/multicolor card needs a source of each OTHER color at the same
                // time, so it wants a little more headroom in THIS color than a mono spell. Add a
                // bounded contention bump (one per other color the spell needs) on top of the isolated
                // figure — modeling the secondary colors inside a ramp-free synthetic deck instead
                // over-penalizes high-MV gold cards (it conflates color access with mana quantity).
                int otherColors = spell.Pips.Count(p => p.Key != color && p.Key != ManaColor.Colorless && p.Value > 0);
                int need = Math.Min(totalLands, simNeed + otherColors);

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

                    // A color-limited shortfall (vs a pure mana/curve limit) is the only kind the mana
                    // base can fix. Tracked separately so the health verdict never reads "needs work"
                    // for an expensive card the base already supports color-wise — that is a curve
                    // problem, not a mana-base one. (UnderSupportedCount keeps counting every late
                    // card for the display "N of M".)
                    if (IsColorLimited(row?.LimitingFactor, color))
                    {
                        colorLimitedUnderSupported++;
                    }

                    // Record the demanding card once (a spell may demand several colors); keep the
                    // lowest cast % seen so the worst-first verdict list is stable.
                    if (!demandingByName.TryGetValue(spell.Name, out int prior) || castPercent < prior)
                    {
                        demandingByName[spell.Name] = castPercent;
                    }
                }
            }

            if (driver == "(none)")
            {
                continue;
            }

            colorSpellCounts[color] = castCount;

            // `required` is already the MULLIGAN-AWARE sim figure for the worst driver (ranked on the
            // sim deficit above, full pip map → gold contention modeled). No mulligan-blind
            // hypergeometric fallback: the sim models Commander's free first mulligan, so the
            // deficit/verdict no longer trip on a phantom double-pip shortfall.
            (double direct, double shared, double conditional) = SourceBreakdown(deck, color);

            findings.Add(new ColorSourceFinding
            {
                Color = color,
                // MEDIUM-4: ActualSources is the color's full weighted source count (all sources).
                // The worst-driver's turn-specific untapped supply lives in the driver/required pair.
                ActualSources = Math.Round(allSources, 1),
                RequiredSources = required,
                DrivingSpell = driver,
                UnderSupportedCount = underSupported,
                ColorLimitedUnderSupportedCount = colorLimitedUnderSupported,
                AverageCastPercent = castCount > 0 ? Math.Round(castSum / castCount, 1) : 0,
                WorstSpellCastPercent = double.IsPositiveInfinity(worstCast) ? 0 : worstCast,
                WorstSpell = worstSpell,
                DirectSources = direct,
                SharedSources = shared,
                ConditionalSources = conditional,
            });
        }

        return OrderFindings(findings, deck, mode, importance, commanderColors);
    }

    // Reduced trial count for the per-color source-requirement search (binary search runs several
    // sims); lower than the headline DefaultTrials to bound cost, with a full-trial boundary confirm.
    private const int SourceSearchTrials = 5_000;

    // Mulligan-aware "how many sources of this color does this spell need": the smallest on-color land
    // count whose sim cast% (Commander free-mull aware) meets the threshold. The probe isolates THIS
    // color (the other colors are abundant) so the search measures the color requirement, not the
    // deck's mana-quantity — a ramp-free synthetic deck that also demanded the secondary color would
    // conflate the two and run to the land ceiling for high-MV gold cards. The caller adds a small
    // gold-contention bump (Codex HIGH-1) on top of this figure. Binary search; the boundary is
    // confirmed at full trials so reduced-trial noise cannot off-by-one the deficit.
    private static int SimRequiredSources(
        int librarySize, int totalLands, ManaColor color, int pips, int onCurveTurn,
        double averageManaValue, bool isSingleton, int threshold)
    {
        if (pips <= 0 || totalLands <= 0)
        {
            return 0;
        }

        int lo = Math.Min(pips, totalLands);
        int hi = totalLands;
        int result = totalLands;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            int pct = SimColorCast(librarySize, totalLands, color, pips, onCurveTurn, averageManaValue, isSingleton, mid, SourceSearchTrials);
            if (pct >= threshold)
            {
                result = mid;
                hi = mid - 1;
            }
            else
            {
                lo = mid + 1;
            }
        }

        // If even an all-on-color base cannot reach the bar, THIS color is not the bottleneck — the
        // spell is mana-/curve-limited (it would need ramp or a lower curve, not more of this color),
        // and that difficulty already shows up in its castability %. Reporting "needs ~totalLands" here
        // would resurrect the phantom deficit this phase set out to kill (e.g. a turn-4 commander on a
        // ramp-free isolation deck), so clamp the requirement to the irreducible minimum (the pips).
        if (result >= totalLands
            && SimColorCast(librarySize, totalLands, color, pips, onCurveTurn, averageManaValue, isSingleton, totalLands, CastabilitySimulator.DefaultTrials) < threshold)
        {
            return pips;
        }

        // Boundary confirm at full trials (reduced-trial noise can mis-place the crossing by one).
        if (result > pips
            && SimColorCast(librarySize, totalLands, color, pips, onCurveTurn, averageManaValue, isSingleton, result - 1, CastabilitySimulator.DefaultTrials) >= threshold)
        {
            result -= 1;
        }
        else if (result < totalLands
            && SimColorCast(librarySize, totalLands, color, pips, onCurveTurn, averageManaValue, isSingleton, result, CastabilitySimulator.DefaultTrials) < threshold)
        {
            result += 1;
        }

        // The mulligan-aware sim may only LOWER the requirement below Karsten's mulligan-blind source
        // count — modeling Commander's free first mulligan can never make a color HARDER than the
        // draw-without-mulligan table. Yet for a double-pip spell in a 99-card deck the Monte-Carlo
        // cast% sits depressed enough that the binary search climbs toward totalLands (a Gruul deck
        // reading "need ~35 of 36 red sources" for an {2}{R}{R} card). Clamp to Karsten's trusted,
        // Snail-validated figure so the sim's only effect is to shave the requirement, never inflate it.
        int karstenCeiling = KarstenManabase.SourcesNeeded(librarySize, totalLands, pips, Math.Max(1, onCurveTurn));
        return Math.Min(result, karstenCeiling);
    }

    // True when THIS color is part of why the card casts late. LimitingFactor (from
    // CastabilitySimulator.DeriveLimitingFactor) is one of: "mana" (pure curve — never color),
    // "both" (mana + color, so every demanded color is stressed), or "color:X" where X is the single
    // most-missing color. For "color:X" we only credit the matching color — otherwise a gold card
    // short on its OTHER color would wrongly mark this one color-starved (Codex review HIGH).
    private static bool IsColorLimited(string? limitingFactor, ManaColor color)
    {
        if (string.IsNullOrEmpty(limitingFactor))
        {
            return false;
        }

        if (limitingFactor.Equals("both", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return limitingFactor.Equals("color:" + color, StringComparison.OrdinalIgnoreCase);
    }

    // Sim cast% for a synthetic `pips`-of-`color` spell at `onCurveTurn` on a base of `onColor`
    // on-color lands plus off-color lands to `totalLands`, padded to `librarySize`. Isolates one
    // color's requirement (other colors fully available, so the search measures color access, not
    // total mana), comparable to Karsten's per-color tables but mulligan-aware.
    private static int SimColorCast(
        int librarySize, int totalLands, ManaColor color, int pips, int onCurveTurn,
        double averageManaValue, bool isSingleton, int onColor, int trials)
    {
        ManaColor off = color == ManaColor.White ? ManaColor.Blue : ManaColor.White;
        var sources = new List<ManaSource>(totalLands);
        for (int i = 0; i < onColor; i++)
        {
            sources.Add(new ManaSource { Name = "OnColor", Produces = new[] { color } });
        }
        for (int i = onColor; i < totalLands; i++)
        {
            sources.Add(new ManaSource { Name = "OffColor", Produces = new[] { off } });
        }

        var probe = new SpellRequirement
        {
            Name = "probe",
            ManaValue = onCurveTurn,
            Pips = new Dictionary<ManaColor, int> { [color] = pips },
        };

        var deck = new ManabaseDeck
        {
            TotalCards = librarySize,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { probe },
            AverageManaValue = averageManaValue,
            IsSingleton = isSingleton,
        };

        return CastabilitySimulator.Simulate(deck, librarySize, probe, onCurveTurn, genericReduction: 0, trials).CastPercent;
    }

    // WeakestColor / ordering = MOST-ACTIONABLE-first: the color whose mana a deck-builder can most
    // usefully shore up leads. We rank color-FIXABLE shortfall ahead of raw tail risk so a single
    // curve-limited bomb (short on mana, not color — e.g. The Skullspore Nexus) never crowns an
    // otherwise over-supported color "weakest". Order: a below-threshold Central commander color,
    // then color-limited under-support breadth (adding a source actually helps), then a raw source
    // deficit, then worst single-spell cast % (tail risk), then mean cast %. This mirrors a
    // marginal-value read (which color's extra source removes the most delay) rather than
    // worst-single-card alone.
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
            .ThenByDescending(f => f.ColorLimitedUnderSupportedCount)
            .ThenByDescending(f => f.Deficit)
            .ThenBy(f => f.WorstSpellCastPercent)
            .ThenBy(f => f.AverageCastPercent)
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

    // Display-only: split a color's total weighted sources into direct (mono-color, the dedicated
    // core), shared (non-conditional multi-color fixers — duals, any-color rocks — real but spread
    // across the deck's colors), and conditional (granted any-color sources the sim only fires
    // ~weight of games). The canonical ActualSources is unchanged; this only explains its makeup so
    // a green-heavy deck's big number reads honestly instead of looking inflated. The three sum to
    // ActualSources within rounding.
    private static (double Direct, double Shared, double Conditional) SourceBreakdown(
        ManabaseDeck deck, ManaColor color)
    {
        double direct = 0.0, shared = 0.0, conditional = 0.0;
        foreach (ManaSource source in deck.Sources)
        {
            if (!source.Produces.Contains(color))
            {
                continue;
            }

            if (source.IsConditional)
            {
                conditional += source.Weight;
                continue;
            }

            // Colorless does not make a source "multi-color"; a Green+Colorless land is still mono.
            int coloredCount = source.Produces.Count(c => c != ManaColor.Colorless);
            if (coloredCount <= 1)
            {
                direct += source.Weight;
            }
            else
            {
                shared += source.Weight;
            }
        }

        // Raw (unrounded) so the parts sum exactly to the unrounded source total; the view rounds
        // each for display. Rounding here would drift (e.g. Math.Round(0.75,1) == 0.8, banker's).
        return (direct, shared, conditional);
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
