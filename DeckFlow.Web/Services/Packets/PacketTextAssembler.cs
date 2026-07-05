using System.Text;
using DeckFlow.Core.Models;

namespace DeckFlow.Web.Services.Packets;

/// <summary>
/// Shared prompt-ASSEMBLY mechanics (Cluster D/E from Phase 83 research) reused by the packet
/// services: the sectioned Commander/Mainboard/Possible-Includes decklist text block, and the
/// key:value request-context line writer. This class owns STRUCTURE only — no prompt prose, no
/// combo-reference formatting (Cluster F), no cache-key text (Cluster C). Per ADR-0001, per-AI
/// prompt prose stays hand-authored in <c>PromptBuilders/*</c>; this assembler is not a shared
/// prose helper.
/// </summary>
internal static class PacketTextAssembler
{
    /// <summary>
    /// Builds the "Commander" / "Mainboard" / "Possible Includes" sectioned plain-text decklist
    /// block reproduced byte-for-byte from Analysis/Comparison/Primer's current output.
    /// </summary>
    /// <remarks>
    /// ASYMMETRY (H1, intentional — do not "fix"): Commander and Mainboard lines flow through
    /// <see cref="FormatDecklistLine"/>, which applies the version `(SET) COLLECTOR` suffix and the
    /// DFC `" // "` left-truncation when <paramref name="includeVersions"/> is <see langword="true"/>,
    /// plus the "[printed as: X]" annotation when <paramref name="oracleNameMap"/> resolves a
    /// different name. Possible-Includes lines use a SEPARATE, plain inline shape —
    /// `"{Quantity} {oracleName} [printed as: {Name}]"` or `"{Quantity} {Name}"` — and NEVER receive
    /// a version suffix or DFC truncation, even when <paramref name="includeVersions"/> is true. This
    /// matches Analysis's current <c>BuildDecklistText</c> exactly.
    /// </remarks>
    internal static string BuildSectionedDecklistText(
        IReadOnlyList<DeckEntry> entries,
        IReadOnlyList<DeckEntry> possibleIncludeEntries,
        bool includeVersions = false,
        IReadOnlyDictionary<string, string>? oracleNameMap = null)
    {
        var commanderLines = entries
            .Where(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => FormatDecklistLine(entry, includeVersions, oracleNameMap))
            .ToList();
        var mainboardLines = entries
            .Where(entry => !string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => FormatDecklistLine(entry, includeVersions, oracleNameMap))
            .ToList();

        var builder = new StringBuilder();
        AppendCommanderSection(builder, commanderLines);
        AppendMainboardSection(builder, mainboardLines);
        AppendPossibleIncludesSection(builder, possibleIncludeEntries, oracleNameMap);

        return builder.ToString().TrimEnd();
    }

    private static void AppendCommanderSection(StringBuilder builder, IReadOnlyList<string> commanderLines)
    {
        if (commanderLines.Count == 0)
        {
            return;
        }

        builder.AppendLine("Commander");
        foreach (var line in commanderLines)
        {
            builder.AppendLine(line);
        }

        builder.AppendLine();
    }

    private static void AppendMainboardSection(StringBuilder builder, IReadOnlyList<string> mainboardLines)
    {
        builder.AppendLine("Mainboard");
        foreach (var line in mainboardLines)
        {
            builder.AppendLine(line);
        }
    }

    private static void AppendPossibleIncludesSection(
        StringBuilder builder,
        IReadOnlyList<DeckEntry> possibleIncludeEntries,
        IReadOnlyDictionary<string, string>? oracleNameMap)
    {
        if (possibleIncludeEntries.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine("Possible Includes");
        foreach (var entry in possibleIncludeEntries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine(FormatPossibleIncludeLine(entry, oracleNameMap));
        }
    }

    // Why: Possible-Includes lines are a deliberately DIFFERENT, plain shape from
    // FormatDecklistLine — never a version suffix, never DFC-slash truncation — even when the
    // caller passed includeVersions=true for the commander/mainboard sections above. Reproducing
    // Analysis's current BuildDecklistText inline selector exactly (research H1).
    private static string FormatPossibleIncludeLine(DeckEntry entry, IReadOnlyDictionary<string, string>? oracleNameMap)
    {
        if (oracleNameMap is not null && oracleNameMap.TryGetValue(entry.Name, out var oracleName)
            && !string.Equals(oracleName, entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            return $"{entry.Quantity} {oracleName} [printed as: {entry.Name}]";
        }

        return $"{entry.Quantity} {entry.Name}";
    }

    private static string FormatDecklistLine(DeckEntry entry, bool includeVersions, IReadOnlyDictionary<string, string>? oracleNameMap)
    {
        var name = entry.Name;
        string? printedAs = null;
        if (oracleNameMap is not null && oracleNameMap.TryGetValue(entry.Name, out var oracleName)
            && !string.Equals(oracleName, entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            printedAs = entry.Name;
            name = oracleName;
        }
        if (includeVersions)
        {
            var slash = name.IndexOf(" // ", StringComparison.Ordinal);
            if (slash >= 0) name = name[..slash].TrimEnd();
        }
        var line = $"{entry.Quantity} {name}";
        if (includeVersions && !string.IsNullOrWhiteSpace(entry.SetCode))
        {
            line += $" ({entry.SetCode.ToUpperInvariant()})";
            if (!string.IsNullOrWhiteSpace(entry.CollectorNumber))
                line += $" {entry.CollectorNumber}";
        }
        if (printedAs is not null)
        {
            line += $" [printed as: {printedAs}]";
        }
        return line;
    }

    /// <summary>
    /// Appends exactly <c>{key}: {normalizeSingleLine(value, fallback)}</c> followed by a newline —
    /// byte-identical to the current inlined per-field request-context pattern repeated across all
    /// four packet services. The normalizer is supplied by the caller as a delegate: this method
    /// does NOT reference any concrete <c>NormalizeSingleLine</c> implementation, since the three
    /// services' normalizers are not byte-equivalent (see 83-RESEARCH.md do_not_unify).
    /// </summary>
    internal static void AppendKeyValueLine(
        StringBuilder builder,
        string key,
        string? value,
        string fallback,
        Func<string?, string, string> normalizeSingleLine)
    {
        builder.AppendLine($"{key}: {normalizeSingleLine(value, fallback)}");
    }
}
