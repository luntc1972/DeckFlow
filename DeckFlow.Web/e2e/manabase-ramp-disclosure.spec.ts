import { expect, test, type Page } from '@playwright/test';

// NSM-RAMP-DISCLOSURE — the Ramp line is an expandable <details class="manabase-ramp"> that lists
// the credited mana rock/dork names and the ≤2 MV ramp/draw names.
//
// Unlike `manabase.spec.ts` (chrome only), this spec submits a real paste decklist, which drives a
// live Scryfall card-resolution round-trip. When the sandbox can't reach Scryfall the result panel
// never appears; the result-dependent assertions are guarded with test.skip so an environment
// limitation does not fail the spec (same convention as manabase-castability.spec.ts).

// A small ramp-heavy shell: a commander, basics, and resolvable ramp staples — Sol Ring (a rock),
// Arcane Signet (a rock), and Llanowar Elves (a dork) — so the disclosure has real rock/dork names.
const PASTE_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '20 Plains',
  '20 Island',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Llanowar Elves',
  '1 Counterspell',
  '1 Swords to Plowshares',
].join('\n');

async function submitDeck(page: Page): Promise<boolean> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(PASTE_DECK);
  // Click Analyze specifically — the page also has a "Load deck" run-button, so a bare
  // `button.run-button` matches two elements (strict-mode failure).
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  const error = page.locator('.error-banner:not(.hidden)');
  await Promise.race([
    result.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
    error.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => undefined),
  ]);

  return (await result.count()) > 0 && (await result.isVisible());
}

async function assertNoHorizontalScroll(page: Page): Promise<void> {
  const overflows = await page.evaluate(
    () => document.documentElement.scrollWidth > window.innerWidth + 1,
  );
  expect(overflows, 'page must not gain a horizontal scrollbar').toBe(false);
}

test('ramp disclosure expands and lists credited rock/dork names', async ({ page }) => {
  const ok = await submitDeck(page);
  test.skip(!ok, 'analysis result unavailable (Scryfall not reachable in this environment)');

  // The Ramp line is an expandable <details class="manabase-ramp">, collapsed by default.
  const ramp = page.locator('details.manabase-ramp');
  await expect(ramp).toHaveCount(1);
  expect(await ramp.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(false);

  // Its summary carries the existing "Ramp:" count sentence.
  await expect(ramp.locator('summary')).toContainText(/Ramp:/i);

  // Expanding it reveals the rock/dork list with at least one real rock name.
  await ramp.locator('summary').click();
  expect(await ramp.evaluate((el) => (el as HTMLDetailsElement).open)).toBe(true);

  const items = await ramp.locator('ul li').allInnerTexts();
  expect(items.length).toBeGreaterThan(0);
  // Sol Ring is a mana rock the analyzer credits — it must surface in the disclosure.
  expect(items.some((n) => n.includes('Sol Ring'))).toBe(true);

  await assertNoHorizontalScroll(page);
});
