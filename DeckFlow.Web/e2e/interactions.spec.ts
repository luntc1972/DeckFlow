import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

const getVisibleRowCount = async (page: Page): Promise<number> =>
  page.locator('#kb-entries-table tbody tr').evaluateAll((rows) =>
    rows
      .filter((row) => row.id !== 'kb-filter-empty')
      .filter((row) => !(row as HTMLTableRowElement).hidden)
      .length,
  );

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

test.describe.configure({ mode: 'serial' });

test('deck primer exposes bracket and section controls', async ({ page }) => {
  const response = await page.goto('/deck-primer');

  expect(response?.ok()).toBeTruthy();
  expect(await page.locator('script[src*="primer-selection.js"]').count()).toBeGreaterThan(0);
  await expect(page.locator('[data-primer-bracket]')).toBeVisible();
  await expect(page.locator('[data-primer-section-checkbox]').first()).toBeVisible();
});

for (const view of [
  { name: 'desktop', width: 1280, height: 900 },
  { name: 'mobile', width: 390, height: 844 },
]) {
  test(`deck primer step tabs are wired and scroll-navigate (${view.name})`, async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'error') {
        consoleErrors.push(message.text());
      }
    });

    await page.setViewportSize({ width: view.width, height: view.height });
    const response = await page.goto('/deck-primer');
    expect(response?.ok()).toBeTruthy();

    const tabs = page.locator('[data-primer-show-step]');
    await expect(tabs).toHaveCount(3);

    // Every tab's aria-controls target section must exist — the scroll-nav anchors.
    for (const id of ['primer-step-panel-1', 'primer-step-panel-2', 'primer-step-panel-3']) {
      await expect(page.locator(`#${id}`)).toHaveCount(1);
    }

    // Clicking a tab must run the handler: roving selection moves to the clicked tab.
    const step2 = tabs.nth(1);
    await step2.click();
    await expect(step2).toHaveAttribute('aria-selected', 'true');
    await expect(tabs.nth(0)).toHaveAttribute('aria-selected', 'false');

    expect(consoleErrors).toEqual([]);
  });
}

