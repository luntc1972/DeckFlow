namespace DeckFlow.Web.Services.PromptBuilders.Bracket;

/// <summary>Joins card names without making commas inside names ambiguous.</summary>
public static class BracketCardNameList
{
    private const string Separator = "; ";

    /// <summary>Joins card names; card names can contain commas.</summary>
    public static string Join(IEnumerable<string> cardNames)
    {
        ArgumentNullException.ThrowIfNull(cardNames);
        return string.Join(Separator, cardNames);
    }
}
