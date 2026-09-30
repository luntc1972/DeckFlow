using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class ManabaseReportGoldenTests
{
    [Theory]
    [InlineData(ManabaseMode.Cedh, "cedh-report.golden.json")]
    [InlineData(ManabaseMode.Casual, "casual-report.golden.json")]
    public void Analyze_Fixture_ReportMatchesGolden(ManabaseMode mode, string goldenFile)
    {
        string actual = Render(mode);
        string path = GoldenPath(goldenFile);
        Assert.Equal(File.ReadAllText(path), actual);
    }

    [Fact]
    public void Analyze_CedhFixture_PrintsMedianMilliseconds()
    {
        const int iterations = 3;
        var elapsed = new long[iterations];
        for (int index = 0; index < iterations; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            _ = Render(ManabaseMode.Cedh);
            stopwatch.Stop();
            elapsed[index] = stopwatch.ElapsedMilliseconds;
        }

        Array.Sort(elapsed);
        Console.WriteLine($"Manabase cEDH median: {elapsed[iterations / 2]} ms");
    }

    private static string Render(ManabaseMode mode)
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildFixtureDeck(), mode, trialsOverride: 500);
        return JsonSerializer.Serialize(report);
    }

    private static string GoldenPath(string file, [CallerFilePath] string sourcePath = "")
        => Path.Combine(Path.GetDirectoryName(sourcePath)!, file);

    private static ManabaseDeck BuildFixtureDeck()
    {
        ManaColor[] colors = [ManaColor.Blue, ManaColor.Red, ManaColor.Green];
        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = Enumerable.Range(0, 33)
                .Select(index => new ManaSource
                {
                    Name = $"Fixture land {index}",
                    IsLand = true,
                    Produces = [colors[index % colors.Length]],
                })
                .ToList(),
            Spells =
            [
                new SpellRequirement
                {
                    Name = "Blue engine",
                    ManaValue = 2,
                    Pips = new Dictionary<ManaColor, int> { [ManaColor.Blue] = 2 },
                    PlanRoles = PlanRole.Engine,
                },
                new SpellRequirement
                {
                    Name = "Red interaction",
                    ManaValue = 1,
                    Pips = new Dictionary<ManaColor, int> { [ManaColor.Red] = 1 },
                    PlanRoles = PlanRole.Interaction,
                },
                new SpellRequirement
                {
                    Name = "Green finisher",
                    ManaValue = 3,
                    Pips = new Dictionary<ManaColor, int> { [ManaColor.Green] = 2 },
                    PlanRoles = PlanRole.Payoff,
                },
            ],
        };
    }
}
