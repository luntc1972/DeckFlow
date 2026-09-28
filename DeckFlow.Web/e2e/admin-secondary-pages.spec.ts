import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  if (heldLock) {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('analytics range chips sit in the page header actions', async ({ page }) => {
  await page.goto('/Admin/Analytics');
  await expect(page.locator('h1')).toHaveCount(1);
  await expect(page.locator('h1')).toHaveText('Page-usage analytics');
  await expect(page.locator('.admin-page-header__lede')).toHaveText('Page-view and traffic analytics over time.');
  const chips = page.locator('.admin-page-header__actions nav[aria-label="Time range"] a.admin-filter-chips__chip');
  await expect(chips).toHaveCount(4);
  await expect(page.locator('.admin-page-header__actions nav[aria-label="Time range"] a.admin-filter-chips__chip[aria-current="true"]')).toHaveCount(1);
  await expect(page.locator('[data-admin-analytics]')).toHaveCount(1);
  await chips.getByText('Last 30 days').click();
  await expect(page).toHaveURL(/range=30d/);
  await expect(chips.getByText('Last 30 days')).toHaveAttribute('aria-current', 'true');
  expect((await chips.getByText('Last 30 days').boundingBox())!.height).toBeGreaterThanOrEqual(44);
  await page.setViewportSize({ width: 375, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
});
