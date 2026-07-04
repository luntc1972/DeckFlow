using System;
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

    [Fact]
    public void ShowCastability_TrueOnlyForCasualWithRows()
    {
        var rows = new[]
        {
            new CardCastability { Name = "X", ManaValue = 2, OnCurveTurn = 2, CastPercent = 50, LimitingFactor = "mana" },
        };

        Assert.True(ViewModel(ManabaseMode.Casual, rows).ShowCastability);
        Assert.False(ViewModel(ManabaseMode.Cedh, rows).ShowCastability);
        Assert.False(ViewModel(ManabaseMode.Casual, Array.Empty<CardCastability>()).ShowCastability);
    }

    [Fact]
    public void ShowCastability_And_HasResult_FalseWhenNoReport()
    {
        // A view model with no report (initial GET or error path) gates both off.
        var empty = new ManabaseViewModel();

        Assert.False(empty.HasResult);
        Assert.False(empty.ShowCastability);
    }

    [Fact]
    public void HasResult_TrueWhenReportPresent()
    {
        Assert.True(ViewModel(ManabaseMode.Casual, Array.Empty<CardCastability>()).HasResult);
    }

    [Fact]
    public void AvgOnCurve_EmptyRows_IsZero_NoDivideByZero()
    {
        Assert.Equal(0, ManabaseDisplay.AvgOnCurve(Array.Empty<CardCastability>()));
    }

    [Fact]
    public void AvgOnCurve_MeansCastPercentAndRounds()
    {
        var rows = new[]
        {
            new CardCastability { Name = "A", ManaValue = 2, OnCurveTurn = 2, CastPercent = 80, LimitingFactor = "mana" },
            new CardCastability { Name = "B", ManaValue = 3, OnCurveTurn = 3, CastPercent = 81, LimitingFactor = "mana" },
        };

        // (80 + 81) / 2 = 80.5 → rounds to 80 (banker's rounding to even).
        Assert.Equal(80, ManabaseDisplay.AvgOnCurve(rows));
    }

    [Theory]
    // ActualSources >= RequiredSources → met, deficit 0.
    [InlineData(18.0, 17, true, 0)]
    [InlineData(17.0, 17, true, 0)]
    // Short: deficit is whole sources needed, clamped to >= 1 so it never shows "-0".
    [InlineData(15.0, 16, false, 1)]
    [InlineData(16.6, 17, false, 1)] // would round to "17" for display, but raw 16.6 < 17 → still ⚠ −1
    [InlineData(12.2, 16, false, 4)]
    public void KarstenMet_UsesRawValue_AndClampsDeficit(double actual, int required, bool met, int deficit)
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

    private static ManabaseViewModel ViewModel(ManabaseMode mode, System.Collections.Generic.IReadOnlyList<CardCastability> rows) => new()
    {
        Report = new ManabaseReport
        {
            ActualLands = 36,
            TargetLands = 37,
            ColorFindings = Array.Empty<ColorSourceFinding>(),
            Mode = mode,
            Castability = rows,
            Summary = "ok",
        },
    };

    private static string GetGloss(string fieldName)
    {
        FieldInfo field = typeof(ManabaseDisplay).GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new Xunit.Sdk.XunitException($"ManabaseDisplay.{fieldName} field missing.");
        return Assert.IsType<string>(field.GetValue(null));
    }
}
