import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

// Phase 81 MULLIGAN-01/02/06 — the opening-hand/mulligan lens card on /manabase, gated behind
// analysis.manabase.mulligan-eval (default ON). This is a LIVE-UX SMOKE (card visible at desktop 1280 + mobile 390 —
// runs under both the chromium-desktop and chromium-mobile Playwright projects — when the flag
// is ON, absent when OFF) — NOT the byte-identity proof, which lives in
// ManabaseViewRenderTests.OffState_IsByteIdenticalToOnWithMulliganCardExcised (an IRazorViewEngine
// excision test).
//
// Submits a real paste decklist, which drives a live Scryfall card-resolution round-trip (same
// convention as manabase-castability.spec.ts / manabase-ramp-disclosure.spec.ts): when the
// sandbox can't reach Scryfall the result panel never appears, so the result-dependent
// assertions are guarded with test.skip rather than failing on an environment limitation.
//
// The flag is toggled via /Admin/Flags (Admin e2e specs share the SQLite feature-flag store and
// the admin brute-force throttle, so they must serialize — mirrors deck-analysis-render.spec.ts's
// lock-file + synthetic CF-Connecting-IP convention).

const mulliganEvalFlagKey = 'analysis.manabase.mulligan-eval';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let originalEnabled = true;

// A small but real two-color shell: a commander, basics, and an early removal spell so a
// representative opener has a genuine tracked early play (mirrors manabase-castability.spec.ts's
// shell).
const PASTE_DECK = [
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
  // Restore this flag to its pre-test state to avoid cross-spec contamination (incident 2026-07-14).
  originalEnabled = await captureOriginalFlagEnabled(page, mulliganEvalFlagKey);
});

test.afterEach(async ({ page }) => {
  try {
    await restoreFlagEnabled(page, mulliganEvalFlagKey, originalEnabled);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

async function submitDeck(page: Page): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(PASTE_DECK);
  // Click Analyze specifically — the page also has a "Load deck" run-button, so a bare
  // `button.run-button` matches two elements (strict-mode failure).
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

test('opening-hand lens card is visible when analysis.manabase.mulligan-eval is ON', async ({ page }) => {
  await setFlagEnabled(page, mulliganEvalFlagKey, true);
  const ok = await submitDeck(page);
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const lens = page.locator('.manabase-mulliganlens');
  await expect(lens).toBeVisible();
  await expect(lens).toContainText('Opening hand');
  await expect(lens).toContainText('keepable hands');
  await expect(lens).toContainText('Keep-size process');

  expect(
    await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1),
  ).toBeTruthy();
});

test('opening-hand lens card is absent when analysis.manabase.mulligan-eval is OFF', async ({ page }) => {
  await setFlagEnabled(page, mulliganEvalFlagKey, false);
  const ok = await submitDeck(page);
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  await expect(page.locator('.manabase-mulliganlens')).toHaveCount(0);
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
