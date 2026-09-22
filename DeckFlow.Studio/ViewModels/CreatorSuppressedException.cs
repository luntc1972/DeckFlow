namespace DeckFlow.Studio.ViewModels;

/// <summary>Thrown when an approval would include a suppressed creator.</summary>
public sealed class CreatorSuppressedException : InvalidOperationException
{
    /// <summary>Creates the exception with a safe operator-facing message.</summary>
    public CreatorSuppressedException()
        : base("A suppressed creator cannot be approved.")
    {
    }
}
