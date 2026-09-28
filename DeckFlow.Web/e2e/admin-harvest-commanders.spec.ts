import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

const commandersGrid = (page: Page) => page.locator('#commanders-grid-container');

const populatedGrid = '<p class="admin-meta">1 commanders — Page 1 of 1</p><div class="admin-table-scroll"><table class="admin-table"><tbody><tr><td>1</td><td>Fixture Commander</td><td>3</td><td>2026-01-01</td></tr></tbody></table></div>';

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('commanders grid failure shows a danger alert and a deterministic retry repopulates the grid', async ({ page }) => {
  let requestCount = 0;
  await page.route('**/Admin/Harvest/commanders**', async (route) => {
    requestCount++;
    if (requestCount === 1) {
      await route.fulfill({ status: 500 });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'text/html; charset=utf-8',
      body: populatedGrid,
    });
  });

  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await page.locator('#harvest-tab-commanders').click();

  const grid = commandersGrid(page);
  const banner = grid.locator('.admin-banner--danger[role="alert"]');
  await expect(banner).toBeVisible();
  await expect(banner).toContainText('Could not load commanders.');
  await expect(banner.locator('#commanders-retry')).toBeVisible();
  await expect(grid).toHaveAttribute('aria-busy', 'false');

  await banner.locator('#commanders-retry').click();
  await expect(banner).toHaveCount(0);
  await expect(grid.locator('p.admin-meta')).toHaveText('1 commanders — Page 1 of 1');
  await expect(grid.getByRole('row').filter({ hasText: 'Fixture Commander' })).toBeVisible();
  await expect(grid).toHaveAttribute('aria-busy', 'false');
  expect(requestCount).toBe(2);
});
