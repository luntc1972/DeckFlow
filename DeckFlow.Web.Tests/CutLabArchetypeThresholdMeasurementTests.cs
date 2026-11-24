using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using DeckFlow.Core.Analysis;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CutLab;
using DeckFlow.Web.Services.Manabase;

using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Measures archetype thresholds on the local cEDH calibration corpus.</summary>
public sealed class CutLabArchetypeThresholdMeasurementTests
{
    private const string AssumedCombo = "combo-assumed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Records live suggestions and checks which mirrored formulas explain them.</summary>
    [CutLabMeasurementFact]
    public void Measure_ArchetypeThresholds_WritesCorpusReport()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string dataDirectory = RequiredPath("DECKFLOW_CUTLAB_MEASURE_DATA");
        string output = RequiredPath("DECKFLOW_CUTLAB_MEASURE_OUT");
        string? categoryPath = Environment.GetEnvironmentVariable("DECKFLOW_CUTLAB_MEASURE_CATEGORIES");
        string decksPath = Path.Combine(dataDirectory, "decks_all.json");
        string cardsPath = Path.Combine(dataDirectory, "cards_full.json");
        var decks = ReadJson<List<CalibrationDeck>>(decksPath);
        var cards = ReadJson<Dictionary<string, ScryfallCardData>>(cardsPath);
        Assert.NotEmpty(decks);
        Assert.NotEmpty(cards);

        var variants = ReadVariants(categoryPath);

        Directory.CreateDirectory(output);
        string csvPath = Path.Combine(output, "evaluations.csv");
        string summaryPath = Path.Combine(output, "summary.md");
        string runPath = Path.Combine(output, "run.json");
        Dictionary<string, int> unresolved = new(StringComparer.Ordinal);
        List<string> inconsistent = [];
        StringBuilder summary = new();
        summary.AppendLine("# Cut Lab archetype threshold measurement");
        summary.AppendLine();
        summary.AppendLine($"Deck count: {decks.Count}");
        summary.AppendLine("Corpus: cEDH only, with no Casual or Focused decks. Categories are approximate and use stale, old-schema observations instead of the production store. EDHREC themes are empty, so the Voltron theme rule is untested. Combo presence is either unavailable or assumed, with no Spellbook lookup.");

        using (StreamWriter csv = new(csvPath))
        {
            csv.WriteLine("deck_index,cmdkey,tier,standing,variant,flavor,tutors_b2,tutors_a1,stax_b2,stax_a1,stax_role_only,fast_mana,avg_mv,spells,storm,voltron,interaction,creatures,slug,confidence,reason,unresolved_names,tutors_a1_lands");
            foreach (Variant variant in variants)
            {
                MeasureVariant(decks, cards, variant, csv, summary, unresolved, inconsistent);
            }
        }

        summary.AppendLine();
        summary.AppendLine($"Unresolved card-name occurrences across decks: {unresolved.Values.Sum()}");
        WriteTopNames(summary, unresolved);
        File.WriteAllText(summaryPath, summary.ToString());
        File.WriteAllText(runPath, JsonSerializer.Serialize(new
        {
            decksPath,
            cardsPath,
            categoryPath,
            output,
            timestamp,
            deckCount = decks.Count,
            cardCount = cards.Count,
            variantCount = variants.Count,
            evaluationCount = decks.Count * variants.Count * 2,
            unresolvedNameCount = unresolved.Values.Sum(),
        }, JsonOptions));

