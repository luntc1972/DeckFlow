# DirectPush Stage 4 — UI Review

**Audited:** 2026-07-03
**Type:** Ad-hoc UI change (NOT a GSD phase)
**Scope:** New "Stage 4 — Commit Bodies to Git & Push (durability)" card + reworded TARGET: PRODUCTION danger banner + new success / no-op / error copy on the DeckFlow.Studio DirectPush page.
**Baseline:** The page's own established Stage 1/2/3 visual language (same file). Studio is a single-theme Bootstrap 5 admin tool — the guild-theme/mobile matrix for public DeckFlow.Web does NOT apply.
**Screenshots:** NOT captured — code-only audit. Stage 4 markup only renders after a real prod SCP + DB publish (Stages 1–3 must succeed against production), which cannot be exercised here. **Live desktop + mobile viewport verification of the Stage 4 card, the two success variants, and the three error variants is OWED.**

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Accurate, non-alarming, secret-safe, concrete recovery — but no-op vs committed phrasing diverges and the recovery command is emitted as prose |
| 2. Visuals | 4/4 | Card/header/badge/alert hierarchy matches Stages 1–3 exactly; icon is decorative + aria-hidden (watch: full 40-char SHA inline on mobile) |
| 3. Color | 4/4 | Semantic Bootstrap only; deliberate `btn-outline-primary` de-escalation correctly reserves red for the fatal stages; zero hardcoded colors |
| 4. Typography | 4/4 | No new sizes/weights; `h5`/`small`/`fw-semibold`/`<code>` reused; caps-for-emphasis matches Stage 3 |
| 5. Spacing | 3/4 | Card/alert/margin rhythm matches siblings, but banner icon uses a leading-space hack instead of the codebase-standard `me-1` |
| 6. Experience Design | 4/4 | Gating chain + hard-guard + non-fatal framing + five distinct states + explicit recovery command; button variant justified |

**Overall: 22/24**

---

## Top 3 Priority Fixes

1. **Danger-banner warning icon uses a whitespace hack for spacing** (WARNING) — `DirectPush.razor:94` renders `<span class="oi oi-warning" aria-hidden="true"></span>` immediately followed by `<strong> TARGET: PRODUCTION</strong>` (leading space inside `<strong>`). Every other open-iconic usage in the codebase (`Harvest.razor:360/395/426`, `Review.razor:184`) uses the `oi oi-warning me-1` margin utility. **Fix:** add `me-1` to the icon span and remove the leading space from the `<strong>` so gap comes from CSS, not markup whitespace.

2. **Push-failure recovery command is shown as un-selectable prose** (WARNING) — `DirectPush.razor.cs:383-385` builds `"... run: git push origin HEAD:refs/heads/{branch}"` and it renders inside the `alert-danger` block as plain text (`@_gitError`). Everywhere else on the page, commands/identifiers are wrapped in `<code>` (branch, SHA, `[skip render]`, `origin`). An operator recovering from a failed push must hand-retype the command, and it is visually indistinguishable from the surrounding sentence. **Fix:** split the message so the command renders in a `<code>` (or `<pre>`) element, or expose `ex.Branch` to the markup and template the command there with `<code>`.

3. **No-op success copy claims a push that should be verified, and diverges from the committed variant** (WARNING) — `DirectPush.razor:441-444` asserts *"Pushed `@_gitBranch` to `origin`"* for the `NothingToCommit` outcome. Confirm the coordinator actually pushes in the byte-identical-to-HEAD branch; if it can short-circuit before the push, the claim is false. Also unify phrasing with the committed variant (line 452 says `origin/@_gitBranch`, the no-op says "`@_gitBranch` to `origin`"), and confirm `@_gitSha` is a short SHA — a full 40-char SHA in inline `<code>` (line 452) risks awkward wrapping/overflow on the OWED mobile screenshot.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

Strong on every weighted concern; docked only for cosmetic/phrasing nits.

