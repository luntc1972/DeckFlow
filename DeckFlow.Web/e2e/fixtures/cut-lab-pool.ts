import type { Page } from '@playwright/test';

export const cutLabPool = [
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
  "1 Dovin's Veto",
  '1 Demonic Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

// Retries a navigation up to twice on a non-ok response. Under fullyParallel CI
// the SQLite store can briefly return a 5xx ("database is locked") when many
// workers hit pages at once; a re-navigate clears it. Mirrors scripts.spec.ts.
export async function gotoOk(page: Page, route: string) {
  let response = await page.goto(route);
  for (let attempt = 0; attempt < 2 && !response?.ok(); attempt++) {
    response = await page.goto(route);
  }
  return response;
}
