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

    /// <summary>Human label for the two-tier health verdict.</summary>
    public static string HealthLabel(ManabaseHealth health) => health switch
    {
        ManabaseHealth.Healthy => "Healthy",
        ManabaseHealth.Functional => "Functional",
        _ => "Needs work",
    };

    /// <summary>
    /// Semantic chip class for the health verdict so it reads by color too (never color alone):
    /// Healthy → good, Functional → ok, NeedsWork → low.
    /// </summary>
    public static string HealthCss(ManabaseHealth health) => health switch
    {
        ManabaseHealth.Healthy => "manabase-chip--good",
        ManabaseHealth.Functional => "manabase-chip--ok",
        _ => "manabase-chip--low",
    };

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
