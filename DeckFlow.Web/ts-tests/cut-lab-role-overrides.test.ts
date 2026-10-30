import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';

import '../wwwroot/ts/cut-lab';

let fetchMock: any;

interface CutLabUiPatch {
  cutLabStateJson: string;
  currentCount: number;
  cardsRemaining: number;
  canBuildExport: boolean;
  nextProposal: {
    isTerminal: boolean;
    isAtTarget: boolean;
    isNothingToCut: boolean;
    cardName: string;
    roundKey: string;
    roundLabel: string;
    roundBannerBody: string;
    findingCount: number;
    findingChips: string[];
  } | null;
  proposalDeltas: null;
  floorWarnings: Array<unknown>;
  cutsMade: Array<unknown>;
  structuralFindings: Array<unknown>;
  comboDataAvailable: boolean;
  categoryDataAvailable: boolean;
  whatifCardOutOptions: string[];
  whatifCardInOptions: string[];
  quantityTuners: Array<{
    cardName: string;
    currentQuantity: number;
    legalMax: number;
    removeDisabled: boolean;
    addDisabled: boolean;
    isLockedOrCommander: boolean;
    isVisible: boolean;
    roleLabel: string;
    isLegalMultiple: boolean;
    isAddedBasic: boolean;
  }>;
  addableBasics: string[];
}

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  document.body.innerHTML = '';
  fetchMock.mockReset();
});

const flushAdjustSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

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

const buildPatch = (
  stateJson: string,
  overrides: Partial<CutLabUiPatch> = {},
): CutLabUiPatch => ({
  cutLabStateJson: stateJson,
  currentCount: 99,
  cardsRemaining: 0,
  canBuildExport: false,
  nextProposal: {
    isTerminal: false,
    isAtTarget: false,
    isNothingToCut: false,
    cardName: 'Island',
    roundKey: 'round-1',
    roundLabel: 'Round 1',
    roundBannerBody: 'Keep tuning basics.',
    findingCount: 0,
    findingChips: [],
  },
  proposalDeltas: null,
  floorWarnings: [],
  cutsMade: [],
  structuralFindings: [],
  comboDataAvailable: true,
  categoryDataAvailable: true,
  whatifCardOutOptions: [],
  whatifCardInOptions: [],
  quantityTuners: [
    {
      cardName: 'Island',
      currentQuantity: 98,
      legalMax: 150,
      removeDisabled: false,
      addDisabled: false,
      isLockedOrCommander: false,
      isVisible: true,
      roleLabel: 'Lands',
      isLegalMultiple: true,
      isAddedBasic: false,
    },
  ],
  addableBasics: ['Plains', 'Swamp'],
  ...overrides,
});

