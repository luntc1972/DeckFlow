namespace DeckFlow.Core.Content;

/// <summary>
/// Canonical feature-flag keys used by Content KB workflows across application hosts.
/// </summary>
public static class ContentKbFeatureFlagKeys
{
    /// <summary>
    /// Serves Content KB bodies exclusively from the git-shipped application tree.
    /// </summary>
    public const string DirectPushGitBody = "sync.directpush-gitbody";
}
