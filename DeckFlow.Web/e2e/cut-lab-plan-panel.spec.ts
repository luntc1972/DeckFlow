import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { getToolEnabled, setToolEnabled } from './support/admin-tools';
import { expandCutLabSection, expandMobileCollapsibles } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';

// Live smoke spec for Phase 8's Cut Lab plan panel (checkbox plan-profile selection), gated on
// 08-07's rendered panel and its /api/cut-lab/plan-apply round trip. This is the one artifact that
// proves the panel, the plan-affinity engine and the EDHREC commander-theme layer work together on
// a real page, at both the desktop and the mobile viewport.
//
// What this spec covers:
//   1. The plan panel renders after import, and the old free-text Primary/Secondary plan fields
//      are gone from the intake form.
//   2. Every generic strategy checkbox documents a definition line and a consequence line.
//   3. The commander theme section renders sorted by deck count with at most three pre-checked,
//      OR the "unavailable" message renders — both are a pass; a page error is not.
//   4. Clearing every checkbox surfaces the zero-selection no-op notice.
//   5. Checking a generic strategy the pool matches changes the proposed cut — the end-to-end
//      proof that a checkbox reaches the engine, exercised through the real
//      POST /api/cut-lab/plan-apply JSON contract (not a hidden-JSON rewrite).
//   6. A checked box survives a pool re-import (round trip).
//   7. Both viewports render without horizontal scroll and get a checkpoint screenshot.
//
// Run:
//   1. Start the app headless: scripts/run-web-test.sh (sets DECKFLOW_DISABLE_AUTO_BROWSER=true)
//   2. cd DeckFlow.Web && env -u DISPLAY npx --no-install playwright test e2e/cut-lab-plan-panel.spec.ts
//
// This file runs once per configured Playwright project (chromium-desktop 1280x900,
// chromium-mobile 390x844) — every test below therefore already exercises both viewports without
// any manual page.setViewportSize() call, the same convention bracket-smoke.spec.ts uses for its
// cross-viewport screenshot test.
//
// Admin creds: read from FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD env vars. A transient flag
// toggle is used for this run (reverted in afterEach), following the Cut Lab admin-lock convention
// the other Cut Lab specs use so this file never races them for the shared admin surface.
import { resolveE2EPort } from './support/e2e-port';

const baseUrl = `http://localhost:${resolveE2EPort()}`;
const screenshotDir = resolve(__dirname, '../../.planning/ui-design/cut-lab/screenshots');

// Why: the local category-knowledge.db (crowd-sourced Archidekt category tags backing the generic
// strategy role-proxy table) is frequently empty in a fresh checkout or CI runner — nothing has
// crawled it yet. Without at least one matching observation, checking a generic strategy box would
// never change any card's plan affinity, and the engine-effect test below would have nothing to
// observe. This seeds one deterministic observation directly into the SQLite file the running
// server reads, mirroring the existing content-kb-pending-hidden.spec.ts precedent for seeding a
// server-read SQLite artifact from Playwright rather than driving it through the UI.
const categoryDbPath = resolve(__dirname, '..', '..', 'artifacts', 'category-knowledge.db');

const engineEffectSourceId = 999_000_000;
const engineEffectStrategySlug = 'landfall';
// Command Tower is a 0-mana-value nonbasic land in the pool below with no plan signal by default;
// at Bracket 4 / Focused it is empirically the first Round 3 proposal (lowest mana value among the
// off-plan candidates), which makes it a reliable "moves once it becomes on-plan" test subject.
const engineEffectCardName = 'Command Tower';
// Why: DeckFlow.Core.Reporting.CategoryFilter.IsJunk treats ANY ASCII digit in a category label as
// crowd-sourced junk and drops it before it ever reaches the strategy matcher (verified empirically
// against the real read path — a label like "Landfall 1" silently resolves to zero categories). The
// label must therefore be letters-only. It is also seeded with INSERT OR IGNORE and never deleted —
// both chromium-desktop and chromium-mobile run this file concurrently against the same SQLite
// file, so a fixed idempotent fixture row (like the shared `cards` row below) avoids a delete race
// where one project's cleanup removes the row while the other project's test still needs it.
const engineEffectCategoryLabel = 'Landfall test fixture';

