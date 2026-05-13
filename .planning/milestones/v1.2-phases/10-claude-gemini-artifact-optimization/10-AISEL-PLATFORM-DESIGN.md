# Design: AiPlatform Value Object — OCP Refactor for v1.3

**Author:** Phase 10 SOLID audit (Codex review 2026-05-09)
**Status:** DESIGNED, not implemented. Targeted for v1.3.
**Effort estimate:** Medium (~4-6 hours focused work + Codex review).

## Problem

After Phase 10 fanout, the per-AI dispatch pattern is **stringly typed and duplicated across 9+ touchpoints**:

1. `ChatGptDeckRequest.TargetAiPlatform` setter — switch on `"ChatGPT" or "Claude" or "Gemini"`
2. `ChatGptDeckComparisonRequest.TargetAiPlatform` setter — same switch
3. `ChatGptCedhMetaGapRequest.TargetAiPlatform` setter — same switch
4. `ChatGptDeckPacketService.BuildAnalysisPrompt` dispatcher — switch on string
5. `ChatGptDeckPacketService.BuildSetUpgradePrompt` dispatcher — switch on string
6. `ChatGptDeckComparisonService.BuildComparisonPrompt` dispatcher — switch on string
7. `ChatGptDeckComparisonService.BuildFollowUpPrompt` dispatcher — switch on string
8. `ChatGptCedhMetaGapService.BuildPrompt` dispatcher — switch on string
9. `_AiSelector.cshtml` Razor partial — three radio options hardcoded

Adding a 4th AI platform (e.g., "Mistral") requires editing **all 9 places** plus the response parser if Mistral has a different output convention. The compiler cannot help — every switch arm is a string literal.

Codex SOLID audit OCP score: **3/10**.

## Goal

Reduce OCP cost of adding a new AI platform from "edit 9 files" to "add one record entry + one dispatch test". Make the compiler enforce exhaustiveness so a missed dispatcher fails to build.

## Design

### Two layers — value object + per-builder strategy

**Layer 1: `AiPlatform` value object** — a sealed record with a static `All` registry.

```csharp
namespace DeckFlow.Web.Models;

/// <summary>
/// Single source of truth for the set of AI platforms DeckFlow supports.
/// Adding a new platform = adding one entry to <see cref="All"/>.
/// </summary>
public sealed record AiPlatform(string Key, string DisplayName, string Description)
{
    public static readonly AiPlatform ChatGpt = new(
        Key: "ChatGPT",
        DisplayName: "ChatGPT",
        Description: "OpenAI's GPT-family models — markdown-headed prompts with fenced JSON output.");

    public static readonly AiPlatform Claude = new(
        Key: "Claude",
        DisplayName: "Claude",
        Description: "Anthropic's Claude models — XML-tagged prompts with <result>-wrapped output.");

    public static readonly AiPlatform Gemini = new(
        Key: "Gemini",
        DisplayName: "Gemini",
        Description: "Google's Gemini models — markdown prompts with persona scaffold and schema-strictness language.");

    /// <summary>
    /// All recognised platforms in display order. Adding a new entry here
    /// is the SINGLE source of truth for the application's AI surface.
    /// </summary>
    public static readonly IReadOnlyList<AiPlatform> All = [ChatGpt, Claude, Gemini];

    /// <summary>
    /// Default platform when input is null, empty, or out-of-set. Keeps
    /// existing zero-config behaviour stable across all three request models.
    /// </summary>
    public static AiPlatform Default => ChatGpt;

    /// <summary>
    /// Normalise a string from form-post or zip request-context entry to a
    /// known platform. Out-of-set values fall back to <see cref="Default"/>.
    /// Used by request-model setters and zip-load deserialisation.
    /// </summary>
    public static AiPlatform Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Default;
        }

        foreach (var platform in All)
        {
            if (string.Equals(platform.Key, key, StringComparison.Ordinal))
            {
                return platform;
            }
        }

        return Default;
    }

    public override string ToString() => Key;
}
```

