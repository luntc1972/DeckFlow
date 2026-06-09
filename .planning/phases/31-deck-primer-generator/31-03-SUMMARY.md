# 31-03 SUMMARY — Deck Primer Packet Service

**Status:** COMPLETE (Codex impl / Claude review) — 2026-06-09
**Requirements:** PRM-05, PRM-06, PRM-07, PRM-08
**Wave:** 2 (`depends_on: ["31-01", "31-02"]`)

## What shipped

- **`IPrimerPromptVariant` + `PrimerPromptVariantRegistry`** — primer prompt contract and AI-platform registry mirrored from the analysis pattern, with nullable Spellbook passthrough, optional EDH Top 16 entries, and the shared `CategoryDistributionSummary`.
- **`DeckPrimerPacketService`** (new) — scoped orchestration core for primer packets. It duplicates the deck-loader path, resolves the commander/bracket, computes a packet cache key, assembles the grounded payload once, and renders **every enabled AI platform** into `PromptTextsByPlatform` (ChatGPT + Claude always, Gemini only when `GeminiEnabled`).
- **Combo grounding (D-2 / PRM-08)** — `BuildComboReferenceText` emits the two required structural blocks (`Known Combos` vs `Speculative Synergies`) plus a separate `Near-Combos` block capped at 15. Null Spellbook responses degrade to the explicit disclosure line instead of throwing.
- **Priority-rank branch selected** — `31-SPIKE.md` recorded `sufficient`, so the active branch ranks by immediacy text then piece count, while the fallback AI-rank/API-order branch is also present in code for the alternate verdict path.
- **Category distribution (PRM-07)** — commander category rows are counted into ramp/draw/tutor/interaction, with `removal` folded into interaction and the entire block omitted when no grounded rows exist.
- **Bracket-5 routing (PRM-06)** — `31-SPIKE.md` recorded `meta-query-available`, so the service calls the new **`IEdhTop16Client.GetTopArchetypesAsync`** only for `cEDH`, and passes null for brackets 1-4.
- **`EdhTop16Client.GetTopArchetypesAsync`** — added the exact spike-recorded meta-wide GraphQL query, response parser, count guard, and `_executeAsync` seam reuse.
- **Tests** — new focused coverage for the primer service seam and the top-archetype parser.

## Deviations

- **Spellbook ranking fields remain unavailable in 31-03.** The spike proved `manaValueNeeded` and `popularity` exist upstream, but the current `SpellbookCombo` scope fence blocked widening `CommanderSpellbookService`. The active ranking branch therefore uses only the fields exposed today (`Results`, `Instructions`, `CardNames.Count`) and documents that limitation inline for the 31-04 follow-up.
- **Top archetype names reuse `EdhTop16Entry`.** Within the 31-03 fence, the meta-wide query maps archetype name into `PlayerName` and `colorId` into `TournamentName`; this is documented in code for the variant step that consumes it.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -warnaserror:CS1591` → Build succeeded, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` → Build succeeded, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "DeckPrimerPacketServiceTests|EdhTop16ClientTopArchetypesTests"` → **11/11 passed, 0 failed**.
- Acceptance greps passed for `BuildComboReferenceText`, the D-2 disclosure/headings, `GetCategoryRowsForCommanderAsync`, `PromptTextsByPlatform`, `GeminiEnabled`, `GetTopArchetypesAsync`, ranking-branch strings, and duplicated `LoadDeckEntriesAsync`.

## Notes / next

- DI registration and the three concrete primer prompt variants remain 31-04 by design; this plan only shipped the contract, registry type, and shared data-assembly service.
- No other files were touched outside the allowed implementation fence, aside from this required summary artifact.