const oversizedPool = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '36 Plains',
  '36 Island',
  '20 Swamp',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Fellwar Stone',
  '1 Mystic Remora',
  '1 Rhystic Study',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  '1 Dovin\'s Veto',
  '1 Demonic Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let cutLabWasEnabled = false;

test.describe.configure({ mode: 'serial' });

function sqlite(sql: string): string {
  // `.timeout` (via -cmd) sets the busy timeout so a briefly-locked SQLite file (the running server
  // holding it mid-request) does not fail the seed outright. Mirrors content-kb-pending-hidden.spec.ts.
  return execFileSync('sqlite3', ['-cmd', '.timeout 8000', categoryDbPath, sql], {
    encoding: 'utf8',
  }).trim();
}

const q = (value: string): string => `'${value.replace(/'/g, "''")}'`;

const importPool = async (page: Page): Promise<void> => {
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();
  await expandCutLabSection(page, 'cut-lab-section-plan-panel');
  await expect(page.locator('[data-cut-lab-plan-panel]')).toBeVisible({ timeout: 30_000 });
};

const reimportPool = async (page: Page): Promise<void> => {
  await page.locator('details.cutlab-intake > summary').click();
  await page.getByRole('button', { name: 'Import pool' }).click();
  await expandCutLabSection(page, 'cut-lab-section-plan-panel');
  await expect(page.locator('[data-cut-lab-plan-panel]')).toBeVisible({ timeout: 30_000 });
};

// Every plan-panel checkbox lives outside the intake <form> (D-1's explicit-apply design) — each
// change fires its own round trip to /api/cut-lab/plan-apply, so wait for that response (not just
// the click) before reading anything the response is expected to have changed.
const togglePlanCheckbox = async (page: Page, checkbox: ReturnType<Page['locator']>): Promise<void> => {
  const applied = page.waitForResponse(r =>
    r.url().includes("/api/cut-lab/plan-apply") && r.request().method() === "POST");
  try {
    await checkbox.click();
    const response = await applied;
    expect(response.ok()).toBe(true);
  } finally {
    applied.catch(() => undefined);
  }
  await expect(checkbox).toBeEnabled();
};

const clearAllPlanCheckboxes = async (page: Page): Promise<void> => {
  const checkedBoxes = page.locator('[data-cut-lab-plan-panel] input[data-cut-lab-plan-checkbox]:checked');
  const maxIterations = 32;
  for (let i = 0; i < maxIterations; i += 1) {
    const remaining = await checkedBoxes.count();
    if (remaining === 0) {
      return;
    }
    // Resolve a stable, name+value-keyed locator before clicking: `checkedBoxes.first()` is
    // filtered by `:checked`, which re-evaluates on every action -- once the click below
    // unchecks the box, the same locator would stop matching it, and the disabled/enabled
    // assertions inside togglePlanCheckbox would fail with "element(s) not found". `value`
    // alone is not unique either -- a generic strategy slug and an EDHREC theme slug can
    // collide (e.g. both named "stax"), so key on both attributes.
    const target = checkedBoxes.first();
    const name = await target.getAttribute('name');
    const value = await target.getAttribute('value');
    const stableCheckbox = page.locator(`[data-cut-lab-plan-panel] input[data-cut-lab-plan-checkbox][name="${name}"][value="${value}"]`);
    await togglePlanCheckbox(page, stableCheckbox);
  }
  throw new Error('plan checkboxes did not clear within 32 toggles');
};

