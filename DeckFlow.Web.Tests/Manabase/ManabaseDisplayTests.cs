using System;
using System.Linq;
using System.Reflection;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Models;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Locks the pure presentation mappings the mana-base view depends on: the LimitingFactor
/// friendly-text translation, the cast-percent chip (text + severity, never color alone), and
/// the Casual-only castability gate on the view model.
/// </summary>
public sealed class ManabaseDisplayTests
{
    [Theory]
    [InlineData("mana", "mana")]
    [InlineData("color:U", "color: U")]
    [InlineData("color:W", "color: W")]
    [InlineData("both", "mana + color")]
    [InlineData("", "mana")]
    [InlineData(null, "mana")]
    public void LimitingText_MapsTokensToFriendlyPhrases(string? token, string expected)
    {
        Assert.Equal(expected, ManabaseDisplay.LimitingText(token));
    }

    [Theory]
    [InlineData(0, "low")]
    [InlineData(69, "low")]
    [InlineData(70, "ok")]
    [InlineData(89, "ok")]
    [InlineData(90, "good")]
    [InlineData(100, "good")]
    public void CastChip_LabelsSeverityByBand(int percent, string expectedLabel)
    {
        var (css, label) = ManabaseDisplay.CastChip(percent);

        Assert.Equal(expectedLabel, label);
        Assert.False(string.IsNullOrWhiteSpace(css), "chip must carry a css modifier so severity is not color-only");
    }

    [Theory]
    [InlineData(80, "manabase-lens-met", "✓")]   // flat 80% threshold (D4): meets target
    [InlineData(100, "manabase-lens-met", "✓")]
    [InlineData(79, "manabase-lens-short", "⚠")] // just below threshold
    [InlineData(0, "manabase-lens-short", "⚠")]
    public void TapMarker_MapsPercentToCorrectCssAndGlyph(int percent, string expectedCss, string expectedMarker)
    {
        var (css, marker) = ManabaseDisplay.TapMarker(percent);

        Assert.Equal(expectedCss, css);
        Assert.Equal(expectedMarker, marker);
    }

