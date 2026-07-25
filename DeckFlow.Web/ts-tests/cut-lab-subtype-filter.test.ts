import { beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabSubtypeEntry {
  name: string;
  typeLine: string;
  isLocked: boolean;
  isCommander: boolean;
}

interface CutLabApi {
  filterPoolBySubtype(entries: CutLabSubtypeEntry[], query: string): CutLabSubtypeEntry[];
}

let api: CutLabApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCutLab: CutLabApi }).DeckFlowCutLab;
});

describe('DeckFlowCutLab subtype filter', () => {
  const entries: CutLabSubtypeEntry[] = [
    { name: 'Kabira Evangel', typeLine: 'Creature — Human Cleric Ally', isLocked: false, isCommander: false },
    { name: 'Professor of Symbology', typeLine: 'Creature — Kor Cleric', isLocked: false, isCommander: false },
    { name: 'Mascot Exhibition', typeLine: 'Sorcery — Lesson', isLocked: false, isCommander: false },
    { name: 'Captain Sisay', typeLine: 'Legendary Creature — Human Soldier', isLocked: true, isCommander: true },
    { name: 'Beluna Grandsquall', typeLine: 'Creature — Giant Noble // Sorcery — Adventure', isLocked: false, isCommander: false },
  ];

  it('matches by subtype substring on the front face', () => {
    expect(api.filterPoolBySubtype(entries, 'ally').map(entry => entry.name)).toEqual(['Kabira Evangel']);
  });

  it('matches Legendary as a supertype substring', () => {
    expect(api.filterPoolBySubtype(entries, 'legendary').map(entry => entry.name)).toEqual(['Captain Sisay']);
  });

  it('uses only the front face for DFC and adventure type lines', () => {
    expect(api.filterPoolBySubtype(entries, 'noble').map(entry => entry.name)).toEqual(['Beluna Grandsquall']);
    expect(api.filterPoolBySubtype(entries, 'adventure')).toEqual([]);
  });

  it('returns no results for an empty query', () => {
    expect(api.filterPoolBySubtype(entries, '   ')).toEqual([]);
  });

  it('returns no results when nothing matches', () => {
    expect(api.filterPoolBySubtype(entries, 'dragon')).toEqual([]);
  });
});
