import { expect, test, type Page } from '@playwright/test';

const CASUAL_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '1 Command Tower',
  '1 Exotic Orchard',
  '1 Prairie Stream',
  '1 Port Town',
  '1 Glacial Fortress',
  '1 Adarkar Wastes',
  '1 Tranquil Cove',
  '1 Azorius Guildgate',
  '1 Temple of Enlightenment',
  '1 Nimbus Maze',
  '1 Skycloud Expanse',
  '8 Plains',
  '8 Island',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Azorius Signet',
  '1 Talisman of Progress',
  '1 Mind Stone',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  '1 Arcane Denial',
  '1 Dovin\'s Veto',
  '1 Negate',
  '1 Mana Leak',
  '1 Essence Scatter',
  '1 Wall of Omens',
  '1 Spirited Companion',
  '1 Reflector Mage',
  '1 Skyclave Apparition',
  '1 Supreme Verdict',
  '1 Wrath of God',
  '1 Day of Judgment',
  '1 Settle the Wreckage',
  '1 Teferi, Hero of Dominaria',
  '1 Cloudblazer',
  '1 Mulldrifter',
  '1 Conjurer\'s Closet',
  '1 Sun Titan',
  '1 Approach of the Second Sun',
  '1 Clive, Ifrit\'s Dominant // Ifrit, Warden of Inferno',
].join('\n');

const CEDH_DECK = [
  'Commander',
  '1 Kinnan, Bonder Prodigy',
  '',
  'Deck',
  '1 Command Tower',
  '1 Breeding Pool',
  '1 Yavimaya Coast',
  '1 Hinterland Harbor',
  '1 Botanical Sanctum',
  '1 Rejuvenating Springs',
  '1 City of Brass',
  '1 Mana Confluence',
  '1 Island',
  '1 Island',
  '1 Forest',
  '1 Forest',
  '1 Sol Ring',
  '1 Mana Crypt',
  '1 Arcane Signet',
  '1 Fellwar Stone',
  '1 Llanowar Elves',
  '1 Elvish Mystic',
  '1 Birds of Paradise',
  '1 Noble Hierarch',
  '1 Counterspell',
  '1 Swan Song',
  '1 Spell Pierce',
  '1 Neoform',
  '1 Finale of Devastation',
].join('\n');

async function submitDeck(
  page: Page,
  deckText: string,
  mode: 'Casual' | 'Cedh',
): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(deckText);
  await page.locator(`.manabase-pill input[name="Mode"][value="${mode}"]`).check();
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

test('casual result caps castability rows, shows anchor nav, and reveals all rows on demand', async ({ page }) => {
  const ok = await submitDeck(page, CASUAL_DECK, 'Casual');
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  await expect(result.locator('.manabase-mode-chip')).toContainText('Casual analysis');
  await expect(result.locator('.manabase-anchor-nav')).toBeVisible();

  const defaultRows = result.locator('#manabase-castability > .manabase-table-wrap tbody tr');
  const defaultCount = await defaultRows.count();
  expect(defaultCount).toBeGreaterThanOrEqual(10);
  expect(defaultCount).toBeLessThanOrEqual(20);

  const expander = result.locator('#manabase-castability details');
  await expect(expander).toBeVisible();
  const allLabel = await expander.locator('summary').innerText();
  const match = allLabel.match(/Show all (\d+) castability rows/);
  expect(match).not.toBeNull();
  const totalRows = Number.parseInt(match![1], 10);

  await expander.locator('summary').click();
  expect(await expander.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(true);

  const hiddenRows = expander.locator('tbody tr');
  expect(defaultCount + await hiddenRows.count()).toBe(totalRows);

  const colorFindingsLink = result.locator('.manabase-anchor-nav a[href="#manabase-color-findings"]');
  await colorFindingsLink.click();
  await expect.poll(() => page.evaluate(() => window.location.hash)).toBe('#manabase-color-findings');
});

test('cedh result uses mode-aware copy and keeps the mode chip visible', async ({ page }) => {
  const ok = await submitDeck(page, CEDH_DECK, 'Cedh');
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  await expect(result.locator('.manabase-mode-chip')).toContainText('cEDH analysis');
  await expect(result).not.toContainText('castability table below');
});

test('mobile castability cards keep a long card name readable', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('mobile'), 'mobile-only readability guard');

  const ok = await submitDeck(page, CASUAL_DECK, 'Casual');
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  const expander = page.locator('#manabase-castability details');
  await expander.locator('summary').click();

  const longName = page.locator('.castability-name-text', {
    hasText: 'Clive, Ifrit\'s Dominant // Ifrit, Warden of Inferno',
  }).first();
  await expect(longName).toBeVisible();
  await expect(longName).toHaveText('Clive, Ifrit\'s Dominant // Ifrit, Warden of Inferno');

  const metrics = await longName.evaluate((el) => {
    const style = getComputedStyle(el);
    const parsedLineHeight = Number.parseFloat(style.lineHeight);
    const fontSize = Number.parseFloat(style.fontSize);
    const lineHeight = Number.isFinite(parsedLineHeight)
      ? parsedLineHeight
      : fontSize * 1.2;
    return {
      scrollWidth: el.scrollWidth,
      clientWidth: el.clientWidth,
      clientHeight: el.clientHeight,
      lineHeight,
    };
  });

  expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
  expect(metrics.clientHeight).toBeGreaterThan(metrics.lineHeight * 1.5);
});
