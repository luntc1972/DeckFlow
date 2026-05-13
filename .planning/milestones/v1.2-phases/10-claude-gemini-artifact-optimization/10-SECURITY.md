# Phase 10 Security Audit — Claude/Gemini Artifact Optimization

**Phase:** 10 — claude-gemini-artifact-optimization
**ASVS Level:** 1
**Audited:** 2026-05-09
**Threats Closed:** 12 / 12
**Block-on:** high (none triggered)

## Summary

All 12 declared threats in the Phase 10 STRIDE register verified. Three threats with `mitigate` disposition (T-10-07, T-10-08, T-10-12) have concrete code-level evidence; nine `accept` threats have rationales consistent with implementation.

## Threat Verification

| Threat ID | Category | Disposition | Evidence |
|-----------|----------|-------------|----------|
| T-10-01 | Spoofing | accept | `ChatGptDeckRequest.cs:105-113` — switch-expression setter normalizes any value not in `{ChatGPT, Claude, Gemini}` to `"ChatGPT"`. Dispatch site `ChatGptDeckPacketService.cs:841-846` has explicit arms only for `"Claude"` / `"Gemini"`; default arm routes to `BuildAnalysisPromptChatGpt` (safe). |
| T-10-02 | Information Disclosure | accept | No new data sources introduced. Per-AI variants reuse the same upstream data (decklist, reference text, schema JSON, banned cards, combo result) as the ChatGPT body — verified in `ChatGptDeckPacketService.cs:1060+` (Claude variant) and `:1272+` (Gemini variant). Server-build, zip-download, user-paste workflow unchanged. |
| T-10-04 | Tampering (within prompt) | accept | `NormalizeSingleLine` defined at `ChatGptDeckPacketService.cs:2430-2431` — collapses whitespace and falls back to default for null/blank. Used at lines 1115/1118/1122/1126 (Claude variant) and 1310/1322/1327/1332 (Gemini variant) for `format`/`deck_name`/`strategy_notes`/`meta_notes`. Card names sourced from authoritative parsers; MTG comprehensive rules disallow `<`/`>`. |
| T-10-05 | Spoofing | accept | `ChatGptDeckComparisonRequest.cs:66-74` and `ChatGptCedhMetaGapRequest.cs:48-56` both have identical normalizing setters constraining `TargetAiPlatform` to the three accepted values; the `targetAiPlatform` parameter on Comparison + CedhMetaGap dispatchers is sourced from these properties only (callers in `BuildAsync`). |
| T-10-06 | Tampering (within prompt) | accept | Same `NormalizeSingleLine` helper applied across Comparison and CedhMetaGap builders (verified by reuse pattern documented in 10-02 SUMMARY: helper colocated per-service or shared with packet service). Decklist text rendered via existing helpers; no new path bypasses it. |
| T-10-07 | Tampering | **mitigate** | `ChatGptJsonTextFormatterService.cs:16-18` — `ResultTagRegex` literal: `@"<result>\s*(.*?)\s*</result>"` with `RegexOptions.Compiled \| RegexOptions.Singleline`. Non-greedy `.*?` quantifier. Match invocation at line 32; first matching pair wins, bounded by required `</result>` close tag. |
| T-10-08 | Denial of Service | **mitigate** | Same regex at `ChatGptJsonTextFormatterService.cs:16-18` — `RegexOptions.Compiled` set; lazy quantifier prevents pathological backtracking; close-tag requirement bounds the match. ASP.NET default 30 MB request-body cap unchanged. |
| T-10-09 | Tampering | accept | `ChatGptPacketArtifactStore.cs:274-281` (Comparison load) and `:309-316` (CedhMetaGap load) — `parsed.TargetAiPlatform` written directly to `request.TargetAiPlatform`; the request setter (verified above for all three models) constrains the value back into `{ChatGPT, Claude, Gemini}`. Stale/crafted entries cannot escape that set. Only `target_ai_platform` field is applied (other parser fields ignored on load per code path). |
| T-10-10 | Information Disclosure | accept | `01-request-context.txt` entries written via `BuildRequestContextText` writers (Comparison + CedhMetaGap services) emit only form-state strings the user supplied. Same data classification as existing zip contents shipped in Phase 9 Packets path. No PII added. |
| T-10-11 | Denial of Service (UX) | accept | `deck-sync.ts:763` — `CHATGPT_DOWNLOAD_DEBOUNCE_MS = 3000` constant (unchanged from pre-Phase-10). Standard `window.setTimeout` at line 774. Failure mode requires browser-engine-level setTimeout failure. |
| T-10-12 | Tampering (state) | **mitigate** | Three layers of evidence: (1) `deck-sync.ts:788` — module-scope `WeakMap<HTMLFormElement, number>` named `skipPersistenceTimers`. (2) `deck-sync.ts:2419-2422` — cancel-on-new-upload: `priorTimer = skipPersistenceTimers.get(form); if (priorTimer !== undefined) { window.clearTimeout(priorTimer); }`. (3) `deck-sync.ts:2423-2429` — guarded auto-clear: `if (form.dataset.skipPersistence === 'true') { delete form.dataset.skipPersistence; }` before `skipPersistenceTimers.delete(form)`. Race-condition fix per commit e4ca510 confirmed. |

## Threat Flags / Unregistered Surface

None. SUMMARY.md `## Threat Flags` sections (across 10-01..10-04) are absent or carry no entries that fall outside the 12-threat register. Implementation strictly within the planned attack surface (per-AI prompt content + zip request-context envelope + response regex shim + browser timer cleanup). No unmapped new attack surface detected.

## Notes

- T-10-09 mitigation chain is the strongest of the `accept` group: even though disposition is `accept`, the setter normalization on all three request models effectively closes the threat by construction. Worth flagging to a future auditor as defense-in-depth.
- `<system>` / `<human>` / `<assistant>` tag exclusion (D-04) is part of plan content but not a STRIDE threat; not in scope for this audit.
- Comparison and CedhMetaGap `LoadFromZip` retain "response file required" throws (RESEARCH.md Pitfall 3) — no partial-zip semantics relaxation.

---
*Phase 10 verified. No blockers. v1.2 ship-readiness gate: clear from security perspective; integration test checklist (10-03 SUMMARY) remains the human-verify gate.*
