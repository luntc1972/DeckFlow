using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabAdjustmentApplier"/> covering shared immutable quantity-tuning rules.</summary>
public sealed class CutLabAdjustmentApplierTests
{
    [Fact]
    public void Apply_PositiveDeltaForLegalMultiple_AddsAdjustment()
    {
        CutLabState state = BuildState(
            new CutLabPoolCard
            {
                Name = "Forest",
                Quantity = 10,
                TypeLine = "Basic Land — Forest",
            });

        CutLabState updated = CutLabAdjustmentApplier.Apply(state, "Forest", 1, isAddedBasic: false);

        CutLabQuantityAdjustment adjustment = Assert.Single(updated.QuantityAdjustments);
        Assert.Equal("Forest", adjustment.Name);
        Assert.Equal(1, adjustment.Delta);
        Assert.False(adjustment.IsAddedBasic);
        CutLabPoolCard forest = Assert.Single(CutLabWorkingList.Derive(updated.Pool, updated.Decisions, updated.QuantityAdjustments), card => card.Name == "Forest");
        Assert.Equal(11, forest.Quantity);
    }

    [Fact]
    public void Apply_NetZeroDelta_RemovesAdjustmentEntry()
    {
        CutLabState state = BuildState(
            new CutLabPoolCard
            {
                Name = "Forest",
                Quantity = 10,
                TypeLine = "Basic Land — Forest",
            }) with
        {
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = 1,
                },
            ],
        };

        CutLabState updated = CutLabAdjustmentApplier.Apply(state, "Forest", -1, isAddedBasic: false);

        Assert.Empty(updated.QuantityAdjustments);
    }

    [Fact]
    public void Apply_PositiveDeltaForSingleton_ThrowsNoChange()
    {
        CutLabState state = BuildState();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CutLabAdjustmentApplier.Apply(state, "Sol Ring", 1, isAddedBasic: false));

        Assert.Equal(CutLabMessages.NoChangeMessage, exception.Message);
    }

    [Fact]
    public void Apply_AddBasicForWhitelistedName_MaterializesAddedBasic()
    {
        CutLabState state = BuildState();

        CutLabState updated = CutLabAdjustmentApplier.Apply(state, "Island", 2, isAddedBasic: true);

        CutLabQuantityAdjustment adjustment = Assert.Single(updated.QuantityAdjustments);
        Assert.Equal("Island", adjustment.Name);
        Assert.Equal(2, adjustment.Delta);
        Assert.True(adjustment.IsAddedBasic);
        CutLabPoolCard island = Assert.Single(CutLabWorkingList.Derive(updated.Pool, updated.Decisions, updated.QuantityAdjustments), card => card.Name == "Island");
        Assert.Equal(2, island.Quantity);
    }

    [Fact]
    public void Apply_AddBasicForNonBasic_ThrowsNoChange()
    {
        CutLabState state = BuildState();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => CutLabAdjustmentApplier.Apply(state, "Sol Ring", 1, isAddedBasic: true));

        Assert.Equal(CutLabMessages.NoChangeMessage, exception.Message);
    }

    [Fact]
    public void Apply_AddedBasicDelta_ClampsToLegalBounds()
    {
        CutLabState state = BuildState();

        CutLabState increased = CutLabAdjustmentApplier.Apply(state, "Island", int.MaxValue, isAddedBasic: true);
        CutLabQuantityAdjustment addedBasic = Assert.Single(increased.QuantityAdjustments);
        Assert.Equal(CutLabLegality.LegalMax("Island"), addedBasic.Delta);

        CutLabState decreased = CutLabAdjustmentApplier.Apply(increased, "Island", int.MinValue, isAddedBasic: true);
        Assert.Empty(decreased.QuantityAdjustments);
    }

    [Fact]
    public void Apply_RepeatedLargeDeltas_ClampNetInLongWithoutWrapping()
    {
        CutLabState state = BuildState(
            new CutLabPoolCard
            {
                Name = "Forest",
                Quantity = 10,
                TypeLine = "Basic Land — Forest",
            }) with
        {
            QuantityAdjustments =
            [
                new CutLabQuantityAdjustment
                {
                    Name = "Forest",
                    Delta = 150,
                },
            ],
        };

        CutLabState updated = CutLabAdjustmentApplier.Apply(state, "Forest", int.MaxValue, isAddedBasic: false);

        CutLabQuantityAdjustment adjustment = Assert.Single(updated.QuantityAdjustments);
        Assert.Equal(CutLabLegality.LegalMax("Forest"), adjustment.Delta);
        CutLabPoolCard forest = Assert.Single(CutLabWorkingList.Derive(updated.Pool, updated.Decisions, updated.QuantityAdjustments), card => card.Name == "Forest");
        Assert.Equal(CutLabLegality.LegalMax("Forest"), forest.Quantity);
        Assert.Equal(0, CutLabWorkingList.Derive(updated.Pool, updated.Decisions, updated.QuantityAdjustments).Sum(card => card.Quantity) - 100 > int.MaxValue ? 1 : 0);
    }

    [Fact]
    public void AdjustApiModels_ExposeExpectedInitProperties()
    {
        CutLabAdjustApiRequest request = new()
        {
            CutLabStateJson = "{}",
            CardName = "Island",
            Delta = 2,
            IsAddedBasic = true,
        };
        CutLabAdjustApiResponse response = new()
        {
            CutLabStateJson = "{}",
            CardsRemaining = 3,
        };

        Assert.Equal("{}", request.CutLabStateJson);
        Assert.Equal("Island", request.CardName);
        Assert.Equal(2, request.Delta);
        Assert.True(request.IsAddedBasic);
        Assert.Equal("{}", response.CutLabStateJson);
        Assert.Equal(3, response.CardsRemaining);
    }

    private static CutLabState BuildState(params CutLabPoolCard[] extraPoolCards)
        => new()
        {
            Commander = "Commander",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Commander",
                    Quantity = 1,
                    TypeLine = "Legendary Creature",
                    IsCommander = true,
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Sol Ring",
                    Quantity = 1,
                    TypeLine = "Artifact",
                },
                new CutLabPoolCard
                {
                    Name = "Counterspell",
                    Quantity = 98,
                    TypeLine = "Instant",
                    IsLocked = true,
                },
                .. extraPoolCards,
            ],
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
                Bracket = 3,
            },
        };
}
