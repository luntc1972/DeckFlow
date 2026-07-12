using System.Collections.Generic;

using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Plan-01 scaffold for the per-trial conditional-count-land simulator primitive.
/// </summary>
public sealed class ManabaseConditionalCountLandTests
{
    [Fact]
    public void FastLand_OtherLandsAtOrBelowTwo_EntersUntapped()
    {
        bool untapped = CastabilitySimulator.ConditionalCountLandEntersUntappedForTest(
            CountConditionKind.FastLand,
            2,
            System.Array.Empty<string>(),
            new List<IReadOnlyList<string>>
            {
                new[] { "Island" },
                new[] { "Plains" },
            });

        Assert.True(untapped);
    }

    [Fact]
    public void FastLand_OtherLandsAtOrAboveThree_EntersTapped()
    {
        bool untapped = CastabilitySimulator.ConditionalCountLandEntersUntappedForTest(
            CountConditionKind.FastLand,
            2,
            System.Array.Empty<string>(),
            new List<IReadOnlyList<string>>
            {
                new[] { "Island" },
                new[] { "Plains" },
                new[] { "Swamp" },
            });

        Assert.False(untapped);
    }

    [Fact]
    public void SlowLand_OtherLandsBelowTwo_EntersTapped()
    {
        bool untapped = CastabilitySimulator.ConditionalCountLandEntersUntappedForTest(
            CountConditionKind.SlowLand,
            2,
            System.Array.Empty<string>(),
            new List<IReadOnlyList<string>>
            {
                new[] { "Island" },
            });

        Assert.False(untapped);
    }

    [Fact]
    public void SlowLand_OtherLandsAtOrAboveTwo_EntersUntapped()
    {
        bool untapped = CastabilitySimulator.ConditionalCountLandEntersUntappedForTest(
            CountConditionKind.SlowLand,
            2,
            System.Array.Empty<string>(),
            new List<IReadOnlyList<string>>
            {
                new[] { "Island" },
                new[] { "Plains" },
            });

        Assert.True(untapped);
    }

    [Fact]
    public void EldThresholdLand_ThreeOtherNamedBasicTypes_EntersUntapped()
    {
        bool untapped = CastabilitySimulator.ConditionalCountLandEntersUntappedForTest(
            CountConditionKind.EldThreshold,
            3,
            new[] { "Island" },
            new List<IReadOnlyList<string>>
            {
                new[] { "Island" },
                new[] { "Island" },
                new[] { "Island" },
                new[] { "Plains" },
            });

        Assert.True(untapped);
    }

    [Fact]
    public void EldThresholdLand_BelowThreeNamedBasicTypes_EntersTapped()
    {
        bool untapped = CastabilitySimulator.ConditionalCountLandEntersUntappedForTest(
            CountConditionKind.EldThreshold,
            3,
            new[] { "Island" },
            new List<IReadOnlyList<string>>
            {
                new[] { "Island" },
                new[] { "Island" },
                new[] { "Plains" },
            });

        Assert.False(untapped);
    }
}