**Layer 2: per-builder strategy interfaces** — one interface per logical prompt-builder family, looked up from a registry keyed by `AiPlatform`.

```csharp
namespace DeckFlow.Web.Services.PromptBuilders;

internal interface IAnalysisPromptVariant
{
    AiPlatform Platform { get; }
    string Build(
        ChatGptDeckRequest request,
        string decklistText,
        string referenceText,
        string deckProfileSchemaJson,
        string? commanderName,
        IReadOnlyList<string> selectedQuestionIds,
        IReadOnlyList<string> bannedCards,
        CommanderSpellbookResult? comboResult,
        bool includeCardVersions);
}

internal sealed class ChatGptAnalysisPromptVariant : IAnalysisPromptVariant { ... }
internal sealed class ClaudeAnalysisPromptVariant : IAnalysisPromptVariant { ... }
internal sealed class GeminiAnalysisPromptVariant : IAnalysisPromptVariant { ... }

internal sealed class AnalysisPromptVariantRegistry
{
    private readonly IReadOnlyDictionary<AiPlatform, IAnalysisPromptVariant> _variants;

    public AnalysisPromptVariantRegistry(IEnumerable<IAnalysisPromptVariant> variants)
    {
        _variants = variants.ToDictionary(v => v.Platform);
    }

    public string Build(AiPlatform platform, /* same args as IAnalysisPromptVariant.Build */ ...)
        => _variants.TryGetValue(platform, out var variant)
            ? variant.Build(...)
            : _variants[AiPlatform.Default].Build(...);
}
```

Repeat the registry pattern for each of the five builder families:
`SetUpgradePromptVariantRegistry`, `ComparisonPromptVariantRegistry`,
`FollowUpPromptVariantRegistry`, `CedhMetaGapPromptVariantRegistry`.

**Wiring:** in `Program.cs`, register all variants + registries in the DI container so a new variant is just `services.AddSingleton<IAnalysisPromptVariant, MistralAnalysisPromptVariant>();`.

### Migration of existing code

1. **Request-model setters** become a one-liner each:

   ```csharp
   public string TargetAiPlatform
   {
       get => _targetAiPlatform;
       set => _targetAiPlatform = AiPlatform.Normalize(value).Key;
   }
   ```

   The string round-trip is preserved (form binding + zip serialization stay string-typed for compatibility), but the normalization invariant lives in one place. Setters drop from 8 lines × 3 files = 24 lines to 4 lines × 3 files = 12 lines, AND the source-of-truth for the accepted set moves from "buried in a switch expression" to "the `AiPlatform.All` registry".

2. **Service dispatchers** delegate to the registry:

   ```csharp
   internal static string BuildAnalysisPrompt(
       ChatGptDeckRequest request, /* args */)
   {
       var platform = AiPlatform.Normalize(request.TargetAiPlatform);
       return AnalysisPromptVariantRegistry.Default.Build(platform, /* args */);
   }
   ```

   Or, if injected via DI (preferable for testability):

   ```csharp
   public ChatGptDeckPacketService(
       AnalysisPromptVariantRegistry analysisPromptRegistry,
       SetUpgradePromptVariantRegistry setUpgradePromptRegistry,
       /* other deps */)
   { ... }
   ```

3. **Razor partial `_AiSelector.cshtml`** iterates `AiPlatform.All`:

   ```cshtml
   @foreach (var platform in AiPlatform.All)
   {
       <label class="ai-selector__option">
           <input type="radio"
                  name="TargetAiPlatform"
                  value="@platform.Key"
                  checked="@(Model == platform.Key ? "checked" : null)" />
           <span class="ai-selector__name">@platform.DisplayName</span>
       </label>
   }
   ```

   Adding a 4th platform now updates the UI automatically.

