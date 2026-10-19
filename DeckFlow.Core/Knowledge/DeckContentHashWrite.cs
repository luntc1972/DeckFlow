namespace DeckFlow.Core.Knowledge;

/// <summary>Describes a queued deck hash update performed with its category replacement.</summary>
/// <param name="DeckId">Queued Archidekt deck ID.</param>
/// <param name="Hash">Canonical hash to store, or null to clear it.</param>
internal sealed record DeckContentHashWrite(string DeckId, string? Hash);
