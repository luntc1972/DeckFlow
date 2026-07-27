using System.Reflection;
using System.Text.Json;

using DeckFlow.Core.Research;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Locks the role-floor figure surface so source attribution remains explicit, EDHREC point
/// estimates remain structurally unable to masquerade as distributions, and the JSON get/init
/// carve-out continues to round-trip these records intact.
/// </summary>
public sealed class RoleFloorFigureTests
{
    private static readonly string[] EdhrecDistributionPropertyNeedles =
    [
        "P25",
        "Percentile",
        "StdDev",
        "ZScore",
        "CohensD",
        "Ratio",
    ];

    private static readonly string[] EdhrecDistributionColumnNeedles =
    [
        "P25",
        "Percentile",
        "StdDev",
        "Z",
        "Cohen",
        "Mean",
        "Ratio",
    ];

    [Fact]
    public void RoleFloorSource_HasExactlyTwoExplicitNonZeroMembers()
    {
        RoleFloorSource[] values = Enum.GetValues<RoleFloorSource>();

        // Forward note: plan 02-09 is narrowly authorized to change this count from 2 to 3 for
        // EdhrecBulk = 3; this assertion exists to prevent a zero-valued accidental default source,
        // not to freeze the enum forever.
        Assert.Equal(2, values.Length);
        Assert.Equal(1, (int)RoleFloorSource.Postgres);
        Assert.Equal(2, (int)RoleFloorSource.Edhrec);
        Assert.All(values, value => Assert.NotEqual(0, (int)value));
    }

    [Fact]
    public void IRoleFloorFigure_DeclaresExactlyThreeProperties()
    {
        PropertyInfo[] properties = typeof(IRoleFloorFigure).GetProperties();

        // Permanent invariant: the interface must stay at these three members, and the EDHREC
        // no-distribution reflection assertion below is permanent too; plan 02-09 adds equivalent
        // coverage for its own type instead of relaxing either assertion.
        Assert.Equal(3, properties.Length);
        Assert.Equal(
            ["Source", "Role", "CommanderName"],
            properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void EdhrecRolePointEstimate_DoesNotExposeDistributionProperties()
    {
        string[] offenders = typeof(EdhrecRolePointEstimate)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Where(name => EdhrecDistributionPropertyNeedles.Any(needle => name.Contains(needle, StringComparison.Ordinal)))
            .ToArray();

        Assert.False(
            offenders.Any(),
            $"EdhrecRolePointEstimate must not expose distribution properties; found: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void PostgresRoleDistribution_SurvivesJsonRoundTrip()
    {
        PostgresRoleDistribution original = new()
        {
            Source = RoleFloorSource.Postgres,
            Role = "interaction",
            CommanderName = "Atraxa, Praetors' Voice",
            DeckCount = 123,
            Mean = 7.5,
            P25 = 6.0,
            StdDev = 1.75,
            Ratio = 1.25,
            ZScore = 3.2,
            CohensD = 0.85,
            ClearsBar = true,
        };

        string json = JsonSerializer.Serialize(original);
        PostgresRoleDistribution? restored = JsonSerializer.Deserialize<PostgresRoleDistribution>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Source, restored!.Source);
        Assert.Equal(original.Role, restored.Role);
        Assert.Equal(original.CommanderName, restored.CommanderName);
        Assert.Equal(original.DeckCount, restored.DeckCount);
        Assert.Equal(original.Mean, restored.Mean);
        Assert.Equal(original.P25, restored.P25);
        Assert.Equal(original.StdDev, restored.StdDev);
        Assert.Equal(original.Ratio, restored.Ratio);
        Assert.Equal(original.ZScore, restored.ZScore);
        Assert.Equal(original.CohensD, restored.CohensD);
        Assert.Equal(original.ClearsBar, restored.ClearsBar);
    }

    [Fact]
    public void EdhrecRolePointEstimate_SurvivesJsonRoundTrip()
    {
        EdhrecRolePointEstimate original = new()
        {
            Source = RoleFloorSource.Edhrec,
            Role = "ramp",
            CommanderName = "Tatyova, Benthic Druid",
            BracketSlug = "core",
            BracketIndex = 2,
            Count = 11.0,
            DeckCount = 684,
            Qualifies = true,
        };

        string json = JsonSerializer.Serialize(original);
        EdhrecRolePointEstimate? restored = JsonSerializer.Deserialize<EdhrecRolePointEstimate>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Source, restored!.Source);
        Assert.Equal(original.Role, restored.Role);
        Assert.Equal(original.CommanderName, restored.CommanderName);
        Assert.Equal(original.BracketSlug, restored.BracketSlug);
        Assert.Equal(original.BracketIndex, restored.BracketIndex);
        Assert.Equal(original.Count, restored.Count);
        Assert.Equal(original.DeckCount, restored.DeckCount);
        Assert.Equal(original.Qualifies, restored.Qualifies);
    }

    [Fact]
    public void RoleFloorFigureTable_ColumnListsCarrySourceColumn()
    {
        Assert.Contains("Source", RoleFloorFigureTable.PostgresColumns);
        Assert.Contains("Source", RoleFloorFigureTable.EdhrecColumns);
        Assert.False(RoleFloorFigureTable.HasSourceColumn(["Commander", "Mean"]));
        Assert.True(RoleFloorFigureTable.HasSourceColumn(["Commander", "Source"]));
    }

    [Fact]
    public void RoleFloorFigureTable_AllDeclaredColumnListsCarrySourceColumn()
    {
        PropertyInfo[] columnProperties = typeof(RoleFloorFigureTable)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(IReadOnlyList<string>))
            .ToArray();

        foreach (PropertyInfo property in columnProperties)
        {
            IReadOnlyList<string>? columns = (IReadOnlyList<string>?)property.GetValue(null);

            Assert.NotNull(columns);
            Assert.True(
                RoleFloorFigureTable.HasSourceColumn(columns!),
                $"RoleFloorFigureTable declaration '{property.Name}' must include a Source column.");
        }
    }

    [Fact]
    public void RoleFloorFigureTable_EdhrecColumns_DoNotExposeDistributionColumns()
    {
        string[] offenders = RoleFloorFigureTable.EdhrecColumns
            .Where(column => EdhrecDistributionColumnNeedles.Any(needle => column.Contains(needle, StringComparison.Ordinal)))
            .ToArray();

        Assert.False(
            offenders.Any(),
            $"EdhrecColumns must not expose distribution columns; found: {string.Join(", ", offenders)}");
    }
}
