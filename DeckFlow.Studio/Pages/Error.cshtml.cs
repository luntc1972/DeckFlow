using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeckFlow.Studio.Pages;

/// <summary>Serves the uncached error page and exposes a trace identifier for support diagnostics.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    /// <summary>Gets or sets the activity or HTTP trace identifier displayed on the error page.</summary>
    public string? RequestId { get; set; }

    /// <summary>Indicates whether the error page should render a request identifier.</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;

    /// <summary>Creates the error page model with its diagnostic logger.</summary>
    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    /// <summary>Captures the current activity or HTTP trace identifier for the error page.</summary>
    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
