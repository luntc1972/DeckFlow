import { afterEach, beforeAll, describe, expect, it } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabPoolSnapshotCard {
  name: string;
  quantity: number;
  typeLine: string;
  isCommander: boolean;
  isLocked: boolean;
  packageId: string | null;
}

interface CutLabPackageSnapshot {
  id: string;
  name: string;
  locked: boolean;
}

interface CutLabIntentSnapshot {
  primaryPlan: string;
  secondaryPlan: string | null;
  bracket: number | null;
  playExperience: string;
}

interface CutLabStateSnapshot {
  commander: string;
  pool: CutLabPoolSnapshotCard[];
  packages: CutLabPackageSnapshot[];
  intent: CutLabIntentSnapshot;
}

interface CutLabApi {
  computePackageCheckboxState(memberLocked: boolean[]): 'checked' | 'unchecked' | 'indeterminate';
  isLandRole(role: string | null | undefined): boolean;
  buildCutLabStateJson(snapshot: CutLabStateSnapshot): string;
}

let api: CutLabApi;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCutLab: CutLabApi }).DeckFlowCutLab;
});

afterEach(() => {
  document.body.innerHTML = '';
});

describe('DeckFlowCutLab', () => {
  it('computes package checkbox states for checked, unchecked, and mixed members', () => {
    expect(api.computePackageCheckboxState([true, true])).toBe('checked');
    expect(api.computePackageCheckboxState([false, false])).toBe('unchecked');
    expect(api.computePackageCheckboxState([true, false])).toBe('indeterminate');
  });

  it('treats only the server-provided land role as a land', () => {
    expect(api.isLandRole('land')).toBe(true);
    expect(api.isLandRole('LAND')).toBe(true);
    expect(api.isLandRole('creature')).toBe(false);
    expect(api.isLandRole(null)).toBe(false);
  });

  it('serializes the exact camelCase contract and forces the commander locked', () => {
    const json = api.buildCutLabStateJson({
      commander: 'Atraxa, Praetors\' Voice',
      pool: [
        {
          name: 'Atraxa, Praetors\' Voice',
          quantity: 1,
          typeLine: 'Legendary Creature — Angel Horror',
          isCommander: true,
          isLocked: false,
          packageId: null,
        },
        {
          name: 'Command Tower',
          quantity: 1,
          typeLine: 'Land',
          isCommander: false,
          isLocked: true,
          packageId: 'pkg-lands-1',
        },
      ],
      packages: [{ id: 'pkg-lands-1', name: 'Mana base', locked: true }],
      intent: {
        primaryPlan: 'Stick Atraxa and snowball card advantage.',
        secondaryPlan: 'Protect the board with proliferate value.',
        bracket: 4,
        playExperience: 'Focused',
      },
    });

    expect(json).toBe(
      '{"commander":"Atraxa, Praetors\' Voice","pool":[{"name":"Atraxa, Praetors\' Voice","quantity":1,"typeLine":"Legendary Creature — Angel Horror","isCommander":true,"isLocked":true,"packageId":null},{"name":"Command Tower","quantity":1,"typeLine":"Land","isCommander":false,"isLocked":true,"packageId":"pkg-lands-1"}],"packages":[{"id":"pkg-lands-1","name":"Mana base","locked":true}],"intent":{"primaryPlan":"Stick Atraxa and snowball card advantage.","secondaryPlan":"Protect the board with proliferate value.","bracket":4,"playExperience":"Focused"}}',
    );
  });

  it('bulk-locks only land rows and writes the live camelCase state to CutLabStateJson', () => {
    document.body.innerHTML = `
      <form action="/cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the control shell intact.</textarea>
        <textarea name="SecondaryPlan">Win through inevitability.</textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <p class="prompt-size-note"></p>
        <button type="button" data-cut-lab-lock-all-lands>Lock all lands</button>
        <table>
          <tbody>
            <tr data-cut-lab-card="Zur the Enchanter" data-cut-lab-type-line="Legendary Creature — Human Wizard" data-cut-lab-role="" data-cut-lab-commander="true">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Zur the Enchanter" checked disabled /></td>
              <td data-label="Card"><strong>1 × Zur the Enchanter</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Zur the Enchanter">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Command Tower" data-cut-lab-type-line="Land" data-cut-lab-role="land" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
              <td data-label="Card"><strong>1 × Command Tower</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Command Tower">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Mystic Remora" data-cut-lab-type-line="Enchantment" data-cut-lab-role="" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Mystic Remora" /></td>
              <td data-label="Card"><strong>1 × Mystic Remora</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Mystic Remora">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
          </tbody>
        </table>
        <section class="result-panel nested-panel">
          <div class="card-picker__rows">
            <div class="card-picker__row hidden" data-cut-lab-new-package-row>
              <div class="card-picker__input-shell">
                <input class="card-picker__input" type="text" data-cut-lab-new-package-input />
              </div>
              <button type="button" data-cut-lab-new-package-save>+</button>
              <button type="button" data-cut-lab-new-package-cancel>x</button>
            </div>
          </div>
        </section>
      </form>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-lock-all-lands]');
    const summary = document.querySelector<HTMLElement>('.prompt-size-note');
    const hiddenInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');
    const landCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Command Tower"]');
    const spellCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Mystic Remora"]');

    button?.click();

    expect(landCheckbox?.checked).toBe(true);
    expect(spellCheckbox?.checked).toBe(false);
    expect(summary?.textContent).toBe('2 cards in pool · 2 locked (protected from any future cut)');

    const parsed = JSON.parse(hiddenInput?.value ?? '') as CutLabStateSnapshot;
    expect(parsed.commander).toBe('Zur the Enchanter');
    expect(parsed.pool).toEqual([
      {
        name: 'Zur the Enchanter',
        quantity: 1,
        typeLine: 'Legendary Creature — Human Wizard',
        isCommander: true,
        isLocked: true,
        packageId: null,
      },
      {
        name: 'Command Tower',
        quantity: 1,
        typeLine: 'Land',
        isCommander: false,
        isLocked: true,
        packageId: null,
      },
      {
        name: 'Mystic Remora',
        quantity: 1,
        typeLine: 'Enchantment',
        isCommander: false,
        isLocked: false,
        packageId: null,
      },
    ]);
    expect(parsed.packages).toEqual([]);
    expect(parsed.intent).toEqual({
      primaryPlan: 'Keep the control shell intact.',
      secondaryPlan: 'Win through inevitability.',
      bracket: 3,
      playExperience: 'Focused',
    });
  });
});
