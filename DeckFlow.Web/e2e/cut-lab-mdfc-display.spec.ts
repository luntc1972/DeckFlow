import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { resolveE2EPort } from './support/e2e-port';

const baseUrl = `http://localhost:${resolveE2EPort()}`;
const fell = 'Fell the Profane // Fell Mire';
const fellDisplay = 'Fell the Profane / Fell Mire';
const shatterskull = 'Shatterskull Smashing // Shatterskull, the Hammer Pass';
const shatterskullDisplay = 'Shatterskull Smashing / Shatterskull, the Hammer Pass';
const oversizedPool = [
  'Commander', '1 Lord Windgrace', '', 'Deck', '98 Swamp',
  `1 ${fell}`, `1 ${shatterskull}`,
].join('\n');

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;
let heldLock: LockHandle | null = null;
test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

const importPool = async (page: Page): Promise<void> => {
  await page.goto(`${baseUrl}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
};

const acceptMdfc = async (page: Page): Promise<void> => {
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await expect(page.locator('.cutlab-proposal__heading')).toContainText(shatterskullDisplay);
  const response = page.waitForResponse(response =>
    response.url().endsWith('/api/cut-lab/decide') && response.request().method() === 'POST');
  await page.locator('.cutlab-proposal .cutlab-decision-btn--accept').click();
  expect((await response).ok()).toBeTruthy();
  await expect(page.locator('.cutlab-proposal__heading')).toContainText('at 100 cards');
};

test('keeps MDFC display names separate from canonical state, decisions and exports', async ({ page }) => {
  test.setTimeout(120_000);
  page.setDefaultTimeout(15_000);
  await importPool(page);
  const fellRow = page.locator(`tr[data-cut-lab-card="${fell}"]`);
  const state = page.locator('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]');

  await test.step('1: pool display and canonical attributes', async () => {
    await expect(fellRow).toBeVisible();
    await expect(fellRow).toContainText(fellDisplay);
    await expect(fellRow).not.toContainText(' // ');
    await expect(fellRow).toHaveAttribute('data-cut-lab-card', fell);
    await expect(fellRow.locator('[data-cutlab-card-open]')).toHaveAttribute('data-cutlab-card-open', fell);
    await expect(fellRow.locator('input[data-cut-lab-lock-card]')).toHaveValue(fell);
    console.info('MDFC step 1 passed');
  });

  await test.step('2: both separator forms find the pool row', async () => {
    const search = page.locator('.cutlab-pool-search');
    // Prove filtering ran before checking either matching form.
    await search.fill('no such MDFC card');
    await expect(fellRow).toHaveAttribute('hidden', '');
    for (const query of ['Profane // Fell', 'Profane / Fell']) {
      await search.fill(query);
      await expect(fellRow).not.toHaveAttribute('hidden');
      await expect(fellRow).toBeVisible();
    }
    await search.fill('');
    console.info('MDFC step 2 passed');
  });

  await test.step('3: locked canonical state survives a page reload', async () => {
    await fellRow.locator('input[data-cut-lab-lock-card]').check();
    // Protect every other eligible card so the proposal deterministically selects the MDFC.
    for (const name of ['Swamp', 'Lord Windgrace']) {
      const lock = page.locator(`tr[data-cut-lab-card="${name}"] input[data-cut-lab-lock-card]`);
      if (await lock.isEnabled()) await lock.check();
    }
    expect(await state.inputValue()).toContain(fell);
    expect(await state.inputValue()).not.toContain(fellDisplay);
    // Recalculate submits the UI's serialized locks before reloading the server-rendered workspace.
    await expandCutLabSection(page, 'cut-lab-section-role-floors');
    await page.locator('[data-cut-lab-recalculate]').click();
    await expect(page.locator('.cutlab-proposal__heading')).toContainText(shatterskullDisplay);
    await page.reload();
    await expandCutLabSection(page, 'cut-lab-section-lock-pool');
    await expect(fellRow.locator('input[data-cut-lab-lock-card]')).toBeChecked();
    await expect(fellRow).toContainText(fellDisplay);
    await expect(fellRow).not.toContainText(' // ');
    await expect(state).toHaveValue(new RegExp(fell));
    console.info('MDFC step 3 passed');
  });

  await test.step('4: cut and restore preserve canonical request names', async () => {
    await acceptMdfc(page);
    // AJAX creates this details element without the server-rendered section ID.
    const cuts = page.locator('details.cutlab-cuts-made');
    if (await cuts.getAttribute('open') === null) {
      await cuts.locator(':scope > summary').click();
    }
    const cutRow = page.locator('.cutlab-cuts-made__row').filter({
      has: page.locator(`[data-cutlab-card-open="${shatterskull}"]`),
    });
    await expect(cutRow).toBeVisible();
    await expect(cutRow).toContainText(shatterskullDisplay);
    await expect(cutRow).not.toContainText(' // ');
    await expect(cutRow.locator('[data-cutlab-card-open]')).toHaveAttribute('data-cutlab-card-open', shatterskull);
    const restoreRequest = page.waitForRequest(request =>
      request.url().endsWith('/api/cut-lab/decide') && request.method() === 'POST'
      && request.postDataJSON().decision === 'restore');
    await cutRow.locator('[data-cut-lab-restore]').click();
    const request = await restoreRequest;
    expect(request.postDataJSON().cardName).toBe(shatterskull);
    expect(request.postData()).toContain(shatterskull);
    expect(request.postData()).not.toContain(shatterskullDisplay);
    await expect(cutRow).toHaveCount(0);
    await expect(page.locator('[data-cut-lab-restore-confirmation]')).toContainText(`${shatterskullDisplay} restored`);
    await expect(page.locator('[data-cut-lab-restore-confirmation]')).not.toContainText(' // ');
    console.info('MDFC step 4 passed');
  });

  await test.step('5: export retains the canonical MDFC name', async () => {
    await acceptMdfc(page);
    await page.locator('#cut-lab-step-tab-5').click();
    await expandCutLabSection(page, 'cut-lab-section-export');
    const exported = page.locator('#cut-lab-export-moxfield-full');
    await expect(exported).toBeVisible();
    expect(await exported.inputValue()).toContain(fell);
    expect(await exported.inputValue()).not.toContain(fellDisplay);
    console.info('MDFC step 5 passed');
  });
});