const buildFixture = (options: { includePlainsRow?: boolean; addableBasics?: string[]; stateWriters?: boolean } = {}): void => {
  const stateJson = buildStateJson(98);
  const addableBasics = options.addableBasics ?? ['Plains', 'Swamp'];
  const addBasicFormMarkup = addableBasics.length > 0
    ? `
      <form method="post" action="/cut-lab/adjust" data-cut-lab-adjust-form class="toolbar cutlab-tuner__add-basic">
        <input type="hidden" name="__RequestVerificationToken" value="token-123" />
        <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
        <input type="hidden" name="Delta" value="1" />
        <input type="hidden" name="IsAddedBasic" value="true" />
        <label for="cut-lab-add-basic-select">Add a basic land</label>
        <select id="cut-lab-add-basic-select" name="CardName" data-cut-lab-add-basic-select required>
          <option value="" selected disabled>Choose a basic…</option>
          ${addableBasics.map(cardName => `<option value="${cardName}">${cardName}</option>`).join('')}
        </select>
        <button type="submit" class="run-button" data-cut-lab-adjust data-cut-lab-add-basic data-cut-lab-delta="1" data-cut-lab-added-basic="true">Add basic land</button>
      </form>`
    : '<p class="cutlab-floor-source-default">All basic land types are already in your working list. Use the steppers above to add more copies.</p>';
  const plainsRowMarkup = options.includePlainsRow
    ? `
        <tr data-cut-lab-tuner-row="Plains" data-cut-lab-quantity="1" data-cut-lab-legal-max="150">
          <td data-label="Card"><strong>Plains</strong><span class="kb-chip cutlab-tuner-badge--added">Added</span></td>
          <td data-label="Role">Lands</td>
          <td data-label="Quantity">
            <div class="cutlab-stepper">
              <form method="post" action="/cut-lab/adjust" data-cut-lab-adjust-form>
                <input type="hidden" name="__RequestVerificationToken" value="token-123" />
                <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
                <input type="hidden" name="CardName" value="Plains" />
                <input type="hidden" name="Delta" value="-1" />
                <input type="hidden" name="IsAddedBasic" value="true" />
                <button type="submit" class="cutlab-stepper-btn" data-cut-lab-adjust data-cut-lab-card="Plains" data-cut-lab-delta="-1">−</button>
              </form>
              <span class="cutlab-stepper__count tabular" data-cut-lab-quantity-value>1</span>
              <form method="post" action="/cut-lab/adjust" data-cut-lab-adjust-form>
                <input type="hidden" name="__RequestVerificationToken" value="token-123" />
                <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
                <input type="hidden" name="CardName" value="Plains" />
                <input type="hidden" name="Delta" value="1" />
                <input type="hidden" name="IsAddedBasic" value="true" />
                <button type="submit" class="cutlab-stepper-btn" data-cut-lab-adjust data-cut-lab-card="Plains" data-cut-lab-delta="1">+</button>
              </form>
            </div>
          </td>
        </tr>`
    : '';

  document.body.innerHTML = `
    <div class="error-banner hidden" role="alert"></div>
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <table>
        <tbody>
          <tr data-cut-lab-card="Commander" data-cut-lab-quantity="1" data-cut-lab-type-line="Legendary Creature" data-cut-lab-role="draw" data-cut-lab-commander="true">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Commander" checked disabled /></td>
            <td data-label="Card"><strong>1 × Commander</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Commander"><option value="">Unlocked pool</option></select></td>
          </tr>
        </tbody>
      </table>
    </form>
    <form id="cut-lab-export-form">
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <button type="submit" class="run-button" disabled>Build export</button>
    </form>
    <section class="result-panel">
      <div class="cutlab-sticky-bar">
        <span data-cut-lab-sticky-locked>1 locked</span>
        <span data-cut-lab-sticky-current>99/100 cards</span>
        <span data-cut-lab-sticky-round>Round 1</span>
        <span data-cut-lab-sticky-remaining>1 to cut</span>
        <span data-cut-lab-sticky-accepted>0 cuts so far</span>
      </div>
      <button type="button" id="cut-lab-step-tab-5" class="prompt-step-tab" aria-disabled="true">Export</button>
    </section>
    <section class="result-panel">
      <div class="cutlab-round-banner">
        <p>Keep tuning basics.</p>
      </div>
      <div class="cutlab-proposal" data-cut-lab-card="Island" data-cut-lab-round="round-1">
        <p class="cutlab-proposal__heading">Proposed cut: Island</p>
        <form method="post">
          <input type="hidden" name="__RequestVerificationToken" value="token-123" />
          <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
          <input type="hidden" name="CardName" value="Island" />
          <input type="hidden" name="Decision" value="accept" />
          <button type="submit" data-cut-lab-decision-submit>Accept</button>
        </form>
        <div class="cutlab-proposal__evidence">
          <p>Flagged by 1 findings:</p>
          <div class="kb-chip-area__chips">
            <span class="kb-chip">Mana</span>
          </div>
        </div>
      </div>
    </section>
    <section class="result-panel" data-cut-lab-structural-findings>
      <div class="panel-heading">
        <div data-cut-lab-findings-count-slot>
          <span class="cutlab-findings-count">1 structural finding</span>
        </div>
      </div>
      <div data-cut-lab-structural-findings-body>
        <div class="cutlab-finding">
          <p class="cutlab-finding__heading">Weak floor cases</p>
          <div class="cutlab-finding__item">
            <p class="cutlab-finding__lead">Existing structural issue.</p>
          </div>
        </div>
      </div>
      <p data-cut-lab-degradation="combo" class="hidden"></p>
      <p data-cut-lab-degradation="category" class="hidden"></p>
    </section>
    <section class="result-panel" data-cut-lab-cuts-made-section="true">
      <details class="cutlab-cuts-made" open>
        <summary>Cuts made · 1 card</summary>
        <div class="cutlab-cuts-made__row">
          <span>Old Cut</span>
          <span class="prompt-size-note">cut in Round 1</span>
        </div>
      </details>
    </section>
    <section class="result-panel nested-panel cutlab-tuner">
      <div class="history-timeline__wrap">
        <table>
          <tbody>
            <tr data-cut-lab-tuner-row="Island" data-cut-lab-quantity="98" data-cut-lab-legal-max="150">
              <td data-label="Card"><strong>Island</strong></td>
              <td data-label="Role">Lands</td>
              <td data-label="Quantity">
                <div class="cutlab-stepper">
                  <form method="post" action="/cut-lab/adjust" data-cut-lab-adjust-form>
                    <input type="hidden" name="__RequestVerificationToken" value="token-123" />
                    <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
                    <input type="hidden" name="CardName" value="Island" />
                    <input type="hidden" name="Delta" value="-1" />
                    <input type="hidden" name="IsAddedBasic" value="false" />
                    <button type="submit" class="cutlab-stepper-btn" data-cut-lab-adjust data-cut-lab-card="Island" data-cut-lab-delta="-1">−</button>
                  </form>
                  <span class="cutlab-stepper__count tabular" data-cut-lab-quantity-value>98</span>
                  <form method="post" action="/cut-lab/adjust" data-cut-lab-adjust-form>
                    <input type="hidden" name="__RequestVerificationToken" value="token-123" />
                    <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
                    <input type="hidden" name="CardName" value="Island" />
                    <input type="hidden" name="Delta" value="1" />
                    <input type="hidden" name="IsAddedBasic" value="false" />
                    <button type="submit" class="cutlab-stepper-btn" data-cut-lab-adjust data-cut-lab-card="Island" data-cut-lab-delta="1">+</button>
                  </form>
                </div>
              </td>
            </tr>
            ${plainsRowMarkup}
          </tbody>
        </table>
      </div>
      ${addBasicFormMarkup}
    </section>
  `;

  if (options.stateWriters) {
    document.body.insertAdjacentHTML('beforeend', '<table><tbody><tr data-cut-lab-card="Test Writer" data-cut-lab-quantity="1"><td><select data-cut-lab-package-card><option value="">None</option><option value="test">Test</option></select></td></tr></tbody></table>');
    document.body.insertAdjacentHTML('beforeend', '<table><tbody><tr data-cut-lab-floor-row="draw"><td><input data-cut-lab-floor type="number" value="0" /></td></tr></tbody></table>');
  }
  document.dispatchEvent(new Event('DOMContentLoaded'));
};


