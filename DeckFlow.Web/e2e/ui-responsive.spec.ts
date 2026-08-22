import { expect, test } from '@playwright/test';
import { setToolEnabled } from './support/admin-tools';

const cutLabPool = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '36 Plains',
  '36 Island',
  '20 Swamp',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Fellwar Stone',
  '1 Mystic Remora',
  '1 Rhystic Study',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  "1 Dovin's Veto",
  '1 Demonic Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

// Guards the Phase-1 mobile UI changes so desktop behavior stays intact while
// mobile-specific navigation, layout defaults, and overflow fixes remain covered.

// Retries a navigation up to twice on a non-ok response. Under fullyParallel CI
// the SQLite store can briefly return a 5xx ("database is locked") when many
// workers hit pages at once; a re-navigate clears it. Mirrors scripts.spec.ts.
async function gotoOk(page: import('@playwright/test').Page, route: string) {
  let response = await page.goto(route);
  for (let attempt = 0; attempt < 2 && !response?.ok(); attempt++) {
    response = await page.goto(route);
  }
  return response;
}

test('tool nav collapses on mobile, expanded on desktop', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await gotoOk(page, '/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const toggle = page.locator('[data-tool-nav-menu-toggle]');
  const firstGroup = page.locator('.tool-nav__group').first();

  if (isMobile) {
    await expect(toggle).toBeVisible();
    await expect(firstGroup).not.toBeVisible();
    await expect(toggle).toHaveAttribute('aria-expanded', 'false');

    await toggle.click();

    await expect(toggle).toHaveAttribute('aria-expanded', 'true');
    await expect(firstGroup).toBeVisible();
    return;
  }

  await expect(toggle).toBeHidden();
  await expect(firstGroup).toBeVisible();
});

test('verbosity layout picker available on mobile and defaults to Compact', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await gotoOk(page, '/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const picker = page.locator('[data-prompt-ui-mode-picker]');
  const form = page.locator('.prompt-packets-form');

  await expect(picker).toBeVisible();
  await expect(form).toBeVisible();

  if (isMobile) {
    await expect(form).toHaveAttribute('data-prompt-ui-mode', 'focused');
    await expect(page.locator('[data-prompt-ui-mode-button="focused"]')).toHaveClass(/is-active/);
    return;
  }

  await expect(form).toHaveAttribute('data-prompt-ui-mode', 'guided');
  await expect(page.locator('[data-prompt-ui-mode-button="guided"]')).toHaveClass(/is-active/);
});

test('download-session button is not the primary run-button on mobile', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await gotoOk(page, '/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const downloadButton = page.locator('.prompt-sticky-download__button');

  await expect(downloadButton).toBeVisible();

  if (!isMobile) {
    return;
  }

  const nextButton = page.locator('[data-prompt-next-step]').first();
  await expect(nextButton).toBeVisible();

  const downloadBackground = await downloadButton.evaluate((element) => getComputedStyle(element).backgroundColor);
  const nextBackground = await nextButton.evaluate((element) => getComputedStyle(element).backgroundColor);

  expect(downloadBackground).not.toBe(nextBackground);
});

test('mobile workflow stepper is compact (no hidden scroll, numbers shown)', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await gotoOk(page, '/deck-analysis');

  expect(response?.ok()).toBeTruthy();

  const nav = page.locator('.prompt-step-nav');
  await expect(nav).toBeVisible();
  expect(await nav.count()).toBeGreaterThan(0);

  const tabs = nav.locator('.prompt-step-tab');
  const firstTab = nav.locator('.prompt-step-tab').first();
  const firstNumber = firstTab.locator('.prompt-step-tab__num');
  const firstLabel = firstTab.locator('.prompt-step-tab__label');

  if (isMobile) {
    expect(await nav.evaluate((el) => el.scrollWidth <= el.clientWidth + 2)).toBe(true);
    await expect(nav).not.toHaveClass(/prompt-step-nav--labeled/);
    await expect(firstNumber).toBeVisible();
    await expect(firstLabel).toBeHidden();
    const tabMetrics = await tabs.evaluateAll(elements => elements.map(element => {
      const rect = element.getBoundingClientRect();
      return { width: rect.width, height: rect.height };
    }));
    expect(tabMetrics.every(tab => tab.width === 44 && tab.height === 44)).toBe(true);
    return;
  }

  await expect(firstLabel).toBeVisible();
  await expect(firstNumber).toBeHidden();
});

test('Cut Lab mobile workflow stepper shows its short step names', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  await setToolEnabled(page, 'Cut Lab', true);
  const response = await gotoOk(page, '/cut-lab');

  expect(response?.ok()).toBeTruthy();

  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(cutLabPool);
  await page.locator('#cut-lab-primary-plan').fill('Protect the control shell.');
  await page.getByRole('button', { name: 'Import pool' }).click();

  const nav = page.locator('.prompt-step-nav');
  const tabs = nav.locator('.prompt-step-tab');
  await expect(tabs).toHaveCount(5);

  if (!isMobile) {
    return;
  }

  await expect(nav).toHaveClass(/prompt-step-nav--labeled/);
  await expect(tabs.locator('.prompt-step-tab__label')).toHaveText([
    'Process',
    'Decide',
    'Plan',
    'Goals',
    'Export',
  ]);

  for (const label of await tabs.locator('.prompt-step-tab__label').all()) {
    await expect(label).toBeVisible();
  }

  const tabMetrics = await tabs.evaluateAll(elements => elements.map(element => {
    const rect = element.getBoundingClientRect();
    return { text: element.textContent?.trim(), width: rect.width, height: rect.height };
  }));
  expect(tabMetrics).toEqual(expect.arrayContaining([
    expect.objectContaining({ text: '1Process' }),
    expect.objectContaining({ text: '2Decide' }),
    expect.objectContaining({ text: '3Plan' }),
    expect.objectContaining({ text: '4Goals' }),
    expect.objectContaining({ text: '5Export' }),
  ]));
  expect(tabMetrics.every(tab => tab.width >= 44 && tab.height >= 44)).toBe(true);
  expect(await nav.evaluate(element => element.scrollWidth > element.clientWidth)).toBe(true);
});

