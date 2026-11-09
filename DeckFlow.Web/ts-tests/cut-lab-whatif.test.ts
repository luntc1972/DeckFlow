import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

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
  cutsMade: Array<{ cardName: string; roundKey: string; roundLabel: string; ordinal: number }>;
  structuralFindings: Array<unknown>;
  comboDataAvailable: boolean;
  categoryDataAvailable: boolean;
  whatifCardOutOptions: string[];
  whatifCardInOptions: string[];
  quantityTuners: Array<unknown>;
  addableBasics: string[];
}

let fetchMock: ReturnType<typeof vi.fn>;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  document.body.innerHTML = '';
  fetchMock.mockReset();
  vi.restoreAllMocks();
});

const flushWhatifSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const buildStateJson = (overrides: Partial<{
  pool: Array<{ name: string; quantity: number; typeLine: string; isCommander: boolean; isLocked: boolean; packageId: string | null }>;
  decisions: Array<{ cardName: string; kind: 'Accepted' | 'Rejected' | 'Deferred' | 0 | 1 | 2; round: string; ordinal: number }>;
}> = {}): string => JSON.stringify({
  commander: 'Commander',
  pool: overrides.pool ?? [
    { name: 'Commander', quantity: 1, typeLine: 'Legendary Creature', isCommander: true, isLocked: true, packageId: null },
    { name: 'Working Card', quantity: 1, typeLine: 'Artifact', isCommander: false, isLocked: false, packageId: null },
    { name: 'Cut Card', quantity: 1, typeLine: 'Instant', isCommander: false, isLocked: false, packageId: null },
  ],
  packages: [],
  decisions: overrides.decisions ?? [
    { cardName: 'Cut Card', kind: 'Accepted', round: 'round-1', ordinal: 1 },
  ],
  baselineSnapshot: {
    metrics: [],
  },
  roleFloors: [],
  goals: {
    commanderByTurn: 3,
    engineByTurn: 2,
    representativeLineByTurn: 4,
  },
  intent: {
    primaryPlan: 'Stay lean.',
    secondaryPlan: null,
    bracket: 3,
    playExperience: 'Focused',
    includeSideboard: false,
    includeMaybeboard: false,
  },
});

