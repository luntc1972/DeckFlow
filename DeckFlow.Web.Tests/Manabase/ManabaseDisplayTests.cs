using System;
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
}
