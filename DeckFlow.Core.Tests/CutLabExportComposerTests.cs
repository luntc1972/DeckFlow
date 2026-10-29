using System.Reflection;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Tests;

public sealed class CutLabExportComposerTests
{
    [Fact]
    public void CommanderIdentityCheck_ReturnsLegalForSubsetAndColorlessCards()
    {
        Assert.Equal("Legal", InvokeCommanderIdentityCheck(["G"], Set("G", "U")));
        Assert.Equal("Legal", InvokeCommanderIdentityCheck([], Set("G", "U")));
    }

    [Fact]
    public void CommanderIdentityCheck_ReturnsIllegalForOutOfIdentityCards()
    {
        Assert.Equal("Illegal", InvokeCommanderIdentityCheck(["R"], Set("G", "U")));
    }

    [Fact]
    public void CommanderIdentityCheck_ReturnsUnverifiedForUnknownIdentity()
    {
        Assert.Equal("Unverified", InvokeCommanderIdentityCheck(null, Set("G", "U")));
    }

    [Fact]
    public void Compose_NormalizesSideboardAndMaybeboardIntoFinishedMainboardExportForBothDialects()
    {
        var commander = Entry("Kinnan, Bonder Prodigy", 1, "commander");
        var keptSideboard = Entry("Fierce Guardianship", 1, "sideboard");
        var keptMaybeboard = Entry("Mystic Remora", 1, "maybeboard");
        var forests = Entry("Forest", 97, "mainboard");

        var result = Compose(
            [commander, keptSideboard, keptMaybeboard, forests],
            [commander, keptSideboard, keptMaybeboard, forests],
            commanderIdentity: Set("G", "U"),
            cardIdentitiesByName: new Dictionary<string, IReadOnlyList<string>?>
            {
                ["Kinnan, Bonder Prodigy"] = ["G", "U"],
                ["Fierce Guardianship"] = ["U"],
                ["Mystic Remora"] = ["U"],
                ["Forest"] = [],
            },
            unverifiedCardNames: Set<string>(),
            bannedCardNamesPresent: Set<string>());

        var moxfieldFull = GetRequiredString(result, "MoxfieldFullListText");
        var archidektFull = GetRequiredString(result, "ArchidektFullListText");

        Assert.Contains("1 Fierce Guardianship", moxfieldFull);
        Assert.Contains("1 Mystic Remora", moxfieldFull);
        Assert.DoesNotContain("Maybeboard", moxfieldFull, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sideboard", moxfieldFull, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("// Commander", archidektFull);
        Assert.Contains("// Mainboard", archidektFull);
        Assert.Contains("1 Fierce Guardianship", archidektFull);
        Assert.Contains("1 Mystic Remora", archidektFull);
        Assert.DoesNotContain("Maybeboard", archidektFull, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sideboard", archidektFull, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compose_IncludesCutsForFullRemovalsAndQuantityDecreasesInBothDialects()
    {
        var commander = Entry("Kinnan, Bonder Prodigy", 1, "commander");
        var finalEntries = new List<DeckEntry>
        {
            commander,
            Entry("Forest", 7, "mainboard"),
            Entry("Island", 92, "mainboard"),
        };
        var originalEntries = new List<DeckEntry>
        {
            commander,
            Entry("Forest", 10, "mainboard"),
            Entry("Island", 92, "mainboard"),
            Entry("Llanowar Elves", 1, "sideboard"),
        };

        var result = Compose(
            finalEntries,
            originalEntries,
            commanderIdentity: Set("G", "U"),
            cardIdentitiesByName: new Dictionary<string, IReadOnlyList<string>?>
            {
                ["Kinnan, Bonder Prodigy"] = ["G", "U"],
                ["Forest"] = [],
                ["Island"] = [],
                ["Llanowar Elves"] = ["G"],
            },
            unverifiedCardNames: Set<string>(),
            bannedCardNamesPresent: Set<string>());

        var moxfieldPatch = GetRequiredString(result, "MoxfieldPatchText");
        var archidektPatch = GetRequiredString(result, "ArchidektPatchText");

        Assert.Contains("# CUT (remove these)", moxfieldPatch);
        Assert.Contains("3 Forest", moxfieldPatch);
        Assert.Contains("1 Llanowar Elves", moxfieldPatch);
        Assert.Contains("No cards to add", moxfieldPatch);

        Assert.Contains("# CUT (remove these)", archidektPatch);
        Assert.Contains("3 Forest", archidektPatch);
        Assert.Contains("// Sideboard", archidektPatch);
        Assert.Contains("1 Llanowar Elves", archidektPatch);
        Assert.Contains("No cards to add", archidektPatch);
    }

    [Fact]
    public void Compose_ConsolidatesDuplicateEquivalentEntriesSoExportedQuantitiesSumTo100()
    {
        var commander = Entry("Kinnan, Bonder Prodigy", 1, "commander");
        var duplicateForestA = Entry("Forest", 40, "mainboard", "ktk", "258");
        var duplicateForestB = Entry("Forest", 59, "mainboard", "ktk", "258");

        var result = Compose(
            [commander, duplicateForestA, duplicateForestB],
            [commander, duplicateForestA, duplicateForestB],
            commanderIdentity: Set("G", "U"),
            cardIdentitiesByName: new Dictionary<string, IReadOnlyList<string>?>
            {
                ["Kinnan, Bonder Prodigy"] = ["G", "U"],
                ["Forest"] = [],
            },
            unverifiedCardNames: Set<string>(),
            bannedCardNamesPresent: Set<string>());

        Assert.True(GetRequiredBoolean(result, "CountOk"));
        Assert.False(GetRequiredBoolean(result, "HardBlock"));

        var moxfieldFull = GetRequiredString(result, "MoxfieldFullListText");
        Assert.Contains("99 Forest (ktk) 258", moxfieldFull);
        Assert.Equal(100, ParseTotalQuantity(moxfieldFull));
    }

    [Fact]
    public void Compose_SeparatesIllegalAndUnverifiedWarningsWithoutOverlapping()
    {
        var commander = Entry("Kinnan, Bonder Prodigy", 1, "commander");
        var finalEntries = new List<DeckEntry>
        {
            commander,
            Entry("Lightning Bolt", 1, "mainboard"),
            Entry("Mystery Card", 1, "mainboard"),
            Entry("Forest", 97, "mainboard"),
        };

        var result = Compose(
            finalEntries,
            finalEntries,
            commanderIdentity: Set("G", "U"),
            cardIdentitiesByName: new Dictionary<string, IReadOnlyList<string>?>
            {
                ["Kinnan, Bonder Prodigy"] = ["G", "U"],
                ["Lightning Bolt"] = ["R"],
                ["Mystery Card"] = null,
                ["Forest"] = [],
            },
            unverifiedCardNames: Set("Mystery Card"),
            bannedCardNamesPresent: Set("Lightning Bolt"));

        var illegal = GetRequiredStringList(result, "IllegalColorIdentity");
        var unverified = GetRequiredStringList(result, "UnverifiedColorIdentity");
        var banlist = GetRequiredStringList(result, "BanlistOffenders");

        Assert.Contains("Lightning Bolt", illegal);
        Assert.DoesNotContain("Mystery Card", illegal);
        Assert.Contains("Mystery Card", unverified);
        Assert.DoesNotContain("Lightning Bolt", unverified);
        Assert.Contains("Lightning Bolt", banlist);
        Assert.True(GetRequiredBoolean(result, "CountOk"));
        Assert.False(GetRequiredBoolean(result, "HardBlock"));
    }

    [Fact]
    public void Compose_HardBlocksOnlyOnCountMismatch()
    {
        var commander = Entry("Kinnan, Bonder Prodigy", 1, "commander");
        var finalEntries = new List<DeckEntry>
        {
            commander,
            Entry("Forest", 98, "mainboard"),
        };

        var result = Compose(
            finalEntries,
            finalEntries,
            commanderIdentity: Set("G", "U"),
            cardIdentitiesByName: new Dictionary<string, IReadOnlyList<string>?>
            {
                ["Kinnan, Bonder Prodigy"] = ["G", "U"],
                ["Forest"] = [],
            },
            unverifiedCardNames: Set<string>(),
            bannedCardNamesPresent: Set<string>());

        Assert.False(GetRequiredBoolean(result, "CountOk"));
        Assert.True(GetRequiredBoolean(result, "HardBlock"));
        Assert.Equal(-1, GetRequiredInt32(result, "OffCount"));
    }

    private static object Compose(
        IReadOnlyList<DeckEntry> finalEntries,
        IReadOnlyList<DeckEntry> originalEntries,
        IReadOnlySet<string> commanderIdentity,
        IReadOnlyDictionary<string, IReadOnlyList<string>?> cardIdentitiesByName,
        IReadOnlySet<string> unverifiedCardNames,
        IReadOnlySet<string> bannedCardNamesPresent)
    {
        var composerType = GetRequiredType("DeckFlow.Core.Exporting.CutLabExportComposer");
        var composeMethod = composerType.GetMethod(
            "Compose",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            ?? throw new Xunit.Sdk.XunitException("CutLabExportComposer.Compose was not found.");

        var instance = composeMethod.IsStatic ? null : Activator.CreateInstance(composerType);
        return composeMethod.Invoke(
            instance,
            [finalEntries, originalEntries, commanderIdentity, cardIdentitiesByName, unverifiedCardNames, bannedCardNamesPresent])
            ?? throw new Xunit.Sdk.XunitException("CutLabExportComposer.Compose returned null.");
    }

    private static string InvokeCommanderIdentityCheck(IReadOnlyList<string>? cardIdentity, IReadOnlySet<string> commanderIdentity)
    {
        var checkType = GetRequiredType("DeckFlow.Core.Exporting.CommanderIdentityCheck");
        var method = checkType.GetMethod(
            "IsWithinCommanderIdentity",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new Xunit.Sdk.XunitException("CommanderIdentityCheck.IsWithinCommanderIdentity was not found.");

        var result = method.Invoke(null, [cardIdentity, commanderIdentity])
            ?? throw new Xunit.Sdk.XunitException("CommanderIdentityCheck.IsWithinCommanderIdentity returned null.");

        return result.ToString() ?? string.Empty;
    }

    private static Type GetRequiredType(string fullName)
        => Type.GetType($"{fullName}, DeckFlow.Core")
            ?? throw new Xunit.Sdk.XunitException($"Type '{fullName}' was not found in DeckFlow.Core.");

    private static DeckEntry Entry(string name, int quantity, string board, string? setCode = null, string? collectorNumber = null, string? category = null)
        => new()
        {
            Name = name,
            NormalizedName = CardNormalizer.Normalize(name),
            Quantity = quantity,
            Board = board,
            SetCode = setCode,
            CollectorNumber = collectorNumber,
            Category = category,
        };

    private static IReadOnlySet<T> Set<T>(params T[] values)
        => new HashSet<T>(values);

    private static string GetRequiredString(object source, string propertyName)
        => GetRequiredPropertyValue(source, propertyName) as string
            ?? throw new Xunit.Sdk.XunitException($"Property '{propertyName}' was not a string.");

    private static bool GetRequiredBoolean(object source, string propertyName)
        => GetRequiredPropertyValue(source, propertyName) as bool?
            ?? throw new Xunit.Sdk.XunitException($"Property '{propertyName}' was not a bool.");

    private static int GetRequiredInt32(object source, string propertyName)
        => GetRequiredPropertyValue(source, propertyName) as int?
            ?? throw new Xunit.Sdk.XunitException($"Property '{propertyName}' was not an int.");

    private static IReadOnlyList<string> GetRequiredStringList(object source, string propertyName)
        => GetRequiredPropertyValue(source, propertyName) as IReadOnlyList<string>
            ?? throw new Xunit.Sdk.XunitException($"Property '{propertyName}' was not an IReadOnlyList<string>.");

    private static object? GetRequiredPropertyValue(object source, string propertyName)
        => source.GetType().GetProperty(propertyName)?.GetValue(source)
            ?? throw new Xunit.Sdk.XunitException($"Property '{propertyName}' was not found.");

    private static int ParseTotalQuantity(string exportText)
    {
        return exportText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Select(line => int.Parse(line.Split(' ', 2)[0]))
            .Sum();
    }
}
