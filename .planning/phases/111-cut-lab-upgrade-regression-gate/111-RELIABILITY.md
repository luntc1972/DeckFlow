# 111 — UI-Test Reliability Analysis & Hardening

Folded into Phase 111 at user request ("why are the UI tests so unreliable — determine why and fix").
Distinguishes genuine flake (nondeterministic) from deterministic-but-wrong tests, and records
what was fixed vs already-mitigated vs residual.

## Key distinction

The failure that triggered this analysis was **not** flake. The CLUP-19 theme spec failed
identically every run because it drove focus with a programmatic `.focus()` then asserted
`:focus-visible`. Chromium only engages `:focus-visible` under **keyboard** modality, so a
programmatic focus on a native `<select>` never matches it. Deterministic → wrong test, not
unreliable. Fixed by pressing `Tab` (keyboard modality) before the programmatic focus.

## Flake taxonomy (this repo's e2e)

| # | Category | Nature | Status after 111 |
|---|----------|--------|------------------|
| 1 | Admin-console contention (shared tool-flag state, N workers) | genuine flake | **Already mitigated** — every admin-dependent spec holds a global `/tmp/deckflow-admin-e2e.lock` for the FULL test (acquire beforeEach → release afterEach) + a synthetic per-test `X-Forwarded-For` IP for rate-limit isolation. Full-test mutual exclusion prevents cross-spec on/off races. No change needed. |
| 2 | Server cross-kill / stale Windows listener | genuine flake | **FIXED** — `scripts/run-web-test.sh` no longer blindly runs `fuser -k 5173`; it curl-probes `:5173` (works across the WSL↔Windows boundary, unlike `ss`/`fuser`) and REUSES a healthy server instead of killing a sibling's. `FORCE_RESTART=1` forces a fresh start. Playwright's `webServer.reuseExistingServer` (WSL-detected) already owns lifecycle, so no manual pre-start / no cross-kill. |
| 3 | WSL↔Windows seams (dotnet.exe vs dotnet, VSTest-in-WSL, DISPLAY/WAYLAND, WSLENV) | environmental friction | Understood/documented; not a spec defect. xUnit run via Windows `dotnet.exe`; e2e run with `env -u DISPLAY -u WAYLAND_DISPLAY`. |
| 4 | Decide-sim starvation under local many-worker parallelism | genuine flake (load) | **Mitigated + documented** — the decide-heavy cut-lab specs (`decide`/`tuning`/`whatif`/`theme-readability`) run a CPU-heavy `/api/cut-lab/decide` loop. Running the full cut-lab e2e set on many local workers starves them; observed as a 30s `Import pool → "Lock your pool"` render timeout. CI already pins `workers: 1` + `retries: 1` (config comment). The same run passes green in isolation (theme-readability 24 themes × 2 viewports, ~24s). **Local guidance:** run the decide-heavy cut-lab specs with a bounded worker count (e.g. `--workers=2`) or run `cut-lab-theme-readability.spec.ts` in its own pass. CI is the authoritative gate. |

## CLUP-19 a11y findings surfaced by the corrected gate (all FIXED)

Once the focus-visible methodology was correct, the all-theme spec did its job and caught real
WCAG defects:

| Finding | Themes | Fix |
|---------|--------|-----|
| Filled accept button focus ring ≈ its own fill (contrast 1.55) | all (systemic) | `.cutlab-decision-btn--accept:focus-visible { outline-color: var(--on-accent); outline-offset: -4px; }` — ring uses the button's text colour, guaranteed to contrast the fill. |
| Checked package-toggle focus ring on accent fill | all | `input[data-cut-lab-package-toggle]:checked:focus-visible { outline-color: var(--on-accent); outline-offset: -2px; }` |
| Accept-button text contrast < 3.0 (white on pale accent) | esper 2.78, abzan 2.84, golgari 2.88 | `--accent` darkened minimally (hue-preserving) to clear ≥3.1: esper `#5f9fe3→#5995d5`, abzan `#86a35a→#7f9b56`, golgari `#6ba83a→#67a138`. |
| Focus ring too dim vs dark element bg (WCAG 1.4.11) | dimir, grixis, jund, rakdos, sultai — select trigger / plan input / Lock All pill / role chip | Focus outline switched to `var(--ink)` (theme body-text colour; contrasts every surface, ≥ the accent it replaced so no regression on the 18 passing themes). `.df-select__trigger` fixed app-wide (the dim ring is not Cut Lab-specific). |

Result: `cut-lab-theme-readability.spec.ts` passes all 24 themes × 2 viewports.

## Residual (Cycle-20 candidate)

True per-worker tool-flag isolation (so admin-dependent specs need not globally serialize) would
let the full e2e suite run fully parallel without the `/tmp` lock. Not needed for correctness —
the current lock is reliable — but it would speed local full-suite runs. Captured as a backlog
candidate, not fixed here.
