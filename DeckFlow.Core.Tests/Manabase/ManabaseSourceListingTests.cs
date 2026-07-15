using System.Collections.Generic;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Pins the additive mana-source listing projection carried on <see cref="ManabaseReport"/>.
/// </summary>
public sealed class ManabaseSourceListingTests
{
    [Fact]
    public void Analyze_ProjectsPhysicalManaSources_IntoDisplayListings()
    {
        var deck = new ManabaseDeck
        {
            TotalCards = 100,
            CommanderCount = 1,
            AverageManaValue = 2.0,
            Sources = new List<ManaSource>
            {
                new()
                {
                    Name = "Command Tower",
                    Produces = new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green },
                    IsLand = true,
                    EntersUntapped = true,
                },
                new()
                {
                    Name = "Arcane Signet",
                    Produces = new[] { ManaColor.White, ManaColor.Blue },
                    IsLand = false,
                    EntersUntapped = false,
                },
                new()
                {
                    Name = "Wastes",
                    Produces = new[] { ManaColor.Colorless },
                    ProducesColorless = true,
                    IsLand = true,
                    EntersUntapped = true,
                },
            },
            Spells = new List<SpellRequirement>
            {
                new() { Name = "Esper Sentinel", ManaValue = 1, Pips = new Dictionary<ManaColor, int> { [ManaColor.White] = 1 } },
            },
        };

        ManabaseReport report = ManabaseAnalyzer.Analyze(deck);

        Assert.Collection(
            report.ManaSourceListings,
            tower =>
            {
                Assert.Equal("Command Tower", tower.Name);
                Assert.Equal(new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green }, tower.Colors);
                Assert.True(tower.IsLand);
                Assert.True(tower.EntersUntapped);
                Assert.False(tower.ProducesColorless);
            },
            signet =>
            {
                Assert.Equal("Arcane Signet", signet.Name);
                Assert.Equal(new[] { ManaColor.White, ManaColor.Blue }, signet.Colors);
                Assert.False(signet.IsLand);
                Assert.False(signet.EntersUntapped);
                Assert.False(signet.ProducesColorless);
            },
            wastes =>
            {
                Assert.Equal("Wastes", wastes.Name);
                Assert.Equal(new[] { ManaColor.Colorless }, wastes.Colors);
                Assert.True(wastes.IsLand);
                Assert.True(wastes.EntersUntapped);
                Assert.True(wastes.ProducesColorless);
            });
    }
}
