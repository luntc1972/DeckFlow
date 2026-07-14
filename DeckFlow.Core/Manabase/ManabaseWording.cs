using System.Globalization;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Shared user-facing wording helpers for advisory manabase counts.
/// </summary>
public static class ManabaseWording
{
    /// <summary>
    /// Convert a fractional advisory shortfall into the displayed "~N" count shown on manabase surfaces.
    /// </summary>
    public static int ApproximateCount(double value) =>
        Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Return the singular or plural form for a counted noun.
    /// </summary>
    public static string Pluralize(string singular, int count) => count == 1 ? singular : singular + "s";

    /// <summary>
    /// Format a "~N noun" phrase with count-driven singular/plural.
    /// </summary>
    public static string ApproximatePhrase(string singular, double value)
    {
        int count = ApproximateCount(value);
        return string.Create(CultureInfo.InvariantCulture, $"~{count} {Pluralize(singular, count)}");
    }

    /// <summary>Shared disclosure line for the analyzer-only cheap-scry source credit.</summary>
    public static string ScrySourceCreditLine(int copies, double credit) => string.Create(
        CultureInfo.InvariantCulture,
        $"Scry source credit: +{credit:0.0} any-color sources ({copies} cheap scry {Pluralize("spell", copies)} × 0.2) — separate from the ≤2 MV ramp/draw land credit, so draw+scry cards can count in both places.");
}
