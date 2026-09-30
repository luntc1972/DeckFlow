using System.Text.Json;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ManabaseAnalyzerTimingTests
{
    [Fact]
    public void Analyze_ReportsSubStageTimings_WithoutChangingReport()
    {
        ManabaseDeck deck = BuildFixtureDeck();
        var timings = new List<(string Name, long ElapsedMilliseconds)>();

        ManabaseReport withoutTiming = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Casual,
            keepShapes: true,
            trialsOverride: 500);
        ManabaseReport withTiming = ManabaseAnalyzer.Analyze(
            deck,
            ManabaseMode.Casual,
            keepShapes: true,
            trialsOverride: 500,
            timingHook: timing => timings.Add((timing.Name, timing.ElapsedMilliseconds)));

        Assert.Equal(JsonSerializer.Serialize(withoutTiming), JsonSerializer.Serialize(withTiming));
        Assert.Contains(timings, timing => timing.Name == "castability per-spell loop");
        Assert.Contains(timings, timing => timing.Name == "color findings");
        Assert.Contains(timings, timing => timing.Name == "source search probes");
        Assert.Contains(timings, timing => timing.Name == "boundary confirms");
        Assert.Contains(timings, timing => timing.Name == "plan presence");
        Assert.Contains(timings, timing => timing.Name == "curve coverage");
        Assert.All(timings, timing => Assert.True(timing.ElapsedMilliseconds >= 0));
    }

    [Fact(Skip = "Manual Release benchmark; remove Skip locally to run")]
    public void Analyze_RepresentativeCedhDeck_PrintsMedianMilliseconds()
    {
        ManabaseDeck deck = BuildRepresentativeCedhDeck();
        _ = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh, keepShapes: true);
        long[] elapsed = new long[5];
        for (int run = 0; run < elapsed.Length; run++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _ = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Cedh, keepShapes: true);
            watch.Stop();
            elapsed[run] = watch.ElapsedMilliseconds;
        }

        Array.Sort(elapsed);
        Console.WriteLine($"Representative cEDH median: {elapsed[elapsed.Length / 2]} ms");
    }

    private static ManabaseDeck BuildRepresentativeCedhDeck()
    {
        ManaColor[] colors = [ManaColor.Blue, ManaColor.Black, ManaColor.Red];
        var sources = new List<ManaSource>();
        for (int index = 0; index < 34; index++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Land {index}",
                IsLand = true,
                Produces = index < 27
                    ? [colors[index % colors.Length]]
                    : [colors[index % colors.Length], colors[(index + 1) % colors.Length]],
            });
        }

        for (int index = 0; index < 8; index++)
        {
            sources.Add(new ManaSource
            {
                Name = $"Mana rock {index}",
                IsLand = false,
                Weight = 0.75,
                Produces = [colors[index % colors.Length]],
            });
        }

        var spells = new List<SpellRequirement>();
        for (int index = 0; index < 57; index++)
        {
            ManaColor primary = colors[index % colors.Length];
            ManaColor secondary = colors[(index + 1) % colors.Length];
            spells.Add(new SpellRequirement
            {
                Name = $"Spell {index}",
                ManaValue = index % 3 == 0 ? 3 : 2 + index % 2,
                IsCommander = index == 0,
                PlanRoles = index % 8 == 0 ? PlanRole.Engine : PlanRole.None,
                Pips = new Dictionary<ManaColor, int>
                {
                    [primary] = index % 3 == 0 ? 2 : 1,
                    [secondary] = 1,
                },
            });
        }

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells = spells,
        };
    }

    private static ManabaseDeck BuildFixtureDeck()
    {
        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = Enumerable.Range(0, 35)
                .Select(index => new ManaSource
                {
                    Name = $"Island {index}",
                    IsLand = true,
                    Produces = new[] { ManaColor.Blue },
                })
                .ToList(),
            Spells =
            [
                new SpellRequirement
                {
                    Name = "Plan Spell",
                    ManaValue = 1,
                    Pips = new Dictionary<ManaColor, int> { [ManaColor.Blue] = 1 },
                    PlanRoles = PlanRole.Engine,
                },
            ],
        };
    }
}
