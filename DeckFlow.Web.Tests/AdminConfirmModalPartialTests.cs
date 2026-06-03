using System.Text.RegularExpressions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// File-level regression tests for the admin confirmation modal partial DOM contract.
/// </summary>
public sealed class AdminConfirmModalPartialTests
{
    [Fact]
    public void Partial_HasDialogId_admin_confirm_modal()
    {
        var content = ReadPartial();

        Assert.Equal(1, CountOccurrences(content, "id=\"admin-confirm-modal\""));
    }

    [Fact]
    public void Partial_HasTitleId_admin_modal_title()
    {
        var content = ReadPartial();

        Assert.Equal(1, CountOccurrences(content, "id=\"admin-modal-title\""));
    }

    [Fact]
    public void Partial_HasMessageId_admin_modal_message()
    {
        var content = ReadPartial();

        Assert.Equal(1, CountOccurrences(content, "id=\"admin-modal-message\""));
    }

    [Fact]
    public void Partial_HasAriaLabelledby()
    {
        var content = ReadPartial();

        Assert.Contains("aria-labelledby=\"admin-modal-title\"", content);
    }

    [Fact]
    public void Partial_HasAriaDescribedby()
    {
        var content = ReadPartial();

        Assert.Contains("aria-describedby=\"admin-modal-message\"", content);
    }

    [Fact]
    public void Partial_UsesParagraphTitleNotH2()
    {
        var content = ReadPartial();

        Assert.Contains("<p id=\"admin-modal-title\"", content);
        Assert.DoesNotMatch(@"<h[1-6]\s+id=""admin-modal-title""", content);
    }

    [Fact]
    public void Partial_CancelButtonHasAutofocus()
    {
        var content = ReadPartial();

        Assert.Matches(
            @"<button\b(?=[^>\r\n]*\bdata-admin-modal-cancel\b)(?=[^>\r\n]*\bautofocus\b)[^>\r\n]*>",
            content);
    }

    [Fact]
    public void Partial_NoAntiForgeryToken()
    {
        var content = ReadPartial();

        Assert.DoesNotContain("@Html.AntiForgeryToken()", content);
    }

    [Fact]
    public void Partial_NoModel()
    {
        var content = ReadPartial();

        Assert.DoesNotContain("@model ", content);
    }

    [Fact]
    public void Partial_HasModalClassOnDialog()
    {
        var content = ReadPartial();

        Assert.Matches(@"<dialog\b[^>]*\bclass=""[^""]*\badmin-modal\b[^""]*""", content);
    }

    private static string ReadPartial()
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "DeckFlow.Web",
            "Views",
            "Shared",
            "_AdminConfirmModal.cshtml"));

    private static int CountOccurrences(string content, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = content.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
