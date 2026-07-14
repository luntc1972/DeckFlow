using System.Collections.Generic;
using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates <see cref="ManabaseSwapPromptBuilder"/>: the prompt frames the flagged color
/// deficits, the deck name, and the decklist for an LLM.
/// </summary>
public sealed class ManabaseSwapPromptBuilderTests
{
    private static readonly ManabaseVerdict IssueVerdict = new()
    {
        HasIssues = true,
        Headline = "Reading your deck",
        NoIssueReason = string.Empty,
        Lines =
        [
            "You're ~3 White sources short - heuristic guidance: add ~3 White-producing lands/rocks; consider cutting a colorless utility land.",
            "Ramp looks light: you run ~6 ramp vs a ~12/12 split for a ~MV4 threshold (your commander's mana value) - add ~6 ramp pieces (e.g. a 2-mana rock). (community heuristic, not Karsten math)",
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

    private static ManabaseReport ReportWithDeficit() => new()
    {
        ActualLands = 34,
        TargetLands = 37.5,
        Summary = "test",
        ColorFindings = new List<ColorSourceFinding>
        {
            new()
            {
                Color = ManaColor.White,
                ActualSources = 12,
                RequiredSources = 15,
                DrivingSpell = "Swords to Plowshares",
            },
        },
        Castability = new List<CardCastability>
        {
            new() { Name = "Isshin, Two Heavens as One", ManaValue = 3, OnCurveTurn = 3, CastPercent = 78, LimitingFactor = "color:White", IsCommander = true },
            new() { Name = "Akiri, Line-Slinger", ManaValue = 2, OnCurveTurn = 2, CastPercent = 92, LimitingFactor = "mana", IsCommander = true },
            new() { Name = "Swords to Plowshares", ManaValue = 1, OnCurveTurn = 1, CastPercent = 97, LimitingFactor = "mana" },
        },
    };

    private static ManabaseInteractionLens PopulatedInteractionLens() => new()
    {
        QualifyingCount = 4,
        OnTargetCount = 2,
        Threshold = 88,
        Rows = new List<ManabaseInteractionRow>
        {
            new() { Name = "Swan Song", HoldablePercent = 63 },
            new() { Name = "Flusterstorm", HoldablePercent = 79 },
            new() { Name = "An Offer You Can't Refuse", HoldablePercent = 89 },
        },
    };

    private static ManabaseInteractionLens EmptyInteractionLens() => new()
    {
        QualifyingCount = 0,
        OnTargetCount = 0,
        Threshold = 88,
        Rows = Array.Empty<ManabaseInteractionRow>(),
    };

    [Fact]
    public void Build_IncludesDeckName_DeficitColor_AndDecklist()
    {
        string prompt = ManabaseSwapPromptBuilder.Build(ReportWithDeficit(), "My Deck", "1 Plains\n1 Island");

        Assert.Contains("My Deck", prompt);
        Assert.Contains("White", prompt);
        Assert.Contains("Swords to Plowshares", prompt);
        Assert.Contains("1 Plains", prompt);
        Assert.Contains("add ~", prompt); // a land recommendation surfaced
        Assert.Contains("per-color add counts are heuristic guidance", prompt);
    }

    [Fact]
    public void Build_HealthyDeck_StatesAdequate()
    {
        var healthy = new ManabaseReport
        {
            ActualLands = 38,
            TargetLands = 37.5,
            Summary = "ok",
            ColorFindings = new List<ColorSourceFinding>
            {
                new() { Color = ManaColor.Blue, ActualSources = 20, RequiredSources = 15, DrivingSpell = "Counterspell" },
            },
        };

        string prompt = ManabaseSwapPromptBuilder.Build(healthy, null, null);

        Assert.Contains("healthy", prompt);
        Assert.DoesNotContain("Decklist:", prompt); // no decklist supplied
    }

    [Fact]
    public void Build_LandShortfallCoveredByRamp_DoesNotAskForLands()
    {
        // Efficacy R2 finding H3: page, .txt, and PrimaryFix all suppress the "add lands" advice
        // when the sim says ramp covers the paper shortfall — the swap prompt used to contradict
        // them and tell the LLM to add ~N lands anyway. A report 3 lands under target with every
        // color clean computes LandShortfallCoveredByRamp = true.
        var covered = new ManabaseReport
        {
            ActualLands = 30,
            TargetLands = 33.0,
            Summary = "ramp-covered",
            ColorFindings = new List<ColorSourceFinding>
            {
                new() { Color = ManaColor.Blue, ActualSources = 20, RequiredSources = 15, DrivingSpell = "Counterspell" },
            },
        };

        Assert.True(covered.LandShortfallCoveredByRamp); // fixture sanity: the sim signal is on

        string prompt = ManabaseSwapPromptBuilder.Build(covered, "My Deck", "1 Island");

        Assert.Contains("ramp covers it", prompt);
        Assert.Contains("do NOT recommend adding lands", prompt);
        Assert.DoesNotContain("add ~3 more lands", prompt);
    }

    [Fact]
    public void Build_NullVerdictAndBudget_IsByteIdentical()
    {
        ManabaseReport report = ReportWithDeficit();

        string baseline = ManabaseSwapPromptBuilder.Build(report, "My Deck", "1 Plains\n1 Island");
        string withNulls = ManabaseSwapPromptBuilder.Build(
            report,
            "My Deck",
            "1 Plains\n1 Island",
            ManabaseMode.Casual,
            null,
            null);

        Assert.Equal(baseline, withNulls);
    }

    [Fact]
    public void Build_DefaultCommandZoneParameters_KeepPromptByteIdentical()
    {
        ManabaseReport report = ReportWithDeficit();

        string baseline = ManabaseSwapPromptBuilder.Build(
            report,
            "My Deck",
            "1 Plains\n1 Island",
            ManabaseMode.Casual,
            IssueVerdict,
            Budget);

        string withExplicitDefaults = ManabaseSwapPromptBuilder.Build(
            report,
            "My Deck",
            "1 Plains\n1 Island",
            ManabaseMode.Casual,
            IssueVerdict,
            Budget,
            false,
            null);

        Assert.Equal(baseline, withExplicitDefaults);
    }

    [Fact]
    public void Build_CedhNullInteractionLens_IsByteIdenticalToDefault()
    {
        ManabaseReport report = ReportWithDeficit();

        string baseline = ManabaseSwapPromptBuilder.Build(report, "My Deck", "1 Plains", ManabaseMode.Cedh);
        string withNullLens = ManabaseSwapPromptBuilder.Build(
            report,
            "My Deck",
            "1 Plains",
            ManabaseMode.Cedh,
            interactionLens: null);

        Assert.Equal(baseline, withNullLens);
        Assert.Contains("prioritize early (turn 1–3) untapped colored access for cheap interaction.", withNullLens);
    }

    [Fact]
    public void Build_CedhInteractionLens_ReplacesGenericSentenceWithRealCounts()
    {
        string prompt = ManabaseSwapPromptBuilder.Build(
            ReportWithDeficit(),
            "My Deck",
            "1 Plains",
            ManabaseMode.Cedh,
            interactionLens: PopulatedInteractionLens());

        Assert.Contains("2 / 4", prompt, StringComparison.Ordinal);
        Assert.Contains("Swan Song", prompt, StringComparison.Ordinal);
        Assert.Contains("Flusterstorm", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("prioritize early (turn 1–3) untapped colored access for cheap interaction.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_CedhEmptyInteractionLens_StatesNoCheapInteractionFound()
    {
        string prompt = ManabaseSwapPromptBuilder.Build(
            ReportWithDeficit(),
            "My Deck",
            "1 Plains",
            ManabaseMode.Cedh,
            interactionLens: EmptyInteractionLens());

        Assert.Contains("no cheap interaction found", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Swan Song", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WithVerdictAndBudget_AppendsReadingDeckBlockBeforeAsk()
    {
        string prompt = ManabaseSwapPromptBuilder.Build(
            ReportWithDeficit(),
            "My Deck",
            "1 Plains\n1 Island",
            ManabaseMode.Casual,
            IssueVerdict,
            Budget);

        int verdictIndex = prompt.IndexOf("Reading your deck:", global::System.StringComparison.Ordinal);
        int askIndex = prompt.IndexOf("Please recommend SPECIFIC lands", global::System.StringComparison.Ordinal);

        Assert.True(verdictIndex >= 0);
        Assert.True(askIndex > verdictIndex);
        Assert.Contains("1. You're ~3 White sources short - heuristic guidance:", prompt);
        Assert.Contains("2. Ramp looks light: you run ~6 ramp", prompt);
        Assert.Contains("Ramp/draw: ~6 ramp / ~12 draw vs a ~12/12 community target for a ~MV4 threshold (your commander's mana value); (1 do both). community heuristic, not Karsten math.", prompt);
    }

    [Fact]
    public void Build_WithCommandZoneEnabled_AppendsCommanderAndCompanionBlock()
    {
        CardCastability companionRow = new()
        {
            Name = "Jegantha, the Wellspring",
            ManaValue = 8,
            OnCurveTurn = 8,
            CastPercent = 41,
            LimitingFactor = "mana",
        };

        string prompt = ManabaseSwapPromptBuilder.Build(
            ReportWithDeficit(),
            "My Deck",
            "1 Plains\n1 Island",
            ManabaseMode.Casual,
            IssueVerdict,
            Budget,
            includeCommandZone: true,
            companionRow: companionRow);

        Assert.Contains("Command-zone castability:", prompt);
        Assert.Contains("Isshin, Two Heavens as One", prompt);
        Assert.Contains("~78%", prompt);
        Assert.Contains("Akiri, Line-Slinger", prompt);
        Assert.Contains("Jegantha, the Wellspring", prompt);
        Assert.Contains("+3 generic to hand tax heuristic", prompt);
    }
}
