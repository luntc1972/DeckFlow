namespace DeckFlow.Web.Models.Admin;

/// <summary>
/// View model for the /Admin/YoutubeExport form: channel input, listing depth, and an
/// error banner for failed lookups.
/// </summary>
public sealed class AdminYoutubeExportViewModel
{
    /// <summary>YouTube channel handle, URL, id, or slug as entered by the operator.</summary>
    public string Channel { get; init; } = string.Empty;

    /// <summary>Maximum number of most-recent uploads to include in the export.</summary>
    public int Limit { get; init; } = 100;

    /// <summary>User-facing error message when the lookup or export failed.</summary>
    public string? ErrorMessage { get; init; }
}
