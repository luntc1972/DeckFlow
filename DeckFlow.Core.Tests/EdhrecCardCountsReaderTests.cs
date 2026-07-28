using System.Reflection;
using DeckFlow.Core.Research;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Covers the streaming EDHREC bulk card-count reader and its denominator gate.
/// </summary>
public sealed class EdhrecCardCountsReaderTests : IDisposable
{
    private readonly string _tempDirectory;

    public EdhrecCardCountsReaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void ReadDistinctCardNames_ReturnsEachDistinctCardOnce_AndCountsMalformedRows()
    {
        string csvPath = WriteTempFile(
            "edhrec.csv",
            """
            commander,card,count
            Alpha,Sol Ring,4
            Beta,Sol Ring,5
            Alpha,Arcane Signet,3
            Broken,OnlyTwoColumns
            Gamma,Lightning Greaves,not-a-number
            """);

        IReadOnlyCollection<string> distinctCards = EdhrecCardCountsReader.ReadDistinctCardNames(csvPath, out int malformedRows);

        Assert.Equal(2, malformedRows);
        Assert.Equal(
            new[] { "Arcane Signet", "Sol Ring" },
            distinctCards.OrderBy(card => card, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ReadDistinctCardNames_PreservesQuotedCommaBearingNames()
    {
        string csvPath = WriteTempFile(
            "edhrec.csv",
            """
            commander,card,count
            "Adrix and Nev, Twincasters","Fire // Ice",365
            "Adrix and Nev, Twincasters","Boros Signet",200
            """);

        IReadOnlyCollection<string> distinctCards = EdhrecCardCountsReader.ReadDistinctCardNames(csvPath, out int malformedRows);

        Assert.Equal(0, malformedRows);
        Assert.Contains("Fire // Ice", distinctCards);
        Assert.Contains("Boros Signet", distinctCards);
    }

    [Fact]
    public void ReadSoloDenominators_UsesSoloRowsOnly()
    {
        string csvPath = WriteTempFile(
            "averages.csv",
            """
            commander,commander2,oracle_id,oracle_id2,avg_creature,avg_instant,avg_sorcery,avg_artifact,avg_enchantment,avg_battle,avg_planeswalker,avg_nonbasicland,avg_basicland,avg_land,number_decks
            Solo Commander,,id-1,,30,10,10,8,5,0,1,24,10,34,120
            Solo Commander,Partner Friend,id-1,id-2,29,9,10,8,5,0,1,23,10,33,999
            Other Commander,,id-3,,28,8,8,7,4,0,1,22,11,33,80
            """);

        IReadOnlyDictionary<string, long> denominators = EdhrecCardCountsReader.ReadSoloDenominators(csvPath);

        Assert.Equal(2, denominators.Count);
        Assert.Equal(120L, denominators["Solo Commander"]);
        Assert.Equal(80L, denominators["Other Commander"]);
    }

    [Fact]
    public void Accumulate_SkipsCommandersWithoutSoloDenominators_AndRecordsThem()
    {
        string csvPath = WriteTempFile(
            "edhrec.csv",
            """
            commander,card,count
            Missing Commander,Sol Ring,5
            Missing Commander,Arcane Signet,4
            """);

        var cardRoles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Sol Ring"] = new[] { "ramp" },
            ["Arcane Signet"] = new[] { "ramp" },
        };

        EdhrecBulkGridResult result = EdhrecCardCountsReader.Accumulate(
            csvPath,
            new Dictionary<string, long>(StringComparer.Ordinal),
            cardRoles,
            new[] { "ramp" });

        Assert.Null(result.Failure);
        Assert.Empty(result.Commanders);
        Assert.Equal(new[] { "Missing Commander" }, result.MissingDenominators);
        Assert.Empty(result.DenominatorMismatches);
        Assert.Equal(2, result.RowsRead);
    }

    [Fact]
    public void Accumulate_RecordsDenominatorMismatch_AndExcludesCommanderWithoutClamping()
    {
        string csvPath = WriteTempFile(
            "edhrec.csv",
            """
            commander,card,count
            Reaper King,Sol Ring,8
            Reaper King,Arcane Signet,5
            Healthy Commander,Sol Ring,3
            Healthy Commander,Swords to Plowshares,2
            """);

        var denominators = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["Reaper King"] = 6,
            ["Healthy Commander"] = 10,
        };
        var cardRoles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Sol Ring"] = new[] { "ramp" },
            ["Arcane Signet"] = new[] { "ramp" },
            ["Swords to Plowshares"] = new[] { "interaction" },
        };

        EdhrecBulkGridResult result = EdhrecCardCountsReader.Accumulate(
            csvPath,
            denominators,
            cardRoles,
            new[] { "ramp", "interaction" });

        Assert.Null(result.Failure);
        Assert.Single(result.DenominatorMismatches);
        Assert.Contains("Reaper King", result.DenominatorMismatches[0]);
        Assert.Contains("Sol Ring", result.DenominatorMismatches[0]);
        Assert.Contains("count=8", result.DenominatorMismatches[0]);
        Assert.Contains("denominator=6", result.DenominatorMismatches[0]);
        Assert.Contains("ratio=1.333333", result.DenominatorMismatches[0]);
        Assert.DoesNotContain(result.Commanders, commander => commander.Commander == "Reaper King");
        Assert.Single(result.Commanders);
        Assert.Equal("Healthy Commander", result.Commanders[0].Commander);
    }

