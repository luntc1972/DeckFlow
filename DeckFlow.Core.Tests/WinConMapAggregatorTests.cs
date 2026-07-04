using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers <see cref="WinConMapAggregator"/> ranking, banding, near-combo separation, assembly-path
/// counting, closing-card classification, and the combo-data-availability sentinel.
/// </summary>
public sealed class WinConMapAggregatorTests
{
    private static WinConComboInput Combo(int? manaValueNeeded, int? popularity, params string[] cardNames)
        => new(cardNames, ["Win the game"], manaValueNeeded, popularity);

    private static WinConClosingCardInput ClosingCard(int qty, string name, string type, string oracle)
        => new(qty, name, type, oracle);

    [Fact]
    public void Compute_RanksByLowManaValueThenHighPopularity_NullManaValueLastNullPopularityLowest()
    {
        var a = Combo(2, 100, "A1", "A2");
        var b = Combo(6, 50, "B1", "B2");
        var c = Combo(2, 200, "C1", "C2");
        var noMv = Combo(null, 999, "N1");

        var map = WinConMapAggregator.Compute(
            [a, b, c, noMv],
            [],
            [],
            comboDataAvailable: true);

        Assert.Equal(4, map.Combos.Count);
        Assert.Equal(["C1", "C2"], map.Combos[0].CardNames);
        Assert.Equal(["A1", "A2"], map.Combos[1].CardNames);
        Assert.Equal(["B1", "B2"], map.Combos[2].CardNames);
        Assert.Equal(["N1"], map.Combos[3].CardNames);
    }

    [Fact]
    public void Compute_NullPopularitySortsLowestAmongEqualManaValue()
    {
        var withPopularity = Combo(2, 10, "P1");
        var noPopularity = Combo(2, null, "P2");

        var map = WinConMapAggregator.Compute(
            [noPopularity, withPopularity],
            [],
            [],
            comboDataAvailable: true);

        Assert.Equal(["P1"], map.Combos[0].CardNames);
        Assert.Equal(["P2"], map.Combos[1].CardNames);
    }

    [Fact]
    public void Compute_TiedManaValueAndPopularity_BreaksTieByOrdinalCardNames_InputOrderIndependent()
    {
        var zed = Combo(3, 40, "Zed Card");
        var alpha = Combo(3, 40, "Alpha Card");

        var forwardOrder = WinConMapAggregator.Compute([zed, alpha], [], [], comboDataAvailable: true);
        var reversedOrder = WinConMapAggregator.Compute([alpha, zed], [], [], comboDataAvailable: true);

        Assert.Equal(["Alpha Card"], forwardOrder.Combos[0].CardNames);
        Assert.Equal(["Zed Card"], forwardOrder.Combos[1].CardNames);
        Assert.Equal(["Alpha Card"], reversedOrder.Combos[0].CardNames);
        Assert.Equal(["Zed Card"], reversedOrder.Combos[1].CardNames);
    }

    /// <summary>
    /// Codex LOW finding #4: the tie-break key must be built from a NORMALIZED (trimmed,
    /// case-insensitively ordered) copy of each combo's card names, not the raw arrival order. Two
    /// logically-identical combos whose card names arrive in a different intra-combo order/casing
    /// must rank identically relative to a third, distinctly-named combo -- proving the tie-break
    /// key is order/casing independent WITHOUT altering the displayed <c>CardNames</c> order.
    /// </summary>
    [Fact]
    public void Compute_SameComboCardNamesInDifferentIntraComboOrderOrCasing_TieBreaksIdentically()
    {
        var comboNamesForwardCasing = Combo(3, 40, "Kiki-Jiki, Mirror Breaker", "Restoration Angel");
        var comboNamesReversedAndMixedCasing = Combo(3, 40, "restoration angel", "KIKI-JIKI, MIRROR BREAKER");
        var distinctThirdCombo = Combo(3, 40, "Splinter Twin");

        var forwardOrder = WinConMapAggregator.Compute(
            [comboNamesForwardCasing, distinctThirdCombo],
            [],
            [],
            comboDataAvailable: true);
        var reorderedCasing = WinConMapAggregator.Compute(
            [comboNamesReversedAndMixedCasing, distinctThirdCombo],
            [],
            [],
            comboDataAvailable: true);

        // Both computations must place the (logically identical) two-card combo at the SAME rank
        // relative to the distinct third combo -- proving the sort key is order/casing independent.
        Assert.Equal(["Kiki-Jiki, Mirror Breaker", "Restoration Angel"], forwardOrder.Combos[0].CardNames);
        Assert.Equal(["Splinter Twin"], forwardOrder.Combos[1].CardNames);

        // The displayed CardNames order/casing on the output record is UNCHANGED -- only the sort
        // key is normalized, never the record itself.
        Assert.Equal(["restoration angel", "KIKI-JIKI, MIRROR BREAKER"], reorderedCasing.Combos[0].CardNames);
        Assert.Equal(["Splinter Twin"], reorderedCasing.Combos[1].CardNames);
    }

    [Theory]
    [InlineData(4, WinConBand.Early)]
    [InlineData(5, WinConBand.Mid)]
    [InlineData(7, WinConBand.Mid)]
    [InlineData(8, WinConBand.Late)]
    [InlineData(null, WinConBand.Unknown)]
    public void Compute_BandsComboByManaValueNeededThreshold(int? manaValueNeeded, WinConBand expectedBand)
    {
        var combo = Combo(manaValueNeeded, 10, "X1");

        var map = WinConMapAggregator.Compute([combo], [], [], comboDataAvailable: true);

        Assert.Equal(expectedBand, map.Combos[0].Band);
    }

