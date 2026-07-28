using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Research;

/// <summary>
/// Carries the accumulated EDHREC bulk totals for one commander.
/// </summary>
public sealed record EdhrecBulkCommanderTotals
{
    /// <summary>
    /// Gets the commander's name from <c>edhrec.csv</c>.
    /// </summary>
    public required string Commander { get; init; }

    /// <summary>
    /// Gets the solo-row <c>number_decks</c> denominator used for this commander.
    /// </summary>
    public required long Denominator { get; init; }

    /// <summary>
    /// Gets the number of EDHREC rows consumed for this commander.
    /// </summary>
    public required int RowsConsumed { get; init; }

    /// <summary>
    /// Gets the maximum inclusion ratio observed across the commander's cards.
    /// </summary>
    public required double MaxRatio { get; init; }

    /// <summary>
    /// Gets the card that produced <see cref="MaxRatio"/>.
    /// </summary>
    public required string MaxRatioCard { get; init; }

    /// <summary>
    /// Gets the sum of all inclusion rates for this commander.
    /// </summary>
    public required double TotalInclusionRate { get; init; }

    /// <summary>
    /// Gets the expected count accumulated for each requested role key.
    /// </summary>
    public required IReadOnlyDictionary<string, double> ExpectedByRole { get; init; }
}

/// <summary>
/// Carries the structured details for a commander excluded by the denominator gate.
/// </summary>
public sealed record EdhrecDenominatorMismatch
{
    /// <summary>
    /// Gets the commander whose denominator failed validation.
    /// </summary>
    public required string Commander { get; init; }

    /// <summary>
    /// Gets the card that produced the impossible ratio.
    /// </summary>
    public required string Card { get; init; }

    /// <summary>
    /// Gets the observed count for <see cref="Card"/>.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Gets the solo-row denominator used for the commander.
    /// </summary>
    public required long Denominator { get; init; }

    /// <summary>
    /// Gets the impossible unclamped ratio that triggered exclusion.
    /// </summary>
    public required double Ratio { get; init; }
}

/// <summary>
/// Carries the structured details for one malformed EDHREC row.
/// </summary>
public sealed record EdhrecMalformedRow
{
    /// <summary>
    /// Gets the one-based line number from <c>edhrec.csv</c>.
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// Gets the parsed CSV field count for the malformed row.
    /// </summary>
    public required int FieldCount { get; init; }

    /// <summary>
    /// Gets a truncated excerpt of the raw line for investigation.
    /// </summary>
    public required string RawLineExcerpt { get; init; }
}

/// <summary>
/// Represents the result of accumulating EDHREC bulk card counts into per-commander totals.
/// </summary>
public sealed record EdhrecBulkGridResult
{
    /// <summary>
    /// Gets the commanders whose denominators validated and whose totals were accumulated.
    /// </summary>
    public required IReadOnlyList<EdhrecBulkCommanderTotals> Commanders { get; init; }

    /// <summary>
    /// Gets the commanders excluded by the denominator gate, with the offending row details.
    /// </summary>
    public required IReadOnlyList<EdhrecDenominatorMismatch> DenominatorMismatches { get; init; }

    /// <summary>
    /// Gets commanders present in <c>edhrec.csv</c> with no solo denominator row in <c>averages.csv</c>.
    /// </summary>
    public required IReadOnlyList<string> MissingDenominators { get; init; }

    /// <summary>
    /// Gets the number of malformed EDHREC rows skipped during the read.
    /// </summary>
    public required int MalformedRows { get; init; }

    /// <summary>
    /// Gets the first malformed EDHREC rows retained for investigation.
    /// </summary>
    public required IReadOnlyList<EdhrecMalformedRow> MalformedRowDetails { get; init; }

    /// <summary>
    /// Gets the number of malformed EDHREC rows omitted from <see cref="MalformedRowDetails"/>.
    /// </summary>
    public required int MalformedRowDetailsOmittedCount { get; init; }

    /// <summary>
    /// Gets the distinct valid card-name count observed while streaming pass 2.
    /// </summary>
    public required int DistinctCardCount { get; init; }

    /// <summary>
    /// Gets the number of valid EDHREC data rows read during pass 2.
    /// </summary>
    public required long RowsRead { get; init; }

    /// <summary>
    /// Gets the fatal failure message, or <see langword="null"/> on success.
    /// </summary>
    public string? Failure { get; init; }
}

