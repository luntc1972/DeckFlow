import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { getToolEnabled, setToolEnabled } from './support/admin-tools';
import { expandCutLabSection, expandMobileCollapsibles } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { cutLabPool } from './fixtures/cut-lab-pool';

// E2E coverage for the Step 3 deck-archetype picker and its plan-apply round trip at desktop
// and mobile viewports. The test intentionally does not depend on commander-theme availability.
// Admin creds: read from FEEDBACK_ADMIN_USER / FEEDBACK_ADMIN_PASSWORD env vars. The Cut Lab
// feature flag is enabled transiently and restored after each test.

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let cutLabWasEnabled: boolean | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  cutLabWasEnabled = null;
  heldLock = await acquireAdminLockForTest(page);
  cutLabWasEnabled = await getToolEnabled(page, 'Cut Lab');
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async ({ page }) => {
  try {
    if (cutLabWasEnabled !== null) {
      await setToolEnabled(page, 'Cut Lab', cutLabWasEnabled);
    }
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
    cutLabWasEnabled = null;
  }
});

const importPool = async (page: Page): Promise<void> => {
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(cutLabPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();
  await expandCutLabSection(page, 'cut-lab-section-plan-panel');
  await expect(page.locator('[data-cut-lab-plan-panel]')).toBeVisible({ timeout: 30_000 });
};

test('Archetype picker renders and applies Stax on desktop and mobile', async ({ page }) => {
  await importPool(page);
  await expandMobileCollapsibles(page);

  const archetypes = page.locator('[data-cut-lab-plan-archetypes] input[type="radio"]');
  await expect(archetypes).toHaveCount(8);
  await expect(page.locator('[data-cut-lab-plan-archetypes] input[name="PlanArchetype"][value=""]')).toBeChecked();

  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  const proposalHeading = page.locator('.cutlab-proposal__heading');
  await expect(proposalHeading).toBeVisible({ timeout: 30_000 });
  await proposalHeading.evaluate(heading => heading.setAttribute('data-e2e-before', '1'));
  await expandCutLabSection(page, 'cut-lab-section-plan-panel');

  const staxRadio = page.locator('[data-cut-lab-plan-archetypes] input[name="PlanArchetype"][value="stax"]');
  const applied = page.waitForResponse(response =>
    response.url().includes('/api/cut-lab/plan-apply') && response.request().method() === 'POST');
  try {
    await staxRadio.locator('xpath=..').click();
    const response = await applied;
    expect(response.ok()).toBe(true);
  } finally {
    applied.catch(() => undefined);
  }

  const staxRow = staxRadio.locator('xpath=..');
  const staxChoiceBadge = staxRow.locator('[data-cut-lab-archetype-badge="choice"]');
  await expect(staxChoiceBadge).toBeVisible();
  await expect(staxChoiceBadge).toHaveText('Your choice');
  const choiceBadges = page.locator('[data-cut-lab-archetype-badge="choice"]');
  await expect.poll(async () => choiceBadges.evaluateAll(badges => badges.filter(badge => {
    const row = badge.closest('label');
    return row?.querySelector('input[name="PlanArchetype"]')?.getAttribute('value') !== 'stax'
      && !badge.classList.contains('hidden');
  }).length)).toBe(0);
  await expect(page.locator('[data-cut-lab-archetype-notice]')).toBeVisible();
  await expect(page.locator('[data-cut-lab-archetype-notice]')).toHaveText(/^Goals set to Stax defaults/);

  const staxStrategy = page.locator('[data-cut-lab-plan-panel] input[name="PlanStrategies"][value="stax"]');
  const staxStrategyRow = staxStrategy.locator('xpath=..');
  await expect(staxStrategy).not.toBeChecked();
  await expect(staxStrategyRow).toHaveClass(/cut-lab-plan-panel__row--implied/);
  await expect(staxStrategyRow.locator('.cut-lab-plan-panel__badge--implied')).toHaveText('Included by Stax');
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await expect(page.locator('.cutlab-proposal__heading[data-e2e-before="1"]')).toHaveCount(0);
  await expandCutLabSection(page, 'cut-lab-section-plan-panel');

  const controlRadio = page.locator('[data-cut-lab-plan-archetypes] input[name="PlanArchetype"][value="control"]');
  const controlApplied = page.waitForResponse(response =>
    response.url().includes('/api/cut-lab/plan-apply') && response.request().method() === 'POST');
  try {
    await controlRadio.locator('xpath=..').click();
    const response = await controlApplied;
    expect(response.ok()).toBe(true);
  } finally {
    controlApplied.catch(() => undefined);
  }

  await expect(controlRadio.locator('xpath=..').locator('[data-cut-lab-archetype-badge="choice"]')).toBeVisible();
  await expect(staxChoiceBadge).not.toBeVisible();

  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await expect(page.locator('.cutlab-proposal__heading[data-e2e-before="1"]')).toHaveCount(0);
  await expect(proposalHeading).toBeVisible({ timeout: 30_000 });
  await expect(proposalHeading).not.toBeEmpty();
});
