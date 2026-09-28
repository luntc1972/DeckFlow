namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>One derived FLAGS-01 namespace filter chip.</summary>
/// <param name="Prefix">Lowercased first key segment followed by one dot, used by the chip filter.</param>
/// <param name="Label">Friendly chip label from <see cref="FlagFilterGroups.LabelFor"/> (D-13); raw namespace is <see cref="Prefix"/> without its trailing dot and appears in the chip title (D-14).</param>
/// <param name="FlagCount">Listed keys whose lowercased form starts with <paramref name="Prefix" />.</param>
public sealed record FlagFilterGroup(string Prefix, string Label, int FlagCount);
