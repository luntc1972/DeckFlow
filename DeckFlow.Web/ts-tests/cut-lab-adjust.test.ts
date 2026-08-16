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
  };
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
      <textarea name="PrimaryPlan">Hit exactly 100.</textarea>
      <textarea name="SecondaryPlan"></textarea>
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

describe('cut-lab adjust enhancement', () => {
  it('patches sticky count and exact-100 export gates after a successful stepper adjust', async () => {
    buildFixture();
    const originalState = buildStateJson(98);
    const nextStateJson = buildStateJson(98, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          currentCount: 100,
          canBuildExport: true,
          quantityTuners: [
            {
              cardName: 'Island',
              currentQuantity: 99,
              legalMax: 99,
              removeDisabled: false,
              addDisabled: true,
              isLockedOrCommander: false,
              isVisible: true,
              roleLabel: 'Lands',
              isLegalMultiple: true,
              isAddedBasic: false,
            },
          ],
        }),
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-delta="1"]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/adjust');
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      cutLabStateJson: originalState,
      cardName: 'Island',
      delta: 1,
      isAddedBasic: false,
    });
    expect(document.querySelector('[data-cut-lab-sticky-remaining]')?.textContent).toBe('0 to cut');
    expect(document.getElementById('cut-lab-step-tab-5')?.getAttribute('aria-disabled')).toBe('false');
    expect((document.getElementById('cut-lab-step-tab-5') as HTMLButtonElement).disabled).toBe(false);
    expect(document.querySelector('#cut-lab-export-form button[type="submit"]')?.hasAttribute('disabled')).toBe(false);
    expect(document.querySelector('[data-cut-lab-quantity-value]')?.textContent).toBe('99');
    const rowButtons = document.querySelectorAll<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-adjust]');
    expect(rowButtons[0]?.disabled).toBe(false);
    expect(rowButtons[1]?.disabled).toBe(true);
  });

  it('preserves the existing proposal and findings panels when adjust returns a light patch', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(98, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          nextProposal: null as any,
          proposalDeltas: null,
          floorWarnings: [],
          cutsMade: [{ cardName: 'Old Cut', roundKey: 'round-1', roundLabel: 'Round 1', ordinal: 1 }],
          structuralFindings: [],
          comboDataAvailable: false,
          categoryDataAvailable: false,
          quantityTuners: [
            {
              cardName: 'Island',
              currentQuantity: 99,
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
        }),
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-delta="1"]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Island');
    expect(document.querySelector('[data-cut-lab-structural-findings-body]')?.textContent).toContain('Existing structural issue.');
    expect(document.querySelector('.cutlab-cuts-made__row [data-cutlab-card-open]')?.textContent).toBe('Old Cut');
    expect(document.querySelector('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-quantity-value]')?.textContent).toBe('99');
    expect(document.querySelector('[data-cut-lab-sticky-remaining]')?.textContent).toBe('0 to cut');
    expect(document.querySelector('[data-cut-lab-sticky-round]')?.textContent).toBe('Round 1');
    expect(document.querySelector('[data-cut-lab-sticky-accepted]')?.textContent).toBe('1 cut so far');
    expect(document.querySelector('[data-cut-lab-sticky-locked]')?.textContent).toBe('1 locked');
    expect(document.querySelector('[data-cut-lab-sticky-current]')?.textContent).toBe('99/100 cards');
  });

  it('renders the terminal at-target proposal when adjust preserves proposals', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(99, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          currentCount: 100,
          canBuildExport: true,
          nextProposal: {
            isTerminal: true,
            isAtTarget: true,
            isNothingToCut: false,
            cardName: '',
            roundKey: '',
            roundLabel: '',
            roundBannerBody: '',
            findingCount: 0,
            findingChips: [],
          },
          quantityTuners: [
            {
              cardName: 'Island',
              currentQuantity: 99,
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
        }),
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-delta="1"]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('.cutlab-proposal__heading')?.textContent).toBe("You're at 100 cards");
    expect(document.querySelector('.cutlab-round-banner')).toBeNull();
    expect(document.querySelector('[data-cut-lab-sticky-remaining]')?.textContent).toBe('0 to cut');
    expect(document.querySelector('[data-cut-lab-sticky-round]')?.textContent).toBe('Round 1');
  });

  it('preserves the existing proposal panel when adjust returns a non-terminal proposal', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(98, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          nextProposal: {
            ...buildPatch(nextStateJson).nextProposal,
            cardName: 'Plains',
            roundLabel: 'Round 2',
            roundBannerBody: 'Different round body.',
          },
          proposalDeltas: null,
          floorWarnings: [],
          cutsMade: [{ cardName: 'Old Cut', roundKey: 'round-1', roundLabel: 'Round 1', ordinal: 1 }],
          structuralFindings: [],
          comboDataAvailable: false,
          categoryDataAvailable: false,
        }),
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-delta="1"]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Island');
    expect(document.querySelector('[data-cut-lab-sticky-round]')?.textContent).toBe('Round 1');
  });

  it('renders the next decision round banner body', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(98, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          nextProposal: {
            ...buildPatch(nextStateJson).nextProposal,
            cardName: 'Plains',
            roundLabel: 'Round 2',
            roundBannerBody: 'Different round body.',
          },
          proposalDeltas: null,
          floorWarnings: [],
          cutsMade: [{ cardName: 'Old Cut', roundKey: 'round-1', roundLabel: 'Round 1', ordinal: 1 }],
          structuralFindings: [],
          comboDataAvailable: false,
          categoryDataAvailable: false,
        }),
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-decision-submit]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('.cutlab-round-banner')?.textContent).toBe('Different round body.');
    expect(document.querySelector('.cutlab-round-banner > p')?.textContent).toBe('Different round body.');
  });

  it('renders locked stepper buttons as disabled with lock guidance after a patch update', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(98);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          quantityTuners: [
            {
              cardName: 'Island',
              currentQuantity: 98,
              legalMax: 150,
              removeDisabled: true,
              addDisabled: true,
              isLockedOrCommander: true,
              isVisible: true,
              roleLabel: 'Lands',
              isLegalMultiple: true,
              isAddedBasic: false,
            },
          ],
        }),
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-delta="1"]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    const rowButtons = document.querySelectorAll<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-adjust]');
    expect(rowButtons).toHaveLength(2);
    expect(rowButtons[0]?.disabled).toBe(true);
    expect(rowButtons[1]?.disabled).toBe(true);
    expect(rowButtons[0]?.getAttribute('title')).toBe('Island is locked - unlock it to adjust quantity');
    expect(rowButtons[1]?.getAttribute('aria-label')).toBe('Island is locked - unlock it to adjust quantity');
  });

  it('surfaces the server error and preserves hidden state on a failed adjust', async () => {
    buildFixture();
    const originalState = stateJsonFromInputs();
    fetchMock.mockResolvedValue({
      ok: false,
      json: async () => ({
        message: "Couldn't recalculate this cut — nothing changed. Try again.",
      }),
    });

    const button = document.querySelector<HTMLButtonElement>('tr[data-cut-lab-tuner-row="Island"] [data-cut-lab-delta="1"]');
    const form = button?.closest('form');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('.error-banner')?.textContent).toContain("Couldn't recalculate this cut");
    expect(stateJsonFromInputs()).toBe(originalState);
  });

  it('inserts a newly added basic row and rebuilds the add-basic dropdown from patch data', async () => {
    buildFixture();
    const nextStateJson = buildStateJson(99, 1);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          currentCount: 100,
          canBuildExport: true,
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
            {
              cardName: 'Plains',
              currentQuantity: 1,
              legalMax: 150,
              removeDisabled: false,
              addDisabled: false,
              isLockedOrCommander: false,
              isVisible: true,
              roleLabel: 'Lands',
              isLegalMultiple: true,
              isAddedBasic: true,
            },
          ],
          addableBasics: ['Swamp'],
        }),
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form.cutlab-tuner__add-basic');
    const select = document.querySelector<HTMLSelectElement>('[data-cut-lab-add-basic-select]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-add-basic]');
    if (select) {
      select.value = 'Plains';
    }

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    const rowNames = Array.from(document.querySelectorAll<HTMLTableRowElement>('tr[data-cut-lab-tuner-row] strong'))
      .map(element => element.textContent);
    expect(rowNames).toEqual(['Island', 'Plains']);
    expect(document.querySelector('tr[data-cut-lab-tuner-row="Plains"] .cutlab-tuner-badge--added')?.textContent).toBe('Added');
    const dropdownOptions = Array.from(document.querySelectorAll<HTMLOptionElement>('#cut-lab-add-basic-select option'))
      .map(option => option.value);
    expect(dropdownOptions).toEqual(['', 'Swamp']);
    expect(document.querySelector<HTMLFormElement>('form.cutlab-tuner__add-basic')?.classList.contains('hidden')).toBe(false);
  });

  it('removes an absent basic row and returns it to the add-basic dropdown from patch data', async () => {
    buildFixture({ includePlainsRow: true, addableBasics: ['Swamp'] });
    const nextStateJson = buildStateJson(98);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(nextStateJson, {
          currentCount: 99,
          canBuildExport: false,
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
        }),
      }),
    });

    const form = Array.from(document.querySelectorAll<HTMLFormElement>('form[data-cut-lab-adjust-form]'))
      .find(candidate => candidate.querySelector<HTMLInputElement>('input[name="CardName"]')?.value === 'Plains'
        && candidate.querySelector<HTMLInputElement>('input[name="Delta"]')?.value === '-1');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-adjust]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushAdjustSubmit();

    expect(document.querySelector('tr[data-cut-lab-tuner-row="Plains"]')).toBeNull();
    const dropdownOptions = Array.from(document.querySelectorAll<HTMLOptionElement>('#cut-lab-add-basic-select option'))
      .map(option => option.value);
    expect(dropdownOptions).toEqual(['', 'Plains', 'Swamp']);
  });
});

function stateJsonFromInputs(): string[] | string {
  const values = Array.from(document.querySelectorAll('input[name="CutLabStateJson"]')).map((input: any) => input.value);
  return values[0] ?? '';
}
