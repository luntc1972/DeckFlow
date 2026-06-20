using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Manabase;

/// <summary>
/// Implements the §6 mana-base scoring recipe: compare a deck's land count to the
/// Karsten curve target, count effective colored sources, and flag the weakest color.
/// Pure CPU; takes a pre-classified <see cref="ManabaseDeck"/> so it has no Scryfall
/// or HTTP dependency and is fully unit-testable.
/// </summary>
public static class ManabaseAnalyzer
{
    /// <summary>Run the full analysis and produce a <see cref="ManabaseReport"/>.</summary>
    public static ManabaseReport Analyze(ManabaseDeck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);

        // A source occupies a land slot when flagged IsLand (even discounted fetches);
        // partial sources (dorks, rocks, MDFC backs) count toward color supply only.
        int actualLands = deck.Sources.Count(s => s.IsLand);

        double targetLands = deck.IsSingleton
            ? KarstenManabase.SingletonLandTarget(
                deck.TotalCards,
                Math.Max(1, deck.CommanderCount),
                deck.AverageManaValue,
                deck.RampAndDrawUnderThree)
            : KarstenManabase.SixtyCardLandTarget(
                deck.AverageManaValue,
                deck.RampAndDrawUnderThree);

        // Library size excludes commanders (they start in the command zone, not the deck).
        int librarySize = deck.TotalCards - deck.CommanderCount;

        var findings = BuildColorFindings(deck, librarySize, actualLands);

        string summary = BuildSummary(actualLands, targetLands, findings);

        return new ManabaseReport
        {
            ActualLands = actualLands,
            TargetLands = targetLands,
            ColorFindings = findings,
            Summary = summary,
        };
    }

    private static IReadOnlyList<ColorSourceFinding> BuildColorFindings(
        ManabaseDeck deck,
        int librarySize,
        int totalLands)
    {
        var findings = new List<ColorSourceFinding>();

        foreach (ManaColor color in EnumerateUsedColors(deck))
        {
            double actual = EffectiveSources(deck, color);

            // The toughest requirement for this color = the spell needing the most sources.
            int required = 0;
            string driver = "(none)";
            foreach (SpellRequirement spell in deck.Spells)
            {
                if (!spell.Pips.TryGetValue(color, out int pips) || pips <= 0)
                {
                    continue;
                }

                // Gold cards bump each color's requirement by one (need all colors present).
                int goldBump = spell.IsGold ? 1 : 0;
                int need = KarstenManabase.SourcesNeeded(librarySize, totalLands, pips, spell.ManaValue) + goldBump;
                if (need > required)
                {
                    required = need;
                    driver = spell.Name;
                }
            }

            if (required == 0)
            {
                continue;
            }

            findings.Add(new ColorSourceFinding
            {
                Color = color,
                ActualSources = Math.Round(actual, 1),
                RequiredSources = required,
                DrivingSpell = driver,
            });
        }

        // Worst deficit first so WeakestColor is findings[0].
        return findings.OrderByDescending(f => f.Deficit).ToList();
    }

    private static IEnumerable<ManaColor> EnumerateUsedColors(ManabaseDeck deck)
    {
        var colors = new HashSet<ManaColor>();
        foreach (SpellRequirement spell in deck.Spells)
        {
            foreach (KeyValuePair<ManaColor, int> pip in spell.Pips)
            {
                if (pip.Value > 0)
                {
                    colors.Add(pip.Key);
                }
            }
        }

        return colors;
    }

    // Known limitation: counts all sources regardless of ManaSource.EntersUntapped. For a
    // turn-1 single pip, tapped lands cannot actually cast the one-drop on curve, so this is
    // slightly optimistic there. Turn-aware (untapped-only) counting is a follow-up.
    private static double EffectiveSources(ManabaseDeck deck, ManaColor color)
    {
        double total = 0.0;
        foreach (ManaSource source in deck.Sources)
        {
            if (source.Produces.Contains(color))
            {
                total += source.Weight;
            }
        }

        return total;
    }

    private static string BuildSummary(
        int actualLands,
        double targetLands,
        IReadOnlyList<ColorSourceFinding> findings)
    {
        var sb = new StringBuilder();
        double delta = actualLands - targetLands;

        sb.Append(CultureInfo.InvariantCulture, $"Lands: {actualLands} vs ~{targetLands:F1} target ");
        if (delta >= -1)
        {
            sb.Append("(land count OK). ");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"(add ~{Math.Ceiling(-delta):F0} land(s)). ");
        }

        ColorSourceFinding? weakest = findings.Count > 0 && findings[0].Deficit > 0 ? findings[0] : null;
        if (weakest is null)
        {
            sb.Append("Colors: every color adequately supported.");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"Weakest color: {weakest.Color} — {weakest.ActualSources:F1} sources vs {weakest.RequiredSources} needed for {weakest.DrivingSpell} (add ~{Math.Ceiling(weakest.Deficit):F0}).");
        }

        return sb.ToString();
    }
}
