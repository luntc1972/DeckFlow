using DeckFlow.Core.Manabase;

namespace DeckFlow.Web.Models;

/// <summary>
/// Pure presentation helpers for the mana-base view: maps Core castability values to the
/// friendly text and semantic chip classes the Razor page renders. Kept here (not inline in the
/// view) so the mapping is unit-testable and reused consistently.
/// </summary>
public static class ManabaseDisplay
{
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

    /// <summary>Human label for the four-tier health scale (Excellent / Solid / Workable / Needs work).</summary>
    public static string HealthLabel(ManabaseHealth health) => health switch
    {
        ManabaseHealth.Healthy => "Excellent",
        ManabaseHealth.Functional => "Solid",
        ManabaseHealth.Workable => "Workable",
        _ => "Needs work",
    };

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

    /// <summary>Human label for an analysis mode (used in the results echo line).</summary>
    public static string ModeLabel(ManabaseMode mode) =>
        mode == ManabaseMode.Cedh ? "cEDH" : "Casual";

    /// <summary>Human label for the commander-importance setting.</summary>
    public static string ImportanceLabel(CommanderImportance importance) => importance switch
    {
        CommanderImportance.Central => "Central",
        CommanderImportance.Low => "Low",
        _ => "Standard",
    };
}
