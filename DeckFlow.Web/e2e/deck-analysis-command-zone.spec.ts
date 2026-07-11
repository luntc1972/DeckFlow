import { expect, test, type Locator, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

// Smoke for Phase 73: the deck-analysis Step 1 companion designator input is gated on the
// analysis.command-zone-awareness flag. When the flag is ON the single input[name="CompanionName"]
// renders; when OFF it is absent and the page is unchanged.
//
// Start the app with scripts/run-web-test.sh first (sets DECKFLOW_DISABLE_AUTO_BROWSER=true so no
// Windows-host browser is opened), then from DeckFlow.Web/ run:
//   npx --no-install playwright test e2e/deck-analysis-command-zone.spec.ts

const commandZoneFlagKey = 'analysis.command-zone-awareness';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

// /Admin/* specs share one SQLite flag store + a single forwarded-IP throttle, so they must run
// serially behind the shared lock to avoid cross-spec flakes.
test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async ({ page }) => {
  try {
    // Restore the default-OFF state so other specs and the prod default are unaffected.
    await setFlagEnabled(page, commandZoneFlagKey, false);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('companion designator renders on deck-analysis Step 1 when the flag is ON', async ({ page }) => {
  await setFlagEnabled(page, commandZoneFlagKey, true);

  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  const companionInput = page.locator('input[name="CompanionName"]');
  await expect(companionInput).toHaveCount(1);

  // The input lives inside a collapsible <details>; open it so visibility holds.
  await page.locator('.deck-analysis-overrides > summary').click();
  await expect(companionInput).toBeVisible();
});

test('companion designator is absent on deck-analysis Step 1 when the flag is OFF', async ({ page }) => {
  await setFlagEnabled(page, commandZoneFlagKey, false);

  const response = await page.goto('/deck-analysis');
  expect(response?.ok()).toBeTruthy();

  await expect(page.locator('input[name="CompanionName"]')).toHaveCount(0);
  await expect(page.locator('.deck-analysis-overrides')).toHaveCount(0);
});

async function setFlagEnabled(page: Page, key: string, enabled: boolean): Promise<void> {
  const response = await page.goto('/Admin/Flags');
  expect(response?.ok()).toBeTruthy();

  const row = getFlagRow(page, key);
  const status = row.locator('[data-label="Status"]');
  const currentStatus = (await status.textContent())?.trim();
  const desiredStatus = enabled ? 'On' : 'Off';
  if (currentStatus === desiredStatus) {
    return;
  }

  await row.getByRole('button', { name: enabled ? 'Enable' : 'Disable', exact: true }).click();
  await expect(page.locator('.admin-banner--success')).toBeVisible();
  await expect(getFlagRow(page, key).locator('[data-label="Status"]')).toHaveText(desiredStatus);
}

function getFlagRow(page: Page, key: string): Locator {
  return page.locator(`tr[data-flag-key="${key}"]`);
}