test('manabase hero keeps the deck-input tool above the mobile fold', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  // The long methodology copy is collapsed by default so the purpose + tool are reachable.
  const detail = page.locator('.hero-detail');
  await expect(detail).toHaveCount(1);
  expect(await detail.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(false);

  // The deck-input method selector must sit within the mobile viewport (above the fold).
  const toolTop = await page.locator('#manabase-input-source').evaluate((el) => el.getBoundingClientRect().top);
  expect(toolTop).toBeLessThan(844);

  // "How it works" expands to reveal the full methodology.
  await page.locator('.hero-detail > summary').click();
  expect(await detail.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(true);
});

test('manabase exposes a Load deck step before Analyze', async ({ page }) => {
  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  // Both submit buttons exist; Load posts to the dedicated detect-costs action.
  const loadButton = page.locator('.manabase-load-button');
  await expect(loadButton).toBeVisible();
  await expect(loadButton).toHaveAttribute('formaction', /\/manabase\/load$/);
  await expect(page.getByRole('button', { name: 'Analyze Mana Base' })).toBeVisible();
});

test('content kb detail copy button signals it builds an AI prompt', async ({ page }) => {
  const listResponse = await page.goto('/content-kb');
  expect(listResponse?.ok()).toBeTruthy();

  const firstCardLink = page.locator('[data-kb-entry] .hub-card__title a').first();
  test.skip((await firstCardLink.count()) === 0, 'no KB entries seeded');

  const href = await firstCardLink.getAttribute('href');
  expect(href).toBeTruthy();

  const detailResponse = await page.goto(href!);
  expect(detailResponse?.ok()).toBeTruthy();

  const copyButton = page.locator('button[data-copy-target="kb-artifact-text"]');
  test.skip((await copyButton.count()) === 0, 'artifact unavailable for this entry');

  // The label and lede must tell the user the copied text is a paste-ready AI
  // prompt, not just an opaque "Copy" action.
  await expect(copyButton).toBeVisible();
  await expect(copyButton).toContainText(/ChatGPT/i);

  const hint = page.locator('.kb-artifact-hint');
  await expect(hint).toBeVisible();
  await expect(hint).toContainText(/ChatGPT|Claude/i);

  // The longer label must not overflow the viewport or wrap to a giant button
  // on the 390px mobile project. Use viewport-relative rect coords.
  const rect = await copyButton.evaluate((el) => {
    const r = el.getBoundingClientRect();
    return { right: r.right, height: r.height, innerWidth: window.innerWidth };
  });
  expect(rect.right).toBeLessThanOrEqual(rect.innerWidth + 1);
  expect(rect.height).toBeLessThanOrEqual(80);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
});

test.describe('admin content kb flows @admin', () => {
  let heldLock: LockHandle | null = null;

  test.beforeEach(async ({ page }) => {
    // The admin Content KB pages share a SQLite-backed admin store. Keep every
    // /Admin/ content-kb test behind the same lock so desktop/mobile workers do
    // not collide and trigger transient 429/500 responses in CI.
    heldLock = await acquireAdminLockForTest(page);
  });

  test.afterEach(async () => {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  });

  test('admin content kb filter wires without console errors and narrows rows', async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on('console', (message) => {
      if (message.type() === 'error') {
        consoleErrors.push(message.text());
      }
    });

    const response = await page.goto('/Admin/ContentKb?visibilityFilter=all');

    expect(response?.ok()).toBeTruthy();
    expect(await page.locator('script[src*="admin-modal.js"]').count()).toBeGreaterThan(0);
    expect(await page.locator('script[src*="kb-entry-filter.js"]').count()).toBeGreaterThan(0);
    expect(await page.locator('script[src*="content-kb-admin.js"]').count()).toBeGreaterThan(0);
    await expect(page.locator('#kb-filter-search')).toBeVisible();

    const totalRows = await page.locator('#kb-entries-table tbody tr').count();
    const visibleRowsBefore = await getVisibleRowCount(page);

    await page.locator('#kb-filter-search').fill('zzzznomatch');

    expect(consoleErrors).toEqual([]);

    if (totalRows > 1) {
      await expect.poll(async () => getVisibleRowCount(page)).toBeLessThan(visibleRowsBefore);
    }
  });

  test('content kb visibility pills sit below Sources and directly above the filter', async ({ page }) => {
    // Regression guard: a layout restructure once hoisted the Entries header
    // (carrying the All/Published/Unpublished/Hidden pills) above the Sources
    // table, separating the pills from the entries filter/grid so they appeared
    // "gone" once scrolled to the filter. Order must be Sources -> pills -> filter.
    const response = await page.goto('/Admin/ContentKb?visibilityFilter=all');
    expect(response?.ok()).toBeTruthy();

    const sourcesY = await page.locator('#kb-bulk-heading').boundingBox();
    const pillsY = await page.locator('.admin-kb-toggle').boundingBox();
    const filterY = await page.locator('#kb-filter-search').boundingBox();
    const gridY = await page.locator('#kb-entries-table').boundingBox();

    expect(sourcesY).not.toBeNull();
    expect(pillsY).not.toBeNull();
    expect(filterY).not.toBeNull();
    expect(gridY).not.toBeNull();

    // Sources table is above the pills; pills are above the filter and grid.
    expect(sourcesY!.y).toBeLessThan(pillsY!.y);
    expect(pillsY!.y).toBeLessThan(filterY!.y);
    expect(filterY!.y).toBeLessThan(gridY!.y);
  });

  test('admin content kb delete requires an arm click before submission', async ({ page }) => {
    const response = await page.goto('/Admin/ContentKb?visibilityFilter=all');

    expect(response?.ok()).toBeTruthy();

    const deleteButtons = page.locator('form[data-admin-confirm-twoclick] button.danger');
    const deleteButtonCount = await deleteButtons.count();
    test.skip(deleteButtonCount === 0, 'no KB rows seeded');

    const firstDeleteButton = deleteButtons.first();
    const startingUrl = page.url();

    await firstDeleteButton.click();

    await expect(page).toHaveURL(startingUrl);

    const buttonText = (await firstDeleteButton.textContent())?.trim() ?? '';
    const isArmed = await firstDeleteButton.evaluate((button) => button.classList.contains('is-armed'));

    expect(isArmed || buttonText === 'Confirm delete').toBeTruthy();
  });

  test('admin content kb keeps creator + search filter across visibility tab switches', async ({ page }) => {
    const response = await page.goto('/Admin/ContentKb?visibilityFilter=all');
    expect(response?.ok()).toBeTruthy();

    const creator = page.locator('#kb-creator-filter');
    const optionCount = await creator.locator('option').count();
    test.skip(optionCount < 2, 'no creator options seeded');

    // Index 0 is the "All creators" placeholder; pick the first real creator.
    const chosenCreator = await creator.locator('option').nth(1).getAttribute('value');
    await creator.selectOption(chosenCreator!);
    await page.locator('#kb-filter-search').fill('combo');

    // Switching the visibility tab is a full-page <a> navigation (not a form
    // submit), so the creator + search filter must be restored from sessionStorage
    // rather than reset. Switch to the Unpublished tab: seeded entries default to
    // unpublished, so that tab is populated and renders the filter bar — the
    // Published tab can be empty (no entries -> no toggle/filter controls at all).
    // Target by href; matching on the text "Unpublished" is fine but href is exact.
    await page.locator('.admin-kb-toggle a[href*="visibilityFilter=unpublished"]').click();
    await expect(page).toHaveURL(/visibilityFilter=unpublished/);

    await expect(page.locator('#kb-creator-filter')).toHaveValue(chosenCreator!);
    await expect(page.locator('#kb-filter-search')).toHaveValue('combo');
  });
});
