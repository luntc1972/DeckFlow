import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('Marker backfill shows starting, running and completed feedback with one reload', async ({ page }) => {
  let backfillResponseSeen = false;
  let redirectSeen = false;
  let runningStatusServed = false;
  let reloadCount = 0;

  page.on('response', (response) => {
    const request = response.request();
    const pathname = new URL(response.url()).pathname.toLowerCase();
    if (request.method() === 'POST' && pathname === '/admin/harvest/maintenance/backfill') {
      backfillResponseSeen = true;
    }
    if (backfillResponseSeen && request.method() === 'GET' && request.isNavigationRequest()
      && request.resourceType() === 'document' && pathname === '/admin/harvest') {
      redirectSeen = true;
    }
  });

  page.on('request', (request) => {
    if (runningStatusServed && request.method() === 'GET' && request.isNavigationRequest()
      && request.resourceType() === 'document' && new URL(request.url()).pathname.toLowerCase() === '/admin/harvest') {
      reloadCount += 1;
    }
  });

  await page.route(/\/admin\/harvest\/maintenance\/backfill$/i, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 1500));
    await route.continue();
  });
  await page.route(/\/admin\/harvest\/maintenance\/status$/i, async (route) => {
    if (!redirectSeen || runningStatusServed) {
      await route.continue();
      return;
    }

    runningStatusServed = true;
    await route.fulfill({
      json: {
        state: 'running',
        tone: 'info',
        headline: 'Marker backfill running.',
        details: [],
        isActive: true,
      },
    });
  });

  await page.goto('/Admin/Harvest');
  const maintenance = page.locator('#harvest-maintenance');
  const backfill = maintenance.locator('form[data-maintenance-operation="backfill"]');
  const button = backfill.getByRole('button');
  const status = maintenance.locator('#harvest-maintenance-status');

  await backfill.getByRole('checkbox', { name: 'Confirm Marker backfill' }).check();
  // Do not wait for navigation: the starting label must be observed during the delayed POST.
  await button.click({ noWaitAfter: true });
  await expect(button).toBeDisabled();
  await expect(button).toHaveText('Starting Marker backfill…');
  await expect(status.locator('[data-maintenance-headline]')).toHaveText('Marker backfill running.');
  await expect(status).toHaveAttribute('data-maintenance-state', 'completed', { timeout: 20_000 });
  await expect(status.locator('[data-maintenance-headline]')).toHaveText('Marker backfill: Done.', { timeout: 20_000 });
  await expect.poll(() => reloadCount).toBe(1);
  // Observe beyond one polling interval to catch an unintended second reload.
  await page.waitForTimeout(6000);
  expect(reloadCount).toBe(1);
  await expect(button).toBeEnabled();
});

test('Preview shows counting feedback and the counts timestamp', async ({ page }) => {
  await page.route(/\/admin\/harvest\/maintenance\/preview$/i, async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 1500));
    await route.continue();
  });

  await page.goto('/Admin/Harvest');
  const maintenance = page.locator('#harvest-maintenance');
  const button = maintenance.getByRole('button', { name: 'Preview', exact: true });

  await button.click({ noWaitAfter: true });
  await expect(button).toBeDisabled();
  await expect(button).toHaveText('Counting…');
  await expect(page.locator('.admin-banner').filter({ hasText: 'Retention excess:' }))
    .toHaveText(/\(counts as of \d{2}:\d{2}:\d{2} UTC\)\.$/);
});
