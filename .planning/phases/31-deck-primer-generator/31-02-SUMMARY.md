# 31-02 SUMMARY — Deck Primer Foundation Models

**Status:** COMPLETE (Codex impl / Claude review) — 2026-06-09
**Requirements:** PRM-03, PRM-04, PRM-09, PRM-10, PRM-11, PRM-12 (model-side contracts)
**Wave:** 1 (independent — `depends_on: []`; does NOT consume the 31-01 spike)

## What shipped

Typed foundation contracts every downstream Phase 31 plan consumes:

- **`PacketArtifactStore.PrimerAllowedNames`** — allowlist HashSet (`OrdinalIgnoreCase`) added immediately after `CedhAllowedNames`, before any public method, with the 8 primer entry names. Allowlist ONLY; `BuildPrimerZip`/`LoadPrimerFromZip` are 31-05 (Pitfall 2: `ReadEntries` throws on unlisted names, so the allowlist must exist first).
- **`PrimerSectionCatalog`** (new) — `PrimerSectionEntry` + `PrimerSectionGroup` records + static catalog: exactly 31 sections across 5 groups (Identity / Combos / Gameplay / Matchups / Maintenance), each with HelpText (PRM-12). Gates: 2 cEDH-only (`cedh-meta-macro-matchups` #24, `stack-wars-and-fast-mana` #25), 1 casual-only (`battlecruiser-politics-and-social-pacing` #26). `GetPresetForBracket` + bracket-aware `NormalizeSelections` (validate / dedup / order + gate-strip).
- **`DeckPrimerRequest`** (new) — sealed CLASS, mutable null-guard setters; `TargetAiPlatform` setter routes through `AiPlatform.Normalize(value).Key` (Phase 10 hardening); `SelectedSectionIds` null-guards to `[]`.
- **`DeckPrimerViewModel`** (new) — sealed `{ get; init; }` class; `ActiveTab` defaults to `DeckPageTab.DeckPrimer`.
- **`DeckPageTab.DeckPrimer`** — added.

## Deviation (plan bug, fixed in review)

- Plan said `DeckPrimer = 12`; its interface audit was stale. The live `DeckPageTab.cs` already had **`ContentKb = 12`** (added in later KB work), so `= 12` created a duplicate enum value. **Corrected to `DeckPrimer = 13`** (next free). No consumer hardcodes the numeric value (names only), so safe. Acceptance-criterion grep `DeckPrimer = 12` is superseded by `= 13`.

## Verification

- `dotnet build DeckFlow.Web -warnaserror:CS1591` → Build succeeded (CS1591-clean).
- `dotnet build DeckFlow.Web.Tests` → succeeded (incl. the in-flight spike harness `Spike001KbValueAbHarness.cs`, untouched).
- `dotnet test --filter "PrimerSectionCatalogTests|DeckPrimerRequestTests"` → **11/11 passed, 0 failed**.
- Test run scoped to the two new classes — the spike's experimental tests were NOT run (user's in-flight gsd-spike work).

## Notes / next

- No service/variant/zip/UI behavior here — those are 31-03 (service, **blocked on 31-01 spike verdicts**), 31-04 (variants), 31-05 (zip), 31-06 (controller/view/TS).
- Concurrency: committed while a gsd-spike session was paused (user testing). STATE.md updated minimally to avoid clobber.
