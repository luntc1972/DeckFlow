using System.Text;
using DeckFlow.Core.Diffing;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Exporting;

/// <summary>
/// Composes builder-compatible export text and validation results for a finished Cut Lab list.
/// </summary>
public static class CutLabExportComposer
{
    /// <summary>
    /// Builds full-list exports, CUT/ADD patches, and validation details for the supplied final and original lists.
    /// </summary>
    public static CutLabExportResult Compose(
        IReadOnlyList<DeckEntry> finalEntries,
        IReadOnlyList<DeckEntry> originalEntries,
        IReadOnlySet<string> commanderIdentity,
        IReadOnlyDictionary<string, IReadOnlyList<string>?> cardIdentitiesByName,
        IReadOnlySet<string> unverifiedCardNames,
        IReadOnlySet<string> bannedCardNamesPresent)
    {
        ArgumentNullException.ThrowIfNull(finalEntries);
        ArgumentNullException.ThrowIfNull(originalEntries);
        ArgumentNullException.ThrowIfNull(commanderIdentity);
        ArgumentNullException.ThrowIfNull(cardIdentitiesByName);
        ArgumentNullException.ThrowIfNull(unverifiedCardNames);
        ArgumentNullException.ThrowIfNull(bannedCardNamesPresent);

        var normalizedFinalEntries = ConsolidateForFullExport(finalEntries);
        var moxfieldFullListText = FullImportExporter.ToText([.. normalizedFinalEntries], [], MatchMode.Loose, "Moxfield", null, CategorySyncMode.SourceTags);
        var archidektFullListText = FullImportExporter.ToText([.. normalizedFinalEntries], [], MatchMode.Loose, "Archidekt", null, CategorySyncMode.SourceTags);

        var diff = new DiffEngine(MatchMode.Loose).Compare([.. finalEntries], [.. originalEntries]);
        var cutEntries = diff.OnlyInArchidekt.Concat(diff.CountMismatch).ToList();
        var addEntries = diff.ToAdd.ToList();

        var countTotal = finalEntries.Sum(entry => entry.Quantity);
        var countOk = countTotal == 100;

        var identityLookup = new Dictionary<string, IReadOnlyList<string>?>(cardIdentitiesByName, StringComparer.OrdinalIgnoreCase);
        var explicitUnverified = new HashSet<string>(unverifiedCardNames, StringComparer.OrdinalIgnoreCase);
        var bannedNames = new HashSet<string>(bannedCardNamesPresent, StringComparer.OrdinalIgnoreCase);
        var illegalColorIdentity = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unresolvedColorIdentity = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cardName in finalEntries
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            identityLookup.TryGetValue(cardName, out var cardIdentity);
            var result = CommanderIdentityCheck.IsWithinCommanderIdentity(cardIdentity, commanderIdentity);
            if (result == CommanderIdentityCheckResult.Illegal)
            {
                illegalColorIdentity.Add(cardName);
                continue;
            }

            if (result == CommanderIdentityCheckResult.Unverified || explicitUnverified.Contains(cardName))
            {
                unresolvedColorIdentity.Add(cardName);
            }
        }

        unresolvedColorIdentity.ExceptWith(illegalColorIdentity);

        var banlistOffenders = finalEntries
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(cardName => bannedNames.Contains(cardName))
            .OrderBy(cardName => cardName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CutLabExportResult(
            moxfieldFullListText,
            archidektFullListText,
            BuildPatch("Moxfield", cutEntries, addEntries),
            BuildPatch("Archidekt", cutEntries, addEntries),
            countOk,
            countTotal - 100,
            !countOk,
            illegalColorIdentity.OrderBy(cardName => cardName, StringComparer.OrdinalIgnoreCase).ToList(),
            unresolvedColorIdentity.OrderBy(cardName => cardName, StringComparer.OrdinalIgnoreCase).ToList(),
            banlistOffenders);
    }

    private static IReadOnlyList<DeckEntry> ConsolidateForFullExport(IReadOnlyList<DeckEntry> finalEntries)
    {
        return finalEntries
            .Select(entry => entry with
            {
                Board = string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)
                    ? "commander"
                    : "mainboard",
            })
            .GroupBy(
                entry => new ConsolidatedEntryKey(
                    entry.NormalizedName,
                    entry.Board,
                    entry.SetCode ?? string.Empty,
                    entry.CollectorNumber ?? string.Empty,
                    entry.Category ?? string.Empty))
            .Select(group => group.First() with { Quantity = group.Sum(entry => entry.Quantity) })
            .OrderBy(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildPatch(string targetSystem, List<DeckEntry> cutEntries, List<DeckEntry> addEntries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CUT (remove these)");
        AppendPatchBody(builder, DeltaExporter.ToText(cutEntries, targetSystem), "No cards to cut.");
        builder.AppendLine();
        builder.AppendLine("# ADD (add these)");
        AppendPatchBody(builder, DeltaExporter.ToText(addEntries, targetSystem), "No cards to add.");
        return builder.ToString().TrimEnd();
    }

    private static void AppendPatchBody(StringBuilder builder, string body, string emptyText)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine(emptyText);
            return;
        }

        builder.AppendLine(body);
    }

    private sealed record ConsolidatedEntryKey(
        string NormalizedName,
        string Board,
        string SetCode,
        string CollectorNumber,
        string Category);
}

/// <summary>
/// Builder-compatible export text plus validation details for a finished Cut Lab list.
/// </summary>
public sealed record CutLabExportResult(
    string MoxfieldFullListText,
    string ArchidektFullListText,
    string MoxfieldPatchText,
    string ArchidektPatchText,
    bool CountOk,
    int OffCount,
    bool HardBlock,
    IReadOnlyList<string> IllegalColorIdentity,
    IReadOnlyList<string> UnverifiedColorIdentity,
    IReadOnlyList<string> BanlistOffenders);
