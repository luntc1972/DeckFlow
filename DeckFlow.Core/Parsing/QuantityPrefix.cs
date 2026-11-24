using System.Globalization;
using System.Text.RegularExpressions;

namespace DeckFlow.Core.Parsing;

/// <summary>Defines the shared pasted-line quantity prefix, such as "1 ", "1x ", or "1X ".</summary>
internal static partial class QuantityPrefix
{
    internal const int MaxQuantity = 999;

    /// <summary>Matches a quantity prefix and captures the quantity and the rest of the line.</summary>
    [GeneratedRegex(@"^(?<quantity>[0-9]+)[xX]?\s+(?<rest>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    internal static partial Regex Pattern();

    internal static bool TryParseQuantity(string value, out int quantity)
    {
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)
            && quantity is >= 1 and <= MaxQuantity;
    }
}
