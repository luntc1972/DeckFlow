using System.Text.RegularExpressions;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Turns a list of <see cref="CardFact"/> (Scryfall-shaped data) into a
/// <see cref="ManabaseDeck"/> ready for <see cref="ManabaseAnalyzer"/>. Applies Karsten's
/// source-counting rules: full-weight lands, mana dorks at 0.5, rocks at 0.75, basic
/// fetches in 3+ color decks at ~0.67, and land/spell MDFC backs at 0.8 (mythic 1.0).
/// </summary>
public static class ManabaseClassifier
{
    // Matches an always-on static generic reducer: an optional type scope (instant/sorcery/
    // creature/artifact words) immediately before "spells you cast cost {N} less". The "you cast"
    // anchor excludes opponent-only and activated-ability discounts. Oracle text is lower-cased.
    private static readonly Regex StaticReducerRegex = new(
        @"(?<scope>(?:[a-z]+ )*?)spells you cast cost \{(?<amt>\d+)\} less",
        RegexOptions.Compiled);

    // Self-cost detection (DetectSelfCost). Oracle text is lower-cased before matching.
    // Evoke / suspend may carry a braced mana cost (Shriekmaw "evoke {1}{B}", Crashing Footfalls
    // "suspend 1—{g}") or a non-mana cost (Grief "evoke—exile a black card"); capture the braced
    // cost when present, else treat the alternative as free. Dash variants -/–/— are tolerated.
    private static readonly Regex EvokeCostRegex = new(
        @"evoke[\s—–-]*((?:\{[^}]+\})+)", RegexOptions.Compiled);

    private static readonly Regex SuspendCostRegex = new(
        @"suspend\s+\d+[\s—–-]*((?:\{[^}]+\})+)", RegexOptions.Compiled);

    // "This spell costs {N} less to cast for each <thing>" — a board-scaling SELF reduction
    // (Blasphemous Act). Self-anchored on "this spell" so it never fires on a card that discounts
    // OTHER spells with a "for each" rider. Distinct from the deck-wide StaticReducerRegex.
    private static readonly Regex ScalingSelfReducerRegex = new(
        @"this spell costs \{\d+\} less to cast for each", RegexOptions.Compiled);

