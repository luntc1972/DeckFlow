import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
});
afterEach(() => { document.body.innerHTML = ''; });
const buildStateJson = (islandQuantity: number, adjustmentDelta = 0): string => JSON.stringify({
  commander: 'Commander',
  pool: [
    { name: 'Commander', quantity: 1, typeLine: 'Legendary Creature', isCommander: true, isLocked: true, packageId: null },
    { name: 'Island', quantity: islandQuantity, typeLine: 'Basic Land — Island', isCommander: false, isLocked: false, packageId: null },
  ],
  packages: [],
  decisions: [],
  quantityAdjustments: adjustmentDelta === 0 ? [] : [{ name: 'Island', delta: adjustmentDelta, isAddedBasic: false }],
  baselineSnapshot: { metrics: [] },
  roleFloors: [],
  goals: {
    commanderByTurn: 3,
    engineByTurn: 2,
    representativeLineByTurn: 4,
  },
  intent: {
    primaryPlan: 'Hit exactly 100.',
    secondaryPlan: null,
    bracket: 3,
    playExperience: 'Focused',
    includeSideboard: false,
    includeMaybeboard: false,
  },
});


const originals = [
  { name: 'Island', quantity: 98, board: 'main', setCode: 'lea', collectorNumber: '1', category: 'Ramp' },
  { name: 'Swamp', quantity: 1, board: 'side', setCode: 'leb', collectorNumber: '2', category: 'Draw' },
];
const api = (): any => (window as any).DeckFlowCutLab;
const fixture = (extra: object = {}): void => {
  document.body.innerHTML = `<form data-cache-key="cut-lab">
    <input name="CutLabStateJson" type="hidden" />
    <table><tr data-cut-lab-card="Island" data-cut-lab-quantity="98" data-cut-lab-type-line="Basic Land" data-cut-lab-commander="false">
    <td><input type="checkbox" data-cut-lab-lock-card="Island" /></td></tr></table>
    </form><form><input name="CutLabStateJson" type="hidden" /></form>`;
  document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!.value = JSON.stringify({
    ...JSON.parse(buildStateJson(98)), originalEntries: originals, ...extra,
  });
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

describe('original entry transport', () => {
  it('V1 preserves every original entry field', () => {
    const state = { ...JSON.parse(buildStateJson(98)), originalEntries: originals };
    const result = JSON.parse(api().buildCutLabStateJson(state));
    expect(result.originalEntries).toEqual(originals);
  });
  it('V2 omits empty arrays', () => {
    const result = JSON.parse(api().buildCutLabStateJson({ ...JSON.parse(buildStateJson(98)), originalEntries: [] }));
    expect(result).not.toHaveProperty('originalEntries');
  });
  it('V3 preserves originals in every form after two lock toggles', () => {
    fixture();
    for (let i = 0; i < 2; i++) {
      document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card]')!.click();
      document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]').forEach(input => {
        expect(JSON.parse(input.value).originalEntries).toEqual(originals);
      });
    }
  });
  it('V4 drops the legacy key while preserving roles and originals after a lock toggle', () => {
    const roleOverrides = [{ name: 'Island', added: ['ramp'], removed: ['draw'] }];
    fixture({ categoryOverrides: [{ name: 'Island', category: 'FastMana' }], roleOverrides });
    document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card]')!.click();
    const inputs = document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]');
    expect(inputs.length).toBe(2);
    inputs.forEach(input => {
      const state = JSON.parse(input.value);
      expect(state).not.toHaveProperty('categoryOverrides');
      expect(state.roleOverrides).toEqual(roleOverrides);
      expect(state.originalEntries).toEqual(originals);
    });
  });
});
