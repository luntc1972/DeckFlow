namespace DeckFlow.Studio;

/// <summary>
/// Resolved at startup; indicates whether the wired LLM distillation backend is a
/// subscription provider (claude-CLI, $0 marginal cost) or a metered provider (OpenAI).
/// </summary>
public sealed record StudioDistillConfig(bool IsSubscriptionProvider);
