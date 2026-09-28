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

test('admin tokens resolve on the live admin shell', async ({ page }) => {
  const response = await page.goto('/Admin/Tools');
  expect(response?.ok()).toBeTruthy();

  await page.locator('details.admin-sidebar__disclosure').evaluate((details) => {
    details.open = true;
  });

  const tokens = await page.evaluate(() => {
    const styles = getComputedStyle(document.documentElement);
    return {
      spaceMd: styles.getPropertyValue('--space-md').trim(),
      statusDanger: styles.getPropertyValue('--status-danger').trim(),
      textBody: styles.getPropertyValue('--text-body').trim(),
    };
  });

  expect(tokens).toEqual({ spaceMd: '16px', statusDanger: '#ef4444', textBody: '15px' });
  await expect(page.locator('a.admin-sidebar__link[aria-current="page"]')).toHaveCount(1);
  await expect(page.locator('a.admin-sidebar__link[aria-current="page"]')).toHaveCSS(
    'background-color',
    'rgba(59, 130, 246, 0.08)');
});
