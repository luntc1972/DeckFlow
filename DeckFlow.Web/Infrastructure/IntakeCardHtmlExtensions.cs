using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace DeckFlow.Web.Infrastructure;

/// <summary>Provides Razor helpers for rendering intake card boundaries.</summary>
public static class IntakeCardHtmlExtensions
{
    /// <summary>Writes an intake card opening tag and returns its disposable closing scope.</summary>
    public static IDisposable BeginIntakeCard(this IHtmlHelper html, IntakeCardOptions options)
    {
        html.ViewContext.Writer.Write(IntakeCardMarkup.Open(options, HtmlEncoder.Default));
        return new IntakeCardScope(html.ViewContext.Writer, IntakeCardMarkup.Close(options.HasResult));
    }
}
