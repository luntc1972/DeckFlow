using System.Collections.Generic;
using System.Linq;

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
        public LibraryCard(CardKind kind, int colorMask, int deployCost, bool isLand, double activationWeight = 1.0, int manaAmount = 1)
        {
            Kind = kind;
            ColorMask = colorMask;
            DeployCost = deployCost;
            IsLand = isLand;
            ActivationWeight = activationWeight;
            ManaAmount = manaAmount;
        }

        public CardKind Kind { get; }

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
    public static CardCastability Simulate(
        ManabaseDeck deck,
        int librarySize,
        SpellRequirement spell,
        int effectiveTurn,
        int genericReduction,
        int trials = DefaultTrials,
        bool useManaQuantity = false)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(spell);

        IReadOnlyList<LibraryCard> library = BuildLibrary(deck, librarySize, useManaQuantity);

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

        // We only ever inspect the opening 7 plus one draw per turn, so shuffling the first
        // (7 + turn) slots is sufficient and far cheaper than a full Fisher-Yates of ~99 cards.
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
            // Tiny decks (some unit fixtures) can be smaller than a 7-card opener — clamp the hand.
            int handCount = Math.Min(library.Count, LondonMulligan(library, shuffled, active, rng, deck.AverageManaValue, prefix, deck.IsSingleton));

            bool success = SimulateGame(
                library, shuffled, active, handCount, turn, effectiveCost, pipReq, availableColors, onlineLandMasks,
                out bool manaShort, out bool colorShort, out int firstCastableTurn);

            // Delay this trial: how many turns LATE the spell first became castable, floored at 0
            // (a spell never tests as castable before its on-curve turn, so this is already >= 0).
            delaySum += Math.Max(0, firstCastableTurn - turn);

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
        };
    }

    // ---- library construction -------------------------------------------------------------

    private static IReadOnlyList<LibraryCard> BuildLibrary(ManabaseDeck deck, int librarySize, bool useManaQuantity)
    {
        var cards = new List<LibraryCard>(librarySize);

        // Map non-land sources (rocks/dorks/granted) to their deploy cost via the matching
        // IsManaSource spell (a rock/dork is BOTH a non-land source AND a flagged spell). Granted
        // sources ("X (granted)") have no spell row — treat them as turn-2 conditional ramp.
        var rampCostByName = deck.Spells
            .Where(s => s.IsManaSource)
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ManaValue, StringComparer.Ordinal);

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
        AddSourcesAsCards(deck, cards, rampCostByName, useManaQuantity);

        // Pad/truncate to the real library size with filler so draw probabilities match the deck.
        int sourceCards = cards.Count;
        for (int i = sourceCards; i < librarySize; i++)
        {
            cards.Add(new LibraryCard(CardKind.Filler, 0, 0, false));
        }

        if (cards.Count > librarySize)
        {
            cards.RemoveRange(librarySize, cards.Count - librarySize);
        }

        return cards;
    }

    private static void AddSourcesAsCards(
        ManabaseDeck deck,
        List<LibraryCard> cards,
        IReadOnlyDictionary<string, int> rampCostByName,
        bool useManaQuantity)
    {
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

            int mask = ColorsToMask(source.Produces);

            // MQ-02: how much mana the source makes per activation. Off → 1 (byte-identical to the
            // pre-MQ-02 sim). Conditional/granted sources always stay 1 (the Bernoulli roll gates a
            // single speculative unit).
            int amount = useManaQuantity && !source.IsConditional ? Math.Max(1, source.ManaAmount) : 1;

            if (source.IsLand)
            {
                CardKind kind = source.EntersUntapped ? CardKind.UntappedLand : CardKind.TappedLand;
                // Lands are never conditional; a discounted basic-fetch is still a full card you draw.
                AddWeighted(cards, kind, mask, deployCost: 0, source.Weight, source.IsConditional, amount);
                continue;
            }

            // Non-land source = ramp. Deploy cost from the matching mana-source spell; granted/unknown
            // sources default to turn-2 (a typical mana rock / dork comes online around then).
            string baseName = source.Name.EndsWith(" (granted)", StringComparison.Ordinal)
                ? source.Name[..^" (granted)".Length]
                : source.Name;
            int deployCost = rampCostByName.TryGetValue(baseName, out int mv) ? mv : 2;
            AddWeighted(cards, CardKind.Ramp, mask, deployCost, source.Weight, source.IsConditional, amount);
        }
    }

    private static void AddWeighted(
        List<LibraryCard> cards,
        CardKind kind,
        int mask,
        int deployCost,
        double weight,
        bool isConditional,
        int amount = 1)
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
            cards.Add(new LibraryCard(kind, mask, deployCost, isLand, manaAmount: amount));
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
            cards.Add(new LibraryCard(kind, mask, deployCost, isLand));
        }

        double frac = weight - whole;
        if (frac > 1e-9)
        {
            cards.Add(new LibraryCard(kind, mask, deployCost, isLand, activationWeight: frac));
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

    // Plays out turns 1..(turn+grace) on the play. Returns true if the spell becomes castable on the
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
        out bool manaShort,
        out bool colorShort,
        out int firstCastableTurn)
    {
        manaShort = false;
        colorShort = false;

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
            // Draw for the turn (skip turn 1 on the play).
            if (currentTurn > 1 && drawPtr < library.Count)
            {
                hand.Add(shuffled[drawPtr++]);
            }

            // Play one land this turn: prefer an untapped land that adds a still-missing color THIS turn,
            // then any untapped land, then a tapped land (it won't help this turn but builds the board).
            // A tapped land played this turn enters with OnlineTurn = currentTurn + 1, so it contributes
            // nothing until next turn (FINDING-1 HIGH).
            PlayOneLand(library, active, hand, landsOnBoard, onlineLandMasks, currentTurn, pipReq);

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
            int availableNow = OnlineMana(landsOnBoard, rampOnBoard, currentTurn);
            if (availableNow < effectiveCost)
            {
                TryDeployRamp(library, active, hand, rampOnBoard, availableNow, currentTurn);
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

    // Grace turns granted past the on-curve turn, tracking Snail's delay tolerance: wider for cheap
    // spells (a 1-drop is fine a turn or two late), tighter for the top of the curve.
    private static int GraceWindow(int turn) => turn switch
    {
        <= 2 => 3,
        <= 5 => 2,
        _ => 1,
    };

    private static void PlayOneLand(
        IReadOnlyList<LibraryCard> library,
        bool[] active,
        List<int> hand,
        List<(int Mask, int OnlineTurn, int Amount)> landsOnBoard,
        List<int> scratchOnlineMasks,
        int currentTurn,
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
            else if (card.Kind == CardKind.TappedLand && bestTapped < 0)
            {
                bestTapped = h;
            }
        }

        int pick = bestUntappedNeeded >= 0 ? bestUntappedNeeded
            : bestUntappedAny >= 0 ? bestUntappedAny
            : bestTapped;
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

    private static void TryDeployRamp(
        IReadOnlyList<LibraryCard> library,
        bool[] active,
        List<int> hand,
        List<(int Mask, int Cost, int OnlineTurn, int Amount)> rampOnBoard,
        int availableNow,
        int currentTurn)
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

            if (card.DeployCost <= availableNow && card.DeployCost < bestCost)
            {
                bestCost = card.DeployCost;
                bestHandIdx = h;
            }
        }

        if (bestHandIdx < 0)
        {
            return;
        }

        LibraryCard ramp = library[hand[bestHandIdx]];
        // 0-cost fast mana is online the same turn; everything else next turn.
        int onlineTurn = ramp.DeployCost == 0 ? currentTurn : currentTurn + 1;
        rampOnBoard.Add((ramp.ColorMask, ramp.DeployCost, onlineTurn, ramp.ManaAmount));
        hand.RemoveAt(bestHandIdx);
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
    // A multi-color source can therefore never pay two DIFFERENT colored pips. With every Amount == 1
    // this reduces EXACTLY to the prior one-source-per-pip behavior (the flag-off byte-identical path).
    private static bool ColorsCoverable(List<(int Mask, int Amount)> sources, (int Bit, int Count)[] pipReq, int effectiveCost)
    {
        if (pipReq.Length == 0)
        {
            return TotalMana(sources) >= effectiveCost; // colorless: pure mana count
        }

        int totalPips = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            totalPips += p.Count;
        }

        if (TotalMana(sources) < effectiveCost)
        {
            return false;
        }

        // Build demand list (one entry per required pip).
        Span<int> demands = totalPips <= 16 ? stackalloc int[totalPips] : new int[totalPips];
        int di = 0;
        foreach ((int Bit, int Count) p in pipReq)
        {
            for (int k = 0; k < p.Count; k++)
            {
                demands[di++] = p.Bit;
            }
        }

        // Per-source remaining mana capacity and the single color it is locked to (0 = not yet used).
        int n = sources.Count;
        Span<int> remaining = n <= 64 ? stackalloc int[n] : new int[n];
        Span<int> locked = n <= 64 ? stackalloc int[n] : new int[n];
        for (int s = 0; s < n; s++)
        {
            remaining[s] = sources[s].Amount;
            locked[s] = 0;
        }

        // Order demands by current rarity (fewest sources able to serve them) and assign greedily.
        for (int d = 0; d < totalPips; d++)
        {
            int rarest = -1;
            int rarestCount = int.MaxValue;
            for (int j = d; j < totalPips; j++)
            {
                int c = demands[j];
                int count = 0;
                for (int s = 0; s < n; s++)
                {
                    if (remaining[s] > 0 && (sources[s].Mask & c) != 0 && (locked[s] == 0 || locked[s] == c))
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
            int color = demands[d];

            // Prefer reusing a source ALREADY locked to this color (its capacity is committed anyway);
            // otherwise take the most-constrained fresh source (fewest colors) able to produce it.
            int pick = -1;
            int pickColors = int.MaxValue;
            for (int s = 0; s < n; s++)
            {
                if (remaining[s] <= 0 || (sources[s].Mask & color) == 0 || (locked[s] != 0 && locked[s] != color))
                {
                    continue;
                }

                if (locked[s] == color)
                {
                    pick = s; // free reuse — its mana is already dedicated to this color
                    break;
                }

                int colorCount = PopCount(sources[s].Mask);
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

            locked[pick] = color;
            remaining[pick]--;
        }

        // All pips covered; total mana >= effectiveCost was checked, so leftover capacity (any color or
        // colorless) covers the generic part.
        return true;
    }

    private static int PopCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);

    // ---- London mulligan ------------------------------------------------------------------

    // Draws 7, keeps on a land-count band; otherwise mulligans to 6 then 5, bottoming highest-cost
    // non-lands first (London: see all 7, choose what to bottom). Returns the kept hand size, with
    // the kept cards moved to the front of `shuffled`. Bands widen with average mana value so a
    // higher curve tolerates more lands.
    private static int LondonMulligan(IReadOnlyList<LibraryCard> library, int[] shuffled, bool[] active, Random rng, double avgMv, int prefix, bool isSingleton)
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

            if ((lands >= lo && lands <= hi) || depth == last)
            {
                // Bottom `bottom` cards: non-lands first, highest deploy/mana cost first, so we keep
                // our lands and cheapest spells (London choose-and-bottom). Free-mull depths bottom 0.
                if (bottom > 0)
                {
                    BottomCards(library, shuffled, toBottom: bottom);
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

    // Move `toBottom` cards from the 7-card look to the bottom: non-lands first, highest cost first.
    // After this, the first (7 - toBottom) slots are the kept hand.
    private static void BottomCards(IReadOnlyList<LibraryCard> library, int[] shuffled, int toBottom)
    {
        // Sort indices [0,7) so the BOTTOMED ones (worst keeps) sort to the end: lands are best keeps,
        // then cheap non-lands; bottom the most expensive non-lands. Stable enough for our purpose.
        int top = Math.Min(7, shuffled.Length);

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
