namespace DeckFlow.Web.Services.CreatorStyle;

/// <summary>Lists public Moxfield deck summaries for an owner.</summary>
public interface IMoxfieldOwnerClient
{
    /// <summary>Lists public deck summaries for <paramref name="username"/>.</summary>
    Task<MoxfieldDeckListResult> ListDeckSummariesAsync(string username, CancellationToken cancellationToken = default);
}
