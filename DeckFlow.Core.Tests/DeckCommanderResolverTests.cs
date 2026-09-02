using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="DeckCommanderResolver"/>.
/// </summary>
public sealed class DeckCommanderResolverTests
{
    [Fact]
    public void ResolveCommanderName_SingleCommander_ReturnsName()
    {
        var result = DeckCommanderResolver.ResolveCommanderName(new[] { CreateEntry("Atraxa", "commander") });

        Assert.Equal("Atraxa", result);
    }

    [Fact]
    public void ResolveCommanderName_NoCommanderEntries_ReturnsNull()
    {
        var result = DeckCommanderResolver.ResolveCommanderName(new[] { CreateEntry("Sol Ring", "mainboard") });

        Assert.Null(result);
    }

    [Fact]
    public void ResolveCommanderName_PartnerPair_ReturnsAlphabeticallyFirstRegardlessOfInputOrder()
    {
        var firstOrder = DeckCommanderResolver.ResolveCommanderName(new[]
        {
            CreateEntry("Thrasios, Triton Hero", "commander"),
            CreateEntry("Tymna the Weaver", "COMMANDER"),
        });
        var secondOrder = DeckCommanderResolver.ResolveCommanderName(new[]
        {
            CreateEntry("Tymna the Weaver", "COMMANDER"),
            CreateEntry("Thrasios, Triton Hero", "commander"),
        });

        Assert.Equal("Thrasios, Triton Hero", firstOrder);
        Assert.Equal("Thrasios, Triton Hero", secondOrder);
    }

    private static DeckEntry CreateEntry(string name, string board) => new()
    {
        Name = name,
        NormalizedName = name.ToLowerInvariant(),
        Quantity = 1,
        Board = board,
    };
}
