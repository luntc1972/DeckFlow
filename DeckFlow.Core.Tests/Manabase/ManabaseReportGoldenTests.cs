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
    public void Analyze_ProductionCedhFixture_ReportMatchesGolden()
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildProductionCedhFixtureDeck(), ManabaseMode.Cedh,
            useManaQuantity: true, colorAwareMulligan: true, gateRampOnCastable: true,
            ritualBurst: true, ritualLandCredit: true, scryCredit: true,
            colorlessSnow: true, keepShapes: true, trialsOverride: 500);
        string actual = JsonSerializer.Serialize(report);
        string path = GoldenPath("cedh-production-report.golden.json");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static string Render(ManabaseMode mode)
    {
        ManabaseReport report = ManabaseAnalyzer.Analyze(
            BuildFixtureDeck(), mode, trialsOverride: 500);
        return JsonSerializer.Serialize(report);
    }

    private static string GoldenPath(string file, [CallerFilePath] string sourcePath = "")
        => Path.Combine(Path.GetDirectoryName(sourcePath)!, file);

    private static ManabaseDeck BuildProductionCedhFixtureDeck()
    {
        var sources = Enumerable.Range(0, 33)
            .Select(index => new ManaSource
            {
                Name = $"Fixture land {index}",
                IsLand = true,
                EntersUntapped = index != 0,
                ManaAmount = index == 1 ? 2 : 1,
                Produces = [index < 16 ? ManaColor.Black : ManaColor.Red],
            })
            .ToList();
        sources.Add(new ManaSource { Name = "Sol Ring", IsLand = false, ManaAmount = 2, Produces = [ManaColor.Colorless] });
        sources.Add(new ManaSource { Name = "Charcoal Diamond", IsLand = false, Weight = 0.75, Produces = [ManaColor.Black] });
        sources.Add(new ManaSource { Name = "Blood Pet", IsLand = false, Weight = 0.5, Produces = [ManaColor.Black] });

        return new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.5,
            Sources = sources,
            Spells =
            [
                new SpellRequirement { Name = "Sol Ring", ManaValue = 1, Pips = new Dictionary<ManaColor, int>(), IsManaSource = true },
                new SpellRequirement { Name = "Charcoal Diamond", ManaValue = 2, Pips = new Dictionary<ManaColor, int>(), IsManaSource = true },
                new SpellRequirement { Name = "Blood Pet", ManaValue = 1, Pips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 1 }, IsManaSource = true },
                new SpellRequirement { Name = "Fixture commander", ManaValue = 3, Pips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 1, [ManaColor.Red] = 1 }, IsCommander = true, PlanRoles = PlanRole.Engine },
                new SpellRequirement { Name = "Black payoff", ManaValue = 3, Pips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 2 }, PlanRoles = PlanRole.Payoff },
                new SpellRequirement { Name = "Triple black payoff", ManaValue = 3, Pips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 3 }, PlanRoles = PlanRole.Payoff },
                new SpellRequirement { Name = "Ring acceleration payoff", ManaValue = 4, Pips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 1, [ManaColor.Red] = 1 }, PlanRoles = PlanRole.Payoff },
            ],
            OneShots =
            [
                new OneShotMana { Name = "Dark Ritual", ProducedColors = [ManaColor.Black], ProducedAmount = 3, OwnPips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 1 }, OwnManaValue = 1 },
                new OneShotMana { Name = "Cabal Ritual", ProducedColors = [ManaColor.Black], ProducedAmount = 3, OwnPips = new Dictionary<ManaColor, int> { [ManaColor.Black] = 1 }, OwnManaValue = 1 },
            ],
        };
    }

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
