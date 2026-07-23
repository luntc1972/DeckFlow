---
phase: 105-builder-compatible-export
plan: 03
status: complete
commit: add5ab19
---

# 105-03 Summary — CutLabExportComposer (Core, EXPORT-01/02/03)

**Built:** `CutLabExportComposer` (pure Core) + `CommanderIdentityCheck` producing, from
the finished working list and the OriginalEntries baseline:
- Both-dialect finished-list text (Moxfield + Archidekt) via the existing
  `FullImportExporter`/`DeltaExporter` with `targetSystem` branching.
- A CUT/ADD patch in both dialects via `DiffEngine.Compare`.
- A validation summary (exactly-100 count, color-identity legal/verified, banlist clean).

**Correctness rules implemented + tested (Codex plan-review catches):**
- Finished list board-normalizes kept sideboard/maybeboard cards to mainboard (else
  `FullImportExporter` drops them).
- CUT = `OnlyInArchidekt` + `CountMismatch` quantity decreases (else a `10→7 Forest`
  trim vanishes).
- Reconstructed entries consolidated (sum by key) so exported quantities sum to 100.

**Verification:** composer/exporter/diff 29/29 independently gated. Blind-verifier PASS.

**Deviations:** none.