const overrides = [{ name: 'Relentless Rats', added: ['draw'] }];
const api = (): any => (window as any).DeckFlowCutLab;
const editor = (): void => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', ''); } });
  document.body.insertAdjacentHTML('beforeend', `
    <button data-cutlab-card-open="Plains">Plains</button>
    <button data-cutlab-card-open="Relentless Rats">Rats</button>
    <button data-cutlab-card-open="Commander">Commander</button>
    <button data-cutlab-card-open="Unknown">Unknown</button>
    <button data-cutlab-card-open="Plains" data-cutlab-card-view-only="true">View only</button>
    <script id="cutlab-card-text-data" type="application/json">${JSON.stringify({
      Plains: { roleEditable: 'true', roleLand: 'true', roles: 'Lands' },
      'Relentless Rats': { roleEditable: 'true', roleEdited: 'true', roles: '<b>Card draw</b>' },
      Commander: { oracleText: 'Commander' },
    })}</script>
    <dialog id="cutlab-card-modal"><h2 id="cutlab-card-modal-title"></h2>
      <p data-cutlab-modal-oracle></p>
      <div data-cut-lab-role-block hidden><p data-cut-lab-role-current></p>
        <form data-cut-lab-role-override-form><input name="CutLabStateJson" />
          <input name="CardName" data-cut-lab-role-override-card />
          <select data-cut-lab-role-override-role><option value="ramp">Ramp</option><option value="draw">Card draw</option></select>
          <button data-cut-lab-role-reset hidden>Reset card roles</button>
        </form>
      </div>
    </dialog>`);
};
const openRoleCard = (name: string, viewOnly = false): void => {
  document.querySelector<HTMLButtonElement>(`[data-cutlab-card-open="${name}"]${viewOnly ? '[data-cutlab-card-view-only]' : ':not([data-cutlab-card-view-only])'}`)!.click();
};
const roleText = (name: string): string => document.querySelector(`[data-cut-lab-tuner-row="${name}"] td[data-label="Role"]`)?.textContent?.trim() ?? '';
const setupTuners = (includePlains: boolean): void => {
  buildFixture({ includePlainsRow: includePlains, addableBasics: ['Plains'] });
  const body = document.querySelector('[data-cut-lab-tuner-row="Island"]')!.parentElement!;
  body.querySelector('[data-cut-lab-tuner-row="Island"]')!.remove();
  body.insertAdjacentHTML('beforeend', `<tr data-cut-lab-tuner-row="Relentless Rats"><td data-label="Role">Card draw · Payoffs</td></tr><tr data-cut-lab-tuner-row="Persistent Petitioners"><td data-label="Role">Engines</td></tr>`);
  if (includePlains) body.querySelector('[data-cut-lab-tuner-row="Plains"] td[data-label="Role"]')!.textContent = 'Lands · Card draw';
};
const adjust = async (newBasic: boolean): Promise<void> => {
  const rows = ['Relentless Rats', 'Persistent Petitioners', 'Plains'].map(cardName => ({
    cardName, currentQuantity: 2, legalMax: 150, removeDisabled: false, addDisabled: false,
    isLockedOrCommander: false, isVisible: true, isLegalMultiple: true,
    isAddedBasic: newBasic && cardName === 'Plains', roleLabel: cardName === 'Plains' ? (newBasic ? 'Lands' : 'Lands · Card draw') : '',
  }));
  fetchMock.mockResolvedValue({ ok: true, json: async () => ({ patch: buildPatch(buildStateJson(98), { quantityTuners: rows }) }) });
  const button = document.querySelector<HTMLButtonElement>(newBasic ? '[data-cut-lab-add-basic]' : '[data-cut-lab-tuner-row="Plains"] [data-cut-lab-delta="1"]')!;
  if (newBasic) document.querySelector<HTMLSelectElement>('[data-cut-lab-add-basic-select]')!.value = 'Plains';
  button.closest('form')!.dispatchEvent(new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: button }));
  await flushAdjustSubmit();
  expect(fetchMock).toHaveBeenCalledTimes(1);
};

