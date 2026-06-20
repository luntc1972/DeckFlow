using System.Collections.Generic;
using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates <see cref="ManabaseSwapPromptBuilder"/>: the prompt frames the flagged color
/// deficits, the deck name, and the decklist for an LLM.
/// </summary>
public sealed class ManabaseSwapPromptBuilderTests
{
    private static ManabaseReport ReportWithDeficit() => new()
    {
        ActualLands = 34,
        TargetLands = 37.5,
        Summary = "test",
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 12,
                RequiredSources = 15,
                DrivingSpell = "Swords to Plowshares",
            },
        },
    };

    [Fact]
    public void Build_IncludesDeckName_DeficitColor_AndDecklist()
    {
        string prompt = ManabaseSwapPromptBuilder.Build(ReportWithDeficit(), "My Deck", "1 Plains\n1 Island");

        Assert.Contains("My Deck", prompt);
        Assert.Contains("White", prompt);
        Assert.Contains("Swords to Plowshares", prompt);
        Assert.Contains("1 Plains", prompt);
        Assert.Contains("add ~", prompt); // a land recommendation surfaced
    }

    [Fact]
    public void Build_HealthyDeck_StatesAdequate()
    {
        var healthy = new ManabaseReport
        {
            ActualLands = 38,
            TargetLands = 37.5,
            Summary = "ok",
            ColorFindings = new List<ColorSourceFinding>
            {
                new() { Color = ManaColor.Blue, ActualSources = 20, RequiredSources = 15, DrivingSpell = "Counterspell" },
            },
        };

        string prompt = ManabaseSwapPromptBuilder.Build(healthy, null, null);

        Assert.Contains("healthy", prompt);
        Assert.DoesNotContain("Decklist:", prompt); // no decklist supplied
    }
}
