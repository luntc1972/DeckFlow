using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

public sealed class RoleFloorBaselineTests
{
    [Fact]
    public void Build_ClearsBarPostgresRoleWithPositiveP25_IsAdopted()
    {
        RoleFloorFindingsDocument document = Document(
            ("Fire Lord Azula", 644, Roles(("ramp", "postgres", 7.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        RoleFloorCommanderSnapshot commander = Assert.Single(snapshot.Commanders.Values);
        Assert.Equal(7, commander.Floors["ramp"]);
    }

    [Fact]
    public void Build_FractionalP25_TruncatesDown()
    {
        RoleFloorFindingsDocument document = Document(
            ("Commander A", 50, Roles(("ramp", "postgres", 7.5, true))),
            ("Commander B", 50, Roles(("draw", "postgres", 6.5, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.Equal(7, snapshot.Commanders["Commander A"].Floors["ramp"]);
        Assert.Equal(6, snapshot.Commanders["Commander B"].Floors["draw"]);
    }

    [Fact]
    public void Build_P25BelowOne_IsDroppedAsNoSignal()
    {
        RoleFloorFindingsDocument document = Document(
            ("Commander Zero", 50, Roles(("ramp", "postgres", 0.0, true), ("draw", "postgres", 2.0, true))),
            ("Commander Fraction", 50, Roles(("ramp", "postgres", 0.6, true), ("draw", "postgres", 2.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.DoesNotContain("ramp", snapshot.Commanders["Commander Zero"].Floors.Keys);
        Assert.DoesNotContain("ramp", snapshot.Commanders["Commander Fraction"].Floors.Keys);
        Assert.Equal(2, snapshot.Commanders["Commander Zero"].Floors["draw"]);
        Assert.Equal(2, snapshot.Commanders["Commander Fraction"].Floors["draw"]);
    }

    [Fact]
    public void Build_RoleThatDidNotClearBar_IsExcluded()
    {
        RoleFloorFindingsDocument document = Document(
            ("Fire Lord Azula", 644, Roles(("ramp", "postgres", 9.0, false), ("draw", "postgres", 2.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.DoesNotContain("ramp", snapshot.Commanders["Fire Lord Azula"].Floors.Keys);
    }

    [Fact]
    public void Build_NonPostgresSource_IsExcluded()
    {
        RoleFloorFindingsDocument document = Document(
            ("Fire Lord Azula", 644, Roles(("ramp", "edhrec", 9.0, true), ("draw", "postgres", 2.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.DoesNotContain("ramp", snapshot.Commanders["Fire Lord Azula"].Floors.Keys);
    }

    [Fact]
    public void Build_PostgresWithDifferentCase_IsExcluded()
    {
        RoleFloorFindingsDocument document = Document(
            ("Fire Lord Azula", 644, Roles(("ramp", "Postgres", 9.0, true), ("draw", "postgres", 2.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.DoesNotContain("ramp", snapshot.Commanders["Fire Lord Azula"].Floors.Keys);
        Assert.Equal(2, snapshot.Commanders["Fire Lord Azula"].Floors["draw"]);
    }

    [Fact]
    public void Build_LandsAndOutOfScopeRoles_AreNeverAdopted()
    {
        RoleFloorFindingsDocument document = Document(
            ("Fire Lord Azula", 644, Roles(
                ("lands", "postgres", 36.0, true),
                ("interaction-mass", "postgres", 9.0, true),
                ("protection", "postgres", 4.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.Empty(snapshot.Commanders);
    }

    [Fact]
    public void Build_CommanderWithNoAdoptedRoles_IsOmitted()
    {
        RoleFloorFindingsDocument document = Document(
            ("Commander Kept", 50, Roles(("draw", "postgres", 3.0, true))),
            ("Commander Dropped", 50, Roles(("ramp", "postgres", 0.0, true), ("engines", "postgres", 0.6, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.DoesNotContain("Commander Dropped", snapshot.Commanders.Keys);
        Assert.Contains("Commander Kept", snapshot.Commanders.Keys);
    }

    [Fact]
    public void Build_SetsAggregateCounters()
    {
        RoleFloorFindingsDocument document = Document(
            ("Commander A", 50, Roles(("ramp", "postgres", 3.0, true), ("draw", "postgres", 2.0, true))),
            ("Commander B", 60, Roles(("engines", "postgres", 4.0, true))),
            ("Commander C", 70, Roles(("wincons", "postgres", 0.0, true))));

        RoleFloorBaselineSnapshot snapshot = RoleFloorBaseline.Build(document, "2026-07-29");

        Assert.Equal(3, snapshot.SampleSize);
        Assert.Equal(3, snapshot.AdoptedPairs);
    }

    private static RoleFloorFindingsDocument Document(
        params (string Name, int N, IReadOnlyDictionary<string, RoleFloorFindingsRole> Roles)[] commanders)
    {
        return new RoleFloorFindingsDocument
        {
            Commanders = commanders.ToDictionary(
                commander => commander.Name,
                commander => new RoleFloorFindingsCommander
                {
                    N = commander.N,
                    Roles = commander.Roles,
                },
                StringComparer.Ordinal),
        };
    }

    private static IReadOnlyDictionary<string, RoleFloorFindingsRole> Roles(
        params (string RoleKey, string Source, double P25, bool ClearsBar)[] roles)
    {
        return roles.ToDictionary(
            role => role.RoleKey,
            role => new RoleFloorFindingsRole
            {
                Source = role.Source,
                P25 = role.P25,
                ClearsBar = role.ClearsBar,
            },
            StringComparer.Ordinal);
    }
}