    /// <summary>Build a <see cref="ManabaseDeck"/> from classified card facts.</summary>
    /// <param name="cards">All cards in the deck (including any commanders, flagged).</param>
    /// <param name="isSingleton">True for Commander/singleton; false for 60-card constructed.</param>
    public static ManabaseDeck Classify(IReadOnlyList<CardFact> cards, bool isSingleton = true)
    {
        ArgumentNullException.ThrowIfNull(cards);

        int deckColorCount = CountDeckColors(cards);
        (Dictionary<string, HashSet<ManaColor>> fetchTypeColors, HashSet<ManaColor> fetchBasicColors) =
            BuildFetchableColors(cards);

        var sources = new List<ManaSource>();
        var spells = new List<SpellRequirement>();
        var reducers = new List<CostReducer>();
        var granters = new List<GranterScope>();
        var suggestions = new List<CostSuggestion>();
        var suggestedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unsupported = new List<UnsupportedInteraction>();
        var unsupportedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalCards = 0;
        int commanderCount = 0;
        double mvSum = 0;
        int nonlandCount = 0;
        int rampUnderThree = 0;
        int mdfcCommon = 0;
        int mdfcMythic = 0;
        int fastMana = 0;

        foreach (CardFact card in cards)
        {
            totalCards += card.Quantity;
            if (card.IsCommander)
            {
                commanderCount += card.Quantity;
            }

            bool frontIsLand = IsLandType(card.TypeLine);
            if (frontIsLand)
            {
                AddLandCopies(sources, card, deckColorCount, fetchTypeColors, fetchBasicColors);
                continue;
            }

            // Spell front: contributes to the curve.
            if (!card.IsCommander)
            {
                mvSum += card.ManaValue * card.Quantity;
                nonlandCount += card.Quantity;
            }

            ParsedManaCost cost = ManaCostParser.Parse(card.ManaCost);
            AddSpellRequirement(spells, card, cost);

            // MQ-04: disclose what the analysis cannot fully model rather than silently absorbing it.
            // X/variable spells are dropped from castability entirely; hybrid/Phyrexian pips are
            // flexible so they carry no hard color requirement (Karsten-correct, but then the color
            // need is approximated). One entry per card, X taking priority over hybrid.
            string? unsupportedReason = cost.HasVariableCost
                ? "Variable (X) cost — castability not simulated"
                : (card.ManaCost?.Contains('/', StringComparison.Ordinal) ?? false)
                    ? "Hybrid/Phyrexian pips — color requirement approximated"
                    : null;
            if (unsupportedReason is not null && unsupportedNames.Add(card.Name))
            {
                unsupported.Add(new UnsupportedInteraction { Name = card.Name, Reason = unsupportedReason });
            }

            if (card.ManaValue <= 2 && IsRampOrDraw(card))
            {
                rampUnderThree += card.Quantity;
            }

            // Tally land-count formula adjustments (MDFC spell-backs, 0-cost fast mana).
            if (card.HasLandFace)
            {
                if (IsMythic(card))
                {
                    mdfcMythic += card.Quantity;
                }
                else
                {
                    mdfcCommon += card.Quantity;
                }
            }
            else if (card.ManaValue == 0 && IsType(card.TypeLine, "Artifact") && ProducesMana(card))
            {
                fastMana += card.Quantity;
            }

            AddPartialSources(sources, card);

            // Detect always-on static cost reducers and mana-ability granters (one per copy).
            CostReducer? reducer = DetectCostReducer(card);
            if (reducer is not null)
            {
                for (int i = 0; i < card.Quantity; i++)
                {
                    reducers.Add(reducer);
                }
            }

            GranterScope? grant = DetectGranter(card);
            if (grant is not null)
            {
                for (int i = 0; i < card.Quantity; i++)
                {
                    granters.Add(grant.Value);
                }
            }

            // Alt/reduced-cost suggestion (free/pitch, board-scaling self-reducer, evoke/suspend).
            // One per distinct card name — these only pre-populate the override box, they don't
            // alter the analysis unless the user applies an override.
            if (suggestedNames.Add(card.Name) && DetectSelfCost(card) is (string effCost, string reason))
            {
                suggestions.Add(new CostSuggestion
                {
                    Name = card.Name,
                    EffectiveCost = effCost,
                    Reason = reason,
                });
            }
        }

        // Mana-ability granters add conditional weighted any-color sources for the creatures they
        // enable (a second pass: it needs the full creature list and the already-built sources).
        AddGrantedSources(sources, cards, granters, deckColorCount);

        double avgMv = nonlandCount > 0 ? mvSum / nonlandCount : 0;

        return new ManabaseDeck
        {
            TotalCards = totalCards,
            CommanderCount = commanderCount,
            Sources = sources,
            Spells = spells,
            AverageManaValue = Math.Round(avgMv, 2),
            RampAndDrawUnderThree = rampUnderThree,
            MdfcCommon = mdfcCommon,
            MdfcMythic = mdfcMythic,
            FastMana = fastMana,
            IsSingleton = isSingleton,
            CostReduction = reducers,
            CostSuggestions = suggestions,
            UnsupportedInteractions = unsupported,
        };
    }

    private static int CountDeckColors(IReadOnlyList<CardFact> cards)
    {
        // Deck color count = colors the deck actually demands (hard pips in card costs incl.
        // the commander). Off-color fixers (Signet, Birds, Treasures) must NOT inflate it,
        // or a 2-color deck reads as 5-color and the fetch-weighting heuristic over-penalizes.
        var colors = new HashSet<ManaColor>();
        foreach (CardFact card in cards)
        {
            foreach (KeyValuePair<ManaColor, int> pip in ManaCostParser.Parse(card.ManaCost).Pips)
            {
                if (pip.Value > 0 && pip.Key != ManaColor.Colorless)
                {
                    colors.Add(pip.Key);
                }
            }
        }

        return colors.Count;
    }

