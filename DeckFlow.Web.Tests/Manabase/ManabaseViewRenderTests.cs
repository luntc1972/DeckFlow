using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Controllers;
using DeckFlow.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Render-level guard for the flag-gated tap-analyzer card on <c>Views/Deck/Manabase.cshtml</c>.
/// Renders the real Razor view through <see cref="IRazorViewEngine"/> so the OFF page invariant
/// (no <c>manabase-taplens</c> markup) and the ON card presence are enforced in CI — a source-text
/// scan cannot distinguish the two states because the markup literal always exists in the .cshtml.
/// </summary>
public sealed class ManabaseViewRenderTests
{
    [Fact]
    public async Task OffState_FlagFalse_RendersNoTapAnalyzerMarkup()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false);

        string html = await RenderManabaseViewAsync(model);

        Assert.DoesNotContain("manabase-taplens", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Untapped sources\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("turn-1 untapped", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_FlagTrue_RendersTapAnalyzerCardWithTurn1AndOverallLines()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: true);

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("manabase-taplens", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Untapped sources\"", html, StringComparison.Ordinal);
        // Turn-1 headline (Turn1UntappedPercent = 76) + its unit microcopy.
        Assert.Contains("turn-1 untapped", html, StringComparison.Ordinal);
        // TAP-02 color-matched pill (overridden 2026-06-28): the explainer must say "of a needed color".
        Assert.Contains(
            "share of games with an untapped source of a needed color on turn 1",
            html,
            StringComparison.Ordinal);
        // Overall row (OverallUntappedPercent = 82) — distinct from the per-color rows (80 / 84).
        Assert.Contains("82% untapped", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OffState_SourceListFlagFalse_RendersNoManaSourceDisclosures()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: true, showSourceList: false);

        string html = await RenderManabaseViewAsync(model);

        Assert.DoesNotContain("Mana sources (", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Tapped sources (", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Temple of Enlightenment", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommunityBaseline_CommanderSource_RendersCommanderCopy_AndEdhrecAttribution()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                showCommunityBaseline: true,
                communityBaseline: new ManabaseCommunityBaseline
                {
                    Bracket = 2,
                    AvgLands = 35,
                    DeckCount = 48802,
                    Source = "edhrec-averages",
                    BracketSource = ManabaseBracketSource.Fallback,
                    ValueSource = ManabaseBaselineSource.Commander,
                    CommanderDisplayName = "The Ur-Dragon",
                }));

        Assert.Contains("EDHREC decks for The Ur-Dragon average", html, StringComparison.Ordinal);
        Assert.Contains("class=\"manabase-baseline-source\">Data from <a href=\"https://edhrec.com\" target=\"_blank\" rel=\"noopener noreferrer\">EDHREC</a></span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommunityBaseline_BlendedValueSource_RendersEdhrecAttribution_WithoutSourcePrefix()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                showCommunityBaseline: true,
                communityBaseline: new ManabaseCommunityBaseline
                {
                    Bracket = 3,
                    AvgLands = 35.2,
                    DeckCount = 250,
                    Source = "edhrec-pilot-aggregate",
                    BracketSource = ManabaseBracketSource.Fallback,
                    ValueSource = ManabaseBaselineSource.Blended,
                    CommanderDisplayName = "Kinnan, Bonder Prodigy",
                }));

        Assert.Contains("Data from <a href=\"https://edhrec.com\" target=\"_blank\" rel=\"noopener noreferrer\">EDHREC</a>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommunityBaseline_GlobalSource_RendersBracketCopy_WithoutAttribution()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                showCommunityBaseline: true,
                communityBaseline: new ManabaseCommunityBaseline
                {
                    Bracket = 2,
                    AvgLands = 35.9,
                    DeckCount = 124221,
                    Source = "edhrec-pilot-aggregate",
                    BracketSource = ManabaseBracketSource.Fallback,
                    ValueSource = ManabaseBaselineSource.Global,
                }));

        Assert.Contains("Community baseline", html, StringComparison.Ordinal);
        Assert.Contains("Core", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Data from EDHREC", html, StringComparison.Ordinal);
        Assert.DoesNotContain("EDHREC decks for", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommunityBaseline_GlobalValueSource_DoesNotRenderEdhrecAttribution_WhenSourceMatchesPrefix()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                showCommunityBaseline: true,
                communityBaseline: new ManabaseCommunityBaseline
                {
                    Bracket = 2,
                    AvgLands = 35.9,
                    DeckCount = 124221,
                    Source = "edhrec-averages",
                    BracketSource = ManabaseBracketSource.Fallback,
                    ValueSource = ManabaseBaselineSource.Global,
                }));

        Assert.DoesNotContain("Data from EDHREC", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommunityBaseline_FlagOff_RendersNoCommanderCopy_OrAttribution()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                showCommunityBaseline: false));

        Assert.DoesNotContain("EDHREC decks for The Ur-Dragon average", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Data from EDHREC", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_SourceListFlagTrue_RendersManaSourceAndTappedSourceDisclosures()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: true, showSourceList: true);

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("Mana sources (4)", html, StringComparison.Ordinal);
        Assert.Contains("Tapped sources (2)", html, StringComparison.Ordinal);
        Assert.Matches("2(?:×|&#xD7;) Temple of Enlightenment", html);
        Assert.Contains("Sol Ring", html, StringComparison.Ordinal);
        Assert.Contains("W U", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_SourceListFlagTrue_CapsRainbowSourceDisplayToCommanderIdentity()
    {
        var model = BuildPopulatedModel(
            showTapAnalyzer: true,
            showSourceList: true,
            commanderColors: new[] { ManaColor.White, ManaColor.Blue },
            manaSourceListings: new List<ManaSourceListing>
            {
                new()
                {
                    Name = "Temple of Enlightenment",
                    Colors = new[] { ManaColor.White, ManaColor.Blue },
                    IsLand = true,
                    EntersUntapped = false,
                },
                new()
                {
                    Name = "Temple of Enlightenment",
                    Colors = new[] { ManaColor.White, ManaColor.Blue },
                    IsLand = true,
                    EntersUntapped = false,
                },
                new()
                {
                    Name = "Command Tower",
                    Colors = new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green },
                    IsLand = true,
                    EntersUntapped = true,
                },
                new()
                {
                    Name = "Plains",
                    Colors = new[] { ManaColor.White },
                    IsLand = true,
                    EntersUntapped = true,
                },
                new()
                {
                    Name = "Sol Ring",
                    Colors = new[] { ManaColor.Colorless },
                    IsLand = false,
                    EntersUntapped = true,
                    ProducesColorless = true,
                },
            });

        string html = await RenderManabaseViewAsync(model);

        Assert.Matches("2(?:×|&#xD7;) Temple of Enlightenment</span>\\s*<span class=\"manabase-source-pips\" aria-label=\"white, blue\">W U</span>", html);
        Assert.Contains("Command Tower", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"white, blue, black, red, green\">W U B R G</span>", html, StringComparison.Ordinal);
        Assert.Matches("Command Tower</span>\\s*<span class=\"manabase-source-pips\" aria-label=\"white, blue\">W U</span>", html);
    }

    [Theory]
    [InlineData(new[] { ManaColor.White, ManaColor.Blue }, false, "W U")]
    [InlineData(new[] { ManaColor.Colorless }, true, "C")]
    [InlineData(new ManaColor[0], true, "C")]
    [InlineData(new ManaColor[0], false, "—")]
    public void ColorPips_FormatsLetters_Colorless_AndEmpty(ManaColor[] colors, bool producesColorless, string expected)
    {
        Assert.Equal(expected, ManabaseDisplay.ColorPips(colors, producesColorless).Text);
    }

    [Fact]
    public void ColorPips_CapsColoredPipsToCommanderIdentity()
    {
        var pips = ManabaseDisplay.ColorPips(
            new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green },
            producesColorless: false,
            commanderColors: new[] { ManaColor.White, ManaColor.Red });

        Assert.Equal("W R", pips.Text);
        Assert.Equal("white, red", pips.AriaLabel);
    }

    [Fact]
    public void ColorPips_KeepsColorlessUnderCommanderCap()
    {
        var colorlessOnly = ManabaseDisplay.ColorPips(
            new[] { ManaColor.Colorless },
            producesColorless: true,
            commanderColors: new[] { ManaColor.White, ManaColor.Red });
        var mixed = ManabaseDisplay.ColorPips(
            new[] { ManaColor.White, ManaColor.Blue, ManaColor.Black, ManaColor.Red, ManaColor.Green, ManaColor.Colorless },
            producesColorless: true,
            commanderColors: new[] { ManaColor.White, ManaColor.Red });

        Assert.Equal("C", colorlessOnly.Text);
        Assert.Equal("W R C", mixed.Text);
    }

    [Fact]
    public void ColorPips_EmptyCommanderColors_DoesNotCap()
    {
        var pips = ManabaseDisplay.ColorPips(
            new[] { ManaColor.White, ManaColor.Blue },
            producesColorless: false,
            commanderColors: Array.Empty<ManaColor>());

        Assert.Equal("W U", pips.Text);
        Assert.Equal("white, blue", pips.AriaLabel);
    }

    [Fact]
    public void ColorPips_NullCommanderColors_DoesNotCap()
    {
        var pips = ManabaseDisplay.ColorPips(
            new[] { ManaColor.White, ManaColor.Blue },
            producesColorless: false,
            commanderColors: null);

        Assert.Equal("W U", pips.Text);
        Assert.Equal("white, blue", pips.AriaLabel);
    }

    [Fact]
    public async Task OffState_MulliganFlagFalse_RendersNoMulliganLensMarkup()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: false);

        string html = await RenderManabaseViewAsync(model);

        Assert.DoesNotContain("manabase-mulliganlens", html, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label=\"Opening hand\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("keepable hands", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnState_MulliganFlagTrue_RendersOpeningHandLensCardWithTrackedSpell()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true);

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("manabase-mulliganlens", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Opening hand\"", html, StringComparison.Ordinal);
        // Keepable-band line (KeepableBand = "high", KeepableHandPercent = 82).
        Assert.Contains("high (~82%)", html, StringComparison.Ordinal);
        Assert.Contains("keepable hands", html, StringComparison.Ordinal);
        // Keep-size process line (Kept7Percent = 55, MulliganTo6Percent = 30, MulliganTo5Percent = 15).
        Assert.Contains("kept 7 ~55%", html, StringComparison.Ordinal);
        // Representative-opener line names the tracked spell, never a generic claim.
        Assert.Contains("Swords to Plowshares castable on curve (turn 1)", html, StringComparison.Ordinal);
        Assert.Contains("workable line", html, StringComparison.Ordinal);
        // Plan-presence line is NOT shown when its own flag is off, even with the opening-hand block on.
        Assert.DoesNotContain("Payoff on curve", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MulliganOpeners_NoCastablePlanSample_RendersNoPlanLine()
    {
        // The mull-to-5 opener in the fixture holds no castable plan (empty tracked name). It must render
        // the plain "no castable plan by its curve turn" phrasing and a "no clear line" verdict — never a
        // dangling "<empty> castable on curve" line.
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showPlanPresence: true);

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("no castable plan by its curve turn", html, StringComparison.Ordinal);
        Assert.Contains("no clear line", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanPresenceFlagTrue_RendersWithAPlanLineAndRoleBreakdown()
    {
        var model = BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showPlanPresence: true);

        string html = await RenderManabaseViewAsync(model);

        // Payoff-led headline (PayoffPercent = 55, PayoffBand = "high") + secondary composite (74%).
        Assert.Contains("Payoff on curve", html, StringComparison.Ordinal);
        Assert.Contains("~55%", html, StringComparison.Ordinal);
        Assert.Contains("~74%", html, StringComparison.Ordinal);
        // Per-role breakdown moves to its own muted sub-line; nonzero roles surfaced, zero-role
        // (Engine) omitted, and the Payoff role is NOT repeated (it is already the headline number).
        Assert.Contains("by role:", html, StringComparison.Ordinal);
        Assert.Contains("tutor/combo ~20%", html, StringComparison.Ordinal);
        Assert.Contains("interaction ~40%", html, StringComparison.Ordinal);
        Assert.DoesNotContain("engine ~", html, StringComparison.Ordinal);
        Assert.DoesNotContain("payoff ~55%", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepShapesFlagTrue_Cedh_RendersSecondHeadline_AndShapeLabels()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showKeepShapes: true, mode: ManabaseMode.Cedh));

        Assert.Contains("mana-keepable hands", html, StringComparison.Ordinal);
        Assert.Contains("plan-keepable hands", html, StringComparison.Ordinal);
        Assert.Contains("passed a cEDH keep shape (explosive / early engine / interaction bridge)", html, StringComparison.Ordinal);
        Assert.Contains("high (~82%)", html, StringComparison.Ordinal);
        Assert.Contains("medium (~64%)", html, StringComparison.Ordinal);
        Assert.Contains("explosive keep", html, StringComparison.Ordinal);
        Assert.Contains("no plan by turn 4 - mulligan", html, StringComparison.Ordinal);
        Assert.DoesNotContain("workable line", html, StringComparison.Ordinal);
        Assert.DoesNotContain("no clear line", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepShapesFlagTrue_Casual_RendersCurveCoverageLine()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showKeepShapes: true, mode: ManabaseMode.Casual));

        Assert.Contains("Curve coverage", html, StringComparison.Ordinal);
        Assert.Contains("plays a spell on ~4 of first 5 turns", html, StringComparison.Ordinal);
        Assert.DoesNotContain("plan-keepable hands", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeepShapesOff_IsByteIdenticalToOnWithKeepShapesMarkupExcised()
    {
        string offHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showPlanPresence: true, showKeepShapes: false, mode: ManabaseMode.Cedh)));
        string onHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true, showPlanPresence: true, showKeepShapes: true, mode: ManabaseMode.Cedh)));

        string excised = ExciseKeepShapesMarkup(onHtml);

        Assert.Equal(offHtml, excised);
    }

    [Fact]
    public async Task OffState_IsByteIdenticalToOnWithMulliganCardExcised()
    {
        // Mirrors OffState_IsByteIdenticalToOnWithTapCardExcised for the opening-hand card: the OFF
        // and ON models are identical except for ShowMulliganEval (ShowTapAnalyzer held constant at
        // false so the tap card never appears in either render), so the only difference between the
        // two pages must be the contiguous mulligan-lens block.
        string offHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: false)));
        string onHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true)));

        int prefix = CommonPrefixLength(offHtml, onHtml);
        int suffix = CommonSuffixLength(offHtml, onHtml, prefix);

        string offMiddle = offHtml[prefix..(offHtml.Length - suffix)];
        string onMiddle = onHtml[prefix..(onHtml.Length - suffix)];

        // OFF emits nothing in the differing region — byte-identical to ON minus the mulligan card.
        Assert.Equal(string.Empty, offMiddle);
        // Sanity: the isolated ON region is exactly the opening-hand lens card.
        Assert.StartsWith("<div class=\"manabase-lens manabase-mulliganlens\"", onMiddle.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("</div>", onMiddle.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Opening hand\"", onMiddle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OffState_IsByteIdenticalToOnWithTapCardExcised()
    {
        // Codex MED2 — a stronger OFF-path guard than substring-absence. The OFF and ON models are
        // identical except for ShowTapAnalyzer, so the ONLY difference between the two rendered pages
        // must be the contiguous tap-card block. We locate that single differing region via the
        // longest common prefix + suffix of the two outputs:
        //   off = A + offMiddle + B   on = A + onMiddle + B
        // and assert offMiddle is EMPTY (byte-for-byte: OFF must emit nothing — not even a stray space
        // or newline — where the @if lives) while onMiddle is exactly the tap-card <div>…</div>. A
        // whitespace leak when OFF would make offMiddle non-empty and fail this test.
        // The page emits two @Html.AntiForgeryToken() fields whose value is randomized per render;
        // neutralize them so the ONLY remaining difference is the tap card itself.
        string offHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false)));
        string onHtml = NormalizeAntiForgery(await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: true)));

        int prefix = CommonPrefixLength(offHtml, onHtml);
        int suffix = CommonSuffixLength(offHtml, onHtml, prefix);

        string offMiddle = offHtml[prefix..(offHtml.Length - suffix)];
        string onMiddle = onHtml[prefix..(onHtml.Length - suffix)];

        // OFF emits nothing in the differing region — byte-identical to ON minus the tap card. A
        // stray whitespace/newline leak when the flag is off would make offMiddle non-empty here.
        Assert.Equal(string.Empty, offMiddle);
        // Sanity: the isolated ON region is exactly the tap-analyzer card (modulo its leading indent).
        Assert.StartsWith("<div class=\"manabase-lens manabase-taplens\"", onMiddle.TrimStart(), StringComparison.Ordinal);
        Assert.EndsWith("</div>", onMiddle.TrimEnd(), StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Untapped sources\"", onMiddle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Summary_LeadsResultPanel_BeforeTheTwoLensGrid()
    {
        // Verdict-first: the .manabase-summary card (health + lands + biggest fix) must render before
        // the supporting two-lens grid so the answer is read before the evidence.
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false));

        int summaryIdx = html.IndexOf("class=\"manabase-summary\"", StringComparison.Ordinal);
        int twoLensIdx = html.IndexOf("manabase-twolens", StringComparison.Ordinal);

        Assert.True(summaryIdx >= 0, "Summary card should render.");
        Assert.True(twoLensIdx >= 0, "Two-lens grid should render for a multi-color report.");
        Assert.True(summaryIdx < twoLensIdx, "Summary must precede the two-lens grid.");
        // The wide color table uses the card-stack reflow on mobile (data-label cells).
        Assert.Contains("manabase-table--card", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BiggestFix_RendersExactlyOnce_InTheSummaryNotBelowTheColorTable()
    {
        // The biggest-fix callout moved into the summary; it must not also render in its old
        // mode-note slot below the color table (no duplication).
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false));

        Assert.Single(Regex.Matches(html, "manabase-summary-fix"));
        Assert.DoesNotContain("mode-note\"><strong>Biggest fix", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningHandHeadline_UsesSoftHierarchyClass()
    {
        // Hierarchy fix: the opening-hand headline is downweighted vs the cast-rate headline.
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, showMulliganEval: true));

        Assert.Contains("manabase-lens-big--soft", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RampDrawBalancedLine_SuppressedWhenBudgetNotBalanced()
    {
        // Contradiction fix (view side): a draw-light budget reports IsBalanced=false, so the
        // "looks balanced" clause must not render beside a draw-light verdict.
        string balancedHtml = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, rampDrawBudget: Budget(isBalanced: true)));
        Assert.Contains("looks balanced", balancedHtml, StringComparison.Ordinal);

        string unbalancedHtml = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, rampDrawBudget: Budget(isBalanced: false)));
        Assert.DoesNotContain("looks balanced", unbalancedHtml, StringComparison.Ordinal);
        // The count line still renders — the section is never empty.
        Assert.Contains("ramp /", unbalancedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BothLensesShown_RendersReconciliationNoteAndBothTableScrollHints()
    {
        // With a Casual report that carries Castability rows, both the Karsten source-check lens and
        // the simulated cast-rate lens render, so the reconciliation note appears; and both wide
        // tables (color + castability) each carry a scroll hint.
        var model = new ManabaseViewModel
        {
            Request = new ManabaseRequest { DeckInputSource = DeckInputSource.PasteText, Mode = ManabaseMode.Casual },
            InputSummary = "Test deck",
            Report = new ManabaseReport
            {
                ActualLands = 36,
                TargetLands = 37.0,
                Mode = ManabaseMode.Casual,
                Summary = "x",
                ColorFindings = new List<ColorSourceFinding>
                {
                    new() { Color = ManaColor.White, ActualSources = 20.0, RequiredSources = 18, DrivingSpell = "Swords to Plowshares", UntappedSources = 16.0 },
                    new() { Color = ManaColor.Blue, ActualSources = 16.0, RequiredSources = 14, DrivingSpell = "Counterspell", UntappedSources = 13.5 },
                },
                Castability = new List<CardCastability>
                {
                    new() { Name = "Swords to Plowshares", ManaValue = 1, OnCurveTurn = 1, CastPercent = 95, LimitingFactor = "color: White" },
                },
            },
        };

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("manabase-twolens-note", html, StringComparison.Ordinal);
        Assert.Contains("Read the two together", html, StringComparison.Ordinal);
        // Card-stack reflow (#4): both wide tables are card-mode with per-cell data-labels; the
        // old scroll-hint is gone.
        Assert.Contains("manabase-table--card", html, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Color\"", html, StringComparison.Ordinal);
        Assert.Contains("data-label=\"Cast on curve\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("manabase-scroll-hint", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommanderSelectionRequired_RendersPickerSelectAndAutocomplete()
    {
        var model = new ManabaseViewModel
        {
            Request = new ManabaseRequest
            {
                DeckInputSource = DeckInputSource.PasteText,
                DeckText = "1 Winota, Joiner of Forces",
                SelectedCommander = "Winota, Joiner of Forces",
            },
            CommanderSelectionRequired = true,
            CommanderChoices = new[] { "Winota, Joiner of Forces", "Alesha, Who Smiles at Death" },
        };

        string html = await RenderManabaseViewAsync(model);

        Assert.Contains("Pick your commander", html, StringComparison.Ordinal);
        Assert.Contains("name=\"SelectedCommander\"", html, StringComparison.Ordinal);
        Assert.Contains("data-commander-search=\"/manabase/commander-search\"", html, StringComparison.Ordinal);
        Assert.Contains("data-commander-target=\"#manabase-selected-commander\"", html, StringComparison.Ordinal);
        Assert.Contains("Winota, Joiner of Forces", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommanderSelectionNotRequired_DoesNotRenderPicker()
    {
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false));

        Assert.DoesNotContain("Pick your commander", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-commander-search=\"/manabase/commander-search\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CasualContextHeader_RendersCommanderName_AndCastingPriority()
    {
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false));

        Assert.Contains("Mode: <strong>Casual</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Commander: <strong>Winota, Joiner of Forces</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Casting priority: <strong>Standard</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CedhContextHeader_RendersCommanderName_WhenCastabilityTableIsHidden()
    {
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Cedh));

        Assert.Contains("Mode: <strong>cEDH</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Commander: <strong>Winota, Joiner of Forces</strong>", html, StringComparison.Ordinal);
        Assert.Contains("Casting priority: <strong>Standard</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Command-zone castability", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CedhSummary_RendersMetaRange_WhenBaselineFieldsArePresent()
    {
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Cedh, includeCedhRange: true));

        Assert.Contains("cEDH meta range ~26–29 lands (33 cEDH tournament decks, 2026-07 sample; mean 27.5 ±1.6).", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CasualSummary_DoesNotRenderMetaRange_WhenBaselineFieldsAreAbsent()
    {
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Casual, includeCedhRange: false));

        Assert.DoesNotContain("cEDH meta range", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CedhHowItWorks_BlendedTarget_RendersBlendWordingAndFloor22()
    {
        string html = await RenderManabaseViewAsync(BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Cedh, includeCedhRange: true));

        Assert.Contains("then nudged 50% toward this commander's tournament land mean", html, StringComparison.Ordinal);
        Assert.Contains("safety floor 22", html, StringComparison.Ordinal);
        Assert.DoesNotContain("floored at 28", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CedhHowItWorks_FlagOffStyleTarget_RendersFloor28WithoutBlendWording()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Cedh, includeCedhRange: false, cedhSafetyFloor: 28.0, cedhBaselineBlended: false));

        Assert.Contains("cEDH adjustment (−3.5, floor 28 lands)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("nudged 50% toward this commander's tournament land mean", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CedhHowItWorks_RendersRitualLandCreditLine_WhenBreakdownCarriesCredit()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Cedh, ritualLandCredit: 1.0, netPositiveRitualCount: 2));

        // Pin the load-bearing tokens (value + count), not the explanatory prose tail.
        Assert.Contains("Ritual land credit: <strong>−1.0</strong>", html, StringComparison.Ordinal);
        Assert.Contains("(2 net-positive rituals × 0.5, cap 3)", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CedhHowItWorks_DoesNotRenderRitualLandCreditLine_WhenBreakdownHasNoCredit()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, mode: ManabaseMode.Cedh, ritualLandCredit: 0.0, netPositiveRitualCount: 2));

        Assert.DoesNotContain("Ritual land credit:", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThisDecksNumbers_RendersScrySourceCreditLine_WhenReportCarriesCredit()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, scrySourceCreditCopies: 2));

        Assert.Contains("Scry source credit:", html, StringComparison.Ordinal);
        Assert.Contains("any-color sources", html, StringComparison.Ordinal);
        Assert.Contains("2 cheap scry spells", html, StringComparison.Ordinal);
        Assert.Contains("0.2", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThisDecksNumbers_DoesNotRenderScrySourceCreditLine_WhenReportHasNoScryCopies()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(showTapAnalyzer: false, scrySourceCreditCopies: 0));

        Assert.DoesNotContain("Scry source credit:", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThisDecksNumbers_RendersDedicatedColorlessAndSnowRequirementLabels()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                colorFindings: new List<ColorSourceFinding>
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
                }));

        Assert.Contains("<strong>Colorless</strong>: 10.0 sources", html, StringComparison.Ordinal);
        Assert.Contains("Thought-Knot Seer", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Snow</strong>: 14.0 sources", html, StringComparison.Ordinal);
        Assert.Contains("Arcum&#x27;s Astrolabe", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThisDecksNumbers_UsesEvaluatedCardCountForSpecialCategoryPopulation()
    {
        string html = await RenderManabaseViewAsync(
            BuildPopulatedModel(
                showTapAnalyzer: false,
                colorFindings: new List<ColorSourceFinding>
                {
                    new()
                    {
                        Color = ManaColor.Colorless,
                        DisplayColor = "Snow",
                        ActualSources = 4.0,
                        RequiredSources = 8,
                        DrivingSpell = "Icehide Golem",
                        UnderSupportedCount = 0,
                        EvaluatedCardCount = 1,
                    },
                }));

        Assert.Contains("0 of 1 under-supported", html, StringComparison.Ordinal);
    }

    // Replace the randomized __RequestVerificationToken value with a constant so two renders of the
    // same model differ only by intentional content (here: the tap card).
    private static string NormalizeAntiForgery(string html) =>
        Regex.Replace(
            html,
            "(__RequestVerificationToken[^>]*?value=\")[^\"]*(\")",
            "$1NORMALIZED$2");

    private static int CommonPrefixLength(string a, string b)
    {
        int max = Math.Min(a.Length, b.Length);
        int i = 0;
        while (i < max && a[i] == b[i])
        {
            i++;
        }

        return i;
    }

    private static int CommonSuffixLength(string a, string b, int prefix)
    {
        int max = Math.Min(a.Length - prefix, b.Length - prefix);
        int i = 0;
        while (i < max && a[a.Length - 1 - i] == b[b.Length - 1 - i])
        {
            i++;
        }

        return i;
    }

    private static string ExciseKeepShapesMarkup(string html)
    {
        html = html.Replace("mana-keepable hands", "keepable hands", StringComparison.Ordinal);
        html = Regex.Replace(
            html,
            @"\s*<div class=""manabase-lens-big manabase-lens-big--soft"">(?:(?!</div>).)*<span>plan-keepable hands</span>\s*</div>\s*<span class=""manabase-lens-pill"">plan-keepable = passed a cEDH keep shape \(explosive / early engine / interaction bridge\)</span>",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.Singleline);
        html = html.Replace("— explosive keep", "— workable line", StringComparison.Ordinal);
        html = html.Replace("— no plan by turn 4 - mulligan", "— no clear line", StringComparison.Ordinal);
        return html;
    }

    private static ManabaseViewModel BuildPopulatedModel(
        bool showTapAnalyzer,
        bool showMulliganEval = false,
        bool showPlanPresence = false,
        bool showKeepShapes = false,
        bool showSourceList = false,
        bool showCommunityBaseline = false,
        ManabaseCommunityBaseline? communityBaseline = null,
        ManabaseRampDrawBudget? rampDrawBudget = null,
        ManabaseMode mode = ManabaseMode.Casual,
        bool includeCedhRange = false,
        double? cedhSafetyFloor = null,
        bool? cedhBaselineBlended = null,
        double ritualLandCredit = 0.0,
        int netPositiveRitualCount = 0,
        int scrySourceCreditCopies = 0,
        IReadOnlyList<ColorSourceFinding>? colorFindings = null,
        IReadOnlyList<ManaColor>? commanderColors = null,
        IReadOnlyList<ManaSourceListing>? manaSourceListings = null) => new()
        {
            Request = new ManabaseRequest
            {
                DeckInputSource = DeckInputSource.PasteText,
                Mode = mode,
            },
            InputSummary = "Test deck · 99 cards + 1 commander",
            Report = ReportWithTapAnalysis(
                mode,
                includeCedhRange,
                cedhSafetyFloor,
                cedhBaselineBlended,
                ritualLandCredit,
                netPositiveRitualCount,
                scrySourceCreditCopies,
                colorFindings,
                commanderColors,
                manaSourceListings),
            ShowTapAnalyzer = showTapAnalyzer,
            ShowMulliganEval = showMulliganEval,
            ShowPlanPresence = showPlanPresence,
            ShowKeepShapes = showKeepShapes,
            ShowSourceList = showSourceList,
            CommunityBaseline = communityBaseline,
            ShowCommunityBaseline = showCommunityBaseline,
            RampDrawBudget = rampDrawBudget,
        };

    private static ManabaseRampDrawBudget Budget(bool isBalanced) => new()
    {
        RampCount = 12,
        DrawCount = isBalanced ? 12 : 8,
        OverlapCount = 0,
        Threshold = 4.0,
        ThresholdSource = ManabaseRampDrawThresholdSource.CommanderManaValue,
        TargetRamp = 12,
        TargetDraw = 12,
        IsBalanced = isBalanced,
        IsRampLight = false,
        IsRampHeavy = false,
        RampShort = 0,
        IsDrawLight = !isBalanced,
        DrawShort = isBalanced ? 0 : 4,
    };

    /// <summary>
    /// A multi-color report carrying populated tap analysis (ColorFindings.Count &gt; 1) AND a
    /// populated mulligan evaluation, so the tap and mulligan flags can be toggled independently
    /// against the same fixed report.
    /// </summary>
    private static ManabaseReport ReportWithTapAnalysis(
        ManabaseMode mode,
        bool includeCedhRange,
        double? cedhSafetyFloor,
        bool? cedhBaselineBlended,
        double ritualLandCredit,
        int netPositiveRitualCount,
        int scrySourceCreditCopies,
        IReadOnlyList<ColorSourceFinding>? colorFindings,
        IReadOnlyList<ManaColor>? commanderColors,
        IReadOnlyList<ManaSourceListing>? manaSourceListings) => new()
        {
            ActualLands = 36,
            TargetLands = 37.0,
            TargetLandsRangeLow = includeCedhRange ? 25.9 : null,
            TargetLandsRangeHigh = includeCedhRange ? 29.1 : null,
            BaselineDeckCount = includeCedhRange ? 33 : null,
            BaselineLandsMean = includeCedhRange ? 27.5 : null,
            BaselineLandsSd = includeCedhRange ? 1.6 : null,
            BaselineMonth = includeCedhRange ? "2026-07" : null,
            ScrySourceCreditCopies = scrySourceCreditCopies,
            ColorFindings = colorFindings ?? new List<ColorSourceFinding>
            {
                new()
                {
                    Color = ManaColor.White,
                    ActualSources = 20.0,
                    RequiredSources = 18,
                    DrivingSpell = "Swords to Plowshares",
                    UntappedSources = 16.0,
                },
                new()
                {
                    Color = ManaColor.Blue,
                    ActualSources = 16.0,
                    RequiredSources = 14,
                    DrivingSpell = "Counterspell",
                    UntappedSources = 13.5,
                },
            },
            CommanderColors = commanderColors ?? new[] { ManaColor.White, ManaColor.Blue },
            Mode = mode,
            Summary = "Mana base looks fine for this test.",
            LandTarget = new ManabaseLandTargetBreakdown
            {
                AverageManaValue = 2.8,
                RampAndDrawUnderThree = 6,
                FastMana = 2,
                CommanderCount = 1,
                LibrarySize = 99,
                BaseTarget = 31.0,
                CedhAdjustment = mode == ManabaseMode.Cedh ? -3.0 : 0.0,
                CedhSafetyFloor = cedhSafetyFloor ?? (mode == ManabaseMode.Cedh ? 22.0 : 0.0),
                CedhBaselineBlended = cedhBaselineBlended ?? (mode == ManabaseMode.Cedh && includeCedhRange),
                RitualLandCredit = ritualLandCredit,
                NetPositiveRitualCount = netPositiveRitualCount,
                FinalTarget = mode == ManabaseMode.Cedh ? 28.0 : 31.0,
            },
            Castability = new List<CardCastability>
        {
            new()
            {
                Name = "Winota, Joiner of Forces",
                ManaValue = 4,
                OnCurveTurn = 4,
                CastPercent = 62,
                LimitingFactor = "mana",
                IsCommander = true,
            },
            new()
            {
                Name = "Swords to Plowshares",
                ManaValue = 1,
                OnCurveTurn = 1,
                CastPercent = 95,
                LimitingFactor = "color: White",
            },
            new()
            {
                Name = "Counterspell",
                ManaValue = 2,
                OnCurveTurn = 2,
                CastPercent = 88,
                LimitingFactor = "color: Blue",
            },
        },
            TapAnalysis = new ManabaseTapAnalysis
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
            },
            ManaSourceListings = manaSourceListings ?? new List<ManaSourceListing>
            {
                new()
                {
                    Name = "Temple of Enlightenment",
                    Colors = new[] { ManaColor.White, ManaColor.Blue },
                    IsLand = true,
                    EntersUntapped = false,
                },
                new()
                {
                    Name = "Temple of Enlightenment",
                    Colors = new[] { ManaColor.White, ManaColor.Blue },
                    IsLand = true,
                    EntersUntapped = false,
                },
                new()
                {
                    Name = "Plains",
                    Colors = new[] { ManaColor.White },
                    IsLand = true,
                    EntersUntapped = true,
                },
                new()
                {
                    Name = "Sol Ring",
                    Colors = new[] { ManaColor.Colorless },
                    IsLand = false,
                    EntersUntapped = true,
                    ProducesColorless = true,
                },
            },
            MulliganEvaluation = new ManabaseMulliganEvaluation
            {
                KeepableHandPercent = 82,
                KeepableBand = "high",
                Kept7Percent = 55,
                MulliganTo6Percent = 30,
                MulliganTo5Percent = 15,
                ColorCount = 2,
                AverageManaValue = 2.8,
                PlanKeepablePercent = 64,
                PlanKeepableBand = "medium",
                CurveCoverageTurns = 3.6,
                RepresentativeOpeners = new List<OpeningHandSample>
            {
                new()
                {
                    Lands = 3,
                    Colors = 2,
                    RampPieces = 1,
                    OtherCards = 3,
                    KeptCards = 7,
                    Decision = "keep 7",
                    TrackedSpellName = "Swords to Plowshares",
                    TrackedOnCurveTurn = 1,
                    OnCurveCastable = true,
                    HasPlan = true,
                    ShapeLabel = "explosive keep",
                },
                new()
                {
                    // Plan-presence opener with no castable plan found at this depth: empty tracked name
                    // must render as "no castable plan", never a dangling on-curve line.
                    Lands = 2,
                    Colors = 1,
                    RampPieces = 0,
                    OtherCards = 3,
                    KeptCards = 5,
                    Decision = "mulligan to 5",
                    TrackedSpellName = string.Empty,
                    TrackedOnCurveTurn = 0,
                    OnCurveCastable = false,
                    HasPlan = false,
                    ShapeLabel = "no plan by turn 4 - mulligan",
                },
            },
                PlanPresence = new ManabasePlanPresence
                {
                    PayoffPercent = 55,
                    PayoffBand = "high",
                    PlanPresencePercent = 74,
                    Band = "high",
                    RolePercents = new Dictionary<PlanRole, int>
                    {
                        [PlanRole.Payoff] = 55,
                        [PlanRole.Engine] = 0,
                        [PlanRole.TutorCombo] = 20,
                        [PlanRole.Interaction] = 40,
                    },
                    KeepableTrials = 17000,
                },
            },
        };

    private static async Task<string> RenderManabaseViewAsync(ManabaseViewModel model)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        services.AddSingleton<DiagnosticListener>(_ => new DiagnosticListener("DeckFlow.Web.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider => serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddSingleton<IWebHostEnvironment>(CreateHostingEnvironment());
        services.AddSingleton<IHostEnvironment>(serviceProvider => serviceProvider.GetRequiredService<IWebHostEnvironment>());
        services.AddLogging();
        services.AddDataProtection();
        // The shared _DeckToolTabs partial (@inject) needs these two services to activate.
        services.AddSingleton<DeckFlow.Web.Services.Tools.IToolRegistry, DeckFlow.Web.Services.Tools.ToolRegistry>();
        services.AddSingleton<DeckFlow.Web.Services.FeatureFlags.IFeatureFlagCache>(new FakeFeatureFlagCache());
        services.AddControllersWithViews().AddApplicationPart(typeof(ManabaseController).Assembly);

        using var serviceProvider = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(new RouteValueDictionary(new Dictionary<string, object?> { ["controller"] = "Deck" })),
            new ActionDescriptor());
        var viewEngine = serviceProvider.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.FindView(actionContext, "Manabase", isMainPage: false);
        Assert.True(viewResult.Success, $"View 'Manabase' was not found. Searched: {string.Join(", ", viewResult.SearchedLocations ?? Array.Empty<string>())}");

        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        await using var writer = new StringWriter();
        var viewContext = new ViewContext(
            actionContext,
            viewResult.View!,
            viewData,
            new TempDataDictionary(httpContext, new StubTempDataProvider()),
            writer,
            new HtmlHelperOptions());

        await viewResult.View!.RenderAsync(viewContext);
        return writer.ToString();
    }

    private static IWebHostEnvironment CreateHostingEnvironment()
    {
        var contentRoot = AppContext.BaseDirectory;
        var fileProvider = new NullFileProvider();
        return new TestWebHostEnvironment
        {
            ApplicationName = typeof(ManabaseController).Assembly.GetName().Name ?? "DeckFlow.Web",
            ContentRootPath = contentRoot,
            ContentRootFileProvider = fileProvider,
            EnvironmentName = Environments.Development,
            WebRootPath = contentRoot,
            WebRootFileProvider = fileProvider,
        };
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
