using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeckFlow.Core.Research;

/// <summary>
/// Represents one parsed EDHREC decklist entry from a bracket cell.
/// </summary>
public sealed record EdhrecCard
{
    /// <summary>
    /// Gets the quantity prefix parsed from the raw deck entry.
    /// </summary>
    public required int Quantity { get; init; }

    /// <summary>
    /// Gets the card name remainder parsed from the raw deck entry.
    /// </summary>
    public required string Name { get; init; }
}

/// <summary>
/// Represents one EDHREC commander x bracket cell read from disk.
/// </summary>
public sealed record EdhrecCell
{
    /// <summary>
    /// Gets the commander name carried by the cell.
    /// </summary>
    public required string Commander { get; init; }

    /// <summary>
    /// Gets the commander's slug from the on-disk contract.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// Gets the EDHREC bracket slug.
    /// </summary>
    public required string Bracket { get; init; }

    /// <summary>
    /// Gets the EDHREC bracket index.
    /// </summary>
    public required int BracketIndex { get; init; }

    /// <summary>
    /// Gets the number of real decks backing the cell.
    /// </summary>
    public required int NDecks { get; init; }

    /// <summary>
    /// Gets a value indicating whether the cell clears the caller-supplied floor.
    /// </summary>
    public required bool Qualifies { get; init; }

    /// <summary>
    /// Gets EDHREC's own land aggregate from the cell.
    /// </summary>
    public required int EdhrecLandCount { get; init; }

    /// <summary>
    /// Gets EDHREC's own basic-land aggregate from the cell.
    /// </summary>
    public required int EdhrecBasicCount { get; init; }

    /// <summary>
    /// Gets EDHREC's own nonbasic-land aggregate from the cell.
    /// </summary>
    public required int EdhrecNonbasicCount { get; init; }

    /// <summary>
    /// Gets the summed quantity across parsed cards.
    /// </summary>
    public required int CardCount { get; init; }

    /// <summary>
    /// Gets the earliest deck save date reported by the cell.
    /// </summary>
    public required string MinSaveDate { get; init; }

    /// <summary>
    /// Gets the latest deck save date reported by the cell.
    /// </summary>
    public required string MaxSaveDate { get; init; }

    /// <summary>
    /// Gets the parsed decklist entries that successfully produced quantity and name.
    /// </summary>
    public required IReadOnlyList<EdhrecCard> Cards { get; init; }

    /// <summary>
    /// Gets the raw decklist entries whose quantity prefixes failed to parse.
    /// </summary>
    public required IReadOnlyList<string> ParseFailures { get; init; }
}

/// <summary>
/// Represents the outcome of reading an EDHREC bracket-cell directory from disk.
/// </summary>
public sealed record EdhrecReadResult
{
    /// <summary>
    /// Gets the valid cells that were read successfully.
    /// </summary>
    public required IReadOnlyList<EdhrecCell> Cells { get; init; }

    /// <summary>
    /// Gets the planned cells that were absent from disk.
    /// </summary>
    public required IReadOnlyList<string> MissingCells { get; init; }

    /// <summary>
    /// Gets invalid cells or planned paths rejected during ingestion, with reasons.
    /// </summary>
    public required IReadOnlyList<string> InvalidCells { get; init; }

    /// <summary>
    /// Gets present cell files that were not planned by the manifest.
    /// </summary>
    public required IReadOnlyList<string> UnexpectedCells { get; init; }

    /// <summary>
    /// Gets cells whose parsed card quantities did not sum to 100.
    /// </summary>
    public required IReadOnlyList<string> CardCountAnomalies { get; init; }

    /// <summary>
    /// Gets the per-cell minimum deck count applied by the reader.
    /// </summary>
    public required int MinCellDeckCount { get; init; }

    /// <summary>
    /// Gets the manifest's selected commander count.
    /// </summary>
    public required int CommandersSelected { get; init; }

    /// <summary>
    /// Gets the manifest's bracket list.
    /// </summary>
    public required IReadOnlyList<string> Brackets { get; init; }

    /// <summary>
    /// Gets the failure message for a fatal read problem, or <see langword="null"/> on success.
    /// </summary>
    public string? Failure { get; init; }
}

/// <summary>
/// Reads the fetcher's on-disk EDHREC commander x bracket cache into typed Core DTOs.
/// </summary>
public static class EdhrecCellReader
{
    private static readonly Regex SlugPattern = new("^[a-z0-9-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, int> BracketIndexes = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["exhibition"] = 1,
        ["core"] = 2,
        ["upgraded"] = 3,
        ["optimized"] = 4,
        ["cedh"] = 5,
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Reads an EDHREC bracket-cell cache from disk.
    /// </summary>
    /// <param name="rootDirectory">The cache root containing <c>manifest.json</c> and <c>cells/</c>.</param>
    /// <param name="minCellDeckCount">The caller-supplied per-cell qualifying floor.</param>
    /// <returns>A failed result for fatal directory or manifest problems, otherwise a populated read result.</returns>
    public static EdhrecReadResult Read(string rootDirectory, int minCellDeckCount)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);

