import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { cutLabPool } from './fixtures/cut-lab-pool';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;
let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
  await page.goto('/cut-lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(cutLabPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();
  await expect(page.locator('#cut-lab-step-tab-3')).toBeVisible({ timeout: 30_000 });
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('reference tools preserve Decide, scroll, live floors, section position and opener focus', async ({ page }) => {
  const sections = ['role-floors', 'structural', 'constraints'] as const;
  const originalNodes = await Promise.all(sections.map(id =>
    page.locator(`#cut-lab-section-${id}`).evaluateHandle(section => ({
      section,
      parent: section.parentNode,
      next: section.nextSibling,
    }))));

  await page.getByRole('tab', { name: 'Decide' }).click();
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await page.locator('#cut-lab-section-cut-rounds').scrollIntoViewIfNeeded();
  await page.evaluate(() => window.scrollBy({ top: 200, behavior: 'instant' }));

  const dialog = page.locator('dialog#cut-lab-reference');
  const decide = page.locator('#cut-lab-step-panel-3');
  for (const [index, id] of sections.entries()) {
    const opener = page.locator(`[data-cut-lab-reference-open="cut-lab-section-${id}"]`);
    await expect(opener).toBeInViewport();
    const scrollY = await page.evaluate(() => window.scrollY);
    await opener.click();
    await expect(dialog).toHaveAttribute('open', '');
    const section = dialog.locator(`#cut-lab-section-${id}`);
    await expect(section).toHaveAttribute('open', '');
    await expect(section).toBeVisible();
    await expect(decide).not.toHaveAttribute('hidden', '');
    await expect(page.locator('#cut-lab-step-tab-3')).toHaveAttribute('aria-selected', 'true');
    await expect.poll(async () => Math.abs(await page.evaluate(() => window.scrollY) - scrollY), { message: `${id}: reference preserves page scroll` }).toBeLessThanOrEqual(2);

    if (id === 'role-floors') {
      const input = section.locator('input[data-cut-lab-floor]').first();
      const role = await input.getAttribute('data-cut-lab-floor');
      const newFloor = Number(await input.inputValue()) === 1 ? '2' : '1';
      await input.fill(newFloor);
      await input.press('Tab');
      const row = section.locator(`tr[data-cut-lab-floor-row="${role}"]`);
      await expect(row).toHaveAttribute('data-cut-lab-floor-user-set', 'true');
      await expect(row.locator('[data-cut-lab-floor-adjusted-badge]')).toBeVisible();
      await expect.poll(() => page.locator('input[name="CutLabStateJson"]').evaluateAll((inputs, { role, newFloor }) =>
        inputs.some(input => {
          const state = JSON.parse((input as HTMLInputElement).value || '{}');
          return state.roleFloors?.some((floor: { role: string; floor: number }) => floor.role === role && floor.floor === Number(newFloor));
        }), { role, newFloor })).toBe(true);
    }

    if (id === 'structural') {
      const card = section.locator('[data-cutlab-card-open]').first();
      await expect(card).toBeVisible();
      await card.click();
      const cardDialog = page.locator('dialog#cutlab-card-modal');
      await expect(cardDialog).toHaveAttribute('open', '');
      await expect(dialog).toHaveAttribute('open', '');
      expect(await cardDialog.evaluate(node => node.matches(':modal'))).toBe(true);
      await page.keyboard.press('Escape');
      await expect(cardDialog).not.toHaveAttribute('open', '');
      await expect(dialog).toHaveAttribute('open', '');
      await expect(card).toBeFocused();
    }

    await page.keyboard.press('Escape');
    await expect(dialog).not.toHaveAttribute('open', '');
    await expect.poll(() => originalNodes[index].evaluate(({ section, parent, next }) =>
      section.parentNode === parent && section.nextSibling === next)).toBe(true);
    if (id !== 'constraints') {
      await expect(page.locator(`#cut-lab-step-panel-1 #cut-lab-section-${id}`)).toBeAttached();
    }
    await expect(opener).toBeFocused();
    await expect(decide).not.toHaveAttribute('hidden', '');
    await expect.poll(async () => Math.abs(await page.evaluate(() => window.scrollY) - scrollY), { message: `${id}: reference preserves page scroll` }).toBeLessThanOrEqual(2);
  }
  for (const node of originalNodes) await node.dispose();
});

test('desktop dialogs stay vertically centred', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'chromium-desktop', 'Desktop grid regression; mobile uses separate dialog layout.');
  await page.getByRole('tab', { name: 'Process' }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await page.locator('#cut-lab-section-lock-pool [data-cutlab-card-open]').filter({ hasText: 'Command Tower' }).first().click();
  const cardDialog = page.locator('dialog#cutlab-card-modal');
  await expect(cardDialog).toHaveAttribute('open', '');
  const assertCentred = async (selector: string): Promise<void> => {
    const box = await page.locator(selector).boundingBox();
    expect(box).not.toBeNull();
    const top = box!.y;
    const bottom = page.viewportSize()!.height - (box!.y + box!.height);
    expect.soft(Math.abs(top - bottom), `${selector}: top=${top}, bottom=${bottom}`).toBeLessThanOrEqual(2);
  };
  await assertCentred('dialog#cutlab-card-modal');
  await page.keyboard.press('Escape');
  await expect(cardDialog).not.toHaveAttribute('open', '');
  await page.getByRole('tab', { name: 'Decide' }).click();
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await page.locator('[data-cut-lab-reference-open="cut-lab-section-role-floors"]').click();
  await expect(page.locator('dialog#cut-lab-reference')).toHaveAttribute('open', '');
  await assertCentred('dialog#cut-lab-reference');
});