const buildFixture = (setup?: () => void): void => {
  const stateJson = buildStateJson();

  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <input type="checkbox" name="IncludeSideboard" value="true" />
      <input type="checkbox" name="IncludeMaybeboard" value="true" />
      <table>
        <tbody>
          <tr data-cut-lab-card="Commander" data-cut-lab-type-line="Legendary Creature" data-cut-lab-role="draw" data-cut-lab-quantity="1" data-cut-lab-commander="true">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Commander" checked disabled /></td>
            <td data-label="Card"><strong>1 × Commander</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Commander"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
          </tr>
          <tr data-cut-lab-card="Working Card" data-cut-lab-type-line="Artifact" data-cut-lab-role="ramp" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Working Card" /></td>
            <td data-label="Card"><strong>1 × Working Card</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Working Card"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
          </tr>
          <tr data-cut-lab-card="Cut Card" data-cut-lab-type-line="Instant" data-cut-lab-role="interaction-targeted" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Cut Card" /></td>
            <td data-label="Card"><strong>1 × Cut Card</strong></td>
            <td data-label="Package assignment"><select data-cut-lab-package-card="Cut Card"><option value="">Unlocked pool</option><option value="__new__">+ New package…</option></select></td>
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
    <form method="post" action="/cut-lab/whatif" data-cut-lab-whatif-form>
      <input type="hidden" name="__RequestVerificationToken" value="token-123" />
      <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
      <select name="cardOut" data-cut-lab-whatif-card-out>
        <option value="">Choose a card</option>
        <option value="Working Card" selected>Working Card</option>
      </select>
      <select name="cardIn" data-cut-lab-whatif-card-in>
        <option value="">Choose a card</option>
        <option value="Cut Card" selected>Cut Card</option>
      </select>
      <div class="cutlab-whatif__preview hidden" data-cut-lab-whatif-preview>
        <p class="cutlab-whatif__selection hidden" data-cut-lab-whatif-selection></p>
        <table><tbody data-cut-lab-whatif-delta-body></tbody></table>
      </div>
      <button type="submit" name="intent" value="preview" data-cut-lab-whatif-preview-submit>Preview swap</button>
      <button type="submit" name="intent" value="keep" class="hidden" data-cut-lab-whatif-keep-submit>Keep swap</button>
      <button type="button" class="hidden" data-cut-lab-whatif-discard>Discard preview</button>
    </form>
    <section class="result-panel">
      <div class="cutlab-sticky-bar">
        <span class="cutlab-sticky-bar__round" data-cut-lab-sticky-round>Round 1</span>
        <span class="cutlab-sticky-bar__count" data-cut-lab-sticky-remaining>1 to cut</span>
        <span class="cutlab-sticky-bar__accepted" data-cut-lab-sticky-accepted>1 cut so far</span>
      </div>
    </section>
    <section class="result-panel" data-cut-lab-cuts-made-section="true">
      <details class="cutlab-cuts-made" open>
        <summary>Cuts made · 1 card</summary>
      </details>
    </section>
    <section class="result-panel">
      <div class="cutlab-round-banner">
        <p class="cutlab-finding__heading">Round 1</p>
        <p>Cards flagged by structural findings.</p>
      </div>
      <div class="cutlab-proposal" data-cut-lab-card="Working Card" data-cut-lab-round="round-1">
        <p class="cutlab-proposal__heading">Proposed cut: Working Card</p>
        <div class="cutlab-proposal__actions">
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value='${stateJson}' />
            <input type="hidden" name="CardName" value="Working Card" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="accept" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--accept" data-cut-lab-decision="accept" data-cut-lab-card="Working Card">Accept cut</button>
          </form>
        </div>
      </div>
    </section>
  `;

  setup?.();
  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const buildPatch = (stateJson: string, overrides: Partial<CutLabUiPatch> = {}): CutLabUiPatch => ({
  cutLabStateJson: stateJson,
  currentCount: 100,
  cardsRemaining: 0,
  canBuildExport: true,
  nextProposal: {
    isTerminal: false,
    isAtTarget: false,
    isNothingToCut: false,
    cardName: 'Alternate Working',
    roundKey: 'round-2',
    roundLabel: 'Round 2',
    roundBannerBody: 'More cuts.',
    findingCount: 0,
    findingChips: [],
  },
  proposalDeltas: null,
  floorWarnings: [],
  cutsMade: [
    { cardName: 'Working Card', roundKey: 'whatif-swap', roundLabel: 'What-if swap', ordinal: 2 },
  ],
  structuralFindings: [],
  comboDataAvailable: true,
  categoryDataAvailable: true,
  whatifCardOutOptions: ['Cut Card'],
  whatifCardInOptions: ['Working Card'],
  quantityTuners: [],
  addableBasics: [],
  ...overrides,
});

describe('cut-lab what-if enhancement', () => {
  it('preview leaves committed state untouched', async () => {
    buildFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        cardOut: 'Working Card',
        cardIn: 'Cut Card',
        deltas: [
          {
            label: 'Commander by turn 3',
            before: 57,
            after: 61,
            delta: 4,
            direction: 'Up',
            isMeaningful: true,
            kind: 'CommanderByTurn',
            unit: 'Percent',
          },
        ],
        changedFamilyCount: 1,
        cutLabStateJson: null,
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cut-lab-whatif-form]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-preview-submit]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushWhatifSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/whatif');
    expect(fetchMock.mock.calls[0][1]).toEqual(expect.objectContaining({
      method: 'POST',
    }));
    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({
      cutLabStateJson: expect.any(String),
      cardOut: 'Working Card',
      cardIn: 'Cut Card',
    });
    expect(document.querySelector('[data-cut-lab-whatif-selection]')?.textContent).toContain('Working Card');
    expect(document.querySelectorAll('[data-cut-lab-whatif-delta-body] tr')).toHaveLength(1);
  });

  it('keep renders the returned patch on successful commit', async () => {
    buildFixture(addNavigationFixture);
    const committedStateJson = JSON.stringify({
      commander: 'Commander',
      pool: [],
      packages: [],
      decisions: [
        { cardName: 'Working Card', kind: 'Accepted', round: 'whatif-swap', ordinal: 2 },
      ],
      baselineSnapshot: { metrics: [] },
      roleFloors: [],
      goals: {
        commanderByTurn: 3,
        engineByTurn: 2,
        representativeLineByTurn: 4,
      },
      intent: {
        primaryPlan: 'Stay lean.',
        secondaryPlan: null,
        bracket: 3,
        playExperience: 'Focused',
        includeSideboard: false,
        includeMaybeboard: false,
      },
    });
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        cardOut: 'Working Card',
        cardIn: 'Cut Card',
        deltas: [],
        changedFamilyCount: 0,
        cutLabStateJson: committedStateJson,
        patch: buildPatch(committedStateJson),
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[data-cut-lab-whatif-form]');
    const button = document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-keep-submit]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushWhatifSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/whatif/commit');
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).every(input => input.value === committedStateJson)).toBe(true);
    expect(document.querySelector('[data-cut-lab-sticky-accepted]')?.textContent).toBe('1 cut so far');
    expect(document.querySelector('.cutlab-cuts-made__row')?.textContent).toContain('Working Card');
    const swap = document.querySelector<HTMLButtonElement>('.cutlab-cuts-made__row [data-cut-lab-swap-card]')!;
    expect(swap.dataset.cutLabSwapCard).toBe('Working Card');
    swap.click();
    expect(document.querySelector<HTMLSelectElement>('[data-cut-lab-whatif-card-in]')!.value).toBe('Working Card');
    expect(document.querySelector('#cut-lab-step-panel-4')!.hasAttribute('hidden')).toBe(false);

  });

  it('SwapJump_RebuiltMdfcRow_PreselectsCanonicalNameAndFocusesCardOutCombobox', async () => {
    buildFixture(addNavigationFixture);
    const name = 'Witch Enchanter // Witch-Blessed Meadow';
    const stateJson = buildStateJson();
    expect(document.querySelector('.cutlab-cuts-made__row [data-cut-lab-swap-card]')).toBeNull();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        cardOut: 'Working Card',
        cardIn: 'Cut Card',
        deltas: [],
        changedFamilyCount: 0,
        cutLabStateJson: stateJson,
        patch: buildPatch(stateJson, {
          cutsMade: [{ cardName: name, roundKey: 'whatif-swap', roundLabel: 'What-if swap', ordinal: 2 }],
          whatifCardInOptions: [name],
        }),
      }),
    });
    const form = document.querySelector<HTMLFormElement>('[data-cut-lab-whatif-form]')!;
    const keep = form.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-keep-submit]')!;
    form.dispatchEvent(new SubmitEvent('submit', { bubbles: true, cancelable: true, submitter: keep }));
    await flushWhatifSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0][0]).toBe('/api/cut-lab/whatif/commit');
    const swap = document.querySelector<HTMLButtonElement>('.cutlab-cuts-made__row [data-cut-lab-swap-card]')!;
    expect(swap.dataset.cutLabSwapCard).toBe(name);
    swap.click();
    expect(form.querySelector<HTMLSelectElement>('[data-cut-lab-whatif-card-in]')!.value).toBe(name);
    expect(document.querySelector('#cut-lab-step-panel-4')!.hasAttribute('hidden')).toBe(false);
    expect(document.activeElement).toBe(form.querySelector('[role="combobox"]'));
  });

  it('rebuilds what-if select options from the returned patch after an accepted cut', async () => {
    buildFixture();
    const refreshedStateJson = buildStateJson({
      pool: [
        { name: 'Commander', quantity: 1, typeLine: 'Legendary Creature', isCommander: true, isLocked: true, packageId: null },
        { name: 'Working Card', quantity: 1, typeLine: 'Artifact', isCommander: false, isLocked: false, packageId: null },
        { name: 'Alternate Working', quantity: 1, typeLine: 'Sorcery', isCommander: false, isLocked: false, packageId: null },
        { name: 'Locked Working', quantity: 1, typeLine: 'Artifact', isCommander: false, isLocked: true, packageId: null },
        { name: 'Cut Card', quantity: 1, typeLine: 'Instant', isCommander: false, isLocked: false, packageId: null },
      ],
      decisions: [
        { cardName: 'Cut Card', kind: 0, round: 'round-1', ordinal: 1 },
        { cardName: 'Working Card', kind: 0, round: 'round-2', ordinal: 2 },
      ],
    });
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        patch: buildPatch(refreshedStateJson, {
          whatifCardOutOptions: ['Alternate Working'],
          whatifCardInOptions: ['Cut Card', 'Working Card'],
          cutsMade: [
            { cardName: 'Working Card', roundKey: 'round-2', roundLabel: 'Round 2', ordinal: 2 },
            { cardName: 'Cut Card', roundKey: 'round-1', roundLabel: 'Round 1', ordinal: 1 },
          ],
        }),
      }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushWhatifSubmit();

    const cardOutOptions = Array.from(document.querySelectorAll<HTMLOptionElement>('select[data-cut-lab-whatif-card-out] option')).map(option => option.value);
    const cardInOptions = Array.from(document.querySelectorAll<HTMLOptionElement>('select[data-cut-lab-whatif-card-in] option')).map(option => option.value);

    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).every(input => input.value === refreshedStateJson)).toBe(true);
    expect(cardOutOptions).toEqual(['', 'Alternate Working']);
    expect(cardInOptions).toEqual(['', 'Cut Card', 'Working Card']);
  });
});


const addNavigationFixture = (): void => {
  document.body.insertAdjacentHTML('beforeend', `
    <button type="button" role="tab" data-cut-lab-step="2" id="cut-lab-step-tab-2">Plan</button>
    <button type="button" role="tab" data-cut-lab-step="4" id="cut-lab-step-tab-4">Goals</button>
    <section role="tabpanel" id="cut-lab-step-panel-2" hidden>
      <details id="cut-lab-section-lock-pool" data-cutlab-mobile-collapse>
        <summary>Locks</summary><details id="cut-lab-section-packages" data-cutlab-mobile-collapse><summary>Packages</summary></details>
      </details>
    </section>
    <section role="tabpanel" id="cut-lab-step-panel-4" hidden>
      <details id="cut-lab-section-whatif" data-cutlab-mobile-collapse><summary>What-if</summary></details>
    </section>
    <a href="#cut-lab-section-packages" data-cut-lab-tab-link="cut-lab-section-packages">Manage packages</a>
    <p data-cut-lab-lock-callout>No cards locked yet.</p>
    <button type="button" data-cut-lab-swap-card="Cut Card">What if I swap this?</button>`);
  const form = document.querySelector<HTMLFormElement>('[data-cut-lab-whatif-form]')!;
  document.querySelector('#cut-lab-section-whatif')!.append(form);
  const out = form.querySelector<HTMLSelectElement>('[data-cut-lab-whatif-card-out]')!;
  const wrapper = document.createElement('div');
  wrapper.className = 'df-select';
  out.before(wrapper);
  wrapper.append(out);
  wrapper.insertAdjacentHTML('beforeend', '<button type="button" class="df-select__trigger" role="combobox">Choose card</button>');
};

describe('Cut Lab navigation shortcuts', () => {
  it('TabLink_NestedTarget_ActivatesTabAndOpensAncestors', () => {
    buildFixture(addNavigationFixture);
    document.querySelector<HTMLAnchorElement>('[data-cut-lab-tab-link]')!.click();
    expect(document.querySelector('#cut-lab-step-panel-2')!.hasAttribute('hidden')).toBe(false);
    expect(document.querySelector<HTMLDetailsElement>('#cut-lab-section-lock-pool')!.open).toBe(true);
    expect(document.querySelector<HTMLDetailsElement>('#cut-lab-section-packages')!.open).toBe(true);
    expect(document.querySelector('dialog[open]')).toBeNull();
  });

  it('LockCallout_CommanderOnlyAndLockUnlock_TogglesNonCommanderVisibility', () => {
    buildFixture(addNavigationFixture);
    const callout = document.querySelector<HTMLElement>('[data-cut-lab-lock-callout]')!;
    const lock = document.querySelector<HTMLInputElement>('[data-cut-lab-lock-card="Working Card"]')!;
    expect(callout.hidden).toBe(false);
    lock.click();
    expect(callout.hidden).toBe(true);
    lock.click();
    expect(callout.hidden).toBe(false);
  });

  it('SwapJump_ExistingPreview_ClearsActionsPreselectsRefreshesAndFocusesCombobox', () => {
    const refresh = vi.fn();
    window.DeckFlow = { ...window.DeckFlow, refreshDfSelect: refresh };
    buildFixture(addNavigationFixture);
    for (const selector of ['[data-cut-lab-whatif-preview]', '[data-cut-lab-whatif-keep-submit]', '[data-cut-lab-whatif-discard]']) {
      document.querySelector(selector)!.classList.remove('hidden');
    }
    document.querySelector<HTMLButtonElement>('[data-cut-lab-swap-card]')!.click();
    const input = document.querySelector<HTMLSelectElement>('[data-cut-lab-whatif-card-in]')!;
    expect(input.value).toBe('Cut Card');
    expect(refresh).toHaveBeenCalledWith(input);
    expect(document.activeElement).toBe(document.querySelector('.df-select__trigger'));
    expect(document.querySelector<HTMLDetailsElement>('#cut-lab-section-whatif')!.open).toBe(true);
    for (const selector of ['[data-cut-lab-whatif-preview]', '[data-cut-lab-whatif-keep-submit]', '[data-cut-lab-whatif-discard]']) {
      expect(document.querySelector(selector)!.classList.contains('hidden')).toBe(true);
    }
  });

  it('SwapJump_PendingPreview_DropsLateResponse', async () => {
    buildFixture(addNavigationFixture);
    let resolve!: (response: unknown) => void;
    fetchMock.mockReturnValue(new Promise(done => { resolve = done; }));
    const form = document.querySelector<HTMLFormElement>('[data-cut-lab-whatif-form]')!;
    form.dispatchEvent(new SubmitEvent('submit', { bubbles: true, cancelable: true,
      submitter: document.querySelector<HTMLButtonElement>('[data-cut-lab-whatif-preview-submit]')! }));
    document.querySelector<HTMLButtonElement>('[data-cut-lab-swap-card]')!.click();
    resolve({ ok: true, json: async () => ({ cardOut: 'Working Card', cardIn: 'Cut Card', deltas: [], changedFamilyCount: 0 }) });
    await flushWhatifSubmit();
    expect(document.querySelector('[data-cut-lab-whatif-selection]')!.textContent).toBe('');
    expect(document.querySelector('[data-cut-lab-whatif-keep-submit]')!.classList.contains('hidden')).toBe(true);
  });
});