describe('role overrides', () => {
  it('disables ramp for lands, refreshes the select, and re-enables ramp for nonlands', () => {
    buildFixture(); editor();
    const refresh = vi.fn();
    (window as any).DeckFlow = { ...(window as any).DeckFlow, refreshDfSelect: refresh };
    const role = document.querySelector<HTMLSelectElement>('[data-cut-lab-role-override-role]')!;
    openRoleCard('Plains');
    expect(role.options[0].disabled).toBe(true);
    expect(role.value).toBe('draw');
    expect(refresh).toHaveBeenCalledWith(role);
    openRoleCard('Relentless Rats');
    expect(role.options[0].disabled).toBe(false);
  });
  it('serializes only non-empty overrides', () => {
    const state = JSON.parse(buildStateJson(98));
    expect(JSON.parse(api().buildCutLabStateJson(state))).not.toHaveProperty('roleOverrides');
    expect(JSON.parse(api().buildCutLabStateJson({ ...state, roleOverrides: [] }))).not.toHaveProperty('roleOverrides');
    expect(JSON.parse(api().buildCutLabStateJson({ ...state, roleOverrides: overrides })).roleOverrides).toEqual(overrides);
  });
  it('preserves overrides in every state input after a lock toggle', () => {
    buildFixture(); editor();
    document.querySelectorAll<HTMLInputElement>('[name="CutLabStateJson"]').forEach(input => { input.value = JSON.stringify({ ...JSON.parse(buildStateJson(98)), roleOverrides: overrides }); });
    const lock = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card]')!;
    lock.disabled = false; lock.checked = false; lock.dispatchEvent(new Event('change', { bubbles: true }));
    document.querySelectorAll<HTMLInputElement>('[name="CutLabStateJson"]').forEach(input => expect(JSON.parse(input.value).roleOverrides).toEqual(overrides));
  });
  it('sets CardName, renders roles as text, and shows Reset only for edited cards', () => {
    buildFixture(); editor();
    openRoleCard('Relentless Rats');
    expect(document.querySelector<HTMLInputElement>('[data-cut-lab-role-override-card]')!.value).toBe('Relentless Rats');
    const current = document.querySelector('[data-cut-lab-role-current]')!;
    expect(current.textContent).toBe('Roles: <b>Card draw</b>');
    expect(current.querySelector('b')).toBeNull();
    expect(document.querySelector<HTMLElement>('[data-cut-lab-role-reset]')!.hidden).toBe(false);
    openRoleCard('Plains');
    expect(current.textContent).toBe('Roles: Lands');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-role-reset]')!.hidden).toBe(true);
  });
  it.each(['Commander', 'Unknown', 'view-only'])('hides the role block for %s after an editable open', name => {
    buildFixture(); editor();
    openRoleCard('Plains');
    const block = document.querySelector<HTMLElement>('[data-cut-lab-role-block]')!;
    expect(block.hidden).toBe(false);
    openRoleCard(name === 'view-only' ? 'Plains' : name, name === 'view-only');
    expect(block.hidden).toBe(true);
  });
  it('carries nonland labels through an adjust patch', async () => {
    setupTuners(true); await adjust(false);
    expect.soft(roleText('Plains')).toBe('Lands · Card draw');
    expect.soft(roleText('Relentless Rats')).toBe('Card draw · Payoffs');
    expect.soft(roleText('Persistent Petitioners')).toBe('Engines');
  });
  it('keeps a new basic server label and existing nonland labels', async () => {
    setupTuners(false); await adjust(true);
    expect.soft(roleText('Plains')).toBe('Lands');
    expect.soft(roleText('Relentless Rats')).toBe('Card draw · Payoffs');
  });
});


