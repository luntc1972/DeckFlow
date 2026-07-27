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
  includeSideboard: boolean;
  includeMaybeboard: boolean;
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
  goals: {
    commanderByTurn: number;
    engineByTurn: number;
    representativeLineByTurn: number;
  };
}

interface CutLabApi {
  computePackageCheckboxState(memberLocked: boolean[]): 'checked' | 'unchecked' | 'indeterminate';
  hasRoleToken(roleList: string | null | undefined, role: string): boolean;
  isLandRole(roleList: string | null | undefined): boolean;
  buildCutLabStateJson(snapshot: CutLabStateSnapshot): string;
}

let api: CutLabApi;
let showModalCalls = 0;

beforeAll(() => {
  api = (globalThis as unknown as { DeckFlowCutLab: CutLabApi }).DeckFlowCutLab;
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value(this: HTMLDialogElement) {
      showModalCalls += 1;
      this.setAttribute('open', '');
    },
  });
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value(this: HTMLDialogElement) {
      this.removeAttribute('open');
    },
  });
});

afterEach(() => {
  document.body.innerHTML = '';
  showModalCalls = 0;
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
        includeSideboard: false,
        includeMaybeboard: false,
      },
      roleFloors: [
        {
          role: 'interaction-targeted',
          floor: 15,
          isUserSet: true,
        },
        {
          role: 'draw',
          floor: 9,
          isUserSet: false,
        },
      ],
      goals: {
        commanderByTurn: 3,
        engineByTurn: 2,
        representativeLineByTurn: 4,
      },
    });

    expect(json).toBe(
      '{"commander":"Atraxa, Praetors\' Voice","pool":[{"name":"Atraxa, Praetors\' Voice","quantity":1,"typeLine":"Legendary Creature — Angel Horror","isCommander":true,"isLocked":true,"packageId":null},{"name":"Command Tower","quantity":1,"typeLine":"Land","isCommander":false,"isLocked":true,"packageId":"pkg-lands-1"}],"packages":[{"id":"pkg-lands-1","name":"Mana base","locked":true}],"decisions":[],"intent":{"primaryPlan":"Stick Atraxa and snowball card advantage.","secondaryPlan":"Protect the board with proliferate value.","bracket":4,"playExperience":"Focused","includeSideboard":false,"includeMaybeboard":false},"roleFloors":[{"role":"interaction-targeted","floor":15,"isUserSet":true}],"goals":{"commanderByTurn":3,"engineByTurn":2,"representativeLineByTurn":4}}',
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
        includeSideboard: false,
        includeMaybeboard: false,
      },
      roleFloors: [],
      goals: {
        commanderByTurn: 4,
        engineByTurn: 3,
        representativeLineByTurn: 5,
      },
    });

    expect(json).toBe(
      '{"commander":"Zur the Enchanter","pool":[],"packages":[],"decisions":[],"intent":{"primaryPlan":"Trim to the cleanest control shell.","secondaryPlan":null,"bracket":3,"playExperience":"Focused","includeSideboard":false,"includeMaybeboard":false},"roleFloors":[],"goals":{"commanderByTurn":4,"engineByTurn":3,"representativeLineByTurn":5}}',
    );
  });

  it('toggles matching role rows, syncs aria-pressed, and writes live role floors to CutLabStateJson', () => {
    const legacyStateJson = '{"commander":"Atraxa, Praetors\\\' Voice","pool":[{"name":"Atraxa, Praetors\\\' Voice","quantity":1,"typeLine":"Legendary Creature — Angel Horror","isCommander":true,"isLocked":true,"packageId":null},{"name":"Command Tower","quantity":1,"typeLine":"Land","isCommander":false,"isLocked":true,"packageId":"pkg-lands-1"}],"packages":[{"id":"pkg-lands-1","name":"Mana base","locked":true}],"decisions":[],"intent":{"primaryPlan":"Stick Atraxa and snowball card advantage.","secondaryPlan":"Protect the board with proliferate value.","bracket":4,"playExperience":"Focused","includeSideboard":false,"includeMaybeboard":false},"roleFloors":[{"role":"interaction","floor":15,"isUserSet":true}],"goals":{"commanderByTurn":3,"engineByTurn":2,"representativeLineByTurn":4}}';

    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value='${legacyStateJson}' />
        <textarea name="PrimaryPlan">Keep the control shell intact.</textarea>
        <textarea name="SecondaryPlan">Win through inevitability.</textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <p class="prompt-size-note"><span data-cut-lab-lock-count></span><span>(protected from any future cut)</span></p>
        <div class="cutlab-sticky-bar cutlab-sticky-bar--pool">
          <span class="cutlab-sticky-bar__count" data-cut-lab-pool-sticky-count></span>
          <span class="cutlab-sticky-bar__accepted" data-cut-lab-pool-sticky-breakdown>Main 99 · Sideboard 0 · Considering/Maybe 0</span>
        </div>
        <div class="cutlab-sticky-bar">
          <span class="cutlab-sticky-bar__locked" data-cut-lab-sticky-locked></span>
        </div>
        <details open>
          <summary>Lands</summary>
          <button type="button" data-cut-lab-lock-role="lands" aria-pressed="false">Lock all lands</button>
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
            <tr data-cut-lab-floor-row="interaction-targeted" data-cut-lab-floor-count="16" data-cut-lab-floor-default="5" data-cut-lab-floor-user-set="false">
              <td data-label="Role">Targeted removal</td>
              <td data-label="In pool">
                <span data-cut-lab-floor-count-label>16 in pool</span>
                <span class="cutlab-floor-state--at hidden" data-cut-lab-floor-at-marker>· at floor</span>
              </td>
              <td data-label="Floor">
                <input type="number" min="0" max="99" step="1" data-cut-lab-floor="interaction-targeted" value="5" />
              </td>
              <td data-label="Source">
                <span data-cut-lab-floor-source-default>Default for B3: 5</span>
                <span class="hidden" data-cut-lab-floor-adjusted-badge>Adjusted</span>
                <button type="button" class="hidden" data-cut-lab-floor-reset="interaction-targeted" data-cut-lab-floor-default="5">Reset to default</button>
              </td>
            </tr>
          </tbody>
        </table>
      </form>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-lock-role="lands"]');
    const summary = document.querySelector<HTMLElement>('[data-cut-lab-lock-count]');
    const poolStickySummary = document.querySelector<HTMLElement>('[data-cut-lab-pool-sticky-count]');
    const stickyLocked = document.querySelector<HTMLElement>('[data-cut-lab-sticky-locked]');
    const hiddenInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');
    const landCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Command Tower"]');
    const fetchLandCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Flooded Strand"]');
    const spellCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Mystic Remora"]');
    const floorInput = document.querySelector<HTMLInputElement>('input[data-cut-lab-floor="interaction-targeted"]');
    const floorMarker = document.querySelector<HTMLElement>('[data-cut-lab-floor-at-marker]');
    const adjustedBadge = document.querySelector<HTMLElement>('[data-cut-lab-floor-adjusted-badge]');
    const resetButton = document.querySelector<HTMLButtonElement>('[data-cut-lab-floor-reset="interaction-targeted"]');

    button?.click();
    expect(button?.getAttribute('aria-pressed')).toBe('true');

    button?.click();
    expect(button?.getAttribute('aria-pressed')).toBe('false');

    button?.click();
    floorInput?.setAttribute('value', '15');
    if (floorInput) {
      floorInput.value = '15';
      floorInput.dispatchEvent(new Event('input', { bubbles: true }));
    }

    expect(landCheckbox?.checked).toBe(true);
    expect(fetchLandCheckbox?.checked).toBe(true);
    expect(spellCheckbox?.checked).toBe(false);
    // 107-03: pool-status chip is now commander-inclusive (matches Compare panel basis) — 3 non-commander + Zur = 4.
    expect(summary?.textContent).toBe('4 cards in pool · 3 locked');
    expect(poolStickySummary?.textContent).toBe('4 cards in pool · 3 locked');
    expect(stickyLocked?.textContent).toBe('3 locked');
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
      includeSideboard: false,
      includeMaybeboard: false,
    });
    expect(parsed.roleFloors).toEqual([
      {
        role: 'interaction-targeted',
        floor: 15,
        isUserSet: true,
      },
    ]);
    expect(parsed.goals).toEqual({
      commanderByTurn: 3,
      engineByTurn: 2,
      representativeLineByTurn: 4,
    });
  });

  it('syncs role-group chip classes and quantity-weighted locked counts from pool-table checkbox state', () => {
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
            <tr data-cut-lab-card="Command Tower" data-cut-lab-type-line="Land" data-cut-lab-role="lands ramp" data-cut-lab-quantity="1" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
              <td data-label="Card"><strong>1 × Command Tower</strong></td>
              <td data-label="Package assignment">
                <select data-cut-lab-package-card="Command Tower">
                  <option value="">Unlocked pool</option>
                  <option value="__new__">+ New package…</option>
                </select>
              </td>
            </tr>
            <tr data-cut-lab-card="Flooded Strand" data-cut-lab-type-line="Land" data-cut-lab-role="lands" data-cut-lab-quantity="3" data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Flooded Strand" /></td>
              <td data-label="Card"><strong>3 × Flooded Strand</strong></td>
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
    expect(lockedCount?.textContent).toBe('4');
  });

  it('opens an individual card pill in the modal and toggles through the canonical pool checkbox', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the lands online.</textarea>
        <textarea name="SecondaryPlan"></textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <details class="cutlab-role-group" open>
          <summary>Lands · 1 card · <span data-cut-lab-group-locked="lands">0</span> locked</summary>
          <button type="button"
                  class="kb-chip cutlab-role-chip"
                  data-cutlab-card-open="Command Tower"
                  data-cut-lab-chip-card="Command Tower"
                  aria-pressed="false">Command Tower</button>
        </details>
        <table>
          <tbody>
            <tr data-cut-lab-card="Command Tower"
                data-cut-lab-type-line="Land"
                data-cut-lab-role="lands ramp"
                data-cut-lab-quantity="1"
                data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
              <td data-label="Card"><strong>1 × Command Tower</strong></td>
              <td data-label="Package">
                <select data-cut-lab-package-card="Command Tower">
                  <option value="">Unlocked pool</option>
                </select>
              </td>
            </tr>
          </tbody>
        </table>
      </form>
      <script type="application/json" id="cutlab-card-text-data">{"Command Tower":{"typeLine":"Land","oracleText":"Add one mana of any color in your commander's color identity."}}</script>
      <dialog id="cutlab-card-modal" aria-labelledby="cutlab-card-modal-title">
        <h2 id="cutlab-card-modal-title"></h2>
        <p data-cutlab-modal-meta hidden></p>
        <p data-cutlab-modal-oracle></p>
        <p data-cutlab-modal-combo hidden></p>
        <button type="button" data-cutlab-modal-lock></button>
        <button type="button" data-cutlab-modal-close>Close</button>
      </dialog>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const pill = document.querySelector<HTMLButtonElement>('[data-cut-lab-chip-card="Command Tower"]');
    const checkbox = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card="Command Tower"]');
    const hiddenInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');
    const modalLockButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');

    pill?.click();

    expect(showModalCalls).toBe(1);
    expect(modalLockButton?.textContent).toBe('Lock');
    expect(checkbox?.checked).toBe(false);

    modalLockButton?.click();

    expect(checkbox?.checked).toBe(true);
    expect(pill?.getAttribute('aria-pressed')).toBe('true');
    expect(pill?.classList.contains('cutlab-role-chip--locked')).toBe(true);
    expect(JSON.parse(hiddenInput?.value ?? '').pool[0].isLocked).toBe(true);

    modalLockButton?.click();

    expect(checkbox?.checked).toBe(false);
    expect(pill?.getAttribute('aria-pressed')).toBe('false');
    expect(pill?.classList.contains('cutlab-role-chip--locked')).toBe(false);
  });

  it('opens the modal when a combo badge span is nested inside the card pill button', () => {
    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the lands online.</textarea>
        <textarea name="SecondaryPlan"></textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <details class="cutlab-role-group" open>
          <summary>Lands · 1 card · <span data-cut-lab-group-locked="lands">0</span> locked</summary>
          <button type="button"
                  class="kb-chip cutlab-role-chip"
                  data-cutlab-card-open="Command Tower"
                  data-cut-lab-chip-card="Command Tower"
                  aria-pressed="false">
            <span>Command Tower</span>
            <span class="cutlab-combo-badge cutlab-combo-badge--complete">Combo piece</span>
          </button>
        </details>
        <table>
          <tbody>
            <tr data-cut-lab-card="Command Tower"
                data-cut-lab-type-line="Land"
                data-cut-lab-role="lands ramp"
                data-cut-lab-quantity="1"
                data-cut-lab-commander="false">
              <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
              <td data-label="Card"><strong>1 × Command Tower</strong></td>
              <td data-label="Package">
                <select data-cut-lab-package-card="Command Tower">
                  <option value="">Unlocked pool</option>
                </select>
              </td>
            </tr>
          </tbody>
        </table>
      </form>
      <script type="application/json" id="cutlab-card-text-data">{"Command Tower":{"typeLine":"Land","oracleText":"Add one mana of any color in your commander's color identity."}}</script>
      <dialog id="cutlab-card-modal" aria-labelledby="cutlab-card-modal-title">
        <h2 id="cutlab-card-modal-title"></h2>
        <p data-cutlab-modal-meta hidden></p>
        <p data-cutlab-modal-oracle></p>
        <p data-cutlab-modal-combo hidden></p>
        <button type="button" data-cutlab-modal-lock></button>
        <button type="button" data-cutlab-modal-close>Close</button>
      </dialog>
    `;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const pill = document.querySelector<HTMLButtonElement>('[data-cut-lab-chip-card="Command Tower"]');
    const badge = pill?.querySelector<HTMLSpanElement>('.cutlab-combo-badge');
    const checkbox = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card="Command Tower"]');
    const modalLockButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');

    badge?.click();

    expect(showModalCalls).toBe(1);
    expect(checkbox?.checked).toBe(false);

    modalLockButton?.click();

    expect(checkbox?.checked).toBe(true);
    expect(pill?.getAttribute('aria-pressed')).toBe('true');
    expect(pill?.classList.contains('cutlab-role-chip--locked')).toBe(true);

    modalLockButton?.click();

    expect(checkbox?.checked).toBe(false);
    expect(pill?.getAttribute('aria-pressed')).toBe('false');
    expect(pill?.classList.contains('cutlab-role-chip--locked')).toBe(false);
  });

  it('syncs every CutLabStateJson input when a pool lock changes', () => {
    const staleSnapshot = api.buildCutLabStateJson({
      commander: 'Aesi, Tyrant of Gyre Strait',
      pool: [
        {
          name: 'Forest',
          quantity: 1,
          typeLine: 'Basic Land — Forest',
          isCommander: false,
          isLocked: false,
          packageId: null,
        },
      ],
      packages: [],
      intent: {
        primaryPlan: 'Keep the mana base stable.',
        secondaryPlan: null,
        bracket: 3,
        playExperience: 'Focused',
        includeSideboard: false,
        includeMaybeboard: false,
      },
      roleFloors: [],
      goals: {
        commanderByTurn: 4,
        engineByTurn: 3,
        representativeLineByTurn: 5,
      },
    });

    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value="" />
        <textarea name="PrimaryPlan">Keep the mana base stable.</textarea>
        <textarea name="SecondaryPlan"></textarea>
        <input type="radio" name="Bracket" value="3" checked />
        <input type="radio" name="PlayExperience" value="Focused" checked />
        <table>
          <tbody>
            <tr data-cut-lab-card="Forest"
                data-cut-lab-type-line="Basic Land — Forest"
                data-cut-lab-role="lands"
                data-cut-lab-quantity="1"
                data-cut-lab-commander="false">
              <td data-label="Lock"><input type="checkbox" data-cut-lab-lock-card="Forest" /></td>
              <td data-label="Card"><strong>1 × Forest</strong></td>
              <td data-label="Package">
                <select data-cut-lab-package-card="Forest">
                  <option value="">Unlocked pool</option>
                </select>
              </td>
            </tr>
          </tbody>
        </table>
      </form>
      <form data-cut-lab-decide-form>
        <input type="hidden" name="CutLabStateJson" value="" />
        <input type="hidden" name="CardName" value="Forest" />
        <input type="hidden" name="Decision" value="accept" />
      </form>
    `;

    Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).forEach(input => {
      input.value = staleSnapshot;
    });

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const checkbox = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card="Forest"]');
    checkbox?.click();
    checkbox?.dispatchEvent(new Event('change', { bubbles: true }));

    const stateInputs = Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]'));
    expect(stateInputs).toHaveLength(2);

    stateInputs.forEach(input => {
      const parsed = JSON.parse(input.value) as CutLabStateSnapshot;
      expect(parsed.pool).toEqual([
        {
          name: 'Forest',
          quantity: 1,
          typeLine: 'Basic Land — Forest',
          isCommander: false,
          isLocked: true,
          packageId: null,
        },
      ]);
    });
  });
});
