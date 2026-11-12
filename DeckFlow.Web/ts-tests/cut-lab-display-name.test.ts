import { describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

const api = (globalThis as unknown as {
  DeckFlowCutLab: { toDisplayCardName(text: string): string };
}).DeckFlowCutLab;

describe('DeckFlowCutLab display names', () => {
  it('replaces the MDFC separator with a single slash', () => {
    expect(api.toDisplayCardName('Witch Enchanter // Witch-Blessed Meadow'))
      .toBe('Witch Enchanter / Witch-Blessed Meadow');
  });

  it('leaves single-faced names and slashes without surrounding spaces unchanged', () => {
    for (const name of ['Sol Ring', 'A//B', 'https://x']) {
      expect(api.toDisplayCardName(name)).toBe(name);
    }
  });

  it('replaces every MDFC separator in a sentence', () => {
    expect(api.toDisplayCardName('Cut A // B and keep C // D.')).toBe('Cut A / B and keep C / D.');
  });
});
