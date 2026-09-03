using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Coverage for <see cref="CutLabPlanProfile"/> and <see cref="CutLabCommanderTheme"/>: legacy-JSON
/// backward compatibility, round-trip fidelity through <see cref="CutLabStateSerializer"/>, and the
/// zero-checkbox default no-op shape (PLPR-01, PLPR-02).
/// </summary>
public sealed class CutLabPlanProfileTests
{
    [Fact]
    public void Deserialize_LegacyBlobWithoutPlanProfile_PreservesLegacyPlansAndLeavesPlanProfileNull()
    {
        const string json =
            """
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [],
              "packages": [],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": "Blink",
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        CutLabState state = CutLabStateSerializer.Deserialize(json);

        Assert.Equal("Counters", state.Intent.PrimaryPlan);
        Assert.Equal("Blink", state.Intent.SecondaryPlan);
        Assert.Null(state.Intent.PlanProfile);
    }

    [Fact]
    public void SerializeDeserialize_PopulatedPlanProfile_RoundTripsStrategiesThemesAndUnavailableFlag()
    {
        var state = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            Intent = new CutLabIntent
            {
                PlayExperience = "Focused",
                PlanProfile = new CutLabPlanProfile
                {
                    GenericStrategies = ["combo", "aristocrats"],
                    CommanderThemes =
                    [
                        new CutLabCommanderTheme { Slug = "stax", DisplayName = "Stax", DeckCount = 1500 },
                    ],
                    CommanderThemesUnavailable = false,
                },
            },
        };

        string json = CutLabStateSerializer.Serialize(state);
        CutLabState roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.NotNull(roundTripped.Intent.PlanProfile);
        Assert.Equal(
            state.Intent.PlanProfile!.GenericStrategies,
            roundTripped.Intent.PlanProfile!.GenericStrategies);
        var theme = Assert.Single(roundTripped.Intent.PlanProfile.CommanderThemes);
        Assert.Equal("stax", theme.Slug);
        Assert.Equal("Stax", theme.DisplayName);
        Assert.Equal(1500, theme.DeckCount);
        Assert.Equal(
            state.Intent.PlanProfile.CommanderThemesUnavailable,
            roundTripped.Intent.PlanProfile.CommanderThemesUnavailable);
    }

    [Fact]
    public void SerializeDeserialize_CommanderThemesUnavailableTrue_RoundTrips()
    {
        var state = new CutLabState
        {
            Intent = new CutLabIntent
            {
                PlanProfile = new CutLabPlanProfile
                {
                    CommanderThemesUnavailable = true,
                },
            },
        };

        string json = CutLabStateSerializer.Serialize(state);
        CutLabState roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.True(roundTripped.Intent.PlanProfile!.CommanderThemesUnavailable);
    }

    [Fact]
    public void DefaultConstructed_PlanProfile_IsEmptyZeroCheckboxNoOpShape()
    {
        var profile = new CutLabPlanProfile();

        Assert.Empty(profile.GenericStrategies);
        Assert.Empty(profile.CommanderThemes);
        Assert.False(profile.CommanderThemesUnavailable);
    }

    [Fact]
    public void EmptyPlanProfile_IsDistinguishableFromNullPlanProfile()
    {
        var withEmptyProfile = new CutLabIntent { PlanProfile = new CutLabPlanProfile() };
        var withNullProfile = new CutLabIntent();

        Assert.NotNull(withEmptyProfile.PlanProfile);
        Assert.Null(withNullProfile.PlanProfile);
    }
}
