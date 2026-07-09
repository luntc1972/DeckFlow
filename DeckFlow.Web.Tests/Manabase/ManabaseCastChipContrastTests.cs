using System.Globalization;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Locks the baked WCAG-AA contrast of the three cast chips (low / ok / good) in
/// <c>site-common.css</c>. The chips use opaque tints with baked text so they are
/// theme-independent — contrast is fully determined by the hex pairs below, so a
/// deterministic computation is a faithful guard (no browser/theme sweep needed).
/// If a chip's CSS colors change, update the pair here and keep the ratio ≥ 4.5:1.
/// </summary>
public sealed class ManabaseCastChipContrastTests
{
    // (text, background) — must mirror .manabase-chip--low/--ok/--good in site-common.css.
    [Theory]
    [InlineData("#9b1c1c", "#fdecec", "low")]
    [InlineData("#854d0e", "#fbf0d9", "ok")]
    [InlineData("#15602f", "#dff3e4", "good")]
    public void CastChip_TextOnTint_MeetsWcagAa(string text, string background, string chip)
    {
        double ratio = ContrastRatio(text, background);

        Assert.True(
            ratio >= 4.5,
            $"cast chip '{chip}' contrast {ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1 must be >= 4.5:1 (WCAG AA normal text)");
    }

    private static double ContrastRatio(string hexA, string hexB)
    {
        double la = RelativeLuminance(hexA);
        double lb = RelativeLuminance(hexB);
        double lighter = System.Math.Max(la, lb);
        double darker = System.Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        hex = hex.TrimStart('#');
        double r = Channel(hex.Substring(0, 2));
        double g = Channel(hex.Substring(2, 2));
        double b = Channel(hex.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Channel(string twoHex)
    {
        double c = int.Parse(twoHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
