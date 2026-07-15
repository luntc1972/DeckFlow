using System.Collections.Generic;
using System.Linq;

using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates the density-graded conditional-Mox post-pass applied to classified mana sources.
/// </summary>
public sealed class ConditionalMoxHeuristicsTests
{
    private static readonly IReadOnlyList<ManaColor> AllColors =
        new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green };

    [Fact]
    public void Apply_MoxAmber_ReliableLegendDensity_KeepsFastManaAndCapsColorsToCommanderIdentity()
    {
        var amber = Mox("Mox Amber");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { amber },
            fastMana: 1,
            commanderColorMask: ColorMask(ManaColor.White, ManaColor.Blue),
            legendaryPermanentCount: 12,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(new[] { ManaColor.White, ManaColor.Blue }, adjusted.Produces);
        Assert.True(adjusted.EntersUntapped);
        Assert.Equal(0.75, adjusted.Weight);
        Assert.Equal(1, fastMana);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(11)]
    public void Apply_MoxAmber_MidLegendDensity_DropsFastManaAndTurnsOffUntapped(int legendaryPermanentCount)
    {
        var amber = Mox("Mox Amber");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { amber },
            fastMana: 1,
            commanderColorMask: ColorMask(ManaColor.White, ManaColor.Blue),
            legendaryPermanentCount: legendaryPermanentCount,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(new[] { ManaColor.White, ManaColor.Blue }, adjusted.Produces);
        Assert.False(adjusted.EntersUntapped);
        Assert.Equal(0.60, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_MoxAmber_WeakLegendDensity_FallsBackToFiveColorsWhenCommanderMaskIsEmpty()
    {
        var amber = Mox("Mox Amber");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { amber },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 5,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.False(adjusted.EntersUntapped);
        Assert.Equal(0.40, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_MoxOpal_ReliableArtifactDensity_KeepsFastMana()
    {
        var opal = Mox("Mox Opal");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { opal },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: 15);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.True(adjusted.EntersUntapped);
        Assert.Equal(0.75, adjusted.Weight);
        Assert.Equal(1, fastMana);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(14)]
    public void Apply_MoxOpal_MidArtifactDensity_DropsFastManaAndTurnsOffUntapped(double effectiveArtifactSupport)
    {
        var opal = Mox("Mox Opal");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { opal },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: effectiveArtifactSupport);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.False(adjusted.EntersUntapped);
        Assert.Equal(0.60, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_MoxOpal_WeakArtifactDensity_DropsFastManaAndUsesWeakWeight()
    {
        var opal = Mox("Mox Opal");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { opal },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: 7);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.False(adjusted.EntersUntapped);
        Assert.Equal(0.40, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_ChromeMox_CapsColorsToCommanderIdentityAndDropsFastMana()
    {
        var chrome = Mox("Chrome Mox");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { chrome },
            fastMana: 1,
            commanderColorMask: ColorMask(ManaColor.White, ManaColor.Blue),
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(new[] { ManaColor.White, ManaColor.Blue }, adjusted.Produces);
        Assert.True(adjusted.EntersUntapped);
        Assert.Equal(0.50, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_ChromeMox_EmptyCommanderMask_FallsBackToFiveColors()
    {
        var chrome = Mox("Chrome Mox");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { chrome },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.True(adjusted.EntersUntapped);
        Assert.Equal(0.50, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_MoxTantalite_StaysFiveColorTappedSourceWithoutFastMana()
    {
        var tantalite = Mox("Mox Tantalite");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { tantalite },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.False(adjusted.EntersUntapped);
        Assert.Equal(0.50, adjusted.Weight);
        Assert.Equal(0, fastMana);
    }

    [Fact]
    public void Apply_MoxDiamond_StaysReliableFastMana()
    {
        var diamond = Mox("Mox Diamond");

        (IReadOnlyList<ManaSource> sources, int fastMana) = ConditionalMoxHeuristics.Apply(
            new[] { diamond },
            fastMana: 1,
            commanderColorMask: 0,
            legendaryPermanentCount: 0,
            effectiveArtifactSupport: 0);

        ManaSource adjusted = Assert.Single(sources);
        Assert.Equal(AllColors, adjusted.Produces);
        Assert.True(adjusted.EntersUntapped);
        Assert.Equal(0.75, adjusted.Weight);
        Assert.Equal(1, fastMana);
    }

    private static ManaSource Mox(string name) => new()
    {
        Name = name,
        Produces = AllColors,
        IsLand = false,
        Weight = 0.75,
        EntersUntapped = true,
    };

    private static int ColorMask(params ManaColor[] colors)
        => colors.Aggregate(0, (mask, color) => mask | color switch
        {
            ManaColor.White => 1 << 0,
            ManaColor.Blue => 1 << 1,
            ManaColor.Black => 1 << 2,
            ManaColor.Red => 1 << 3,
            ManaColor.Green => 1 << 4,
            _ => 0,
        });
}
