using System.Text.RegularExpressions;

namespace DeckFlow.Core.Parsing;

/// <summary>Defines the shared pasted-line quantity prefix, such as "1 ", "1x ", or "1X ".</summary>
internal static partial class QuantityPrefix
{
    /// <summary>Matches a quantity prefix and captures the quantity and the rest of the line.</summary>
    [GeneratedRegex(@"^(?<quantity>\d+)[xX]?\s+(?<rest>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    internal static partial Regex Pattern();
}
