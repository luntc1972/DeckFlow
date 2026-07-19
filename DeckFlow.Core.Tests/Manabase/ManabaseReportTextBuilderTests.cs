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
        Headline = "Reading the deck",
        NoIssueReason = string.Empty,
        Lines =
        [
            "You're ~3 White sources short - heuristic guidance: add ~3 White-producing lands/rocks; consider cutting a colorless utility land.",
            "Ramp looks light: the deck runs ~6 ramp vs a ~12/12 split for a ~MV4 threshold (the commander's mana value) - add ~6 ramp pieces (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
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

    private static ManabaseInteractionLens PopulatedInteractionLens() => new()
    {
        QualifyingCount = 3,
        OnTargetCount = 1,
        Threshold = 88,
        Rows = new List<ManabaseInteractionRow>
        {
            new() { Name = "Swan Song", HoldablePercent = 61 },
            new() { Name = "An Offer You Can't Refuse", HoldablePercent = 74 },
            new() { Name = "Flusterstorm", HoldablePercent = 91, IsCostOverridden = true },
        },
    };

    private static ManabaseInteractionLens EmptyInteractionLens() => new()
    {
        QualifyingCount = 0,
        OnTargetCount = 0,
        Threshold = 88,
        Rows = Array.Empty<ManabaseInteractionRow>(),
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
    public void Build_NullInteractionLens_IsByteIdenticalToDefault()
    {
        ManabaseReport report = CedhReport();

        string baseline = ManabaseReportTextBuilder.Build(report, null, null, ManabaseMode.Cedh);
        string withNullLens = ManabaseReportTextBuilder.Build(
            report,
            null,
            null,
            ManabaseMode.Cedh,
            interactionLens: null);

        Assert.Equal(baseline, withNullLens);
        Assert.DoesNotContain("Early interaction (turns 1-3)", withNullLens, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithInteractionLens_AppendsEarlyInteractionBlock()
    {
        string output = ManabaseReportTextBuilder.Build(
            CedhReport(),
            null,
            null,
            ManabaseMode.Cedh,
            interactionLens: PopulatedInteractionLens());

        Assert.Contains("Early interaction (turns 1-3)", output, StringComparison.Ordinal);
        Assert.Contains("1 / 3 interaction held up by turn 3", output, StringComparison.Ordinal);
        Assert.Contains("Swan Song", output, StringComparison.Ordinal);
        Assert.Contains("61%", output, StringComparison.Ordinal);
        Assert.Contains("An Offer You Can't Refuse", output, StringComparison.Ordinal);
        Assert.Contains("assumes mana is held open", output, StringComparison.Ordinal);
        Assert.Contains("First-pass read only", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithEmptyInteractionLens_AppendsCautionWithoutRows()
    {
        string output = ManabaseReportTextBuilder.Build(
            CedhReport(),
            null,
            null,
            ManabaseMode.Cedh,
            interactionLens: EmptyInteractionLens());

        Assert.Contains("no cheap interaction found", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Swan Song", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Holdable %", output, StringComparison.Ordinal);
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
    public void Build_FullArtifactFractionalLandShortfall_HasNoPluralArtifacts_AndMatchesSummaryCount()
    {
        var report = new ManabaseReport
        {
            ActualLands = 36,
            TargetLands = 37.05,
            Summary = "Mode: Casual — Lands: 36 vs ~37.0 target (add ~1 land). Colors: every color adequately supported.",
            ColorFindings =
            [
                new ColorSourceFinding
                {
                    Color = ManaColor.Blue,
                    ActualSources = 20.0,
                    RequiredSources = 25,
                    DrivingSpell = "Counterspell",
                    UnderSupportedCount = 3,
                },
            ],
        };

        string output = ManabaseReportTextBuilder.Build(report, null, null, ManabaseMode.Casual);

        Assert.DoesNotContain("(s)", output, StringComparison.Ordinal);
        Assert.Contains("Lands: 36 vs ~37.0 recommended (add ~1 land).", output);
        Assert.Contains("Summary:", output);
        Assert.Contains("Mode: Casual — Lands: 36 vs ~37.0 target (add ~1 land).", output);
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

    // M10: a report whose castability rows include a commander, for the command-zone block.
    private static ManabaseReport ReportWithCommanderRow() => new()
    {
        ActualLands = 36,
        TargetLands = 37.0,
        ColorFindings = System.Array.Empty<ColorSourceFinding>(),
        Mode = ManabaseMode.Casual,
        Castability = new List<CardCastability>
        {
            new() { Name = "Atraxa", ManaValue = 4, OnCurveTurn = 4, CastPercent = 78, LimitingFactor = "color:W", IsCommander = true },
            new() { Name = "Cultivate", ManaValue = 3, OnCurveTurn = 3, CastPercent = 90, LimitingFactor = "mana" },
        },
        Summary = "Fine.",
    };

    [Fact]
    public void Build_IncludeCommandZone_AppendsCommanderAndCompanionBlock()
    {
        var companion = new CardCastability
        {
            Name = "Jegantha, the Wellspring",
            ManaValue = 5,
            OnCurveTurn = 5,
            CastPercent = 64,
            LimitingFactor = "mana",
        };

        string output = ManabaseReportTextBuilder.Build(
            ReportWithCommanderRow(), "Atraxa", null, ManabaseMode.Casual,
            includeCommandZone: true, companionRow: companion);

        Assert.Contains("Command-zone castability:", output, System.StringComparison.Ordinal);
        Assert.Contains("- Commander: Atraxa (~78%).", output, System.StringComparison.Ordinal);
        Assert.Contains("- Companion: Jegantha, the Wellspring (~64%, +3 generic to hand tax heuristic).", output, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Build_CommandZoneOff_IsByteIdenticalToDefault()
    {
        ManabaseReport report = ReportWithCommanderRow();

        string baseline = ManabaseReportTextBuilder.Build(report, "Atraxa", null);
        string off = ManabaseReportTextBuilder.Build(
            report, "Atraxa", null, ManabaseMode.Casual, includeCommandZone: false, companionRow: null);

        Assert.Equal(baseline, off);
        Assert.DoesNotContain("Command-zone castability:", off, System.StringComparison.Ordinal);
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
        int verdictIndex = output.IndexOf("Reading the deck:", StringComparison.Ordinal);
        int colorSourcesIndex = output.IndexOf("Color Sources (per-color shortfalls are heuristic guidance):", StringComparison.Ordinal);

        Assert.True(verdictIndex > summaryIndex);
        Assert.True(colorSourcesIndex > verdictIndex);
        Assert.Contains("1. You're ~3 White sources short - heuristic guidance:", output);
        Assert.Contains("2. Ramp looks light: the deck runs ~6 ramp", output);
        Assert.Contains("Ramp/draw: ~6 ramp / ~12 draw vs a ~12/12 community target for a ~MV4 threshold (the commander's mana value); (1 do both). community heuristic, not Karsten math.", output);
    }

    [Fact]
    public void Build_VerdictWithOverflowLine_PreservesPlusCountInTextArtifact()
    {
        ManabaseVerdict verdict = new()
        {
            HasIssues = true,
            Headline = "Reading the deck",
            NoIssueReason = string.Empty,
            Lines =
            [
                "First",
                "Second",
                "Third",
                "…plus 2 more",
            ],
        };

        string output = ManabaseReportTextBuilder.Build(HealthyCasualReport(), null, null, ManabaseMode.Casual, verdict);

        Assert.Contains("4. …plus 2 more", output);
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

    [Fact]
    public void Build_WithScrySourceCredit_AppendsAuditableDisclosureLine()
    {
        var report = new ManabaseReport
        {
            ActualLands = 35,
            TargetLands = 36.0,
            ColorFindings = new List<ColorSourceFinding>
            {
                new()
                {
                    Color = ManaColor.Blue,
                    ActualSources = 12.4,
                    RequiredSources = 10,
                    DrivingSpell = "Counterspell",
                },
            },
            ScrySourceCreditCopies = 2,
            Mode = ManabaseMode.Casual,
            Summary = "Scry credit test.",
        };

        string output = ManabaseReportTextBuilder.Build(report, null, null);

        Assert.Contains("Scry source credit: +0.4 any-color sources", output, StringComparison.Ordinal);
        Assert.Contains("(2 cheap scry spells × 0.2)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithColorlessAndSnowRequirementRows_UsesDedicatedLabels()
    {
        var report = new ManabaseReport
        {
            ActualLands = 24,
            TargetLands = 24.0,
            ColorFindings = new List<ColorSourceFinding>
            {
                new()
                {
                    Color = ManaColor.Colorless,
                    DisplayColor = "Colorless",
                    ActualSources = 10.0,
                    RequiredSources = 10,
                    DrivingSpell = "Thought-Knot Seer",
                },
                new()
                {
                    Color = ManaColor.Colorless,
                    DisplayColor = "Snow",
                    ActualSources = 14.0,
                    RequiredSources = 14,
                    DrivingSpell = "Arcum's Astrolabe",
                },
            },
            Mode = ManabaseMode.Casual,
            Summary = "Category rows test.",
        };

        string output = ManabaseReportTextBuilder.Build(report, null, null);

        Assert.Contains("Colorless", output, StringComparison.Ordinal);
        Assert.Contains("Thought-Knot Seer", output, StringComparison.Ordinal);
        Assert.Contains("Snow", output, StringComparison.Ordinal);
        Assert.Contains("Arcum's Astrolabe", output, StringComparison.Ordinal);
    }

    // --- TAP-01/TAP-02 (Phase 75) ----------------------------------------
    // Byte-identity is GREEN now (tap=null appends nothing). The content/omit facts are RED until
    // plan 75-02 appends the "Untapped Sources:" block.

    private static ManabaseTapAnalysis MultiColorTap() => new()
    {
        OverallUntappedPercent = 82,
        UntappedSources = 29.5,
        TotalSources = 36.0,
        Turn1UntappedPercent = 76,
        ColorTap = new Dictionary<ManaColor, ColorTapFinding>
        {
            [ManaColor.White] = new() { UntappedSources = 16.0, TotalSources = 20.0, UntappedPercent = 80 },
            [ManaColor.Blue] = new() { UntappedSources = 13.5, TotalSources = 16.0, UntappedPercent = 84 },
        },
    };

    [Fact]
    public void Build_NullTap_OutputByteIdenticalToOverloadWithoutTapParam()
    {
        ManabaseReport report = HealthyCasualReport();

        string withoutTap = ManabaseReportTextBuilder.Build(report, "Test", null);
        string withNullTap = ManabaseReportTextBuilder.Build(report, "Test", null, tap: null);

        Assert.Equal(withoutTap, withNullTap);
    }

    [Fact]
    public void Build_WithTapAnalysis_ContainsUntappedSourcesSection()
    {
        string text = ManabaseReportTextBuilder.Build(
            HealthyCasualReport(), "Test", null, tap: MultiColorTap());

        Assert.Contains("Untapped Sources:", text);
        Assert.Contains("Turn-1 untapped availability:", text);
        // TAP-02 color-matched microcopy (overridden 2026-06-28): the parenthetical must call out
        // that the untapped source has to be a NEEDED COLOR, not merely any untapped mana.
        Assert.Contains(
            "(share of games with an untapped source of a needed color on turn 1)",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SingleColorDeckWithTap_OmitsPerColorTable()
    {
        var report = new ManabaseReport
        {
            ActualLands = 36,
            TargetLands = 37.0,
            ColorFindings = new List<ColorSourceFinding>
            {
                new()
                {
                    Color = ManaColor.Green,
                    ActualSources = 30.0,
                    RequiredSources = 18,
                    DrivingSpell = "Craterhoof Behemoth",
                },
            },
            Mode = ManabaseMode.Casual,
            Summary = "Mono-green mana base.",
        };
        var tap = new ManabaseTapAnalysis
        {
            OverallUntappedPercent = 90,
            UntappedSources = 27.0,
            TotalSources = 30.0,
            Turn1UntappedPercent = 88,
            ColorTap = new Dictionary<ManaColor, ColorTapFinding>
            {
                [ManaColor.Green] = new() { UntappedSources = 27.0, TotalSources = 30.0, UntappedPercent = 90 },
            },
        };

        string text = ManabaseReportTextBuilder.Build(report, "Mono Green", null, tap: tap);

        // The block exists (RED until 75-02) ...
        Assert.Contains("Untapped Sources:", text);
        // ... but for a single-color deck the per-color table is omitted (Pitfall 5).
        int blockStart = text.IndexOf("Untapped Sources:", StringComparison.Ordinal);
        string block = blockStart >= 0 ? text[blockStart..] : text;
        Assert.DoesNotContain("Color", block.Replace("colored sources", string.Empty));
    }
}
