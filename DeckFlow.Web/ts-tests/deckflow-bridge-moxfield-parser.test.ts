import { createRequire } from 'node:module';
import { describe, expect, it } from 'vitest';

const require = createRequire(import.meta.url);
const { buildExportBundle, getZone, hasDeckZones } = require('../../browser-extensions/deckflow-bridge/moxfield-deck-parser.js');

const ZONES = ['commanders', 'mainboard', 'sideboard', 'maybeboard'];
const EMPTY_DECK_ERROR = 'No commander, mainboard, sideboard, or maybeboard cards were found.';

const commander = { quantity: 1, card: { name: 'Tymna the Weaver' } };
const mainCard = { quantity: 2, card: { name: 'Sol Ring', set: 'CMM', cn: '396' } };
const authorTags = {
  'Tymna the Weaver': [' Commander ', ' Draw Cards ', '', 7],
  'Sol Ring': [' Mana Rocks ', 'Mana Rocks', ' Ramp ']
};
const v2 = {
  name: '  Test deck  ', authorTags,
  commanders: { 'Tymna the Weaver': commander },
  mainboard: { 'Sol Ring': mainCard }
};
const v3 = {
  name: v2.name, authorTags, main: { ignored: true },
  boards: {
    commanders: { count: 1, cards: { 'commander-entry-id': commander } },
    mainboard: { count: 2, cards: { 'main-entry-id': mainCard } }
  }
};

const expected = {
  deckName: 'Test deck', cardCount: 2,
  moxfieldText: '1 Tymna the Weaver #Commander #DrawCards\n2 Sol Ring (cmm) 396 #ManaRocks #Ramp\n'
};

describe('DeckFlow Bridge Moxfield parser', () => {
  it('preserves exact v2 printing, tags, order and entry count', () => {
    expect(buildExportBundle(v2, 'fallback')).toEqual(expected);
  });

  it('exports equivalent v3 entry-id boards identically to v2', () => {
    expect(buildExportBundle(v3, 'fallback')).toEqual(expected);
  });

  it('exports v3 sideboard and maybeboard in order with board tags', () => {
    const boards = {
      ...v3.boards,
      sideboard: { cards: { side: { quantity: 1, card: { name: 'Swords to Plowshares' } } } },
      maybeboard: { cards: { maybe: { quantity: 1, card: { name: 'Path to Exile' } } } }
    };
    expect(buildExportBundle({ ...v3, boards }, 'fallback')).toEqual({
      ...expected, cardCount: 4,
      moxfieldText: expected.moxfieldText + '1 Swords to Plowshares #Sideboard\n1 Path to Exile #Maybeboard\n'
    });
  });

  it('recognizes each supported v2 and v3 zone', () => {
    for (const zone of ZONES) {
      expect(hasDeckZones({ [zone]: { card: mainCard } })).toBe(true);
      expect(hasDeckZones({ boards: { [zone]: { cards: { id: mainCard } } } })).toBe(true);
    }
  });

  it('rejects main metadata alone and missing zone containers', () => {
    expect(hasDeckZones({ main: { count: 100 } })).toBe(false);
    expect(hasDeckZones({ boards: { mainboard: {} } })).toBe(false);
    expect(hasDeckZones({})).toBe(false);
    expect(hasDeckZones(null)).toBe(false);
  });

  it('prefers v3 cards, including empty boards, and falls back to v2 for invalid cards', () => {
    const zone = getZone({ ...v2, boards: v3.boards }, 'mainboard');
    expect(zone).toEqual({ cards: v3.boards.mainboard.cards, isV3: true });
    expect(zone.cards).toBe(v3.boards.mainboard.cards);
    expect(getZone({ ...v2, boards: { mainboard: { cards: {} } } }, 'mainboard')).toEqual({ cards: {}, isV3: true });
    const fallback = getZone({ ...v2, boards: { mainboard: { cards: null } } }, 'mainboard');
    expect(fallback).toEqual({ cards: v2.mainboard, isV3: false });
    expect(fallback.cards).toBe(v2.mainboard);
  });

  it.each([undefined, null, 123, 'invalid'])('returns null for invalid zone containers %s', (cards) => {
    expect(getZone({ mainboard: cards, boards: { mainboard: { cards } } }, 'mainboard')).toBeNull();
  });

  it('keeps v2 fallback names, default quantity and fallback deck name', () => {
    expect(buildExportBundle({ mainboard: { 'Fallback Card': { quantity: 0 } } }, 'abc')).toEqual({
      deckName: 'moxfield-abc', cardCount: 1, moxfieldText: '1 Fallback Card\n'
    });
  });

  it.each(ZONES)('recognizes empty v2 and v3 %s containers and preserves the exact empty-deck error', (zone) => {
    for (const payload of [{ [zone]: {} }, { boards: { [zone]: { cards: {} } } }]) {
      expect(hasDeckZones(payload)).toBe(true);
      expect(() => buildExportBundle(payload, 'fallback')).toThrow(new Error(
        EMPTY_DECK_ERROR
      ));
    }
  });

  it.each([undefined, null, 123, '', '   '])('skips v3 entries with invalid card.name %s without exporting their ids', (name) => {
    const cards = {
      'valid-entry-id': mainCard,
      'nameless-entry-id': { quantity: 1, card: { name } },
      'missing-card-entry-id': { quantity: 1, name: 'Entry metadata is not a card name' }
    };
    const result = buildExportBundle({ boards: { mainboard: { cards } } }, 'fallback');
    expect(result).toEqual({
      deckName: 'moxfield-fallback', cardCount: 1,
      moxfieldText: '2 Sol Ring (cmm) 396\n'
    });
    expect(result.moxfieldText).not.toContain('nameless-entry-id');
    expect(result.moxfieldText).not.toContain('missing-card-entry-id');
  });

  it('preserves the empty-deck error', () => {
    expect(() => buildExportBundle({}, 'fallback')).toThrow(
      EMPTY_DECK_ERROR
    );
  });
});
