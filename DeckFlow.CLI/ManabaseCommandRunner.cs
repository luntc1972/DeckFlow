using System.Globalization;
using System.Net;
using System.Text.Json.Serialization;
using DeckFlow.Core.Manabase;
using DeckFlow.Core.Models;
using RestSharp;

namespace DeckFlow.CLI;

/// <summary>
/// Runs the <c>manabase</c> command: load a public deck, resolve every card through
/// Scryfall's collection endpoint, then run the Karsten §6 mana-base pipeline
/// (<see cref="ScryfallCardFactMapper"/> → <see cref="ManabaseClassifier"/> →
/// <see cref="ManabaseAnalyzer"/>) and print the report.
/// </summary>
internal static class ManabaseCommandRunner
{
    // Scryfall's collection endpoint accepts at most 75 identifiers per request.
    private const int CollectionBatchSize = 75;

    // Scryfall asks for ~50-100ms between requests; pace batches conservatively.
    private static readonly TimeSpan BatchDelay = TimeSpan.FromMilliseconds(120);

    // Only these boards belong in a Commander mana-base analysis; a sideboard / maybeboard
    // is not part of the 100-card deck and would skew the land target.
    private static readonly HashSet<string> AnalyzedBoards =
        new(StringComparer.OrdinalIgnoreCase) { "mainboard", "commander" };

    /// <summary>Resolve a deck and print its mana-base report. Returns a process exit code.</summary>
    /// <param name="archidektUrl">Public Archidekt deck URL, or null.</param>
    /// <param name="moxfieldUrl">Public Moxfield deck URL, or null.</param>
    /// <param name="mode">Analysis profile: "casual" (default) or "cedh".</param>
    /// <param name="includeSwapPrompt">When true, also print the paste-ready LLM swap prompt.</param>
    public static async Task<int> RunAsync(
        string? archidektUrl,
        string? moxfieldUrl,
        string mode = "casual",
        bool includeSwapPrompt = false)
    {
        bool hasArchidekt = !string.IsNullOrWhiteSpace(archidektUrl);
        bool hasMoxfield = !string.IsNullOrWhiteSpace(moxfieldUrl);
        if (hasArchidekt == hasMoxfield)
        {
            Console.Error.WriteLine("Specify exactly one of --archidekt-url or --moxfield-url.");
            return 1;
        }

        if (!TryParseMode(mode, out ManabaseMode manabaseMode))
        {
            Console.Error.WriteLine("--mode must be 'casual' or 'cedh'.");
            return 1;
        }

        try
        {
            List<DeckEntry> entries = hasArchidekt
                ? await DeckCommandRunners.LoadArchidektEntriesAsync(null, archidektUrl)
                : await DeckCommandRunners.LoadMoxfieldEntriesAsync(null, moxfieldUrl);

            // Keep only the boards that make up the deck under analysis.
            var deckCards = entries
                .Where(e => AnalyzedBoards.Contains(e.Board))
                .ToList();

            if (deckCards.Count == 0)
            {
                Console.Error.WriteLine("No mainboard/commander cards found in the deck.");
                return 2;
            }

            // Resolve each distinct card once. Prefer an exact printing (set + collector
            // number) so alternate / flavor / accented card names still resolve; fall back
            // to a plain name identifier when the entry carries no printing.
            var identifiers = deckCards
                .Select(CardIdentifier.ForEntry)
                .Distinct()
                .ToList();

            (var index, var notFound) = await ResolveCardsAsync(identifiers);

            var deckEntries = new List<DeckCardEntry>();
            var unresolved = new List<string>();
            foreach (DeckEntry entry in deckCards)
            {
                if (index.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out ScryfallCardData? card))
                {
                    deckEntries.Add(new DeckCardEntry
                    {
                        Card = card!,
                        Quantity = entry.Quantity,
                        IsCommander = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase),
                    });
                }
                else
                {
                    unresolved.Add(entry.Name);
                }
            }

            if (deckEntries.Count == 0)
            {
                Console.Error.WriteLine("Scryfall resolved none of the deck's cards; cannot analyze.");
                return 2;
            }

            IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);

            // Mirror the production web defaults so the CLI verdict matches the live site. These
            // four flags (MQ-02 mana-quantity, MQ-03 ramp-credit-v2, MQ-05 color-aware-mulligan,
            // and 70-03b land-ramp-sim) are seeded ON in prod; the CLI has no flag store, so they
            // are pinned ON here. The health-band flags are seeded OFF in prod, so they are left at
            // their false defaults. ramp-credit-v2 + land-ramp-sim change the classifier's land
            // target/ramp credit (printed), so threading them keeps the CLI numbers aligned.
            ManabaseDeck deck = ManabaseClassifier.Classify(
                facts, isSingleton: true, rampCreditV2: true, landRampSim: true);
            ManabaseReport report = ManabaseAnalyzer.Analyze(
                deck, manabaseMode, CommanderImportance.Standard, costOverrides: null,
                useManaQuantity: true, colorAwareMulligan: true, gateRampOnCastable: true);

            // Plain-language verdict + ramp/draw advisory mirror the web tool: both are Casual-only
            // (cEDH leaves them null, matching ManabaseAnalysisService).
            ManabaseRampDrawBudget? budget = null;
            ManabaseVerdict? verdict = null;
            if (manabaseMode == ManabaseMode.Casual)
            {
                budget = ManabaseRampDrawBudgetCalculator.Calculate(deck);
                verdict = ManabaseVerdictSynthesizer.Synthesize(report, manabaseMode, budget);
            }

            PrintReport(report, verdict, budget, unresolved, notFound);

            if (includeSwapPrompt)
            {
                string decklistText = string.Join(
                    "\n",
                    deckCards.Select(e => $"{e.Quantity} {e.Name}"));
                Console.WriteLine();
                Console.WriteLine("--- ChatGPT swap prompt ---");
                Console.WriteLine(ManabaseSwapPromptBuilder.Build(
                    report, deckName: null, decklistText, manabaseMode, verdict, budget));
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    // Batch-resolve identifiers through Scryfall's collection endpoint. Returns a card index
    // (keyed by printing + name) and labels for the identifiers Scryfall could not find.
    private static async Task<(ScryfallCardNameIndex Index, List<string> NotFound)> ResolveCardsAsync(
        IReadOnlyList<CardIdentifier> identifiers)
    {
        var client = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://api.scryfall.com"),
            ThrowOnAnyError = false,
            // Bound each request so a stalled connection can't hang the CLI indefinitely.
            Timeout = TimeSpan.FromSeconds(30),
        });
        client.AddDefaultHeader("User-Agent", "DeckFlow.CLI/1.0 (+https://github.com/luntc1972/DeckFlow)");
        client.AddDefaultHeader("Accept", "application/json;q=0.9,*/*;q=0.8");

        var index = new ScryfallCardNameIndex();
        var notFound = new List<string>();

        for (int offset = 0; offset < identifiers.Count; offset += CollectionBatchSize)
        {
            if (offset > 0)
            {
                await Task.Delay(BatchDelay);
            }

            var batch = identifiers.Skip(offset).Take(CollectionBatchSize).ToList();
            var body = new CollectionRequest(batch);

            var request = new RestRequest("cards/collection", Method.Post);
            request.AddJsonBody(body);

            RestResponse<CollectionResponse> response = await client.ExecuteAsync<CollectionResponse>(request);
            if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300 || response.Data is null)
            {
                throw new InvalidOperationException(
                    $"Scryfall collection lookup failed with HTTP {(int)response.StatusCode}.");
            }

            foreach (ScryfallCardData card in response.Data.Data)
            {
                index.Add(card);
            }

            foreach (CardIdentifier missing in response.Data.NotFound ?? new List<CardIdentifier>())
            {
                notFound.Add(missing.Label);
            }
        }

        return (index, notFound);
    }

    private static void PrintReport(
        ManabaseReport report,
        ManabaseVerdict? verdict,
        ManabaseRampDrawBudget? budget,
        IReadOnlyList<string> unresolved,
        IReadOnlyList<string> notFound)
    {
        Console.WriteLine();
        Console.WriteLine("=== Mana-base analysis (Karsten §6) ===");
        Console.WriteLine();
        WriteInvariant($"Lands: {report.ActualLands}  vs target ~{report.TargetLands:F1}  (delta {report.LandDelta:+0.0;-0.0;0.0})");
        WriteInvariant($"Health: {(report.IsHealthy ? "OK" : "needs work")}");
        Console.WriteLine();
        Console.WriteLine("Color  Sources  Needed  Deficit  Driving spell");
        Console.WriteLine("-----  -------  ------  -------  -------------");
        foreach (ColorSourceFinding f in report.ColorFindings)
        {
            WriteInvariant($"{f.Color,-5}  {f.ActualSources,7:F1}  {f.RequiredSources,6}  {f.Deficit,7:+0.0;-0.0;0.0}  {f.DrivingSpell}");
        }

        Console.WriteLine();
        Console.WriteLine(report.Summary);

        if (verdict is not null)
        {
            Console.WriteLine();
            Console.WriteLine(verdict.Headline);
            if (verdict.HasIssues)
            {
                foreach (string line in verdict.Lines)
                {
                    Console.WriteLine($"- {line}");
                }
            }
            else
            {
                Console.WriteLine(verdict.NoIssueReason);
            }
        }

        if (budget is not null)
        {
            Console.WriteLine();
            WriteInvariant(
                $"Ramp/draw budget: {budget.RampCount:0.#} ramp / {budget.DrawCount:0.#} draw (target ~{budget.TargetRamp}/{budget.TargetDraw}).");
            if (budget.IsRampLight)
            {
                WriteInvariant($"  Ramp looks light — about {budget.RampShort} more ramp piece(s) suggested.");
            }
            else if (budget.IsRampHeavy)
            {
                Console.WriteLine("  Ramp looks heavy for this curve.");
            }
            if (budget.IsDrawLight)
            {
                WriteInvariant($"  Card draw looks light — about {budget.DrawShort} more draw piece(s) suggested.");
            }
        }

        if (notFound.Count > 0)
        {
            Console.WriteLine();
            WriteInvariant($"Scryfall could not find {notFound.Count} name(s): {string.Join(", ", notFound)}");
        }

        if (unresolved.Count > 0)
        {
            Console.WriteLine();
            WriteInvariant($"Skipped {unresolved.Count} unmatched entry/entries: {string.Join(", ", unresolved)}");
        }
    }

    // Console.WriteLine lacks an IFormatProvider overload; format invariantly first so
    // decimals render with a "." regardless of the host culture.
    private static void WriteInvariant(FormattableString line) =>
        Console.WriteLine(line.ToString(CultureInfo.InvariantCulture));

    // Map the --mode option to the Core enum. Case-insensitive; anything else is rejected.
    private static bool TryParseMode(string mode, out ManabaseMode parsed)
    {
        switch (mode?.Trim().ToLowerInvariant())
        {
            case "casual":
            case "":
            case null:
                parsed = ManabaseMode.Casual;
                return true;
            case "cedh":
                parsed = ManabaseMode.Cedh;
                return true;
            default:
                parsed = ManabaseMode.Casual;
                return false;
        }
    }

    /// <summary>Scryfall <c>cards/collection</c> request body.</summary>
    private sealed record CollectionRequest(
        [property: JsonPropertyName("identifiers")] IReadOnlyList<CardIdentifier> Identifiers);

    /// <summary>
    /// A Scryfall collection identifier: either a printing (set + collector number) or a
    /// name. Null members are omitted from the JSON so a printing identifier doesn't carry
    /// an empty name. Also used to read the not-found echo back.
    /// </summary>
    private sealed record CardIdentifier(
        [property: JsonPropertyName("name")][property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
        [property: JsonPropertyName("set")][property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Set = null,
        [property: JsonPropertyName("collector_number")][property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CollectorNumber = null)
    {
        /// <summary>Build the best identifier for a deck entry: printing when known, else name.</summary>
        public static CardIdentifier ForEntry(DeckEntry entry) =>
            !string.IsNullOrWhiteSpace(entry.SetCode) && !string.IsNullOrWhiteSpace(entry.CollectorNumber)
                ? new CardIdentifier(Set: entry.SetCode, CollectorNumber: entry.CollectorNumber)
                : new CardIdentifier(Name: entry.Name);

        /// <summary>Human-readable label for diagnostics (the not-found list).</summary>
        public string Label => Name ?? $"{Set} #{CollectorNumber}";
    }

    /// <summary>Scryfall <c>cards/collection</c> response.</summary>
    private sealed record CollectionResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ScryfallCardData> Data,
        [property: JsonPropertyName("not_found")] IReadOnlyList<CardIdentifier>? NotFound);
}