    [Fact]
    public void TapAnalyzerGloss_IsNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ManabaseDisplay.TapAnalyzerGloss));
    }

    [Theory]
    [InlineData("color:", "color")]            // empty color suffix → bare "color"
    [InlineData("color:  ", "color")]          // whitespace-only suffix → bare "color"
    [InlineData("COLOR:U", "color: U")]        // case-insensitive prefix
    [InlineData("color:Red", "color: Red")]    // multi-letter color name preserved
    [InlineData("   ", "mana")]                // all-whitespace token → default "mana"
    [InlineData("MANA", "mana")]               // case-insensitive "mana"
    [InlineData("BOTH", "mana + color")]       // case-insensitive "both"
    [InlineData("unexpected", "unexpected")]   // unknown token passes through verbatim
    public void LimitingText_HandlesEdgeTokens(string? token, string expected)
    {
        Assert.Equal(expected, ManabaseDisplay.LimitingText(token));
    }

    [Theory]
    [InlineData(-5, "low")]   // a clamped/negative percent still bands as "low"
    [InlineData(101, "good")] // an over-100 percent still bands as "good"
    public void CastChip_OutOfRangePercents_StillBand(int percent, string expectedLabel)
    {
        var (css, label) = ManabaseDisplay.CastChip(percent);
        Assert.Equal(expectedLabel, label);
        Assert.False(string.IsNullOrWhiteSpace(css));
    }

    [Theory]
    [InlineData(0.0, "on curve")]
    [InlineData(0.04, "on curve")]   // below the rounding threshold → still "on curve"
    [InlineData(0.4, "+0.4 turns")]
    [InlineData(1.0, "+1.0 turns")]
    [InlineData(2.35, "+2.4 turns")] // one-decimal rounding
    public void DelayText_FormatsAverageDelay(double delay, string expected)
    {
        Assert.Equal(expected, ManabaseDisplay.DelayText(delay));
    }

    [Fact]
    public void EarlyCastSummary_EmptyList_ReturnsNull()
    {
        Assert.Null(ManabaseDisplay.EarlyCastSummary(Array.Empty<int>()));
    }

    [Fact]
    public void EarlyCastSummary_AllZeroPercents_ReturnsNull()
    {
        Assert.Null(ManabaseDisplay.EarlyCastSummary(new[] { 0, 0, 0 }));
    }

    [Fact]
    public void EarlyCastSummary_TrimsLeadingZeros_AndFormatsRemainingTurns()
    {
        string? summary = ManabaseDisplay.EarlyCastSummary(new[] { 0, 12, 48 });

        Assert.Equal("Earlier turns: T2 12% · T3 48%", summary);
    }

    [Fact]
    public void EarlyCastSummary_SingleTurn_FormatsOneEntry()
    {
        string? summary = ManabaseDisplay.EarlyCastSummary(new[] { 35 });

        Assert.Equal("Earlier turns: T1 35%", summary);
    }

    [Fact]
    public void AvgManaValueText_UsesInvariantCulture_MatchesPasteArtifact()
    {
        // The paste artifact formats every figure with InvariantCulture; the on-page lens card must
        // stay byte-identical to it. Under a comma-decimal request culture, a request-culture ToString
        // would render "2,5" and drift from the artifact's "2.5". Pin the invariant contract here.
        System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("2.5", ManabaseDisplay.AvgManaValueText(2.5));
            Assert.Equal("3.0", ManabaseDisplay.AvgManaValueText(3.0));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ModeAndImportanceLabels_AreHumanReadable()
    {
        Assert.Equal("cEDH", ManabaseDisplay.ModeLabel(ManabaseMode.Cedh));
        Assert.Equal("Casual", ManabaseDisplay.ModeLabel(ManabaseMode.Casual));
        Assert.Equal("Central", ManabaseDisplay.ImportanceLabel(CommanderImportance.Central));
        Assert.Equal("Standard", ManabaseDisplay.ImportanceLabel(CommanderImportance.Standard));
        Assert.Equal("Low", ManabaseDisplay.ImportanceLabel(CommanderImportance.Low));
    }

    [Theory]
    // Within one full source of the requirement counts as met, deficit 0.
    [InlineData(24.0, 24, true, 0)]
    [InlineData(23.8, 24, true, 0)]
    [InlineData(23.0, 24, true, 0)]
    [InlineData(17.0, 17, true, 0)]
    [InlineData(16.0, 17, true, 0)]
    // Short: deficit is whole sources needed, clamped to >= 1 so it never shows "-0".
    [InlineData(22.95, 24, false, 1)]
    [InlineData(22.9, 24, false, 1)]
    [InlineData(22.8, 24, false, 1)]
    [InlineData(21.9, 24, false, 2)]
    [InlineData(12.2, 16, false, 4)]
    public void KarstenMet_AppliesOneSourceTolerance_AndClampsDeficit(double actual, int required, bool met, int deficit)
    {
        var finding = new ColorSourceFinding
        {
            Color = ManaColor.Blue,
            ActualSources = actual,
            RequiredSources = required,
            DrivingSpell = "Test",
        };

        (bool Met, int Deficit) result = ManabaseDisplay.KarstenMet(finding);

        Assert.Equal(met, result.Met);
        Assert.Equal(deficit, result.Deficit);
    }

    [Fact]
    public void DefaultVisibleCastabilityCount_AllGoodDeck_ShowsMinimumRows()
    {
        var rows = BuildUniformRows(25, 95);

        int visible = ManabaseDisplay.DefaultVisibleCastabilityCount(rows);

        Assert.Equal(ManabaseDisplay.MinVisibleCastabilityRows, visible);
    }

    [Fact]
    public void DefaultVisibleCastabilityCount_AllBadDeck_CapsAtMaximumRows()
    {
        var rows = BuildUniformRows(30, 68);

        int visible = ManabaseDisplay.DefaultVisibleCastabilityCount(rows);

        Assert.Equal(ManabaseDisplay.MaxVisibleCastabilityRows, visible);
    }

    [Fact]
    public void DefaultVisibleCastabilityCount_ShowsAllLowAndOkRows_WhenWithinBounds()
    {
        var rows = BuildRowsWithPercents(
            55,
            60,
            66,
            70,
            74,
            78,
            82,
            85,
            88,
            89,
            91,
            93,
            95,
            97);

        int visible = ManabaseDisplay.DefaultVisibleCastabilityCount(rows);

        Assert.Equal(10, visible);
    }

    [Fact]
    public void CastabilitySummaryText_UsesHiddenFloorAndCount()
    {
        var rows = BuildRowsWithPercents(51, 55, 60, 65, 72, 76, 81, 86, 88, 89, 92, 97);

        string summary = ManabaseDisplay.CastabilitySummaryText(rows, 10);

        Assert.Equal("Showing the 10 hardest casts — 2 more at 92%+ are fine.", summary);
    }

    [Fact]
    public void CastRateShapeText_EmptyRows_ReturnsEmptyString()
    {
        string shape = ManabaseDisplay.CastRateShapeText(Array.Empty<CardCastability>());

        Assert.Equal(string.Empty, shape);
    }

    [Fact]
    public void CastRateShapeText_SingleRow_FormatsSingularCounts()
    {
        CardCastability[] rows = [BuildCastabilityRow("Arcane Signet", 100)];

        string shape = ManabaseDisplay.CastRateShapeText(rows);

        Assert.Equal("≥90% cast: 1 spell · 70–89%: 0 · <70%: 0", shape);
    }

    [Fact]
    public void CastRateShapeText_BucketsBoundaryPercents_UsingExistingThresholds()
    {
        var rows = BuildRowsWithPercents(90, 70, 69);

        string shape = ManabaseDisplay.CastRateShapeText(rows);

        Assert.Equal("≥90% cast: 1 spell · 70–89%: 1 · <70%: 1", shape);
    }

    [Theory]
    [InlineData(87, 88, "manabase-lens-short", "⚠")]
    [InlineData(88, 88, "manabase-lens-met", "✓")]
    [InlineData(90, 88, "manabase-lens-met", "✓")]
    public void InteractionHoldableMarker_UsesSuppliedThreshold(
        int holdablePercent,
        int threshold,
        string expectedCss,
        string expectedMarker)
    {
        var (css, marker) = ManabaseDisplay.InteractionHoldableMarker(holdablePercent, threshold);

        Assert.Equal(expectedCss, css);
        Assert.Equal(expectedMarker, marker);
    }

    [Fact]
    public void CedhInteractionLensGloss_ContainsHoldManaOpenCaveat()
    {
        Assert.Contains("assumes you hold mana open", ManabaseDisplay.CedhInteractionLensGloss);
    }

    [Theory]
    [InlineData("KarstenSourceGloss", "need -3 means about 3 short")]
    [InlineData("CastRateGloss", "Higher = smoother.")]
    [InlineData("WeakestColorGloss", "first color to fix")]
    [InlineData("DemandingCardsGloss", "hardest spells to cast on time")]
    public void GlossConstants_AreNonEmpty_AndContainAnchorPhrase(string fieldName, string anchor)
    {
        string value = GetGloss(fieldName);

        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.Contains(anchor, value);
        Assert.DoesNotContain('—', value);
        Assert.DoesNotContain('–', value);
    }

    private static string GetGloss(string fieldName)
    {
        FieldInfo field = typeof(ManabaseDisplay).GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new Xunit.Sdk.XunitException($"ManabaseDisplay.{fieldName} field missing.");
        return Assert.IsType<string>(field.GetValue(null));
    }

    private static CardCastability[] BuildUniformRows(int count, int castPercent)
        => Enumerable.Range(1, count)
            .Select(i => BuildCastabilityRow($"Spell {i}", castPercent))
            .ToArray();

    private static CardCastability[] BuildRowsWithPercents(params int[] castPercents)
        => castPercents
            .Select((percent, index) => BuildCastabilityRow($"Spell {index + 1}", percent))
            .ToArray();

    private static CardCastability BuildCastabilityRow(string name, int castPercent)
        => new()
        {
            Name = name,
            ManaValue = 3,
            OnCurveTurn = 3,
            CastPercent = castPercent,
            AverageDelay = castPercent >= 90 ? 0.0 : 1.0,
            LimitingFactor = "mana",
        };
}
