import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

let heldLock: Awaited<ReturnType<typeof acquireAdminLockForTest>> | null = null;
test.describe.configure({ mode: 'serial' });
test.beforeEach(async ({ page }) => { heldLock = await acquireAdminLockForTest(page); });
test.afterEach(async () => { await releaseAdminLockForTest(heldLock); heldLock = null; });

async function assertNoOverflow(page: import('@playwright/test').Page): Promise<void> {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBeTruthy();
}

async function guardRun(page: import('@playwright/test').Page, path: string): Promise<() => number> {
  let posts = 0;
  await page.route(url => url.pathname.toLowerCase() === path, async route => { posts++; await route.abort(); });
  return () => posts;
}

test('deck tendencies uses the shared card and fields', async ({ page }) => {
  await page.goto('/Admin/CreatorProfile');
  await expect(page.getByRole('heading', { level: 1, name: 'Deck Tendencies' })).toHaveCount(1);
  await expect(page.locator('.admin-page-header__lede')).toContainText('Upsert a creator source');
  for (const label of ['Slug', 'Username', 'Platform', 'Force Refresh']) await expect(page.getByLabel(label, { exact: true })).toBeVisible();
  const button = page.getByRole('button', { name: 'Run Crawl + Measure' });
  await expect(button).toHaveClass(/admin-button--primary/);
  expect((await button.boundingBox())?.height).toBeGreaterThanOrEqual(44);
  await page.setViewportSize({ width: 375, height: 900 }); await assertNoOverflow(page);
  const label = page.locator('label[for="creator-profile-force-refresh"]');
  const checkbox = page.locator('#creator-profile-force-refresh');
  const labelBox = await label.boundingBox();
  const checkboxBox = await checkbox.boundingBox();
  expect(labelBox?.height).toBeGreaterThanOrEqual(44);
  expect(checkboxBox?.width).toBeGreaterThanOrEqual(19.5);
  expect(checkboxBox?.height).toBeGreaterThanOrEqual(19.5);
  expect((checkboxBox?.x ?? Infinity) + (checkboxBox?.width ?? 0)).toBeLessThanOrEqual((labelBox?.x ?? -Infinity) + 0.5);
  const checked = await checkbox.isChecked();
  await label.click();
  await expect(checkbox).toBeChecked({ checked: !checked });
  await label.click();
  await expect(checkbox).toBeChecked({ checked });
});