- **Accuracy — PASS.** The reworded danger banner (`razor:93-99`) correctly describes all four effects (live DB write, Render `/data` disk, git commit, push to `origin` with `[skip render]`) and ends with "There is no undo." No false "origin parity" claim anywhere. The committed-success copy scopes its claim precisely: "and pushed to `origin/@_gitBranch`. No production redeploy (`[skip render]`) — the content was already live" (`razor:449-454`).
- **Non-alarming where content is already live — PASS.** Every git-failure string leads with the reassurance that content is already LIVE and only the durability backup is affected: cancelled (`cs:366-367`), push-failed (`cs:383-385`), generic (`cs:397-399`). This matches the deliberate non-fatal framing.
- **Concrete next action — PASS.** The `DirectPushPushException` path gives the exact command (`cs:385`), and correctly branches null-SHA vs known-SHA (`cs:380-382`) so it never prints "committed as (none)".
- **Secret-leak safety — PASS (strong).** All operator-facing strings surface only the SHA and branch name. Remote URL / credentials stay in `InnerException` and go to the Serilog sink only (`cs:377`, `cs:392-396` comments + `Logger.LogError`). The banner and success copy never print the SCP host, remote URL, or connection string.
- **NIT (phrasing divergence).** No-op says "Pushed `@_gitBranch` to `origin`" (`razor:443-444`) while committed says "pushed to `origin/@_gitBranch`" (`razor:452`). Unify.
- **NIT (jargon / dropped qualifier).** The visible header "Stage 4 — Commit Bodies to Git & Push (durability)" (`razor:393`) drops the "no redeploy" qualifier that the section comment carries (`razor:390`). "(durability)" is systems jargon; acceptable in an operator tool but the parenthetical could read "(backup — no redeploy)".

### Pillar 2: Visuals (4/4)

- **Hierarchy — PASS.** Stage 4 reuses the exact card → `card-header` `h2.h5.fw-semibold.mb-0` → `card-body` structure of Stages 1–3 (`razor:391-395` vs `razor:299-303`). Focal point (the gated action button) is clear; badges/alerts carry status weight.
- **Icon — PASS.** The banner icon (`razor:94`) is `aria-hidden="true"` (decorative), correct because "TARGET: PRODUCTION" text already conveys the meaning. `oi oi-warning` is a real glyph (`\e0d8`) and open-iconic is loaded via `wwwroot/css/site.css:1`.
- **No unlabeled icon-only controls.** The action button has a text label in both idle and in-flight states; the in-flight spinner carries `role="status"` + `aria-label` + `visually-hidden` text (`razor:423-427`), matching Stages 1–3.
- **WATCH (OWED mobile verify).** `@_gitSha` renders in inline `<code>` (`razor:452`); if it is a full 40-char SHA it may wrap unattractively at 375px. Confirm on the owed mobile screenshot or truncate to a short SHA.

### Pillar 3: Color (4/4)

- **Semantic-only — PASS.** Stage 4 introduces zero hardcoded colors and zero inline `style` (the only inline styles on the page — `max-width:300px` — live in the pre-existing Stage 1/3 tables, not Stage 4).
- **Intentional de-escalation is correct.** The action button is `btn-outline-primary` (`razor:417`), deliberately less alarming than the `btn-danger` of Stages 2/3 (`razor:229`, `razor:317`) because content is already live — and it matches Stage 1's `btn-outline-primary` (`razor:113`). Red stays reserved for the two irreversible stages and the `alert-danger` prod banner (`razor:93`) / error blocks. This is a coherent 60/30/10 story: neutral chrome dominant, primary accent on the safe action, danger reserved.
- Success blocks use `alert-success` (`razor:440`, `razor:449`), spinner uses `text-primary` to sit on the outline-primary button (`razor:423`) — consistent with Stage 1's `text-primary` spinner (`razor:119`).

### Pillar 4: Typography (4/4)

- **No scale drift — PASS.** Stage 4 uses only sizes/weights already present: `h5 fw-semibold` header, `small` muted notes (`razor:397`, `razor:412`), `<code>` inline, `<strong>` emphasis. No `h1`–`h4` or arbitrary sizes introduced.
- **Emphasis device is consistent.** Caps-for-emphasis ("LIVE", "FAILED", "NOTHING" in `cs:383-385`, `cs:397-399`) mirrors Stage 3's "NOTHING was written" (`cs:297-298`) — an established in-file convention, not a new one.

### Pillar 5: Spacing (3/4)

- **Rhythm — PASS.** `card mt-3`, `alert ... py-2`, `p ... mb-2`, success `alert-success py-2 mt-3` all match the Stage 1–3 cadence exactly (`razor:391`, `razor:407`, `razor:397`, `razor:440`).
- **DEFECT (whitespace-for-spacing).** `razor:94` sets the icon→text gap with a leading space inside `<strong>` rather than the `me-1` margin utility used by every other `oi` icon in the app. This is both a consistency drift and a markup-whitespace anti-pattern (collapses unpredictably, not theme/DPI-stable). Small, but it is the one concrete spacing defect in an otherwise clean card. See Top Fix #1.