    private static void AddLandCopies(List<ManaSource> sources, CardFact card, int deckColorCount,
        Dictionary<string, HashSet<ManaColor>> fetchTypeColors, HashSet<ManaColor> fetchBasicColors)
    {
        IReadOnlyList<ManaColor> produces = MapColors(card.ProducedMana);
        if (produces.Count == 0)
        {
            // Fetchlands report empty produced_mana on Scryfall (they tap for no mana directly),
            // but they effectively supply the colors of the lands they can fetch. Derive those
            // from the basic land types named in the fetch's oracle text so a Flooded Strand
            // counts as a white AND blue source, not colorless.
            produces = FetchLandColors(card, fetchTypeColors, fetchBasicColors);
        }

        bool basicFetch = IsBasicFetch(card);
        // A choice-fetch in a 3+ color deck can only grab one color at a time.
        double weight = basicFetch && deckColorCount >= 3 ? 0.67 : 1.0;
        bool untapped = !EntersTapped(card);

        for (int i = 0; i < card.Quantity; i++)
        {
            sources.Add(new ManaSource
            {
                Name = card.Name,
                Produces = produces,
                Weight = weight,
                EntersUntapped = untapped,
                IsCommander = card.IsCommander,
            });
        }
    }

    private static void AddSpellRequirement(List<SpellRequirement> spells, CardFact card, ParsedManaCost cost)
    {
        // X/Y/Z spells: printed mana value is not the real cast turn, so an on-curve source
        // check at that turn is meaningless. Skip them rather than strain colors at a bogus turn.
        if (cost.HasVariableCost)
        {
            return;
        }

        // Colorless fixed-cost payoffs (Ugin, Wurmcoil) now become SpellRequirements too (empty
        // Pips); they show in the castability rows with a mana-only cast chance. Only mana
        // rocks/dorks are flagged IsManaSource so they are hidden from the rows but kept in pools.
        spells.Add(new SpellRequirement
        {
            Name = card.Name,
            // True printed mana value (0-cost cards stay 0 for display). The min-1 cast-turn
            // floor is enforced downstream by EffectiveTurn and the simulator, not here.
            ManaValue = Math.Max(0, (int)Math.Round(card.ManaValue)),
            Pips = cost.Pips,
            IsGold = cost.DistinctColors >= 2,
            IsManaSource = IsRockOrDork(card),
            Kinds = ClassifyKinds(card.TypeLine),
            IsCommander = card.IsCommander,
        });
    }

    // The exact rock/dork test AddPartialSources uses, factored out so the row-exclusion set ==
    // the partial-source set. NOT bare ProducesMana (deliberately broad — would hide hybrid
    // payoff cards). MDFC land-backs are NOT rocks/dorks; they are real spells with a land face.
    private static bool IsRockOrDork(CardFact card)
    {
        if (card.HasLandFace || card.ProducedMana.Count == 0 || !ProducesMana(card))
        {
            return false;
        }

        return IsType(card.TypeLine, "Creature") || IsType(card.TypeLine, "Artifact");
    }

    private static SpellKinds ClassifyKinds(string typeLine)
    {
        string front = typeLine.Split("//")[0];
        SpellKinds kinds = SpellKinds.None;
        if (IsType(front, "Creature"))
        {
            kinds |= SpellKinds.Creature;
        }

        if (IsType(front, "Artifact"))
        {
            kinds |= SpellKinds.Artifact;
        }

        if (IsType(front, "Instant"))
        {
            kinds |= SpellKinds.Instant;
        }

        if (IsType(front, "Sorcery"))
        {
            kinds |= SpellKinds.Sorcery;
        }

        if (kinds == SpellKinds.None)
        {
            kinds = SpellKinds.Other;
        }

        return kinds;
    }

    private static void AddPartialSources(List<ManaSource> sources, CardFact card)
    {
        // Land/spell MDFC back face: count as a partial colored source (0.8 / mythic 1.0).
        if (card.HasLandFace)
        {
            double mdfcWeight = IsMythic(card) ? 1.0 : 0.8;
            AddWeighted(sources, card, mdfcWeight);
            return;
        }

        if (!IsRockOrDork(card))
        {
            return;
        }

        // Mana dork (creature) ≈ 0.5; mana rock (artifact) ≈ 0.75.
        if (IsType(card.TypeLine, "Creature"))
        {
            AddWeighted(sources, card, 0.5);
        }
        else if (IsType(card.TypeLine, "Artifact"))
        {
            AddWeighted(sources, card, 0.75);
        }
    }