test('deck primer section groups collapse on mobile', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await gotoOk(page, '/deck-primer');

  expect(response?.ok()).toBeTruthy();

  const groups = page.locator('details.primer-group');
  const groupCount = await groups.count();

  expect(groupCount).toBeGreaterThan(1);

  const firstGroup = groups.first();
  const secondGroup = groups.nth(1);

  if (isMobile) {
    expect(await firstGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
    expect(await secondGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(false);
    return;
  }

  expect(await firstGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
  expect(await secondGroup.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
});

test('content kb filters collapse on mobile', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  const response = await gotoOk(page, '/content-kb');

  expect(response?.ok()).toBeTruthy();

  const filters = page.locator('details.kb-filters');
  const filterCount = await filters.count();

  if (filterCount === 0) {
    test.skip();
  }

  if (isMobile) {
    expect(await filters.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(false);

    await page.locator('.kb-filters__summary').click();

    expect(await filters.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
    return;
  }

  expect(await filters.evaluate((element) => (element as HTMLDetailsElement).open)).toBe(true);
});

test('content kb card is tappable', async ({ page }) => {
  const response = await gotoOk(page, '/content-kb');

  expect(response?.ok()).toBeTruthy();

  const cards = page.locator('[data-kb-entry]');
  if ((await cards.count()) === 0) {
    test.skip();
  }

  const firstCard = cards.first();
  const titleLink = firstCard.locator('.hub-card__title a');
  const detailHref = await titleLink.getAttribute('href');

  expect(detailHref).toBeTruthy();

  await firstCard.locator('.hub-card__description').click();

  expect(page.url()).toContain(detailHref ?? '/content-kb/');
});

test('content kb search box is not oversized on desktop', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  if (isMobile) {
    // Mobile stacks the filter bar in a column; this guards the desktop-only
    // sizing regression (search input formerly stretched full-width and grew
    // vertically to the open filters block height).
    test.skip();
  }

  const response = await gotoOk(page, '/content-kb');
  expect(response?.ok()).toBeTruthy();

  const search = page.locator('[data-kb-search]');
  const bar = page.locator('.kb-filter-bar');
  const details = page.locator('details.kb-filters');

  await expect(search).toBeVisible();

  const searchBox = await search.boundingBox();
  const barBox = await bar.boundingBox();
  const detailsBox = await details.boundingBox();

  expect(searchBox).not.toBeNull();
  expect(barBox).not.toBeNull();
  expect(detailsBox).not.toBeNull();

  // Horizontal: `flex: 0 1 22rem` caps the search input (~352px @ 16px root)
  // instead of letting it stretch across the whole filter bar.
  expect(searchBox!.width).toBeLessThanOrEqual(420);
  expect(searchBox!.width).toBeLessThan(barBox!.width * 0.75);

  // Vertical: `align-items: flex-start` keeps the search input at its natural
  // control height rather than stretching to fill the filter bar. (The Filters
  // disclosure now lays its selects out as a horizontal grid on an inner wrapper,
  // so the open block is short — we no longer assert it is taller than the search
  // box; the search-box height cap is the real regression guard.)
  expect(searchBox!.height).toBeLessThanOrEqual(60);
});

test('content kb enhanced filters do not create desktop horizontal overflow', async ({ page }) => {
  if (test.info().project.name.includes('mobile')) {
    test.skip();
  }

  const response = await gotoOk(page, '/content-kb');
  expect(response?.ok()).toBeTruthy();

  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
});

for (const route of ['/deck-analysis', '/deck-primer', '/sync', '/card-lookup']) {
  test(`no horizontal overflow on key pages: ${route}`, async ({ page }) => {
    const response = await gotoOk(page, route);

    expect(response?.ok()).toBeTruthy();
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  });
}

test('readonly output textareas get a readable height on mobile without uncapping inputs', async ({ page }) => {
  const isMobile = test.info().project.name.includes('mobile');
  await gotoOk(page, '/judge-questions');

  await page.locator('[data-judge-result]').evaluate((element) => element.classList.remove('hidden'));

  const outputHeight = await page.locator('#judge-prompt-output').evaluate((element) => element.clientHeight);
  const viewportHeight = await page.evaluate(() => window.innerHeight);

  if (isMobile) {
    expect(outputHeight).toBeGreaterThanOrEqual(0.4 * viewportHeight);

    const inputHeight = await page.locator('#judge-question-text').evaluate((element) => element.clientHeight);
    expect(inputHeight).toBeLessThanOrEqual(200);
  } else {
    expect(outputHeight).toBeGreaterThanOrEqual(200);
    expect(outputHeight).toBeLessThanOrEqual(400);
  }
});
