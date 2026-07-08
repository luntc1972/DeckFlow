using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Seeded Monte-Carlo castability: for one spell it answers the single JOINT event
/// "by my effective on-curve turn T, can I produce ≥ T mana INCLUDING the spell's colored pips?"
/// over many simulated games with a London mulligan.
/// </summary>
/// <remarks>
/// <para>
/// This replaces the old analytic <c>P_mana × P_color</c> independence product, which double-counts
/// the fact that the SAME physical lands supply both mana quantity and colors (so it understated the
/// joint by ~30 points versus the Salubrious Snail / Karsten model — see phase 64 VALIDATION). A
/// simulation models the correlation directly: one shuffled library, one sequence of land drops, and
/// a single success test that requires enough total mana AND a color assignment that covers every pip.
/// </para>
/// <para>
/// It also bakes in the London mulligan (Karsten/Snail do; the old model didn't), which materially
/// lifts early-land consistency — the single largest source of the gap.
/// </para>
/// <para>
/// <b>Seed:</b> each spell uses a stable hash of its name as the RNG seed, so the result is
/// reproducible across runs and across machines (no global mutable RNG, no wall-clock seed). Two
/// spells with identical profiles but different names get independent — but each individually fixed —
/// streams, which is fine: 20k trials swamps the per-name seed variance to well under a point.
/// </para>
/// </remarks>
public static class CastabilitySimulator
{
    /// <summary>Trials per spell. 20k keeps the Monte-Carlo error well under ~0.5 points.</summary>
    public const int DefaultTrials = 20_000;

    // MQ-05: max distinct colors the opening lands must show before a multi-color hand is kept. Capped
    // at 2 (user decision 2026-06-23): want >=2 colors in a 2+ color deck, never demand all of a 3-5c
    // deck's colors in the opener (real play keeps WU in a WUBRG deck). Only consulted when the
    // color-aware-mulligan flag is on AND the deck plays 2+ colors.
    private const int ColorKeepCap = 2;

    // Reusable category for a single library card. Lands carry their color set; ramp carries a deploy
    // cost (its mana value) plus the color set it taps for; filler is everything else.
    private enum CardKind
    {
        UntappedLand,
        TappedLand,
        Ramp,
        Filler,
    }

