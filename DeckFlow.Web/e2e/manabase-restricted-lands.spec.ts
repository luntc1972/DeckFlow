import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

// MBGAP-01 D-05 disclosure smoke: runs under both Playwright viewport projects
// (chromium-desktop + chromium-mobile). Uses the Admin/Flags toggle seam shared by the other
// manabase flag specs, and guards the result-dependent assertions with test.skip when Scryfall is
// unreachable in the environment.

const restrictedLandsFlagKey = 'analysis.manabase.restricted-lands';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let originalEnabled = true;

const PASTE_DECK = [
  'Commander',
  '1 Tymna the Weaver',
  '',
  'Deck',
  '18 Plains',
  '18 Island',
  '1 Cavern of Souls',
  '1 Ancient Ziggurat',
  '1 Nykthos, Shrine to Nyx',
  '1 Llanowar Elves',
  '1 Elvish Mystic',
  '1 Counterspell',
].join('\n');

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  // Restore this flag to its pre-test state to avoid cross-spec contamination (incident 2026-07-14).
  originalEnabled = await captureOriginalFlagEnabled(page, restrictedLandsFlagKey);
});

test.afterEach(async ({ page }) => {
  try {
    await restoreFlagEnabled(page, restrictedLandsFlagKey, originalEnabled);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

async function submitDeck(page: Page): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(PASTE_DECK);
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

test('restricted-land disclosure marker, footnote, and panel entry render when flag is ON', async ({ page }) => {
  await setFlagEnabled(page, restrictedLandsFlagKey, true);
  const ok = await submitDeck(page);
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const restrictedTable = page.locator('table.manabase-restricted-sources');
  await expect(restrictedTable).toBeVisible();
  await expect(restrictedTable).toContainText('Cavern of Souls');
  await expect(restrictedTable).toContainText('Ancient Ziggurat');
  await expect(restrictedTable).toContainText('Nykthos, Shrine to Nyx');
  await expect(
    page.getByLabel('restricted-source approximation applied').first(),
  ).toBeVisible();

  await expect(
    page.locator('.manabase-help', { hasText: 'restricted-source approximation applied to these land rows' }),
  ).toBeVisible();

  const unsupported = page.locator('details.manabase-unsupported');
  await unsupported.locator('summary').click();
  await expect(unsupported).toContainText('Restricted land approximation');
  await expect(unsupported).toContainText('Cavern of Souls');

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
