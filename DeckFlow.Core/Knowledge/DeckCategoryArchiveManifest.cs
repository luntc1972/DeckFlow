using System.Text;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Records the exact contributions needed to reverse one deck's archive operation.
/// </summary>
internal sealed record DeckCategoryArchiveManifest(
    IReadOnlyList<(long CardId, string Category, string Board, int DeckCount)> Observations,
    IReadOnlyList<(long CardId, string Board, int DeckCount)> Totals,
    IReadOnlyList<(long CardId, string CanonicalCategory, string Board, int IncludedDeckCount, int FallbackDeckCount, int IncludedLabelRows)> Contributions)
{
    internal const int FormatVersion = 1;

    /// <summary>
    /// Encodes all three sections using little-endian BCL primitives and UTF-8 strings.
    /// </summary>
    public byte[] Encode()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Observations.Count);
        foreach (var row in Observations)
        {
            writer.Write(row.CardId);
            writer.Write(row.Category);
            writer.Write(row.Board);
            writer.Write(row.DeckCount);
        }
        writer.Write(Totals.Count);
        foreach (var row in Totals)
        {
            writer.Write(row.CardId);
            writer.Write(row.Board);
            writer.Write(row.DeckCount);
        }
        // Why: reserve the contribution section in version 1 before the fold starts populating it.
        writer.Write(Contributions.Count);
        foreach (var row in Contributions)
        {
            writer.Write(row.CardId);
            writer.Write(row.CanonicalCategory);
            writer.Write(row.Board);
            writer.Write(row.IncludedDeckCount);
            writer.Write(row.FallbackDeckCount);
            writer.Write(row.IncludedLabelRows);
        }
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>
    /// Decodes the payload using its separately stored format version; unknown versions fail closed.
    /// </summary>
    public static DeckCategoryArchiveManifest Decode(int formatVersion, byte[] payload)
    {
        if (formatVersion != FormatVersion)
        {
            throw new InvalidDataException($"Unknown category archive manifest version: {formatVersion}.");
        }
        using var stream = new MemoryStream(payload, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var observations = ReadRows(reader, r => (r.ReadInt64(), r.ReadString(), r.ReadString(), r.ReadInt32()));
        var totals = ReadRows(reader, r => (r.ReadInt64(), r.ReadString(), r.ReadInt32()));
        var contributions = ReadRows(reader, r => (r.ReadInt64(), r.ReadString(), r.ReadString(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
        if (stream.Position != stream.Length)
        {
            throw new InvalidDataException("Unexpected trailing category archive manifest data.");
        }
        return new DeckCategoryArchiveManifest(observations, totals, contributions);
    }

    private static IReadOnlyList<T> ReadRows<T>(BinaryReader reader, Func<BinaryReader, T> readRow)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Invalid category archive manifest row count.");
        }
        var rows = new List<T>();
        for (var index = 0; index < count; index++)
        {
            rows.Add(readRow(reader));
        }
        return rows;
    }
}