4. **`ChatGptRequestContextParser`** can stay string-typed (it's a forward-tolerant parser) but the loaders should call `AiPlatform.Normalize` before assigning back to the request, defensively.

### Test impact

- **Existing tests stay green** — string-typed `TargetAiPlatform` round-trips preserved; normalization invariant identical.
- **New tests** — the `AiPlatform.All` registry can be iterated in tests, replacing the `[Theory] [InlineData("ChatGPT")] [InlineData("Claude")] [InlineData("Gemini")]` blocks with `[MemberData(nameof(AllPlatforms))]` driven from `AiPlatform.All`. Adding a 4th platform automatically extends the test matrix.
- The `<result>`-contract tests landed in `ChatGptResultContractTests.cs` are exactly this pattern — refactor those theory data sources to read from `AiPlatform.All` once the value object exists.

### OCP score after refactor

**Adding a 4th AI ("Mistral"):**

1. Add one entry to `AiPlatform.All`: `public static readonly AiPlatform Mistral = new("Mistral", "Mistral", "Mistral models — ...");`
2. Implement five new variant classes (one per prompt-builder family) — concrete domain work, unavoidable
3. Register them in `Program.cs` DI

**Total touchpoints**: 1 + N variant implementations + 1 DI line. The dispatcher switches, the request-model setters, the Razor partial, the response parser, and the test matrices all auto-extend.

OCP score: **8/10** (the variant implementations themselves are unavoidable per-platform work; everything else extends without modification).

## Out of scope for this design

- Migrating `NormalizeSingleLine` and `CollapseWhitespace` to a shared text helpers class. Tracked separately as v1.3 follow-up.
- Splitting `ChatGptDeckPacketService` god-class. Already deferred per `.planning/PROJECT.md` (own refactor milestone).
- Splitting `ChatGptPacketArtifactStore` into per-workflow codecs. Recommended by Codex audit; out of scope for this design.
- Extracting generic stepped-form helpers from `deck-sync.ts`. Recommended; out of scope.

## Risks

- **DI graph complexity** — five new registries + ~15 new variant singletons. Modest increase but adds wiring code in `Program.cs`. Mitigation: convention-based registration via `services.Scan(...)` from `Scrutor` (already a transitive dep, or 1 NuGet add).
- **Backwards compatibility** — string-typed `TargetAiPlatform` on the request models stays as-is; only the normalization implementation changes. Saved zips with `target_ai_platform: ChatGPT` still round-trip identically. Tests should explicitly cover the boundary.
- **Test refactor scope** — existing `[InlineData("ChatGPT")] [InlineData("Claude")] [InlineData("Gemini")]` tests need migration to `[MemberData]` driven from `AiPlatform.All`. Mechanical but touches every Phase 10 test file.

## Acceptance criteria for the v1.3 plan

When this design is implemented, the following must hold:

1. `AiPlatform.All.Count == 3` after the refactor (no behavioural change in v1.3 cleanup pass).
2. Adding a hypothetical `AiPlatform.Test` to `AiPlatform.All` and a corresponding stub variant per builder family does NOT require editing any switch expression, request-model setter, Razor partial, or `ChatGptRequestContextParser`.
3. All 52 Phase 10 unit tests still pass.
4. New unit tests asserting the registry pattern for each builder family — i.e., `Registry.Build(platform, ...)` returns the variant whose `Platform` property equals the input platform, and falls back to default for unknown platforms.
5. Zero changes to the user-facing string contract — saved zips and form posts continue to use `"ChatGPT"`, `"Claude"`, `"Gemini"`.
6. Build clean. Codex code review passes (PASS or PASS-with-nits, no NEEDS-FIX).

## Forward path

This design is captured for v1.3. After v1.2 ships, the v1.3 milestone roadmap should include a phase titled "OCP refactor: AiPlatform value object + per-builder strategy registries" with this document as the design input.

If a 4th AI platform request lands BEFORE v1.3, this refactor jumps to the head of the queue — implementing the new platform without this refactor would require touching 9+ files vs ~7 with it. The cost-benefit flips at the second fanout.

---
*Design author: Phase 10 SOLID audit (Codex review 2026-05-09)*
*v1.3 candidate plan*
