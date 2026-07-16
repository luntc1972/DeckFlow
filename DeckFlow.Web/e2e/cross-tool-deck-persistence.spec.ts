import { expect, test, type Browser, type Page } from '@playwright/test';

const THEME_STORAGE_KEY = 'deckflow-theme';
const representativeThemes = ['site.css', 'site-azorius.css', 'site-nyx.css'] as const;
const pastedDeck = '1 Sol Ring\n1 Arcane Signet';
const urlDeck = 'https://moxfield.com/decks/abc123';
const storeValueDeck = 'STORE VALUE DECK';
const postedDeck = 'POSTED DECK';

async function setThemeBeforeNavigation(page: Page, themeFile: string): Promise<void> {
  await page.addInitScript(
    ([storageKey, storageValue]) => {
      window.localStorage.setItem(storageKey, storageValue);
    },
    [THEME_STORAGE_KEY, themeFile]
  );
}

async function expectThemeSelected(page: Page, themeFile: string): Promise<void> {
  await expect(page.locator('#theme-picker')).toHaveValue(themeFile);
}

async function fillDeckAnalysisPasteText(page: Page, deckText: string): Promise<void> {
  await page.goto('/deck-analysis');
  await page.locator('select[name="DeckInputSource"]').selectOption('PasteText');
  await page.locator('textarea[name="DeckText"]').fill(deckText);
}

async function expectRestoredNoticeHidden(page: Page): Promise<void> {
  await expect(page.locator('.deck-restored-notice')).toHaveCount(0);
}

async function runThemePrefillCheck(browser: Browser, themeFile: string): Promise<void> {
  const page = await browser.newPage();

  try {
    await setThemeBeforeNavigation(page, themeFile);
    await fillDeckAnalysisPasteText(page, pastedDeck);
    await expectThemeSelected(page, themeFile);

    await page.goto('/manabase');
    await expectThemeSelected(page, themeFile);
    await expect(page.locator('textarea[name="DeckText"]')).toHaveValue(pastedDeck);
    await expect(page.locator('input[name="DeckUrl"]')).toHaveValue('');
  } finally {
    await page.close();
  }
}

test('deck text prefills across single-deck tools for representative themes', async ({ browser }) => {
  for (const themeFile of representativeThemes) {
    await runThemePrefillCheck(browser, themeFile);
  }
});

test('url mode restore keeps the correct input method across tools', async ({ page }) => {
  await page.goto('/deck-analysis');
  await page.locator('select[name="DeckInputSource"]').selectOption('PublicUrl');
  await page.locator('input[name="DeckUrl"]').fill(urlDeck);

  await page.goto('/convert');
  await expect(page.locator('select[name="InputSource"]')).toHaveValue('PublicUrl');
  await expect(page.locator('input[name="DeckUrl"]')).toHaveValue(urlDeck);
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue('');
});

test('deck text prefills into deck primer from deck analysis', async ({ page }) => {
  await page.goto('/deck-analysis');
  await page.locator('select[name="DeckInputSource"]').selectOption('PasteText');
  await page.locator('textarea[name="DeckText"]').fill(pastedDeck);

  await page.goto('/deck-primer');
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue(pastedDeck);
});

test('postback keeps the server-echoed deck instead of overwriting it from sessionStorage', async ({ page }) => {
  await page.addInitScript(([storageKey, storedDeck]) => {
    window.sessionStorage.setItem(storageKey, JSON.stringify(storedDeck));
  }, ['deckflow.last-deck', {
    inputSource: 'PasteText',
    deckUrl: '',
    deckText: storeValueDeck,
  }]);

  // DeckConvert gives the simplest same-page POST rerender for asserting that
  // the echoed DeckText survives DOMContentLoaded without a second overwrite.
  await page.goto('/convert');
  await page.locator('select[name="InputSource"]').selectOption('PasteText');
  await page.locator('textarea[name="DeckText"]').fill(postedDeck);
  await page.getByRole('button', { name: 'Convert' }).click();
  await page.waitForLoadState('networkidle');

  const deckText = page.locator('textarea[name="DeckText"]');
  await expect(deckText).toHaveValue(postedDeck);
  await expect(deckText).not.toHaveValue(storeValueDeck);
});

test('fresh browser contexts do not inherit another tab session deck', async ({ page }) => {
  await page.goto('/manabase');
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue('');
  await expect(page.locator('input[name="DeckUrl"]')).toHaveValue('');
});

test('start over clears the carried deck store', async ({ page }) => {
  await fillDeckAnalysisPasteText(page, pastedDeck);

  await page.goto('/deck-primer');
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue(pastedDeck);
  await expect(page.locator('.deck-restored-notice')).toBeVisible();

  await page.getByRole('button', { name: 'Start Over' }).click();
  await page.waitForURL('**/deck-primer');

  await page.goto('/deck-primer');
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue('');
  await expectRestoredNoticeHidden(page);
});

test('restored notice appears across representative themes when a deck is actually prefilling', async ({ browser }) => {
  for (const themeFile of representativeThemes) {
    const page = await browser.newPage();

    try {
      await setThemeBeforeNavigation(page, themeFile);
      await fillDeckAnalysisPasteText(page, pastedDeck);
      await expectThemeSelected(page, themeFile);

      await page.goto('/deck-primer');
      await expectThemeSelected(page, themeFile);
      await expect(page.locator('textarea[name="DeckText"]')).toHaveValue(pastedDeck);
      await expect(page.locator('.deck-restored-notice')).toBeVisible();
      await expect(page.locator('.deck-restored-notice')).toContainText('Restored your last deck.');
    } finally {
      await page.close();
    }
  }
});

test('restored notice clear empties the current tool and removes future prefills', async ({ page }) => {
  await fillDeckAnalysisPasteText(page, pastedDeck);

  await page.goto('/deck-primer');
  const deckText = page.locator('textarea[name="DeckText"]');
  const restoredNotice = page.locator('.deck-restored-notice');
  await expect(restoredNotice).toBeVisible();
  await expect(deckText).toHaveValue(pastedDeck);

  await restoredNotice.locator('[data-deck-restored-clear]').click();
  await expect(deckText).toHaveValue('');
  await expectRestoredNoticeHidden(page);

  await page.goto('/convert');
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue('');
  await expect(page.locator('input[name="DeckUrl"]')).toHaveValue('');
  await expectRestoredNoticeHidden(page);
});

test('fresh context does not show a restored deck notice', async ({ page }) => {
  await page.goto('/deck-primer');
  await expect(page.locator('textarea[name="DeckText"]')).toHaveValue('');
  await expect(page.locator('input[name="DeckUrl"]')).toHaveValue('');
  await expectRestoredNoticeHidden(page);
});