        if (!Directory.Exists(rootDirectory))
        {
            return CreateFailureResult($"EDHREC root directory does not exist: {rootDirectory}", minCellDeckCount);
        }

        string rootFullPath = Path.GetFullPath(rootDirectory);
        string manifestPath = Path.Combine(rootFullPath, "manifest.json");
        ManifestDocument? manifest;

        try
        {
            manifest = JsonSerializer.Deserialize<ManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return CreateFailureResult($"Failed to read manifest at '{manifestPath}': {ex.Message}", minCellDeckCount);
        }

        if (manifest is null)
        {
            return CreateFailureResult($"Failed to deserialize manifest at '{manifestPath}'.", minCellDeckCount);
        }

        IReadOnlyList<string> brackets = manifest.Brackets ?? Array.Empty<string>();
        IReadOnlyList<SelectedCommanderDocument> selectedCommanders = manifest.SelectedCommanders ?? Array.Empty<SelectedCommanderDocument>();

        string cellsDirectory = Path.Combine(rootFullPath, "cells");
        HashSet<string> plannedFileNames = [];
        List<EdhrecCell> cells = [];
        List<string> missingCells = [];
        List<string> invalidCells = [];
        List<string> cardCountAnomalies = [];

        foreach (SelectedCommanderDocument selectedCommander in selectedCommanders)
        {
            string slug = selectedCommander.Slug ?? string.Empty;

            foreach (string bracket in brackets)
            {
                if (!SlugPattern.IsMatch(slug))
                {
                    invalidCells.Add($"Rejected planned cell for slug '{slug}' and bracket '{bracket}': slug failed ^[a-z0-9-]+$ validation.");
                    continue;
                }

                string fileName = $"{slug}__{bracket}.json";
                plannedFileNames.Add(fileName);
                string candidatePath = Path.Combine(cellsDirectory, fileName);
                string resolvedPath = Path.GetFullPath(candidatePath);

                // Why: the manifest is cache content, not trusted input; a crafted slug or bracket must
                // never cause the reader to open a file outside the requested EDHREC root.
                if (!IsUnderRoot(rootFullPath, resolvedPath))
                {
                    invalidCells.Add($"Rejected planned cell '{fileName}': resolved path escaped root '{rootFullPath}'.");
                    continue;
                }

                if (!File.Exists(resolvedPath))
                {
                    missingCells.Add(fileName);
                    continue;
                }

                if (TryReadCell(resolvedPath, minCellDeckCount, out EdhrecCell? cell, out string? invalidReason))
                {
                    cells.Add(cell!);

                    if (cell!.CardCount != 100)
                    {
                        cardCountAnomalies.Add($"{fileName}: parsed card count was {cell.CardCount}.");
                    }
                }
                else
                {
                    invalidCells.Add($"{fileName}: {invalidReason}");
                }
            }
        }

        List<string> unexpectedCells = [];

        if (Directory.Exists(cellsDirectory))
        {
            foreach (string path in Directory.GetFiles(cellsDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(path);

                if (!plannedFileNames.Contains(fileName))
                {
                    unexpectedCells.Add(fileName);
                }
            }
        }

        return new EdhrecReadResult
        {
            Cells = cells,
            MissingCells = missingCells,
            InvalidCells = invalidCells,
            UnexpectedCells = unexpectedCells,
            CardCountAnomalies = cardCountAnomalies,
            MinCellDeckCount = minCellDeckCount,
            CommandersSelected = manifest.CommandersSelected ?? selectedCommanders.Count,
            Brackets = brackets,
            Failure = null,
        };
    }

