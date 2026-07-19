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

interface CutLabRoleFloorSnapshot {
  role: string;
  floor: number;
  isUserSet: boolean;
}

interface CutLabStateSnapshot {
  commander: string;
  pool: CutLabPoolSnapshotCard[];
  packages: CutLabPackageSnapshot[];
  intent: CutLabIntentSnapshot;
  roleFloors: CutLabRoleFloorSnapshot[];
}

interface CutLabApi {
  computePackageCheckboxState(memberLocked: boolean[]): 'checked' | 'unchecked' | 'indeterminate';
  hasRoleToken(roleList: string | null | undefined, role: string): boolean;
  isLandRole(roleList: string | null | undefined): boolean;
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

  it('matches role tokens exactly across multi-role values', () => {
    expect(api.hasRoleToken('lands ramp', 'lands')).toBe(true);
    expect(api.hasRoleToken('lands ramp', 'ramp')).toBe(true);
    expect(api.hasRoleToken('land', 'lands')).toBe(false);
    expect(api.hasRoleToken(null, 'lands')).toBe(false);
    expect(api.hasRoleToken('LANDS', 'lands')).toBe(true);
    expect(api.isLandRole('lands draw')).toBe(true);
  });

  it('serializes the exact camelCase contract including user-set role floors', () => {
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
      roleFloors: [
        {
          role: 'interaction',
          floor: 15,
          isUserSet: true,
        },
        {
          role: 'draw',
          floor: 9,
          isUserSet: false,
        },
      ],
    });

