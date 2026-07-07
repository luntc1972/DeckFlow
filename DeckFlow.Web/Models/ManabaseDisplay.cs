using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Models;

/// <summary>
/// Pure presentation helpers for the mana-base view: maps Core castability values to the
/// friendly text and semantic chip classes the Razor page renders. Kept here (not inline in the
/// view) so the mapping is unit-testable and reused consistently.
/// </summary>
public static class ManabaseDisplay
{
    /// <summary>UI-only gloss for the Karsten source-check metric.</summary>
    public const string KarstenSourceGloss =
        "Enough lands/rocks of each color to reliably have that color when you need it. need -3 means about 3 short.";

    /// <summary>UI-only gloss for the simulated cast-rate metric.</summary>
    public const string CastRateGloss =
        "Across simulated games, how often your spells are castable on or before their ideal turn. Higher = smoother.";

    /// <summary>UI-only gloss for the weakest-color callout.</summary>
    public const string WeakestColorGloss =
        "The color with the biggest source shortfall or most fragile support - usually the first color to fix.";

    /// <summary>UI-only gloss for the demanding-cards callout.</summary>
    public const string DemandingCardsGloss =
        "The hardest spells to cast on time with this mana base - the cards most likely to expose weak support.";

    /// <summary>Plain-language gloss for the tap-analyzer card (shown when ShowPlainLanguage is also on).</summary>
    public const string TapAnalyzerGloss =
        "Tapped lands (Temples, tri-lands, taplands) can't tap for mana the turn they enter, " +
        "so they push back your first castable turn. Higher untapped % = faster, smoother starts.";

    /// <summary>
    /// Maps a <see cref="CardCastability.LimitingFactor"/> token (<c>"mana"</c>, <c>"color:U"</c>,
    /// <c>"both"</c>) to a human-readable phrase for the table's "Limiting" column.
    /// </summary>
    public static string LimitingText(string? limitingFactor)
    {
        if (string.IsNullOrWhiteSpace(limitingFactor))
        {
            return "mana";
        }

        if (string.Equals(limitingFactor, "mana", StringComparison.OrdinalIgnoreCase))
        {
            return "mana";
        }

        if (string.Equals(limitingFactor, "both", StringComparison.OrdinalIgnoreCase))
        {
            return "mana + color";
        }

        // "color:X" → "color: X"
        if (limitingFactor.StartsWith("color:", StringComparison.OrdinalIgnoreCase))
        {
            string colorPart = limitingFactor["color:".Length..].Trim();
            return colorPart.Length > 0 ? $"color: {colorPart}" : "color";
        }

        return limitingFactor;
    }

    /// <summary>
    /// Maps a cast percentage to a (cssModifier, label) pair so the chip conveys severity by both
    /// text and color (never color alone): &lt;70 danger/"low", 70–89 warning/"ok", ≥90 success/"good".
    /// </summary>
    public static (string Css, string Label) CastChip(int castPercent)
    {
        if (castPercent < 70)
        {
            return ("manabase-chip--low", "low");
        }

        if (castPercent < 90)
        {
            return ("manabase-chip--ok", "ok");
        }

        return ("manabase-chip--good", "good");
    }

    /// <summary>
    /// Maps an untapped-source percentage to a (cssClass, glyph) pair for the tap-analyzer card.
    /// ≥80% = met (✓); below = short (⚠). Flat 80% threshold (decision D4), mode-independent and
    /// informational only — never contradicts or affects the health verdict.
    /// </summary>
    public static (string Css, string Marker) TapMarker(int untappedPercent)
        => untappedPercent >= 80
            ? ("manabase-lens-met", "✓")
            : ("manabase-lens-short", "⚠");

    /// <summary>
    /// Maps a <see cref="ManabaseMulliganEvaluation.KeepableBand"/> value ("high"/"medium"/"low") to
    /// a (cssClass, glyph) pair for the opening-hand lens card, reusing the same met/short marker
    /// classes as <see cref="TapMarker"/> — no new css tokens. "high" reads as met (✓); "medium"
    /// and "low" both read as short (⚠), since only "high" clears the aggregator's own top band.
    /// </summary>
    public static (string Css, string Marker) KeepableMarker(string keepableBand)
        => string.Equals(keepableBand, "high", StringComparison.OrdinalIgnoreCase)
            ? ("manabase-lens-met", "✓")
            : ("manabase-lens-short", "⚠");

    /// <summary>
    /// Formats the opening-hand lens card's average mana value to one decimal using
    /// <see cref="System.Globalization.CultureInfo.InvariantCulture"/>, so the on-page number stays
    /// byte-identical to the paste artifact (<c>ManabaseReportTextBuilder</c> formats every figure
    /// invariantly) even under a comma-decimal request culture — the card's plan contract is that the
    /// rendered figures match the pasteable artifact exactly.
    /// </summary>
    public static string AvgManaValueText(double averageManaValue)
        => averageManaValue.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Human label for the four-tier health scale (Excellent / Solid / Workable / Needs work).</summary>
    public static string HealthLabel(ManabaseHealth health) => ManabaseLabels.Health(health);

    /// <summary>
    /// Health-scale chip class. These are health-only (distinct from the shared cast-chip
    /// --good/--ok/--low) and use fixed, theme-independent filled status colors so the verdict stays
    /// readable on every guild theme — the theme's --info/--warning tokens are surface colors, not
    /// status colors, so binding the chip to them made the Solid label invisible on light themes.
    /// </summary>
    public static string HealthCss(ManabaseHealth health) => health switch
    {
        ManabaseHealth.Healthy => "manabase-health--excellent",
        ManabaseHealth.Functional => "manabase-health--solid",
        ManabaseHealth.Workable => "manabase-health--workable",
        _ => "manabase-health--needswork",
    };

    /// <summary>
    /// Human label for a spell's average cast delay: "on curve" when ~0, else "+N.N turns" — the
    /// mean turns late it first becomes castable (supporting context, not a hard metric).
    /// </summary>
    public static string DelayText(double averageDelay)
    {
        if (averageDelay < 0.05)
        {
            return "on curve";
        }

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"+{averageDelay:0.0} turns");
    }

    /// <summary>
    /// Karsten source-check for the left lens of the two-lens header. <c>Met</c> uses the raw
    /// (weighted, fractional) <see cref="ColorSourceFinding.ActualSources"/> against the integer
    /// requirement; <c>Deficit</c> is the whole sources still needed when short, clamped to at least
    /// 1 so an unmet color never renders "−0". The view shows <c>ActualSources</c> to one decimal so
    /// the displayed number and the ✓/⚠ marker can never contradict each other.
    /// </summary>
    public static (bool Met, int Deficit) KarstenMet(ColorSourceFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        bool met = finding.ActualSources >= finding.RequiredSources;
        int deficit = met ? 0 : Math.Max(1, (int)Math.Ceiling(finding.RequiredSources - finding.ActualSources));
        return (met, deficit);
    }

    /// <summary>Human label for an analysis mode (used in the results echo line).</summary>
    public static string ModeLabel(ManabaseMode mode) => ManabaseLabels.Mode(mode);

    /// <summary>Human label for the commander-importance setting.</summary>
    public static string ImportanceLabel(CommanderImportance importance) => importance switch
    {
        CommanderImportance.Central => "Central",
        CommanderImportance.Low => "Low",
        _ => "Standard",
    };
}
