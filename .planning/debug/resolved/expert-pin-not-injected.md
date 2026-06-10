---
slug: expert-pin-not-injected
status: resolved
trigger: "SEL-02 expert-pin not injected into analysis prompt — pinned video missing from analysis.txt Expert Context block"
created: 2026-06-09
updated: 2026-06-09
goal: find_root_cause_only
fix_commit: a106c6a (refactor bfe16b1)
resolution: fixed
---

# Debug Session: expert-pin-not-injected

## Symptoms

- **Expected:** User pins a curated video in the Expert Context selection UI → that pinned video appears as a tier-1 clip in the deck-analysis prompt's Expert Context block (analysis.txt), ahead of auto-relevance clips.
- **Actual:** Pinned video is absent from analysis.txt. Auto-relevance, follow-creator, and evergreen tiers work; the manual pin specifically does not survive into the merged clip set / prompt.
- **Errors:** None thrown — silent drop (pin id does not match any published row, so it yields no clip).
- **Timeline:** Open since 2026-06-08 (Phase 32 UAT). Never fixed. Re-surfaced by v1.5 milestone integration check 2026-06-09.
- **Reproduction:** On a deck-analysis flow with `content.kb.enabled` ON, pin a video via the "What Experts Say" / selection UI, generate the analysis packet, inspect analysis.txt Expert Context block — pinned clip missing.

## Lead (from milestone audit + prior memory `followup_expert_pin_not_in_prompt`)

Suspected pin-ID shape mismatch between generation (client) and matching (server):
- **Match side:** `ContentKbRelevanceService.GetPinId(row) => row.YoutubeVideoId ?? row.RssGuid` (`ContentKbRelevanceService.cs:599`); used in `GetMergedClipsAsync` tier-1 (`:234-245`) and `ResolvePinTitlesAsync` (`:299`).
- **Generate side:** client pins via `button.dataset.videoId` (`kb-selection.ts:207`); the `data-video-id` attribute is emitted in the Razor selection panel — need to confirm what value it carries (raw YouTube id vs a 3-level composite id incl. source/channel/Id).
- Prior memory note: "pin-id generate(3-level incl Id) vs match(2-level GetPinId/ResolvePinTitles) mismatch."

## Current Focus

- hypothesis: The `data-video-id` the client submits is not the same string shape as `row.YoutubeVideoId`, so tier-1 pin matching in `GetMergedClipsAsync` finds no row and the pin is silently dropped.
- next_action: Trace the exact value emitted into `data-video-id` in the Razor panel and compare to `ContentSiteIndexRow.YoutubeVideoId` shape; confirm where the divergence is and which end is canonical.

## Evidence

- timestamp 2026-06-09: `GetPinId(row) => row.YoutubeVideoId ?? row.RssGuid` confirmed at ContentKbRelevanceService.cs:599 (grep).
- timestamp 2026-06-09: client pin id source = `button.dataset.videoId` at kb-selection.ts:207 (grep).
- timestamp 2026-06-09: `pinIds = selection.PinnedVideoIds.Distinct(Ordinal).Take(3)` at ContentKbRelevanceService.cs:234 — exact-Ordinal match, no normalization.

## Eliminated

- hypothesis: "Pin id 3-level (YoutubeVideoId ?? RssGuid ?? Id) generate vs 2-level match mismatch" — **REFUTED**. Read-path hydration (`ContentSiteIndexStore.cs:473-480`) sets `natural_key_type` ∈ {Youtube, Podcast} (else throws), so on every published row EXACTLY ONE of YoutubeVideoId/RssGuid is non-null. Therefore `YoutubeVideoId ?? RssGuid` is always non-null and the `?? Id.ToString()` fallback in `ContentKbSearchApiController.cs:55` + `ContentKbController.cs:62` is DEAD CODE. 2-level match always equals 3-level generate for real rows. Latent smell, not the live cause. (This is what memory 3915/4100 + two audit agents fixated on — incorrectly.)
- hypothesis: "localStorage pins never reach the form submit" — **REFUTED**. `kb-selection.ts:382-391` `injectHiddenInputs` removes + re-injects `PinnedVideoIds` hidden inputs, called unconditionally at the end of `renderAnalysis` (`:465-467`), so stored pins are submitted.

## Resolution (DIAGNOSED — fix delegated to Codex)

**Root cause:** Pinned rows are gated through `ParseRowsAsync(..., includeFailedRowsAsZeroScore: false)` at `ContentKbRelevanceService.cs:231`, which runs BEFORE tier-1 pin selection (`:239-247`). `ParseRowsAsync` (`:408-434`) catches per-artifact parse/read failures, logs `"Skipping Content KB artifact {ArtifactPath} after parse/read failure."` (`:430`), and — when the flag is false — does NOT add the row to `parsedRows` (the `if (includeFailedRowsAsZeroScore)` add at `:432-434` is skipped). Result: a pinned video whose artifact is missing/unreadable/unparseable is silently excluded from the candidate set, so tier-1 finds no row to pin even though the id matches. An explicit pin is a user override and should survive a thin/failed artifact.

**Empirical confirmation step (do before fixing):** grep prod logs for `Skipping Content KB artifact` around the failing pin's ArtifactPath — confirms the pinned video's artifact failed to parse.

**Minimal fix recommendation (Codex to implement, do not implement here):** In `GetMergedClipsAsync`, parse with `includeFailedRowsAsZeroScore: true` (`:231`) so failed rows remain in `parsedRows` (empty ScoreInput, score 0) and stay eligible for the EXPLICIT-selection tiers (tier-1 pin, tier-2 follow, tier-4 evergreen), while tier-3 auto (`row.Score >= MinSelectionScore`, `:269`) still excludes zero-score rows — auto-relevance unchanged. Verify `CreateClipsForArtifacts` produces a usable clip (title + attribution + deep-link) from a failed/empty-ScoreInput row; if the excerpt would be empty, add a minimal title-only clip for pinned rows so the pin is honored visibly.

**Failing-test spec (Codex writes first, confirm RED, then fix → GREEN):**
- Project: `DeckFlow.Web.Tests`; Class: `ContentKbMergedClipsTests` (alongside existing tier tests).
- Arrange: `FakeContentSiteIndexStore` returns one published YouTube row `R` with `YoutubeVideoId = "VID1"` whose artifact FAILS to parse/read (point `ArtifactPath` at a missing/garbage file, or inject a throwing artifact reader so `ParseRowsAsync` hits the `catch` at `:430`). `content.kb.enabled` flag ON. `ExpertSelection { PinnedVideoIds = ["VID1"], FollowedCreators = [] }`.
- Act: `GetMergedClipsAsync(selection, commander, bracket, archetypes, maxRenderedChars)`.
- Assert (expected post-fix): result is non-null and contains a tier-1 clip (`ClipOrigin == "pinned"`) for `VID1`.
- Current (RED): result is null / does not contain VID1 (row dropped by ParseRowsAsync before tier-1).
- Note for Codex: also add a sibling test proving tier-3 AUTO still EXCLUDES a parse-failed row (score 0 < MinSelectionScore) so the flag flip doesn't leak failed rows into auto-relevance.

status → diagnosed (root cause confirmed; fix not applied per Claude-investigates / Codex-fixes rule).
