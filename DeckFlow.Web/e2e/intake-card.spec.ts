import { expect, test, type Locator, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { getToolEnabled, setToolEnabled } from './support/admin-tools';

const PASTED_DECK = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Brainstorm',
  '10 Plains',
  '10 Island',
  '10 Swamp',
].join('\n');

const BRACKET_LABEL = 'Intake Bracket Deck';
const HISTORY_LABEL = 'Intake History Deck';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let bracketWasEnabled = false;
let deckHistoryWasEnabled = false;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  bracketWasEnabled = await getToolEnabled(page, 'Bracket Check');
  deckHistoryWasEnabled = await getToolEnabled(page, 'Deck History');
  await setToolEnabled(page, 'Bracket Check', true);
  await setToolEnabled(page, 'Deck History', true);
});

test.afterEach(async ({ page }) => {
  try {
    await setToolEnabled(page, 'Bracket Check', bracketWasEnabled);
    await setToolEnabled(page, 'Deck History', deckHistoryWasEnabled);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

function intake(page: Page): Locator {
  return page.locator('.cutlab-intake');
}

async function expectEmptyIntake(page: Page): Promise<void> {
  const card = intake(page);
  const inputSource = card.locator('select[name="DeckInputSource"]');

  await expect(card.locator('.cutlab-intake-summary')).toHaveCount(0);
  await expect(card.getByText('Details', { exact: true })).toHaveCount(0);
  await expect(card.locator('form')).toBeVisible();
  await expect(inputSource).toHaveValue('PublicUrl');
  await expect(card.locator('[data-sync-panel$="-deck-url"]')).toBeVisible();
  await expect(card.locator('textarea[name="DeckText"]')).toBeHidden();
}

async function setPastedDeck(page: Page): Promise<void> {
  const card = intake(page);
  await card.locator('select[name="DeckInputSource"]').selectOption('PasteText');
  await card.locator('textarea[name="DeckText"]').fill(PASTED_DECK);
}

async function expectRestoredPasteText(page: Page): Promise<void> {
  const card = intake(page);
  await expect(card.locator('select[name="DeckInputSource"]')).toHaveValue('PasteText');
  await expect(card.locator('[data-sync-panel$="-deck-text"]')).toBeVisible();
  await expect(card.locator('[data-sync-panel$="-deck-url"]')).toBeHidden();
}

async function expectCollapsedResult(page: Page, label: string): Promise<void> {
  const card = page.locator('details.cutlab-intake');
  const summary = card.locator('.cutlab-intake-summary');

  await expect(card).toBeAttached();
  await expect(card).not.toHaveAttribute('open');
  await expect(summary).toContainText(label);
  await expect(summary).toContainText('Edit');

  const summaryBox = await summary.boundingBox();
  expect(summaryBox?.height).toBeGreaterThanOrEqual(44);
  await expect(page.locator('#results')).toBeFocused();

  await summary.click();
  await expect(card.locator('form')).toBeVisible();
  await expect(card.locator('textarea[name="DeckText"]')).toHaveValue(PASTED_DECK);

  await summary.click();
  await expect(card).not.toHaveAttribute('open');
}

for (const path of ['/manabase', '/bracket', '/deck-history']) {
  test(`${path} empty intake shows the public URL form`, async ({ page }) => {
    await page.goto(path);
    await expectEmptyIntake(page);
  });
}

test('/bracket result collapses intake and restores pasted deck when edited', async ({ page }) => {
  await page.goto('/bracket');
  await setPastedDeck(page);
  await intake(page).locator('input[name="DeckName"]').fill(BRACKET_LABEL);
  await page.getByRole('button', { name: 'Classify deck' }).click();
  await expect(page.locator('#results')).toBeVisible();

  await expectCollapsedResult(page, BRACKET_LABEL);
});

test('/deck-history result collapses intake and restores pasted deck when edited', async ({ page }) => {
  await page.goto('/deck-history');
  await setPastedDeck(page);
  await intake(page).locator('input[name="DeckName"]').fill(HISTORY_LABEL);
  await page.getByRole('button', { name: 'Update history' }).click();
  await expect(page.locator('#results')).toBeVisible();

  await expectCollapsedResult(page, HISTORY_LABEL);
});

for (const path of ['/bracket', '/manabase']) {
  test(`${path} restores cached PasteText input panels after reload`, async ({ page }) => {
    await page.goto(path);
    await setPastedDeck(page);
    await page.reload();

    await expectRestoredPasteText(page);
  });
}

test('/manabase live result collapses intake when enabled', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: Manabase calls Scryfall');

  await page.goto('/manabase');
  await setPastedDeck(page);
  await intake(page).locator('input[name="DeckName"]').fill('Intake Manabase Deck');
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();
  await expect(page.locator('#results')).toBeVisible({ timeout: 60_000 });

  await expectCollapsedResult(page, 'Intake Manabase Deck');
});
