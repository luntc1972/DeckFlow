namespace DeckFlow.Web.Models;

/// <summary>Kinds of public feedback users can submit.</summary>
public enum FeedbackType
{
    /// <summary>Feedback reporting broken or incorrect behavior.</summary>
    Bug = 0,
    /// <summary>Feedback proposing a product improvement.</summary>
    Suggestion = 1,
    /// <summary>General comment that is not a bug report or suggestion.</summary>
    Comment = 2
}
