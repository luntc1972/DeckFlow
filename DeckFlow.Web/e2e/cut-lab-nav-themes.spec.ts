import { expect, test, type Browser, type Locator, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandMobileCollapsibles } from './support/cut-lab-mobile-collapse';

const baseUrl = 'http://localhost:5173';
const screenshotDir = resolve(__dirname, '../../.planning/ui-design/cut-lab/screenshots');
const mobileViewport = { width: 430, height: 2200 };

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
  await page.locator('#cut-lab-primary-plan').fill('Protect the control shell, then trim to the cleanest Zur line.');
  await page.locator('#cut-lab-secondary-plan').fill('Keep the fast mana package intact.');
  await page.locator('input[name="Bracket"][value="4"]').check();
  await page.locator('input[name="PlayExperience"][value="Focused"]').check();
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' }).locator('summary').click();
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
  await page.locator('#cut-lab-primary-plan').fill('Protect the control shell, then trim to the cleanest Zur line.');
  await page.locator('#cut-lab-secondary-plan').fill('Keep the fast mana package intact.');
  await page.locator('input[name="Bracket"][value="4"]').check();
  await page.locator('input[name="PlayExperience"][value="Focused"]').check();
  await page.getByRole('button', { name: 'Import pool' }).click();

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

test('captures cross-theme mobile chrome coverage for Cut Lab navigation and disclosures', async ({ page }) => {
  mkdirSync(screenshotDir, { recursive: true });
  await page.setViewportSize(mobileViewport);

  for (const theme of themes) {
    await page.context().clearCookies();
    await page.context().addCookies([{ name: 'deckflow-theme', value: theme.cookie, url: baseUrl }]);

    await importPool(page);
    await expandMobileCollapsibles(page);

    const anchorNav = page.locator('.cutlab-anchor-nav');
    const anchorLinks = page.locator('.cutlab-anchor-nav-list a');
    const stickyBar = page.locator('.cutlab-sticky-bar');
    const backToTopButton = page.locator('#back-to-top-button');
    const poolFilter = page.locator('.cutlab-pool-filter');
    const poolTableHeader = page.locator('#cut-lab-section-lock-pool .conflicts-table thead');
    const cardTextDetails = page.locator('.cutlab-card-text').first();

    await expect(anchorNav).toBeVisible();
    await expect(anchorLinks.first()).toBeVisible();

    await page.locator('#cut-lab-section-cut-rounds').scrollIntoViewIfNeeded();
    await expect(stickyBar).toBeVisible();

    const anchorNavPosition = await anchorNav.evaluate(node => getComputedStyle(node).position);
    expect(anchorNavPosition).toBe('sticky');

    const stuckNavBox = await getBoundingBox(anchorNav, 'Anchor nav');
    expect(stuckNavBox.y).toBeLessThanOrEqual(4);

    const linkMetrics = await anchorLinks.evaluateAll(links =>
      links.map(link => {
        const element = link as HTMLAnchorElement;
        return {
          text: element.textContent?.trim() ?? '',
          scrollWidth: element.scrollWidth,
          clientWidth: element.clientWidth,
        };
      }));
    for (const metric of linkMetrics) {
      expect(metric.scrollWidth, `${theme.name}: anchor pill text should not be clipped for "${metric.text}"`).toBeLessThanOrEqual(metric.clientWidth + 1);
    }

    // The back-to-top button is display:none below 600px (site-mobile.css
    // @media max-width:600px), so at the 430px mobile viewport it is not
    // rendered and cannot obscure the sticky nav. Assert the "not obscured"
    // invariant programmatically: EITHER the button is not displayed, OR it is
    // displayed and its box does not intersect the stuck nav.
    if (await backToTopButton.isVisible()) {
      const backToTopBox = await getBoundingBox(backToTopButton, 'Back-to-top button');
      assertNoOverlap(stuckNavBox, backToTopBox, `${theme.name}: anchor nav should not overlap the back-to-top button`);
    } else {
      expect(
        await backToTopButton.evaluate(node => getComputedStyle(node).display),
        `${theme.name}: back-to-top button should be display:none at mobile width so it cannot obscure the nav`,
      ).toBe('none');
    }

    const stickyBarBox = await getBoundingBox(stickyBar, 'Sticky bar');
    expect(stickyBarBox.y, `${theme.name}: sticky bar should start below the anchor nav when both are visible`).toBeGreaterThanOrEqual(stuckNavBox.y + stuckNavBox.height - 1);

    await expect(poolFilter).toBeVisible();
    await expect(page.locator('.cutlab-pool-search')).toBeVisible();
    await expect(page.locator('.cutlab-pool-match-count')).toBeVisible();
    const poolFilterBox = await getBoundingBox(poolFilter, 'Pool filter');
    const poolHeaderBox = await getBoundingBox(poolTableHeader, 'Pool table header');
    expect(poolFilterBox.y + poolFilterBox.height, `${theme.name}: pool filter should sit above the table header`).toBeLessThanOrEqual(poolHeaderBox.y);

    await expect(cardTextDetails).not.toHaveAttribute('open', '');
    await cardTextDetails.locator('.cutlab-card-text__summary').click();
    await expect(cardTextDetails).toHaveAttribute('open', '');
    await expect(cardTextDetails.locator('.cutlab-card-text__body')).toBeVisible();

    await page.screenshot({
      path: join(screenshotDir, `cut-lab-nav-${theme.name}-mobile.png`),
      fullPage: true,
    });
  }
});

test('proves the no-JS Cut Lab navigation and disclosure fallbacks', async ({ browser }) => {
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
    await expect(noJs.page.locator('#cut-lab-step-tab-4')).toBeDisabled();
    await expect(noJs.page.locator('#cut-lab-step-tab-4')).toHaveAttribute('type', 'submit');
    await expect(noJs.page.locator('#cut-lab-step-tab-4')).toHaveAttribute('form', 'cut-lab-export-form');
    await acceptProposalNoJs(noJs.page);

    const anchorLink = noJs.page.locator('.cutlab-anchor-nav-list a[href="#cut-lab-section-cut-rounds"]').first();
    await expect(anchorLink).toHaveAttribute('href', '#cut-lab-section-cut-rounds');
    await anchorLink.click();
    await expect.poll(() => new URL(noJs.page.url()).hash).toBe('#cut-lab-section-cut-rounds');
    const targetInView = await noJs.page.locator('#cut-lab-section-cut-rounds').evaluate(node => {
      const rect = node.getBoundingClientRect();
      return rect.top >= 0 && rect.top < window.innerHeight;
    });
    expect(targetInView).toBe(true);

    const mobileDetails = noJs.page.locator('details[data-cutlab-mobile-collapse]').first();
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

    // The collapse-toggle assertions above left the first section closed; re-open
    // all collapsibles so the card-text disclosure (which lives inside a pool row)
    // is in an expanded section and its native <details> body can render.
    await expandMobileCollapsibles(noJs.page);
    const cardTextDetails = noJs.page.locator('.cutlab-card-text').first();
    await expect(cardTextDetails).not.toHaveAttribute('open', '');
    await cardTextDetails.locator('.cutlab-card-text__summary').click();
    await expect(cardTextDetails).toHaveAttribute('open', '');
    await expect(cardTextDetails.locator('.cutlab-card-text__body')).toBeVisible();
  } finally {
    await noJs.close();
  }
});