    private readonly struct LibraryCard
    {
        public LibraryCard(
            CardKind kind,
            int colorMask,
            int deployCost,
            bool isLand,
            double activationWeight = 1.0,
            int manaAmount = 1,
            (int Bit, int Count)[]? rampPips = null,
            PlanRole planRoles = PlanRole.None,
            int planManaValue = 0,
            (int Bit, int Count)[]? planPips = null)
        {
            Kind = kind;
            ColorMask = colorMask;
            DeployCost = deployCost;
            IsLand = isLand;
            ActivationWeight = activationWeight;
            ManaAmount = manaAmount;
            RampPips = rampPips;
            PlanRoles = planRoles;
            PlanManaValue = planManaValue;
            PlanPips = planPips;
        }

        /// <summary>
        /// Plan-presence only: the win-directed roles this card fills, or None. Mana-inert — a plan card
        /// is still ordinary non-source filler for every per-spell castability sim; these fields are read
        /// solely by <see cref="SimulatePlanPresence"/>, so tagging them never changes existing results.
        /// </summary>
        public PlanRole PlanRoles { get; }

        /// <summary>Plan-presence only: this plan card's own on-curve turn (its mana value).</summary>
        public int PlanManaValue { get; }

        /// <summary>Plan-presence only: this plan card's colored pip requirement, for its castability test.</summary>
        public (int Bit, int Count)[]? PlanPips { get; }

        public bool IsPlanCard => PlanRoles != PlanRole.None;

        public CardKind Kind { get; }

        /// <summary>
        /// P4 gated-ramp: the ramp spell's OWN colored pip requirement (the pips needed to CAST it),
        /// so the simulator can refuse to credit a ramp piece the board cannot yet pay the colored cost
        /// for. <see langword="null"/> for non-ramp cards and when the gate is off (legacy path). Empty
        /// means a colorless ramp (only generic mana needed).
        /// </summary>
        public (int Bit, int Count)[]? RampPips { get; }

        /// <summary>Bitmask over the five colors (see <see cref="ColorBit"/>); 0 for colorless-only.</summary>
        public int ColorMask { get; }

        /// <summary>Mana value to deploy a ramp piece (0 = fast mana, plays turn 1). Unused for lands.</summary>
        public int DeployCost { get; }

        public bool IsLand { get; }

        /// <summary>
        /// MQ-02: how much mana this source makes per activation, all of ONE chosen color (Sol Ring /
        /// Ancient Tomb = 2 colorless, Gilded Lotus = 3 of one color). 1 unless the mana-quantity flag
        /// is on. The simulator caps a single source at covering <see cref="ManaAmount"/> pips and locks
        /// them to one color, so a multi-color source can never pay two DIFFERENT colored pips.
        /// </summary>
        public int ManaAmount { get; }

        /// <summary>
        /// Probability this source is "live" in any given game, in (0,1]. 1.0 for a whole source.
        /// <para>
        /// This is now used ONLY by enabler-conditional granted sources (<see cref="ManaSource.IsConditional"/>):
        /// the 0.25 any-color sources from Cryptolith Rite / Relic of Legends that only produce if the
        /// granter is on the battlefield AND the creature survives. They keep a per-trial Bernoulli roll
        /// at this weight because that dependency is genuinely speculative and out of scope to model fully.
        /// </para>
        /// <para>
        /// Deployable ramp (mana rocks, dorks, MDFC backs, fast mana) and discounted lands (basic-fetch)
        /// are NOT activated this way: they enter the sim at FULL value (1.0) as a single card. They are
        /// cards you draw and play, and the simulator ALREADY models their friction explicitly — deploy
        /// cost (their MV) plus summoning-sickness/online-next-turn timing. The analytic 0.75/0.5/0.67
        /// weights are proxies for THAT SAME friction (used by the color-source counting math), so
        /// re-applying them as activation here would double-discount and push every card's cast % ~5-7
        /// points below the Salubrious Snail / reality baseline. A drawn-and-cast Sol Ring is a full mana
        /// source.
        /// </para>
        /// </summary>
        public double ActivationWeight { get; }

        /// <summary>True when <see cref="ActivationWeight"/> is below a full source (needs a per-trial roll).</summary>
        public bool IsPartial => ActivationWeight < 1.0;
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

    /// <summary>
    /// Simulate the castability of one spell. The library is built once per call from the deck; the
    /// caller supplies the spell, its colored pips, and its effective on-curve turn (already shifted
    /// by any cost reducer). Returns a <see cref="CardCastability"/> with the simulated cast %.
    /// </summary>
    /// <param name="deck">The classified deck (provides lands, ramp sources, and granted sources).</param>
    /// <param name="librarySize">Cards in the library (deck minus commanders).</param>
    /// <param name="spell">The spell to score.</param>
    /// <param name="effectiveTurn">The turn the spell is cast on curve, after cost reduction.</param>
    /// <param name="genericReduction">Generic mana shaved off the spell's cost by reducers (capped upstream).</param>
    /// <param name="trials">Trial count (default <see cref="DefaultTrials"/>).</param>
    /// <param name="useManaQuantity">
    /// MQ-02 flag (snapshotted once by the caller). When false (default) every source is worth exactly
    /// 1 mana — byte-identical to the pre-MQ-02 behavior. When true, a source pays its
    /// <see cref="ManaSource.ManaAmount"/> in mana (all of one chosen color).
    /// </param>
    /// <param name="colorAwareMulligan">
    /// MQ-05 flag (snapshotted once by the caller). When false (default) the London mulligan keeps on
    /// land COUNT only — byte-identical to the pre-MQ-05 behavior. When true AND the deck plays 2+
    /// colors, a non-forced keep also requires the opening lands to show enough distinct colors (see
    /// <see cref="ColorKeepCap"/>). Mono-color decks are byte-identical even when this is true.
    /// </param>
    /// <param name="gateRampOnCastable">
    /// P4 gated-ramp flag (snapshotted once by the caller, tied to the land-ramp-sim flag). When false
    /// (default) a drawn ramp piece is deployed as soon as its DEPLOY COST is affordable by generic mana
    /// — byte-identical to the pre-fix behavior. When true, a ramp piece is deployed only when the
    /// board's online sources can also pay the ramp's OWN COLORED cost (mirrors 17Lands: a ramp source
    /// is credited only once the ramp itself was castable), so an un-castable ramp never inflates the
    /// spell's mana.
    /// </param>
    public static CardCastability Simulate(
        ManabaseDeck deck,
        int librarySize,
        SpellRequirement spell,
        int effectiveTurn,
        int genericReduction,
        int trials = DefaultTrials,
        bool useManaQuantity = false,
        bool colorAwareMulligan = false,
        bool gateRampOnCastable = false)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(spell);

        // 70-03b: exclude one same-name land-ramp source when scoring this spell's own row (a card
        // cannot ramp itself out). No-op unless this spell is a modeled land-ramp source.
        IReadOnlyList<LibraryCard> library = BuildLibrary(deck, librarySize, useManaQuantity, gateRampOnCastable, excludeSourceName: spell.Name);

        // MQ-05: distinct colors the deck actually demands across all spell pips (capped at 5). Only
        // computed when the flag is on; <=1 makes the color gate a no-op (mono decks stay identical).
        int deckColorCount = colorAwareMulligan ? DeckColorCount(deck) : 0;

        // The spell's effective cost: colored pips (immutable) plus generic. Reducers only shave
        // generic mana, and never below the pip count — the floor is enforced by the caller via
        // effectiveTurn, but we also clamp generic here so a deep reducer can't go negative.
        int totalPips = spell.Pips
            .Where(p => p.Key != ManaColor.Colorless && p.Value > 0)
            .Sum(p => p.Value);
        int printedGeneric = Math.Max(0, spell.ManaValue - totalPips);
        int effectiveGeneric = Math.Max(0, printedGeneric - Math.Max(0, genericReduction));
        int effectiveCost = Math.Max(Math.Max(1, totalPips), effectiveGeneric + totalPips);

        // The colored pips as (bit, count) pairs for the greedy color-coverage check.
        var pipReq = spell.Pips
            .Where(p => p.Key != ManaColor.Colorless && p.Value > 0)
            .Select(p => (Bit: ColorBit(p.Key), Count: p.Value))
            .ToArray();

        int turn = Math.Max(1, effectiveTurn);

        // Stable per-spell seed (no global mutable RNG; reproducible across runs/machines).
        var rng = new Random(StableSeed(spell.Name));

        int successes = 0;
        int manaShortFailures = 0; // had wrong/short mana count regardless of colors
        int colorShortFailures = 0; // had enough total mana but couldn't cover the pips
        long delaySum = 0; // sum of max(0, firstCastableTurn - onCurveTurn) over all trials
        int turn1UntappedSuccesses = 0; // TAP-02: trials with >=1 untapped/usable source on turn 1

        // MULLIGAN-01..04: pure-observation keep-size counters + up to 3 representative openers,
        // bucketed by the keep VALUE LondonMulligan RETURNS (never the mulligan-depth index).
        int keepableTrials = 0;
        int kept7 = 0;
        int mulliganTo6 = 0;
        int mulliganTo5 = 0;
        var openerSamples = new List<OpeningHandSample>(3);

        // The deck's color-keep target for HasPlan, computed once (independent of colorAwareMulligan —
        // do NOT reuse `deckColorCount`, which is 0 when that flag is off).
        int planColorTarget = Math.Min(DeckColorCount(deck), ColorKeepCap);

        // Scratch arrays reused across trials to keep allocations low.
        int[] deck0 = new int[library.Count];
        for (int i = 0; i < library.Count; i++)
        {
            deck0[i] = i;
        }

        int[] shuffled = new int[library.Count];
        var availableColors = new List<(int Mask, int Amount)>(20); // online sources as (mask, mana amount)
        var onlineLandMasks = new List<int>(20); // scratch: lands whose online-turn <= currentTurn (masks only)

        // Partial sources (FINDING-2 MEDIUM): indices of sub-1 cards needing a per-trial Bernoulli roll.
        // `active[i]` is true when card i is live this trial; full cards are always active, partials are
        // active with probability == ActivationWeight. Inactive partials are treated as inert filler.
        int[] partialIndices = Enumerable.Range(0, library.Count).Where(i => library[i].IsPartial).ToArray();
        bool[] active = new bool[library.Count];
        Array.Fill(active, true);

        // We only ever inspect the opening 7 plus one draw per turn (every turn, including turn 1
        // — Commander is multiplayer), so shuffling the first (7 + turn) slots is sufficient and
        // far cheaper than a full Fisher-Yates of ~99 cards.
        // Critically it must cover BOTH the mulligan look AND every per-turn draw — otherwise the
        // un-shuffled tail (which BuildLibrary front-loads with sources) biases draws land-heavy.
        int prefix = Math.Min(library.Count, 7 + turn + GraceWindow(turn) + 2);

        for (int t = 0; t < trials; t++)
        {
            // Roll which partial sources are live this game (Bernoulli on the seeded RNG). Done BEFORE
            // the shuffle/mulligan so an inactive partial counts as inert for land-counts too.
            foreach (int pi in partialIndices)
            {
                active[pi] = rng.NextDouble() < library[pi].ActivationWeight;
            }

            Array.Copy(deck0, shuffled, library.Count);
            ShufflePrefix(shuffled, prefix, rng);
            int keptSize = LondonMulligan(library, shuffled, active, rng, deck.AverageManaValue, prefix, deck.IsSingleton, colorAwareMulligan, deckColorCount);
            // Tiny decks (some unit fixtures) can be smaller than a 7-card opener — clamp the hand.
            int handCount = Math.Min(library.Count, keptSize);

            // MULLIGAN STAGE 1 (pure observation, no rng draw): bucket by the RETURNED keep value —
            // a singleton's depth-1 Commander free mulligan still returns 7, so it lands in Kept7Trials,
            // never MulliganTo6Trials. Also stash this trial's kept-hand composition (if this is the
            // first trial to observe this keptSize) for the STAGE 2 sample built after SimulateGame,
            // once firstCastableTurn is known.
            switch (keptSize)
            {
                case 7:
                    kept7++;
                    break;
                case 6:
                    mulliganTo6++;
                    break;
                case 5:
                    mulliganTo5++;
                    break;
                default:
                    // Defensive: LondonMulligan's schedule only ever returns 7/6/5. An unexpected value
                    // is observed but deliberately not miscounted into any bucket.
                    break;
            }

            if (keptSize >= 6)
            {
                keepableTrials++;
            }

            // Only sample a fully-dealt hand: handCount = Math.Min(library.Count, keptSize) clamps below
            // keptSize only for a degenerate sub-7-card library, where the composition tally (over
            // handCount) could not sum to the displayed KeptCards (keptSize). Real 99-card decks always
            // satisfy handCount == keptSize, so this is a no-op there and merely suppresses a
            // self-contradicting opener readout for tiny partial pastes.
            bool needSample = openerSamples.Count < 3 && handCount == keptSize && !openerSamples.Any(s => s.KeptCards == keptSize);
            int stashedLands = 0, stashedRamp = 0, stashedOther = 0, stashedColors = 0;
            string stashedDecision = string.Empty;
            if (needSample)
            {
                int landColorMask = 0;
                for (int i = 0; i < handCount; i++)
                {
                    int idx = shuffled[i];
                    LibraryCard card = library[idx];
                    if (active[idx] && card.IsLand)
                    {
                        stashedLands++;
                        landColorMask |= card.ColorMask;
                    }
                    else if (active[idx] && card.Kind == CardKind.Ramp)
                    {
                        stashedRamp++;
                    }
                    else
                    {
                        stashedOther++;
                    }
                }

                stashedColors = CountColors(landColorMask);
                stashedDecision = keptSize switch
                {
                    7 => "keep 7",
                    6 => "mulligan to 6",
                    5 => "mulligan to 5",
                    _ => string.Empty,
                };
            }

            bool success = SimulateGame(
                library, shuffled, active, handCount, turn, effectiveCost, pipReq, availableColors, onlineLandMasks,
                gateRampOnCastable, out bool manaShort, out bool colorShort, out int firstCastableTurn,
                out bool hadUntappedT1);

            // MULLIGAN STAGE 2 (pure observation, no rng draw): build the sample now that
            // firstCastableTurn is known, attributing it to THIS row's tracked spell.
            if (needSample)
            {
                bool onCurveCastable = firstCastableTurn <= turn;
                bool hasPlan = stashedLands >= 2 && stashedColors >= planColorTarget && onCurveCastable;
                openerSamples.Add(new OpeningHandSample
                {
                    Lands = stashedLands,
                    Colors = stashedColors,
                    RampPieces = stashedRamp,
                    OtherCards = stashedOther,
                    KeptCards = keptSize,
                    Decision = stashedDecision,
                    TrackedSpellName = spell.Name,
                    TrackedOnCurveTurn = turn,
                    OnCurveCastable = onCurveCastable,
                    HasPlan = hasPlan,
                });
            }

            // Delay this trial: how many turns LATE the spell first became castable, floored at 0
            // (a spell never tests as castable before its on-curve turn, so this is already >= 0).
            delaySum += Math.Max(0, firstCastableTurn - turn);

            if (hadUntappedT1)
            {
                turn1UntappedSuccesses++;
            }

            if (success)
            {
                successes++;
            }
            else if (manaShort)
            {
                manaShortFailures++;
            }
            else if (colorShort)
            {
                colorShortFailures++;
            }
        }

        int castPercent = Math.Clamp((int)Math.Round(100.0 * successes / trials), 0, 100);
        string limiting = DeriveLimitingFactor(pipReq.Length == 0, manaShortFailures, colorShortFailures, spell);
        double averageDelay = trials > 0 ? Math.Round((double)delaySum / trials, 1) : 0;

        return new CardCastability
        {
            Name = spell.Name,
            ManaValue = spell.ManaValue,
            OnCurveTurn = turn,
            CastPercent = castPercent,
            LimitingFactor = limiting,
            IsCommander = spell.IsCommander,
            IsCostOverridden = spell.IsCostOverridden,
            AverageDelay = averageDelay,
            Turn1UntappedTrials = turn1UntappedSuccesses,
            KeepableTrials = keepableTrials,
            Kept7Trials = kept7,
            MulliganTo6Trials = mulliganTo6,
            MulliganTo5Trials = mulliganTo5,
            RepresentativeOpeners = openerSamples,
        };
    }

