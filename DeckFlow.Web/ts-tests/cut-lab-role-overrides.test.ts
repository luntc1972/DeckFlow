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

const buildFixture = (options: { includePlainsRow?: boolean; addableBasics?: string[] } = {}): void => {
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

  document.dispatchEvent(new Event('DOMContentLoaded'));
};


const overrides = [{ name: 'Relentless Rats', added: ['draw'] }];
const api = (): any => (window as any).DeckFlowCutLab;
const editor = (): void => {
  document.body.insertAdjacentHTML('beforeend', `<a href="#cut-lab-role-editor" data-cut-lab-role-edit-card="Plains">Edit roles</a>
    <form id="cut-lab-role-editor" data-cut-lab-role-override-form>
      <input name="CutLabStateJson" />
      <select data-cut-lab-role-override-card><option value="Relentless Rats">Rats</option><option value="Plains" data-cut-lab-role-override-land="true">Plains</option></select>
      <select data-cut-lab-role-override-role><option value="ramp">Ramp</option><option value="draw">Card draw</option></select>
    </form>`);
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
  it('disables Ramp on initialization when the first card is a land', () => {
    buildFixture();
    editor();
    const cardSelect = document.querySelector<HTMLSelectElement>('[data-cut-lab-role-override-card]')!;
    cardSelect.prepend(cardSelect.options[1]);
    cardSelect.selectedIndex = 0;

    document.dispatchEvent(new Event('DOMContentLoaded'));

    const roleSelect = document.querySelector<HTMLSelectElement>('[data-cut-lab-role-override-role]')!;
    expect(roleSelect.querySelector<HTMLOptionElement>('option[value="ramp"]')!.disabled).toBe(true);
    expect(roleSelect.value).toBe('draw');
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
  it('preselects the card when an edit link is clicked', () => {
    buildFixture(); editor();
    document.querySelector<HTMLElement>('[data-cut-lab-role-edit-card]')!.click();
    expect(document.querySelector<HTMLSelectElement>('[data-cut-lab-role-override-card]')!.value).toBe('Plains');
  });
  it('disables ramp for lands and selects an enabled role', () => {
    buildFixture(); editor();
    const card = document.querySelector<HTMLSelectElement>('[data-cut-lab-role-override-card]')!;
    card.value = 'Plains'; card.dispatchEvent(new Event('change', { bubbles: true }));
    const role = document.querySelector<HTMLSelectElement>('[data-cut-lab-role-override-role]')!;
    expect(role.options[0].disabled).toBe(true); expect(role.value).toBe('draw');
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
