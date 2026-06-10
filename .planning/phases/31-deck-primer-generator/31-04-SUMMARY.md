# 31-04 SUMMARY — Primer Prompt Variants + DI Wiring

**Status:** COMPLETE (Codex impl / Claude review pending) — 2026-06-09
**Requirements:** PRM-05, PRM-06, PRM-08, PRM-09
**Wave:** 3 (`depends_on: ["31-01", "31-03"]`)

## What shipped

- **`ChatGptPrimerPromptVariant`** — new markdown-headed primer variant for ChatGPT with deck context, matchup routing, the D-2 combo-grounding block, numbered section directives, decklist payload, and fenced-markdown output instructions.
- **`ClaudePrimerPromptVariant`** — new XML-tagged primer variant for Claude using the required `<deck_primer>` / `<primer_output>` structure, with the same grounded payload rendered in Claude-specific framing and no `<result>` wrapper.
- **`GeminiPrimerPromptVariant`** — new Gemini-specific primer variant with the required persona scaffold, "Think carefully through the problem before responding." instruction, the D-2 combo-grounding block, and a **32,000-character** `DefensivePromptCharCap`.
- **Gemini D-4 trim guard** — the Gemini variant applies section-level trimming by **character count** (`builder.Length`), records omitted sections, and appends the required disclosure line. The known-combos ground-truth block remains outside the trim path.
- **PRM-06 matchup routing per variant** — bracket 5 uses named EDH Top 16 archetypes when `top16Entries` is present; bracket 5 with null entries and brackets 1-4 all reuse the same five generic matchup buckets (Aggro / Control / Midrange / Combo / Stax-Hate) inside each variant file.
- **Program.cs DI wiring** — added all three `IPrimerPromptVariant` registrations, `PrimerPromptVariantRegistry`, and the scoped `DeckPrimerPacketService` factory with `IOptions<AiPlatformOptions>` and optional logger resolution, without reordering the pre-existing registrations.
- **Primer prompt tests** — new `PrimerPromptVariantTests` directly instantiate the three internal variants and cover D-2 combo separation, null Spellbook disclosure, bracket-5 named archetypes, bracket-5 generic-bucket degradation, non-cEDH generic buckets, Gemini trimming, and the absence of trimming in ChatGPT/Claude.

## Deviations

- **No deviations from the scope fence.** The implementation stayed within the five allowed code files plus this required summary artifact.
- **Gemini trim tests use an oversized synthetic prompt, not byte counting.** This matches D-4's required `string.Length` character guard and the spike's bytes-vs-chars note.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -warnaserror:CS1591` → Build succeeded, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` → Build succeeded, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "PrimerPromptVariantTests"` → **17/17 passed, 0 failed**.
- Acceptance greps passed for `DefensivePromptCharCap = 32000`, the Gemini omit-disclosure line, `builder.Length` cap checks, the three `AddSingleton<IPrimerPromptVariant>` registrations, `PrimerPromptVariantRegistry`, `AddScoped<IDeckPrimerPacketService>`, and `IOptions<AiPlatformOptions>`.

## Notes / next

- The enabled-platform gate remains in `DeckPrimerPacketService.BuildAsync` from 31-03 by design; the new variant files do not check `DECKFLOW_GEMINI_ENABLED`.
- Cross-variant prose duplication is intentional and load-bearing per ADR 0001; no shared constants, base class, or shared prose helper were introduced.
