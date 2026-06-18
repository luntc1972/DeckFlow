using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the AiPlatform prompt-builder strategy registries
/// (Phase 15-02). One extension call wires all six prompt-variant families (Analysis,
/// SetUpgrade, Comparison, FollowUp, MetaGap, Primer) — ChatGpt/Claude/Gemini variants
/// plus their per-family registries.
/// </summary>
public static class PromptVariantServiceCollectionExtensions
{
    /// <summary>
    /// Registers the six prompt-variant families and their registries as singletons.
    /// Each family has three platform-specific implementations (ChatGpt, Claude, Gemini)
    /// and a registry that selects the correct variant at runtime.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowPromptVariants(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // AiPlatform prompt-builder strategy registries (Phase 15-02)
        services.AddSingleton<IAnalysisPromptVariant, ChatGptAnalysisPromptVariant>();
        services.AddSingleton<IAnalysisPromptVariant, ClaudeAnalysisPromptVariant>();
        services.AddSingleton<IAnalysisPromptVariant, GeminiAnalysisPromptVariant>();
        services.AddSingleton<AnalysisPromptVariantRegistry>();
        services.AddSingleton<ISetUpgradePromptVariant, ChatGptSetUpgradePromptVariant>();
        services.AddSingleton<ISetUpgradePromptVariant, ClaudeSetUpgradePromptVariant>();
        services.AddSingleton<ISetUpgradePromptVariant, GeminiSetUpgradePromptVariant>();
        services.AddSingleton<SetUpgradePromptVariantRegistry>();
        services.AddSingleton<IComparisonPromptVariant, ChatGptComparisonPromptVariant>();
        services.AddSingleton<IComparisonPromptVariant, ClaudeComparisonPromptVariant>();
        services.AddSingleton<IComparisonPromptVariant, GeminiComparisonPromptVariant>();
        services.AddSingleton<ComparisonPromptVariantRegistry>();
        services.AddSingleton<IFollowUpPromptVariant, ChatGptFollowUpPromptVariant>();
        services.AddSingleton<IFollowUpPromptVariant, ClaudeFollowUpPromptVariant>();
        services.AddSingleton<IFollowUpPromptVariant, GeminiFollowUpPromptVariant>();
        services.AddSingleton<FollowUpPromptVariantRegistry>();
        services.AddSingleton<IMetaGapPromptVariant, ChatGptMetaGapPromptVariant>();
        services.AddSingleton<IMetaGapPromptVariant, ClaudeMetaGapPromptVariant>();
        services.AddSingleton<IMetaGapPromptVariant, GeminiMetaGapPromptVariant>();
        services.AddSingleton<MetaGapPromptVariantRegistry>();
        services.AddSingleton<IPrimerPromptVariant, ChatGptPrimerPromptVariant>();
        services.AddSingleton<IPrimerPromptVariant, ClaudePrimerPromptVariant>();
        services.AddSingleton<IPrimerPromptVariant, GeminiPrimerPromptVariant>();
        services.AddSingleton<PrimerPromptVariantRegistry>();

        return services;
    }
}
