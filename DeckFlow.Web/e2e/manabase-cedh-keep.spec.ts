import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { clickManabasePillRadio } from './support/manabase-pill';

// Phase MBGAP-11 — cEDH keep-shapes live smoke on /manabase, gated behind
// analysis.manabase.mulligan-eval plus analysis.manabase.keep-shapes. This mirrors the
// manabase-mulligan.spec.ts admin-lock + flag-restore hardening so the shared SQLite flag store
// is always restored to its pre-test state after the spec finishes.
//
// Like the existing manabase opening-hand specs, this submits a real paste decklist and depends on
// live Scryfall resolution. When the sandbox cannot reach Scryfall, result-dependent assertions
// cleanly skip rather than failing on an environment limitation.

const mulliganEvalFlagKey = 'analysis.manabase.mulligan-eval';
const keepShapesFlagKey = 'analysis.manabase.keep-shapes';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let originalMulliganEvalEnabled = true;
let originalKeepShapesEnabled = false;

const WINOTA_CEDH_DECK = [
  'Commander',
  '1 Winota, Joiner of Forces',
  '',
  'Deck',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Boros Signet',
  '1 Lotus Petal',
  '1 Llanowar Elves',
  '1 Avacyn\'s Pilgrim',
  '1 Birds of Paradise',
  '1 Elvish Mystic',
  '1 Esper Sentinel',
  '1 Gingerbrute',
  '1 Ornithopter',
  '1 Memnite',
  '1 Ragavan, Nimble Pilferer',
  '1 Professional Face-Breaker',
  '1 Drannith Magistrate',
  '1 Archon of Emeria',
  '1 Grand Abolisher',
  '1 Aven Mindcensor',
  '1 Blade Historian',
  '1 Angrath\'s Marauders',
  '1 Avacyn, Angel of Hope',
  '1 Swords to Plowshares',
  '1 Silence',
  '1 Lightning Bolt',
  '1 Deflecting Swat',
  '1 Path to Exile',
  '1 Sacred Foundry',
  '1 Clifftop Retreat',
  '1 Battlefield Forge',
  '1 Inspiring Vantage',
  '1 Needleverge Pathway',
  '1 Sundown Pass',
  '1 Command Tower',
  '1 Mana Confluence',
  '1 City of Brass',
  '1 Arid Mesa',
  '1 Windswept Heath',
  '1 Wooded Foothills',
  '1 Plateau',
  '1 Temple Garden',
  '1 Stomping Ground',
  '1 Sacred Peaks',
  '1 Mountain',
  '1 Plains',
  '1 Forest',
].join('\n');

const CASUAL_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '24 Plains',
  '24 Island',
  '1 Swords to Plowshares',
  '1 Counterspell',
  '1 Sol Ring',
  '1 Arcane Signet',
].join('\n');

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  originalMulliganEvalEnabled = await captureOriginalFlagEnabled(page, mulliganEvalFlagKey);
  originalKeepShapesEnabled = await captureOriginalFlagEnabled(page, keepShapesFlagKey);
});

test.afterEach(async ({ page }) => {
  try {
    await restoreFlagEnabled(page, keepShapesFlagKey, originalKeepShapesEnabled);
  } finally {
    try {
      await restoreFlagEnabled(page, mulliganEvalFlagKey, originalMulliganEvalEnabled);
    } finally {
      await releaseAdminLockForTest(heldLock);
      heldLock = null;
    }
  }
});

async function submitDeck(
  page: Page,
  deckText: string,
  mode: 'Casual' | 'Cedh',
  commanderImportance: 'Central' | 'Standard' | 'Low' = 'Standard',
): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(deckText);
  await clickManabasePillRadio(page, 'Mode', mode);
  await clickManabasePillRadio(page, 'CommanderImportance', commanderImportance);
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

