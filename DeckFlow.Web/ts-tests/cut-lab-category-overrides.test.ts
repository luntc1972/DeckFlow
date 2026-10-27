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
const edits = [{ name: 'Island', category: 'FastMana' }];
const api = (): any => (window as any).DeckFlowCutLab;
const fixture = (entry: object = {}): void => {
  document.body.innerHTML = `<form data-cache-key="cut-lab">
    <input name="CutLabStateJson" type="hidden" />
    <table><tr data-cut-lab-card="Island" data-cut-lab-quantity="98" data-cut-lab-type-line="Basic Land" data-cut-lab-commander="false">
    <td><input type="checkbox" data-cut-lab-lock-card="Island" /></td></tr></table>
    <details class="cutlab-role-group" data-cutlab-group-kind="category"><span data-cut-lab-category-locked="Ramp">0</span><button type="button" data-cut-lab-chip-card="Island" data-cutlab-card-open="Island">Island</button></details>
    </form>
    <script id="cutlab-card-text-data" type="application/json">{}</script>
    <dialog id="cutlab-card-modal"><h2 id="cutlab-card-modal-title"></h2><p data-cutlab-modal-oracle></p>
    <div data-cut-lab-category-block hidden><p data-cut-lab-category-current></p><p data-cut-lab-category-also hidden></p>
    <form><input type="hidden" name="CutLabStateJson" /><input data-cut-lab-category-card name="CardName" /><input data-cut-lab-category-input name="Category" /><button data-cut-lab-category-reset hidden>Use imported category</button></form></div>
    <div class="cutlab-card-modal__actions"><button type="button" data-cutlab-modal-close>Close</button></div></dialog>`;
  document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!.value = JSON.stringify({ ...JSON.parse(buildStateJson(98)), originalEntries: originals, categoryOverrides: edits });
  document.getElementById('cutlab-card-text-data')!.textContent = JSON.stringify({ Island: entry });
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

describe('category transport', () => {
  it('V1 preserves every original entry field and category edit', () => {
    const state = { ...JSON.parse(buildStateJson(98)), originalEntries: originals, categoryOverrides: edits };
    const result = JSON.parse(api().buildCutLabStateJson(state));
    expect(result.originalEntries).toEqual(originals);
    expect(result.categoryOverrides).toEqual(edits);
  });
  it('V2 omits empty arrays', () => {
    const result = JSON.parse(api().buildCutLabStateJson({ ...JSON.parse(buildStateJson(98)), originalEntries: [], categoryOverrides: [] }));
    expect(result).not.toHaveProperty('originalEntries');
    expect(result).not.toHaveProperty('categoryOverrides');
  });
  it('V3 preserves both fields in every form after two lock toggles', () => {
    fixture();
    for (let i = 0; i < 2; i++) {
      document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card]')!.click();
      document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]').forEach(input => {
        expect(JSON.parse(input.value).originalEntries).toEqual(originals);
        expect(JSON.parse(input.value).categoryOverrides).toEqual(edits);
      });
    }
  });
  it('V7 updates quantity-weighted category locked counts', () => {
    fixture();
    document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card]')!.click();
    expect(document.querySelector('[data-cut-lab-category-locked]')!.textContent).toBe('98');
  });
});

describe('category modal', () => {
  const open = (entry: object, viewOnly = false): void => {
    fixture(entry);
    const chip = document.querySelector<HTMLButtonElement>('[data-cutlab-card-open]')!;
    if (viewOnly) chip.dataset.cutlabCardViewOnly = 'true';
    chip.click();
  };
  it('V4 populates the category editor and imported tags', () => {
    open({ category: 'Ramp', alsoTagged: 'Draw', categoryEdited: 'true' });
    expect(document.querySelector<HTMLElement>('[data-cut-lab-category-block]')!.hidden).toBe(false);
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-category-input]')!.value).toBe('Ramp');
    expect(document.querySelector('[data-cut-lab-category-current]')!.textContent).toBe('Category: Ramp');
    expect(document.querySelector('[data-cut-lab-category-also]')!.textContent).toBe('Also tagged: Draw');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-category-also]')!.hidden).toBe(false);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-category-reset]')!.hidden).toBe(false);
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-category-card]')!.value).toBe('Island');
  });
  it('V5 shows Uncategorized with no reset or other tags', () => {
    open({});
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-category-input]')!.value).toBe('');
    expect(document.querySelector('[data-cut-lab-category-current]')!.textContent).toBe('Category: Uncategorized');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-category-also]')!.hidden).toBe(true);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-category-reset]')!.hidden).toBe(true);
  });
  it('V6 hides the editor for a view-only card', () => {
    open({ category: 'Ramp' }, true);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-category-block]')!.hidden).toBe(true);
  });
  it('V8 renders category text without creating an image', () => {
    open({ category: '<img src=x onerror=alert(1)>' });
    expect(document.querySelector('#cutlab-card-modal img')).toBeNull();
  });
});
