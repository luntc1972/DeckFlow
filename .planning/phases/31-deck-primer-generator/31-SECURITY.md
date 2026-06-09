---
phase: 31
slug: deck-primer-generator
status: verified
threats_open: 0
asvs_level: 2
created: 2026-06-09
---

# Phase 31 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| browser form → POST /deck-primer(/download/upload) | Untrusted deck text/URL + bracket + platform + section ids; antiforgery + SameOrigin enforced; normalized server-side | Decklist text, deck URL, AI platform key, bracket, section ids |
| uploaded zip → LoadPrimerFromZip | Untrusted re-uploaded archive (path traversal, zip bomb, unlisted entries); enforced by ReadEntries allowlist + size caps | Zip archive entries |
| localStorage section ids → form submit | Client-stored ids re-validated by NormalizeSelections server-side; rendered values HTML-encoded by Razor | Per-bracket section id selections |
| decklist / upstream JSON (Spellbook/EdhTop16) → AI prompt | Untrusted card + third-party combo/meta text rendered into prompt body as literal data, never instructions | Card data, combo text, meta archetypes |
| service → AI prompt | Assembled server-side; combo ground-truth block structurally fenced from the speculative ask (D-2) | Final prompt string (no execution/eval) |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-31-01 | Information Disclosure | Spike probe accidentally committed to public repo | mitigate | Only `31-SPIKE.md` planning doc committed; throwaway harness never committed; public repo clean | closed |
| T-31-02 | Tampering | Stale captured fixture treated as authoritative | accept | RESEARCH valid-until 2026-07-08; verdicts re-checkable; no fixture in production code | closed |
| T-31-03 | Tampering | Crafted TargetAiPlatform string posted | mitigate | `DeckPrimerRequest.cs:99` setter routes through `AiPlatform.Normalize(value).Key`; unknown collapses to Default (`AiPlatform.cs:60-76`) | closed |
| T-31-04 | Tampering | Crafted/cross-bracket SelectedSectionIds posted | mitigate | `PrimerSectionCatalog.NormalizeSelections` (line 199) validates against `AllSections` + `IsSectionAvailableForBracket`; strips unknown + bracket-gated ids server-side | closed |
| T-31-05 | Tampering | Primer zip upload with unlisted entry name | mitigate | `PacketArtifactStore.cs:84-94` `PrimerAllowedNames` (8 exact entries); `ReadEntries` enforces at line 621 | closed |
| T-31-06 | Denial of Service | Null list/string assignment → NRE | mitigate | All setters null-guard (`DeckPrimerRequest.cs:27,36,48,71,80,89,108` — `value ?? string.Empty / "Commander" / []`) | closed |
| T-31-07 | Tampering | Prompt-injection via decklist/deck name/upstream combo text | mitigate | User + upstream text emitted as literal fenced data; `## Known Combos (ground truth — do not speculate)` (`DeckPrimerPacketService.cs:444`) structurally separated from speculative ask (D-2) | closed |
| T-31-08 | Denial of Service | Pathological combo volume bloating prompt | mitigate | `MaxNearCombos = 15` via `.Take` (`DeckPrimerPacketService.cs:69,499`); section count bounded by 31-entry catalog; Gemini cap trims | closed |
| T-31-09 | Information Disclosure | Upstream (EdhTop16/Spellbook) failure surfacing stack trace | mitigate | `FindCombosAsync` null contract; Top16 + category fetch wrapped in try/catch → null/degraded (`DeckPrimerPacketService.cs:280-305`) | closed |
| T-31-10 | Spoofing | Crafted commander name → unexpected category rows | mitigate | `CategoryKnowledgeRepository.cs:309` parameterized read (`AddParameter @commanderName`) via dialect abstraction; empty result omits block | closed |
| T-31-11 | Tampering | Injection in combo/archetype data masquerading as speculative directive (per variant) | mitigate | D-2 preserved in every variant; `## Speculative Synergies (you propose)` (`DeckPrimerPacketService.cs:491-493`) distinct from fenced ground-truth block | closed |
| T-31-12 | Denial of Service | Oversized primer overflowing Gemini paste limit | mitigate | `GeminiPrimerPromptVariant.cs:13,81-103` `DefensivePromptCharCap = 32000`; `AppendIfFits` trims low-priority sections with disclosure (char-count) | closed |
| T-31-13 | Tampering | Cross-variant prose consolidation altering all three outputs | mitigate | ADR-0001: each variant `: IPrimerPromptVariant` directly, no shared base class / prose const (`ChatGpt/Claude/GeminiPrimerPromptVariant.cs:10`) | closed |
| T-31-14 | Tampering | Zip path traversal on re-uploaded primer | mitigate | `PacketArtifactStore.cs:799-801` `ReadEntries` throws on `/` or `\` in `entry.FullName`; no custom archive parsing | closed |
| T-31-15 | Denial of Service | Zip bomb / oversized primer upload | mitigate | `PacketArtifactStore.cs:14-15` `MaxEntryUncompressedBytes = 2 MB` + `MaxTotalUncompressedBytes = 10 MB`, enforced lines 809-815 | closed |
| T-31-16 | Tampering | Spoofed cross-workflow zip loaded as primer | mitigate | `PacketArtifactStore.cs:621` `LoadPrimerFromZip` → `ReadEntries(PrimerAllowedNames)` sole allowlist; non-primer names throw | closed |
| T-31-17 | Tampering | Restored SelectedSectionIds with crafted/cross-bracket ids | mitigate | Restored ids re-run through `NormalizeSelections` after zip load (`DeckPrimerPacketService.cs:203,245`); zip not a trust bypass | closed |
| T-31-18 | Tampering | CSRF on POST /deck-primer(/download/upload) | mitigate | `[ValidateAntiForgeryToken]` on all three primer POSTs (`DeckController.cs:535,592,659`) + SameOriginRequestValidator; no new exemption | closed |
| T-31-19 | Tampering | XSS via section labels/help text, restored deck name, preset data attributes | mitigate | Razor default HTML encoding on dynamic text; `data-preset-ids="@JsonSerializer.Serialize(...)"` catalog ids in quoted attr, no `Html.Raw` (`DeckPrimer.cshtml:166`); prior Html.Raw bug fixed in `9fd1c65`, confirmed absent | closed |
| T-31-20 | Tampering | localStorage-injected/cross-bracket section ids submitted | mitigate | `primer-selection.ts:138-168` `enforceBracketGating()` client-side AND `NormalizeSelections` server-side; localStorage not a trust boundary | closed |
| T-31-21 | Tampering | Crafted zip uploaded to /deck-primer/upload | mitigate | `DeckController.cs:693` `LoadPrimerFromZip` → `ReadEntries(PrimerAllowedNames)`: allowlist + traversal + bomb checks | closed |
| T-31-22 | Information Disclosure | Service exception surfacing stack trace on page | mitigate | Controller catches `InvalidOperationException` → friendly message; catch `Exception` → log + generic copy (`DeckController.cs:555-584,624-651,707-755`); no raw exception to view | closed |
| T-31-SC | Tampering | npm/pip/cargo installs | accept | Zero package additions: no new `<PackageReference>` in any `.csproj`, empty `package.json` diff `main..HEAD` | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-31-01 | T-31-02 | Spike fixtures are operator-run probes with a documented valid-until (2026-07-08); verdicts re-checkable, no fixture checked into production code | luntc1972 | 2026-06-09 |
| AR-31-02 | T-31-SC | No new packages introduced in Phase 31; supply-chain surface unchanged. TS compiles via existing MSBuild target | luntc1972 | 2026-06-09 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-09 | 23 | 23 | 0 | gsd-security-auditor (sonnet) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-09
