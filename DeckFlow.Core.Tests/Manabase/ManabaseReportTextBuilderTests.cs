using System;
using System.Collections.Generic;
using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates <see cref="ManabaseReportTextBuilder"/>: the plain-text report contains all the
/// information rendered by the view, formatted so it can be pasted directly into ChatGPT or Claude
/// without any reformatting.
/// </summary>
public sealed class ManabaseReportTextBuilderTests
{
    private static readonly ManabaseVerdict IssueVerdict = new()
    {
        HasIssues = true,
        Headline = "Reading your deck",
        NoIssueReason = string.Empty,
        Lines =
        [
            "You're ~3 White source(s) short - add ~3 White-producing lands/rocks; consider cutting a colorless utility land.",
            "Ramp looks light: you run ~6 ramp vs a ~12/12 split for a ~MV4 threshold (your commander's mana value) - add ~6 ramp piece(s) (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
        ],
    };

    private static readonly ManabaseRampDrawBudget Budget = new()
    {
        RampCount = 6.0,
        DrawCount = 12.0,
        OverlapCount = 1,
        Threshold = 4.0,
        ThresholdSource = ManabaseRampDrawThresholdSource.CommanderManaValue,
        TargetRamp = 12,
        TargetDraw = 12,
        IsBalanced = false,
        IsRampLight = true,
        IsRampHeavy = false,
        RampShort = 6,
        IsDrawLight = false,
        DrawShort = 0,
    };

    // --- fixtures ------------------------------------------------------------

