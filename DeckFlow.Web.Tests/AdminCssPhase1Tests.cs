using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// File-level regression tests for the Phase 1 admin modal CSS contract.
/// </summary>
public sealed class AdminCssPhase1Tests
{
    private const string StartMarker = "/* === Phase 1 (v1.4) — WDG-04 Focus-Trapped Modal === */";
    private const string EndMarker = "/* === END Phase 1 === */";

    [Fact]
    public void Phase1_StartMarker_Present()
    {
        var content = ReadAdminCss();

        Assert.Contains(StartMarker, content);
    }

    [Fact]
    public void Phase1_EndMarker_Present()
    {
        var content = ReadAdminCss();

        Assert.Contains(EndMarker, content);
    }

    [Fact]
    public void Phase1_Section_NoBareElementSelectors()
    {
        var section = ReadPhase1Section();

        Assert.DoesNotMatch(
            new Regex(
                @"^(button|h[1-6]|div|p|input|select|textarea|a|ul|ol|li|table|tr|td|th)[\s,{]",
                RegexOptions.IgnoreCase | RegexOptions.Multiline),
            section);
    }

    [Fact]
    public void Phase1_HasTextWrapBalance()
    {
        var section = ReadPhase1Section();

        Assert.Contains("text-wrap: balance", section);
    }

    [Fact]
    public void Phase1_HasReducedMotionGate()
    {
        var section = ReadPhase1Section();

        Assert.Contains("@media (prefers-reduced-motion: reduce)", section);
    }

    [Fact]
    public void Phase1_DangerColor_BackgroundDeclaration()
    {
        var section = ReadPhase1Section();

        // Phase 18 (4a85e1e) refactored the literal danger color into the --danger token.
        // Assert both: the danger button binds to the token, and the token resolves to #dc2626.
        Assert.Matches(new Regex(@"^\s+background:\s*var\(--danger\)", RegexOptions.Multiline), section);
        Assert.Matches(new Regex(@"--danger:\s*#dc2626", RegexOptions.IgnoreCase), ReadAdminCss());
    }

    [Fact]
    public void Phase1_DangerHoverColor_BackgroundDeclaration()
    {
        var section = ReadPhase1Section();

        Assert.Matches(new Regex(@"^\s+background:\s*#b91c1c", RegexOptions.Multiline), section);
    }

    [Fact]
    public void Phase1_BackdropColor()
    {
        var section = ReadPhase1Section();

        Assert.Contains("rgba(15, 23, 42, 0.72)", section);
    }

    [Fact]
    public void Phase1_TouchTargetMinHeight()
    {
        var section = ReadPhase1Section();

        Assert.Contains("min-height: 44px", section);
    }

    [Fact]
    public void Phase1_TouchTargetMinWidth()
    {
        var section = ReadPhase1Section();

        Assert.Contains("min-width: 44px", section);
    }

    [Fact]
    public void Phase1_PanelMaxWidth()
    {
        var section = ReadPhase1Section();

        Assert.Contains("max-width: 480px", section);
    }

    [Fact]
    public void Phase1_FilterCascadeFixPresent()
    {
        var section = ReadPhase1Section();

        Assert.Contains("filter: none", section);
    }

    [Fact]
    public void Phase1_DangerExclusionOnConfirmHover()
    {
        var section = ReadPhase1Section();

        Assert.Contains(":not(.admin-modal__button--danger):hover", section);
    }

    private static string ReadPhase1Section()
    {
        var content = ReadAdminCss();
        var start = content.IndexOf(StartMarker, StringComparison.Ordinal);
        var end = content.IndexOf(EndMarker, StringComparison.Ordinal);

        Assert.True(start >= 0, "Phase 1 start marker was not found.");
        Assert.True(end > start, "Phase 1 end marker was not found after the start marker.");

        return content[(start + StartMarker.Length)..end];
    }

    private static string ReadAdminCss()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "wwwroot",
            "css",
            "admin-common.css"));
}