    // ---- library construction -------------------------------------------------------------

    /// <summary>
    /// Plan-presence: the share of KEEPABLE opening hands that hold a win-directed card castable on its
    /// own mana-value turn. A dedicated single deck-level pass (one loop, not the ~N per-spell sims)
    /// reusing the same London-mulligan and the same turn-by-turn board model as the per-spell
    /// castability. A plan card counts only when it is drawn by its on-curve turn AND the board can pay
    /// its cost then — a plan card you cannot cast is not a plan. Returns an all-zero result when the
    /// deck carries no plan-tagged spell (flag off / nothing classified).
    /// </summary>
    public static ManabasePlanPresence SimulatePlanPresence(
        ManabaseDeck deck,
        int librarySize,
        int trials = DefaultTrials,
        bool useManaQuantity = false,
        bool colorAwareMulligan = false,
        bool gateRampOnCastable = false)
    {
        ArgumentNullException.ThrowIfNull(deck);

        IReadOnlyList<LibraryCard> library =
            BuildLibrary(deck, librarySize, useManaQuantity, gateRampOnCastable, excludeSourceName: null);

        var planIndices = new List<int>();
        int maxPlanTurn = 1;
        for (int i = 0; i < library.Count; i++)
        {
            if (library[i].IsPlanCard)
            {
                planIndices.Add(i);
                maxPlanTurn = Math.Max(maxPlanTurn, Math.Max(1, library[i].PlanManaValue));
            }
        }

        PlanRole[] singleRoles = { PlanRole.Payoff, PlanRole.Engine, PlanRole.TutorCombo, PlanRole.Interaction };
        var roleCounts = new Dictionary<PlanRole, int>();
        foreach (PlanRole role in singleRoles)
        {
            roleCounts[role] = 0;
        }

        if (planIndices.Count == 0 || trials <= 0)
        {
            return new ManabasePlanPresence
            {
                PlanPresencePercent = 0,
                Band = PlanPresenceBand(0),
                RolePercents = roleCounts,
                KeepableTrials = 0,
            };
        }

        int deckColorCount = colorAwareMulligan ? DeckColorCount(deck) : 0;

        // Stable deck-independent seed: plan-presence is deck-level (not tied to any one tracked spell),
        // so a fixed seed keeps the result reproducible across runs of the same deck.
        var rng = new Random(StableSeed("__deckflow_plan_presence__"));

        int[] deck0 = Enumerable.Range(0, library.Count).ToArray();
        int[] shuffled = new int[library.Count];
        var availableColors = new List<(int Mask, int Amount)>(20);
        var onlineLandMasks = new List<int>(20);
        int[] partialIndices = Enumerable.Range(0, library.Count).Where(i => library[i].IsPartial).ToArray();
        bool[] active = new bool[library.Count];

        // The window must cover the opener plus one draw per turn out to the latest plan card's on-curve
        // turn (+ grace + margin), the same prefix rule the per-spell sim uses.
        int prefix = Math.Min(library.Count, 7 + maxPlanTurn + GraceWindow(maxPlanTurn) + 2);

        int keepable = 0;
        int withPlan = 0;

        for (int t = 0; t < trials; t++)
        {
            Array.Fill(active, true);
            foreach (int pi in partialIndices)
            {
                active[pi] = rng.NextDouble() < library[pi].ActivationWeight;
            }

            Array.Copy(deck0, shuffled, library.Count);
            ShufflePrefix(shuffled, prefix, rng);
            int keptSize = LondonMulligan(
                library, shuffled, active, rng, deck.AverageManaValue, prefix, deck.IsSingleton, colorAwareMulligan, deckColorCount);

            // Plan-presence is measured over KEEPABLE hands only (kept 7 or mull-to-6) — the same
            // keepable band the opener block reports; a mull-to-5 is not a hand you kept on purpose.
            if (keptSize < 6)
            {
                continue;
            }

            keepable++;
            int handCount = Math.Min(library.Count, keptSize);

            PlanRole rolesThisHand = PlanRole.None;
            foreach (int planIdx in planIndices)
            {
                int planTurn = Math.Max(1, library[planIdx].PlanManaValue);

                // Is this plan card drawn by its on-curve turn? Opening cards (pos < handCount) are seen
                // at turn 0; a card at position p is drawn on turn (p - handCount + 1) — one draw per turn
                // including turn 1. Beyond the shuffled prefix it is never seen this trial.
                int pos = -1;
                for (int p = 0; p < prefix; p++)
                {
                    if (shuffled[p] == planIdx)
                    {
                        pos = p;
                        break;
                    }
                }

                if (pos < 0)
                {
                    continue;
                }

                int drawnByTurn = pos < handCount ? 0 : pos - handCount + 1;
                if (drawnByTurn > planTurn)
                {
                    continue;
                }

                // Castable by its on-curve turn? Reuse the full board sim for a spell of this card's cost.
                (int Bit, int Count)[] pips = library[planIdx].PlanPips ?? Array.Empty<(int, int)>();
                bool castable = SimulateGame(
                    library, shuffled, active, handCount, planTurn, planTurn, pips,
                    availableColors, onlineLandMasks, gateRampOnCastable,
                    out _, out _, out int firstCastableTurn, out _);

                if (castable && firstCastableTurn <= planTurn)
                {
                    rolesThisHand |= library[planIdx].PlanRoles;
                }
            }

            if (rolesThisHand != PlanRole.None)
            {
                withPlan++;
                foreach (PlanRole role in singleRoles)
                {
                    if (rolesThisHand.HasFlag(role))
                    {
                        roleCounts[role]++;
                    }
                }
            }
        }

        int percent = keepable > 0 ? (int)Math.Round(100.0 * withPlan / keepable) : 0;
        var rolePercents = new Dictionary<PlanRole, int>();
        foreach (PlanRole role in singleRoles)
        {
            rolePercents[role] = keepable > 0 ? (int)Math.Round(100.0 * roleCounts[role] / keepable) : 0;
        }

        return new ManabasePlanPresence
        {
            PlanPresencePercent = percent,
            Band = PlanPresenceBand(percent),
            RolePercents = rolePercents,
            KeepableTrials = keepable,
        };
    }

    // Provisional bands for the plan-presence headline; re-baselined against calibration decks in the
    // ship phase. Kept as a single golden-tested mapping so the thresholds live in one place.
    private static string PlanPresenceBand(int percent) => percent switch
    {
        >= 65 => "high",
        >= 40 => "medium",
        _ => "low",
    };

    private static IReadOnlyList<LibraryCard> BuildLibrary(ManabaseDeck deck, int librarySize, bool useManaQuantity, bool gateRampOnCastable, string? excludeSourceName)
    {
        var cards = new List<LibraryCard>(librarySize);

        // Map non-land sources (rocks/dorks/granted) to their deploy cost via the matching
        // IsManaSource spell (a rock/dork is BOTH a non-land source AND a flagged spell). Granted
        // sources ("X (granted)") have no spell row — treat them as turn-2 conditional ramp.
        var rampCostByName = deck.Spells
            .Where(s => s.IsManaSource)
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ManaValue, StringComparer.Ordinal);

        // P4 gated-ramp: the colored pip requirement to CAST each ramp piece, keyed by name. Built only
        // when the gate is on. A rock/dork has an IsManaSource spell row; a modeled land-ramp source
        // (Cultivate) has a normal spell row by the same name. Missing/unmatched names → no colored
        // requirement (treated as colorless ramp), so the gate degrades to the generic-mana check.
        Dictionary<string, (int Bit, int Count)[]>? rampPipsByName = gateRampOnCastable
            ? deck.Spells
                .GroupBy(s => s.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => PipArray(g.First()), StringComparer.Ordinal)
            : null;

        // Source modeling distinguishes DEPLOYABLE ramp from ENABLER-CONDITIONAL granted sources:
        //  * Deployable ramp (rocks, dorks, MDFC backs, fast mana) and discounted lands (basic-fetch)
        //    enter at FULL value — one card, activation 1.0. They are cards you draw and play, and the
        //    sim already models their friction via deploy cost + online-turn timing; the analytic
        //    0.75/0.5/0.67 weight is a PROXY for that same friction (it feeds the color-counting math),
        //    so applying it again here as activation would double-discount.
        //  * Enabler-conditional granted sources (IsConditional: the 0.25 any-color sources from
        //    Cryptolith Rite / Relic of Legends) keep a per-trial Bernoulli roll at their weight, because
        //    their production really is speculative (granter alive AND creature survives) and modeling the
        //    enabler fully is out of scope. Their whole part becomes full copies and any leftover fraction
        //    becomes ONE partial card carrying that fraction as its activation probability, so a 0.25
        //    source produces mana in ~25% of games (E[copies] = weight).
        AddSourcesAsCards(deck, cards, rampCostByName, rampPipsByName, useManaQuantity, excludeSourceName);