### Pillar 6: Experience Design (4/4)

- **Gating — PASS (defense in depth).** Stage 4 is triple-gated: `disabled="@(!_dbSuccess || _operationInFlight)"` on the button (`razor:419`), a `text-muted small` lock note when `!_dbSuccess` (`razor:410-415`, matching Stage 3's lock note `razor:310-315`), and a hard-guard in the handler `if (!_dbSuccess || _operationInFlight) return;` (`cs:328-331`) with an explanatory "disabled button alone is not sufficient" comment. There is even a test seam (`InvokeCommitAndPushForTest`, `cs:417`) proving the guard is exercised.
- **Non-fatal framing — PASS.** The stage repeatedly and correctly frames itself as durability-only: explanatory paragraph (`razor:397-403`), and all three failure strings assert content is already live (`cs:366`, `cs:383-385`, `cs:397-399`). The generic catch comment explicitly documents the non-fatal rationale (`cs:392-396`).
- **State coverage — PASS (five states).** Idle, in-flight (spinner + "Committing and pushing bodies..."), committed-success, no-op success (byte-identical to HEAD, distinct copy via `_gitNoOp`, `cs:356`), commit-landed-but-push-failed (`DirectPushPushException`, preserves SHA/branch, `cs:372-389`), null-SHA push-fail, cancelled, and sanitized generic failure. Ordering is correct: explanation → error (if any) → lock note (if locked) → button → success.
- **Button variant makes sense — PASS.** `btn-outline-primary` correctly signals "safe, reversible-ish backup" vs the `btn-danger` fatal stages.
- **NIT (recovery command as prose).** The one polish gap: the recovery command is not `<code>`-formatted (see Top Fix #2) — mildly error-prone to retype, but does not break the flow.
- **NIT (retry semantics).** After a push failure `_gitSuccess` stays false and the button remains enabled (`_dbSuccess` still true), so retry is available; a retry after a landed commit correctly falls into the `NothingToCommit` no-op path. Acceptable, no dead-end.

---

## Consistency vs Stages 1–3 (summary)

| Element | Stage 1–3 | Stage 4 | Match |
|---------|-----------|---------|-------|
| Card + header (`card mt-3` / `h2.h5.fw-semibold.mb-0`) | yes | yes | ✅ |
| Locked note (`p.text-muted.small`) | Stage 3 | yes | ✅ |
| Hard-guard in handler | Stage 3 (`WriteRowsAsync`) | yes (`CommitAndPushAsync`) | ✅ |
| Gated button `aria-label` | Stage 3 | yes | ✅ |
| In-flight spinner (`role`+`aria-label`+`visually-hidden`) | yes | yes | ✅ |
| `alert-danger py-2` error / `alert-success py-2 mt-3` result | yes | yes | ✅ |
| Button variant | `btn-danger` (S2/S3), `btn-outline-primary` (S1) | `btn-outline-primary` | ✅ (matches S1, intentional) |
| Icon spacing (`me-1`) | Harvest/Review pattern | leading-space hack | ⚠️ drift |
| Command shown in `<code>` | yes (branch/SHA/`origin`) | recovery command as prose | ⚠️ drift |

---

## Minor Recommendations (beyond Top 3)

- Header "(durability)" jargon / restore the "no redeploy" qualifier the section comment carries.
- OWED: capture live desktop (1440) + mobile (375) screenshots of the rendered Stage 4 card, both success variants, and all three error variants once a real prod publish can drive Stages 1–3.

---

## Files Audited

- `DeckFlow.Studio/Pages/DirectPush.razor` (markup — Stage 4 card `razor:390-458`, reworded banner `razor:92-99`, success/no-op copy)
- `DeckFlow.Studio/Pages/DirectPush.razor.cs` (`CommitAndPushAsync` `cs:322-404`, Stage 4 state fields `cs:78-85`, error/no-op strings)
- Cross-referenced: `DeckFlow.Studio/Pages/Harvest.razor`, `Review.razor`, `PullFromProd.razor`, `Shared/NavMenu.razor`, `wwwroot/css/site.css` (icon-library + `me-1` convention verification)