        Assert.All(new[] { csvPath, summaryPath, runPath }, path => Assert.True(new FileInfo(path).Length > 0, path));
        Assert.True(inconsistent.Count == 0, string.Join(Environment.NewLine, inconsistent));
    }

    /// <summary>Measures the live Consistency tiers and tutor counts across the calibration corpus.</summary>
    [CutLabMeasurementFact]
    public void Measure_ConsistencyTiers_WritesCorpusReport()
    {
        string dataDirectory = RequiredPath("DECKFLOW_CUTLAB_MEASURE_DATA");
        string output = Path.Combine(RequiredPath("DECKFLOW_CUTLAB_MEASURE_OUT"), "consistency");
        var decks = ReadJson<List<CalibrationDeck>>(Path.Combine(dataDirectory, "decks_all.json"));
        var cards = ReadJson<Dictionary<string, ScryfallCardData>>(Path.Combine(dataDirectory, "cards_full.json"));
        Assert.NotEmpty(decks);
        Assert.NotEmpty(cards);
        Directory.CreateDirectory(output);

        var flavors = new[]
        {
            (Name: "combo-unavailable", ComboCount: 0, ComboAvailable: false, Bands: new int[6]),
            (Name: "combo-assumed", ComboCount: 3, ComboAvailable: true, Bands: new int[6]),
        };
        int[] tutorBuckets = new int[6];
        int totalTutors = 0;
        int totalUnresolved = 0;
        SortedDictionary<string, DeckStatCardInput> searchCards = new(StringComparer.Ordinal);

        string csvPath = Path.Combine(output, "consistency.csv");
        using (StreamWriter csv = new(csvPath))
        {
            csv.WriteLine("deck_index,cmdkey,flavor,tutors,avg_mv,consistency_band,unresolved_names");
            for (int index = 0; index < decks.Count; index++)
            {
                CalibrationDeck deck = decks[index];
                var commanders = deck.Commanders.ToHashSet(StringComparer.Ordinal);
                List<DeckStatCardInput> inputs = [];
                int unresolved = 0;
                foreach (var group in deck.Commanders.Concat(deck.Maindeck).GroupBy(name => name, StringComparer.Ordinal))
                {
                    if (!cards.TryGetValue(group.Key, out ScryfallCardData? scryfall))
                    {
                        unresolved++;
                        continue;
                    }

                    CardFact fact = ScryfallCardFactMapper.ToCardFact(scryfall, group.Count(), commanders.Contains(group.Key));
                    if (fact.IsCommander)
                    {
                        continue;
                    }

                    DeckStatCardInput input = new(fact.Quantity, fact.TypeLine, fact.OracleText ?? "", fact.ManaCost ?? "");
                    inputs.Add(input);
                    if (input.OracleText.Contains("search your library for", StringComparison.OrdinalIgnoreCase))
                    {
                        int previousCopies = searchCards.GetValueOrDefault(fact.Name).Quantity;
                        searchCards[fact.Name] = input with { Quantity = previousCopies + input.Quantity };
                    }
                }

                DeckStatSummary stats = DeckStatAggregator.Compute(inputs);
                totalTutors += stats.Tutors;
                totalUnresolved += unresolved;
                int tutorBucket = stats.Tutors switch
                {
                    0 => 0,
                    1 => 1,
                    < 5 => 2,
                    < 8 => 3,
                    < 12 => 4,
                    _ => 5,
                };
                tutorBuckets[tutorBucket]++;

                foreach (var flavor in flavors)
                {
                    DeckMultiAxisScore score = MultiAxisScorer.Score(
                        stats, gameChangerCount: 0, twoCardComboCount: flavor.ComboCount,
                        comboDetectionAvailable: flavor.ComboAvailable, bracketNumber: 3);
                    flavor.Bands[score.ConsistencyBand]++;
                    csv.WriteLine(FormattableString.Invariant(
                        $"{index},{CsvField(deck.CmdKey)},{flavor.Name},{stats.Tutors},{stats.AverageManaValue},{score.ConsistencyBand},{unresolved}"));
                }
            }
        }

        string tutorCardsPath = Path.Combine(output, "tutor-cards.csv");
        using (StreamWriter csv = new(tutorCardsPath))
        {
            csv.WriteLine("name,copies,is_tutor");
            foreach (var (name, input) in searchCards)
            {
                bool isTutor = DeckStatAggregator.Compute([input with { Quantity = 1 }]).Tutors > 0;
                csv.WriteLine(FormattableString.Invariant($"{CsvField(name)},{input.Quantity},{(isTutor ? "true" : "false")}"));
            }
        }

        string summaryPath = Path.Combine(output, "summary.md");
        List<string> summary =
        [
            "# Consistency tier measurement",
            "",
            FormattableString.Invariant($"- Decks: {decks.Count}"),
            FormattableString.Invariant($"- Unresolved name occurrences: {totalUnresolved}"),
            FormattableString.Invariant($"- Total Tutors: {totalTutors}"),
            $"- Tutor buckets (0 / 1 / 2-4 / 5-7 / 8-11 / 12+): {string.Join(" / ", tutorBuckets)}",
        ];
        foreach (var flavor in flavors)
        {
            summary.Add($"- Consistency bands (0 / 1 / 2 / 3 / 4 / 5), {flavor.Name}: {string.Join(" / ", flavor.Bands)}");
        }

        File.WriteAllLines(summaryPath, summary);
        Assert.True(File.Exists(csvPath));
        Assert.True(File.Exists(tutorCardsPath));
        Assert.True(File.Exists(summaryPath));
    }

    /// <summary>Fingerprints Manabase plan roles and samples mulligan metrics to detect fetch-land classifier regressions.</summary>
    [CutLabMeasurementFact]
    public void Measure_ManabasePlanRoles_WritesCorpusReport()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        string dataDirectory = RequiredPath("DECKFLOW_CUTLAB_MEASURE_DATA");
        string output = Path.Combine(RequiredPath("DECKFLOW_CUTLAB_MEASURE_OUT"), "manabase");
        string? categoryPath = Environment.GetEnvironmentVariable("DECKFLOW_CUTLAB_MEASURE_CATEGORIES");
        string? sampleValue = Environment.GetEnvironmentVariable("DECKFLOW_CUTLAB_MEASURE_MANABASE_SAMPLE");
        int sample = string.IsNullOrWhiteSpace(sampleValue) ? 200 : int.Parse(sampleValue, CultureInfo.InvariantCulture);
        string decksPath = Path.Combine(dataDirectory, "decks_all.json");
        string cardsPath = Path.Combine(dataDirectory, "cards_full.json");
        var decks = ReadJson<List<CalibrationDeck>>(decksPath);
        var cards = ReadJson<Dictionary<string, ScryfallCardData>>(cardsPath);
        var variants = ReadVariants(categoryPath);
        int stride = sample <= 0 ? 0 : Math.Max(1, decks.Count / sample);

        Directory.CreateDirectory(output);
        string csvPath = Path.Combine(output, "plan-roles.csv");
        string summaryPath = Path.Combine(output, "summary.md");
        string runPath = Path.Combine(output, "run.json");
        StringBuilder summary = new();
        summary.AppendLine("# Manabase plan-role measurement");

        using (StreamWriter csv = new(csvPath))
        {
            csv.WriteLine("deck_index,cmdkey,variant,spells,plan_spells,tutorcombo_spells,tutorcombo_land_front_spells,roles_sha256,analyzed,keepable_pct,plan_keepable_pct,plan_presence_pct,shape_explosive_pct");
            foreach (Variant variant in variants)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                List<ManabaseMeasurement> rows = new(decks.Count);
                for (int index = 0; index < decks.Count; index++)
                {
                    CalibrationDeck deck = decks[index];
                    bool analyze = stride > 0 && index % stride == 0;
                    var row = MeasureManabaseDeck(deck, cards, variant, analyze);
                    rows.Add(row);
                    csv.WriteLine(FormattableString.Invariant(
                        $"{index},{CsvField(deck.CmdKey)},{variant.Name},{row.Spells},{row.PlanSpells},{row.TutorComboSpells},{row.LandFrontTutorComboSpells},{row.RolesHash},{row.Analyzed},{row.Mulligan?.KeepableHandPercent},{row.Mulligan?.PlanKeepablePercent},{row.Mulligan?.PlanPresence?.PlanPresencePercent},{row.Mulligan?.PlanPresence?.ShapeExplosivePercent}"));
                }

                summary.AppendLine();
                summary.AppendLine($"## {variant.Name}");
                summary.AppendLine($"Deck count: {rows.Count}");
                summary.AppendLine($"Total spells: {rows.Sum(row => row.Spells)}");
                summary.AppendLine($"Total tutorcombo_spells: {rows.Sum(row => row.TutorComboSpells)}");
                summary.AppendLine($"Total tutorcombo_land_front_spells: {rows.Sum(row => row.LandFrontTutorComboSpells)}");
                summary.AppendLine($"Analyzed count: {rows.Count(row => row.Analyzed)}");
                summary.AppendLine(FormattableString.Invariant($"Mean keepable_pct: {rows.Average(row => row.Mulligan?.KeepableHandPercent):F2}"));
                summary.AppendLine(FormattableString.Invariant($"Mean plan_keepable_pct: {rows.Average(row => row.Mulligan?.PlanKeepablePercent):F2}"));
                summary.AppendLine(FormattableString.Invariant($"Mean plan_presence_pct: {rows.Average(row => row.Mulligan?.PlanPresence?.PlanPresencePercent):F2}"));
                summary.AppendLine(FormattableString.Invariant($"Mean shape_explosive_pct: {rows.Average(row => row.Mulligan?.PlanPresence?.ShapeExplosivePercent):F2}"));
                summary.AppendLine(FormattableString.Invariant($"Elapsed seconds: {stopwatch.Elapsed.TotalSeconds:F2}"));
            }
        }

        File.WriteAllText(summaryPath, summary.ToString());
        File.WriteAllText(runPath, JsonSerializer.Serialize(new
        {
            decksPath,
            cardsPath,
            categoryPath,
            sample,
            stride,
            deckCount = decks.Count,
            timestamp,
        }, JsonOptions));
        Assert.All(new[] { csvPath, summaryPath, runPath }, path => Assert.True(new FileInfo(path).Length > 0, path));
    }

    private static ManabaseMeasurement MeasureManabaseDeck(
        CalibrationDeck deck,
        IReadOnlyDictionary<string, ScryfallCardData> cardData,
        Variant variant,
        bool analyze)
    {
        var commanders = deck.Commanders.ToHashSet(StringComparer.Ordinal);
        List<CardFact> facts = [];
        Dictionary<string, CardFact> factByName = new(StringComparer.OrdinalIgnoreCase);
        foreach (var group in deck.Commanders.Concat(deck.Maindeck).GroupBy(name => name, StringComparer.Ordinal))
        {
            if (cardData.TryGetValue(group.Key, out ScryfallCardData? scryfall))
            {
                CardFact fact = ScryfallCardFactMapper.ToCardFact(scryfall, group.Count(), commanders.Contains(group.Key));
                facts.Add(fact);
                factByName[fact.Name] = fact;
            }
        }

        ManabaseDeck classified = ManabaseClassifier.Classify(facts);
        List<SpellRequirement> tagged = new(classified.Spells.Count);
        int planSpells = 0;
        int tutorComboSpells = 0;
        int landFrontTutorComboSpells = 0;
        foreach (var spell in classified.Spells)
        {
            PlanRole roles = PlanRole.None;
            bool merit = false;
            if (factByName.TryGetValue(spell.Name, out CardFact? fact))
            {
                IReadOnlyList<string> categories = variant.Categories?.GetValueOrDefault(CardNormalizer.Normalize(fact.Name)) ?? [];
                roles = PlanRoleClassifier.Classify(fact, categories, isComboPiece: false, ManabaseMode.Cedh, out merit);
                if ((roles & PlanRole.TutorCombo) != 0)
                {
                    tutorComboSpells++;
                    if (CardTypeLine.FrontFace(fact.TypeLine).Contains("Land", StringComparison.OrdinalIgnoreCase))
                    {
                        landFrontTutorComboSpells++;
                    }
                }
            }

            planSpells += roles != PlanRole.None ? 1 : 0;
            tagged.Add(spell with { PlanRoles = roles, IsInteractionSpell = merit });
        }

        var roleLines = tagged.Select(spell => FormattableString.Invariant($"{spell.Name}|{(int)spell.PlanRoles}|{spell.IsInteractionSpell}"))
            .Order(StringComparer.Ordinal);
        byte[] rolesSha256 = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', roleLines)));
        var mulligan = analyze
            ? ManabaseAnalyzer.Analyze(classified with { Spells = tagged }, ManabaseMode.Cedh, keepShapes: true, interactionLens: true).MulliganEvaluation
            : null;
        return new ManabaseMeasurement(tagged.Count, planSpells, tutorComboSpells, landFrontTutorComboSpells,
            Convert.ToHexStringLower(rolesSha256.AsSpan(0, 8)), analyze, mulligan);
    }

    private static IReadOnlyList<Variant> ReadVariants(string? categoryPath)
    {
        List<Variant> variants = [new("oracle", null)];
        if (!string.IsNullOrWhiteSpace(categoryPath))
        {
            var observations = ReadJson<Dictionary<string, Dictionary<string, int>>>(categoryPath);
            variants.Add(new Variant("categories", observations.ToDictionary(
                entry => entry.Key, entry => SelectCategories(entry.Value), StringComparer.Ordinal)));
        }
        return variants;
    }

    private static void MeasureVariant(
        IReadOnlyList<CalibrationDeck> decks,
        IReadOnlyDictionary<string, ScryfallCardData> cardData,
        Variant variant,
        StreamWriter csv,
        StringBuilder summary,
        Dictionary<string, int> unresolved,
        List<string> inconsistent)
    {
        DeckPlanStrategyCatalog.TryGetBySlug("stax", out DeckPlanStrategyEntry stax);
        var mode = CutLabRoleAssigner.ResolveMode("cEDH");
        VariantResults results = new();
        for (int index = 0; index < decks.Count; index++)
        {
            CalibrationDeck deck = decks[index];
            var commanders = deck.Commanders.ToHashSet(StringComparer.Ordinal);
            Dictionary<string, IReadOnlyList<string>> categoriesByName = new(CutLabCardNames.Comparer);
            List<CutLabAnalyzedCard> cards = [];
            int unresolvedNames = 0;
            foreach (var group in deck.Commanders.Concat(deck.Maindeck).GroupBy(name => name, StringComparer.Ordinal))
            {
                string name = group.Key;
                bool isCommander = commanders.Contains(name);
                int quantity = group.Count();
                cardData.TryGetValue(name, out ScryfallCardData? scryfall);
                IReadOnlyList<string> categories = variant.Categories?.GetValueOrDefault(CardNormalizer.Normalize(name)) ?? [];
                categoriesByName[CutLabCardNames.Normalize(name)] = categories;
                IReadOnlyList<string> roles = [];
                double manaValue = 0;
                if (scryfall is not null)
                {
                    var fact = ScryfallCardFactMapper.ToCardFact(scryfall, quantity, isCommander);
                    roles = CutLabRoleAssigner.AssignRoles(fact, categories, mode);
                    manaValue = fact.ManaValue;
                }
                else
                {
                    unresolvedNames++;
                    if (!variant.HasCategories)
                    {
                        AddCopies(unresolved, name, 1);
                    }
                }

                cards.Add(new CutLabAnalyzedCard(name, manaValue, roles.Contains("lands", StringComparer.Ordinal), roles, categories)
                {
                    Quantity = quantity,
                    IsCommander = isCommander,
                    TypeLine = scryfall?.TypeLine ?? string.Empty,
                    SemanticProfile = scryfall is not null && scryfall.CardFaces is not { Count: > 0 }
                        ? new CutLabSemanticProfile(scryfall.OracleId, scryfall.ManaCost, scryfall.TypeLine, scryfall.Power, scryfall.Toughness, scryfall.Keywords, scryfall.ColorIdentity, scryfall.OracleText, scryfall.Layout, OracleName: scryfall.Name)
                        : null,
                });
            }

            var nonCommander = cards.Where(card => !card.IsCommander).ToArray();
            Counts counts = MeasureCounts(nonCommander, variant.HasCategories, stax, results);
            results.DeckCounts.Add(counts);

            CutLabClassificationContext classification = new(
                [], false, variant.HasCategories, categoriesByName, new Dictionary<string, CutLabCardComboMembership>());
            WriteEvaluation(index, deck, variant.Name, "combo-unavailable", cards, classification, counts, unresolvedNames, results, csv);

            Dictionary<string, CutLabCardComboMembership> membership = new(CutLabCardNames.Comparer);
            if (nonCommander.FirstOrDefault(card => !card.IsLand) is { } firstNonland)
            {
                SpellbookCombo combo = new([firstNonland.Name], ["Win"], "Win the game.");
                membership[CutLabCardNames.Normalize(firstNonland.Name)] = new CutLabCardComboMembership([combo], []);
            }

            var assumed = classification with { ComboDataAvailable = true, CardComboMembership = membership };
            WriteEvaluation(index, deck, variant.Name, AssumedCombo, cards, assumed, counts, unresolvedNames, results, csv);
        }

        WriteVariantSummary(summary, variant, results, inconsistent);
    }

    private static IReadOnlyList<string> SelectCategories(Dictionary<string, int> observations)
    {
        // Why: CardCategoryRepository.BuildCategoryLookupSql and SelectMeaningfulCategories
        // are mirrored here as a measurement-only approximation over old-schema observations.
        var qualified = observations.Where(row => row.Value >= CategoryCacheSchema.DefaultMinObservationRows).ToArray();
        int topCount = qualified.Select(row => row.Value).DefaultIfEmpty().Max();
        var included = qualified.Where(row => row.Value * 2000L >= topCount && CategoryFilter.IsIncluded(row.Key)).ToArray();
        int total = included.Sum(row => row.Value);

        return included.Where(row => row.Value >= total * CategoryLookupOptions.CutLabMinimumObservationShare)
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.Key, StringComparer.OrdinalIgnoreCase)
            .Take(CategoryLookupOptions.CutLabMaximumCategoriesPerCard)
            .Select(row => row.Key)
            .ToArray();
    }

    private static Counts MeasureCounts(
        IReadOnlyList<CutLabAnalyzedCard> cards,
        bool hasCategories,
        DeckPlanStrategyEntry stax,
        VariantResults results)
    {
        int tutorsB2 = 0;
        int tutorsA1 = 0;
        int tutorsA1Lands = 0;
        int staxB2 = 0;
        int staxA1 = 0;
        int staxRoleOnly = 0;
        foreach (var card in cards)
        {
            int quantity = Math.Max(0, card.Quantity);
            bool useCategories = hasCategories && card.Categories.Count > 0;
            bool tutorRole = HasRole(card, CutLabRoleKeys.Tutor);
            bool staxRole = HasRole(card, CutLabRoleKeys.Stax);
            bool staxCategory = DeckPlanStrategyCatalog.MatchesCategories(stax, card.Categories);
            bool tutorB2 = useCategories
                ? card.Categories.Any(category => category.Contains("tutor", StringComparison.OrdinalIgnoreCase))
                : tutorRole;
            bool cardStaxB2 = useCategories ? staxCategory : staxRole;
            bool cardStaxA1 = staxRole || staxCategory;

            tutorsB2 += tutorB2 ? quantity : 0;
            tutorsA1 += tutorRole ? quantity : 0;
            staxB2 += cardStaxB2 ? quantity : 0;
            staxA1 += cardStaxA1 ? quantity : 0;
            staxRoleOnly += staxRole ? quantity : 0;
            if (card.IsLand && tutorRole)
            {
                tutorsA1Lands += quantity;
                AddCopies(results.TutorRoleLandCopies, card.Name, quantity);
            }

            if (!hasCategories)
            {
                continue;
            }

            if (!card.IsLand)
            {
                results.NonlandCopies += quantity;
                results.CategorizedNonlandCopies += useCategories ? quantity : 0;
            }

            RecordDifference(results.TutorGains, results.TutorLosses, card.Name, quantity, tutorB2, tutorRole);
            RecordDifference(results.StaxGains, results.StaxLosses, card.Name, quantity, cardStaxB2, cardStaxA1);
        }

        var nonlands = cards.Where(card => !card.IsLand && card.Quantity > 0).ToArray();
        int nonlandQuantity = nonlands.Sum(card => card.Quantity);
        double? averageManaValue = nonlandQuantity == 0 ? null : nonlands.Sum(card => card.ManaValue * card.Quantity) / nonlandQuantity;
        return new Counts(
            tutorsB2, tutorsA1, staxB2, staxA1, staxRoleOnly,
            Count(cards, card => !card.IsLand && HasRole(card, "ramp") && card.ManaValue <= 1),
            averageManaValue,
            Count(cards, card => HasType(card, "Instant") || HasType(card, "Sorcery")),
            Count(cards, card => card.SemanticProfile?.Keywords?.Contains("Storm", StringComparer.OrdinalIgnoreCase) == true),
            Count(cards, card => HasType(card, "Equipment") || HasType(card, "Aura")),
            Count(cards, card => HasRole(card, "interaction-targeted") || HasRole(card, "interaction-mass")),
            Count(cards, card => HasType(card, "Creature")),
            tutorsA1Lands);
    }

    private static void WriteEvaluation(
        int index,
        CalibrationDeck deck,
        string variant,
        string flavor,
        IReadOnlyList<CutLabAnalyzedCard> cards,
        CutLabClassificationContext classification,
        Counts counts,
        int unresolved,
        VariantResults results,
        StreamWriter csv)
    {
        var suggestion = CutLabArchetypeSuggester.Suggest(cards, classification, []);
        string slug = suggestion.Slug;
        var key = (flavor, slug);
        results.ArchetypeCounts[key] = results.ArchetypeCounts.GetValueOrDefault(key) + 1;

        string sample = $"{index} ({deck.CmdKey}), {flavor}, {slug}: {suggestion.Reason}";
        if (ViolatesFormula(suggestion, classification, counts, counts.TutorsB2, counts.StaxB2))
        {
            results.B2.Add(sample);
        }

        if (ViolatesFormula(suggestion, classification, counts, counts.TutorsA1, counts.StaxA1))
        {
            results.A1.Add(sample);
        }

        csv.WriteLine(FormattableString.Invariant(
            $"{index},{CsvField(deck.CmdKey)},{CsvField(deck.Tier)},{deck.Standing},{variant},{flavor},{counts.TutorsB2},{counts.TutorsA1},{counts.StaxB2},{counts.StaxA1},{counts.StaxRoleOnly},{counts.FastMana},{counts.AverageManaValue},{counts.Spells},{counts.Storm},{counts.Voltron},{counts.Interaction},{counts.Creatures},{slug},{suggestion.Confidence},{CsvField(suggestion.Reason)},{unresolved},{counts.TutorsA1Lands}"));
    }

    private static bool ViolatesFormula(
        CutLabArchetypeSuggestion suggestion,
        CutLabClassificationContext classification,
        Counts counts,
        int tutors,
        int stax)
    {
        const string comboPrefix = "combo present, ";
        return (suggestion.Slug == "stax" && LeadingCount(suggestion.Reason) != stax)
            || (suggestion.Slug == "turbo-combo"
                && (!suggestion.Reason.StartsWith(comboPrefix, StringComparison.Ordinal)
                    || LeadingCount(suggestion.Reason[comboPrefix.Length..]) != tutors))
            || (suggestion.Slug is not ("turbo-combo" or "stax") && stax >= 5)
            || (classification.ComboDataAvailable && tutors >= 8 && counts.FastMana >= 8
                && counts.AverageManaValue is <= 2.5 && suggestion.Slug != "turbo-combo");
    }

    private static int? LeadingCount(string text)
    {
        int space = text.IndexOf(' ');
        return space > 0 && int.TryParse(text.AsSpan(0, space), NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            ? count
            : null;
    }

    private static void WriteVariantSummary(StringBuilder summary, Variant variant, VariantResults results, List<string> inconsistent)
    {
        List<string> formulas = [];
        if (results.B2.Count == 0)
        {
            formulas.Add("B2");
        }

        if (results.A1.Count == 0)
        {
            formulas.Add("A1");
        }

        if (formulas.Count == 0)
        {
            var samples = results.B2.Samples.Take(5).Select(sample => $"B2: {sample}")
                .Concat(results.A1.Samples.Take(5).Select(sample => $"A1: {sample}"));
            inconsistent.Add($"{variant.Name}: {string.Join(Environment.NewLine, samples)}");
        }

        summary.AppendLine();
        summary.AppendLine($"## {variant.Name}");
        summary.AppendLine();
        summary.AppendLine($"consistent_formula[{variant.Name}]: {(formulas.Count == 0 ? "none" : string.Join(", ", formulas))}");
        summary.AppendLine($"Formula violations across evaluations: B2={results.B2.Count}, A1={results.A1.Count}.");
        if (variant.HasCategories)
        {
            summary.AppendLine($"Category coverage of nonland, non-commander copies: {Share(results.CategorizedNonlandCopies, results.NonlandCopies)}.");
        }

        foreach (var group in results.ArchetypeCounts.GroupBy(entry => entry.Key.Flavor).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var items = group.OrderBy(entry => entry.Key.Slug, StringComparer.Ordinal)
                .Select(entry => $"{entry.Key.Slug}={entry.Value}");
            summary.AppendLine($"- {variant.Name}/{group.Key}: {string.Join(", ", items)}");
        }

        summary.AppendLine();
        summary.AppendLine("Count histograms, with the share of decks meeting each threshold:");
        WriteHistogram(summary, "tutors_b2", results.DeckCounts.Select(count => count.TutorsB2), 4, 12, 8);
        WriteHistogram(summary, "tutors_a1", results.DeckCounts.Select(count => count.TutorsA1), 4, 12, 8);
        WriteHistogram(summary, "stax_b2", results.DeckCounts.Select(count => count.StaxB2), 2, 7, 5);
        WriteHistogram(summary, "stax_a1", results.DeckCounts.Select(count => count.StaxA1), 2, 7, 5);
        WriteHistogram(summary, "stax_role_only", results.DeckCounts.Select(count => count.StaxRoleOnly), 2, 7, 5);
        WriteThreshold(summary, "fast_mana >= 8", results.DeckCounts, count => count.FastMana >= 8);
        WriteThreshold(summary, "avg_mv <= 2.5", results.DeckCounts, count => count.AverageManaValue is <= 2.5);
        WriteThreshold(summary, "spells >= 30", results.DeckCounts, count => count.Spells >= 30);
        WriteThreshold(summary, "storm >= 1", results.DeckCounts, count => count.Storm >= 1);
        WriteThreshold(summary, "voltron >= 10", results.DeckCounts, count => count.Voltron >= 10);
        WriteThreshold(summary, "interaction >= 20", results.DeckCounts, count => count.Interaction >= 20);
        WriteThreshold(summary, "creatures <= 15", results.DeckCounts, count => count.Creatures <= 15);

        if (variant.HasCategories)
        {
            summary.AppendLine();
            summary.AppendLine("Top names by card copies that change the count:");
            summary.AppendLine("- Tutors counted only by A1:");
            WriteTopNames(summary, results.TutorGains);
            summary.AppendLine("- Tutors counted only by B2:");
            WriteTopNames(summary, results.TutorLosses);
            summary.AppendLine("- Stax counted only by A1:");
            WriteTopNames(summary, results.StaxGains);
            summary.AppendLine("- Stax counted only by B2:");
            WriteTopNames(summary, results.StaxLosses);
        }

        summary.AppendLine();
        summary.AppendLine($"Tutor-role copies: {results.DeckCounts.Sum(count => count.TutorsA1)}; on lands: {results.DeckCounts.Sum(count => count.TutorsA1Lands)}.");
        summary.AppendLine("Tutor-role copies on lands, by name:");
        WriteTopNames(summary, results.TutorRoleLandCopies);
    }

    private static void WriteHistogram(StringBuilder summary, string name, IEnumerable<int> values, int floor, int ceiling, int threshold)
    {
        int[] counts = values.ToArray();
        List<string> buckets = [$"0-{floor}={counts.Count(count => count <= floor)}"];
        for (int bucket = floor + 1; bucket < ceiling; bucket++)
        {
            buckets.Add($"{bucket}={counts.Count(count => count == bucket)}");
        }

        buckets.Add($"{ceiling}+={counts.Count(count => count >= ceiling)}");
        summary.AppendLine($"- {name}: {string.Join(", ", buckets)}. >= {threshold}: {Share(counts.Count(count => count >= threshold), counts.Length)}.");
    }

    private static void WriteThreshold(StringBuilder summary, string label, IReadOnlyList<Counts> counts, Func<Counts, bool> matches)
        => summary.AppendLine($"- {label}: {Share(counts.Count(matches), counts.Count)}.");

    private static string Share(int numerator, int denominator)
        => denominator == 0 ? "n/a (0/0)" : FormattableString.Invariant($"{(double)numerator / denominator:P2} ({numerator}/{denominator})");

    private static void WriteTopNames(StringBuilder summary, Dictionary<string, int> names)
    {
        if (names.Count == 0)
        {
            summary.AppendLine("  - None.");
        }

        foreach (var entry in names.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.Ordinal).Take(20))
        {
            summary.AppendLine($"  - {entry.Key}: {entry.Value}");
        }
    }

    private static void RecordDifference(
        Dictionary<string, int> gains,
        Dictionary<string, int> losses,
        string name,
        int quantity,
        bool b2,
        bool a1)
    {
        if (b2 != a1 && quantity > 0)
        {
            AddCopies(a1 ? gains : losses, name, quantity);
        }
    }

    private static void AddCopies(Dictionary<string, int> counts, string name, int quantity)
        => counts[name] = counts.GetValueOrDefault(name) + quantity;

    private static string CsvField(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    private static int Count(IEnumerable<CutLabAnalyzedCard> cards, Func<CutLabAnalyzedCard, bool> predicate)
        => cards.Where(predicate).Sum(card => Math.Max(0, card.Quantity));

    private static bool HasRole(CutLabAnalyzedCard card, string role)
        => card.Roles.Contains(role, StringComparer.Ordinal);

    private static bool HasType(CutLabAnalyzedCard card, string type)
        => card.TypeLine.Contains(type, StringComparison.OrdinalIgnoreCase);

    private static string RequiredPath(string variable)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } path
            ? path
            : throw new InvalidOperationException($"Set {variable} to run the Cut Lab measurement.");

    private static T ReadJson<T>(string path) where T : class
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Empty measurement input: {path}");
    }

    private sealed record CalibrationDeck(string Tier, int Size, string CmdKey, List<string> Commanders, int Standing, List<string> Maindeck);

    private sealed record Variant(string Name, IReadOnlyDictionary<string, IReadOnlyList<string>>? Categories)
    {
        public bool HasCategories => Categories is not null;
    }

    private sealed record ManabaseMeasurement(
        int Spells, int PlanSpells, int TutorComboSpells, int LandFrontTutorComboSpells,
        string RolesHash, bool Analyzed, ManabaseMulliganEvaluation? Mulligan);

    private sealed record Counts(
        int TutorsB2, int TutorsA1, int StaxB2, int StaxA1, int StaxRoleOnly, int FastMana,
        double? AverageManaValue, int Spells, int Storm, int Voltron, int Interaction, int Creatures, int TutorsA1Lands);

    private sealed class VariantResults
    {
        public List<Counts> DeckCounts { get; } = [];
        public Dictionary<(string Flavor, string Slug), int> ArchetypeCounts { get; } = [];
        public Violations B2 { get; } = new();
        public Violations A1 { get; } = new();
        public Dictionary<string, int> TutorRoleLandCopies { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> TutorGains { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> TutorLosses { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> StaxGains { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> StaxLosses { get; } = new(StringComparer.Ordinal);
        public int NonlandCopies { get; set; }
        public int CategorizedNonlandCopies { get; set; }
    }

    private sealed class Violations
    {
        public int Count { get; private set; }
        public List<string> Samples { get; } = [];

        public void Add(string sample)
        {
            Count++;
            if (Samples.Count < 5)
            {
                Samples.Add(sample);
            }
        }
    }
}