    private static void AddWeighted(List<ManaSource> sources, CardFact card, double weight)
    {
        IReadOnlyList<ManaColor> produces = MapColors(card.ProducedMana);
        if (produces.Count == 0)
        {
            return;
        }

        for (int i = 0; i < card.Quantity; i++)
        {
            sources.Add(new ManaSource { Name = card.Name, Produces = produces, Weight = weight, IsLand = false, IsCommander = card.IsCommander });
        }
    }

    private static IReadOnlyList<ManaColor> MapColors(IReadOnlyList<string> produced)
    {
        var colors = new List<ManaColor>();
        foreach (string letter in produced)
        {
            ManaColor? c = ManaCostParser.MapSymbol(letter.ToUpperInvariant());
            if (c is not null && !colors.Contains(c.Value))
            {
                colors.Add(c.Value);
            }
        }

        return colors;
    }

    private static bool IsLandType(string typeLine)
    {
        // Use the front face only (before "//") so MDFC spell-fronts aren't treated as lands.
        string front = typeLine.Split("//")[0];
        return IsType(front, "Land");
    }

    private static bool IsType(string typeLine, string type) =>
        typeLine.Contains(type, StringComparison.OrdinalIgnoreCase);

    private static bool ProducesMana(CardFact card) =>
        (card.OracleText?.Contains("Add ", StringComparison.OrdinalIgnoreCase) ?? false)
        || card.ProducedMana.Count > 0;

    private static bool IsBasicFetch(CardFact card)
    {
        string? text = card.OracleText;
        return text is not null
            && text.Contains("Search your library for a", StringComparison.OrdinalIgnoreCase)
            && text.Contains("basic land", StringComparison.OrdinalIgnoreCase);
    }

    // Basic land type -> the color it taps for, used to color fetchlands whose produced_mana is empty.
    private static readonly (string Type, ManaColor Color)[] BasicLandColors =
    {
        ("Plains", ManaColor.White),
        ("Island", ManaColor.Blue),
        ("Swamp", ManaColor.Black),
        ("Mountain", ManaColor.Red),
        ("Forest", ManaColor.Green),
    };

