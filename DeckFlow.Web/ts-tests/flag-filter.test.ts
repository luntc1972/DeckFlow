import { beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/flag-filter';

interface FlagFilterApi {
  keyMatches(key: string, query: string): boolean;
  statusMatches(enabled: boolean, status: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
}

let api: FlagFilterApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowFlagFilter: FlagFilterApi }).DeckFlowFlagFilter;
});

describe('DeckFlowFlagFilter', () => {
  it('matches everything on empty query', () => {
    expect(api.keyMatches('tool.card-lookup.enabled', '')).toBe(true);
  });

  it('matches when the key starts with the query', () => {
    expect(api.keyMatches('tool.card-lookup.enabled', 'tool.')).toBe(true);
  });

  it('does not match on non-prefix substrings', () => {
    expect(api.keyMatches('analysis.manabase.x', 'manabase')).toBe(false);
  });

  it('matches case-insensitively', () => {
    expect(api.keyMatches('Service.Scryfall-Tagger.Enabled', 'service.')).toBe(true);
  });

  it('matches all statuses when the status filter is empty', () => {
    expect(api.statusMatches(true, '')).toBe(true);
    expect(api.statusMatches(false, '')).toBe(true);
  });

  it('matches only enabled rows for the on status filter', () => {
    expect(api.statusMatches(true, 'on')).toBe(true);
    expect(api.statusMatches(false, 'on')).toBe(false);
  });

  it('matches only disabled rows for the off status filter', () => {
    expect(api.statusMatches(true, 'off')).toBe(false);
    expect(api.statusMatches(false, 'off')).toBe(true);
  });

  it('treats unknown status filters as matching all rows', () => {
    expect(api.statusMatches(true, 'unknown')).toBe(true);
    expect(api.statusMatches(false, 'unknown')).toBe(true);
  });

  it('formats the live count', () => {
    expect(api.formatCount(3, 14)).toBe('3 of 14 flags shown');
  });

  it('hides the empty row when there are matches', () => {
    expect(api.emptyRowHidden(2, 10)).toBe(true);
  });

  it('shows the empty row when zero matches remain but rows exist', () => {
    expect(api.emptyRowHidden(0, 10)).toBe(false);
  });

  it('hides the empty row when there are no rows at all', () => {
    expect(api.emptyRowHidden(0, 0)).toBe(true);
  });
});
