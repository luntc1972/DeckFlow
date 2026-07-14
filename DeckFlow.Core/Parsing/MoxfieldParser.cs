using System.Text.RegularExpressions;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Core.Parsing;

/// <summary>
/// Parses Moxfield plain-text deck exports into <see cref="DeckFlow.Core.Models.DeckEntry"/> lists.
/// Also tolerates the Arena-family export dialects that share the same line grammar: MTG Arena /
/// MTGGoldfish "About" + "Name ..." preambles and legacy .dec "SB:" sideboard prefixes.
/// </summary>
public sealed partial class MoxfieldParser : IParser
{
    /// <inheritdoc />
    public List<DeckEntry> ParseFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return ParseText(File.ReadAllText(filePath));
    }

    /// <inheritdoc />
    public List<DeckEntry> ParseText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DeckParseException("Moxfield text is empty.");
        }

        var entries = new List<DeckEntry>();
        var board = "mainboard";
        var commanderSectionActive = false;
        var foundEntries = false;
        var parseableBlocks = new List<ParseableBlock>();
        ParseableBlock? currentBlock = null;
        var blockPrecededByBlankLine = false;
        var pendingHeader = false;
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (currentBlock is not null)
                {
                    parseableBlocks.Add(currentBlock);
                    currentBlock = null;
                }

                if (commanderSectionActive)
                {
                    board = "mainboard";
                    commanderSectionActive = false;
                }

                blockPrecededByBlankLine = foundEntries;
                pendingHeader = false;
                continue;
            }

            if (IsStoppingLine(line) && foundEntries)
            {
                break;
            }

            if (TryGetBoardHeader(line, out var headerBoard))
            {
                if (currentBlock is not null)
                {
                    parseableBlocks.Add(currentBlock);
                    currentBlock = null;
                }

                board = headerBoard;
                commanderSectionActive = headerBoard == "commander";
                pendingHeader = true;
                continue;
            }

            if (IsIgnorableLine(line))
            {
                continue;
            }

            if (!foundEntries && IsDeckNamePreambleLine(line))
            {
                continue;
            }

            var entryLine = line;
            var entryBoard = board;
            var sideboardPrefixed = line.StartsWith("SB:", StringComparison.OrdinalIgnoreCase);
            if (sideboardPrefixed)
            {
                entryLine = line.AsSpan(3).TrimStart().ToString();
                entryBoard = "sideboard";
            }

            if (!TryParseEntry(entryLine, entryBoard, allowImplicitQuantity: true, out var entry))
            {
                if (foundEntries && IsNonDeckTextLine(line))
                {
                    continue;
                }

                throw new DeckParseException($"Unable to parse Moxfield line {i + 1}: \"{line}\"");
            }

            if (entry.Quantity == 0)
            {
                continue;
            }

            // Why: an explicit SB: prefix is a board marker just like a section header, so a block it
            // opens must be exempt from trailing-commander promotion (never promote explicit sideboard).
            currentBlock ??= new ParseableBlock(entries.Count, board, pendingHeader || sideboardPrefixed, blockPrecededByBlankLine);
            entries.Add(entry);
            foundEntries = true;
            blockPrecededByBlankLine = false;
            pendingHeader = false;
        }

        if (entries.Count == 0)
        {
            throw new DeckParseException("Moxfield text did not contain any card lines.");
        }

        if (currentBlock is not null)
        {
            parseableBlocks.Add(currentBlock);
        }

        PromoteTrailingCommanderBlock(entries, parseableBlocks);
        return entries;
    }

    private static void PromoteTrailingCommanderBlock(List<DeckEntry> entries, IReadOnlyList<ParseableBlock> parseableBlocks)
    {
        if (entries.Any(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (parseableBlocks.Count < 2)
        {
            return;
        }

        // Blocks partition the entries contiguously, so each block's entry count is derivable from
        // the next block's start (or the end of the list for the trailing block).
        var trailingBlock = parseableBlocks[^1];
        var previousBlock = parseableBlocks[^2];
        int trailingCount = entries.Count - trailingBlock.EntryStartIndex;
        if (trailingBlock.HasHeader
            || !trailingBlock.PrecededByBlankLine
            || trailingCount > 2
            || !IsSideOrMaybeBoard(previousBlock.Board))
        {
            return;
        }

        var trailingEntries = entries
            .Skip(trailingBlock.EntryStartIndex)
            .Take(trailingCount)
            .ToList();
        if (trailingEntries.Any(entry => entry.Quantity != 1)
            || trailingEntries.Any(entry => !string.Equals(entry.Board, previousBlock.Board, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        for (var i = 0; i < trailingCount; i++)
        {
            var entryIndex = trailingBlock.EntryStartIndex + i;
            entries[entryIndex] = entries[entryIndex] with
            {
                Board = "commander",
                Category = null,
            };
        }
    }

    private static bool TryParseEntry(string line, string board, bool allowImplicitQuantity, out DeckEntry entry)
    {
        entry = default!;

        var quantity = 1;
        var remainder = line;
        var match = QuantityRegex().Match(line);
        if (match.Success)
        {
            quantity = int.Parse(match.Groups["quantity"].Value);
            remainder = match.Groups["rest"].Value.Trim();
        }
        else if (!allowImplicitQuantity)
        {
            return false;
        }

        var hashtagCategories = ExtractHashtagCategories(ref remainder);
        var boardOverride = DetermineBoard(hashtagCategories);
        if (!string.IsNullOrWhiteSpace(boardOverride))
        {
            board = boardOverride;
        }

        var isFoil = false;
        if (remainder.EndsWith("★", StringComparison.Ordinal))
        {
            isFoil = true;
            remainder = remainder[..^1].TrimEnd();
        }

        if (remainder.EndsWith("*F*", StringComparison.OrdinalIgnoreCase))
        {
            isFoil = true;
            remainder = remainder[..^3].TrimEnd();
        }

        // *E* marks an etched-foil finish. Strip it like *F* so the trailing token does not
        // defeat PrintingRegex (its collector group is end-anchored) and leave the set/collector
        // junk in the card name — which would make both the printing and name lookups miss.
        if (remainder.EndsWith("*E*", StringComparison.OrdinalIgnoreCase))
        {
            isFoil = true;
            remainder = remainder[..^3].TrimEnd();
        }

        var setMatch = PrintingRegex().Match(remainder);
        var rawName = remainder;
        string? setCode = null;
        string? collectorNumber = null;
        if (setMatch.Success)
        {
            rawName = setMatch.Groups["name"].Value.Trim();
            setCode = NullIfWhiteSpace(setMatch.Groups["set"].Value);
            collectorNumber = NullIfWhiteSpace(setMatch.Groups["collector"].Value);
        }
        else if (!match.Success && string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        var cleanName = CleanName(rawName);
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            return false;
        }

        entry = new DeckEntry
        {
            Name = cleanName,
            NormalizedName = CardNormalizer.Normalize(cleanName),
            Quantity = quantity,
            Board = board,
            SetCode = setCode,
            CollectorNumber = collectorNumber,
            IsFoil = isFoil,
            Category = NormalizeCategory(hashtagCategories, board),
        };
        return true;
    }

    private static string? DetermineBoard(IReadOnlyList<string> tags)
    {
        if (tags.Any(tag => string.Equals(tag, "sideboard", StringComparison.OrdinalIgnoreCase)))
        {
            return "sideboard";
        }

        if (tags.Any(tag => string.Equals(tag, "maybeboard", StringComparison.OrdinalIgnoreCase)))
        {
            return "maybeboard";
        }

        if (tags.Any(tag => string.Equals(tag, "commander", StringComparison.OrdinalIgnoreCase)))
        {
            return "commander";
        }

        return null;
    }

    private static string? NormalizeCategory(IReadOnlyList<string> tags, string board)
    {
        var categories = tags
            .Where(tag =>
                !string.Equals(tag, "commander", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tag, "sideboard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(tag, "maybeboard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (categories.Count > 0)
        {
            return string.Join(",", categories);
        }

        return board switch
        {
            "maybeboard" => "Maybeboard",
            "sideboard" => "Sideboard",
            _ => null
        };
    }

    private static List<string> ExtractHashtagCategories(ref string remainder)
    {
        var categories = HashtagRegex()
            .Matches(remainder)
            .Select(match => match.Groups["tag"].Value.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        if (categories.Count > 0)
        {
            remainder = HashtagRegex().Replace(remainder, string.Empty).Trim();
        }

        return categories;
    }

    private static bool TryGetBoardHeader(string line, out string board)
    {
        if (IsSectionHeader(line, "Commander"))
        {
            board = "commander";
            return true;
        }

        if (IsSectionHeader(line, "Maybeboard"))
        {
            board = "maybeboard";
            return true;
        }

        if (IsSectionHeader(line, "Sideboard"))
        {
            board = "sideboard";
            return true;
        }

        if (IsSectionHeader(line, "Mainboard") || IsSectionHeader(line, "Deck"))
        {
            board = "mainboard";
            return true;
        }

        if (IsSectionHeader(line, "Possible Includes"))
        {
            board = "maybeboard";
            return true;
        }

        board = string.Empty;
        return false;
    }

    private static string CleanName(string rawName)
    {
        return rawName
            .Replace("★", string.Empty, StringComparison.Ordinal)
            .Replace("*F*", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("*E*", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static bool IsSectionHeader(string line, string header)
        => string.Equals(line.TrimEnd(':'), header, StringComparison.OrdinalIgnoreCase);

    private static bool IsSideOrMaybeBoard(string board) =>
        string.Equals(board, "sideboard", StringComparison.OrdinalIgnoreCase)
        || string.Equals(board, "maybeboard", StringComparison.OrdinalIgnoreCase);

    private static bool IsIgnorableLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var trimmed = line.Trim();
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return true;
        }

        var normalized = trimmed.TrimEnd(':');
        return string.Equals(normalized, "Deck", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "About", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Commander", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Maybeboard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Sideboard", StringComparison.OrdinalIgnoreCase);
    }

    // Why: the caller already trims each line; match the file's GeneratedRegex idiom rather than
    // hand-rolled index checks (the label word, one whitespace run, then any non-blank deck name).
    private static bool IsDeckNamePreambleLine(string line) => DeckNamePreambleRegex().IsMatch(line);

    private static bool IsStoppingLine(string line)
    {
        var normalized = line.Trim().TrimEnd(':');
        return string.Equals(normalized, "Possible names", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Possible name", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Notes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Description", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "Primer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonDeckTextLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return true;
        }

        if (trimmed.StartsWith("-", StringComparison.Ordinal)
            || trimmed.StartsWith("•", StringComparison.Ordinal)
            || trimmed.StartsWith(">", StringComparison.Ordinal))
        {
            return true;
        }

        if (trimmed.Contains("→", StringComparison.Ordinal)
            || trimmed.Contains("👉", StringComparison.Ordinal)
            || trimmed.Contains("🧩", StringComparison.Ordinal)
            || trimmed.Contains("🎯", StringComparison.Ordinal)
            || trimmed.Contains("💡", StringComparison.Ordinal)
            || trimmed.Contains("🔥", StringComparison.Ordinal)
            || trimmed.Contains("⚡", StringComparison.Ordinal)
            || trimmed.Contains("🧠", StringComparison.Ordinal)
            || trimmed.Contains("✅", StringComparison.Ordinal)
            || trimmed.Contains("🚀", StringComparison.Ordinal)
            || trimmed.Contains("❌", StringComparison.Ordinal))
        {
            return true;
        }

        if (char.IsDigit(trimmed[0]))
        {
            return false;
        }

        if (trimmed.Contains("(", StringComparison.Ordinal) && trimmed.Contains(")", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^(?<quantity>\d+)\s+(?<rest>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex QuantityRegex();

    [GeneratedRegex(@"\s+#(?<tag>[A-Za-z0-9][A-Za-z0-9_-]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex HashtagRegex();

    [GeneratedRegex(@"^(?<name>.+?)\s+\((?<set>[^)]+)\)\s+(?<collector>\S+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex PrintingRegex();

    [GeneratedRegex(@"^Name\s+\S", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DeckNamePreambleRegex();

    private sealed record ParseableBlock(int EntryStartIndex, string Board, bool HasHeader, bool PrecededByBlankLine);
}
