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
    public static async Task<int> RunAsync(string? archidektUrl, string? moxfieldUrl)
    {
        bool hasArchidekt = !string.IsNullOrWhiteSpace(archidektUrl);
        bool hasMoxfield = !string.IsNullOrWhiteSpace(moxfieldUrl);
        if (hasArchidekt == hasMoxfield)
        {
            Console.Error.WriteLine("Specify exactly one of --archidekt-url or --moxfield-url.");
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

            // Resolve each distinct printed name once, then fan the result back out per entry.
            var distinctNames = deckCards
                .Select(e => e.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            (var byName, var notFound) = await ResolveCardsAsync(distinctNames);

            var deckEntries = new List<DeckCardEntry>();
            var unresolved = new List<string>();
            foreach (DeckEntry entry in deckCards)
            {
                if (TryMatch(byName, entry.Name, out ScryfallCardData? card))
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
            ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true);
            ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

            PrintReport(report, unresolved, notFound);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    // Batch-resolve names through Scryfall's collection endpoint. Returns a name->card lookup
    // (keyed by a normalized name plus the front-face name) and the list of names Scryfall
    // could not find.
    private static async Task<(Dictionary<string, ScryfallCardData> ByName, List<string> NotFound)> ResolveCardsAsync(
        IReadOnlyList<string> names)
    {
        var client = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://api.scryfall.com"),
            ThrowOnAnyError = false,
        });
        client.AddDefaultHeader("User-Agent", "DeckFlow.CLI/1.0 (+https://github.com/luntc1972/DeckFlow)");
        client.AddDefaultHeader("Accept", "application/json;q=0.9,*/*;q=0.8");

        var byName = new Dictionary<string, ScryfallCardData>(StringComparer.Ordinal);
        var notFound = new List<string>();

        for (int offset = 0; offset < names.Count; offset += CollectionBatchSize)
        {
            if (offset > 0)
            {
                await Task.Delay(BatchDelay);
            }

            var batch = names.Skip(offset).Take(CollectionBatchSize).ToList();
            var body = new CollectionRequest(batch.Select(n => new NameIdentifier(n)).ToList());

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
                Index(byName, card);
            }

            foreach (NameIdentifier missing in response.Data.NotFound ?? new List<NameIdentifier>())
            {
                if (!string.IsNullOrWhiteSpace(missing.Name))
                {
                    notFound.Add(missing.Name);
                }
            }
        }

        return (byName, notFound);
    }

    // Index a resolved card under its normalized full name and its front-face name so an
    // entry written as either "A // B" or just "A" resolves.
    private static void Index(Dictionary<string, ScryfallCardData> byName, ScryfallCardData card)
    {
        byName[Normalize(card.Name)] = card;

        int split = card.Name.IndexOf("//", StringComparison.Ordinal);
        if (split > 0)
        {
            byName[Normalize(card.Name[..split])] = card;
        }
    }

    private static bool TryMatch(
        IReadOnlyDictionary<string, ScryfallCardData> byName,
        string entryName,
        out ScryfallCardData? card)
    {
        if (byName.TryGetValue(Normalize(entryName), out ScryfallCardData? hit))
        {
            card = hit;
            return true;
        }

        int split = entryName.IndexOf("//", StringComparison.Ordinal);
        if (split > 0 && byName.TryGetValue(Normalize(entryName[..split]), out hit))
        {
            card = hit;
            return true;
        }

        card = null;
        return false;
    }

    private static string Normalize(string name) =>
        name.Trim().ToLowerInvariant();

    private static void PrintReport(
        ManabaseReport report,
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

    /// <summary>Scryfall <c>cards/collection</c> request body.</summary>
    private sealed record CollectionRequest(
        [property: JsonPropertyName("identifiers")] IReadOnlyList<NameIdentifier> Identifiers);

    /// <summary>A single name identifier for a collection lookup (and the not-found echo).</summary>
    private sealed record NameIdentifier(
        [property: JsonPropertyName("name")] string Name);

    /// <summary>Scryfall <c>cards/collection</c> response.</summary>
    private sealed record CollectionResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ScryfallCardData> Data,
        [property: JsonPropertyName("not_found")] IReadOnlyList<NameIdentifier>? NotFound);
}
