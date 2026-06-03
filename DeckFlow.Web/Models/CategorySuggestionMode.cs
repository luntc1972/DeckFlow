namespace DeckFlow.Web.Models;

/// <summary>Suggestion sources that can contribute card category recommendations.</summary>
public enum CategorySuggestionMode
{
    /// <summary>Use only categories inferred from harvested local deck data.</summary>
    CachedData = 0,
    /// <summary>Use only categories from the supplied reference deck.</summary>
    ReferenceDeck = 1,
    /// <summary>Use only Scryfall Tagger category hints.</summary>
    ScryfallTagger = 2,
    /// <summary>Use every available category suggestion source.</summary>
    All = 3,
}