    expect(json).toBe(
      '{"commander":"Atraxa, Praetors\' Voice","pool":[{"name":"Atraxa, Praetors\' Voice","quantity":1,"typeLine":"Legendary Creature — Angel Horror","isCommander":true,"isLocked":true,"packageId":null},{"name":"Command Tower","quantity":1,"typeLine":"Land","isCommander":false,"isLocked":true,"packageId":"pkg-lands-1"}],"packages":[{"id":"pkg-lands-1","name":"Mana base","locked":true}],"intent":{"primaryPlan":"Stick Atraxa and snowball card advantage.","secondaryPlan":"Protect the board with proliferate value.","bracket":4,"playExperience":"Focused"},"roleFloors":[{"role":"interaction","floor":15,"isUserSet":true}]}',
    );
  });

  it('serializes an empty roleFloors array when no floor rows are user-set', () => {
    const json = api.buildCutLabStateJson({
      commander: 'Zur the Enchanter',
      pool: [],
      packages: [],
      intent: {
        primaryPlan: 'Trim to the cleanest control shell.',
        secondaryPlan: null,
        bracket: 3,
        playExperience: 'Focused',
      },
      roleFloors: [],
    });

    expect(json).toBe(
      '{"commander":"Zur the Enchanter","pool":[],"packages":[],"intent":{"primaryPlan":"Trim to the cleanest control shell.","secondaryPlan":null,"bracket":3,"playExperience":"Focused"},"roleFloors":[]}',
    );
  });

  it('bulk-locks matching role rows and writes live role floors to CutLabStateJson', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the control shell intact.</textarea>
        <textarea name="SecondaryPlan">Win through inevitability.</textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <p class="prompt-size-note"><span data-cut-lab-lock-count></span><span>(protected from any future cut)</span></p>
        <details open>
          <summary>Lands</summary>
          <button type="button" data-cut-lab-lock-role="lands">Lock all lands</button>
        </details>
        <table>
          <tbody>
            <tr data-cut-lab-card="Zur the Enchanter" data-cut-lab-type-line="Legendary Creature — Human Wizard" data-cut-lab-role="payoffs wincons" data-cut-lab-commander="true">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Zur the Enchanter" checked disabled /></td>
              <td data-label="Card"><strong>1 × Zur the Enchanter</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Zur the Enchanter">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Command Tower" data-cut-lab-type-line="Land" data-cut-lab-role="lands ramp" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
              <td data-label="Card"><strong>1 × Command Tower</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Command Tower">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Flooded Strand" data-cut-lab-type-line="Land" data-cut-lab-role="lands" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Flooded Strand" /></td>
              <td data-label="Card"><strong>1 × Flooded Strand</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Flooded Strand">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Mystic Remora" data-cut-lab-type-line="Enchantment" data-cut-lab-role="draw engines" data-cut-lab-commander="false">
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
        <table>
          <tbody>
            <tr data-cut-lab-floor-row="interaction" data-cut-lab-floor-count="16" data-cut-lab-floor-default="12" data-cut-lab-floor-user-set="false">
              <td data-label="Role">Interaction</td>
              <td data-label="In pool">
                <span data-cut-lab-floor-count-label>16 in pool</span>
                <span class="cutlab-floor-state--at hidden" data-cut-lab-floor-at-marker>· at floor</span>
              </td>
              <td data-label="Floor">
                <input type="number" min="0" max="99" step="1" data-cut-lab-floor="interaction" value="12" />
              </td>
              <td data-label="Source">
                <span data-cut-lab-floor-source-default>Default for B3: 12</span>
                <span class="hidden" data-cut-lab-floor-adjusted-badge>Adjusted</span>
                <button type="button" class="hidden" data-cut-lab-floor-reset="interaction" data-cut-lab-floor-default="12">Reset to default</button>
              </td>
            </tr>
          </tbody>
        </table>
      </form>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-lock-role="lands"]');
    const summary = document.querySelector<HTMLElement>('[data-cut-lab-lock-count]');
    const hiddenInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');
    const landCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Command Tower"]');
    const fetchLandCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Flooded Strand"]');
    const spellCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Mystic Remora"]');
    const floorInput = document.querySelector<HTMLInputElement>('input[data-cut-lab-floor="interaction"]');
    const floorMarker = document.querySelector<HTMLElement>('[data-cut-lab-floor-at-marker]');
    const adjustedBadge = document.querySelector<HTMLElement>('[data-cut-lab-floor-adjusted-badge]');
    const resetButton = document.querySelector<HTMLButtonElement>('[data-cut-lab-floor-reset="interaction"]');

    button?.click();
    floorInput?.setAttribute('value', '15');
    if (floorInput) {
      floorInput.value = '15';
      floorInput.dispatchEvent(new Event('input', { bubbles: true }));
    }

    expect(landCheckbox?.checked).toBe(true);
    expect(fetchLandCheckbox?.checked).toBe(true);
    expect(spellCheckbox?.checked).toBe(false);
    expect(summary?.textContent).toBe('3 cards in pool · 3 locked');
    expect(floorMarker?.classList.contains('hidden')).toBe(false);
    expect(adjustedBadge?.classList.contains('hidden')).toBe(false);
    expect(resetButton?.classList.contains('hidden')).toBe(false);

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
        name: 'Flooded Strand',
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
    expect(parsed.roleFloors).toEqual([
      {
        role: 'interaction',
        floor: 15,
        isUserSet: true,
      },
    ]);
  });

  it('syncs role-group chip classes and locked counts from pool-table checkbox state', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the lands online.</textarea>
        <textarea name="SecondaryPlan"></textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <details class="cutlab-role-group" open>
          <summary class="cutlab-role-group__summary">
            Lands · 2 cards · <span data-cut-lab-group-locked="lands">0</span> locked
          </summary>
          <div class="cutlab-role-group__body">
            <button type="button" data-cut-lab-lock-role="lands">Lock all lands</button>
            <div class="kb-chip-area__chips">
              <span class="kb-chip" data-cut-lab-chip-card="Command Tower">Command Tower</span>
              <span class="kb-chip" data-cut-lab-chip-card="Flooded Strand">Flooded Strand</span>
            </div>
          </div>
        </details>
        <table>
          <tbody>
            <tr data-cut-lab-card="Command Tower" data-cut-lab-type-line="Land" data-cut-lab-role="lands ramp" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
              <td data-label="Card"><strong>1 × Command Tower</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Command Tower">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Flooded Strand" data-cut-lab-type-line="Land" data-cut-lab-role="lands" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Flooded Strand" /></td>
              <td data-label="Card"><strong>1 × Flooded Strand</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Flooded Strand">
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

    const lockAllButton = document.querySelector<HTMLButtonElement>('[data-cut-lab-lock-role="lands"]');
    const firstChip = document.querySelector<HTMLElement>('[data-cut-lab-chip-card="Command Tower"]');
    const secondChip = document.querySelector<HTMLElement>('[data-cut-lab-chip-card="Flooded Strand"]');
    const lockedCount = document.querySelector<HTMLElement>('[data-cut-lab-group-locked="lands"]');

    lockAllButton?.click();

    expect(firstChip?.classList.contains('cutlab-role-chip--locked')).toBe(true);
    expect(secondChip?.classList.contains('cutlab-role-chip--locked')).toBe(true);
    expect(lockedCount?.textContent).toBe('2');
  });
});
