using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Root-cause guard (efficacy R2 recommendation #6). A "live-oracle canary": it fetches a handful
/// of bellwether cards straight from Scryfall and asserts the classifier still reaches the right
/// verdict on the WORDING those cards actually ship today. Finding H1 rotted for ~a year with green
/// unit tests because every fixture pinned the stale 2020 phrasing ("enters the battlefield tapped")
/// while live data had moved to the 2024 rewording ("This land enters tapped."). Fixture-only tests
/// cannot catch that class of rot by construction — only live oracle text can.
///
/// The test is OPT-IN: it makes a network call, so it is a no-op (passes) unless the environment
/// variable <c>DECKFLOW_LIVE_ORACLE</c> is set to a non-empty value. Default <c>dotnet test</c> runs
/// stay offline and deterministic. Run it on demand or on a schedule with:
/// <code>DECKFLOW_LIVE_ORACLE=1 dotnet test --filter Category=LiveOracle</code>
/// When a card's assertion fails, the message prints the live oracle text so the reworded phrase is
/// obvious and the matching classifier predicate can be updated.
/// </summary>
[Trait("Category", "LiveOracle")]
public sealed class ManabaseLiveOracleCanaryTests
{
    private const string EnvGate = "DECKFLOW_LIVE_ORACLE";
    private const string ScryfallNamedUrl = "https://api.scryfall.com/cards/named?exact=";

    // Scryfall asks callers to identify themselves and to pace requests 50-100ms apart.
    private static readonly TimeSpan RequestSpacing = TimeSpan.FromMilliseconds(120);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public async Task LiveOracle_BellwetherCards_ClassifyAsExpected()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvGate)))
        {
            // Offline default run: skip the network call. Set DECKFLOW_LIVE_ORACLE to exercise it.
            return;
        }

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("DeckFlow-LiveOracleCanary/1.0");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var failures = new List<string>();
        foreach (Bellwether card in Bellwethers)
        {
            ScryfallCardData data = await FetchAsync(http, card.Name);
            CardFact fact = ScryfallCardFactMapper.ToCardFact(data, quantity: 1);
            ManabaseDeck deck = ManabaseClassifier.Classify(new List<CardFact> { fact });

            string? failure = card.Check(deck);
            if (failure is not null)
            {
                failures.Add($"{card.Name}: {failure}\n  live oracle text: {Oracle(data)}");
            }

            await Task.Delay(RequestSpacing);
        }

        Assert.True(failures.Count == 0,
            "Live Scryfall wording no longer matches classifier predicates — a reword likely rotted a "
            + "predicate (see finding H1). Update the matching predicate in ManabaseClassifier:\n"
            + string.Join("\n", failures));
    }

    private static async Task<ScryfallCardData> FetchAsync(HttpClient http, string cardName)
    {
        string url = ScryfallNamedUrl + Uri.EscapeDataString(cardName);
        using HttpResponseMessage response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync();
        ScryfallCardData? data = JsonSerializer.Deserialize<ScryfallCardData>(json, JsonOptions);
        Assert.NotNull(data);
        return data!;
    }

    private static string Oracle(ScryfallCardData data) =>
        data.OracleText
        ?? string.Join(" // ", data.CardFaces?.Select(f => f.OracleText ?? "") ?? Enumerable.Empty<string>());

    // A land whose sole source entry enters tapped (H1: the "enters tapped" wording guard).
    private static string? ExpectTapland(ManabaseDeck deck)
    {
        ManaSource? land = deck.Sources.FirstOrDefault(s => s.IsLand);
        if (land is null)
        {
            return "expected a land source, found none";
        }

        return land.EntersUntapped
            ? "classified UNTAPPED but this is a tapland — the 'enters tapped' predicate missed the printed wording"
            : null;
    }

    // A card that must NOT count as a permanent weighted partial source (H2: Treasure-makers and
    // one-shot sacrifice mana carry produced_mana / reminder text that used to read as a real dork).
    private static string? ExpectNotAPartialSource(ManabaseDeck deck)
    {
        bool present = deck.Sources.Any(s => !s.IsLand);
        return present
            ? "counted as a permanent partial mana source — a one-shot/Treasure ability was read as repeatable"
            : null;
    }

    // A genuine repeatable dork MUST still count (positive control: guards against over-stripping the
    // H2 fix so real mana dorks keep their 0.5 Karsten weight).
    private static string? ExpectPartialDork(ManabaseDeck deck)
    {
        ManaSource? dork = deck.Sources.FirstOrDefault(s => !s.IsLand);
        if (dork is null)
        {
            return "dropped a real repeatable mana dork — the H2 grant/one-shot filter is too aggressive";
        }

        return dork.Weight > 0.6
            ? $"weight {dork.Weight} is not the expected 0.5 mana-dork weight"
            : null;
    }

    private sealed record Bellwether(string Name, Func<ManabaseDeck, string?> Check);

    private static readonly IReadOnlyList<Bellwether> Bellwethers = new[]
    {
        // H1 — tapland wording. Azorius Guildgate and Temple of Enlightenment both print the 2024
        // "enters tapped" rewording live; either reverting would fail here.
        new Bellwether("Azorius Guildgate", ExpectTapland),
        new Bellwether("Temple of Enlightenment", ExpectTapland),

        // H2 — Treasure-maker (reminder text "...Add one mana of any color.") and one-shot sac mana
        // (Lotus Petal "{T}, Sacrifice this artifact: Add ...") must not read as permanent sources.
        new Bellwether("Dockside Extortionist", ExpectNotAPartialSource),
        new Bellwether("Lotus Petal", ExpectNotAPartialSource),

        // Positive control — a real repeatable dork must keep its 0.5 partial weight.
        new Bellwether("Llanowar Elves", ExpectPartialDork),
    };
}
