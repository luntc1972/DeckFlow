import { expect, test, type Browser, type Locator, type Page } from '@playwright/test';
import { join } from 'node:path';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection, expandMobileCollapsibles } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { uiDesignDir } from './support/ui-design-dir';

import { resolveE2EPort } from './support/e2e-port';

const baseUrl = `http://localhost:${resolveE2EPort()}`;
const screenshotDir = uiDesignDir('cut-lab');
const desktopViewport = { width: 1280, height: 900 };
const mobileViewport = { width: 390, height: 844 };

const themes = [
  { name: 'classic', cookie: 'site.css' },
  { name: 'nyx', cookie: 'site-nyx.css' },
  { name: 'commander-table', cookie: 'site-commander-table.css' },
] as const;

const oversizedPool = [
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
  '1 Dovin\'s Veto',
  '1 Demonic Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;
type NoJsPageHandle = {
  page: Page;
  close: () => Promise<void>;
};

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

const importPool = async (page: Page): Promise<void> => {
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();

  await page.getByRole('tab', { name: 'Process' }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expandCutLabSection(page, 'cut-lab-section-competes');
  await page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' }).locator(':scope > summary').click();
  await expect(page.locator('[data-cut-lab-lock-role="lands"]')).toBeVisible();
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

const importPoolNoJs = async (page: Page): Promise<void> => {
  await page.goto(`${baseUrl}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.evaluate(() => {
    document.querySelector('[data-sync-panel="cut-lab-deck-text"]')?.classList.remove('hidden');
    document.querySelector('[data-sync-panel="cut-lab-deck-url"]')?.classList.add('hidden');
  });
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expandCutLabSection(page, 'cut-lab-section-lock-pool', false);
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 60_000 });
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

const buildNoJsPage = async (browser: Browser): Promise<NoJsPageHandle> => {
  const context = await browser.newContext({
    javaScriptEnabled: false,
    viewport: mobileViewport,
    httpCredentials: {
      username: process.env.FEEDBACK_ADMIN_USER ?? 'admin',
      password: process.env.FEEDBACK_ADMIN_PASSWORD ?? 'changeme-local',
      send: 'always',
    },
  });
  const page = await context.newPage();
  return {
    page,
    close: () => context.close(),
  };
};

const clearClientState = async (page: Page): Promise<void> => {
  await page.goto(baseUrl);
  await page.evaluate(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
  });
};

const ensureDetailsOpen = async (details: Locator): Promise<void> => {
  if ((await details.getAttribute('open')) !== null) {
    return;
  }

  await details.locator(':scope > summary').click();
  await expect(details).toHaveAttribute('open', '');
};

const assertNoOverlap = (first: { x: number; y: number; width: number; height: number }, second: { x: number; y: number; width: number; height: number }, message: string): void => {
  const overlaps = !(
    first.x + first.width <= second.x
    || second.x + second.width <= first.x
    || first.y + first.height <= second.y
    || second.y + second.height <= first.y
  );
  expect(overlaps, message).toBe(false);
};

const getBoundingBox = async (locator: Locator, name: string): Promise<{ x: number; y: number; width: number; height: number }> => {
  const box = await locator.boundingBox();
  expect(box, `${name} should have a bounding box`).not.toBeNull();
  return box!;
};

const createLockedFastManaPackage = async (page: Page): Promise<void> => {
  const packagesDetails = page.locator('#cut-lab-section-packages');
  await ensureDetailsOpen(packagesDetails);

  const packagePanel = page.locator('[data-cut-lab-package-id]').filter({ hasText: 'Fast mana' });
  if (!(await packagePanel.count())) {
    await page.locator('select[data-cut-lab-package-card="Sol Ring"]').selectOption('__new__');
    await page.locator('[data-cut-lab-new-package-input]').fill('Fast mana');
    await page.locator('[data-cut-lab-new-package-save]').click();
    await page.locator('select[data-cut-lab-package-card="Arcane Signet"]').selectOption({ label: 'Fast mana' });
  }

  const fastManaPanel = page.locator('[data-cut-lab-package-id]').filter({ hasText: 'Fast mana' });
  await expect(fastManaPanel).toBeVisible({ timeout: 30_000 });
  await fastManaPanel.locator('input[data-cut-lab-package-toggle]').check();
  await expect(fastManaPanel.locator('input[data-cut-lab-package-toggle]')).toBeChecked();
};

const driveOneJsDecide = async (page: Page): Promise<void> => {
  const decideResponse = page.waitForResponse(response =>
    response.url().includes('/api/cut-lab/decide') && response.request().method() === 'POST');

  await page.locator('.cutlab-proposal .cutlab-decision-btn--accept').click();

  const response = await decideResponse;
  expect(response.ok(), 'Cut Lab decide request must succeed before review capture').toBeTruthy();
  await page.getByRole('tab', { name: 'Process' }).click();
  await expect(page.locator('[data-cut-lab-structural-findings]')).toBeVisible();
  expect(
    await page.locator('.cutlab-proposal__evidence .kb-chip, [data-cut-lab-package-id] .kb-chip').count(),
    'Review capture should include chips after one JS decide and package creation',
  ).toBeGreaterThan(0);
};

const prepareReviewCapture = async (page: Page): Promise<void> => {
  await importPool(page);
  await expandMobileCollapsibles(page);
  await createLockedFastManaPackage(page);
  await page.getByRole('tab', { name: 'Decide' }).click();
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await driveOneJsDecide(page);

  await page.getByRole('tab', { name: 'Process' }).click();
  await expandCutLabSection(page, 'cut-lab-section-competes');
  const landsGroup = page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' });
  await ensureDetailsOpen(landsGroup);
  await expect(landsGroup.locator('[data-cut-lab-lock-role="lands"]')).toBeVisible();
  await expect(landsGroup.locator('[data-cut-lab-chip-card="Plains"]')).toBeVisible();
  await expect(landsGroup.locator('[data-cut-lab-chip-card="Island"]')).toBeVisible();
  await expect(page.locator('#cut-lab-section-packages')).toHaveAttribute('open', '');
  await expect(page.locator('[data-cut-lab-package-id]').filter({ hasText: 'Fast mana' })).toBeVisible();

  await page.getByRole('tab', { name: 'Decide' }).click();
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await page.locator('#cut-lab-section-cut-rounds').scrollIntoViewIfNeeded();
  await expect(page.locator('.cutlab-sticky-bar[data-cut-lab-sticky-target]')).toBeVisible();
  await expect(page.locator('[data-cut-lab-sticky-remaining]')).toBeVisible();
};

const openCardModal = async (trigger: Locator, page: Page, cardName: string): Promise<Locator> => {
  const modal = page.locator('dialog#cutlab-card-modal');
  await trigger.click();
  await expect(modal).toHaveAttribute('open', '');
  await expect(modal.locator('#cutlab-card-modal-title')).toHaveText(cardName);
  await expect(modal.locator('[data-cutlab-modal-oracle]')).toBeVisible();
  return modal;
};

const acceptProposalNoJs = async (page: Page): Promise<string> => {
  const heading = await page.locator('.cutlab-proposal__heading').textContent();
  const cardName = heading?.replace(/^Proposed cut:\s*/, '').trim() ?? '';
  const decideRequestPromise = page.waitForRequest(request =>
    request.isNavigationRequest()
    && request.method() === 'POST'
    && request.url().includes('/cut-lab/decide'),
  );
  const navigationPromise = page.waitForNavigation({ waitUntil: 'domcontentloaded' });

  await page.locator('.cutlab-proposal .cutlab-decision-btn--accept').click();

  const [decideRequest] = await Promise.all([decideRequestPromise, navigationPromise]);
  expect(decideRequest.url()).toContain('/cut-lab/decide');
  await expect(page.locator('.cutlab-cuts-made__row')).toContainText(cardName);
  return cardName;
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('captures cross-theme mobile chrome and back-to-top tap-target coverage for Cut Lab', async ({ page }) => {
  await page.setViewportSize(mobileViewport);

  for (const theme of themes) {
    await page.context().clearCookies();
    await page.context().addCookies([{ name: 'deckflow-theme', value: theme.cookie, url: baseUrl }]);

    await page.setViewportSize(desktopViewport);
    await importPool(page);
    const tablist = page.locator('.cutlab-step-rail [role="tablist"]');
    await expect(tablist).toBeVisible();
    const desktopTabs = await getBoundingBox(tablist, 'Desktop step tabs');
    const results = await getBoundingBox(page.locator('#cut-lab-step-panel-1'), 'Process panel');
    expect(desktopTabs.x + desktopTabs.width, `${theme.name}: tabs occupy the left column`).toBeLessThanOrEqual(results.x);
    await page.locator('#cut-lab-step-tab-1').focus();
    await page.keyboard.press('ArrowDown');
    await expect(page.locator('#cut-lab-step-tab-2')).toHaveAttribute('aria-selected', 'true');
    await expect(page.locator('#cut-lab-step-panel-2')).toBeVisible();
    await page.setViewportSize(mobileViewport);
    await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
    await expandMobileCollapsibles(page);

    const rail = page.locator('.cutlab-step-rail');
    const stickyBar = page.locator('.cutlab-sticky-bar[data-cut-lab-sticky-target]');
    const backToTopButton = page.locator('#back-to-top-button');
    const poolFilter = page.locator('.cutlab-pool-filter');
    const poolTableHeader = page.locator('#cut-lab-section-lock-pool .conflicts-table thead');
    const commandTowerTrigger = page.locator('button.cutlab-card-link[data-cutlab-card-open="Command Tower"]').first();

    await expect(tablist).toBeVisible();
    expect(await rail.evaluate(node => getComputedStyle(node).position)).toBe('fixed');
    const bottomBar = await getBoundingBox(rail, 'Bottom step bar');
    expect(bottomBar.y + bottomBar.height).toBeCloseTo(mobileViewport.height, 0);
    await page.evaluate(() => window.scrollTo({ top: document.documentElement.scrollHeight, behavior: 'instant' }));
    const lastControl = page.locator('.cutlab-workspace button:visible:enabled, .cutlab-workspace input:visible:enabled, .cutlab-workspace select:visible:enabled, .cutlab-workspace textarea:visible:enabled, .cutlab-workspace a[href]:visible').filter({ hasNot: page.locator('[role="tab"]') });
    // The rail is earlier in DOM order than the result controls.
    const lastControlBox = await getBoundingBox(lastControl.last(), 'Last page control');
    expect(lastControlBox.y + lastControlBox.height, `${theme.name}: last control clears the bottom bar`).toBeLessThanOrEqual(bottomBar.y);

    await page.locator('#cut-lab-section-cut-rounds').scrollIntoViewIfNeeded();
    await page.evaluate(() => window.scrollBy({ top: 200, behavior: 'instant' }));
    await expect(stickyBar).toBeVisible();
    await expect(backToTopButton).toBeVisible();
    const backToTopBox = await getBoundingBox(backToTopButton, 'Back-to-top button');
    expect(backToTopBox.width, `${theme.name}: back-to-top button should be at least 44px wide`).toBeGreaterThanOrEqual(44);
    expect(backToTopBox.height, `${theme.name}: back-to-top button should be at least 44px tall`).toBeGreaterThanOrEqual(44);
    assertNoOverlap(bottomBar, backToTopBox, `${theme.name}: bottom bar should not overlap the back-to-top button`);

    const stickyBarBox = await getBoundingBox(stickyBar, 'Sticky bar');
    expect(Math.abs(stickyBarBox.y), `${theme.name}: stuck bar starts at viewport top`).toBeLessThanOrEqual(4);

    await page.getByRole('tab', { name: 'Process' }).click();
    await expandCutLabSection(page, 'cut-lab-section-lock-pool');
    await expect(poolFilter).toBeVisible();
    await expect(page.locator('.cutlab-pool-search')).toBeVisible();
    await expect(page.locator('.cutlab-pool-match-count')).toBeVisible();
    const poolFilterBox = await getBoundingBox(poolFilter, 'Pool filter');
    const poolHeaderBox = await getBoundingBox(poolTableHeader, 'Pool table header');
    expect(poolFilterBox.y + poolFilterBox.height, `${theme.name}: pool filter should sit above the table header`).toBeLessThanOrEqual(poolHeaderBox.y);

    const cardModal = await openCardModal(commandTowerTrigger, page, 'Command Tower');
    await expect(cardModal.locator('[data-cutlab-modal-oracle]')).toContainText('Add one mana');
    await cardModal.locator('[data-cutlab-modal-close]').click();
    await expect(cardModal).not.toHaveAttribute('open', '');

    await page.screenshot({
      path: join(screenshotDir, `cut-lab-nav-${theme.name}-mobile.png`),
      fullPage: true,
    });
  }
});

test('captures Lock your pool review screenshots across themes at desktop and mobile', async ({ page }) => {
  for (const theme of themes) {
    for (const viewport of [
      { name: 'desktop', size: desktopViewport },
      { name: 'mobile', size: mobileViewport },
    ] as const) {
      await page.setViewportSize(viewport.size);
      await page.context().clearCookies();
      await clearClientState(page);
      await page.context().addCookies([{ name: 'deckflow-theme', value: theme.cookie, url: baseUrl }]);

      await prepareReviewCapture(page);
      await page.screenshot({
        path: join(screenshotDir, `cut-lab-review-${theme.name}-${viewport.name}.png`),
        fullPage: true,
      });
    }
  }
});

test("shows a newly created package in another card's visible package widget without reload", async ({ page }) => {
  await importPool(page);

  const packagesDetails = page.locator('#cut-lab-section-packages');
  await ensureDetailsOpen(packagesDetails);

  await page.locator('select[data-cut-lab-package-card="Sol Ring"]').selectOption('__new__');
  await page.locator('[data-cut-lab-new-package-input]').fill('Fast mana');
  await page.locator('[data-cut-lab-new-package-save]').click();
  await expect(page.locator('[data-cut-lab-package-id]').filter({ hasText: 'Fast mana' })).toBeVisible({ timeout: 30_000 });

  const otherCardWidget = page
    .locator('select[data-cut-lab-package-card="Fellwar Stone"]')
    .locator('xpath=preceding-sibling::div[contains(@class, "df-select")][1]');
  const otherCardTrigger = otherCardWidget.locator('button.df-select__trigger');
  const otherCardListbox = otherCardWidget.getByRole('listbox');

  await otherCardTrigger.click();
  await expect(otherCardListbox.getByRole('option', { name: 'Fast mana' })).toBeVisible();
});

test('proves the no-JS Cut Lab navigation and card-trigger fallbacks', async ({ browser }) => {
  // No-JS decide/adjust POSTs run the sim-heavy pipeline server-side on the
  // default Debug build; reaching the 100-card Export gate takes several native
  // round-trips, so this progressive-enhancement test needs headroom beyond the
  // 120s default (mirrors the config comment about Debug-build decide latency).
  test.setTimeout(240_000);
  const noJs = await buildNoJsPage(browser);

  try {
    await importPoolNoJs(noJs.page);

    // (e) CLUP-07 no-JS submit fallback (D-04 SubmitFormId seam) — run first, on
    // the freshly imported page (before the nav/collapse/disclosure interactions
    // below mutate state), mirroring the proven cut-lab-structure no-JS accept.
    // The Export step-tab is the only type="submit" tab but enables only at
    // exactly 100 cards, requiring several sim-heavy no-JS decide round-trips
    // (impractically slow/flaky here, and pre-Phase-110 export plumbing). Prove
    // the same server-authored native-submit seam via the accept decision form —
    // a plain <button type="submit"> posting to /cut-lab/decide with no script:
    // acceptProposalNoJs asserts the native POST navigation and the server
    // re-render (accepted card in the cuts-made list). Confirm the submit-type
    // Export tab is correctly gated first. Full 100-card Export-gate submit stays
    // in manual/checkpoint coverage.
    await expect(noJs.page.locator('#cut-lab-step-tab-5')).toBeDisabled();
    await expect(noJs.page.locator('#cut-lab-step-tab-5')).toHaveAttribute('type', 'submit');
    await expect(noJs.page.locator('#cut-lab-step-tab-5')).toHaveAttribute('form', 'cut-lab-export-form');
    await acceptProposalNoJs(noJs.page);

    for (const id of ['role-floors', 'structural', 'constraints']) {
      await expect(noJs.page.locator(`#cut-lab-section-${id}`)).toBeAttached();
    }
    await expect(noJs.page.locator('#cut-lab-step-panel-1 #cut-lab-section-role-floors')).toBeAttached();
    await expect(noJs.page.locator('#cut-lab-step-panel-1 #cut-lab-section-structural')).toBeAttached();
    await expect(noJs.page.locator('#cut-lab-reference')).not.toHaveAttribute('open', '');

    const mobileDetails = noJs.page.locator('#cut-lab-section-cut-rounds');
    await expect(mobileDetails).toHaveAttribute('open', '');
    await mobileDetails.locator('> summary').click();
    await expect(mobileDetails).not.toHaveAttribute('open', '');
    await mobileDetails.locator('> summary').click();
    await expect(mobileDetails).toHaveAttribute('open', '');
    await mobileDetails.locator('> summary').click();
    await expect(mobileDetails).not.toHaveAttribute('open', '');

    const poolFilter = noJs.page.locator('.cutlab-pool-filter');
    await expect(poolFilter).toHaveAttribute('hidden', '');
    const poolRows = noJs.page.locator('tr[data-cut-lab-card]');
    expect(await poolRows.count()).toBeGreaterThan(0);
    const allRowsVisible = await poolRows.evaluateAll(rows =>
      rows.every(row => !row.hasAttribute('hidden') && getComputedStyle(row).display !== 'none'));
    expect(allRowsVisible).toBe(true);

    // The inline card-text disclosure was removed in favor of the JS-driven modal,
    // so no-JS coverage now verifies the card trigger remains present after the
    // collapsible sections are re-opened.
    await expandMobileCollapsibles(noJs.page);
    await expect(noJs.page.locator('.cutlab-card-text')).toHaveCount(0);
    const commandTowerTrigger = noJs.page.locator('button.cutlab-card-link[data-cutlab-card-open="Command Tower"]').first();
    await expect(commandTowerTrigger).toBeVisible();
    await expect(commandTowerTrigger).toHaveAttribute('data-cutlab-card-open', 'Command Tower');
  } finally {
    await noJs.close();
  }
});

test('keeps pool rows hidden by search and lock filters', async ({ page }) => {
  await importPool(page);

  const solRing = page.locator('tr[data-cut-lab-card="Sol Ring"]');
  const arcaneSignet = page.locator('tr[data-cut-lab-card="Arcane Signet"]');
  const search = page.locator('.cutlab-pool-search');
  await search.fill('Sol Ring');
  await expect(arcaneSignet).toHaveAttribute('hidden', '');
  await expect(arcaneSignet).toBeHidden();
  await expect(solRing).toBeVisible();

  await search.fill('');
  await solRing.locator('[data-cut-lab-lock-card]').check();
  await arcaneSignet.locator('[data-cut-lab-lock-card]').uncheck();
  await page.locator('input[name="CutLabPoolFilter"][value="locked"]').check();
  await expect(arcaneSignet).toHaveAttribute('hidden', '');
  await expect(arcaneSignet).toBeHidden();
  await expect(solRing).toBeVisible();

  await page.locator('input[name="CutLabPoolFilter"][value="unlocked"]').check();
  await expect(solRing).toHaveAttribute('hidden', '');
  await expect(solRing).toBeHidden();
  await expect(arcaneSignet).toBeVisible();
});
