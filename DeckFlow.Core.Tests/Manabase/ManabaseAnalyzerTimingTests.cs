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