        // Plan-presence: place win-directed spells as IDENTIFIABLE (still mana-inert) filler so
        // SimulatePlanPresence can find them in a simulated hand and test their on-curve castability.
        // They take filler slots that would otherwise be anonymous, so the library size and every draw
        // probability are unchanged; and because all filler is interchangeable to the per-spell sims (no
        // mana, not a source), tagging them leaves those results byte-identical.
        foreach (SpellRequirement spell in deck.Spells)
        {
            if (cards.Count >= librarySize)
            {
                break;
            }

            if (spell.PlanRoles == PlanRole.None || spell.IsManaSource)
            {
                continue;
            }

            cards.Add(new LibraryCard(
                CardKind.Filler, 0, 0, false,
                planRoles: spell.PlanRoles,
                planManaValue: Math.Max(1, spell.ManaValue),
                planPips: PipArray(spell)));
        }

        // Pad/truncate to the real library size with anonymous filler so draw probabilities match the deck.
        for (int i = cards.Count; i < librarySize; i++)
        {
            cards.Add(new LibraryCard(CardKind.Filler, 0, 0, false));
        }

        if (cards.Count > librarySize)
        {
            cards.RemoveRange(librarySize, cards.Count - librarySize);
        }

        return cards;
    }

    // P4 gated-ramp: extract a spell's colored pip requirement as (bit, count) pairs (colorless pips
    // excluded — only colored access gates a cast). Empty array means "colorless to cast".
    private static (int Bit, int Count)[] PipArray(SpellRequirement spell) =>
        spell.Pips
            .Where(p => p.Key != ManaColor.Colorless && p.Value > 0)
            .Select(p => (Bit: ColorBit(p.Key), Count: p.Value))
            .ToArray();

    private static void AddSourcesAsCards(
        ManabaseDeck deck,
        List<LibraryCard> cards,
        IReadOnlyDictionary<string, int> rampCostByName,
        IReadOnlyDictionary<string, (int Bit, int Count)[]>? rampPipsByName,
        bool useManaQuantity,
        string? excludeSourceName)
    {
        // 70-03b self-exclusion: when scoring a land-ramp spell's OWN row, the single physical copy is
        // the spell being cast, so it must not ALSO be drawable as a ramp source in the same game. Skip
        // ONE matching MODELED LAND-RAMP source (DeployCost set is the unique marker — rocks/dorks and
        // MDFC spell-back sources leave it null, so they are never excluded; off-path is byte-identical).
        bool excludedOne = false;

        foreach (ManaSource source in deck.Sources)
        {
            // Command-zone sources (a mana-producing commander, or the commander as a granted
            // any-color source) are NOT in the 99 — they must never be drawn into the library.
            // librarySize already excludes the commander count; including its source here would
            // both let the sim "draw" the commander and truncate a real card to make room.
            if (source.IsCommander)
            {
                continue;
            }

            if (!excludedOne && !source.IsLand && source.DeployCost is not null
                && excludeSourceName is not null
                && string.Equals(source.Name, excludeSourceName, StringComparison.Ordinal))
            {
                excludedOne = true;
                continue;
            }

            int mask = ColorsToMask(source.Produces);

            // MQ-02: how much mana the source makes per activation. Off → 1 (byte-identical to the
            // pre-MQ-02 sim). Conditional/granted sources always stay 1 (the Bernoulli roll gates a
            // single speculative unit).
            int amount = useManaQuantity && !source.IsConditional ? Math.Max(1, source.ManaAmount) : 1;

            if (source.IsLand)
            {
                CardKind kind = source.EntersUntapped ? CardKind.UntappedLand : CardKind.TappedLand;
                // Lands are never conditional; a discounted basic-fetch is still a full card you draw.
                AddWeighted(cards, kind, mask, deployCost: 0, source.Weight, source.IsConditional, amount, rampPips: null);
                continue;
            }

            // Non-land source = ramp. Deploy cost from the matching mana-source spell; granted/unknown
            // sources default to turn-2 (a typical mana rock / dork comes online around then).
            string baseName = source.Name.EndsWith(" (granted)", StringComparison.Ordinal)
                ? source.Name[..^" (granted)".Length]
                : source.Name;
            // 70-03b: an explicit DeployCost wins (modeled land-ramp, which has no IsManaSource spell
            // row to key off); otherwise resolve from the matching mana-source spell, default turn-2.
            int deployCost = source.DeployCost
                ?? (rampCostByName.TryGetValue(baseName, out int mv) ? mv : 2);

            // P4 gated-ramp: the colored cost to cast THIS ramp piece (null when the gate is off, or
            // for a granted/unmatched source with no spell row → degrades to the generic-mana check).
            (int Bit, int Count)[]? rampPips = rampPipsByName is not null
                && rampPipsByName.TryGetValue(baseName, out (int Bit, int Count)[]? pips)
                ? pips
                : (rampPipsByName is not null ? Array.Empty<(int, int)>() : null);
            AddWeighted(cards, CardKind.Ramp, mask, deployCost, source.Weight, source.IsConditional, amount, rampPips);
        }
    }

    private static void AddWeighted(
        List<LibraryCard> cards,
        CardKind kind,
        int mask,
        int deployCost,
        double weight,
        bool isConditional,
        int amount,
        (int Bit, int Count)[]? rampPips)
    {
        bool isLand = kind is CardKind.UntappedLand or CardKind.TappedLand;

        if (!isConditional)
        {
            // DEPLOYABLE ramp / discounted land: a card you DRAW and PLAY. It enters the sim at FULL
            // value (one card, activation 1.0). The analytic sub-1 weight (rock 0.75, dork 0.5, fetch
            // 0.67, MDFC back 0.8) is a PROXY for deploy-cost + summoning-sickness friction that the sim
            // ALREADY models explicitly (DeployCost from MV, online-next-turn). Re-applying the weight as
            // a Bernoulli activation here would double-discount and pull cast % ~5-7 pts under Snail. A
            // drawn-and-cast Sol Ring is a full mana source. (Each card is one physical copy; MQ-02
            // gives it amount mana, all of one chosen color.)
            cards.Add(new LibraryCard(kind, mask, deployCost, isLand, manaAmount: amount, rampPips: rampPips));
            return;
        }

        // ENABLER-CONDITIONAL granted source (IsConditional): genuinely speculative (granter on board AND
        // creature survives), so keep the per-trial Bernoulli activation at its weight. The whole part
        // becomes full copies; any leftover fraction becomes ONE partial card carrying that fraction as
        // its activation probability (rolled in SimulateGame). A lone 0.25 source contributes mana in
        // ~25% of trials instead of rounding to zero copies. E[copies] = whole + frac = weight.
        int whole = (int)Math.Floor(weight);
        for (int i = 0; i < whole; i++)
        {
            cards.Add(new LibraryCard(kind, mask, deployCost, isLand, rampPips: rampPips));
        }

        double frac = weight - whole;
        if (frac > 1e-9)
        {
            cards.Add(new LibraryCard(kind, mask, deployCost, isLand, activationWeight: frac, rampPips: rampPips));
        }
    }

    private static int ColorsToMask(IReadOnlyList<ManaColor> colors)
    {
        int mask = 0;
        foreach (ManaColor c in colors)
        {
            mask |= ColorBit(c);
        }

        return mask;
    }

    // ---- one game -------------------------------------------------------------------------

    // Plays out turns 1..(turn+grace), drawing every turn (Commander is multiplayer, so the starting
    // player draws on turn 1 too). Returns true if the spell becomes castable on the
    // effective turn OR within the grace window after it. The grace window tracks Snail/Karsten,
    // whose "cast rate" is not strict-on-curve but tolerates a short delay (a player happily casts a
    // 6-drop on turn 7-8). Out-params attribute the LAST turn's failure to mana vs color coverage.
    private static bool SimulateGame(
        IReadOnlyList<LibraryCard> library,
        int[] shuffled,
        bool[] active,
        int handCount,
        int turn,
        int effectiveCost,
        (int Bit, int Count)[] pipReq,
        List<(int Mask, int Amount)> availableColors,
        List<int> onlineLandMasks,
        bool gateRampOnCastable,
        out bool manaShort,
        out bool colorShort,
        out int firstCastableTurn,
        out bool hadUntappedT1)
    {
        manaShort = false;
        colorShort = false;
        hadUntappedT1 = false;

        // Snail's metric forgives a short delay; lower drops get a slightly wider window (a 1-drop is
        // rarely cast exactly on turn 1, but a player will still happily cast it on turn 2-3). The
        // window shrinks with the curve so a 6-drop isn't credited for casting on turn 10.
        int grace = GraceWindow(turn);
        int lastTurn = turn + grace;

        // Default: never castable within the grace window → cap the "first castable" at one turn past
        // the last simulated turn, so the delay metric is bounded (not implementation-dependent).
        firstCastableTurn = lastTurn + 1;

        // Hand = first handCount indices of the shuffled library; library draw pointer follows.
        int drawPtr = handCount;

        // Track our board: each land is (color mask, the turn it first produces mana). An untapped land
        // is online the turn it is played; an ETB-tapped land is online only NEXT turn — so it must NOT
        // count toward this turn's mana or color access. We model this exactly like ramp's OnlineTurn
        // (FINDING-1 HIGH): tapped lands previously inflated both the mana count and color coverage the
        // turn they entered.
        var landsOnBoard = new List<(int Mask, int OnlineTurn, int Amount)>(turn + 2);

        // Working hand as a list of library indices.
        var hand = new List<int>(handCount + turn);
        for (int i = 0; i < handCount; i++)
        {
            hand.Add(shuffled[i]);
        }

        // Ramp deployed this turn that comes online NEXT turn (cost > 0); 0-cost is same-turn.
        // We just re-scan the board each turn for simplicity (turn counts are tiny). Amount is the
        // mana it makes once online (MQ-02): 1 unless the mana-quantity flag is on.
        var rampOnBoard = new List<(int Mask, int Cost, int OnlineTurn, int Amount)>(8);

        for (int currentTurn = 1; currentTurn <= lastTurn; currentTurn++)
        {
            // Draw for the turn — including turn 1. Commander is multiplayer, so the "player on
            // the play skips their first draw" rule (CR 103.8a) never applies here — it is a
            // two-player-only rule. Every player, including the one who goes first, draws on their
            // first turn, so by turn N a card has seen 7 + N cards.
            if (drawPtr < library.Count)
            {
                hand.Add(shuffled[drawPtr++]);
            }

            // Play one land this turn: prefer an untapped land that adds a still-missing color THIS turn,
            // then any untapped land, then a tapped land (it won't help this turn but builds the board).
            // A tapped land played this turn enters with OnlineTurn = currentTurn + 1, so it contributes
            // nothing until next turn (FINDING-1 HIGH). On a slack turn before the cast turn, a tapped
            // fixer is preferred over a color-useless untapped land (M2) — the ETB-tapped delay is free
            // when we are not casting this turn.
            PlayOneLand(library, active, hand, landsOnBoard, onlineLandMasks, currentTurn, turn, pipReq);

            // Online lands for this turn: only those whose online-turn has arrived (masks only, for
            // the "still-missing color" check; mana quantity is summed separately below).
            onlineLandMasks.Clear();
            foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
            {
                if (land.OnlineTurn <= currentTurn)
                {
                    onlineLandMasks.Add(land.Mask);
                }
            }

            // Deploy one affordable ramp piece if we still need more mana to reach the spell's cost.
            // Stopping once we can already make the cost prevents runaway over-ramping. 0-cost ramp is
            // same-turn; else next turn. Affordability counts only mana online THIS turn — summing each
            // source's amount (MQ-02): a Sol Ring online contributes 2 toward the cost, not 1.
            //
            // DEPLOY-FRICTION (debug session manabase-too-optimistic, the real P4 fix): the mana spent
            // PLAYING the ramp piece is a real cost. Casting a {2} Signet on turn 2 taps two lands, and
            // those two mana are then NOT available for the payoff spell that same turn — the rock only
            // comes online NEXT turn (its output is already deferred via OnlineTurn). The pre-fix model
            // deployed ramp for free: it added the rock's future mana yet still let the full board pay
            // the payoff this turn, double-counting the deploy turn (~7 pts of over-optimism on the
            // Avatar fixture). TryDeployRamp now returns the cost it spent; we reserve exactly that much
            // generic capacity out of THIS turn's sources before testing the payoff. 0-cost fast mana
            // (Lotus Petal / Mox) reserves nothing and stays genuinely same-turn. This also caps the
            // realistic deploy rate: with only N mana online you cannot play a rock costing more than N.
            int rampSpentThisTurn = 0;
            int availableNow = OnlineMana(landsOnBoard, rampOnBoard, currentTurn);
            if (availableNow < effectiveCost)
            {
                // P4 gated-ramp (kept as a correctness sub-improvement, also tied to land-ramp-sim):
                // when the gate is on, rebuild the board's online sources as (mask, amount) so
                // TryDeployRamp can verify the ramp's OWN colored cost is payable before crediting it.
                // Skipped (null) when the gate is off → legacy generic-mana check.
                List<(int Mask, int Amount)>? onlineForRamp = null;
                if (gateRampOnCastable)
                {
                    onlineForRamp = availableColors;
                    onlineForRamp.Clear();
                    foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
                    {
                        if (land.OnlineTurn <= currentTurn)
                        {
                            onlineForRamp.Add((land.Mask, land.Amount));
                        }
                    }

                    foreach ((int Mask, int Cost, int OnlineTurn, int Amount) r in rampOnBoard)
                    {
                        if (r.OnlineTurn <= currentTurn)
                        {
                            onlineForRamp.Add((r.Mask, r.Amount));
                        }
                    }
                }

                rampSpentThisTurn = TryDeployRamp(library, active, hand, rampOnBoard, availableNow, currentTurn, onlineForRamp);
            }

            // TAP-02 (color-matched, overridden 2026-06-28 after Codex review): record whether any
            // mana source online on turn 1 can produce an untapped source of a NEEDED COLOR on turn 1
            // (colorless spells accept any source). An untapped land played T1 (OnlineTurn == 1) or
            // 0-cost fast mana deployed T1 qualifies only when its color mask intersects the spell's
            // needed colors. A 1-bit observation inside the existing loop (no second sim, no RNG draw,
            // so determinism is preserved). Evaluated BEFORE the on-curve early-continue so it is
            // always set on turn 1 regardless of the spell's effective turn.
            if (currentTurn == 1)
            {
                hadUntappedT1 = HasColorMatchedUntappedT1(landsOnBoard, rampOnBoard, pipReq);
            }

            // From the effective turn onward, test castability; succeed on the first turn it lands.
            if (currentTurn < turn)
            {
                continue;
            }

            // Rebuild the online sources as (mask, amount) capacity records — lands plus ramp that is
            // online this turn (re-read AFTER the ramp deploy so 0-cost same-turn ramp counts).
            availableColors.Clear();
            foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
            {
                if (land.OnlineTurn <= currentTurn)
                {
                    availableColors.Add((land.Mask, land.Amount));
                }
            }

            foreach ((int Mask, int Cost, int OnlineTurn, int Amount) r in rampOnBoard)
            {
                if (r.OnlineTurn <= currentTurn)
                {
                    availableColors.Add((r.Mask, r.Amount));
                }
            }

            // DEPLOY-FRICTION (land-ramp-sim on): reserve the mana we just spent playing a ramp piece, so
            // the payoff spell cannot also use it this turn. We tap the LEAST color-flexible sources first
            // (mirrors real play — pay generic with the lands that least restrict your colored access), so
            // the reserve never wrongly steals a scarce color the payoff still needs. Gated on the flag so
            // the flag-OFF path is byte-identical; no-op when nothing was deployed (rampSpentThisTurn == 0)
            // and for 0-cost fast mana (it reserves nothing).
            if (gateRampOnCastable && rampSpentThisTurn > 0)
            {
                ReserveGenericForRamp(availableColors, rampSpentThisTurn);
            }

            if (TotalMana(availableColors) < effectiveCost)
            {
                manaShort = true;
                colorShort = false;
                continue;
            }

            // Color coverage: assign online sources to cover every colored pip; a source pays up to its
            // amount in pips, all of ONE chosen color (so a multi-color source can't pay two colors).
            if (!ColorsCoverable(availableColors, pipReq, effectiveCost))
            {
                manaShort = false;
                colorShort = true;
                continue;
            }

            firstCastableTurn = currentTurn;
            return true;
        }

        return false;
    }

    // Grace turns granted past the on-curve turn: a uniform +1 ("castable on its turn, or one turn
    // late"). This matches the 17Lands manabase-evaluator convention (strict on-curve, +1 tolerance at
    // most) rather than the old 3/2/1 window, which credited a 1-2 drop as "on curve" up to THREE turns
    // late and let the deploy-friction delay of a self-cast ramp piece (debug session
    // manabase-too-optimistic) be silently forgiven — masking the ramp over-credit it was meant to
    // correct. With +1 the Avatar fixture lands at its honest headline and its weakest color reads White
    // (matching the independent Salubrious Snail baseline) instead of Blue.
    private static int GraceWindow(int turn) => 1;

    private static void PlayOneLand(
        IReadOnlyList<LibraryCard> library,
        bool[] active,
        List<int> hand,
        List<(int Mask, int OnlineTurn, int Amount)> landsOnBoard,
        List<int> scratchOnlineMasks,
        int currentTurn,
        int turn,
        (int Bit, int Count)[] pipReq)
    {
        // Colors a still-missing pip needs, judged only against lands ALREADY online (a land that
        // entered tapped last turn but is online now counts; one that enters tapped THIS turn does not
        // help this turn, so picking it to "complete a missing color" would be a lie — FINDING-1 HIGH).
        scratchOnlineMasks.Clear();
        foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
        {
            if (land.OnlineTurn <= currentTurn)
            {
                scratchOnlineMasks.Add(land.Mask);
            }
        }

        int neededColors = MissingColorMask(scratchOnlineMasks, pipReq);

        int bestUntappedNeeded = -1;
        int bestUntappedAny = -1;
        int bestTapped = -1;
        int bestTappedNeeded = -1;

        for (int h = 0; h < hand.Count; h++)
        {
            // An inactive partial source is a dead card this game — skip it as a land (FINDING-2).
            if (!active[hand[h]])
            {
                continue;
            }

            LibraryCard card = library[hand[h]];
            if (card.Kind == CardKind.UntappedLand)
            {
                if (bestUntappedAny < 0)
                {
                    bestUntappedAny = h;
                }

                // Only an untapped land can complete a missing color THIS turn; a tapped land can't.
                if (bestUntappedNeeded < 0 && (card.ColorMask & neededColors) != 0)
                {
                    bestUntappedNeeded = h;
                }
            }
            else if (card.Kind == CardKind.TappedLand)
            {
                if (bestTapped < 0)
                {
                    bestTapped = h;
                }

                // A tapped land that adds a still-missing color: useless THIS turn, but online next
                // turn — worth developing on a slack turn (M2).
                if (bestTappedNeeded < 0 && (card.ColorMask & neededColors) != 0)
                {
                    bestTappedNeeded = h;
                }
            }
        }

        // M2: on a slack turn (before the spell's cast turn) with no untapped land that adds a missing
        // color, a tapped fixer that DOES add one beats a color-useless untapped land. The tapped land
        // comes online next turn — still on or before the cast turn — so the ETB-tapped delay costs no
        // tempo here, whereas holding the fixer until the cast turn would enter tapped and miss the
        // color. On the cast turn itself (currentTurn >= turn) the old priority stands: only an untapped
        // land completes a color in time.
        //
        // Deliberate approximation: this does not look ahead to ramp. In the rare shape where the
        // color-useless untapped land would let a ramp piece deploy THIS turn (online by the cast turn),
        // developing the tapped fixer instead defers that ramp by a turn — the untapped land stays in
        // hand and is simply played next turn. A full land/ramp co-sequencing lookahead is out of scope
        // for this per-turn greedy step; the calibration decks confirm the heuristic does not over-
        // correct (only the tapland-heavy 'army now' fixture shifts band, in the correct direction).
        int pick;
        if (currentTurn < turn && bestUntappedNeeded < 0 && bestTappedNeeded >= 0)
        {
            pick = bestTappedNeeded;
        }
        else
        {
            pick = bestUntappedNeeded >= 0 ? bestUntappedNeeded
                : bestUntappedAny >= 0 ? bestUntappedAny
                : bestTapped;
        }

        if (pick < 0)
        {
            return; // no land to play this turn
        }

        LibraryCard played = library[hand[pick]];

        // Untapped: online this turn. Tapped: online next turn — contributes nothing until then.
        int onlineTurn = played.Kind == CardKind.TappedLand ? currentTurn + 1 : currentTurn;
        landsOnBoard.Add((played.ColorMask, onlineTurn, played.ManaAmount));
        hand.RemoveAt(pick);
    }

    // Deploys at most one affordable ramp piece this turn. Returns the deploy cost actually spent (0 if
    // nothing was deployed) so the caller can charge that mana against THIS turn's payoff (deploy
    // friction). 0-cost fast mana returns 0 — genuinely free this turn.
    private static int TryDeployRamp(
        IReadOnlyList<LibraryCard> library,
        bool[] active,
        List<int> hand,
        List<(int Mask, int Cost, int OnlineTurn, int Amount)> rampOnBoard,
        int availableNow,
        int currentTurn,
        List<(int Mask, int Amount)>? onlineForRamp)
    {
        // Prefer the cheapest affordable ramp piece in hand (gets online soonest / frees mana).
        int bestHandIdx = -1;
        int bestCost = int.MaxValue;
        for (int h = 0; h < hand.Count; h++)
        {
            // An inactive partial ramp source is dead this game — skip it (FINDING-2).
            if (!active[hand[h]])
            {
                continue;
            }

            LibraryCard card = library[hand[h]];
            if (card.Kind != CardKind.Ramp)
            {
                continue;
            }

            // P4 gated-ramp: a ramp piece is castable only when its DEPLOY COST is affordable AND
            // (when the gate is on) the board's online sources can also pay its OWN COLORED cost. An
            // un-castable-by-color ramp is NOT credited — it mirrors 17Lands (credit a ramp source only
            // once the ramp itself was castable). RampPips is null when the gate is off → no color test.
            if (card.DeployCost > availableNow || card.DeployCost >= bestCost)
            {
                continue;
            }

            if (onlineForRamp is not null && card.RampPips is { Length: > 0 }
                && !ColorsCoverable(onlineForRamp, card.RampPips, card.DeployCost))
            {
                continue; // colored cost of the ramp itself not yet payable this turn
            }

            bestCost = card.DeployCost;
            bestHandIdx = h;
        }

        if (bestHandIdx < 0)
        {
            return 0;
        }

        LibraryCard ramp = library[hand[bestHandIdx]];
        // 0-cost fast mana is online the same turn; everything else next turn.
        int onlineTurn = ramp.DeployCost == 0 ? currentTurn : currentTurn + 1;
        rampOnBoard.Add((ramp.ColorMask, ramp.DeployCost, onlineTurn, ramp.ManaAmount));
        hand.RemoveAt(bestHandIdx);
        return ramp.DeployCost;
    }

    // DEPLOY-FRICTION reserve: subtract `cost` generic mana from this turn's online sources to model the
    // mana spent playing a ramp piece. Tap the LEAST color-flexible sources first (fewest distinct
    // colors, e.g. a mono/colorless land before a dual), so paying generic for the rock never strips a
    // scarce color the payoff still needs. Sources are reduced in place (amount drained, dropped at 0).
    private static void ReserveGenericForRamp(List<(int Mask, int Amount)> sources, int cost)
    {
        while (cost > 0)
        {
            // Pick the live source with the FEWEST colors (ties: lowest capacity), so we spend the most
            // generic-only / least flexible mana first and keep flexible duals for colored pips.
            int pick = -1;
            int pickColors = int.MaxValue;
            int pickAmount = int.MaxValue;
            for (int s = 0; s < sources.Count; s++)
            {
                if (sources[s].Amount <= 0)
                {
                    continue;
                }

                int colors = PopCount(sources[s].Mask);
                if (colors < pickColors || (colors == pickColors && sources[s].Amount < pickAmount))
                {
                    pickColors = colors;
                    pickAmount = sources[s].Amount;
                    pick = s;
                }
            }

            if (pick < 0)
            {
                return; // no capacity left to reserve (shouldn't happen: cost <= availableNow)
            }

            (int Mask, int Amount) src = sources[pick];
            int take = Math.Min(cost, src.Amount);
            sources[pick] = (src.Mask, src.Amount - take);
            cost -= take;
        }

        // Drop fully-drained sources so neither the unit fast-path (which counts list entries against
        // effectiveCost) nor the MQ-02 DFS sees a zero-capacity ghost source.
        sources.RemoveAll(s => s.Amount <= 0);
    }

    // Colors still missing from the colored requirement given what the board can already tap.
    private static int MissingColorMask(List<int> landMasks, (int Bit, int Count)[] pipReq)
    {
        int have = 0;
        foreach (int m in landMasks)
        {
            have |= m;
        }

        int missing = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            if ((have & p.Bit) == 0)
            {
                missing |= p.Bit;
            }
        }

        return missing;
    }

    // Total mana online this turn (MQ-02): each source contributes its Amount, not 1.
    private static int OnlineMana(
        List<(int Mask, int OnlineTurn, int Amount)> lands,
        List<(int Mask, int Cost, int OnlineTurn, int Amount)> ramp,
        int currentTurn)
    {
        int mana = 0;
        foreach ((int Mask, int OnlineTurn, int Amount) l in lands)
        {
            if (l.OnlineTurn <= currentTurn)
            {
                mana += l.Amount;
            }
        }

        foreach ((int Mask, int Cost, int OnlineTurn, int Amount) r in ramp)
        {
            if (r.OnlineTurn <= currentTurn)
            {
                mana += r.Amount;
            }
        }

        return mana;
    }

    /// <summary>
    /// TAP-02 (color-matched): true when at least one online turn-1 source can produce a color the
    /// spell needs. Colorless spells (no colored pips) accept any online source. A 1-bit observation
    /// over existing board state — no RNG draw, so determinism is preserved.
    /// </summary>
    private static bool HasColorMatchedUntappedT1(
        List<(int Mask, int OnlineTurn, int Amount)> landsOnBoard,
        List<(int Mask, int Cost, int OnlineTurn, int Amount)> rampOnBoard,
        (int Bit, int Count)[] pipReq)
    {
        int neededMask = 0;
        foreach ((int Bit, int Count) pip in pipReq)
        {
            neededMask |= pip.Bit;
        }

        bool colorless = neededMask == 0;

        foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
        {
            if (land.OnlineTurn <= 1 && (colorless || (land.Mask & neededMask) != 0))
            {
                return true;
            }
        }

        foreach ((int Mask, int Cost, int OnlineTurn, int Amount) ramp in rampOnBoard)
        {
            if (ramp.OnlineTurn <= 1 && (colorless || (ramp.Mask & neededMask) != 0))
            {
                return true;
            }
        }

        return false;
    }

    private static int TotalMana(List<(int Mask, int Amount)> sources)
    {
        int mana = 0;
        foreach ((int Mask, int Amount) s in sources)
        {
            mana += s.Amount;
        }

        return mana;
    }

    // Capacity-aware color assignment (MQ-02): can the online sources cover every colored pip, where a
    // single source pays up to its Amount in pips but ALL of ONE chosen color (locked on first use)?
    // A multi-color source can therefore never pay two DIFFERENT colored pips.
    private static bool ColorsCoverable(List<(int Mask, int Amount)> sources, (int Bit, int Count)[] pipReq, int effectiveCost)
    {
        if (pipReq.Length == 0)
        {
            return TotalMana(sources) >= effectiveCost; // colorless: pure mana count
        }

        if (TotalMana(sources) < effectiveCost)
        {
            return false;
        }

        // FLAG-OFF FAST PATH: with every source worth 1 mana there is no capacity to share, so the
        // problem is the classic one-source-per-pip matching. Run the EXACT prior greedy unchanged on
        // the flat mask list (built lands-then-ramp, same order as before) so behavior is byte-identical.
        bool hasMulti = false;
        foreach ((int Mask, int Amount) s in sources)
        {
            if (s.Amount > 1)
            {
                hasMulti = true;
                break;
            }
        }

        if (!hasMulti)
        {
            var masks = new List<int>(sources.Count);
            foreach ((int Mask, int Amount) s in sources)
            {
                masks.Add(s.Mask);
            }

            return ColorsCoverableUnit(masks, pipReq, effectiveCost);
        }

        // MQ-02 path: at least one source makes >1 mana. The greedy above is unsound here (it can waste
        // a low-capacity source on a color a high-capacity one should cover), so solve exactly. Sizes
        // are tiny (a handful of pips, a handful of online sources), so DFS with capacity + single-color
        // lock per source is cheap and correct. Total mana >= effectiveCost is already checked, so
        // covering every colored pip guarantees the generic part too.
        int totalPips = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            totalPips += p.Count;
        }

        var demands = new int[totalPips];
        int di = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            for (int k = 0; k < p.Count; k++)
            {
                demands[di++] = p.Bit;
            }
        }

        // Group identical colors together so the DFS tries lock-reuse before consuming a fresh source.
        System.Array.Sort(demands);

        int[] remaining = new int[sources.Count];
        int[] locked = new int[sources.Count];
        for (int s = 0; s < sources.Count; s++)
        {
            remaining[s] = sources[s].Amount;
        }

        return CoverPips(sources, demands, 0, remaining, locked);
    }

    // Exact backtracking: assign demand[d..] to sources, each source paying up to its remaining capacity
    // in pips of ONE locked color. Returns true iff every demand can be covered simultaneously.
    private static bool CoverPips(List<(int Mask, int Amount)> sources, int[] demands, int d, int[] remaining, int[] locked)
    {
        if (d >= demands.Length)
        {
            return true;
        }

        int color = demands[d];
        for (int s = 0; s < sources.Count; s++)
        {
            if (remaining[s] <= 0 || (sources[s].Mask & color) == 0 || (locked[s] != 0 && locked[s] != color))
            {
                continue;
            }

            int prevLocked = locked[s];
            locked[s] = color;
            remaining[s]--;

            if (CoverPips(sources, demands, d + 1, remaining, locked))
            {
                return true;
            }

            remaining[s]++;
            locked[s] = prevLocked;
        }

        return false;
    }

    // The original (pre-MQ-02) greedy matching, operating on a flat mask list with each source used at
    // most once. Preserved verbatim so the flag-off path stays byte-identical to historic behavior.
    private static bool ColorsCoverableUnit(List<int> sources, (int Bit, int Count)[] pipReq, int effectiveCost)
    {
        // Expand the pip requirement into a flat list of single-color demands, hardest-constrained
        // first (rarest color among the sources). Then greedily assign the most-restrictive source.
        int totalPips = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            totalPips += p.Count;
        }

        if (sources.Count < effectiveCost)
        {
            return false;
        }

        Span<int> demands = totalPips <= 16 ? stackalloc int[totalPips] : new int[totalPips];
        int di = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            for (int k = 0; k < p.Count; k++)
            {
                demands[di++] = p.Bit;
            }
        }

        Span<bool> used = sources.Count <= 64 ? stackalloc bool[sources.Count] : new bool[sources.Count];

        for (int d = 0; d < totalPips; d++)
        {
            int rarest = -1;
            int rarestCount = int.MaxValue;
            for (int j = d; j < totalPips; j++)
            {
                int count = 0;
                for (int s = 0; s < sources.Count; s++)
                {
                    if (!used[s] && (sources[s] & demands[j]) != 0)
                    {
                        count++;
                    }
                }

                if (count < rarestCount)
                {
                    rarestCount = count;
                    rarest = j;
                }
            }

            if (rarest < 0 || rarestCount == 0)
            {
                return false; // a pip cannot be covered
            }

            (demands[d], demands[rarest]) = (demands[rarest], demands[d]);

            int pick = -1;
            int pickColors = int.MaxValue;
            for (int s = 0; s < sources.Count; s++)
            {
                if (used[s] || (sources[s] & demands[d]) == 0)
                {
                    continue;
                }

                int colorCount = PopCount(sources[s]);
                if (colorCount < pickColors)
                {
                    pickColors = colorCount;
                    pick = s;
                }
            }

            if (pick < 0)
            {
                return false;
            }

            used[pick] = true;
        }

        // All pips covered; we already checked total mana >= effectiveCost, so the generic part is
        // satisfied by the remaining sources (generic accepts any source).
        return true;
    }

    private static int PopCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);

    /// <summary>
    /// MQ-02 test seam: drive the capacity-aware color assignment directly with domain types, so the
    /// single-color-lock payment rule can be unit-tested deterministically (no Monte-Carlo). Each
    /// source is (its colors, mana amount); a source pays up to its amount in pips of ONE chosen color.
    /// </summary>
    internal static bool ColorsCoverableForTest(
        IReadOnlyList<(IReadOnlyList<ManaColor> Colors, int Amount)> sources,
        IReadOnlyList<(ManaColor Color, int Count)> pips,
        int effectiveCost)
    {
        var src = sources.Select(s => (ColorsToMask(s.Colors), s.Amount)).ToList();
        (int Bit, int Count)[] pipReq = pips.Select(p => (ColorBit(p.Color), p.Count)).ToArray();
        return ColorsCoverable(src, pipReq, effectiveCost);
    }

    // ---- London mulligan ------------------------------------------------------------------

    // Draws 7, keeps on a land-count band; otherwise mulligans to 6 then 5, bottoming highest-cost
    // non-lands first (London: see all 7, choose what to bottom). Returns the kept hand size, with
    // the kept cards moved to the front of `shuffled`. Bands widen with average mana value so a
    // higher curve tolerates more lands.
    private static int LondonMulligan(IReadOnlyList<LibraryCard> library, int[] shuffled, bool[] active, Random rng, double avgMv, int prefix, bool isSingleton, bool colorAware, int deckColorCount)
    {
        // Acceptable land bands per mulligan depth. Upper bound widens for higher-curve decks.
        int hiCap = avgMv >= 3.0 ? 5 : 4;

        // Per-depth (keep, bottom-count, low-land, high-land). Commander grants a FREE first
        // mulligan, so singleton depth 1 still keeps 7 (bottoms 0) under the same keepable band as a
        // fresh 7; bottoming only begins at depth 2. Non-singleton is standard London (each mulligan
        // bottoms one more). Bottom-count is explicit so later depths never bottom the wrong amount.
        (int Keep, int Bottom, int Lo, int Hi)[] schedule = isSingleton
            ? new[]
            {
                (7, 0, 2, hiCap), // depth 0
                (7, 0, 2, hiCap), // depth 1 — Commander free mulligan
                (6, 1, 2, 4),     // depth 2
                (5, 2, 1, 4),     // depth 3 — forced keep
            }
            : new[]
            {
                (7, 0, 2, hiCap), // depth 0
                (6, 1, 2, 4),     // depth 1
                (5, 2, 1, 4),     // depth 2 — forced keep
            };

        int last = schedule.Length - 1;
        for (int depth = 0; depth <= last; depth++)
        {
            // Depth 0's prefix is already shuffled by the caller; each later depth reshuffles to draw
            // a genuinely fresh 7.
            if (depth > 0)
            {
                ShufflePrefix(shuffled, prefix, rng);
            }

            int lands = CountLands(library, active, shuffled, 7);
            (int keep, int bottom, int lo, int hi) = schedule[depth];

            bool forced = depth == last;

            // MQ-05: a non-forced keep also needs the opening lands to show enough distinct colors.
            // Gate is a no-op when the flag is off or this is the forced final keep — in those cases the
            // land-count band alone decides, byte-identical to pre-MQ-05. (ColorKeepSatisfied also
            // no-ops mono decks internally.)
            bool colorOk = !colorAware || forced
                || ColorKeepSatisfied(OpeningLandColorMask(library, active, shuffled, 7), lands, deckColorCount);

            if (((lands >= lo && lands <= hi) && colorOk) || forced)
            {
                // Bottom `bottom` cards: non-lands first, highest deploy/mana cost first, so we keep
                // our lands and cheapest spells (London choose-and-bottom). Free-mull depths bottom 0.
                if (bottom > 0)
                {
                    BottomCards(library, shuffled, toBottom: bottom, prefix: prefix);
                }

                return keep;
            }
        }

        return schedule[last].Keep;
    }

    // Fisher-Yates over the first `count` slots. Enough because we only inspect the opening 7 plus
    // one draw per simulated turn; shuffling the whole ~99-card library every trial is wasteful.
    private static void ShufflePrefix(int[] shuffled, int count, Random rng)
    {
        int n = shuffled.Length;
        int limit = Math.Min(count, n);
        for (int i = 0; i < limit; i++)
        {
            int j = i + rng.Next(n - i);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
    }

    private static int CountLands(IReadOnlyList<LibraryCard> library, bool[] active, int[] shuffled, int top)
    {
        int n = Math.Min(top, shuffled.Length);
        int lands = 0;
        for (int i = 0; i < n; i++)
        {
            // An inactive partial land is dead this game — don't count it toward the keep band.
            if (active[shuffled[i]] && library[shuffled[i]].IsLand)
            {
                lands++;
            }
        }

        return lands;
    }

    // MQ-05: distinct colors the deck demands across all spell pips (low 5 bits), capped at 5.
    private static int DeckColorCount(ManabaseDeck deck)
    {
        int mask = 0;
        foreach (SpellRequirement spell in deck.Spells)
        {
            foreach (KeyValuePair<ManaColor, int> pip in spell.Pips)
            {
                if (pip.Key != ManaColor.Colorless && pip.Value > 0)
                {
                    mask |= ColorBit(pip.Key);
                }
            }
        }

        return CountColors(mask);
    }

    // MQ-05: union of the color masks of the active LANDS in the opening `top` cards. Ramp is excluded
    // (it must be cast before it makes color) — mirrors the land-only basis of the count band.
    private static int OpeningLandColorMask(IReadOnlyList<LibraryCard> library, bool[] active, int[] shuffled, int top)
    {
        int n = Math.Min(top, shuffled.Length);
        int mask = 0;
        for (int i = 0; i < n; i++)
        {
            if (active[shuffled[i]] && library[shuffled[i]].IsLand)
            {
                mask |= library[shuffled[i]].ColorMask;
            }
        }

        return mask;
    }

    // Popcount over the five color bits.
    private static int CountColors(int mask) => BitOperations.PopCount((uint)(mask & 0b11111));

    // MQ-05 color gate (flag-on, non-forced case only). True ⇒ this opener's lands show enough distinct
    // colors to keep. Mono decks (deckColorCount <= 1) always pass — the gate is a no-op for them.
    // Threshold = min(deckColorCount, lands, ColorKeepCap): never demand more colors than the deck
    // plays, than the hand could physically show, or than the cap (2).
    private static bool ColorKeepSatisfied(int openingLandColorMask, int lands, int deckColorCount)
        => deckColorCount <= 1
            || CountColors(openingLandColorMask) >= Math.Min(Math.Min(deckColorCount, lands), ColorKeepCap);

    /// <summary>
    /// Test seam for the MQ-05 color-keep gate: exposes <c>ColorKeepSatisfied</c> over the colors the
    /// opening lands can tap, so the threshold logic is unit-testable without driving the Monte-Carlo
    /// loop. <paramref name="openingLandColors"/> is the UNION of colors across the kept lands.
    /// </summary>
    internal static bool ColorKeepSatisfiedForTest(IReadOnlyList<ManaColor> openingLandColors, int lands, int deckColorCount)
        => ColorKeepSatisfied(ColorsToMask(openingLandColors), lands, deckColorCount);

    // Move `toBottom` cards from the 7-card look to the bottom: non-lands first, highest cost first.
    // After this, the first (7 - toBottom) slots are the kept hand. `prefix` is the caller's shuffled
    // window (7 + turn + grace + 2, clamped to the library) — only these slots hold a genuine random
    // sample, so a bottomed card is parked at the FAR end of it, not in the unshuffled physical tail.
    private static void BottomCards(IReadOnlyList<LibraryCard> library, int[] shuffled, int toBottom, int prefix)
    {
        // Sort indices [0,7) so the BOTTOMED ones (worst keeps) sort to the end: lands are best keeps,
        // then cheap non-lands; bottom the most expensive non-lands. Stable enough for our purpose.
        int top = Math.Min(7, shuffled.Length);

        // London bottoming is only well-defined against a full 7-card look: every bottoming depth in
        // the schedule keeps `keep` and bottoms `toBottom` with keep + toBottom == 7. With a library
        // smaller than 7 (degenerate unit fixtures only — real 60/99-card decks always have ≥ 7 in the
        // library) keep + toBottom would exceed the deck, so a "bottomed" card could land back inside
        // the kept hand [0, keep). Skip bottoming entirely in that case: keep the opener as dealt.
        if (top < 7)
        {
            return;
        }

        // Never bottom more cards than the opening window holds — a tiny/empty library (e.g. a deck
        // that is only commanders, librarySize == 0) would otherwise drive keptBoundary negative and
        // index shuffled[-1]. Clamp so the loop is a no-op when there's nothing to bottom.
        toBottom = Math.Min(toBottom, top);

        // Simple selection: repeatedly find the "worst to keep" card in [0, kept+...) and swap it out
        // to the tail of the 7-window, then drop it past the kept boundary.
        int keptBoundary = top; // shrinks as we bottom cards toward the tail
        for (int b = 0; b < toBottom; b++)
        {
            int worst = 0;
            for (int i = 1; i < keptBoundary; i++)
            {
                if (WorseKeep(library[shuffled[i]], library[shuffled[worst]]))
                {
                    worst = i;
                }
            }

            keptBoundary--;
            (shuffled[worst], shuffled[keptBoundary]) = (shuffled[keptBoundary], shuffled[worst]);

            // M1: the bottomed card now sits at slot `keptBoundary`, which is exactly where the turn
            // loop's draw pointer starts (drawPtr == kept size). Left here, a mulligan would
            // deterministically redraw the very card it just bottomed on turn 1. Relocate it to the FAR
            // END of the shuffled prefix (prefix carries a +2 slot margin past the deepest draw any
            // game length reaches, so [prefix-toBottom, prefix) is never drawn) and pull the uniform-
            // random card sitting there UP into the draw zone. Parking in the shuffled prefix — not the
            // unshuffled physical tail — is what makes the replacement a real random draw instead of the
            // deterministic filler that pads the library's end. Guarded so a degenerate tiny fixture
            // (prefix == library and no never-drawn margin) falls back to in-window placement rather
            // than corrupting the kept prefix.
            int bottomSlot = prefix - 1 - b;
            if (bottomSlot > keptBoundary)
            {
                (shuffled[keptBoundary], shuffled[bottomSlot]) = (shuffled[bottomSlot], shuffled[keptBoundary]);
            }
        }
    }

    // True if `a` is a worse card to keep than `b`: prefer keeping lands; among non-lands prefer
    // cheaper (lower deploy/mana cost) ones, so the most expensive non-land is the first to bottom.
    private static bool WorseKeep(LibraryCard a, LibraryCard b)
    {
        if (a.IsLand != b.IsLand)
        {
            return !a.IsLand; // a non-land is worse to keep than a land
        }

        if (a.IsLand)
        {
            return false; // two lands: equally good to keep
        }

        // Two non-lands: the higher-cost one is worse to keep. Ramp uses DeployCost; filler is 0,
        // so filler is kept over expensive ramp (it's cheaper to "spend" later). Treat filler cost
        // as a small constant so a 0-cost rock isn't preferred-bottomed over filler.
        int costA = a.Kind == CardKind.Filler ? 3 : a.DeployCost;
        int costB = b.Kind == CardKind.Filler ? 3 : b.DeployCost;
        return costA > costB;
    }

    // ---- helpers --------------------------------------------------------------------------

    private static string DeriveLimitingFactor(bool colorless, int manaShort, int colorShort, SpellRequirement spell)
    {
        if (colorless)
        {
            return "mana";
        }

        if (manaShort == 0 && colorShort == 0)
        {
            return "mana"; // (near-)always castable; nominal
        }

        if (manaShort > colorShort * 2)
        {
            return "mana";
        }

        if (colorShort > manaShort * 2)
        {
            return "color:" + MostMissingColor(spell);
        }

        return "both";
    }

    private static string MostMissingColor(SpellRequirement spell)
    {
        // The color with the most pips is the likeliest culprit when colors are the bottleneck.
        ManaColor worst = ManaColor.Colorless;
        int max = 0;
        foreach (KeyValuePair<ManaColor, int> pip in spell.Pips)
        {
            if (pip.Key != ManaColor.Colorless && pip.Value > max)
            {
                max = pip.Value;
                worst = pip.Key;
            }
        }

        return worst.ToString();
    }

    // Deterministic, stable across runs and platforms (NOT string.GetHashCode, which is randomized
    // per-process). FNV-1a over the UTF-16 code units.
    private static int StableSeed(string name)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char c in name)
            {
                hash ^= c;
                hash *= prime;
            }

            return (int)hash;
        }
    }
}
