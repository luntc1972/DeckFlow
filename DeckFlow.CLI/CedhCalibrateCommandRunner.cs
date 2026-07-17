using System.Text.Json;

using DeckFlow.Core.Manabase;

namespace DeckFlow.CLI;

/// <summary>
/// Runs the <c>cedh-land-calibrate</c> command: replay cached cEDH decks through the real Core
/// classifier and compare the historic flat-28 target against the enabled-context hybrid target.
/// </summary>
internal static class CedhCalibrateCommandRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Build the cEDH land-target calibration markdown report from cached JSON inputs.</summary>
    /// <param name="dataDirectory">Directory containing <c>decks_all.json</c> and <c>cards_full.json</c>.</param>
    /// <param name="baselinePath">Path to the committed cEDH baseline snapshot JSON.</param>
    /// <param name="outputPath">Markdown report output path. Defaults to <c>&lt;data&gt;/cedh-calibration.md</c>.</param>
    public static Task<int> RunAsync(string dataDirectory, string baselinePath, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            Console.Error.WriteLine("--data is required.");
            return Task.FromResult(1);
        }

        if (string.IsNullOrWhiteSpace(baselinePath))
        {
            Console.Error.WriteLine("--baseline is required.");
            return Task.FromResult(1);
        }

        string resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? Path.Combine(dataDirectory, "cedh-calibration.md")
            : outputPath;

        try
        {
            string decksPath = Path.Combine(dataDirectory, "decks_all.json");
            string cardsPath = Path.Combine(dataDirectory, "cards_full.json");
            if (!File.Exists(decksPath) || !File.Exists(cardsPath))
            {
                Console.Error.WriteLine($"Expected calibration files at {decksPath} and {cardsPath}.");
                return Task.FromResult(2);
            }

            if (!File.Exists(baselinePath))
            {
                Console.Error.WriteLine($"Expected baseline snapshot at {baselinePath}.");
                return Task.FromResult(2);
            }

            List<CalibrationDeck>? decks = JsonSerializer.Deserialize<List<CalibrationDeck>>(
                File.ReadAllText(decksPath),
                JsonOptions);
            Dictionary<string, ScryfallCardData>? cards = JsonSerializer.Deserialize<Dictionary<string, ScryfallCardData>>(
                File.ReadAllText(cardsPath),
                JsonOptions);
            CedhLandBaselineSnapshot? snapshot = JsonSerializer.Deserialize<CedhLandBaselineSnapshot>(
                File.ReadAllText(baselinePath),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            if (decks is null || cards is null || snapshot is null)
            {
                Console.Error.WriteLine("Could not deserialize calibration inputs.");
                return Task.FromResult(2);
            }

            var rows = new List<CedhCalibrationRow>(decks.Count);
            foreach (CalibrationDeck deck in decks)
            {
                var facts = new List<CardFact>(deck.Commanders.Count + deck.Maindeck.Count);
                AddCardFacts(facts, deck.Commanders, cards, isCommander: true);
                AddCardFacts(facts, deck.Maindeck, cards, isCommander: false);

                if (facts.Count is < 95 or > 101)
                {
                    continue;
                }

                // These accuracy flags are pinned ON so the calibration's land count and target match
                // the app's own classification (Sources.IsLand) under the prod accuracy profile — the
                // same profile the committed baseline was built with (see CedhBaselineCommandRunner).
                ManabaseDeck classifiedDeck = ManabaseClassifier.Classify(
                    facts,
                    isSingleton: true,
                    rampCreditV2: true,
                    landRampSim: true,
                    payLifeUntapped: true,
                    checkLandUntapped: true);

                if (!CedhLandBaseline.PassesCedhGate(facts.Count, classifiedDeck.AverageManaValue))
                {
                    continue;
                }

                int actualLands = classifiedDeck.Sources.Count(source => source.IsLand);
                double oldTarget = Math.Max(
                    28.0,
                    KarstenManabase.SingletonLandTarget(
                    classifiedDeck.TotalCards,
                    classifiedDeck.CommanderCount,
                    classifiedDeck.AverageManaValue,
                    classifiedDeck.RampAndDrawUnderThree,
                    classifiedDeck.FastMana) - 3.5);

                bool hasBaseline = snapshot.Commanders.TryGetValue(deck.CmdKey, out CedhCommanderBaselineSnapshot? commanderBaseline)
                    && commanderBaseline.N >= 10;
                var context = hasBaseline
                    ? new CedhLandContext(commanderBaseline!.LandsMean, commanderBaseline.N, Enabled: true)
                    : new CedhLandContext(null, 0, Enabled: true);
                double newTarget = KarstenManabase.CedhLandTarget(
                    classifiedDeck.TotalCards,
                    classifiedDeck.CommanderCount,
                    classifiedDeck.AverageManaValue,
                    classifiedDeck.RampAndDrawUnderThree,
                    classifiedDeck.FastMana,
                    context);
                double newTargetWithRitualCredit = KarstenManabase.CedhLandTarget(
                    classifiedDeck.TotalCards,
                    classifiedDeck.CommanderCount,
                    classifiedDeck.AverageManaValue,
                    classifiedDeck.RampAndDrawUnderThree,
                    classifiedDeck.FastMana,
                    context,
                    netPositiveRitualCount: classifiedDeck.OneShots.Count,
                    ritualLandCredit: true);

                rows.Add(new CedhCalibrationRow(
                    deck.CmdKey,
                    actualLands,
                    oldTarget,
                    newTarget,
                    newTargetWithRitualCredit,
                    hasBaseline));
            }

            CedhCalibrationReport report = CedhCalibration.Build(rows);
            if (report.SampleSize == 0)
            {
                Console.Error.WriteLine("The cEDH gate kept zero decks; nothing to write.");
                return Task.FromResult(2);
            }

            string? outputDirectory = Path.GetDirectoryName(resolvedOutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            SnapshotFileWriter.WriteLfFile(resolvedOutputPath, CedhCalibration.RenderMarkdown(report));

            Console.WriteLine($"Wrote {resolvedOutputPath}");
            Console.WriteLine(CedhCalibration.RenderHeadline(report));
            return Task.FromResult(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return Task.FromResult(1);
        }
    }

    private static void AddCardFacts(
        List<CardFact> facts,
        IEnumerable<string> names,
        IReadOnlyDictionary<string, ScryfallCardData> cards,
        bool isCommander)
    {
        foreach (string name in names)
        {
            if (cards.TryGetValue(name, out ScryfallCardData? card))
            {
                facts.Add(ScryfallCardFactMapper.ToCardFact(card, quantity: 1, isCommander: isCommander));
            }
        }
    }

    private sealed record CalibrationDeck
    {
        public required string Tier { get; init; }

        public required string CmdKey { get; init; }

        public required List<string> Commanders { get; init; }

        public required List<string> Maindeck { get; init; }
    }
}