/// <summary>
/// Streams the sanctioned EDHREC bulk card-count archive without materializing all rows in memory.
/// </summary>
public static class EdhrecCardCountsReader
{
    // Why: the archive is large enough that a badly malformed file could contain huge numbers of bad
    // rows; retaining only the first 50 keeps investigation actionable without letting diagnostics
    // grow without bound.
    private const int MalformedRowDetailsCap = 50;
    private const int MalformedRowExcerptMaxLength = 256;

    /// <summary>
    /// Reads every distinct valid card name from the EDHREC bulk archive.
    /// </summary>
    /// <param name="edhrecCsvPath">The path to <c>edhrec.csv</c>.</param>
    /// <param name="malformedRows">Receives the number of malformed rows skipped.</param>
    /// <returns>The distinct valid card names observed in the archive.</returns>
    public static IReadOnlyCollection<string> ReadDistinctCardNames(string edhrecCsvPath, out int malformedRows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edhrecCsvPath);

        malformedRows = 0;
        var distinctCards = new HashSet<string>(StringComparer.Ordinal);

        using StreamReader reader = OpenReader(edhrecCsvPath);
        HeaderIndexes header = ReadEdhrecHeader(reader);

        string? line;
        int lineNumber = 1;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                malformedRows++;
                continue;
            }

            if (!TryParseEdhrecRow(line, header, out string _, out string cardName, out int _, out int _))
            {
                malformedRows++;
                continue;
            }

            distinctCards.Add(cardName);
        }

        return distinctCards.ToArray();
    }

    /// <summary>
    /// Reads solo <c>number_decks</c> denominators from <c>averages.csv</c>.
    /// </summary>
    /// <param name="averagesCsvPath">The path to <c>averages.csv</c>.</param>
    /// <returns>The solo denominators keyed by commander name.</returns>
    public static IReadOnlyDictionary<string, long> ReadSoloDenominators(string averagesCsvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(averagesCsvPath);

        var denominators = new Dictionary<string, long>(StringComparer.Ordinal);
        using StreamReader reader = OpenReader(averagesCsvPath);
        HeaderIndexes header = ReadAveragesHeader(reader);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!TryParseAveragesRow(line, header, out string commander, out string? partnerName, out long denominator))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(partnerName))
            {
                continue;
            }

            if (denominators.TryGetValue(commander, out long existing))
            {
                denominators[commander] = Math.Max(existing, denominator);
                continue;
            }

            denominators.Add(commander, denominator);
        }

        return denominators;
    }

    /// <summary>
    /// Accumulates expected-by-role totals over the EDHREC bulk archive.
    /// </summary>
    /// <param name="edhrecCsvPath">The path to <c>edhrec.csv</c>.</param>
    /// <param name="denominators">The solo denominators keyed by commander name.</param>
    /// <param name="cardRoles">The caller-supplied card-to-role map.</param>
    /// <param name="roleKeys">The role keys that should be accumulated.</param>
    /// <returns>The accumulated grid result, or a failure result for a fatal read problem.</returns>
    public static EdhrecBulkGridResult Accumulate(
        string edhrecCsvPath,
        IReadOnlyDictionary<string, long> denominators,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardRoles,
        IReadOnlyCollection<string> roleKeys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edhrecCsvPath);
        ArgumentNullException.ThrowIfNull(denominators);
        ArgumentNullException.ThrowIfNull(cardRoles);
        ArgumentNullException.ThrowIfNull(roleKeys);

        try
        {
            using StreamReader reader = OpenReader(edhrecCsvPath);
            HeaderIndexes header = ReadEdhrecHeader(reader);

            var roleKeySet = new HashSet<string>(roleKeys, StringComparer.Ordinal);
            var distinctCards = new HashSet<string>(StringComparer.Ordinal);
            var missingDenominators = new HashSet<string>(StringComparer.Ordinal);
            var accumulators = new Dictionary<string, CommanderAccumulator>(StringComparer.Ordinal);
            var malformedRowDetails = new List<EdhrecMalformedRow>(MalformedRowDetailsCap);

            int malformedRows = 0;
            int malformedRowDetailsOmittedCount = 0;
            long rowsRead = 0;
            int lineNumber = 1;

            // Why: edhrec.csv is 618 MB / 14,150,220 lines, so any API that returns all lines is
            // disqualified. This design takes two streaming passes because a single pass would require
            // holding either all rows or all counts, and 14.15 million rows is where "just load it"
            // stops working.
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    malformedRows++;
                    RecordMalformedRow(malformedRowDetails, ref malformedRowDetailsOmittedCount, lineNumber, 0, line);
                    continue;
                }

                if (!TryParseEdhrecRow(line, header, out string commander, out string cardName, out int count, out int lineFieldCount))
                {
                    malformedRows++;
                    RecordMalformedRow(malformedRowDetails, ref malformedRowDetailsOmittedCount, lineNumber, lineFieldCount, line);
                    continue;
                }

                rowsRead++;
                distinctCards.Add(cardName);

                if (!denominators.TryGetValue(commander, out long denominator))
                {
                    missingDenominators.Add(commander);
                    continue;
                }

                CommanderAccumulator accumulator = GetOrCreateAccumulator(accumulators, commander, denominator, roleKeySet);
                accumulator.RowsConsumed++;

                double ratio = count / (double)denominator;
                accumulator.TotalInclusionRate += ratio;
                if (ratio > accumulator.MaxRatio)
                {
                    accumulator.MaxRatio = ratio;
                    accumulator.MaxRatioCard = cardName;
                    accumulator.MaxRatioCount = count;
                }

                if (cardRoles.TryGetValue(cardName, out IReadOnlyList<string>? roles))
                {
                    foreach (string role in roles)
                    {
                        if (!roleKeySet.Contains(role))
                        {
                            continue;
                        }

                        accumulator.ExpectedByRole[role] += ratio;
                    }
                }
            }

            List<EdhrecDenominatorMismatch> denominatorMismatches = [];
            List<EdhrecBulkCommanderTotals> commanders = [];

            foreach (CommanderAccumulator accumulator in accumulators.Values.OrderBy(acc => acc.Commander, StringComparer.Ordinal))
            {
                // Why: a ratio above 1.0 is structurally impossible because a card cannot appear in
                // more of a commander's decks than the commander has decks. When it happens, the
                // denominator is wrong for that commander, most plausibly due to the solo-versus-partner
                // mismatch, and reporting that finding is more valuable than any figure the commander
                // would have produced.
                if (accumulator.MaxRatio > 1.0d)
                {
                    denominatorMismatches.Add(new EdhrecDenominatorMismatch
                    {
                        Commander = accumulator.Commander,
                        Card = accumulator.MaxRatioCard,
                        Count = accumulator.MaxRatioCount,
                        Denominator = accumulator.Denominator,
                        Ratio = accumulator.MaxRatio,
                    });
                    continue;
                }

                commanders.Add(new EdhrecBulkCommanderTotals
                {
                    Commander = accumulator.Commander,
                    Denominator = accumulator.Denominator,
                    RowsConsumed = accumulator.RowsConsumed,
                    MaxRatio = accumulator.MaxRatio,
                    MaxRatioCard = accumulator.MaxRatioCard,
                    TotalInclusionRate = accumulator.TotalInclusionRate,
                    ExpectedByRole = accumulator.ExpectedByRole
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                });
            }

            return new EdhrecBulkGridResult
            {
                Commanders = commanders,
                // Why: Task 3 needs the worst five mismatches directly from this artifact, so keep
                // them in numeric ratio order instead of forcing a string parse and re-sort later.
                DenominatorMismatches = denominatorMismatches
                    .OrderByDescending(mismatch => mismatch.Ratio)
                    .ToArray(),
                MissingDenominators = missingDenominators.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                MalformedRows = malformedRows,
                MalformedRowDetails = malformedRowDetails,
                MalformedRowDetailsOmittedCount = malformedRowDetailsOmittedCount,
                DistinctCardCount = distinctCards.Count,
                RowsRead = rowsRead,
                Failure = null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return new EdhrecBulkGridResult
            {
                Commanders = Array.Empty<EdhrecBulkCommanderTotals>(),
                DenominatorMismatches = Array.Empty<EdhrecDenominatorMismatch>(),
                MissingDenominators = Array.Empty<string>(),
                MalformedRows = 0,
                MalformedRowDetails = Array.Empty<EdhrecMalformedRow>(),
                MalformedRowDetailsOmittedCount = 0,
                DistinctCardCount = 0,
                RowsRead = 0,
                Failure = ex.Message,
            };
        }
    }

    private static StreamReader OpenReader(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"CSV file does not exist: {path}", path);
        }

        return new StreamReader(path);
    }

    private static HeaderIndexes ReadEdhrecHeader(StreamReader reader)
    {
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new FormatException("EDHREC CSV header row is missing.");
        }

        IReadOnlyList<string> header = ParseCsvLine(headerLine);
        return new HeaderIndexes(
            GetRequiredColumnIndex(header, "commander"),
            GetRequiredColumnIndex(header, "card"),
            GetRequiredColumnIndex(header, "count"));
    }

    private static HeaderIndexes ReadAveragesHeader(StreamReader reader)
    {
        string? headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new FormatException("Averages CSV header row is missing.");
        }

        IReadOnlyList<string> header = ParseCsvLine(headerLine);
        return new HeaderIndexes(
            GetRequiredColumnIndex(header, "commander"),
            GetRequiredColumnIndex(header, "commander2"),
            GetRequiredColumnIndex(header, "number_decks"));
    }

    private static bool TryParseEdhrecRow(
        string line,
        HeaderIndexes header,
        out string commander,
        out string cardName,
        out int count,
        out int lineFieldCount)
    {
        IReadOnlyList<string> fields = ParseCsvLine(line);
        lineFieldCount = fields.Count;

        commander = string.Empty;
        cardName = string.Empty;
        count = 0;

        int requiredFieldCount = Math.Max(Math.Max(header.First, header.Second), header.Third) + 1;
        if (fields.Count < requiredFieldCount)
        {
            return false;
        }

        commander = fields[header.First].Trim();
        cardName = fields[header.Second].Trim();
        return !string.IsNullOrWhiteSpace(commander)
            && !string.IsNullOrWhiteSpace(cardName)
            && int.TryParse(fields[header.Third], CultureInfo.InvariantCulture, out count);
    }

    private static bool TryParseAveragesRow(
        string line,
        HeaderIndexes header,
        out string commander,
        out string? partnerName,
        out long denominator)
    {
        IReadOnlyList<string> fields = ParseCsvLine(line);
        commander = string.Empty;
        partnerName = null;
        denominator = 0;

        int requiredFieldCount = Math.Max(Math.Max(header.First, header.Second), header.Third) + 1;
        if (fields.Count < requiredFieldCount)
        {
            return false;
        }

        commander = fields[header.First].Trim();
        partnerName = NullIfWhiteSpace(fields[header.Second]);
        return !string.IsNullOrWhiteSpace(commander)
            && long.TryParse(fields[header.Third], CultureInfo.InvariantCulture, out denominator);
    }

    private static CommanderAccumulator GetOrCreateAccumulator(
        IDictionary<string, CommanderAccumulator> accumulators,
        string commander,
        long denominator,
        IReadOnlyCollection<string> roleKeys)
    {
        if (accumulators.TryGetValue(commander, out CommanderAccumulator? existing))
        {
            return existing;
        }

        var expectedByRole = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string roleKey in roleKeys)
        {
            expectedByRole[roleKey] = 0d;
        }

        var created = new CommanderAccumulator
        {
            Commander = commander,
            Denominator = denominator,
            MaxRatioCard = string.Empty,
            ExpectedByRole = expectedByRole,
        };

        accumulators.Add(commander, created);
        return created;
    }

    private static int GetRequiredColumnIndex(IReadOnlyList<string> header, string name)
    {
        for (int index = 0; index < header.Count; index++)
        {
            if (string.Equals(header[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        throw new FormatException($"CSV header is missing required column '{name}'.");
    }

    private static string? NullIfWhiteSpace(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> ParseCsvLine(string line)
    {
        // Why: EdhrecAveragesConverter documents the same EDHREC-dump assumption: quoted fields do
        // not contain embedded newlines, so this parser intentionally does not span multiple records.
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int index = 0; index < line.Length; index++)
        {
            char ch = line[index];
            if (ch == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static void RecordMalformedRow(
        ICollection<EdhrecMalformedRow> malformedRowDetails,
        ref int omittedCount,
        int lineNumber,
        int fieldCount,
        string line)
    {
        if (malformedRowDetails.Count >= MalformedRowDetailsCap)
        {
            omittedCount++;
            return;
        }

        malformedRowDetails.Add(new EdhrecMalformedRow
        {
            LineNumber = lineNumber,
            FieldCount = fieldCount,
            RawLineExcerpt = TruncateForDiagnostic(line),
        });
    }

    private static string TruncateForDiagnostic(string value)
    {
        if (value.Length <= MalformedRowExcerptMaxLength)
        {
            return value;
        }

        return value[..MalformedRowExcerptMaxLength];
    }

    private sealed record HeaderIndexes(int First, int Second, int Third);

    private sealed class CommanderAccumulator
    {
        public required string Commander { get; init; }

        public required long Denominator { get; init; }

        public required Dictionary<string, double> ExpectedByRole { get; init; }

        public required string MaxRatioCard { get; set; }

        public int MaxRatioCount { get; set; }

        public int RowsConsumed { get; set; }

        public double MaxRatio { get; set; }

        public double TotalInclusionRate { get; set; }
    }
}
