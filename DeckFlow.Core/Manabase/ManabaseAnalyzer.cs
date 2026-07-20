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
    internal const int FocusedSupportThreshold = 85;
    private const int CedhSupportThreshold = 88;

    /// <summary>
    /// The companion "to hand" rule tax — accessing a companion from outside the game costs +3
    /// generic mana first (a heuristic).
    /// </summary>
    public const int CompanionToHandTax = 3;

    /// <summary>Run the full analysis in the default Casual / Standard-commander profile.</summary>
    public static ManabaseReport Analyze(ManabaseDeck deck)
        => Analyze(deck, ManabaseMode.Casual);

    /// <summary>
    /// Simulate a companion's castability against the deck's existing library, excluding commanders.
    /// </summary>
    public static CardCastability SimulateCompanion(
        ManabaseDeck deck,
        SpellRequirement companionSpell,
        bool useManaQuantity = false,
        bool colorAwareMulligan = false,
        bool gateRampOnCastable = false,
        bool ritualBurst = false)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(companionSpell);

        int librarySize = deck.TotalCards - deck.CommanderCount;
        int effectiveTurn = companionSpell.ManaValue;
        // HEURISTIC: the caller pre-applies the companion's +3 "to hand" tax to ManaValue.
        // Do not model it via genericReduction; CastabilitySimulator clamps negative reductions to 0.
        const int genericReduction = 0;

        return CastabilitySimulator.Simulate(
            deck,
            librarySize,
            companionSpell,
            effectiveTurn,
            genericReduction,
            useManaQuantity: useManaQuantity,
            colorAwareMulligan: colorAwareMulligan,
            gateRampOnCastable: gateRampOnCastable,
            ritualBurst: ritualBurst);
    }

    /// <summary>
    /// Build the taxed companion <see cref="SpellRequirement"/>: clamp the printed mana value to a
    /// sane 0..20 range (guards adversarial API mana values), then add the +3 "to hand" tax. Pips and
    /// gold-ness come from the printed cost. IsCommander is false (companion is outside the 99).
    /// </summary>
    public static SpellRequirement BuildCompanionSpell(string name, ParsedManaCost printedCost, double printedCmc)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(printedCost);
        int clampedPrinted = Math.Clamp((int)Math.Round(printedCmc), 0, 20);
        return new SpellRequirement
        {
            Name = name,
            // HEURISTIC: companion access costs an extra CompanionToHandTax generic mana to move it to hand first.
            ManaValue = clampedPrinted + CompanionToHandTax,
            Pips = printedCost.Pips,
            TrueColorlessPips = printedCost.TrueColorlessPips,
            SnowPips = printedCost.SnowPips,
            IsGold = printedCost.DistinctColors >= 2,
            IsCommander = false,
        };
    }

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
    /// <param name="ritualBurst">
    /// Ritual burst flag. When true the castability ROWS and plan-presence sim may credit classified
    /// one-shot mana rituals on the tracked cast-attempt turn, but credited ONLY when
    /// <c>mode == ManabaseMode.Cedh</c>; in Casual the burst is suppressed and the path is
    /// byte-identical to flag-off. When false (default) the simulator omits them from the library
    /// and the path is byte-identical. Threaded ONLY into the display castability path — the
    /// per-color source requirement probe stays unchanged.
    /// </param>
    /// <param name="useHealthBandCastability">
    /// MQ-health-band flag. When true, the composite-weakest color's worst-spell cast % feeds the
    /// health-band verdict: a color that is composite-worst AND casts its worst spell below the mode
    /// threshold (80 Casual / 88 cEDH) counts as a color issue, tipping Functional→Workable. Threaded
    /// into <see cref="ManabaseReport.UseHealthBandCastability"/>; <see cref="ManabaseReport.Health"/>
    /// reads it in <c>ComputeColorSignals</c>. When false (default), behavior is byte-identical.
    /// </param>
    /// <param name="useHealthBandHeadlineFloor">
    /// MQ-health-band headline-floor flag. When true, a deck with a strong headline average and no
    /// catastrophic color can promote from NeedsWork to Workable when the only red signal is one soft
    /// color issue plus a land shortfall. Threaded into
    /// <see cref="ManabaseReport.UseHealthBandHeadlineFloor"/>. When false (default), behavior is
    /// byte-identical.
    /// </param>
    /// <param name="cedhContext">
    /// Optional cEDH-only baseline context resolved by the Web layer. Default/disabled preserves the
    /// historic flat-28 cEDH target path byte-for-byte.
    /// </param>
    /// <param name="ritualLandCredit">
    /// When true, cEDH may reduce the strategic land target for net-positive rituals using the
    /// existing classified <see cref="ManabaseDeck.OneShots"/> list. Tactical ritual burst in the
    /// castability sim remains a separate flag and path.
    /// </param>
    /// <param name="scryCredit">
    /// When true, each qualifying cheap scry spell copy contributes a small analyzer-only any-color
    /// source credit to the Karsten per-color source counts. This never creates a
    /// <see cref="ManaSource"/>, never changes the castability sim, and never changes the land target.
    /// </param>
    /// <param name="colorlessSnow">
    /// When true, true colorless <c>{C}</c> and snow <c>{S}</c> costs are treated as separate
    /// source-requirement categories. The analyzer adds dedicated requirement rows and the castability
    /// sim enforces the matching source capabilities. When false (default), output stays byte-identical.
    /// </param>
    /// <param name="interactionLens">
    /// When true, the report may include the cEDH-only early-interaction lens derived from the
    /// existing castability rows. When false (default), or in non-cEDH modes, the output remains
    /// byte-identical with <see cref="ManabaseReport.InteractionLens"/> left null.
    /// </param>
    /// <param name="keepShapes">
    /// When true in cEDH mode, the mulligan read also surfaces the three-shape keep gate from
    /// <see cref="CastabilitySimulator.SimulatePlanPresence"/>. Off by default so existing callers
    /// remain byte-identical until the Web flag is wired.
    /// </param>
    /// <param name="trialsOverride">
    /// Optional override for the existing simulator trial count. When omitted, the analyzer uses
    /// <see cref="CastabilitySimulator.DefaultTrials"/> and remains byte-identical to prior behavior.
    /// </param>
    public static ManabaseReport Analyze(
        ManabaseDeck deck,
        ManabaseMode mode,
        CommanderImportance importance = CommanderImportance.Standard,
        IReadOnlyDictionary<string, string>? costOverrides = null,
        bool useManaQuantity = false,
        bool colorAwareMulligan = false,
        bool gateRampOnCastable = false,
        bool ritualBurst = false,
        bool ritualLandCredit = false,
        bool scryCredit = false,
        bool colorlessSnow = false,
        bool interactionLens = false,
        bool keepShapes = false,
        bool useHealthBandCastability = false,
        bool useHealthBandHeadlineFloor = false,
        CedhLandContext cedhContext = default,
        int? trialsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(deck);

        // Why: HIGH-3 is pure parameterization of the existing simulator cost. Cut Lab's in-loop delta
        // path may pass a reduced trial count for the D-11 latency budget; every existing caller passes
        // null and stays byte-identical to the current DefaultTrials behavior.
        int trials = trialsOverride ?? CastabilitySimulator.DefaultTrials;
        int sourceSearchTrials = trialsOverride is null
            ? SourceSearchTrials
            : Math.Max(500, (int)((long)SourceSearchTrials * trials / CastabilitySimulator.DefaultTrials));

        // Apply user cost overrides BEFORE anything reads the spell list: substitute each affected
        // spell with an effective requirement (new MV + pips from the override cost). Every
        // downstream consumer — castability rows, the simulator, and the color findings — then
        // reads the substituted spells, so the table and the color verdict stay consistent.
        deck = ApplyCostOverrides(deck, costOverrides, out IReadOnlyList<string> unmatchedOverrides);

        // A source occupies a land slot when flagged IsLand (even discounted fetches);
        // partial sources (dorks, rocks, MDFC backs) count toward color supply only.
        int actualLands = deck.Sources.Count(s => s.IsLand);

        bool ritualLandCreditActive = ritualLandCredit && mode == ManabaseMode.Cedh;
        double targetLands = ComputeTargetLands(deck, mode, cedhContext, ritualLandCreditActive, out ManabaseLandTargetBreakdown landTarget);

        // Library size excludes commanders (they start in the command zone, not the deck).
        int librarySize = deck.TotalCards - deck.CommanderCount;

        // Ritual burst is hard-gated to cEDH: rituals (Dark Ritual, etc.) substitute for lands on the
        // explosive early turns that define competitive play; in Casual the credit is suppressed so the
        // flag-on path stays byte-identical there. The simulator itself is mode-agnostic — the policy gate
        // lives here where the mode is known.
        bool ritualBurstActive = ritualBurst && mode == ManabaseMode.Cedh;
        bool interactionLensActive = interactionLens && mode == ManabaseMode.Cedh;

        // Per-spell castability comes FIRST; the color findings then consume these rows so the
        // table and the color verdict never drift apart.
        var castabilityByName = new Dictionary<string, CardCastability>(StringComparer.Ordinal);
        IReadOnlyList<CardCastability> castability = BuildCastability(deck, librarySize, actualLands, castabilityByName, useManaQuantity, colorAwareMulligan, gateRampOnCastable, ritualBurstActive, colorlessSnow, trials);

        var colorSpellCounts = new Dictionary<ManaColor, int>();
        var demandingByName = new Dictionary<string, int>(StringComparer.Ordinal);
        double scrySourceCreditAmount = scryCredit ? KarstenManabase.ScrySourceCreditAmount(deck.ScrySourceCreditCopies) : 0.0;
        int scrySourceCreditCopies = scryCredit ? deck.ScrySourceCreditCopies : 0;
        var findings = BuildColorFindings(
            deck,
            librarySize,
            actualLands,
            castabilityByName,
            mode,
            importance,
            colorSpellCounts,
            demandingByName,
            scrySourceCreditAmount,
            colorlessSnow,
            sourceSearchTrials,
            trials);

        // Demanding cards (below their color's bar) worst-first — surfaced by the two-tier verdict.
        IReadOnlyList<DemandingCard> demandingCards = demandingByName
            .Select(kvp => new DemandingCard { Name = kvp.Key, CastPercent = kvp.Value })
            .OrderBy(d => d.CastPercent)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        double? baselineRangeLow = null;
        double? baselineRangeHigh = null;
        int? baselineDeckCount = null;
        double? baselineLandsMean = null;
        double? baselineLandsSd = null;
        string? baselineMonth = null;
        if (cedhContext.Enabled
            && cedhContext.BaselineN >= 10
            && cedhContext.BaselineMean is { } baselineMean
            && cedhContext.BaselineSd is { } baselineSd)
        {
            baselineDeckCount = cedhContext.BaselineN;
            baselineLandsMean = baselineMean;
            baselineLandsSd = baselineSd;
            baselineMonth = cedhContext.BaselineMonth;
            baselineRangeLow = baselineMean - baselineSd;
            baselineRangeHigh = baselineMean + baselineSd;
        }

        string summary = BuildSummary(actualLands, targetLands, findings, castability, colorSpellCounts, mode, importance);

        // Plan-presence: a dedicated single deck-level pass, run ONLY when the deck carries plan-tagged
        // spells (the Web layer tags them only when the plan-presence flag is on). No tags → null, so
        // the flag-off path adds no sim and stays byte-identical.
        ManabasePlanPresence? planPresence = deck.Spells.Any(s => s.PlanRoles != PlanRole.None)
            ? CastabilitySimulator.SimulatePlanPresence(
                deck, librarySize, trials, useManaQuantity, colorAwareMulligan, gateRampOnCastable, ritualBurstActive, colorlessSnow, mode, keepShapes)
            : null;

        return new ManabaseReport
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            TargetLandsRangeLow = baselineRangeLow,
            TargetLandsRangeHigh = baselineRangeHigh,
            BaselineDeckCount = baselineDeckCount,
            BaselineLandsMean = baselineLandsMean,
            BaselineLandsSd = baselineLandsSd,
            BaselineMonth = baselineMonth,
            ColorFindings = findings,
            UnmatchedOverrideNames = unmatchedOverrides,
            Mode = mode,
            UseHealthBandCastability = useHealthBandCastability,
            UseHealthBandHeadlineFloor = useHealthBandHeadlineFloor,
            Castability = castability,
            ColorSpellCounts = colorSpellCounts,
            CommanderColors = CommanderColors(deck).ToArray(),
            LandTarget = landTarget,
            // TAP-01/TAP-02: tap-quality metrics derived from the same castability rows + color
            // findings (no second sim). Always computed in Core; the Web layer flag-gates display.
            TapAnalysis = ComputeTapAnalysis(deck, findings, castability, trials, scrySourceCreditAmount),
            // MULLIGAN-01..05: opening-hand / mulligan evaluation derived from the same castability
            // rows (no second sim). Always computed in Core; the Web layer flag-gates display.
            MulliganEvaluation = ComputeMulliganEvaluation(
                deck,
                castability,
                trials,
                librarySize,
                mode,
                importance,
                keepShapes,
                useManaQuantity,
                colorAwareMulligan,
                gateRampOnCastable,
                ritualBurstActive,
                colorlessSnow,
                planPresence),
            InteractionLens = interactionLensActive
                ? ComputeInteractionLens(deck, castability, trials, CedhSupportThreshold)
                : null,
            DemandingCards = demandingCards,
            // Genuine mana rocks/dorks only: artifacts/creatures that tap for mana (weight 0.5 dork
            // / 0.75 rock). Excludes conditional "granted" creatures (a creature handed a mana
            // ability by Cryptolith Rite / Elven Chorus is not itself a rock or dork) and MDFC
            // land-backs (real lands, so !IsLand already drops them — not ramp pieces) so the
            // at-a-glance count matches its label instead of over-reporting every non-land source.
            RampSourceCount = deck.Sources.Count(s => !s.IsLand && !s.IsConditional && s.Weight <= 0.75),
            // Project the SAME rock/dork predicate to names so the disclosure lists exactly what the
            // count credited; de-dup by name preserving first-seen (deck) order.
            RampSourceNames = deck.Sources.Where(s => !s.IsLand && !s.IsConditional && s.Weight <= 0.75).Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RampAndDrawNames = deck.RampAndDrawNames,
            ManaSourceListings = deck.Sources.Select(source => new ManaSourceListing
            {
                Name = source.Name,
                Colors = source.Produces.ToArray(),
                IsLand = source.IsLand,
                EntersUntapped = source.EntersUntapped,
                ProducesColorless = source.ProducesColorless,
            }).ToList(),
            ScrySourceCreditCopies = scrySourceCreditCopies,
            RestrictedSourceLandNames = deck.RestrictedSourceLandNames,
            UnsupportedInteractions = AppendRestrictedLandUnsupportedInteraction(deck),
            Summary = summary,
        };
    }

    private static ManabaseInteractionLens ComputeInteractionLens(
        ManabaseDeck deck,
        IReadOnlyList<CardCastability> castability,
        int defaultTrials,
        int threshold)
    {
        var spellsByName = new Dictionary<string, SpellRequirement>(StringComparer.OrdinalIgnoreCase);
        foreach (SpellRequirement spell in deck.Spells)
        {
            spellsByName[spell.Name] = spell;
        }

        List<ManabaseInteractionRow> rows = castability
            .Where(row => spellsByName.TryGetValue(row.Name, out SpellRequirement? spell)
                // The OR preserves cheap instant/sorcery interaction whose PlanRole.Interaction was
                // intentionally stripped by the Web-layer permanent gate for plan-presence semantics.
                && (spell.PlanRoles.HasFlag(PlanRole.Interaction) || spell.IsInteractionSpell)
                && spell.ManaValue <= 2)
            .Select(row => new ManabaseInteractionRow
            {
                Name = row.Name,
                HoldablePercent = defaultTrials > 0
                    ? (int)Math.Round(100.0 * row.ByTurn3HoldableTrials / defaultTrials)
                    : 0,
                IsCostOverridden = row.IsCostOverridden,
            })
            .OrderBy(row => row.HoldablePercent)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();

        return new ManabaseInteractionLens
        {
            QualifyingCount = rows.Count,
            OnTargetCount = rows.Count(row => row.HoldablePercent >= threshold),
            Threshold = threshold,
            Rows = rows,
        };
    }

    private static IReadOnlyList<UnsupportedInteraction> AppendRestrictedLandUnsupportedInteraction(ManabaseDeck deck)
    {
        if (deck.RestrictedSourceLandNames.Count == 0)
        {
            return deck.UnsupportedInteractions;
        }

        string landList = string.Join(", ", deck.RestrictedSourceLandNames);
        return deck.UnsupportedInteractions
            .Concat(new[]
            {
                new UnsupportedInteraction
                {
                    Name = "Restricted land approximation",
                    Reason = $"Approximated restricted colored sources for: {landList}.",
                },
            })
            .ToList();
    }

    // Substitute each overridden spell with an effective requirement built from the override cost.
    // Keyed by resolved display name (case-insensitive) first, normalized name as a fallback so
    // punctuation / DFC front-face names still match. Deck-level aggregates (land target, average
    // mana value) are intentionally untouched — an alt cost changes castability, not the curve.
    private static ManabaseDeck ApplyCostOverrides(
        ManabaseDeck deck,
        IReadOnlyDictionary<string, string>? overrides,
        out IReadOnlyList<string> unmatchedOverrides)
    {
        if (overrides is null || overrides.Count == 0)
        {
            unmatchedOverrides = Array.Empty<string>();
            return deck;
        }

        var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byNormalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> kvp in overrides)
        {
            exact[kvp.Key] = kvp.Value;
            byNormalized[DeckFlow.Core.Normalization.CardNormalizer.Normalize(kvp.Key)] = kvp.Value;
        }

        // Collect the spell-name keys as we walk so the "which override bound no spell" report is
        // derived from the SAME single pass — no second walk of the deck, and no drift-prone second
        // copy of the exact-then-normalized match rule.
        var spellExact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var spellNormalized = new HashSet<string>(StringComparer.Ordinal);

        var spells = new List<SpellRequirement>(deck.Spells.Count);
        foreach (SpellRequirement spell in deck.Spells)
        {
            spellExact.Add(spell.Name);
            spellNormalized.Add(DeckFlow.Core.Normalization.CardNormalizer.Normalize(spell.Name));

            string? cost = ResolveOverrideCost(spell.Name, exact, byNormalized);
            spells.Add(cost is null ? spell : ApplyOverride(spell, cost));
        }

        var unmatched = new List<string>();
        foreach (string key in overrides.Keys)
        {
            if (!spellExact.Contains(key)
                && !spellNormalized.Contains(DeckFlow.Core.Normalization.CardNormalizer.Normalize(key)))
            {
                unmatched.Add(key);
            }
        }

        unmatchedOverrides = unmatched;
        return deck with { Spells = spells };
    }

    // Exact (case-insensitive) then normalized match. ApplyCostOverrides reuses this same rule to
    // decide which override keys bound no spell (its unmatched-overrides out-param), so a name it
    // reports as "not applied" is exactly one this method would never resolve.
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
            TrueColorlessPips = cost.TrueColorlessPips,
            SnowPips = cost.SnowPips,
            IsGold = cost.DistinctColors >= 2,
            IsCostOverridden = true,
        };
    }

    // FORMULA-01: returns the land target AND the additive term breakdown the "show the work" panel
    // renders. The returned target is byte-for-byte what KarstenManabase produces — the breakdown
    // only surfaces the inputs and the (already-applied) cEDH adjustment; it never recomputes.
    private static double ComputeTargetLands(
        ManabaseDeck deck,
        ManabaseMode mode,
        CedhLandContext cedhContext,
        bool ritualLandCredit,
        out ManabaseLandTargetBreakdown breakdown)
    {
        if (!deck.IsSingleton)
        {
            double sixty = KarstenManabase.SixtyCardLandTarget(
                deck.AverageManaValue,
                deck.RampAndDrawUnderThree,
                deck.FastMana);
            breakdown = BuildBreakdown(
                deck,
                mode,
                cedhContext,
                commanders: 0,
                librarySize: deck.TotalCards,
                baseTarget: sixty,
                finalTarget: sixty);
            return sixty;
        }

        int commanderCount = Math.Max(1, deck.CommanderCount);
        int librarySize = deck.TotalCards - commanderCount;
        double ritualLandCreditAmount = 0.0;

        double singleton = KarstenManabase.SingletonLandTarget(
            deck.TotalCards,
            commanderCount,
            deck.AverageManaValue,
            deck.RampAndDrawUnderThree,
            deck.FastMana);

        // The out value is the credit the target math actually subtracted, so the breakdown
        // can never drift from CedhLandTarget's internal gating.
        double finalTarget = mode == ManabaseMode.Cedh
            ? KarstenManabase.CedhLandTarget(
                deck.TotalCards,
                commanderCount,
                deck.AverageManaValue,
                deck.RampAndDrawUnderThree,
                deck.FastMana,
                cedhContext,
                deck.OneShots.Count,
                ritualLandCredit,
                out ritualLandCreditAmount)
            : singleton;

        int netPositiveRitualCount = ritualLandCreditAmount > 0 ? deck.OneShots.Count : 0;

        breakdown = BuildBreakdown(
            deck,
            mode,
            cedhContext,
            commanderCount,
            librarySize,
            baseTarget: singleton,
            finalTarget: finalTarget,
            ritualLandCredit: ritualLandCreditAmount,
            netPositiveRitualCount: netPositiveRitualCount);
        return finalTarget;
    }

    private static ManabaseLandTargetBreakdown BuildBreakdown(
        ManabaseDeck deck,
        ManabaseMode mode,
        CedhLandContext cedhContext,
        int commanders,
        int librarySize,
        double baseTarget,
        double finalTarget,
        double ritualLandCredit = 0.0,
        int netPositiveRitualCount = 0)
    {
        double baselineMean = cedhContext.BaselineMean.GetValueOrDefault();
        bool cedhBaselineBlended = mode == ManabaseMode.Cedh
            && cedhContext.Enabled
            && cedhContext.BaselineN >= 10
            && cedhContext.BaselineMean.HasValue
            && cedhContext.BaselineSd.HasValue
            && double.IsFinite(baselineMean)
            && baselineMean is >= 10.0 and <= 60.0;
        double cedhSafetyFloor = mode == ManabaseMode.Cedh
            ? (cedhContext.Enabled ? KarstenManabase.CedhSafetyFloor : KarstenManabase.CedhDisabledFloor)
            : 0.0;

        return new ManabaseLandTargetBreakdown
        {
            AverageManaValue = deck.AverageManaValue,
            RampAndDrawUnderThree = deck.RampAndDrawUnderThree,
            FastMana = deck.FastMana,
            CommanderCount = commanders,
            LibrarySize = librarySize,
            BaseTarget = baseTarget,
            // The cEDH adjustment is the signed delta after the applied floor/clamp.
            CedhAdjustment = finalTarget - baseTarget,
            CedhSafetyFloor = cedhSafetyFloor,
            CedhBaselineBlended = cedhBaselineBlended,
            RitualLandCredit = ritualLandCredit,
            NetPositiveRitualCount = netPositiveRitualCount,
            FinalTarget = finalTarget,
        };
    }

    // CAST-01/02/04 + COMMANDER-01: build the per-spell castability rows. Rocks/dorks are excluded
    // (but counted in the pools) UNLESS the row is the commander. Commanders pin to the top.
    private static IReadOnlyList<CardCastability> BuildCastability(
        ManabaseDeck deck,
        int librarySize,
        int totalLands,
        Dictionary<string, CardCastability> byName,
        bool useManaQuantity,
        bool colorAwareMulligan,
        bool gateRampOnCastable,
        bool ritualBurst,
        bool colorlessSnow,
        int trials = CastabilitySimulator.DefaultTrials)
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
            int onCurveTurn = EffectiveTurn(spell, deck.CostReduction, colorlessSnow);

            CardCastability row = CastabilitySimulator.Simulate(
                deck, librarySize, spell, onCurveTurn, genericReduction,
                useManaQuantity: useManaQuantity, colorAwareMulligan: colorAwareMulligan, gateRampOnCastable: gateRampOnCastable, ritualBurst: ritualBurst, colorlessSnow: colorlessSnow, trials: trials);
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
    private static int EffectiveTurn(SpellRequirement spell, IReadOnlyList<CostReducer> reducers, bool colorlessSnow)
    {
        int totalPips = spell.Pips.Where(p => p.Key != ManaColor.Colorless).Sum(p => p.Value);
        if (colorlessSnow)
        {
            totalPips += spell.TrueColorlessPips + spell.SnowPips;
        }

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
        Dictionary<string, int> demandingByName,
        double scrySourceCredit,
        bool colorlessSnow,
        int sourceSearchTrials,
        int sourceSearchBoundaryTrials)
    {
        var findings = new List<ColorSourceFinding>();
        var commanderColors = CommanderColors(deck);

        // Spells whose only shortfall is a cheap turn-1 color miss in a color the base already supplies
        // (a "structural cheap miss"), tracked across every color's pass and pruned from the demanding
        // list AFTER the color loop. `sourceFixableNames` records spells that ARE genuinely source-short
        // in at least one demanded color; a spell is pruned only when it is structural AND appears in no
        // source-fixable color — so a card limited by "both" colors that is short in one but supplied in
        // the other (structural in the supplied pass, fixable in the short pass) correctly survives.
        var structuralCheapNames = new HashSet<string>(StringComparer.Ordinal);
        var sourceFixableNames = new HashSet<string>(StringComparer.Ordinal);

        // Cache the mulligan-aware required-source search by (target color + full effective pip
        // signature + on-curve turn + threshold). Ranking every candidate spell on the sim figure
        // (Codex HIGH-2) would otherwise re-run the binary search for identical requirements; most
        // spells share a handful of signatures, so this keeps the extra sims bounded.
        var simRequiredCache = new Dictionary<string, int>(StringComparer.Ordinal);
        var effectiveTurnBySpellName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (SpellRequirement spell in deck.Spells)
        {
            effectiveTurnBySpellName.TryAdd(
                spell.Name,
                EffectiveTurn(spell, deck.CostReduction, colorlessSnow));
        }

        foreach (ManaColor color in EnumerateUsedColors(deck))
        {
            double allSources = EffectiveSources(deck, color, untappedOnly: false, scrySourceCredit);
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
                // Mirrored logic — keep in sync with AddSpecialCategoryFinding; full unification deferred.
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
                // verdict matches the castability table. When a mana source row is excluded from the
                // castability table, recompute the same effective turn here instead of falling back.
                int onCurveTurn = castabilityByName.TryGetValue(spell.Name, out CardCastability? curveRow)
                    ? curveRow.OnCurveTurn
                    : effectiveTurnBySpellName[spell.Name];

                double available = onCurveTurn <= 1 ? untappedSources : allSources;

                // Codex HIGH-2: rank the driver on the MULLIGAN-AWARE sim requirement, not the old
                // mulligan-blind hypergeometric. The mono-color sim isolates this color's access;
                // cached per (color, target pips, turn, threshold) so identical requirements reuse it.
                string sig = $"{(int)color}|{pips}|t{onCurveTurn}|th{threshold}";
                if (!simRequiredCache.TryGetValue(sig, out int simNeed))
                {
                    simNeed = SimRequiredSources(
                        librarySize, totalLands, color, pips, onCurveTurn,
                        deck.AverageManaValue, deck.IsSingleton, threshold, sourceSearchTrials, sourceSearchBoundaryTrials);
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
                    // Honest display count: every card below the bar, mana- OR color-limited. (Drives
                    // the "N of M" readout and the everyColorClear gate.)
                    underSupported++;

                    bool colorLimited = IsColorLimited(row?.LimitingFactor, color);

                    // Whether MORE of the RIGHT sources would actually help this spell. `deficit` here is
                    // this spell's TURN-AWARE figure: need vs the sources available AT its on-curve turn
                    // (untapped-only for a turn-1 cast, all sources later — see `available` above). So a
                    // color that is comfortably supplied for this spell's turn shows deficit <= 0 and its
                    // cheap turn-1 misses are structural (a single land drop, no source count moves them);
                    // whereas a color whose sources mostly enter TAPPED can be untapped-short on turn 1
                    // (deficit > 0) even at a healthy total count — a real, base-fixable "add untapped
                    // sources" problem the gate keeps.
                    bool sourceFixable = colorLimited && deficit > 0;

                    // A color-limited shortfall is the only kind the mana base can fix — but ONLY when the
                    // color is genuinely short for the spell's turn. This is what the health verdict and the
                    // "add N lands" advice key off, so a color that supplies its spells on time never reads
                    // "starved"/"needs work" just because a cheap spell misses its turn-1 window (the field
                    // report: a color the source table shows over-supplied). Mana-limited curve bombs (not
                    // colorLimited) still do not count here — that is a curve problem, not a mana-base one.
                    if (sourceFixable)
                    {
                        colorLimitedUnderSupported++;
                        sourceFixableNames.Add(spell.Name);
                    }

                    // Record the demanding card once, keeping the lowest cast % seen across the colors it
                    // needs (worst-first list stability).
                    if (!demandingByName.TryGetValue(spell.Name, out int prior) || castPercent < prior)
                    {
                        demandingByName[spell.Name] = castPercent;
                    }

                    // "Demanding" = hardest to cast, meant to expose WEAK SUPPORT. A cheap spell that is
                    // color-limited only by its turn-1 window while THIS (its limiting) color is fully
                    // supplied is structural variance, not weak support — mark it for removal after the
                    // loop (the user's "these are all one-mana cards" report). Because colorLimited is true
                    // only in the spell's limiting color, this is decided exactly once per spell; a
                    // mana-limited bomb or a genuinely source-short card is never marked and so survives.
                    if (colorLimited && deficit <= 0)
                    {
                        structuralCheapNames.Add(spell.Name);
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
                EvaluatedCardCount = castCount,
                // TAP-01: the RAW (un-rounded) untapped weight for this color. ActualSources above is
                // rounded for display; tap math must divide by the raw total, so keep this un-rounded.
                UntappedSources = untappedSources,
            });
        }

        if (colorlessSnow)
        {
            foreach (SourceRequirementCategory category in new[]
                     {
                         SourceRequirementCategory.Colorless,
                         SourceRequirementCategory.Snow,
                     })
            {
                AddSpecialCategoryFinding(
                    findings,
                    deck,
                    librarySize,
                    totalLands,
                    castabilityByName,
                    mode,
                    demandingByName,
                    simRequiredCache,
                    structuralCheapNames,
                    sourceFixableNames,
                    effectiveTurnBySpellName,
                    category,
                    sourceSearchTrials,
                    sourceSearchBoundaryTrials);
            }
        }

        // Drop structural cheap misses from the demanding list now that every color has been scored — but
        // only when the card is source-fixable in NO demanded color. A card short in one color yet
        // supplied in another (e.g. limiting factor "both") stays, since more sources would still help it.
        // Mana-limited cards are never marked structural, so they remain too.
        foreach (string name in structuralCheapNames)
        {
            if (!sourceFixableNames.Contains(name))
            {
                demandingByName.Remove(name);
            }
        }

        return OrderFindings(findings, deck, mode, importance, commanderColors);
    }
    private static void AddSpecialCategoryFinding(
        List<ColorSourceFinding> findings,
        ManabaseDeck deck,
        int librarySize,
        int totalLands,
        IReadOnlyDictionary<string, CardCastability> castabilityByName,
        ManabaseMode mode,
        Dictionary<string, int> demandingByName,
        Dictionary<string, int> simRequiredCache,
        HashSet<string> structuralCheapNames,
        HashSet<string> sourceFixableNames,
        IReadOnlyDictionary<string, int> effectiveTurnBySpellName,
        SourceRequirementCategory category,
        int sourceSearchTrials,
        int sourceSearchBoundaryTrials)
    {
        double allSources = EffectiveSources(deck, SourceQualifier(category), untappedOnly: false);
        double untappedSources = EffectiveSources(deck, SourceQualifier(category), untappedOnly: true);

        int required = 0;
        string driver = "(none)";
        double worstDeficit = double.NegativeInfinity;
        int underSupported = 0;
        int colorLimitedUnderSupported = 0;
        double castSum = 0;
        int castCount = 0;
        double worstCast = double.PositiveInfinity;
        string worstSpell = "(none)";
        int threshold = mode switch
        {
            ManabaseMode.Cedh => CedhSupportThreshold,
            ManabaseMode.Focused => FocusedSupportThreshold,
            _ => CasualSupportThreshold,
        };

        foreach (SpellRequirement spell in deck.Spells)
        {
            // Mirrored logic — keep in sync with BuildColorFindings' per-spell loop; full unification deferred.
            int categoryPips = SpecialCategoryPips(spell, category);
            if (categoryPips <= 0)
            {
                continue;
            }

            bool hasRow = castabilityByName.TryGetValue(spell.Name, out CardCastability? row);
            int onCurveTurn = hasRow ? row!.OnCurveTurn : effectiveTurnBySpellName[spell.Name];
            double available = onCurveTurn <= 1 ? untappedSources : allSources;

            string sig = $"special:{category}|{categoryPips}|t{onCurveTurn}|th{threshold}";
            if (!simRequiredCache.TryGetValue(sig, out int simNeed))
            {
                simNeed = SimRequiredSpecialSources(
                    librarySize, totalLands, category, categoryPips, onCurveTurn,
                    deck.AverageManaValue, deck.IsSingleton, threshold, sourceSearchTrials, sourceSearchBoundaryTrials);
                simRequiredCache[sig] = simNeed;
            }

            double deficit = simNeed - available;
            if (deficit > worstDeficit)
            {
                worstDeficit = deficit;
                required = simNeed;
                driver = spell.Name;
            }

            int castPercent = hasRow ? row!.CastPercent : 0;
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
                bool colorLimited = IsSpecialCategoryLimited(row?.LimitingFactor, category);
                bool sourceFixable = colorLimited && deficit > 0;
                if (sourceFixable)
                {
                    colorLimitedUnderSupported++;
                    sourceFixableNames.Add(spell.Name);
                }

                if (!demandingByName.TryGetValue(spell.Name, out int prior) || castPercent < prior)
                {
                    demandingByName[spell.Name] = castPercent;
                }

                if (colorLimited && deficit <= 0)
                {
                    structuralCheapNames.Add(spell.Name);
                }
            }
        }

        if (driver == "(none)")
        {
            return;
        }

        findings.Add(new ColorSourceFinding
        {
            Color = ManaColor.Colorless,
            DisplayColor = category.DisplayLabel(),
            ActualSources = Math.Round(allSources, 1),
            RequiredSources = required,
            DrivingSpell = driver,
            UnderSupportedCount = underSupported,
            ColorLimitedUnderSupportedCount = colorLimitedUnderSupported,
            AverageCastPercent = castCount > 0 ? Math.Round(castSum / castCount, 1) : 0,
            WorstSpellCastPercent = double.IsPositiveInfinity(worstCast) ? 0 : worstCast,
            WorstSpell = worstSpell,
            DirectSources = Math.Round(allSources, 1),
            SharedSources = 0.0,
            ConditionalSources = 0.0,
            EvaluatedCardCount = castCount,
            UntappedSources = untappedSources,
        });
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
        double averageManaValue, bool isSingleton, int threshold, int sourceSearchTrials, int sourceSearchBoundaryTrials)
        => SimRequiredSourcesCore(
            librarySize,
            totalLands,
            pips,
            onCurveTurn,
            isSingleton,
            threshold,
            sourceSearchTrials,
            sourceSearchBoundaryTrials,
            (sources, trials) => SimColorCast(
                librarySize, totalLands, color, pips, onCurveTurn, averageManaValue, isSingleton, sources, trials));

    private static int SimRequiredSpecialSources(
        int librarySize, int totalLands, SourceRequirementCategory category, int pips, int onCurveTurn,
        double averageManaValue, bool isSingleton, int threshold, int sourceSearchTrials, int sourceSearchBoundaryTrials)
    {
        if (pips <= 0 || totalLands <= 0)
        {
            return 0;
        }

        return SimRequiredSourcesCore(
            librarySize,
            totalLands,
            pips,
            onCurveTurn,
            isSingleton,
            threshold,
            sourceSearchTrials,
            sourceSearchBoundaryTrials,
            (sources, trials) => SimSpecialCategoryCast(
                librarySize, totalLands, category, pips, onCurveTurn, averageManaValue, isSingleton, sources, trials));
    }

    private static int SimRequiredSourcesCore(
        int librarySize,
        int totalLands,
        int pips,
        int onCurveTurn,
        bool isSingleton,
        int threshold,
        int sourceSearchTrials,
        int sourceSearchBoundaryTrials,
        Func<int, int, int> simCast)
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
            int pct = simCast(mid, sourceSearchTrials);
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
        if (result >= totalLands && simCast(totalLands, sourceSearchBoundaryTrials) < threshold)
        {
            return pips;
        }

        // Boundary confirm at full trials (reduced-trial noise can mis-place the crossing by one).
        if (result > pips && simCast(result - 1, sourceSearchBoundaryTrials) >= threshold)
        {
            result -= 1;
        }
        else if (result < totalLands && simCast(result, sourceSearchBoundaryTrials) < threshold)
        {
            result += 1;
        }

        // The mulligan-aware sim may only LOWER the requirement below Karsten's mulligan-blind source
        // count — modeling Commander's free first mulligan can never make a color HARDER than the
        // draw-without-mulligan table. Yet for a double-pip spell in a 99-card deck the Monte-Carlo
        // cast% sits depressed enough that the binary search climbs toward totalLands (a Gruul deck
        // reading "need ~35 of 36 red sources" for an {2}{R}{R} card). Clamp to Karsten's trusted,
        // Snail-validated figure so the sim's only effect is to shave the requirement, never inflate it.
        int karstenCeiling = KarstenManabase.SourcesNeeded(
            librarySize,
            totalLands,
            pips,
            Math.Max(1, onCurveTurn),
            onPlay: !isSingleton);
        return Math.Min(result, karstenCeiling);
    }

    // True when THIS color is part of why the card casts late. LimitingFactor (from
    // CastabilitySimulator.DeriveLimitingFactor) is one of: "mana" (pure curve — never color),
    // "both" (mana + color, so every demanded color is stressed), or "color:X" where X is the single
    // most-missing color. For "color:X" we only credit the matching color — otherwise a gold card
    // short on its OTHER color would wrongly mark this one color-starved (Codex review HIGH).
    private static bool IsColorLimited(string? limitingFactor, ManaColor color) =>
        IsColorLimited(limitingFactor, "color:" + color);

    private static bool IsColorLimited(string? limitingFactor, string expectedLabel)
    {
        if (string.IsNullOrEmpty(limitingFactor))
        {
            return false;
        }

        if (limitingFactor.Equals("both", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return limitingFactor.Equals(expectedLabel, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpecialCategoryLimited(string? limitingFactor, SourceRequirementCategory category)
        => IsColorLimited(limitingFactor, category.LimitingFactorToken());

    // Sim cast% for a synthetic `pips`-of-`color` spell at `onCurveTurn` on a base of `onColor`
    // on-color lands plus off-color lands to `totalLands`, padded to `librarySize`. Isolates one
    // color's requirement (other colors fully available, so the search measures color access, not
    // total mana), comparable to Karsten's per-color tables but mulligan-aware.
    private static int SimColorCast(
        int librarySize, int totalLands, ManaColor color, int pips, int onCurveTurn,
        double averageManaValue, bool isSingleton, int onColor, int trials)
    {
        ManaColor off = color == ManaColor.White ? ManaColor.Blue : ManaColor.White;
        ManaSource onColorTemplate = new() { Name = "OnColor", Produces = new[] { color } };
        ManaSource offColorTemplate = new() { Name = "OffColor", Produces = new[] { off } };
        SpellRequirement probe = new()
        {
            Name = "probe",
            ManaValue = onCurveTurn,
            Pips = new Dictionary<ManaColor, int> { [color] = pips },
        };

        return SimSyntheticCast(
            librarySize,
            totalLands,
            averageManaValue,
            isSingleton,
            onColor,
            trials,
            onCurveTurn,
            onColorTemplate,
            offColorTemplate,
            probe);
    }

    private static int SimSpecialCategoryCast(
        int librarySize, int totalLands, SourceRequirementCategory category, int pips, int onCurveTurn,
        double averageManaValue, bool isSingleton, int qualifyingSources, int trials)
    {
        ManaSource qualifyingTemplate = category switch
        {
            SourceRequirementCategory.Colorless => new ManaSource
            {
                Name = "Colorless",
                Produces = Array.Empty<ManaColor>(),
                ProducesColorless = true,
            },
            SourceRequirementCategory.Snow => new ManaSource
            {
                Name = "Snow",
                Produces = Array.Empty<ManaColor>(),
                IsSnow = true,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
        ManaSource otherTemplate = new() { Name = "Other", Produces = Array.Empty<ManaColor>() };
        SpellRequirement probe = new()
        {
            Name = "probe",
            ManaValue = onCurveTurn,
            Pips = new Dictionary<ManaColor, int>(),
            TrueColorlessPips = category == SourceRequirementCategory.Colorless ? pips : 0,
            SnowPips = category == SourceRequirementCategory.Snow ? pips : 0,
        };

        return SimSyntheticCast(
            librarySize,
            totalLands,
            averageManaValue,
            isSingleton,
            qualifyingSources,
            trials,
            onCurveTurn,
            qualifyingTemplate,
            otherTemplate,
            probe,
            colorlessSnow: true);
    }

    private static int SimSyntheticCast(
        int librarySize,
        int totalLands,
        double averageManaValue,
        bool isSingleton,
        int qualifyingSources,
        int trials,
        int onCurveTurn,
        ManaSource qualifyingSourceTemplate,
        ManaSource otherSourceTemplate,
        SpellRequirement probe,
        bool colorlessSnow = false)
    {
        var sources = new List<ManaSource>(totalLands);
        for (int i = 0; i < qualifyingSources; i++)
        {
            sources.Add(qualifyingSourceTemplate);
        }
        for (int i = qualifyingSources; i < totalLands; i++)
        {
            sources.Add(otherSourceTemplate);
        }

        var deck = new ManabaseDeck
        {
            TotalCards = librarySize,
            CommanderCount = 0,
            Sources = sources,
            Spells = new List<SpellRequirement> { probe },
            AverageManaValue = averageManaValue,
            IsSingleton = isSingleton,
        };
        return CastabilitySimulator.Simulate(
            deck,
            librarySize,
            probe,
            onCurveTurn,
            genericReduction: 0,
            trials: trials,
            colorlessSnow: colorlessSnow).CastPercent;
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
        int baseThreshold = mode switch
        {
            ManabaseMode.Cedh => CedhSupportThreshold,
            ManabaseMode.Focused => FocusedSupportThreshold,
            _ => CasualSupportThreshold,
        };
        if (importance == CommanderImportance.Central && commanderColors.Contains(color))
        {
            return Math.Max(baseThreshold, CedhSupportThreshold);
        }

        return baseThreshold;
    }

    private static IReadOnlySet<ManaColor> CommanderColors(ManabaseDeck deck)
        => new HashSet<ManaColor>(ManabaseColorMask.ColorsFromMask(ManabaseColorMask.CommanderColorMask(deck.Spells)));

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
    private static double EffectiveSources(ManabaseDeck deck, ManaColor color, bool untappedOnly, double scrySourceCredit = 0.0)
        => EffectiveSources(deck, source => source.Produces.Contains(color), untappedOnly, scrySourceCredit);

    private static double EffectiveSources(
        ManabaseDeck deck,
        Func<ManaSource, bool> qualifier,
        bool untappedOnly,
        double baseCredit = 0.0)
    {
        double total = baseCredit;
        foreach (ManaSource source in deck.Sources)
        {
            if (!qualifier(source))
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

    private static Func<ManaSource, bool> SourceQualifier(SourceRequirementCategory category) => category switch
    {
        SourceRequirementCategory.Colorless => source => source.ProducesColorless,
        SourceRequirementCategory.Snow => source => source.IsSnow,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    private static int SpecialCategoryPips(SpellRequirement spell, SourceRequirementCategory category) => category switch
    {
        SourceRequirementCategory.Colorless => spell.TrueColorlessPips,
        SourceRequirementCategory.Snow => spell.SnowPips,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    // TAP-01/TAP-02: build the tap-quality metrics from the already-computed color findings and
    // castability rows — no second simulation pass. Composition (overall + per color) divides the RAW
    // (un-rounded) untapped weight by the RAW total weight; using the rounded ColorSourceFinding
    // .ActualSources would skew whole-percent outputs (Codex HIGH-2 / D5). Turn-1 availability is the
    // mean of CardCastability.Turn1UntappedTrials over NON-COMMANDER rows (D1/D3; fall back to all rows
    // only when there are none), divided by the trial budget. All divisions guard against zero.
    private static ManabaseTapAnalysis ComputeTapAnalysis(
        ManabaseDeck deck,
        IReadOnlyList<ColorSourceFinding> colorFindings,
        IReadOnlyList<CardCastability> castability,
        int defaultTrials,
        double scrySourceCredit)
    {
        double totalUntapped = 0.0;
        double totalAll = 0.0;
        var colorTap = new Dictionary<ManaColor, ColorTapFinding>();
        foreach (ColorSourceFinding f in colorFindings)
        {
            if (f.IsSpecialCategory)
            {
                continue;
            }

            // RAW (un-rounded) numerator + denominator — never the rounded f.ActualSources.
            double rawUntapped = f.UntappedSources;
            double rawTotal = EffectiveSources(deck, f.Color, untappedOnly: false, scrySourceCredit);
            totalUntapped += rawUntapped;
            totalAll += rawTotal;
            colorTap[f.Color] = new ColorTapFinding
            {
                UntappedSources = rawUntapped,
                TotalSources = rawTotal,
                UntappedPercent = rawTotal > 0
                    ? (int)Math.Round(100.0 * rawUntapped / rawTotal)
                    : 0,
            };
        }

        int overallPct = totalAll > 0
            ? (int)Math.Round(100.0 * totalUntapped / totalAll)
            : 0;

        // D1/D3: average T1 availability over non-commander rows (a commander is rarely a T1 play);
        // fall back to all rows only when the deck has no non-commander castability rows.
        var nonCommanderRows = castability.Where(r => !r.IsCommander).ToList();
        IReadOnlyList<CardCastability> avgRows = nonCommanderRows.Count > 0 ? nonCommanderRows : castability;
        int turn1Pct = avgRows.Count > 0 && defaultTrials > 0
            ? (int)Math.Round(100.0 * avgRows.Average(r => r.Turn1UntappedTrials) / defaultTrials)
            : 0;

        return new ManabaseTapAnalysis
        {
            OverallUntappedPercent = overallPct,
            UntappedSources = totalUntapped,
            TotalSources = totalAll,
            Turn1UntappedPercent = turn1Pct,
            ColorTap = colorTap,
        };
    }

    // MULLIGAN-01..05: build the deck-level opening-hand / mulligan evaluation from the already-
    // computed castability rows — no second simulation pass. Keepable %/keep-size percents are
    // spell-INDEPENDENT (LondonMulligan ignores the spell), so they are validly averaged across ALL
    // non-commander rows (mirrors ComputeTapAnalysis's D1/D3 fallback). Representative openers are
    // SPELL-SPECIFIC (on-curve castability differs per row), so they are selected from the EARLIEST
    // (lowest ManaValue, then OnCurveTurn) non-commander rows only, so the surfaced read is about a
    // genuine early play the deck must make on curve.
    private static ManabaseMulliganEvaluation ComputeMulliganEvaluation(
        ManabaseDeck deck,
        IReadOnlyList<CardCastability> castability,
        int defaultTrials,
        int librarySize,
        ManabaseMode mode,
        CommanderImportance importance,
        bool keepShapes,
        bool useManaQuantity,
        bool colorAwareMulligan,
        bool gateRampOnCastable,
        bool ritualBurst,
        bool colorlessSnow,
        ManabasePlanPresence? planPresence = null)
    {
        var nonCommanderRows = castability.Where(r => !r.IsCommander).ToList();
        IReadOnlyList<CardCastability> avgRows = nonCommanderRows.Count > 0 ? nonCommanderRows : castability;
        bool shapeGateActive = keepShapes && mode == ManabaseMode.Cedh;

        // kept7 and to6 are the observed primary shares. keepable and to5 are DERIVED from them rather
        // than independently rounded so the pasteable artifact's numbers always reconcile: the simulator
        // increments keepableTrials iff keptSize >= 6 (CastabilitySimulator: every keep is 7 or 6 or 5),
        // so keepable == kept7 + to6 and the three keep-size shares partition 100%. Independent
        // Math.Round of each raw counter can otherwise print e.g. keepable 85% over a 60% / 24% breakdown
        // (sum 84) or three shares summing to 99/101. Deriving eliminates that drift by construction.
        int kept7Percent = AveragePercent(avgRows, r => r.Kept7Trials, defaultTrials);
        int mulliganTo6Percent = AveragePercent(avgRows, r => r.MulliganTo6Trials, defaultTrials);
        int keepablePercent = kept7Percent + mulliganTo6Percent;
        int mulliganTo5Percent = avgRows.Count > 0 ? Math.Max(0, 100 - keepablePercent) : 0;

        string band = keepablePercent switch
        {
            >= 85 => "high",
            >= 70 => "medium",
            _ => "low",
        };

        // Openers surface a GENUINE early play the deck must make on curve, so free / zero-cost spells
        // are excluded from the row pool: Deflecting Swat, Fierce Guardianship, Force of Negation and
        // the rest of the "cast without paying its mana cost" cycle are auto-reduced to effective 0
        // (DetectSelfCost), which makes them the lowest-ManaValue rows and pulls them to the front of
        // the ordering below — yet a 0-cost spell is trivially castable turn 1 and carries no mana-base
        // signal, so naming it as the representative early play is misleading. Prefer rows that actually
        // demand mana (ManaValue >= 1); fall back to all non-commander rows only when every tracked
        // spell is free (a degenerate paste), so the read is never silently emptied.
        bool commanderCentral = shapeGateActive && IsCommanderCentral(deck, castability, importance, mode);
        List<CardCastability> openerPool = commanderCentral
            ? castability.Where(r => shapeGateActive ? r.OnCurveTurn <= CedhMulliganCalibration.RepresentativeLineTurnCap : true).ToList()
            : nonCommanderRows;
        List<CardCastability> demandingRows = openerPool.Where(r => r.ManaValue >= 1).ToList();
        List<CardCastability> openerRows = demandingRows.Count > 0 ? demandingRows : openerPool;

        // Earliest-row-first, then concatenate each row's own samples, then keep the first sample seen
        // per distinct Decision (at most 3: "keep 7" / "mulligan to 6" / "mulligan to 5") — each sample
        // already carries its own row's TrackedSpellName + TrackedOnCurveTurn, so this never fabricates
        // a cross-row claim. Openers are SPELL-SPECIFIC (unlike the spell-independent keepable/keep-size
        // percents above), so they are drawn ONLY from non-commander rows — a commander is rarely the
        // early on-curve play the read is meant to surface. Empty when the deck has no non-commander row.
        // When the plan-presence pass ran (flag on), it produced openers that PREFER a hand holding a
        // castable-on-curve plan card (the permanents-only "hand with a plan" read) at each kept size —
        // that is the more informative opener, so it wins. With the flag off (planPresence null / no
        // openers) fall back to the per-row samples: earliest-row-first, first sample per Decision, ≤3.
        // Each per-row sample already carries its own TrackedSpellName + TrackedOnCurveTurn, so this
        // never fabricates a cross-row claim.
        List<OpeningHandSample> openers = planPresence?.RepresentativeOpeners is { Count: > 0 } planOpeners
            ? planOpeners.ToList()
            : openerRows
                .Where(r => !shapeGateActive || r.OnCurveTurn <= CedhMulliganCalibration.RepresentativeLineTurnCap)
                .OrderBy(r => commanderCentral && r.IsCommander && r.OnCurveTurn < r.ManaValue ? 0 : 1)
                .ThenBy(r => r.ManaValue)
                .ThenBy(r => r.OnCurveTurn)
                .SelectMany(r => r.RepresentativeOpeners)
                .GroupBy(s => s.Decision, StringComparer.Ordinal)
                .Select(g => g.First())
                .Take(3)
                .ToList();
        double curveCoverageTurns = keepShapes
            ? CastabilitySimulator.SimulateCurveCoverage(
                deck,
                librarySize,
                defaultTrials,
                useManaQuantity,
                colorAwareMulligan,
                gateRampOnCastable,
                ritualBurst,
                colorlessSnow)
            : 0.0;

        return new ManabaseMulliganEvaluation
        {
            KeepableHandPercent = keepablePercent,
            KeepableBand = band,
            Kept7Percent = kept7Percent,
            MulliganTo6Percent = mulliganTo6Percent,
            MulliganTo5Percent = mulliganTo5Percent,
            ColorCount = EnumerateUsedColors(deck).Count(),
            AverageManaValue = deck.AverageManaValue,
            RepresentativeOpeners = openers,
            PlanPresence = planPresence,
            PlanKeepablePercent = shapeGateActive && planPresence is not null
                ? planPresence.PlanKeepablePercent
                : 0,
            PlanKeepableBand = shapeGateActive && planPresence is not null
                ? planPresence.PlanKeepableBand
                : string.Empty,
            CurveCoverageTurns = curveCoverageTurns,
        };
    }

    // Shared divide-by-zero-guarded average-percent helper for the keepable/keep-size figures.
    private static int AveragePercent(IReadOnlyList<CardCastability> rows, Func<CardCastability, int> selector, int defaultTrials)
        => rows.Count > 0 && defaultTrials > 0
            ? (int)Math.Round(100.0 * rows.Average(selector) / defaultTrials)
            : 0;

    /// <summary>
    /// MULLIGAN test seam: exposes <see cref="ComputeMulliganEvaluation"/> directly so the aggregation
    /// logic (keepable-band thresholds, keep-size percent derivation, early-row opener selection,
    /// empty-rows safe-zero) is unit-testable over hand-constructed castability rows — no Monte-Carlo
    /// needed (mirrors the <c>ColorKeepSatisfiedForTest</c> seam pattern in <c>CastabilitySimulator</c>).
    /// </summary>
    internal static ManabaseMulliganEvaluation ComputeMulliganEvaluationForTest(
        ManabaseDeck deck,
        IReadOnlyList<CardCastability> castability,
        int defaultTrials,
        ManabaseMode mode = ManabaseMode.Casual,
        CommanderImportance importance = CommanderImportance.Standard,
        bool keepShapes = false,
        ManabasePlanPresence? planPresence = null)
        => ComputeMulliganEvaluation(
            deck,
            castability,
            defaultTrials,
            deck.TotalCards - deck.CommanderCount,
            mode,
            importance,
            keepShapes,
            useManaQuantity: false,
            colorAwareMulligan: false,
            gateRampOnCastable: false,
            ritualBurst: false,
            colorlessSnow: false,
            planPresence);

    /// <summary>
    /// D-02 commander-centrality heuristic: combines the already-computed command-zone castability row,
    /// the commander's <see cref="SpellRequirement.PlanRoles"/>, and
    /// <see cref="CommanderImportance"/> to decide whether cEDH representative-openers may surface the
    /// commander as a central early line. Classification-degraded fallback is allowed only when the
    /// entire deck has no role tags at all.
    /// </summary>
    private static bool IsCommanderCentral(
        ManabaseDeck deck,
        IReadOnlyList<CardCastability> castability,
        CommanderImportance importance,
        ManabaseMode mode)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(castability);

        if (mode != ManabaseMode.Cedh || importance == CommanderImportance.Low)
        {
            return false;
        }

        CardCastability? strongestCommanderRow = castability
            .Where(c => c.IsCommander)
            .MaxBy(c => c.CastPercent);
        if (strongestCommanderRow is null || strongestCommanderRow.CastPercent < CedhSupportThreshold)
        {
            return false;
        }

        bool rolesUnavailable = deck.Spells.All(s => s.PlanRoles == PlanRole.None);
        if (rolesUnavailable)
        {
            return true;
        }

        return deck.Spells.Any(
            s => s.IsCommander
                && (s.PlanRoles.HasFlag(PlanRole.Payoff)
                    || s.PlanRoles.HasFlag(PlanRole.Engine)
                    || s.PlanRoles.HasFlag(PlanRole.TutorCombo)));
    }

    internal static bool IsCommanderCentralForTest(
        ManabaseDeck deck,
        IReadOnlyList<CardCastability> castability,
        CommanderImportance importance,
        ManabaseMode mode)
        => IsCommanderCentral(deck, castability, importance, mode);

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
            sb.Append(CultureInfo.InvariantCulture, $"(add {ManabaseWording.ApproximatePhrase("land", -delta)}). ");
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
            int addSources = weakest.Deficit > 0 ? ManabaseWording.ApproximateCount(weakest.Deficit) : 0;
            string addClause = addSources > 0 ? $" (add ~{addSources})" : string.Empty;
            sb.Append(CultureInfo.InvariantCulture,
                $"Weakest color: {weakest.CategoryName} — {weakest.ActualSources:F1} sources vs {weakest.RequiredSources} needed for {weakest.DrivingSpell}{addClause}. ");
            sb.Append(CultureInfo.InvariantCulture,
                $"{weakest.UnderSupportedCount} of {weakest.DisplayEvaluatedCardCount} {weakest.CategoryName} cards under-supported; worst cast: {weakest.WorstSpell} (~{weakest.WorstSpellCastPercent:F0}%).");
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
            CardCastability? commander = castability.Where(c => c.IsCommander).MinBy(c => c.CastPercent);
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

    private static string ModeLabel(ManabaseMode mode) => mode switch
    {
        ManabaseMode.Cedh => "cEDH",
        ManabaseMode.Focused => "Focused",
        _ => "Casual",
    };
}

/// <summary>
/// Shared commander/deck color-mask helpers for the manabase classifier and analyzer.
/// </summary>
internal static class ManabaseColorMask
{
    /// <summary>The five colored mana colors in canonical WUBRG order.</summary>
    internal static readonly IReadOnlyList<ManaColor> Wubrg =
        new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green };

    /// <summary>Builds the colored commander-identity mask from commander spell pips only.</summary>
    /// <param name="spells">The spell requirements to inspect.</param>
    /// <returns>A five-bit mask over WUBRG.</returns>
    internal static int CommanderColorMask(IReadOnlyList<SpellRequirement> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        int mask = 0;
        foreach (SpellRequirement spell in spells)
        {
            if (!spell.IsCommander)
            {
                continue;
            }

            foreach (KeyValuePair<ManaColor, int> pip in spell.Pips)
            {
                if (pip.Value > 0 && pip.Key != ManaColor.Colorless)
                {
                    mask |= ColorBit(pip.Key);
                }
            }
        }

        return mask;
    }

    /// <summary>Expands a five-bit WUBRG mask back into an ordered color list.</summary>
    /// <param name="mask">The colored bitmask.</param>
    /// <returns>An ordered list of colors present in the mask.</returns>
    internal static IReadOnlyList<ManaColor> ColorsFromMask(int mask)
    {
        var colors = new List<ManaColor>(5);
        foreach (ManaColor color in Wubrg)
        {
            if ((mask & ColorBit(color)) != 0)
            {
                colors.Add(color);
            }
        }

        return colors;
    }

    private static int ColorBit(ManaColor color) => color switch
    {
        ManaColor.White => 1 << 0,
        ManaColor.Blue => 1 << 1,
        ManaColor.Black => 1 << 2,
        ManaColor.Red => 1 << 3,
        ManaColor.Green => 1 << 4,
        _ => 0,
    };
}
