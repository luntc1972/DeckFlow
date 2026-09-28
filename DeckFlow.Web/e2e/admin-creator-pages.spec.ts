import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

let heldLock: Awaited<ReturnType<typeof acquireAdminLockForTest>> | null = null;

test.describe.configure({ mode: 'serial' });
test.beforeEach(async ({ page }) => { heldLock = await acquireAdminLockForTest(page); });
test.afterEach(async () => { await releaseAdminLockForTest(heldLock); heldLock = null; });

async function assertNoOverflow(page: import('@playwright/test').Page): Promise<void> {
  await page.setViewportSize({ width: 375, height: 900 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBeTruthy();
}

test('deck tendencies uses the shared card and fields', async ({ page }) => {
  await page.goto('/Admin/CreatorProfile');
  await expect(page.getByRole('heading', { level: 1, name: 'Deck Tendencies' })).toHaveCount(1);
  await expect(page.locator('.admin-page-header__lede')).toContainText('Upsert a creator source');
  for (const label of ['Slug', 'Username', 'Platform', 'Force Refresh']) await expect(page.getByLabel(label, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Run Crawl + Measure' })).toHaveClass(/admin-button--primary/);
  await assertNoOverflow(page);
});

test('deck tendencies report fixtures fit the viewport', async ({ page }) => {
  let posts = 0;
  await page.route((url) => url.pathname.toLowerCase() === '/admin/creatorprofile/run', async (route) => { posts++; await route.abort(); });
  await page.goto('/Admin/CreatorProfile');
  await page.evaluate(() => {
    const stack = document.querySelector('.admin-stack')!;
    for (const [id, empty] of [['p04-09-fx-decks', false], ['p04-09-fx-empty', true]] as const) {
      const section = document.createElement('section'); section.className = 'admin-card'; section.setAttribute('aria-labelledby', id);
      const title = document.createElement('h2'); title.id = id; title.className = 'admin-card__title'; title.textContent = 'Decks'; section.append(title);
      const table = document.createElement('table'); table.className = 'admin-table admin-table--card'; const caption = document.createElement('caption'); caption.className = 'sr-only'; caption.textContent = 'Decks'; table.append(caption);
      const body = document.createElement('tbody'); const row = document.createElement('tr'); const cell = document.createElement('td'); cell.colSpan = 5; cell.textContent = empty ? 'No decks.' : 'x'.repeat(120); row.append(cell); body.append(row); table.append(body); section.append(table); stack.append(section);
    }
  });
  await assertNoOverflow(page); expect(posts).toBe(0);
});

test('creator style uses the shared card and fields', async ({ page }) => {
  await page.goto('/Admin/CreatorStyle');
  await expect(page.getByRole('heading', { level: 1, name: 'Creator Style' })).toHaveCount(1);
  await expect(page.locator('.admin-page-header__lede')).toContainText('creator-style critique packet');
  for (const label of ['Creator', 'Input method', 'Format']) await expect(page.getByLabel(label, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Generate critique packet' })).toHaveClass(/admin-button--primary/);
  await assertNoOverflow(page);
});

test('creator style long artifact wraps inside the result region', async ({ page }) => {
  let posts = 0;
  await page.route((url) => url.pathname.toLowerCase() === '/admin/creatorstyle/run', async (route) => { posts++; await route.abort(); });
  await page.goto('/Admin/CreatorStyle');
  await page.locator('[data-creator-style-result]').evaluate((region) => { const pre = document.createElement('pre'); pre.className = 'admin-artifact'; pre.dataset.creatorStyleArtifact = ''; pre.textContent = `Creator Targets\n${'x'.repeat(400)}\n${Array(38).fill('ordinary sentence').join('\n')}`; region.append(pre); });
  const pre = page.locator('pre[data-creator-style-artifact]'); await expect(pre).toHaveCSS('white-space', 'pre-wrap'); await expect(pre).toHaveCSS('overflow-wrap', 'anywhere');
  await assertNoOverflow(page); expect(posts).toBe(0);
});
