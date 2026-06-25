using System.Text.Json.Serialization;
using DeckFlow.Core.Manabase;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Regression guard for the manabase over-optimism fix (debug session manabase-too-optimistic). The
/// Avatar (Sokka/Aang Jeskai) deck was reported at 96% avg-on-curve — ~7 pts above the independent
/// Salubrious Snail baseline (89.1%) — because the simulator deployed drawn ramp for FREE (no deploy
/// friction), the grace window forgave a 1-2 drop up to three turns late, and four free-cast cards were
/// analyzed at printed cost. After the deploy-friction ramp model + uniform-+1 grace + free-cost
/// auto-apply, the honest headline is ~94% with WHITE the weakest color (matching Snail's call, which
/// the printed-cost model got wrong as Blue).
/// <para>
/// This test resolves the deck against Scryfall live, so it is GATED on DECKFLOW_MANABASE_HARNESS=1 (or
/// a .manabase-harness-on sentinel) and is a no-op in CI / normal runs. It is the Avatar-fixture
/// regression check; the deterministic, offline mechanism guards live in <c>LandRampSimTests</c> and
/// the new deploy-friction unit tests. Band is intentionally NOT asserted (band logic is a separate,
/// deferred defect).
/// </para>
/// Run: DECKFLOW_MANABASE_HARNESS=1 dotnet.exe test DeckFlow.Core.Tests --filter AvatarManabaseRegressionTests
/// </summary>
public sealed class AvatarManabaseRegressionTests
{
    // Resolve paths relative to the repo root (found by walking up to DeckFlow.sln) so the test works
    // whether it runs under the WSL or Windows .NET host.
    private static readonly string FixturePath =
        Path.Combine(RepoRoot(), ".planning", "debug", "manabase-too-optimistic-deck.txt");

    private static readonly string[] FreeCostCards =
    {
        "Force of Negation", "Fierce Guardianship", "Deflecting Swat", "Flawless Maneuver",
    };

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeckFlow.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }

    private readonly ITestOutputHelper _out;

    public AvatarManabaseRegressionTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Avatar_PostFix_HeadlineNear94_WeakestColorWhite_FreeCardsAutoApplied()
    {
        bool enabled = Environment.GetEnvironmentVariable("DECKFLOW_MANABASE_HARNESS") == "1"
            || File.Exists(Path.Combine(RepoRoot(), ".manabase-harness-on"));
        if (!enabled)
        {
            return; // gated: skipped in normal runs (no env var, no sentinel file)
        }

        (var facts, var unresolved) = await LoadFactsAsync();
        Assert.True(facts.Count > 90,
            $"expected the Avatar deck to resolve; got {facts.Count} (unresolved: {string.Join(", ", unresolved)})");

        // Mirror the Web flag posture for the Avatar repro: all four Phase-70 manabase flags ON. The
        // land-ramp-sim flag now carries the deploy-friction + colored-cost-gate ramp model.
        ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: true, landRampSim: true);
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            deck, ManabaseMode.Casual, CommanderImportance.Standard,
            costOverrides: null, useManaQuantity: true, colorAwareMulligan: true, gateRampOnCastable: true);

        int avg = AvgOnCurve(report.Castability);
        _out.WriteLine($"avg on-curve: {avg}%   health: {report.Health}");
        ColorSourceFinding? weakest = report.ColorFindings.Count > 0 ? report.ColorFindings[0] : null;
        _out.WriteLine(weakest is null ? "weakest color: (none)" : $"weakest color: {weakest.Color}");

        // HEADLINE: the honest post-fix value measured at 94% (debug Evidence trail). A +-2 band absorbs
        // the 20k-trial Monte-Carlo noise without being so loose it would miss a regression back to the
        // old 96% over-optimism. NOT dialed to Snail's 89.1% — 94% IS the model's number.
        Assert.InRange(avg, 92, 95);
        Assert.True(avg < 96,
            $"headline must be below the pre-fix 96% over-optimism, got {avg}%");

        // WEAKEST COLOR: the printed-cost model said Blue; the corrected model agrees with Snail (White).
        Assert.NotNull(weakest);
        Assert.Equal(ManaColor.White, weakest!.Color);

        // FREE-COST AUTO-APPLY: the four self-anchored free-cast cards are recognized and cast for free
        // (MV 0, on-curve turn 1), so they are no longer false "demanding" rows at printed cost.
        foreach (string name in FreeCostCards)
        {
            CardCastability? row = report.Castability.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            Assert.True(row is not null, $"{name} should have a castability row");
            Assert.Equal(0, row!.ManaValue);
            Assert.True(row.CastPercent >= 98,
                $"{name} is free-cast and should be ~always castable, got {row.CastPercent}%");
        }
    }

    private static int AvgOnCurve(IReadOnlyList<CardCastability> rows)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        long sum = rows.Sum(r => (long)r.CastPercent);
        return (int)Math.Round((double)sum / rows.Count);
    }

    private static async Task<(IReadOnlyList<CardFact> Facts, List<string> Unresolved)> LoadFactsAsync()
    {
        var lines = (await File.ReadAllLinesAsync(FixturePath))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("//", StringComparison.Ordinal))
            .ToList();

        // Parse "<qty> <name>"; the first two card lines are the commanders (Sokka, Aang). Resolve the
        // FRONT face name (before " / ") against Scryfall.
        var parsed = new List<(int Qty, string Name)>();
        foreach (string line in lines)
        {
            int sp = line.IndexOf(' ');
            int qty = int.Parse(line[..sp]);
            string name = line[(sp + 1)..].Trim();
            int slash = name.IndexOf(" / ", StringComparison.Ordinal);
            if (slash >= 0)
            {
                name = name[..slash].Trim();
            }

            parsed.Add((qty, name));
        }

        var client = new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://api.scryfall.com"),
            ThrowOnAnyError = false,
            Timeout = TimeSpan.FromSeconds(30),
        });
        client.AddDefaultHeader("User-Agent", "DeckFlow.Harness/1.0 (+https://github.com/luntc1972/DeckFlow)");
        client.AddDefaultHeader("Accept", "application/json;q=0.9,*/*;q=0.8");

        var index = new ScryfallCardNameIndex();
        var names = parsed.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        const int batch = 75;
        for (int offset = 0; offset < names.Count; offset += batch)
        {
            if (offset > 0)
            {
                await Task.Delay(120);
            }

            object[] ids = names.Skip(offset).Take(batch).Select(n => (object)new { name = n }).ToArray();
            var req = new RestRequest("cards/collection", Method.Post);
            req.AddJsonBody(new { identifiers = ids });
            RestResponse<CollectionResponse> resp = await client.ExecuteAsync<CollectionResponse>(req);
            Assert.True((int)resp.StatusCode is >= 200 and < 300 && resp.Data is not null,
                $"Scryfall HTTP {(int)resp.StatusCode}");
            foreach (ScryfallCardData c in resp.Data!.Data)
            {
                index.Add(c);
            }
        }

        var entries = new List<DeckCardEntry>();
        var unresolved = new List<string>();
        for (int i = 0; i < parsed.Count; i++)
        {
            (int qty, string name) = parsed[i];
            if (index.TryResolve(name, out ScryfallCardData? card) && card is not null)
            {
                entries.Add(new DeckCardEntry { Card = card, Quantity = qty, IsCommander = i < 2 });
            }
            else
            {
                unresolved.Add(name);
            }
        }

        return (ScryfallCardFactMapper.ToCardFacts(entries), unresolved);
    }

    private sealed record CollectionResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<ScryfallCardData> Data);
}