describe('JSON role edits', () => {
  const setup = (stateWriters = false): HTMLFormElement => {
    buildFixture({ stateWriters }); editor(); openRoleCard('Relentless Rats');
    document.querySelector('form[data-cache-key="cut-lab"]')!.insertAdjacentHTML('beforeend', '<input name="__RequestVerificationToken" value="token-123" />');
    document.querySelector('dialog')!.insertAdjacentHTML('beforeend', '<button data-cutlab-modal-lock>Lock</button><button data-cutlab-modal-close>Close</button>');
    const block = document.querySelector('[data-cut-lab-role-block]')!;
    block.insertAdjacentHTML('afterbegin', '<p data-cut-lab-role-status role="status" tabindex="-1"></p>');
    const form = block.querySelector('form')!;
    form.action = '/cut-lab/role-override';
    form.insertAdjacentHTML('beforeend', '<button type="submit" name="Operation" value="Add">Add</button>');
    return form;
  };
  const submit = (form: HTMLFormElement): void => {
    form.dispatchEvent(new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: form.querySelector('[value="Add"]') }));
  };
  it('posts an Add edit as JSON to the role-override endpoint', async () => {
    const form = setup();
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ unchanged: true }) });
    submit(form); await flushAdjustSubmit();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/cut-lab/role-override');
    expect(JSON.parse(init.body)).toMatchObject({ cardName: 'Relentless Rats', role: 'ramp', operation: 'Add' });
    expect(JSON.parse(init.body).cutLabStateJson).toBe(document.querySelector<HTMLInputElement>('#cutlab-form input[name="CutLabStateJson"]')?.value ?? document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')!.value);
    expect(init.headers.RequestVerificationToken).toBe('token-123');
    expect(document.querySelector('[data-cut-lab-role-status]')!.textContent).toBe('No change to roles.');
  });
  it('shows the server message and restores controls on a 400', async () => {
    const form = setup();
    fetchMock.mockResolvedValue({ ok: false, json: async () => ({ message: 'Refused edit' }) });
    submit(form); await flushAdjustSubmit();
    expect(document.querySelector('[data-cut-lab-role-status]')!.textContent).toBe('Refused edit');
    expect(form.querySelector('select')!.disabled).toBe(false);
  });
  it('blocks state writers and a second role submit while pending', async () => {
    const form = setup(true); let finish!: (value: unknown) => void; fetchMock.mockReturnValue(new Promise(resolve => { finish = resolve; }));
    submit(form); submit(form);
    const stateInput = document.querySelector<HTMLInputElement>('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]')!;
    const priorState = stateInput.value;
    const floor = document.querySelector<HTMLInputElement>('[data-cut-lab-floor]')!;
    floor.value = '7'; floor.dispatchEvent(new Event('input', { bubbles: true })); floor.dispatchEvent(new Event('change', { bubbles: true }));
    const packageSelect = document.querySelector<HTMLSelectElement>('[data-cut-lab-package-card]')!;
    packageSelect.value = 'test'; packageSelect.dispatchEvent(new Event('change', { bubbles: true }));
    expect(stateInput.value).toBe(priorState);
    expect(document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]')!.disabled).toBe(true);
    expect(document.querySelector<HTMLButtonElement>('[data-cutlab-modal-close]')!.disabled).toBe(true);
    const cancel = new Event('cancel', { cancelable: true });
    document.querySelector('dialog')!.dispatchEvent(cancel);
    expect(cancel.defaultPrevented).toBe(true);
    document.body.insertAdjacentHTML('beforeend', '<input type="checkbox" data-cut-lab-lock-card="Island"><form data-goals-test></form><form data-export-test></form>');
    const lock = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card="Island"]')!;
    const checked = lock.checked; lock.click(); expect(lock.checked).toBe(checked);
    for (const selector of ['[data-goals-test]', '[data-export-test]']) {
      const event = new Event('submit', { bubbles: true, cancelable: true });
      document.querySelector(selector)!.dispatchEvent(event); expect(event.defaultPrevented).toBe(true);
    }
    const event = new Event('submit', { bubbles: true, cancelable: true });
    document.querySelector('form')!.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    finish({ ok: true, json: async () => ({ unchanged: true }) });
    await flushAdjustSubmit();
  });
  it.each([
    { before: 'ramp', after: ['draw'], label: 'Card draw' },
    { before: 'ramp', after: ['other'], label: 'Other' },
    { before: 'interaction-targeted', after: ['interaction-mass'], label: 'Mass interaction' },
  ])('patches roles, quantities, cache and groups from $before to $after', async ({ before, after, label }) => {
    const form = setup();
    document.body.insertAdjacentHTML('beforeend', `<table><tbody><tr data-cut-lab-card="Relentless Rats" data-cut-lab-quantity="2" data-cut-lab-role="${before}"><td data-label="Type / role"><span class="cutlab-role-list">Old</span></td></tr></tbody></table>
      <div data-type-chip><span data-cut-lab-chip-card="Relentless Rats">Type</span></div><div data-finding-chip><span data-cut-lab-chip-card="Relentless Rats">Finding</span></div>
      ${['ramp', 'draw', 'interaction-targeted', 'interaction-mass', 'other'].map(key => `<details data-cut-lab-group-role="${key}"><span data-cut-lab-group-count="${key}"></span><span data-cut-lab-group-locked="${key}"></span><div class="kb-chip-area__chips">${key === before ? '<span data-cut-lab-chip-card="Relentless Rats">Rats</span>' : ''}</div><p class="kb-chip-area__empty-hint"></p></details>`).join('')}`);
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ cardName: 'Relentless Rats', roleKeys: after, roleLabel: label, roleEdited: true, patch: buildPatch(buildStateJson(98), { nextProposal: null, quantityTuners: [] }) }) });
    submit(form); await flushAdjustSubmit();
    expect(document.querySelector('[data-cut-lab-role-status]')!.textContent).toBe('Roles updated.');
    const row = document.querySelector<HTMLElement>('tr[data-cut-lab-card="Relentless Rats"]')!;
    expect(row.dataset.cutLabRole).toBe(after.join(' '));
    expect(row.querySelector('.cutlab-role-list')!.textContent).toBe(label);
    expect(row.querySelector('.cutlab-role-edited-chip')!.textContent).toBe('Edited');
    const target = document.querySelector(`[data-cut-lab-group-role="${after[0]}"]`)!;
    expect(target.querySelector('[data-cut-lab-group-count]')!.textContent).toBe(after[0] === 'draw' ? '3' : '2');
    expect(target.querySelectorAll('[data-cut-lab-chip-card]')).toHaveLength(1);
    expect(target.querySelector('[data-cut-lab-group-locked]')!.textContent).toBe(after[0] === 'draw' ? '1' : '0');
    expect(target.querySelector<HTMLElement>('.kb-chip-area__empty-hint')!.hidden).toBe(true);
    expect(document.querySelector(`[data-cut-lab-group-role="${before}"] [data-cut-lab-chip-card]`)).toBeNull();
    expect(document.querySelector('[data-type-chip]')!.textContent).toBe('Type');
    expect(document.querySelector('[data-finding-chip]')!.textContent).toBe('Finding');
    expect(document.querySelector('dialog')!.hasAttribute('open')).toBe(true);
    expect(document.querySelector('[data-cut-lab-role-current]')!.textContent).toContain(label);
  });
  it('does not start a role edit while an adjust mutation is pending', async () => {
    const form = setup();
    let finish!: (value: unknown) => void;
    fetchMock.mockReturnValue(new Promise(resolve => { finish = resolve; }));
    document.querySelector<HTMLButtonElement>('button[data-cut-lab-adjust]')!.click();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    submit(form);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    finish({ ok: true, json: async () => ({ patch: buildPatch(buildStateJson(98)) }) });
    await flushAdjustSubmit();
  });
  it('re-posts the edit through a native form on a formFallback 400', async () => {
    const form = setup();
    const native = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {});
    fetchMock.mockResolvedValue({ ok: false, json: async () => ({ formFallback: true }) });
    submit(form); await flushAdjustSubmit();
    expect(native).toHaveBeenCalledTimes(1);
    const posted = native.mock.instances[0] as HTMLFormElement;
    expect(posted.action).toBe(form.action);
    expect(new FormData(posted).get('Operation')).toBe('Add');
    expect(new FormData(posted).get('CardName')).toBe('Relentless Rats');
    expect(form.querySelector('select')!.disabled).toBe(true);
    native.mockRestore();
  });

});
