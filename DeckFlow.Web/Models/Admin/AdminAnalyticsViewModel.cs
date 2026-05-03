using System.Globalization;
using System.Text;

namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for /Admin/Analytics. Range filter (today/7d/30d/all) drives the
/// SUM/GROUP BY over <c>request_metrics</c>; per-route sparkline covers the last
/// 14 days regardless of selected range (D-16..D-18).
/// </summary>
public sealed class AdminAnalyticsViewModel
{
    /// <summary>Valid range parameter values per D-16. Invalid input falls back to "7d".</summary>
    public static readonly SortedSet<string> AllowedRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        "today", "7d", "30d", "all",
    };

    /// <summary>The validated range currently displayed (never null; always in AllowedRanges).</summary>
    public required string Range { get; init; }

    /// <summary>Top routes sorted by hit_count DESC, capped at 50 per D-17.</summary>
    public required IReadOnlyList<RouteRow> Routes { get; init; }

    /// <summary>One aggregated row per route_key for the selected time window.</summary>
    /// <param name="RouteKey">The normalized route identifier recorded by the middleware.</param>
    /// <param name="Hits">Total hit count across all status classes in the window.</param>
    /// <param name="UniqueIps">Distinct IP hashes seen for this route in the window.</param>
    /// <param name="ErrorRate">Fraction 0–1 of requests that were errors (4xx excl. 404 + 5xx).</param>
    /// <param name="HitsByDay">14-element array: index 0 = 13 days ago, index 13 = today UTC (D-18).</param>
    public sealed record RouteRow(
        string RouteKey,
        long Hits,
        long UniqueIps,
        double ErrorRate,
        int[] HitsByDay);

    /// <summary>
    /// Renders a 14-day bar-chart sparkline as inline SVG per D-18.
    /// Omits bars for days with zero traffic (gap is intentional signal).
    /// Uses <c>currentColor</c> so the color is controlled by CSS
    /// (<c>.admin-sparkline { color: var(--muted); }</c>).
    /// No JS library, no external dependency.
    /// </summary>
    /// <param name="hitsByDay">14-element array oldest-first. Null or wrong length renders empty SVG.</param>
    /// <returns>Inline SVG string safe to emit via <c>Html.Raw</c>.</returns>
    public static string RenderSparkline(int[] hitsByDay)
    {
        const int width = 120;
        const int height = 24;
        const int barCount = 14;
        const int gap = 1;
        var barWidth = (width - (barCount - 1) * gap) / (double)barCount;

        if (hitsByDay is null || hitsByDay.Length != barCount)
        {
            return $"<svg width=\"{width}\" height=\"{height}\" aria-hidden=\"true\"></svg>";
        }

        var max = 0;
        for (var i = 0; i < hitsByDay.Length; i++)
        {
            if (hitsByDay[i] > max) max = hitsByDay[i];
        }

        if (max == 0)
        {
            return $"<svg width=\"{width}\" height=\"{height}\" aria-hidden=\"true\"></svg>";
        }

        var sb = new StringBuilder(512);
        sb.Append("<svg width=\"").Append(width).Append("\" height=\"").Append(height)
          .Append("\" viewBox=\"0 0 ").Append(width).Append(' ').Append(height)
          .Append("\" role=\"img\" aria-label=\"14-day traffic sparkline\">");

        for (var i = 0; i < barCount; i++)
        {
            var v = hitsByDay[i];
            if (v <= 0) continue;   // D-18: omit empty days — gap is signal
            var barHeight = (int)Math.Round(v / (double)max * (height - 2));
            if (barHeight < 1) barHeight = 1;
            var x = Math.Round(i * (barWidth + gap), 2);
            var y = height - barHeight;
            sb.Append("<rect x=\"").Append(x.ToString(CultureInfo.InvariantCulture))
              .Append("\" y=\"").Append(y)
              .Append("\" width=\"").Append(barWidth.ToString("0.##", CultureInfo.InvariantCulture))
              .Append("\" height=\"").Append(barHeight)
              .Append("\" fill=\"currentColor\" />");
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>Formats an error rate fraction as "X.X%" for display in the table.</summary>
    /// <param name="rate">Value in range 0–1 (e.g. 0.013 → "1.3%").</param>
    public static string FormatErrorRate(double rate)
        => (rate * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%";
}
