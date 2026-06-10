import { beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/kb-entry-filter';

interface KbEntryFilterApi {
  rowMatches(searchText: string, query: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
}

let api: KbEntryFilterApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowKbFilter: KbEntryFilterApi }).DeckFlowKbFilter;
});

describe('DeckFlowKbFilter (KBUX-01)', () => {
  it('matches everything on empty query', () => {
    expect(api.rowMatches('sol ring artifact', '')).toBe(true);
  });

  it('matches on substring of the search text', () => {
    expect(api.rowMatches('sol ring ramp', 'ring')).toBe(true);
  });

  it('does not match when substring absent', () => {
    expect(api.rowMatches('sol ring ramp', 'counterspell')).toBe(false);
  });

  it('formats the live count', () => {
    expect(api.formatCount(3, 10)).toBe('3 of 10 entries shown');
  });

  it('hides empty row when there are matches', () => {
    expect(api.emptyRowHidden(2, 10)).toBe(true);
  });

  it('shows empty row when zero matches but rows exist', () => {
    expect(api.emptyRowHidden(0, 10)).toBe(false);
  });

  it('hides empty row when the table has no rows at all', () => {
    expect(api.emptyRowHidden(0, 0)).toBe(true);
  });
});
