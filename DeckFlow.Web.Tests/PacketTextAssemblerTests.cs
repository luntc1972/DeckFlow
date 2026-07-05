using System.Text;
using DeckFlow.Core.Models;
using DeckFlow.Web.Services.Packets;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Characterization tests for <see cref="PacketTextAssembler"/>, locking its output to the three
/// current service shapes (Analysis/Comparison/Primer) including the Possible-Includes-stays-plain
/// asymmetry (research H1), and proving <c>AppendKeyValueLine</c> does not hardcode a normalizer.
/// </summary>
public sealed class PacketTextAssemblerTests
{
    // ---------------------------------------------------------------------------------------
    // Test 1: Analysis shape — includeVersions=true + oracleNameMap set.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void BuildSectionedDecklistText_AnalysisShape_AppliesVersionSuffixAndDfcTruncationButNotToPossibleIncludes()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Blex, Vexing Pest // Search for Blex", 1, "commander", "mid", "219"),
            CreateDeckEntry("Kraum, Ludevic's Opus", 1, "commander", "c16", "39"),
            CreateDeckEntry("Bolt Variant Name", 1, "mainboard", "lea", "161"),
            CreateDeckEntry("Sol Ring", 2, "mainboard", "c16", "272"),
        };
        var possibleIncludes = new List<DeckEntry>
        {
            CreateDeckEntry("Renamed Possible // Other Face", 3, "maybeboard", "afr", "5"),
        };
        var oracleNameMap = new Dictionary<string, string>
        {
            ["Bolt Variant Name"] = "Lightning Bolt",
            ["Renamed Possible // Other Face"] = "Renamed Possible",
        };

        var result = PacketTextAssembler.BuildSectionedDecklistText(entries, possibleIncludes, includeVersions: true, oracleNameMap);

        var expected = string.Join(Environment.NewLine, new[]
        {
            "Commander",
            "1 Blex, Vexing Pest (MID) 219",
            "1 Kraum, Ludevic's Opus (C16) 39",
            "",
            "Mainboard",
            "1 Lightning Bolt (LEA) 161 [printed as: Bolt Variant Name]",
            "2 Sol Ring (C16) 272",
            "",
            "Possible Includes",
            "3 Renamed Possible [printed as: Renamed Possible // Other Face]",
        });

        Assert.Equal(expected, result);

        // Explicit asymmetry assertion (H1): the Possible-Includes line has NO " (SET)" suffix
        // and NO DFC-slash truncation, even though includeVersions=true.
        Assert.DoesNotContain("Renamed Possible (AFR)", result, StringComparison.Ordinal);
        Assert.Contains("3 Renamed Possible [printed as: Renamed Possible // Other Face]", result, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // Test 2: Comparison shape — oracleNameMap set, no versions.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void BuildSectionedDecklistText_ComparisonShape_AppliesPrintedAsAnnotationWithNoVersionSuffix()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Kraum, Ludevic's Opus", 1, "commander", "c16", "39"),
            CreateDeckEntry("Bolt Variant Name", 1, "mainboard", "lea", "161"),
            CreateDeckEntry("Sol Ring", 1, "mainboard", "c16", "272"),
        };
        var possibleIncludes = Array.Empty<DeckEntry>();
        var oracleNameMap = new Dictionary<string, string>
        {
            ["Bolt Variant Name"] = "Lightning Bolt",
        };

        var result = PacketTextAssembler.BuildSectionedDecklistText(entries, possibleIncludes, includeVersions: false, oracleNameMap);

        var expected = string.Join(Environment.NewLine, new[]
        {
            "Commander",
            "1 Kraum, Ludevic's Opus",
            "",
            "Mainboard",
            "1 Lightning Bolt [printed as: Bolt Variant Name]",
            "1 Sol Ring",
        });

        Assert.Equal(expected, result);
        Assert.DoesNotContain("(C16)", result, StringComparison.Ordinal);
        Assert.DoesNotContain("(LEA)", result, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // Test 3: Primer shape — neither includeVersions nor oracleNameMap.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void BuildSectionedDecklistText_PrimerShape_PlainQuantityAndNameOnly()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", 1, "commander", "c16", "1"),
            CreateDeckEntry("Sol Ring", 1, "mainboard", "c16", "272"),
            CreateDeckEntry("Arcane Signet", 1, "mainboard", "eld", "331"),
        };
        var possibleIncludes = new List<DeckEntry>
        {
            CreateDeckEntry("Ponder", 1, "maybeboard", "c21", "118"),
        };

        var result = PacketTextAssembler.BuildSectionedDecklistText(entries, possibleIncludes);

        var expected = string.Join(Environment.NewLine, new[]
        {
            "Commander",
            "1 Atraxa, Praetors' Voice",
            "",
            "Mainboard",
            "1 Arcane Signet",
            "1 Sol Ring",
            "",
            "Possible Includes",
            "1 Ponder",
        });

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildSectionedDecklistText_NoCommanderOrPossibleIncludes_OmitsBothSections()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Sol Ring", 1, "mainboard", null, null),
        };

        var result = PacketTextAssembler.BuildSectionedDecklistText(entries, Array.Empty<DeckEntry>());

        Assert.Equal("Mainboard" + Environment.NewLine + "1 Sol Ring", result);
        Assert.DoesNotContain("Commander", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Possible Includes", result, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // Test 4: AppendKeyValueLine takes the normalizer as a delegate — proving it is not hardcoded.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void AppendKeyValueLine_WithDifferentNormalizerDelegates_ProducesDifferentOutputForSameInput()
    {
        const string tabAndNewlineBearingInput = "line one\tline two\nline three";

        // Normalizer A: newline/tab -> space (JsonTextFormatterService-style).
        string NormalizerA(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Replace('\n', ' ').Replace('\t', ' ');

        // Normalizer B: strip newlines and tabs entirely (deliberately different from A).
        string NormalizerB(string? value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Replace("\n", string.Empty).Replace("\t", string.Empty);

        var builderA = new StringBuilder();
        PacketTextAssembler.AppendKeyValueLine(builderA, "deck_name", tabAndNewlineBearingInput, string.Empty, NormalizerA);

        var builderB = new StringBuilder();
        PacketTextAssembler.AppendKeyValueLine(builderB, "deck_name", tabAndNewlineBearingInput, string.Empty, NormalizerB);

        var resultA = builderA.ToString().TrimEnd();
        var resultB = builderB.ToString().TrimEnd();

        Assert.NotEqual(resultA, resultB);
        Assert.Equal("deck_name: line one line two line three", resultA);
        Assert.Equal("deck_name: line oneline twoline three", resultB);
    }

    [Fact]
    public void AppendKeyValueLine_NullValue_UsesFallbackViaNormalizer()
    {
        var builder = new StringBuilder();
        PacketTextAssembler.AppendKeyValueLine(builder, "commander", null, "Unknown Commander", (value, fallback) => value ?? fallback);

        Assert.Equal("commander: Unknown Commander", builder.ToString().TrimEnd());
    }

    private static DeckEntry CreateDeckEntry(
        string name,
        int quantity,
        string board,
        string? setCode,
        string? collectorNumber)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = board,
            SetCode = setCode,
            CollectorNumber = collectorNumber,
        };
}