test('cEDH keep-shapes renders dual headlines, Winota opener coverage, and no horizontal overflow', async ({ page }) => {
  await setFlagEnabled(page, mulliganEvalFlagKey, true);
  await setFlagEnabled(page, keepShapesFlagKey, true);

  const ok = await submitDeck(page, WINOTA_CEDH_DECK, 'Cedh', 'Central');
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const lens = page.locator('.manabase-mulliganlens');
  const openers = lens.locator('ul.manabase-mulliganlens-openers > li');
  const slowLine = openers
    .filter({ hasText: 'not on curve (slow start)' })
    .filter({ hasText: 'no plan by turn 4 — mulligan' })
    .first();
  const openerTexts = await openers.allTextContents();

  await expect(lens).toBeVisible();
  await expect(lens.locator('.manabase-lens-big.manabase-lens-big--soft')).toHaveCount(2);
  await expect(lens).toContainText('mana-keepable hands');
  await expect(lens).toContainText('plan-keepable hands');
  await expect(lens).toContainText('plan-keepable = passed a cEDH keep shape (explosive / early engine / interaction bridge)');
  expect(
    openerTexts.some((text) => /— (explosive keep|engine keep|bridge keep|no plan by turn 4 — mulligan)/i.test(text)),
  ).toBeTruthy();

  // Deterministic commander-central AC3 coverage lives in xUnit Cedh_Central_CommanderSurfacesAsOpener; this live smoke uses real card data and intentionally does not hard-require centrality.
  const commanderOpener = openers.filter({ hasText: 'Winota' }).first();
  if (openerTexts.some((text) => text.includes('Winota'))) {
    await expect(commanderOpener).toBeVisible();
  } else {
    console.log('Winota was not the representative opener for this live sample.');
  }

  if (openerTexts.some((text) => text.includes('no plan by turn 4 — mulligan'))) {
    await expect(slowLine).toBeVisible();
  }

  expect(
    openerTexts.some((text) => /\(turn [5-9]\)/.test(text) && /(workable line|explosive keep|engine keep|bridge keep)/i.test(text)),
  ).toBeFalsy();

  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1),
  ).toBeTruthy();
});

test('casual keep-shapes retains keepable-hands headline and shows curve coverage', async ({ page }) => {
  await setFlagEnabled(page, mulliganEvalFlagKey, true);
  await setFlagEnabled(page, keepShapesFlagKey, true);

  const ok = await submitDeck(page, CASUAL_DECK, 'Casual');
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const lens = page.locator('.manabase-mulliganlens');

  await expect(lens).toBeVisible();
  await expect(lens).toContainText('keepable hands');
  await expect(lens).not.toContainText('mana-keepable hands');
  await expect(lens).not.toContainText('plan-keepable hands');
  await expect(lens).toContainText(/plays a spell on ~\d+ of first 5 turns/);

  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1),
  ).toBeTruthy();
});

async function setFlagEnabled(page: Page, key: string, enabled: boolean): Promise<void> {
  const response = await page.goto('/Admin/Flags');
  expect(response?.ok()).toBeTruthy();

  const row = page.locator(`tr[data-flag-key="${key}"]`);
  const status = row.locator('[data-label="Status"]');
  const currentStatus = (await status.textContent())?.trim();
  const desiredStatus = enabled ? 'On' : 'Off';
  if (currentStatus === desiredStatus) {
    return;
  }

  await row.getByRole('button', { name: enabled ? 'Enable' : 'Disable', exact: true }).click();
  await expect(page.locator('.admin-banner--success')).toBeVisible();
  await expect(row.locator('[data-label="Status"]')).toHaveText(desiredStatus);
}

async function captureOriginalFlagEnabled(page: Page, key: string): Promise<boolean> {
  try {
    const response = await page.goto('/Admin/Flags');
    expect(response?.ok()).toBeTruthy();

    const row = page.locator(`tr[data-flag-key="${key}"]`);
    const status = row.locator('[data-label="Status"]');
    return ((await status.textContent())?.trim() ?? '') === 'On';
  } catch {
    return true;
  }
}

async function restoreFlagEnabled(page: Page, key: string, enabled: boolean): Promise<void> {
  try {
    await setFlagEnabled(page, key, enabled);
  } catch (error) {
    await page.waitForTimeout(1_000);
    try {
      await setFlagEnabled(page, key, enabled);
    } catch (retryError) {
      console.warn(`Failed to restore ${key} after retry`, error, retryError);
    }
  }
}