    [Fact]
    public void Accumulate_ComputesExpectedByRole_RowsConsumed_AndTotalInclusionRate()
    {
        string csvPath = WriteTempFile(
            "edhrec.csv",
            """
            commander,card,count
            Healthy Commander,Sol Ring,4
            Healthy Commander,Arcane Signet,2
            Healthy Commander,Swords to Plowshares,3
            Healthy Commander,Unknown Card,1
            """);

        var denominators = new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["Healthy Commander"] = 10,
        };
        var cardRoles = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["Sol Ring"] = new[] { "ramp" },
            ["Arcane Signet"] = new[] { "ramp" },
            ["Swords to Plowshares"] = new[] { "interaction" },
        };

        EdhrecBulkGridResult result = EdhrecCardCountsReader.Accumulate(
            csvPath,
            denominators,
            cardRoles,
            new[] { "ramp", "interaction" });

        EdhrecBulkCommanderTotals commander = Assert.Single(result.Commanders);
        Assert.Equal("Healthy Commander", commander.Commander);
        Assert.Equal(10L, commander.Denominator);
        Assert.Equal(4, commander.RowsConsumed);
        Assert.Equal("Sol Ring", commander.MaxRatioCard);
        Assert.Equal(0.4d, commander.MaxRatio, precision: 6);
        Assert.Equal(1.0d, commander.TotalInclusionRate, precision: 6);
        Assert.Equal(0.6d, commander.ExpectedByRole["ramp"], precision: 6);
        Assert.Equal(0.3d, commander.ExpectedByRole["interaction"], precision: 6);
    }

    [Fact]
    public void Reader_PublicSurface_ReferencesNoClassifierOrResolverTypes()
    {
        Assembly assembly = typeof(EdhrecCardCountsReader).Assembly;
        Type readerType = assembly.GetType("DeckFlow.Core.Research.EdhrecCardCountsReader", throwOnError: true)!;

        string[] forbiddenTypeNames =
        {
            "CutLabRoleAssigner",
            "IScryfallCardResolver",
            "CardFact",
        };

        foreach (MethodInfo method in readerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            Assert.DoesNotContain(forbiddenTypeNames, forbidden => method.ToString()?.Contains(forbidden, StringComparison.Ordinal) == true);
            Assert.DoesNotContain(forbiddenTypeNames, forbidden => method.ReturnType.FullName?.Contains(forbidden, StringComparison.Ordinal) == true);

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Assert.DoesNotContain(forbiddenTypeNames, forbidden => parameter.ParameterType.FullName?.Contains(forbidden, StringComparison.Ordinal) == true);
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private string WriteTempFile(string fileName, string contents)
    {
        string path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllText(path, contents.ReplaceLineEndings("\n"));
        return path;
    }
}
