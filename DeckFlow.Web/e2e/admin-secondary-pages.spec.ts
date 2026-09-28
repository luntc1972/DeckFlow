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

test('feedback list and seeded detail use the shared components and delete confirms', async ({ page }) => {
  const marker = `p04-08 e2e feedback ${crypto.randomUUID().replaceAll('-', '').slice(0, 16)}`;
  let feedbackId: string | undefined;
  const feedbackPath = (url: URL) => url.pathname.toLowerCase() === '/feedback';
  await page.route(feedbackPath, async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }
    await route.continue({ headers: { ...route.request().headers(), 'cf-connecting-ip': '2001:db8:1:2:3:4' } });
  });
  try {
    await page.goto('/Feedback');
    await page.getByLabel('Message').fill(marker);
    await page.getByRole('button', { name: 'Send Feedback' }).click();
    await expect(page.locator('.feedback-banner--success[role=status]')).toBeVisible();
    await page.unroute(feedbackPath);
    await page.goto('/Admin/Feedback');
    const row = page.locator('tr', { hasText: marker });
    await expect(row).toHaveCount(1);
    const view = row.getByRole('link', { name: 'View' });
    feedbackId = new URL(await view.getAttribute('href')!, page.url()).pathname.match(/\/(\d+)$/)?.[1];
    expect(feedbackId).toBeTruthy();
    await expect(page.locator('h1')).toHaveText('Feedback');
    await expect(page.locator('.admin-page-header__lede')).toHaveText('Review and triage user-submitted feedback and bug reports.');
    await expect(page.locator('nav[aria-label="Status filter"] a.admin-filter-chips__chip')).toHaveCount(4);
    await view.click();
    await expect(page.locator('h1')).toHaveText(`Feedback #${feedbackId}`);
    await expect(page.locator('pre.admin-artifact')).toContainText(marker);
    const deleteForm = page.locator(`form[data-admin-confirm-delete][data-admin-feedback-id="${feedbackId}"]`);
    await expect(deleteForm.locator('button.admin-button--danger')).toBeVisible();
    let deletes = 0;
    await page.route((url) => url.pathname.toLowerCase() === `/admin/feedback/${feedbackId}/delete`, async (route) => {
      deletes++;
      await route.continue();
    });
    await deleteForm.getByRole('button', { name: 'Delete' }).click();
    await page.locator('[data-admin-modal-cancel]').click();
    expect(deletes).toBe(0);
    await deleteForm.getByRole('button', { name: 'Delete' }).click();
    await page.locator('[data-admin-modal-confirm]').click();
    await expect(page).toHaveURL(/\/admin\/feedback$/i);
    expect(deletes).toBe(1);
    await expect(page.locator('tr', { hasText: marker })).toHaveCount(0);
    feedbackId = undefined;
  } finally {
    if (feedbackId) {
      await page.goto(`/Admin/Feedback/${feedbackId}`);
      const form = page.locator('form[data-admin-confirm-delete]');
      if (await form.count()) {
        await Promise.all([page.waitForURL(/\/admin\/feedback$/i), form.evaluate((element: HTMLFormElement) => element.submit())]);
      }
    }
  }
});