    private static ManabaseReport HealthyCasualReport() => new()
    {
        ActualLands = 37,
        TargetLands = 37.0,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 20.0,
                RequiredSources = 18,
                DrivingSpell = "Swords to Plowshares",
            },
            new()
            {
                Color = ManaColor.Blue,
                ActualSources = 16.0,
                RequiredSources = 14,
                DrivingSpell = "Counterspell",
            },
        },
        Mode = ManabaseMode.Casual,
        Summary = "Mana base is well-built.",
    };

    private static ManabaseReport CedhReport() => new()
    {
        ActualLands = 28,
        TargetLands = 29.5,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.Blue,
                ActualSources = 22.0,
                RequiredSources = 20,
                DrivingSpell = "Force of Will",
            },
        },
        Mode = ManabaseMode.Cedh,
        Castability = new List<CardCastability>
        {
            new()
            {
                Name = "Force of Will",
                ManaValue = 5,
                OnCurveTurn = 5,
                CastPercent = 90,
                LimitingFactor = "color:U",
            },
        },
        Summary = "cEDH mana base looks solid.",
    };

    private static ManabaseReport CasualReportWithCastability() => new()
    {
        ActualLands = 35,
        TargetLands = 37.0,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.Green,
                ActualSources = 18.5,
                RequiredSources = 16,
                DrivingSpell = "Cultivate",
            },
        },
        Mode = ManabaseMode.Casual,
        Castability = new List<CardCastability>
        {
            new()
            {
                Name = "Cultivate",
                ManaValue = 3,
                OnCurveTurn = 3,
                CastPercent = 88,
                LimitingFactor = "color:G",
            },
            new()
            {
                Name = "Craterhoof Behemoth",
                ManaValue = 8,
                OnCurveTurn = 8,
                CastPercent = 72,
                LimitingFactor = "mana",
            },
        },
        Summary = "Decent mana base.",
    };

    private static ManabaseReport ReportWithLandsFix() => new()
    {
        ActualLands = 33,
        TargetLands = 37.5,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.Red,
                ActualSources = 20.0,
                RequiredSources = 18,
                DrivingSpell = "Lightning Bolt",
                // One under-supported card so LandShortfallCoveredByRamp stays false
                // (broadUnderSupport requires UnderSupportedCount > tolerance).
                // Instead, give it a ColorLimitedUnderSupportedCount so BroadUnderSupport fires.
                UnderSupportedCount = 10,
            },
        },
        Mode = ManabaseMode.Casual,
        Summary = "Short on lands.",
    };

    private static ManabaseReport ReportWithNoPrimaryFix() => new()
    {
        ActualLands = 38,
        TargetLands = 37.0,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 22.0,
                RequiredSources = 18,
                DrivingSpell = "Wrath of God",
            },
        },
        Mode = ManabaseMode.Casual,
        Summary = "Every color adequately supported.",
    };

    private static ManabaseReport ReportWithRampAndUnsupported() => new()
    {
        ActualLands = 35,
        TargetLands = 36.5,
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.Green,
                ActualSources = 16.0,
                RequiredSources = 14,
                DrivingSpell = "Selvala, Heart of the Wilds",
            },
        },
        Mode = ManabaseMode.Casual,
        RampSourceNames = new List<string> { "Sol Ring", "Arcane Signet" },
        RampAndDrawNames = new List<string> { "Nature's Lore", "Farseek" },
        UnsupportedInteractions = new List<UnsupportedInteraction>
        {
            new() { Name = "Hydroid Krasis", Reason = "Variable (X) cost" },
        },
        Summary = "Good ramp package.",
    };

    // --- tests ---------------------------------------------------------------

    [Fact]
    public void Build_NullReport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ManabaseReportTextBuilder.Build(null!, null, null));
    }

    [Fact]
    public void Build_HealthyCasualReport_ContainsLandsHealthSummaryAndColorRows()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Atraxa Stax", null);

        // Title and deck name
        Assert.Contains("Mana Base Analysis", output);
        Assert.Contains("Atraxa Stax", output);

        // Mode
        Assert.Contains("Casual", output);

        // Lands line
        Assert.Contains("Lands:", output);
        Assert.Contains("37", output);
        Assert.Contains("37.0", output);

        // Health verdict — Healthy maps to "Excellent"
        Assert.Contains("Excellent", output);

        // Summary
        Assert.Contains("Mana base is well-built.", output);

        // Color rows
        Assert.Contains("White", output);
        Assert.Contains("Blue", output);
        Assert.Contains("Swords to Plowshares", output);
        Assert.Contains("Counterspell", output);
    }

    [Fact]
    public void Build_CedhReport_OmitsCastabilitySection()
    {
        string output = ManabaseReportTextBuilder.Build(
            CedhReport(), null, null, ManabaseMode.Cedh);

        // cEDH mode label
        Assert.Contains("cEDH", output);

        // Castability section must NOT appear in cEDH output
        Assert.DoesNotContain("Castability", output);
        Assert.DoesNotContain("Cast on curve", output);
    }

    [Fact]
    public void Build_CasualReportWithCastability_IncludesCastabilityRows()
    {
        string output = ManabaseReportTextBuilder.Build(
            CasualReportWithCastability(), null, null, ManabaseMode.Casual);

        Assert.Contains("Castability", output);
        Assert.Contains("Cultivate", output);
        Assert.Contains("88%", output);
        Assert.Contains("Craterhoof Behemoth", output);
        Assert.Contains("72%", output);
    }

    [Fact]
    public void Build_PrimaryFixLands_EmitsAddLandsLineNotNegative()
    {
        string output = ManabaseReportTextBuilder.Build(
            ReportWithLandsFix(), null, null, ManabaseMode.Casual);

        // Should say add lands (positive number)
        Assert.Contains("Biggest fix", output);
        Assert.Contains("land", output);

        // Must never emit a negative add-N value
        Assert.DoesNotContain("add ~-", output);
    }

    [Fact]
    public void Build_PrimaryFixNone_EmitsEveryColorAdequate()
    {
        string output = ManabaseReportTextBuilder.Build(
            ReportWithNoPrimaryFix(), null, null, ManabaseMode.Casual);

        // PrimaryFix.Kind == None → "every color adequately supported" wording
        Assert.Contains("adequately supported", output);

        // Must never emit a negative add-N value
        Assert.DoesNotContain("add ~-", output);
    }

    [Fact]
    public void Build_WithRampAndUnsupportedInteractions_ListsThem()
    {
        string output = ManabaseReportTextBuilder.Build(
            ReportWithRampAndUnsupported(), null, null, ManabaseMode.Casual);

        // Ramp section
        Assert.Contains("Sol Ring", output);
        Assert.Contains("Arcane Signet", output);
        Assert.Contains("Nature's Lore", output);
        Assert.Contains("Farseek", output);

        // Unsupported interactions
        Assert.Contains("Hydroid Krasis", output);
        Assert.Contains("Variable (X) cost", output);
    }

    [Fact]
    public void Build_WithDeckName_IncludesName()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "My CEDH Deck", null);

        Assert.Contains("My CEDH Deck", output);
    }

    [Fact]
    public void Build_BlankDeckName_OmitsNameDecoration()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "   ", null);

        // Blank/whitespace deck name must not leave a decoration artifact like ': '
        Assert.DoesNotContain("Deck: ", output);
    }

    [Fact]
    public void Build_WithDecklistText_AppendsItAtEnd()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), null, "1 Sol Ring\n1 Command Tower");

        Assert.Contains("Decklist:", output);
        Assert.Contains("1 Sol Ring", output);
        Assert.Contains("1 Command Tower", output);

        // Decklist must come after the summary line
        int summaryIdx = output.IndexOf("Mana base is well-built.", StringComparison.Ordinal);
        int decklistIdx = output.IndexOf("Decklist:", StringComparison.Ordinal);
        Assert.True(decklistIdx > summaryIdx, "Decklist must appear after the summary.");
    }

    [Fact]
    public void Build_NullDecklistText_OmitsDecklistSection()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), null, null);

        Assert.DoesNotContain("Decklist:", output);
    }

    [Fact]
    public void Build_NullVerdictAndBudget_IsByteIdentical()
    {
        ManabaseReport report = HealthyCasualReport();

        string baseline = ManabaseReportTextBuilder.Build(report, "Atraxa Stax", "1 Sol Ring");
        string withNulls = ManabaseReportTextBuilder.Build(
            report,
            "Atraxa Stax",
            "1 Sol Ring",
            ManabaseMode.Casual,
            null,
            null);

        Assert.Equal(baseline, withNulls);
    }

    [Fact]
    public void Build_WithVerdictAndBudget_AppendsReadingDeckBlockAfterSummary()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(),
            "Atraxa Stax",
            null,
            ManabaseMode.Casual,
            IssueVerdict,
            Budget);

        int summaryIndex = output.IndexOf("Mana base is well-built.", StringComparison.Ordinal);
        int verdictIndex = output.IndexOf("Reading your deck:", StringComparison.Ordinal);
        int colorSourcesIndex = output.IndexOf("Color Sources:", StringComparison.Ordinal);

        Assert.True(verdictIndex > summaryIndex);
        Assert.True(colorSourcesIndex > verdictIndex);
        Assert.Contains("1. You're ~3 White source(s) short", output);
        Assert.Contains("2. Ramp looks light: you run ~6 ramp", output);
        Assert.Contains("Ramp/draw: ~6 ramp / ~12 draw vs a ~12/12 community target for a ~MV4 threshold (your commander's mana value); (1 do both). community heuristic, not Karsten math.", output);
    }

    [Fact]
    public void Build_ColorRows_ShowActualVsRequiredAndDeficitOrOk()
    {
        string output = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), null, null);

        // Both colors are adequate: should show OK (not a deficit warning)
        Assert.Contains("OK", output);
    }

    [Fact]
    public void Build_ColorDeficit_ShowsDeficit()
    {
        var report = new ManabaseReport
        {
            ActualLands = 34,
            TargetLands = 37.5,
            ColorFindings = new List<ColorSourceFinding>
            {
                new()
                {
                    Color = ManaColor.Black,
                    ActualSources = 10.0,
                    RequiredSources = 15,
                    DrivingSpell = "Demonic Tutor",
                },
            },
            Mode = ManabaseMode.Casual,
            Summary = "Needs more black sources.",
        };

        string output = ManabaseReportTextBuilder.Build(report, null, null);

        // Deficit should show as the positive deficit value (5.0)
        Assert.Contains("5.0", output);
        Assert.Contains("Demonic Tutor", output);
    }
}
