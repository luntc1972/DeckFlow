import { expect, test, type Locator, type Page } from '@playwright/test';
import { clickManabasePillRadio } from './support/manabase-pill';

// Live-only Phase 71 verdict surfaces.
// Run:
//   1. Start the app with scripts/run-web-test.sh and the manabase plain-language flag ON.
//   2. From DeckFlow.Web/, run:
//      DECKFLOW_LIVE_E2E=1 npx --no-install playwright test manabase-verdict

const CASUAL_ISSUE_DECK = [
  'Commander',
  '1 Brago, King Eternal',
  '',
  'Deck',
  '10 Plains',
  '6 Island',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Counterspell',
  '1 Supreme Verdict',
  '1 Cryptic Command',
  '1 Wrath of God',
  '1 Teferi, Hero of Dominaria',
].join('\n');

// A genuinely clean mono-blue shell: Karsten source target cleared, and — with the plain-language
// layer now default-on — enough repeatable ramp AND card draw to satisfy the ramp/draw budget for
// the commander's MV threshold, so the verdict reports the why-it-is-fine path (no issue list).
// Mono-color avoids any color-shortfall issue; the ramp/draw balance is what unlocks verdict-fine.
const CASUAL_CLEAN_DECK = [
  'Commander',
  '1 Talrand, Sky Summoner',
  '',
  'Deck',
  '32 Island',
  '1 Sol Ring',
  '1 Mind Stone',
  '1 Fellwar Stone',
  '1 Thought Vessel',
  '1 Everflowing Chalice',
  '1 Sky Diamond',
  '1 Worn Powerstone',
  '1 Gilded Lotus',
  '1 Hedron Archive',
  '1 Thran Dynamo',
  '1 Dreamstone Hedron',
  '1 Palladium Myr',
  '1 Rhystic Study',
  '1 Mystic Remora',
  '1 Fact or Fiction',
  '1 Preordain',
  '1 Ponder',
  '1 Brainstorm',
  '1 Divination',
  '1 Compulsive Research',
  '1 Chart a Course',
  '1 Behold the Multiverse',
  '1 Windfall',
  '1 Mind Spring',
].join('\n');

const CEDH_DECK = [
  'Commander',
  '1 Kinnan, Bonder Prodigy',
  '',
  'Deck',
  '14 Island',
  '14 Forest',
  '1 Command Tower',
  '1 Breeding Pool',
  '1 Sol Ring',
  '1 Mana Crypt',
  '1 Arcane Signet',
  '1 Fellwar Stone',
  '1 Llanowar Elves',
  '1 Birds of Paradise',
  '1 Counterspell',
  '1 Swan Song',
  '1 Neoform',
].join('\n');

async function analyzeDeck(
  page: Page,
  deckText: string,
  mode: 'Casual' | 'Cedh',
): Promise<Locator> {
  await page.goto('/manabase');
  await page.locator('#manabase-input-source').selectOption('PasteText');
  await page.locator('#manabase-deck-text').fill(deckText);
  await clickManabasePillRadio(page, 'Mode', mode);
  await page.getByRole('button', { name: 'Analyze Mana Base' }).click();

  const result = page.locator('.result-panel:has(h2:has-text("Result"))');
  await expect(result).toBeVisible({ timeout: 60_000 });
  return result;
}

test('casual issue deck shows glosses, an issue verdict list, and ramp/draw budget', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall + flag on');

  const result = await analyzeDeck(page, CASUAL_ISSUE_DECK, 'Casual');
  const verdict = result.locator('.manabase-verdict');
  const verdictItems = result.locator('.manabase-verdict-list li');

  await expect(result.locator('.manabase-lens-gloss').first()).toBeVisible();
  await expect(verdict).toBeVisible();
  await expect(verdictItems.first()).toBeVisible();
  await expect(verdict).not.toContainText('(s)');
  await expect(result.locator('td[data-label="Short by (heuristic guidance)"]').first()).toBeVisible();

  const verdictText = await verdict.textContent() ?? '';
  if (verdictText.includes('…plus'))
  {
    await expect(verdict).toContainText('…plus');
  }
  else
  {
    expect(await verdictItems.count()).toBeLessThanOrEqual(3);
  }

  await expect(result.locator('.manabase-rampdraw')).toBeVisible();
  await expect(result.locator('.manabase-rampdraw')).toHaveClass(/manabase-lens/);
});

test('casual clean deck shows the why-it-is-fine verdict with no issue list', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall + flag on');

  const result = await analyzeDeck(page, CASUAL_CLEAN_DECK, 'Casual');

  await expect(result.locator('.manabase-lens-gloss').first()).toBeVisible();
  await expect(result.locator('.manabase-verdict')).toBeVisible();
  await expect(result.locator('.manabase-verdict-fine')).toBeVisible();
  await expect(result.locator('.manabase-verdict-list')).toHaveCount(0);
  await expect(result.locator('.manabase-rampdraw')).toBeVisible();
  await expect(result.locator('.manabase-rampdraw')).toHaveClass(/manabase-lens/);
});

test('cedh shows glosses but suppresses the casual-only verdict and ramp/draw advisory', async ({ page }) => {
  test.skip(!process.env.DECKFLOW_LIVE_E2E, 'live-only: needs Scryfall + flag on');

  const result = await analyzeDeck(page, CEDH_DECK, 'Cedh');

  await expect(result.locator('.manabase-lens-gloss').first()).toBeVisible();
  await expect(result.locator('.manabase-rampdraw')).toHaveCount(0);
  await expect(result.locator('.manabase-verdict')).toHaveCount(0);
});