    [Fact]
    public void Compute_OverallBandIsBandOfFastestCombo()
    {
        var slow = Combo(9, 10, "Slow1"); // Late
        var fast = Combo(3, 10, "Fast1"); // Early

        var map = WinConMapAggregator.Compute([slow, fast], [], [], comboDataAvailable: true);

        Assert.Equal(WinConBand.Early, map.OverallBand);
    }

    [Fact]
    public void Compute_OverallBandIsUnknownWhenAllCombosHaveNullManaValue()
    {
        var map = WinConMapAggregator.Compute(
            [Combo(null, 10, "A"), Combo(null, 20, "B")],
            [],
            [],
            comboDataAvailable: true);

        Assert.Equal(WinConBand.Unknown, map.OverallBand);
    }

    [Fact]
    public void Compute_OverallBandIsUnknownWhenNoCombos()
    {
        var map = WinConMapAggregator.Compute([], [], [], comboDataAvailable: true);

        Assert.Equal(WinConBand.Unknown, map.OverallBand);
    }

    [Fact]
    public void Compute_AssemblyPathCountEqualsIncludedComboCount_NearCombosExcluded()
    {
        var map = WinConMapAggregator.Compute(
            [Combo(2, 10, "A"), Combo(3, 10, "B")],
            [new WinConNearComboInput("Missing", ["A"], ["Infinite mana"])],
            [],
            comboDataAvailable: true);

        Assert.Equal(2, map.AssemblyPathCount);
        Assert.Equal(2, map.Combos.Count);
        Assert.Single(map.NearCombos);
    }

    [Fact]
    public void Compute_NearCombosAreStrictlySeparateFromCombos()
    {
        var map = WinConMapAggregator.Compute(
            [Combo(2, 10, "InDeckCombo")],
            [new WinConNearComboInput("MissingPiece", ["InDeckCombo", "OtherCard"], ["Win the game"])],
            [],
            comboDataAvailable: true);

        Assert.DoesNotContain(map.NearCombos, n => map.Combos.Any(c => c.CardNames.SequenceEqual(new[] { n.MissingCard })));
        Assert.Single(map.Combos);
        Assert.Single(map.NearCombos);
        Assert.Equal("MissingPiece", map.NearCombos[0].MissingCard);
    }

    [Fact]
    public void Compute_ClosingCardsIncludesOnlyClassifiedClosers_QuantityPreserved()
    {
        var map = WinConMapAggregator.Compute(
            [],
            [],
            [
                ClosingCard(2, "Craterhoof Behemoth", "Legendary Creature — Craterhoof", "Trample. When Craterhoof Behemoth enters the battlefield, creatures you control get +X/+X."),
                ClosingCard(1, "Exsanguinate", "Sorcery", "Each opponent loses X life and you gain that much life."),
                ClosingCard(3, "Grizzly Bears", "Creature — Bear", "Vanilla 2/2."),
            ],
            comboDataAvailable: true);

        Assert.Contains(map.ClosingCards, c => c.Name == "Craterhoof Behemoth" && c.Quantity == 2);
        Assert.Contains(map.ClosingCards, c => c.Name == "Exsanguinate" && c.Quantity == 1);
        Assert.DoesNotContain(map.ClosingCards, c => c.Name == "Grizzly Bears");
    }

    [Fact]
    public void Compute_ClosingCardsSkipsNonPositiveQuantity()
    {
        var map = WinConMapAggregator.Compute(
            [],
            [],
            [ClosingCard(0, "You Win The Game Card", "Sorcery", "You win the game.")],
            comboDataAvailable: true);

        Assert.Empty(map.ClosingCards);
    }

    [Fact]
    public void Compute_ComboDataUnavailable_ReturnsEmptyCombosZeroAssemblyUnknownBand_ButClosingCardsStillPopulated()
    {
        var map = WinConMapAggregator.Compute(
            [Combo(2, 10, "ShouldBeIgnored")],
            [new WinConNearComboInput("Ignored", ["X"], ["Y"])],
            [ClosingCard(1, "You Win The Game Card", "Sorcery", "You win the game.")],
            comboDataAvailable: false);

        Assert.False(map.ComboDataAvailable);
        Assert.Empty(map.Combos);
        Assert.Empty(map.NearCombos);
        Assert.Equal(0, map.AssemblyPathCount);
        Assert.Equal(WinConBand.Unknown, map.OverallBand);
        Assert.Single(map.ClosingCards);
        Assert.Equal("You Win The Game Card", map.ClosingCards[0].Name);
    }

    [Fact]
    public void Compute_ComboDataAvailableButEmpty_IsDistinctFromUnavailable()
    {
        var map = WinConMapAggregator.Compute([], [], [], comboDataAvailable: true);

        Assert.True(map.ComboDataAvailable);
        Assert.Empty(map.Combos);
        Assert.Equal(0, map.AssemblyPathCount);
        Assert.Equal(WinConBand.Unknown, map.OverallBand);
    }

    [Fact]
    public void Compute_ThrowsOnNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => WinConMapAggregator.Compute(null!, [], [], true));
        Assert.Throws<ArgumentNullException>(() => WinConMapAggregator.Compute([], null!, [], true));
        Assert.Throws<ArgumentNullException>(() => WinConMapAggregator.Compute([], [], null!, true));
    }
}