    private static bool TryReadCell(string cellPath, int minCellDeckCount, out EdhrecCell? cell, out string? invalidReason)
    {
        cell = null;
        invalidReason = null;
        CellDocument? document;

        try
        {
            document = JsonSerializer.Deserialize<CellDocument>(File.ReadAllText(cellPath), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            invalidReason = ex.Message;
            return false;
        }

        if (document is null)
        {
            invalidReason = "Cell JSON deserialized to null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.Slug))
        {
            invalidReason = "Missing required field 'slug'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(document.Bracket))
        {
            invalidReason = "Missing required field 'bracket'.";
            return false;
        }

        if (document.BracketIndex is null)
        {
            invalidReason = "Missing required field 'bracket_index'.";
            return false;
        }

        if (document.NDecks is null)
        {
            invalidReason = "Missing required field 'n_decks'.";
            return false;
        }

        if (document.Deck is null)
        {
            invalidReason = "Missing required field 'deck'.";
            return false;
        }

        if (!BracketIndexes.TryGetValue(document.Bracket, out int expectedBracketIndex))
        {
            invalidReason = $"Unknown bracket '{document.Bracket}'.";
            return false;
        }

        if (document.BracketIndex.Value != expectedBracketIndex)
        {
            invalidReason = $"bracket_index {document.BracketIndex.Value} does not match bracket '{document.Bracket}'.";
            return false;
        }

        // Why: manifest.json min_decks is the commander-selection floor from averages.csv, not the
        // per-cell reporting floor. Qualification must come only from each cell's own n_decks.
        bool qualifies = document.NDecks.Value >= minCellDeckCount;
        List<EdhrecCard> cards = [];
        List<string> parseFailures = [];

        foreach (string? entry in document.Deck)
        {
            if (!TryParseDeckEntry(entry, out EdhrecCard? card))
            {
                parseFailures.Add(entry ?? string.Empty);
                continue;
            }

            cards.Add(card!);
        }

        // Why: earlier plan drafts expected source and estimate-kind constants on disk, but the shipped
        // fetcher never wrote them. Validation must key off the fields that actually exist on disk.
        cell = new EdhrecCell
        {
            Commander = document.Commander ?? string.Empty,
            Slug = document.Slug,
            Bracket = document.Bracket,
            BracketIndex = document.BracketIndex.Value,
            NDecks = document.NDecks.Value,
            Qualifies = qualifies,
            EdhrecLandCount = document.Land ?? 0,
            EdhrecBasicCount = document.Basic ?? 0,
            EdhrecNonbasicCount = document.Nonbasic ?? 0,
            CardCount = cards.Sum(card => card.Quantity),
            MinSaveDate = document.SavedateSummary?.MinDate ?? string.Empty,
            MaxSaveDate = document.SavedateSummary?.MaxDate ?? string.Empty,
            Cards = cards,
            ParseFailures = parseFailures,
        };

        return true;
    }

    private static bool TryParseDeckEntry(string? entry, out EdhrecCard? card)
    {
        card = null;

        if (string.IsNullOrWhiteSpace(entry))
        {
            return false;
        }

        int separatorIndex = entry.IndexOf(' ');

        if (separatorIndex <= 0)
        {
            return false;
        }

        string quantityText = entry[..separatorIndex];
        string cardName = entry[(separatorIndex + 1)..].Trim();

        if (!int.TryParse(quantityText, out int quantity) || quantity < 0 || string.IsNullOrWhiteSpace(cardName))
        {
            return false;
        }

        card = new EdhrecCard
        {
            Quantity = quantity,
            Name = cardName,
        };

        return true;
    }

    private static bool IsUnderRoot(string rootFullPath, string candidateFullPath)
    {
        string normalizedRoot = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        return candidateFullPath.Equals(normalizedRoot, StringComparison.Ordinal)
            || candidateFullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal);
    }

    private static EdhrecReadResult CreateFailureResult(string failure, int minCellDeckCount)
        => new()
        {
            Cells = Array.Empty<EdhrecCell>(),
            MissingCells = Array.Empty<string>(),
            InvalidCells = Array.Empty<string>(),
            UnexpectedCells = Array.Empty<string>(),
            CardCountAnomalies = Array.Empty<string>(),
            MinCellDeckCount = minCellDeckCount,
            CommandersSelected = 0,
            Brackets = Array.Empty<string>(),
            Failure = failure,
        };

    private sealed record ManifestDocument
    {
        public IReadOnlyList<string>? Brackets { get; init; }

        public int? CommandersSelected { get; init; }

        public IReadOnlyList<SelectedCommanderDocument>? SelectedCommanders { get; init; }
    }

    private sealed record SelectedCommanderDocument
    {
        public string? Slug { get; init; }
    }

    private sealed record CellDocument
    {
        public int? Basic { get; init; }

        public string? Bracket { get; init; }

        public int? BracketIndex { get; init; }

        public string? Commander { get; init; }

        public IReadOnlyList<string?>? Deck { get; init; }

        public int? Land { get; init; }

        public int? NDecks { get; init; }

        public int? Nonbasic { get; init; }

        public SavedateSummaryDocument? SavedateSummary { get; init; }

        public string? Slug { get; init; }
    }

    private sealed record SavedateSummaryDocument
    {
        public string? MaxDate { get; init; }

        public string? MinDate { get; init; }
    }
}
