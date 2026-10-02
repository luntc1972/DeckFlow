namespace DeckFlow.Web.Services.CommanderCategoryNorms;

/// <summary>Loads harvested norms; returns null when unavailable, below floor, or failed, and throws only for caller cancellation.</summary>
public interface ICommanderCategoryNormsProvider
{
    /// <summary>Gets norms for the stable harvest key.</summary>
    Task<CommanderCategoryNormsResult?> GetNormsAsync(string harvestKey, CancellationToken cancellationToken = default);
}
