namespace DeckFlow.Web.Controllers.Admin;

/// <summary>
/// Input model for the <c>/Admin/CreatorProfile</c> crawl-and-measure form.
/// </summary>
public class AdminCreatorProfileInputModel
{
    /// <summary>Creator slug persisted to the source store.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Platform username used for crawl routing.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Platform identifier; currently <c>archidekt</c> or <c>moxfield</c>.</summary>
    public string Platform { get; init; } = "archidekt";

    /// <summary>When true, bypasses the creator crawl freshness window and forces a re-crawl.</summary>
    public bool ForceRefresh { get; init; }
}
