# Phase 13 Discussion Log

**Mode:** `--auto` (workflow.auto_advance=true; chain flag active from Phase 12 completion)
**Gathered:** 2026-05-17
**Areas auto-selected:** all (per `discuss-phase/modes/auto.md`)

## Auto-selected gray areas

Identified via codebase scout (26 ChatGpt-prefixed class definitions, 142 hits in DeckController.cs, 6 services, 11 model files, 3 DeckPageTab enum values) + cross-reference with CLASSRENAME-01..03 + AIPLATFORM-01 spec text.

| Area | Question | Auto-selected option (recommended default) |
|------|----------|--------------------------------------------|
| Naming convention | What prefix replaces "ChatGpt" on the rename triplet? | Page-aligned: `DeckAnalysis*`, `DeckComparison*`, `MetaGap*` — matches Phase 12 URL slugs AND explicit target names in CLASSRENAME-01 + AIPLATFORM-01 spec |
| Naming convention | Rename shared static helper `ChatGptPacketArtifactStore` to what? | Bare `PacketArtifactStore` (no page prefix) — serves all three pages, matches existing static-helper precedent in CONVENTIONS.md |
| Naming convention | Rename `DeckPageTab` enum values? | Yes — `DeckPageTab.DeckAnalysis`, `DeckPageTab.DeckComparison`, `DeckPageTab.CedhMetaGap` — keep enum aligned with URL slugs |
| Doc-comment scope | Generate stub summaries or hand-craft per class? | Hand-craft anchored to class responsibility — match terse single-sentence style from existing ScryfallCardLookupService / CommanderSpellbookService doc-comments |
| Doc-comment scope | Remove `NoWarn 1591;1573;1587` from csproj after this phase? | No — keep suppression; this phase only guarantees renamed types compile clean, not whole assembly. Removing the suppression is gated on every untouched type also gaining docs. |
| Rename execution | Single-wave or multi-wave grouping? | Four waves (Models → Services → Controller+Views → Tests+final build gate) — keeps intermediate build state contained per wave and ordering matches type dependency graph |
| Rename execution | Worktree-parallel inside waves? | Sequential — every wave overlaps DeckController.cs + Program.cs; parallel worktrees would race on merge |
| Rename execution | Use `git mv` for file renames? | Yes — preserves git blame + follow history per CLAUDE.md commit hygiene |
| String preservation | What stays as `"ChatGPT"` / `chatgpt-*`? | AI platform Key constant + targetAiPlatform property + form field name + Phase 10 artifact filename fallback + narrative doc-comment + visible UI prose — full list in CONTEXT.md D-07 |
| String preservation | Sweep internal HTML/JS/CSS identifiers (data-cache-key, class names) in this phase? | No — TS/CSS coupling requires its own test pass; defer to dedicated hygiene phase (Phase 16 candidate) |
| Verification | Run `dotnet test` in WSL? | No — CLAUDE.md "VSTest unreliable in WSL"; rely on `dotnet build` clean + manual T1–T8 round-trip from MILESTONE-AUDIT.md as the verification gate |
| Verification | Add new automated tests in this phase? | No — pure rename + doc-comment add. Behavior change is zero by definition. Manual round-trip catches behavior drift. |

## Deferred during discussion

- AIPLATFORM-01 / AIPLATFORM-02 value object refactor → Phase 15
- DeckController god-class split → own refactor milestone
- JS/TS/CSS internal identifier sweep → Phase 16 candidate
- Removing `NoWarn 1591;1573;1587` from csproj → blocked on whole-codebase doc backfill
- Refactor-shaped surprises uncovered during rename (e.g., prompt builder extraction) → AUDIT-01 / Phase 14

## Claude's discretion noted

- Helper-method name detail (per-case removal of internal "ChatGpt" mentions) — decide during execution
- File rename order within a wave — alphabetical OK
- Whether to add new interfaces for currently-interface-less services — DO NOT add in this phase (rename only)

## Single-pass auto-mode cap

Per `discuss-phase/modes/auto.md` "CRITICAL — Auto-mode pass cap": this CONTEXT.md is the single authoritative pass output. No re-read for "gaps" — proceeding directly to commit + chain.