const readProposedCardName = async (page: Page): Promise<string> => {
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  const heading = page.locator('.cutlab-proposal__heading');
  await expect(heading).toBeVisible({ timeout: 30_000 });
  const text = await heading.textContent();
  return text?.replace(/^Proposed cut:\s*/, '').trim() ?? '';
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  cutLabWasEnabled = await getToolEnabled(page, 'Cut Lab');
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async ({ page }) => {
  try {
    // Restore the captured flag state so no persistent state leaks between test runs, matching
    // bracket-smoke.spec.ts's afterEach.
    await setToolEnabled(page, 'Cut Lab', cutLabWasEnabled);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('the plan panel renders after import and the free-text plan fields are gone', async ({ page }) => {
  await importPool(page);
  await expandMobileCollapsibles(page);

  await expect(page.locator('[data-cut-lab-plan-panel]')).toBeVisible();
  await expect(page.locator('input[name="PrimaryPlan"], textarea[name="PrimaryPlan"]')).toHaveCount(0);
  await expect(page.locator('input[name="SecondaryPlan"], textarea[name="SecondaryPlan"]')).toHaveCount(0);
});

test('every generic strategy checkbox documents a definition and a consequence', async ({ page }) => {
  await importPool(page);
  await expandMobileCollapsibles(page);

  const rows = page.locator('.cut-lab-plan-panel__strategies .cut-lab-plan-panel__row');
  const count = await rows.count();
  expect(count, 'the fixed twelve-entry generic strategy catalog should render').toBe(12);

  for (let index = 0; index < count; index += 1) {
    const row = rows.nth(index);
    const definition = row.locator('.cut-lab-plan-panel__row-definition');
    const consequence = row.locator('.cut-lab-plan-panel__row-consequence');
    await expect(definition).toBeVisible();
    await expect(consequence).toBeVisible();
    expect((await definition.textContent())?.trim(), `strategy row ${index} definition`).not.toBe('');
    expect((await consequence.textContent())?.trim(), `strategy row ${index} consequence`).not.toBe('');
  }
});

test('commander themes render sorted with at most three pre-checked, or the unavailable message renders', async ({
  page,
}, testInfo) => {
  await importPool(page);
  await expandMobileCollapsibles(page);

  const unavailable = page.locator('.cut-lab-plan-panel__themes-unavailable');

  if ((await unavailable.count()) > 0) {
    await expect(unavailable).toBeVisible();
    testInfo.annotations.push({ type: 'commander-theme-branch', description: 'unavailable' });
    expect(
      process.env.E2E_REQUIRE_EDHREC === '1',
      'EDHREC theme branch required but got the unavailable branch (E2E_REQUIRE_EDHREC=1)',
    ).toBe(false);
    return;
  }

  testInfo.annotations.push({ type: 'commander-theme-branch', description: 'populated' });

  const themeRows = page.locator('.cut-lab-plan-panel__themes .cut-lab-plan-panel__row');
  const themeCount = await themeRows.count();
  expect(themeCount, 'a populated theme branch should render at least one theme row').toBeGreaterThan(0);

  const deckCounts: number[] = [];
  for (let index = 0; index < themeCount; index += 1) {
    const definitionText = await themeRows.nth(index).locator('.cut-lab-plan-panel__row-definition').textContent();
    const match = definitionText?.match(/(\d+)\s+decks\s*\([\d.]+%\)\s+on EDHREC/);
    expect(match, `theme row ${index} should show a deck count`).not.toBeNull();
    deckCounts.push(Number(match![1]));
  }

  for (let index = 1; index < deckCounts.length; index += 1) {
    expect(deckCounts[index], 'theme rows must be sorted by non-increasing deck count').toBeLessThanOrEqual(
      deckCounts[index - 1],
    );
  }

  const checkedThemeCount = await page
    .locator('.cut-lab-plan-panel__themes input[name="PlanThemes"]:checked')
    .count();
  expect(checkedThemeCount, 'at most the top three themes pre-check').toBeLessThanOrEqual(3);
});

test('the zero-selection notice appears once every checkbox is cleared', async ({ page }) => {
  await importPool(page);
  await expandMobileCollapsibles(page);

  await clearAllPlanCheckboxes(page);

  // Why: re-import verifies the persisted zero-selection state after the immediate client-side
  // update performed by syncPlanPanel following the plan-apply response.
  await reimportPool(page);
  await expandMobileCollapsibles(page);

  await expect(page.locator('[data-cut-lab-plan-zero-notice]')).toBeVisible();
});

test('checking a generic strategy the pool matches changes the proposed cut', async ({ page }) => {
  test.skip(!existsSync(categoryDbPath), 'category-knowledge.db not present; server has not initialized it yet.');

  let observationSeeded = false;
  let observationSeedFailure: 'cli-unavailable' | 'seed-failed' | null = null;
  try {
    const normalizedCardName = engineEffectCardName.toLowerCase();
    sqlite(
      `INSERT OR IGNORE INTO cards (normalized_card_name, display_name) VALUES (${q(normalizedCardName)}, ${q(engineEffectCardName)});`,
    );
    const cardId = Number(sqlite(`SELECT id FROM cards WHERE normalized_card_name=${q(normalizedCardName)};`));
    expect(cardId, 'the seeded card row should resolve to an id').toBeGreaterThan(0);

    // INSERT OR IGNORE (never DELETE-cleaned): both chromium-desktop and chromium-mobile run this
    // file concurrently against the same SQLite file, and this row is an idempotent fixture keyed
    // by the unique (source_id, card_id, category, board) index — a second concurrent insert of the
    // identical tuple is a safe no-op, unlike a delete race that could pull the row out from under a
    // sibling project mid-test.
    sqlite(
      `INSERT OR IGNORE INTO card_category_observations
         (source_id, card_id, card_name, category, board, deck_count, count, last_seen_utc)
       VALUES
         (${engineEffectSourceId}, ${cardId}, ${q(engineEffectCardName)}, ${q(engineEffectCategoryLabel)}, 'mainboard', 1, 1, '2026-01-01T00:00:00Z');`,
    );
    observationSeeded = true;
  } catch (error: unknown) {
    console.warn('plan-panel category seed failed:', error);
    observationSeedFailure = (error as NodeJS.ErrnoException).code === 'ENOENT' ? 'cli-unavailable' : 'seed-failed';
    observationSeeded = false;
  }
  test.skip(
    !observationSeeded,
    observationSeedFailure === 'cli-unavailable'
      ? 'sqlite3 CLI unavailable; cannot seed a deterministic category match.'
      : 'sqlite3 category seed failed; see plan-panel category seed failed diagnostic.',
  );

  await importPool(page);
  await expandMobileCollapsibles(page);
  await clearAllPlanCheckboxes(page);

  const baselineCard = await readProposedCardName(page);
  expect(baselineCard, 'baseline proposed cut should resolve to a real card').not.toBe('');

  await expandCutLabSection(page, 'cut-lab-section-plan-panel');
  await expandMobileCollapsibles(page);
  const strategyCheckbox = page.locator(`input[name="PlanStrategies"][value="${engineEffectStrategySlug}"]`);
  await togglePlanCheckbox(page, strategyCheckbox);
  await expect(strategyCheckbox).toBeChecked();

  const afterCheckCard = await readProposedCardName(page);
  expect(afterCheckCard, 'checking a matching strategy must change the proposed cut').not.toBe(baselineCard);
});

test('a checked plan box survives a pool re-import', async ({ page }) => {
  await importPool(page);
  await expandMobileCollapsibles(page);

  const controlCheckbox = page.locator('input[name="PlanStrategies"][value="control"]');
  await togglePlanCheckbox(page, controlCheckbox);
  await expect(controlCheckbox).toBeChecked();

  await reimportPool(page);
  await expandMobileCollapsibles(page);

  await expect(page.locator('input[name="PlanStrategies"][value="control"]')).toBeChecked({ timeout: 30_000 });
});

test('captures a plan panel screenshot and enforces no horizontal scroll on mobile', async ({ page }) => {
  mkdirSync(screenshotDir, { recursive: true });

  await importPool(page);
  await expandMobileCollapsibles(page);
  await expect(page.locator('[data-cut-lab-plan-panel]')).toBeVisible();

  const projectName = test.info().project.name;
  const screenshotPath = join(screenshotDir, `cut-lab-plan-panel-${projectName}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });

  const noHorizontalScroll = await page.evaluate(
    () => document.documentElement.scrollWidth <= window.innerWidth + 1,
  );
  expect(noHorizontalScroll, `${projectName} viewport must not require horizontal scroll`).toBe(true);
});