    // Pre-pass: map each basic land type to the colors that every NON-fetch land in the deck bearing
    // that type can produce. A typed fetch ("Plains or Island card") can grab not just basics but any
    // land with a matching type — a Plains-typed shock (Hallowed Fountain → W,U) or a triome
    // (Raffine's Tower → W,U,B) — so the fetch's real colors are the union over its fetched types.
    private static (Dictionary<string, HashSet<ManaColor>> TypeColors, HashSet<ManaColor> BasicColors)
        BuildFetchableColors(IReadOnlyList<CardFact> cards)
    {
        var typeColors = new Dictionary<string, HashSet<ManaColor>>(StringComparer.OrdinalIgnoreCase);
        foreach ((string type, ManaColor _) in BasicLandColors)
        {
            typeColors[type] = new HashSet<ManaColor>();
        }

        var basicColors = new HashSet<ManaColor>();
        foreach (CardFact card in cards)
        {
            if (!IsLandType(card.TypeLine))
            {
                continue;
            }

            IReadOnlyList<ManaColor> colors = MapColors(card.ProducedMana);
            if (colors.Count == 0)
            {
                continue; // a fetch (empty produced_mana) or colorless utility land seeds no color
            }

            string front = card.TypeLine.Split("//")[0];
            bool isBasic = front.Contains("Basic", StringComparison.OrdinalIgnoreCase);
            foreach ((string type, ManaColor color) in BasicLandColors)
            {
                if (!front.Contains(type, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (ManaColor c in colors)
                {
                    typeColors[type].Add(c);
                }

                if (isBasic)
                {
                    basicColors.Add(color);
                }
            }
        }

        return (typeColors, basicColors);
    }

    // Colors a fetchland can provide. Typed fetches (Flooded Strand: "Plains or Island card") return
    // the union of every deck land sharing a named type (basics, duals, triomes) plus the named
    // basics' own colors; a generic "basic land" fetch (Prismatic Vista, Evolving Wilds) grabs any
    // basic, so it counts for the basic colors actually in the deck (or all five if none parsed).
    private static IReadOnlyList<ManaColor> FetchLandColors(CardFact card,
        Dictionary<string, HashSet<ManaColor>> typeColors, HashSet<ManaColor> basicColors)
    {
        string? text = card.OracleText;
        if (text is null || !text.Contains("Search your library", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<ManaColor>();
        }

        var colors = new List<ManaColor>();
        bool namedAny = false;
        foreach ((string type, ManaColor color) in BasicLandColors)
        {
            if (!text.Contains(type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            namedAny = true;
            // Only colors actually reachable: the union of deck lands bearing this type (basics,
            // duals, triomes). If the deck runs no land of this subtype, the fetch genuinely can't
            // get that color — do NOT credit the named basic's color speculatively (Codex MEDIUM).
            foreach (ManaColor c in typeColors[type])
            {
                if (!colors.Contains(c))
                {
                    colors.Add(c);
                }
            }
        }

        if (!namedAny && text.Contains("basic land", StringComparison.OrdinalIgnoreCase))
        {
            // Generic basic fetch (Prismatic Vista, Evolving Wilds): only the basic colors the deck
            // actually runs. A deck with no basics can't fetch anything — empty, not all five.
            return basicColors.ToList();
        }

        return colors;
    }

    private static bool EntersTapped(CardFact card) =>
        card.OracleText?.Contains("enters the battlefield tapped", StringComparison.OrdinalIgnoreCase)
        ?? false;

    private static bool IsRampOrDraw(CardFact card)
    {
        string text = card.OracleText ?? string.Empty;
        bool ramp = (text.Contains("Search your library for", StringComparison.OrdinalIgnoreCase)
                && text.Contains("land", StringComparison.OrdinalIgnoreCase))
            || text.Contains("Add ", StringComparison.OrdinalIgnoreCase)
            || text.Contains("create a Treasure", StringComparison.OrdinalIgnoreCase);
        bool draw = text.Contains("draw a card", StringComparison.OrdinalIgnoreCase)
            || text.Contains("draw two cards", StringComparison.OrdinalIgnoreCase);
        return ramp || draw;
    }

    private static bool IsMythic(CardFact card) =>
        string.Equals(card.Rarity, "mythic", StringComparison.OrdinalIgnoreCase);

    // ---- REDUCE-01: always-on static generic cost reducers --------------------------------

    /// <summary>
    /// Detect an always-on static generic cost reducer ("&lt;Type&gt;? spells you cast cost {N}
    /// less"). The "you cast" anchor is required. Excludes (v1): "for each", "costs {N} less for",
    /// affinity/improvise/convoke/delve, one-shot/ritual discounts, opponent-symmetric/opponent-only
    /// text. Returns <see langword="null"/> when not a recognized reducer.
    /// </summary>
    private static CostReducer? DetectCostReducer(CardFact card)
    {
        string text = card.OracleText ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        string lower = text.ToLowerInvariant();

        // The required always-on anchor: "spells you cast cost {N} less".
        var match = StaticReducerRegex.Match(lower);
        if (!match.Success)
        {
            return null;
        }

        // v1 exclusions: scaling / non-static / opponent-facing reducers.
        if (lower.Contains("for each", StringComparison.Ordinal)
            || lower.Contains("less for", StringComparison.Ordinal)
            || lower.Contains("affinity", StringComparison.Ordinal)
            || lower.Contains("improvise", StringComparison.Ordinal)
            || lower.Contains("convoke", StringComparison.Ordinal)
            || lower.Contains("delve", StringComparison.Ordinal)
            || lower.Contains("opponent", StringComparison.Ordinal)
            || lower.Contains("opponents", StringComparison.Ordinal))
        {
            return null;
        }

        if (!int.TryParse(match.Groups["amt"].Value, out int amount) || amount <= 0)
        {
            return null;
        }

        // The matched scope prefix sits just before "spells you cast"; classify it.
        ReductionScope scope = ClassifyReducerScope(match.Groups["scope"].Value);

        return new CostReducer
        {
            GenericReduction = amount,
            Scope = scope,
            SourceManaValue = Math.Max(0, (int)Math.Round(card.ManaValue)),
        };
    }

    // Detect a card whose realistic cost is below its printed mana value: free/pitch spells,
    // board-scaling self-reducers (Blasphemous Act), and evoke/suspend. Returns a canonical braced
    // effective cost ("0", "{R}", "{1}{B}") plus a short reason, or null when nothing applies.
    // This is a SUGGESTION only — it pre-populates the override box; it never changes the analysis
    // by itself. Most-specific category first.
    private static (string EffectiveCost, string Reason)? DetectSelfCost(CardFact card)
    {
        // Known limitation: OracleText is joined across faces, so a multi-face card could inherit a
        // suggestion from a non-front face. Low harm — a suggestion only pre-fills the editable box
        // and applying it is opt-in; the user sees and can clear a wrong line.
        string text = (card.OracleText ?? string.Empty).ToLowerInvariant();
        if (text.Length == 0)
        {
            return null;
        }

        // 1) Free / pitch — self-anchored wording ("rather than pay this spell's mana cost").
        //    Not the "without paying its mana cost" wording, which casts OTHER spells for free.
        if (text.Contains("rather than pay this spell's mana cost", StringComparison.Ordinal))
        {
            return ("0", "free / alternative cost");
        }

        // 2) Board-scaling self-reduction ("this spell costs {1} less to cast for each ..."):
        //    drop all generic, keep the colored pips — the practical floor when fully online.
        if (ScalingSelfReducerRegex.IsMatch(text))
        {
            string colored = RenderColoredPips(ManaCostParser.Parse(card.ManaCost).Pips);
            return (colored.Length == 0 ? "0" : colored,
                "scales down with the board — assuming the reduction is fully online");
        }

        // 3) Evoke — use its braced mana cost when it has one (Shriekmaw "evoke {1}{B}"); a
        //    non-mana evoke cost (Grief "exile a black card") is free of mana, so 0.
        if (text.Contains("evoke", StringComparison.Ordinal))
        {
            Match evoke = EvokeCostRegex.Match(text);
            return evoke.Success
                ? (NormalizeBracedCost(evoke.Groups[1].Value), "evoke cost")
                : ("0", "evoke (alternative cost)");
        }

        // 4) Suspend — the suspend cost is a mana cost (Crashing Footfalls "suspend 1—{g}").
        Match suspend = SuspendCostRegex.Match(text);
        if (suspend.Success)
        {
            return (NormalizeBracedCost(suspend.Groups[1].Value), "suspend cost");
        }

        return null;
    }

    // Render hard colored pips as a canonical braced cost in WUBRG(+C) order (e.g. "{R}", "{U}{U}").
    private static string RenderColoredPips(IReadOnlyDictionary<ManaColor, int> pips)
    {
        var sb = new System.Text.StringBuilder();
        foreach (ManaColor color in new[]
                 {
                     ManaColor.White, ManaColor.Blue, ManaColor.Black,
                     ManaColor.Red, ManaColor.Green, ManaColor.Colorless,
                 })
        {
            int count = pips.GetValueOrDefault(color);
            for (int i = 0; i < count; i++)
            {
                sb.Append('{').Append(ColorSymbol(color)).Append('}');
            }
        }

        return sb.ToString();
    }

    private static char ColorSymbol(ManaColor color) => color switch
    {
        ManaColor.White => 'W',
        ManaColor.Blue => 'U',
        ManaColor.Black => 'B',
        ManaColor.Red => 'R',
        ManaColor.Green => 'G',
        _ => 'C',
    };

    // Re-render an already-braced cost (captured from oracle text, lower-cased) into canonical
    // upper-case braced form so the stored suggestion matches ManaCostParser's expectations.
    private static string NormalizeBracedCost(string braced) =>
        braced.ToUpperInvariant();

    private static ReductionScope ClassifyReducerScope(string scopePhrase)
    {
        string s = scopePhrase.Trim();
        bool instant = s.Contains("instant", StringComparison.Ordinal);
        bool sorcery = s.Contains("sorcery", StringComparison.Ordinal);
        if (instant || sorcery)
        {
            return ReductionScope.InstantSorcery;
        }

        if (s.Contains("creature", StringComparison.Ordinal))
        {
            return ReductionScope.Creature;
        }

        if (s.Contains("artifact", StringComparison.Ordinal))
        {
            return ReductionScope.Artifact;
        }

        return ReductionScope.All;
    }

    // ---- GRANT-01: mana-ability granters --------------------------------------------------

    /// <summary>Which creatures a granter turns into conditional any-color sources.</summary>
    private enum GranterScope
    {
        /// <summary>All creatures you control (Cryptolith Rite, Song of Freyalise).</summary>
        AllCreatures,

        /// <summary>Legendary creatures you control (Relic of Legends).</summary>
        LegendaryCreatures,
    }

    /// <summary>
    /// Detect a mana-ability granter (Cryptolith Rite / Song of Freyalise / Relic of Legends, or
    /// any "creatures you control have '{T}: Add'" text), including Equipment/Aura that grant the
    /// equipped/enchanted creature a mana ability (Paradise Mantle). Returns the scope or null.
    /// </summary>
    private static GranterScope? DetectGranter(CardFact card)
    {
        string text = card.OracleText ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        string lower = text.ToLowerInvariant();

        // MEDIUM-5: Equipment/Aura granters — "equipped/enchanted creature has '{T}: Add'".
        // A single equip/aura only ever enables one creature, so treat it as the broad
        // any-creature scope (the eligible-count cap in AddGrantedSources keeps it from stacking).
        if (lower.Contains("equipped creature has \"{t}: add", StringComparison.Ordinal)
            || lower.Contains("enchanted creature has \"{t}: add", StringComparison.Ordinal))
        {
            return GranterScope.AllCreatures;
        }

        bool tapForMana = lower.Contains("{t}: add", StringComparison.Ordinal);
        if (!tapForMana && !lower.Contains("have \"{t}", StringComparison.Ordinal))
        {
            return null;
        }

        if (lower.Contains("legendary creatures you control", StringComparison.Ordinal))
        {
            return GranterScope.LegendaryCreatures;
        }

        if (lower.Contains("creatures you control have", StringComparison.Ordinal))
        {
            return GranterScope.AllCreatures;
        }

        return null;
    }

    private static void AddGrantedSources(
        List<ManaSource> sources,
        IReadOnlyList<CardFact> cards,
        IReadOnlyList<GranterScope> granters,
        int deckColorCount)
    {
        if (granters.Count == 0)
        {
            return;
        }

        // Only the broadest scope present matters — eligible counts don't stack per-creature.
        bool anyAllCreatures = granters.Contains(GranterScope.AllCreatures);

        IReadOnlyList<ManaColor> deckColors = DeckColors(cards);
        if (deckColors.Count == 0)
        {
            return;
        }

        foreach (CardFact card in cards)
        {
            // MEDIUM-3: commanders ARE eligible granted sources (a commander creature is on the
            // battlefield like any other). Only exclude non-creatures and existing rocks/dorks.
            if (!IsType(card.TypeLine, "Creature"))
            {
                continue;
            }

            // A creature that is already a dork contributes a full weighted color source — don't
            // blanket-add a second any-color source on top of it.
            if (IsRockOrDork(card))
            {
                continue;
            }

            bool eligible = anyAllCreatures || IsLegendary(card.TypeLine);
            if (!eligible)
            {
                continue;
            }

            for (int i = 0; i < card.Quantity; i++)
            {
                sources.Add(new ManaSource
                {
                    Name = card.Name + " (granted)",
                    Produces = deckColors,
                    Weight = 0.25,
                    IsLand = false,
                    IsCommander = card.IsCommander,

                    // Enabler-conditional: this source only produces if the granter (Cryptolith Rite,
                    // Relic of Legends, ...) is on the battlefield AND this creature survives. That is
                    // genuinely speculative and out of scope to model fully, so the simulator keeps the
                    // per-trial Bernoulli activation at the 0.25 weight ONLY for these. Deployable ramp
                    // is full-value in the sim (its friction is the deploy cost + online-turn).
                    IsConditional = true,
                });
            }
        }
    }

    private static bool IsLegendary(string typeLine) =>
        IsType(typeLine.Split("//")[0], "Legendary");

    private static IReadOnlyList<ManaColor> DeckColors(IReadOnlyList<CardFact> cards)
    {
        var colors = new List<ManaColor>();
        foreach (CardFact card in cards)
        {
            foreach (KeyValuePair<ManaColor, int> pip in ManaCostParser.Parse(card.ManaCost).Pips)
            {
                if (pip.Value > 0 && pip.Key != ManaColor.Colorless && !colors.Contains(pip.Key))
                {
                    colors.Add(pip.Key);
                }
            }
        }

        return colors;
    }
}
