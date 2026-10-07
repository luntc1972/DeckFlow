using System.Globalization;
using RestSharp;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Reads and parses Retry-After response headers.
/// </summary>
public static class RetryAfterHeader
{
    /// <summary>
    /// Gets the Retry-After header value from a response.
    /// </summary>
    /// <param name="response">The response that may contain the header.</param>
    /// <returns>The header value, or null when absent.</returns>
    public static string? Read(RestResponse response)
        => response.Headers?
            .FirstOrDefault(header => string.Equals(header.Name, "Retry-After", StringComparison.OrdinalIgnoreCase))?
            .Value?.ToString();

    /// <summary>
    /// Parses a Retry-After value as either delta seconds or an HTTP date.
    /// </summary>
    /// <param name="raw">The raw header value.</param>
    /// <param name="utcNow">Supplies the current UTC time for HTTP-date values.</param>
    /// <returns>The requested delay, or null when the value is missing or invalid.</returns>
    public static TimeSpan? Parse(string? raw, Func<DateTimeOffset> utcNow)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
        {
            var delta = when - utcNow();
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        return null;
    }
}