test('deck tendencies report fixtures fit the viewport', async ({ page }) => {
  const posts = await guardRun(page, '/admin/creatorprofile/run');
  await page.goto('/Admin/CreatorProfile');
  await page.evaluate(() => {
    const stack = document.querySelector('div.admin-stack')!;
    const add = (id: string, empty: boolean) => {
      const section = document.createElement('section'); section.className = 'admin-card'; section.setAttribute('aria-labelledby', id);
      const title = document.createElement('h2'); title.id = id; title.className = 'admin-card__title'; title.textContent = 'Decks'; section.append(title);
      const table = document.createElement('table'); table.className = 'admin-table admin-table--card'; const caption = document.createElement('caption'); caption.className = 'sr-only'; caption.textContent = 'Decks'; table.append(caption);
      const head = document.createElement('thead'); const headRow = document.createElement('tr'); ['Deck Id', 'Deck Name', 'Card Count', 'Folder Name', 'Commanders'].forEach(value => { const th = document.createElement('th'); th.scope = 'col'; th.textContent = value; headRow.append(th); }); head.append(headRow); table.append(head);
      const body = document.createElement('tbody');
      if (empty) { const row = document.createElement('tr'); const cell = document.createElement('td'); cell.colSpan = 5; cell.textContent = 'No decks.'; row.append(cell); body.append(row); }
      else for (let rowIndex = 0; rowIndex < 12; rowIndex++) { const row = document.createElement('tr'); ['Deck Id', 'Deck Name', 'Card Count', 'Folder Name', 'Commanders'].forEach((label, cellIndex) => { const cell = document.createElement('td'); cell.dataset.label = label; const value = rowIndex === 0 && (cellIndex === 1 || cellIndex === 4) ? 'x'.repeat(120) : `${label} ${rowIndex}`; if (cellIndex === 0) { const code = document.createElement('code'); code.textContent = value; cell.append(code); } else cell.textContent = value; row.append(cell); }); body.append(row); }
      table.append(body); section.append(table); stack.append(section);
    };
    add('p04-09-fx-decks', false); add('p04-09-fx-empty', true);
  });
  const sizes = [await page.viewportSize(), { width: 375, height: 900 }];
  for (const size of sizes) {
    if (size) await page.setViewportSize(size);
    await assertNoOverflow(page);
    for (const id of ['p04-09-fx-decks', 'p04-09-fx-empty']) {
      const card = page.locator(`section[aria-labelledby='${id}']`); const box = await card.boundingBox(); expect(box!.x + box!.width).toBeLessThanOrEqual((await page.viewportSize())!.width + 1);
      for (const cell of await card.locator('td').all()) { const cellBox = await cell.boundingBox(); expect(cellBox!.x + cellBox!.width).toBeLessThanOrEqual(box!.x + box!.width + 1); }
    }
  }
  await expect(page.locator("section[aria-labelledby='p04-09-fx-decks'] tbody tr")).toHaveCount(12);
  await page.setViewportSize({ width: 375, height: 900 }); expect(await page.locator("section[aria-labelledby='p04-09-fx-decks'] tbody tr").first().evaluate(row => getComputedStyle(row).display)).toBe('block');
  await page.setViewportSize({ width: 1280, height: 900 }); expect(await page.locator("section[aria-labelledby='p04-09-fx-decks'] tbody tr").first().evaluate(row => getComputedStyle(row).display)).not.toBe('block');
  expect(posts()).toBe(0);
});

test('creator style uses the shared card and fields', async ({ page }) => {
  await page.goto('/Admin/CreatorStyle');
  await expect(page.getByRole('heading', { level: 1, name: 'Creator Style' })).toHaveCount(1);
  await expect(page.locator('.admin-page-header__lede')).toContainText('creator-style critique packet');
  for (const label of ['Creator', 'Input method', 'Format']) await expect(page.getByLabel(label, { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Generate critique packet' })).toHaveClass(/admin-button--primary/);
  const notice = page.locator('[data-creator-style-notice]'); if (await notice.count()) { await expect(notice).toHaveClass(/admin-banner--warning/); expect((await notice.textContent())?.trim()).not.toBe(''); }
  await page.setViewportSize({ width: 375, height: 900 }); await assertNoOverflow(page);
});

test('creator style long artifact wraps inside the result region', async ({ page }) => {
  const posts = await guardRun(page, '/admin/creatorstyle/run');
  await page.goto('/Admin/CreatorStyle');
  await page.locator('[data-creator-style-result]').evaluate(region => { region.querySelectorAll('pre[data-creator-style-artifact]').forEach(pre => pre.remove()); const pre = document.createElement('pre'); pre.className = 'admin-artifact'; pre.dataset.creatorStyleArtifact = ''; pre.textContent = `Creator Targets\n${'x'.repeat(400)}\n${Array(38).fill('ordinary sentence').join('\n')}`; region.append(pre); });
  const artifact = page.locator('pre[data-creator-style-artifact]');
  for (const size of [await page.viewportSize(), { width: 375, height: 900 }]) { if (size) await page.setViewportSize(size); expect(await artifact.evaluate(pre => getComputedStyle(pre).whiteSpace)).toBe('pre-wrap'); expect(await artifact.evaluate(pre => getComputedStyle(pre).overflowWrap)).toBe('anywhere'); expect(await artifact.evaluate(pre => pre.scrollWidth <= pre.clientWidth + 1)).toBeTruthy(); const box = await artifact.boundingBox(); expect(box!.x + box!.width).toBeLessThanOrEqual((await page.viewportSize())!.width + 1); await assertNoOverflow(page); }
  expect(posts()).toBe(0);
});
