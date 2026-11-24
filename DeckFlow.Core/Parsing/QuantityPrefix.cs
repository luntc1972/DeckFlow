using System.Globalization;
using System.Text.RegularExpressions;

namespace DeckFlow.Core.Parsing;

/// <summary>Defines the shared pasted-line quantity prefix, such as "1 ", "1x ", or "1X ".</summary>
internal static partial class QuantityPrefix
{
    private const string OutOfRangeMessagePrefix = "Quantity in \"";

    /// <summary>Largest accepted pasted quantity; larger values are typos, not real deck counts.</summary>
    internal const int MaxQuantity = 999;

    /// <summary>Matches a quantity prefix and captures the quantity and the rest of the line.</summary>
    [GeneratedRegex(@"^(?<quantity>[0-9]+)[xX]?\s+(?<rest>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    internal static partial Regex Pattern();

    /// <summary>Parses an ASCII quantity capture and accepts it only within 0..<see cref="MaxQuantity"/>.</summary>
    internal static bool TryParseQuantity(string value, out int quantity)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)
            && quantity is >= 0 and <= MaxQuantity;
    }

    /// <summary>Builds the one user-facing message for a quantity outside 0..<see cref="MaxQuantity"/>.</summary>
    /// <remarks>Why: the quoted line identifies the offender, so callers need not thread a line number.</remarks>
    internal static string OutOfRangeMessage(string line) =>
        $"{OutOfRangeMessagePrefix}{line}\" is outside the allowed range 0..{MaxQuantity}.";

    /// <summary>Identifies quantity-range errors so paste auto-detection can preserve them.</summary>
    internal static bool IsOutOfRangeMessage(string message) =>
        message.StartsWith(OutOfRangeMessagePrefix, StringComparison.Ordinal);
}
