using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using DeckFlow.Core.Manabase;

namespace DeckFlow.CLI;

/// <summary>
/// Runs the <c>cedh-land-baseline</c> command: load cached EDHTop16/Scryfall calibration data,
/// classify each deck with DeckFlow's mana-base pipeline, apply the cEDH gate, and emit the
/// committed monthly land-baseline artifacts consumed by the web app.
/// </summary>
internal static class CedhBaselineCommandRunner
{
    private static readonly Regex MonthLabelRegex = new(@"^\d{4}-\d{2}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Build the monthly cEDH land baseline from calibration JSON files.</summary>
    /// <param name="dataDirectory">Directory containing <c>decks_all.json</c> and <c>cards_full.json</c>.</param>
    /// <param name="outputDirectory">Directory to write the monthly markdown/JSON artifacts into.</param>
    /// <param name="month">Month label in <c>YYYY-MM</c> form.</param>
    public static Task<int> RunAsync(string dataDirectory, string outputDirectory, string month)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            Console.Error.WriteLine("--data is required.");
            return Task.FromResult(1);
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            Console.Error.WriteLine("--out is required.");
            return Task.FromResult(1);
        }

        if (!MonthLabelRegex.IsMatch(month))
        {
            Console.Error.WriteLine("--month must be in YYYY-MM format.");
            return Task.FromResult(1);
        }

        try
        {
            string decksPath = Path.Combine(dataDirectory, "decks_all.json");
            string cardsPath = Path.Combine(dataDirectory, "cards_full.json");
            if (!File.Exists(decksPath) || !File.Exists(cardsPath))
            {
                Console.Error.WriteLine($"Expected calibration files at {decksPath} and {cardsPath}.");
                return Task.FromResult(2);
            }

            List<CalibrationDeck>? decks = JsonSerializer.Deserialize<List<CalibrationDeck>>(
                File.ReadAllText(decksPath),
                JsonOptions);
            Dictionary<string, ScryfallCardData>? cards = JsonSerializer.Deserialize<Dictionary<string, ScryfallCardData>>(
                File.ReadAllText(cardsPath),
                JsonOptions);

            if (decks is null || cards is null)
            {
                Console.Error.WriteLine("Could not deserialize calibration inputs.");
                return Task.FromResult(2);
            }

            var samples = new List<CedhDeckSample>(decks.Count);
            foreach (CalibrationDeck deck in decks)
            {
                var facts = new List<CardFact>(deck.Commanders.Count + deck.Maindeck.Count);
                AddCardFacts(facts, deck.Commanders, cards, isCommander: true);
                AddCardFacts(facts, deck.Maindeck, cards, isCommander: false);

                // These accuracy flags are pinned ON so the baseline's land count matches the app's
                // own classification (Sources.IsLand) under the prod accuracy profile; changing them
                // would drift the committed baseline from live analysis. See the Phase A plan.
                ManabaseDeck classifiedDeck = ManabaseClassifier.Classify(
                    facts,
                    isSingleton: true,
                    rampCreditV2: true,
                    landRampSim: true,
                    payLifeUntapped: true,
                    checkLandUntapped: true);

                samples.Add(new CedhDeckSample(
                    deck.CmdKey,
                    deck.Tier,
                    classifiedDeck.Sources.Count(source => source.IsLand),
                    classifiedDeck.AverageManaValue,
                    facts.Count));
            }

            CedhLandBaselineResult result = CedhLandBaseline.Build(samples, month);
            CedhLandBaselineSnapshot snapshot = CedhLandBaseline.ToSnapshot(result);
            if (result.SampleSize == 0)
            {
                Console.Error.WriteLine("The cEDH gate kept zero decks; nothing to write.");
                return Task.FromResult(2);
            }

            Directory.CreateDirectory(outputDirectory);

            string markdownPath = Path.Combine(outputDirectory, $"{month}.md");
            string monthlyJsonPath = Path.Combine(outputDirectory, $"{month}.json");
            string latestJsonPath = Path.Combine(outputDirectory, "latest.json");

            SnapshotFileWriter.WriteLfFile(markdownPath, BuildMarkdownReport(result));
            string snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
            SnapshotFileWriter.WriteLfFile(monthlyJsonPath, snapshotJson);
            SnapshotFileWriter.WriteLfFile(latestJsonPath, snapshotJson);

            Console.WriteLine($"Wrote {markdownPath}");
            Console.WriteLine($"Wrote {monthlyJsonPath}");
            Console.WriteLine($"Wrote {latestJsonPath}");
            Console.WriteLine(
                FormattableString.Invariant(
                    $"SampleSize={snapshot.SampleSize}, OverallMeanLands={snapshot.OverallMeanLands:0.0}, Commanders={snapshot.Commanders.Count}"));

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

    private static string BuildMarkdownReport(CedhLandBaselineResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# cEDH land baseline — {result.Month}");
        sb.AppendLine();
        sb.AppendLine(
            FormattableString.Invariant(
                $"Raw sample: {result.RawSampleSize} decks -> **{result.SampleSize}** kept cEDH decks (dropped {result.DroppedForCurve} high-curve, {result.DroppedForIncomplete} incomplete)"));
        sb.AppendLine(
            FormattableString.Invariant(
                $"Overall: N **{result.Overall.SampleSize}** | mean **{result.Overall.MeanLands:0.0}** | SD {result.Overall.StandardDeviation:0.0} | min-max {result.Overall.MinLands}-{result.Overall.MaxLands}"));
        sb.AppendLine();
        sb.AppendLine("## By size tier");
        sb.AppendLine("| Tier | N | Lands mean | SD | min-max |");
        sb.AppendLine("|------|---|-----------:|---:|--------:|");
        foreach (CedhLandTierStat tier in result.Tiers)
        {
            sb.AppendLine(
                FormattableString.Invariant(
                    $"| {tier.Tier} | {tier.SampleSize} | {tier.MeanLands:0.0} | {tier.StandardDeviation:0.0} | {tier.MinLands}-{tier.MaxLands} |"));
        }

        sb.AppendLine();
        sb.AppendLine("## Land histogram");
        foreach (CedhLandHistogramEntry bin in result.Histogram)
        {
            sb.AppendLine($"- {bin.Lands} lands: {bin.Count}");
        }

        sb.AppendLine();
        sb.AppendLine("## By commander (N>=3)");
        sb.AppendLine("| Commander | N | Lands mean | SD | min-max |");
        sb.AppendLine("|-----------|---|-----------:|---:|--------:|");
        foreach (KeyValuePair<string, CedhLandStats> pair in result.Commanders
                     .OrderByDescending(entry => entry.Value.SampleSize)
                     .ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            CedhLandStats commander = pair.Value;
            sb.AppendLine(
                FormattableString.Invariant(
                    $"| {EscapePipe(pair.Key)} | {commander.SampleSize} | {commander.MeanLands:0.0} | {commander.StandardDeviation:0.0} | {commander.MinLands}-{commander.MaxLands} |"));
        }

        return sb.ToString();
    }

    private static string EscapePipe(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private sealed record CalibrationDeck
    {
        public required string Tier { get; init; }

        public required string CmdKey { get; init; }

        public required List<string> Commanders { get; init; }

        public required List<string> Maindeck { get; init; }
    }
}
