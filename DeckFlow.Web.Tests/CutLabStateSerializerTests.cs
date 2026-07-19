using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>Tests for <see cref="CutLabStateSerializer"/> covering round-trip, tamper defense, and size/error handling.</summary>
public sealed class CutLabStateSerializerTests
{
    [Fact]
    public void SerializeDeserialize_RoundTripsState_AndReLocksCommander()
    {
        var state = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = "Atraxa, Praetors' Voice",
                    Quantity = 1,
                    TypeLine = "Legendary Creature — Phyrexian Angel Horror",
                    IsCommander = true,
                    IsLocked = false,
                },
                new CutLabPoolCard
                {
                    Name = "Arcane Signet",
                    Quantity = 1,
                    TypeLine = "Artifact",
                    IsLocked = true,
                    PackageId = "ramp",
                },
            ],
            Packages =
            [
                new CutLabPackage
                {
                    Id = "ramp",
                    Name = "Ramp Core",
                    Locked = true,
                },
            ],
            RoleFloors =
            [
                new CutLabRoleFloor
                {
                    Role = "interaction",
                    Floor = 7,
                    IsUserSet = true,
                },
                new CutLabRoleFloor
                {
                    Role = "draw",
                    Floor = 12,
                    IsUserSet = false,
                },
            ],
            Intent = new CutLabIntent
            {
                PrimaryPlan = "Counters",
                SecondaryPlan = "Blink",
                Bracket = 3,
                PlayExperience = "Resilient midrange",
            },
        };

        var json = CutLabStateSerializer.Serialize(state);
        var roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.Contains("\"roleFloors\"", json);
        Assert.Equal("Atraxa, Praetors' Voice", roundTripped.Commander);
        Assert.Equal(2, roundTripped.Pool.Count);
        Assert.Equal("ramp", Assert.Single(roundTripped.Packages).Id);
        Assert.Equal(state.RoleFloors, roundTripped.RoleFloors);
        Assert.Equal("Counters", roundTripped.Intent.PrimaryPlan);
        Assert.Equal("Blink", roundTripped.Intent.SecondaryPlan);
        Assert.Equal(3, roundTripped.Intent.Bracket);
        Assert.Equal("Resilient midrange", roundTripped.Intent.PlayExperience);
        Assert.True(Assert.Single(roundTripped.Pool, card => card.IsCommander).IsLocked);
        Assert.True(Assert.Single(roundTripped.Pool, card => card.Name == "Arcane Signet").IsLocked);
    }

    [Fact]
    public void Deserialize_Pre102JsonWithoutRoleFloors_ReturnsEmptyRoleFloors_AndReLocksCommander()
    {
        const string json =
            """
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [
                {
                  "name": "Atraxa, Praetors' Voice",
                  "quantity": 1,
                  "typeLine": "Legendary Creature — Phyrexian Angel Horror",
                  "isCommander": true,
                  "isLocked": false
                },
                {
                  "name": "Arcane Signet",
                  "quantity": 1,
                  "typeLine": "Artifact",
                  "isCommander": false,
                  "isLocked": true
                }
              ],
              "packages": [],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Empty(state.RoleFloors);
        Assert.True(Assert.Single(state.Pool, card => card.IsCommander).IsLocked);
    }

    [Fact]
    public void Deserialize_TamperedRoleFloors_ClampsAndDropsInvalidEntries()
    {
        const string json =
            """
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [],
              "packages": [],
              "roleFloors": [
                {
                  "role": "wincons",
                  "floor": -3,
                  "isUserSet": true
                },
                {
                  "role": "battlecruiser",
                  "floor": 5,
                  "isUserSet": true
                }
              ],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        var floor = Assert.Single(state.RoleFloors);
        Assert.Equal("wincons", floor.Role);
        Assert.Equal(0, floor.Floor);
        Assert.True(floor.IsUserSet);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_BlankJson_ReturnsEmptyState(string? json)
    {
        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Equal(string.Empty, state.Commander);
        Assert.Empty(state.Pool);
        Assert.Empty(state.Packages);
        Assert.Equal(string.Empty, state.Intent.PrimaryPlan);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsEmptyState()
    {
        var state = CutLabStateSerializer.Deserialize("{ not-json");

        Assert.Equal(string.Empty, state.Commander);
        Assert.Empty(state.Pool);
        Assert.Empty(state.Packages);
        Assert.Equal(string.Empty, state.Intent.PlayExperience);
    }

    [Fact]
    public void Serialize_StateExceedsMaxUploadBytes_ThrowsInvalidOperationException()
    {
        var oversizedName = new string('A', CutLabStateSerializer.MaxUploadBytes);
        var state = new CutLabState
        {
            Commander = "Atraxa, Praetors' Voice",
            Pool =
            [
                new CutLabPoolCard
                {
                    Name = oversizedName,
                    Quantity = 1,
                    TypeLine = "Artifact",
                },
            ],
        };

        var exception = Assert.Throws<InvalidOperationException>(() => CutLabStateSerializer.Serialize(state));

        Assert.Equal("The Cut Lab working session is too large to save.", exception.Message);
    }
}
