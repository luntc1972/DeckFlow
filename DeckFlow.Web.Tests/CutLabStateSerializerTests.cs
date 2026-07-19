using System.Text;
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
    public void Deserialize_OversizedJson_ReturnsEmptyState()
    {
        var oversizedName = new string('A', CutLabStateSerializer.MaxUploadBytes);
        var state = CutLabStateSerializer.Deserialize($"{{\"commander\":\"{oversizedName}\"}}");

        Assert.Equal(string.Empty, state.Commander);
        Assert.Empty(state.Pool);
        Assert.Empty(state.Packages);
    }

    [Fact]
    public void Deserialize_TamperedPackages_DropsEmptyNamesAndCapsAtFifty()
    {
        string packagesJson = string.Join(
            ",",
            Enumerable.Range(1, 52).Select(index =>
                $$"""{"id":"pkg-{{index}}","name":"Package {{index}}","locked":{{(index % 2 == 0).ToString().ToLowerInvariant()}}}"""));
        string json =
            $$"""
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [],
              "packages": [
                {"id":"blank-1","name":"","locked":false},
                {"id":"blank-2","name":"   ","locked":true},
                {{packagesJson}}
              ],
              "roleFloors": [],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Equal(50, state.Packages.Count);
        Assert.DoesNotContain(state.Packages, package => string.IsNullOrWhiteSpace(package.Name));
        Assert.Equal("pkg-1", state.Packages[0].Id);
        Assert.Equal("pkg-50", state.Packages[^1].Id);
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

    [Fact]
    public void SerializeDeserialize_RoundTripsDecisionsAndBaselineSnapshot_WithoutMutatingPool()
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
                    IsLocked = true,
                },
                new CutLabPoolCard
                {
                    Name = "Arcane Signet",
                    Quantity = 1,
                    TypeLine = "Artifact",
                    IsLocked = true,
                    PackageId = "ramp",
                },
                new CutLabPoolCard
                {
                    Name = "Brainstorm",
                    Quantity = 1,
                    TypeLine = "Instant",
                },
            ],
            Decisions =
            [
                new CutLabDecision
                {
                    CardName = "Arcane Signet",
                    Kind = CutLabDecisionKind.Accepted,
                    Round = "obvious-cuts",
                    Ordinal = 3,
                },
                new CutLabDecision
                {
                    CardName = "Brainstorm",
                    Kind = CutLabDecisionKind.Rejected,
                    Round = "structural-choices",
                    Ordinal = 4,
                },
                new CutLabDecision
                {
                    CardName = "Ponder",
                    Kind = CutLabDecisionKind.Deferred,
                    Round = "preference-calls",
                    Ordinal = 5,
                },
            ],
            BaselineSnapshot = CreateBaselineSnapshot(),
        };

        var json = CutLabStateSerializer.Serialize(state);
        var roundTripped = CutLabStateSerializer.Deserialize(json);

        Assert.Equal(state.Pool, roundTripped.Pool);
        Assert.Equal(state.Decisions, roundTripped.Decisions);
        Assert.NotNull(roundTripped.BaselineSnapshot);
        Assert.Equal(state.BaselineSnapshot!.Metrics, roundTripped.BaselineSnapshot.Metrics);
    }

    [Fact]
    public void Deserialize_DecisionsOverMax_TruncatesToFiveHundredNonBlankEntries()
    {
        string decisionsJson = string.Join(
            ",",
            Enumerable.Range(1, 503).Select(index =>
                $$"""{"cardName":"Card {{index}}","kind":0,"round":"round-1","ordinal":{{index}}}"""));
        string json =
            $$"""
            {
              "commander": "Atraxa, Praetors' Voice",
              "pool": [],
              "packages": [],
              "decisions": [
                {"cardName":"","kind":0,"round":"round-0","ordinal":0},
                {"cardName":"   ","kind":1,"round":"round-0","ordinal":-1},
                {{decisionsJson}}
              ],
              "roleFloors": [],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Equal(500, state.Decisions.Count);
        Assert.DoesNotContain(state.Decisions, decision => string.IsNullOrWhiteSpace(decision.CardName));
        Assert.Equal("Card 1", state.Decisions[0].CardName);
        Assert.Equal("Card 500", state.Decisions[^1].CardName);
    }

    [Fact]
    public void Deserialize_Pre103JsonWithoutDecisionsOrBaselineSnapshot_ReturnsEmptyDefaults()
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
                }
              ],
              "packages": [],
              "roleFloors": [],
              "intent": {
                "primaryPlan": "Counters",
                "secondaryPlan": null,
                "bracket": 3,
                "playExperience": "Focused"
              }
            }
            """;

        var state = CutLabStateSerializer.Deserialize(json);

        Assert.Empty(state.Decisions);
        Assert.Null(state.BaselineSnapshot);
    }

    [Fact]
    public void Serialize_WorstCaseDecisionHistoryAndBaselineSnapshot_StaysUnderMaxUploadBytes()
    {
        var pool = Enumerable.Range(1, 150)
            .Select(index => new CutLabPoolCard
            {
                Name = $"Card {index}",
                Quantity = 1,
                TypeLine = index == 1 ? "Legendary Creature — Human Wizard" : "Artifact",
                IsCommander = index == 1,
                IsLocked = index == 1,
                PackageId = index <= 10 ? "pkg-core" : null,
            })
            .ToArray();
        var decisions = new List<CutLabDecision>();
        int ordinal = 1;

        foreach (int index in Enumerable.Range(1, 50))
        {
            decisions.Add(new CutLabDecision
            {
                CardName = $"Card {index}",
                Kind = CutLabDecisionKind.Deferred,
                Round = "round-1",
                Ordinal = ordinal++,
            });
            decisions.Add(new CutLabDecision
            {
                CardName = $"Card {index}",
                Kind = CutLabDecisionKind.Rejected,
                Round = "round-2",
                Ordinal = ordinal++,
            });
            decisions.Add(new CutLabDecision
            {
                CardName = $"Card {index}",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-3",
                Ordinal = ordinal++,
            });
        }

        foreach (int index in Enumerable.Range(51, 100))
        {
            decisions.Add(new CutLabDecision
            {
                CardName = $"Card {index}",
                Kind = CutLabDecisionKind.Accepted,
                Round = "round-3",
                Ordinal = ordinal++,
            });
        }

        var state = new CutLabState
        {
            Commander = "Card 1",
            Pool = pool,
            Decisions = decisions,
            BaselineSnapshot = CreateBaselineSnapshot(),
        };

        var json = CutLabStateSerializer.Serialize(state);

        Assert.True(Encoding.UTF8.GetByteCount(json) < CutLabStateSerializer.MaxUploadBytes);
    }

    private static CutLabMetricSnapshot CreateBaselineSnapshot()
        => new()
        {
            Metrics =
            [
                CreateMetric(CutLabMetricKind.CommanderOnTime, CutLabMetricFamily.CommanderOnTime, "Commander on time", 71.2, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.KeepableHand, CutLabMetricFamily.KeepableHand, "Keepable hand", 82.5, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.ManaColorReliability, CutLabMetricFamily.ManaColorReliability, "Mana and color reliability", 76.3, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.EarlyInteraction, CutLabMetricFamily.EarlyInteraction, "Early interaction", 48.9, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.PlanPresence, CutLabMetricFamily.PlanPresence, "Plan presence", 64.1, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, "Commander by turn 3", 57.7, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.EngineByTurn, CutLabMetricFamily.CategoryByTurn, "Engine by turn 2", 43.8, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.RepresentativeLineByTurn, CutLabMetricFamily.CategoryByTurn, "Representative line by turn 4", 39.2, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.Flood, CutLabMetricFamily.FloodScrewCurveRisk, "Flood risk", 2.0, CutLabMetricUnit.Cards),
                CreateMetric(CutLabMetricKind.Screw, CutLabMetricFamily.FloodScrewCurveRisk, "Screw risk", 11.4, CutLabMetricUnit.Percent),
                CreateMetric(CutLabMetricKind.Curve, CutLabMetricFamily.FloodScrewCurveRisk, "Curve risk", 6.0, CutLabMetricUnit.Cards),
            ],
        };

    private static CutLabMetricValue CreateMetric(
        CutLabMetricKind kind,
        CutLabMetricFamily family,
        string label,
        double value,
        CutLabMetricUnit unit)
        => new()
        {
            Kind = kind,
            Family = family,
            Label = label,
            Value = value,
            Unit = unit,
        };
}
