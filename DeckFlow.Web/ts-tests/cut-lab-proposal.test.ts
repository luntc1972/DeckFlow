import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabDecideMetricDeltaDto {
  label: string;
  delta: number;
  direction: 'Up' | 'Down' | 'None';
  isMeaningful: boolean;
  kind: string;
  unit: 'Percent' | 'Cards';
}

interface CutLabDecideFindingDto {
  kind: string;
  heading: string;
  lead: string;
  evidence: string[];
}

interface CutLabDecideFindingGroupDto {
  kind: string;
  heading: string;
  items: CutLabDecideFindingDto[];
}

interface CutLabUiPatch {
  cutLabStateJson: string;
  currentCount: number;
  canBuildExport: boolean;
  cardTextByCardName?: Record<string, {
    typeLine?: string;
    manaCost?: string;
    setCode?: string;
    collectorNumber?: string;
    oracleText?: string;
    power?: string;
    toughness?: string;
    cmc?: number;
    castPercent?: number;
  }>;
  actualLands?: number;
  targetLands?: number;
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
  proposalDeltas: {
    cardName: string;
    changedFamilyCount: number;
    deltas: CutLabDecideMetricDeltaDto[];
  } | null;
  lockedOvershootAdvisory?: {
    cardsOverTarget: number;
    hiddenCount: number;
    groups: Array<{
      roleLabel: string;
      cardNames: string[];
    }>;
  } | null;
  floorWarnings: Array<{ message: string }>;
  cardsRemaining: number;
  cutsMade: Array<{ cardName: string; roundKey: string; roundLabel: string; ordinal: number }>;
  structuralFindings: CutLabDecideFindingGroupDto[];
  comboDataAvailable: boolean;
  categoryDataAvailable: boolean;
  whatifCardOutOptions: string[];
  whatifCardInOptions: string[];
  quantityTuners: Array<unknown>;
  addableBasics: string[];
}

interface CutLabPatchResponse {
  patch?: CutLabUiPatch | null;
}

let fetchMock: ReturnType<typeof vi.fn>;
let showModalCalls = 0;
let confirmMock: ReturnType<typeof vi.fn>;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
  confirmMock = vi.fn();
  vi.stubGlobal('confirm', confirmMock);
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
  fetchMock.mockReset();
  confirmMock.mockReset();
  showModalCalls = 0;
});

const flushDecisionSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const deferred = <T,>(): {
  promise: Promise<T>;
  resolve: (value: T) => void;
} => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(pendingResolve => {
    resolve = pendingResolve;
  });

  return { promise, resolve };
};

const buildDecisionFixture = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab">
      <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
      <textarea name="PrimaryPlan">Keep the fast mana density honest.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <table>
        <tbody>
          <tr data-cut-lab-card="Tymna the Weaver" data-cut-lab-type-line="Legendary Creature" data-cut-lab-role="draw" data-cut-lab-commander="true">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Tymna the Weaver" checked disabled /></td>
            <td data-label="Card"><strong>1 × Tymna the Weaver</strong></td>
            <td data-label="Package assignment">
              <select data-cut-lab-package-card="Tymna the Weaver">
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
    <section class="result-panel">
      <div class="panel-heading__actions">
        <form method="post" action="/cut-lab/restart-rounds" data-cut-lab-restart-rounds-form>
          <input type="hidden" name="__RequestVerificationToken" value="token-123" />
          <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
          <button type="submit" data-cut-lab-restart-rounds data-cut-lab-restart-rounds-api="/api/cut-lab/restart-rounds">Restart rounds 1 &amp; 2</button>
        </form>
      </div>
      <div class="cutlab-sticky-bar">
        <span class="cutlab-sticky-bar__locked" data-cut-lab-sticky-locked>1 locked</span>
        <span class="cutlab-sticky-bar__current" data-cut-lab-sticky-current>112/100 cards</span>
        <span class="cutlab-sticky-bar__round" data-cut-lab-sticky-round>Round 1 · Obvious cuts</span>
        <span class="cutlab-sticky-bar__count" data-cut-lab-sticky-remaining>12 to cut</span>
        <span class="cutlab-sticky-bar__accepted" data-cut-lab-sticky-accepted>0 cut so far</span>
      </div>
      <details class="cutlab-collapsible" data-cut-lab-lands-disclosure>
        <summary class="cutlab-collapsible__summary">Lands right now</summary>
        <p data-cut-lab-lands-text>Lands: 37/39 (95%) as your pool stands now (112 cards).</p>
      </details>
      <button type="button" id="cut-lab-step-tab-5" disabled aria-disabled="true">Export</button>
      <div class="cutlab-round-banner">
        <p class="cutlab-finding__heading">Round 1 · Obvious cuts</p>
        <p>Cards flagged by 2 or more structural findings from the section above.</p>
      </div>
      <div class="cutlab-proposal" data-cut-lab-card="Sol Ring" data-cut-lab-round="round-1">
        <p class="cutlab-proposal__heading">Proposed cut: Sol Ring</p>
        <div class="cutlab-proposal__evidence">
          <p>Flagged by 2 findings:</p>
          <div class="kb-chip-area__chips">
            <span class="kb-chip">Curve congestion</span>
          </div>
        </div>
        <div class="cutlab-delta">
          <p>1 of 7 metric families changed meaningfully.</p>
          <div class="cutlab-delta__line">
            <span class="cutlab-delta__sentence">cutting Sol Ring lowers keepable hand by 2.1%.</span>
            <span class="cutlab-delta__value cutlab-delta__value--down">
              <span aria-hidden="true">▼</span>
              <span class="cutlab-delta__value--down">2.1%</span>
            </span>
          </div>
        </div>
        <details data-cut-lab-delta-expander>
          <summary>Show full metric breakdown</summary>
          <div class="cutlab-delta"></div>
        </details>
        <div class="cutlab-proposal__actions">
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="accept" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--accept" data-cut-lab-decision="accept" data-cut-lab-card="Sol Ring">Accept cut</button>
          </form>
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="reject" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--reject" data-cut-lab-decision="reject" data-cut-lab-card="Sol Ring">Reject cut</button>
          </form>
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="defer" />
            <button type="submit" class="cutlab-decision-btn cutlab-decision-btn--defer" data-cut-lab-decision="defer" data-cut-lab-card="Sol Ring">Defer decision</button>
          </form>
        </div>
      </div>
    </section>
    <section class="result-panel" data-cut-lab-structural-findings>
      <div class="panel-heading">
        <div>
          <h2>Structural findings</h2>
          <p>Measured observations about your pool's shape. Nothing here says a card is bad — it says what the numbers show.</p>
        </div>
        <div class="panel-heading__actions" data-cut-lab-findings-count-slot>
          <span class="prompt-size-note cutlab-findings-count">1 structural finding</span>
        </div>
      </div>
      <div data-cut-lab-structural-findings-body>
        <div class="cutlab-finding">
          <p class="cutlab-finding__heading">Weak floor cases</p>
          <div class="cutlab-finding__item">
            <p class="cutlab-finding__lead">Ramp is at 1 against a floor of 2.</p>
            <div class="kb-chip-area__chips">
              <span class="kb-chip">Arcane Signet</span>
            </div>
          </div>
        </div>
      </div>
      <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="combo">Combo data unavailable right now.</p>
      <p class="cutlab-degradation-note" data-cut-lab-degradation="category">Community category data unavailable.</p>
    </section>
    <section class="result-panel">
      <details class="cutlab-cuts-made" open>
        <summary>Cuts made · 1 cards</summary>
        <div class="cutlab-cuts-made__row">
          <span>Mana Crypt</span>
          <span class="prompt-size-note">cut in Round 1 · Obvious cuts</span>
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Mana Crypt" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="restore" />
            <button type="submit" class="cutlab-restore-btn" data-cut-lab-restore data-cut-lab-card="Mana Crypt">Restore</button>
          </form>
        </div>
      </details>
    </section>
    <section class="result-panel" id="cut-lab-step-panel-5">
      <form id="cut-lab-export-form">
        <div class="cutlab-export">
          <div class="cutlab-export__summary">
            <div class="cutlab-export__status" data-cut-lab-export-count>
              <strong>❌ Card count = 112</strong>
              <span>Reach 100 cards to unlock the finished-list export.</span>
            </div>
          </div>
          <button type="submit" disabled>Build export</button>
        </div>
      </form>
    </section>
    <script type="application/json" id="cutlab-card-text-data">{"Counterspell":{"typeLine":"Instant","manaCost":"{U}{U}","setCode":"TMP","collectorNumber":"55","oracleText":"Counter target spell.","comboContext":"Infinite cards"},"Sol Ring":{"typeLine":"Artifact","manaCost":"{1}","oracleText":"{T}: Add {C}{C}."}}</script>
    <dialog id="cutlab-card-modal" aria-labelledby="cutlab-card-modal-title">
      <h2 id="cutlab-card-modal-title"></h2>
      <p data-cutlab-modal-meta hidden></p>
      <p data-cutlab-modal-castability hidden></p>
      <p data-cutlab-modal-oracle></p>
      <p data-cutlab-modal-combo hidden></p>
      <button type="button" data-cutlab-modal-lock></button>
      <button type="button" data-cutlab-modal-close>Close</button>
    </dialog>
  `;

  document.dispatchEvent(new Event('DOMContentLoaded'));
};

const buildPatch = (overrides: Partial<CutLabUiPatch> = {}): CutLabUiPatch => ({
  cutLabStateJson: '{"version":2}',
  currentCount: 111,
  actualLands: 38,
  targetLands: 40,
  canBuildExport: false,
  nextProposal: {
    isTerminal: false,
    isAtTarget: false,
    isNothingToCut: false,
    cardName: 'Arcane Signet',
    roundKey: 'round-2',
    roundLabel: 'Round 2 · Structural choices',
    roundBannerBody: 'Cards flagged by exactly one structural finding.',
    findingCount: 1,
    findingChips: ['Redundant finishers'],
  },
  proposalDeltas: {
    cardName: 'Arcane Signet',
    changedFamilyCount: 2,
    deltas: [
      {
        label: 'Keepable hand',
        delta: -2.1,
        direction: 'Down',
        isMeaningful: true,
        kind: 'KeepableHand',
        unit: 'Percent',
      },
      {
        label: 'Flood risk',
        delta: 0,
        direction: 'None',
        isMeaningful: false,
        kind: 'Flood',
        unit: 'Cards',
      },
    ],
  },
  floorWarnings: [{ message: 'Cutting Arcane Signet drops ramp to 9, below your floor of 10.' }],
  cardsRemaining: 11,
  cutsMade: [{ cardName: 'Sol Ring', roundKey: 'round-1', roundLabel: 'Round 1 · Obvious cuts', ordinal: 2 }],
  structuralFindings: [
    {
      kind: 'WeakFloorCase',
      heading: 'Weak floor cases',
      items: [
        {
          kind: 'WeakFloorCase',
          heading: 'Weak floor cases',
          lead: 'Interaction is at 1 against a floor of 2 — every card in this role is effectively protected already.',
          evidence: ['Counterspell'],
        },
      ],
    },
  ],
  comboBadgeByCardName: {},
  comboDataAvailable: true,
  categoryDataAvailable: true,
  whatifCardOutOptions: [],
  whatifCardInOptions: [],
  quantityTuners: [],
  addableBasics: [],
  ...overrides,
});

describe('cut-lab proposal enhancement', () => {
  it('intercepts decision-form submit, posts JSON, and syncs every hidden state field from the response', async () => {
    buildDecisionFixture();
    const response: CutLabPatchResponse = { patch: buildPatch() };
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => response,
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const submitEvent = new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    });

    const dispatchResult = form?.dispatchEvent(submitEvent);
    await flushDecisionSubmit();

    expect(dispatchResult).toBe(false);
    expect(submitEvent.defaultPrevented).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith('/api/cut-lab/decide', expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        'Content-Type': 'application/json',
      }),
      body: JSON.stringify({
        cutLabStateJson: '{"version":1}',
        cardName: 'Sol Ring',
        decision: 'accept',
      }),
    }));
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value)).toEqual([
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
      '{"version":2}',
    ]);
  });

  it('patches the sticky bar, round banner, and proposal card in place after a successful decision', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch() }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-round]')?.textContent).toBe('Round 2 · Structural choices');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-remaining]')?.textContent).toBe('11 to cut');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.textContent).toBe('1 cut so far');
    expect(document.querySelector<HTMLElement>('.cutlab-round-banner .cutlab-finding__heading')?.textContent).toBe('Round 2 · Structural choices');
    expect(document.querySelector<HTMLElement>('.cutlab-round-banner p:last-child')?.textContent).toBe('Cards flagged by exactly one structural finding.');
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Arcane Signet');
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__evidence p')?.textContent).toBe('Flagged by 1 findings:');
    expect(document.querySelectorAll('.cutlab-delta__line')).toHaveLength(3);
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__floor-warning .cutlab-finding__lead')?.textContent).toBe('Cutting Arcane Signet drops ramp to 9, below your floor of 10.');
    expect(document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made summary')?.textContent).toBe('Cuts made · 1 card');
  });

  it('renders the locked overshoot advisory alongside a non-terminal proposal', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        currentCount: 107,
        cardsRemaining: 7,
        nextProposal: {
          ...buildPatch().nextProposal,
          cardName: 'Arcane Signet',
          roundKey: 'round-2',
          roundLabel: 'Round 2 · Structural choices',
          roundBannerBody: 'Cards flagged by exactly one structural finding.',
        },
        lockedOvershootAdvisory: {
          cardsOverTarget: 5,
          hiddenCount: 0,
          groups: [
            {
              roleLabel: 'Payoffs',
              cardNames: ['Locked Stack'],
            },
          ],
        },
      }) }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-locked-overshoot-advisory]')).not.toBeNull();
    expect(document.querySelector<HTMLElement>('[data-cut-lab-locked-overshoot-advisory]')?.textContent).toContain('Locked Stack');
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Arcane Signet');
  });

  it('live-patches only the structural findings body, count slot, and degradation notes after a decision', async () => {
    buildDecisionFixture();
    document.querySelector('table tbody')?.insertAdjacentHTML('beforeend', `
      <tr data-cut-lab-card="Counterspell"
          data-cut-lab-type-line="Instant"
          data-cut-lab-role="interaction-targeted"
          data-cut-lab-quantity="1"
          data-cut-lab-commander="false">
        <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Counterspell" /></td>
        <td data-label="Card"><strong>1 × Counterspell</strong></td>
        <td data-label="Package">
          <select data-cut-lab-package-card="Counterspell"><option value="">Unlocked pool</option></select>
        </td>
      </tr>
      <tr data-cut-lab-card="kommand tower"
          data-cut-lab-type-line="Land"
          data-cut-lab-role="mana"
          data-cut-lab-quantity="1"
          data-cut-lab-commander="false">
        <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="kommand tower" /></td>
        <td data-label="Card"><strong>1 × kommand tower</strong></td>
        <td data-label="Package">
          <select data-cut-lab-package-card="kommand tower"><option value="">Unlocked pool</option></select>
        </td>
      </tr>
    `);
    fetchMock
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ patch: buildPatch({
          structuralFindings: [
            {
              kind: 'WeakFloorCase',
              heading: 'Weak floor cases',
              items: [
                {
                  kind: 'WeakFloorCase',
                  heading: 'Weak floor cases',
                  lead: 'You have no ramp cards yet; the suggested floor is 1.',
                  evidence: [],
                },
                {
                  kind: 'WeakFloorCase',
                  heading: 'Weak floor cases',
                  lead: 'Interaction is at 1 against a floor of 2 — every card in this role is effectively protected already.',
                  evidence: [
                    'Counterspell · MV 2',
                    '1 card below the floor',
                    'Kommand Tower',
                    'Counterspell · MV ',
                    'Counterspell · MV unknown',
                    'Counterspell · MV 2 extra',
                    'Counterspell · MV 2.123',
                  ],
                },
              ],
            },
          ],
          comboDataAvailable: false,
          categoryDataAvailable: true,
        }) }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ patch: buildPatch({
          structuralFindings: [],
          comboDataAvailable: true,
          categoryDataAvailable: true,
        }) }),
      });

    const acceptForm = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const acceptButton = acceptForm?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const sectionHeading = document.querySelector<HTMLElement>('[data-cut-lab-structural-findings] h2');

    let activeForm = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    let activeButton = activeForm?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    activeForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: activeButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(sectionHeading?.textContent).toBe('Structural findings');
    expect(document.querySelector<HTMLElement>('.cutlab-findings-count')?.textContent).toBe('2 structural findings');
    expect(document.querySelectorAll('[data-cut-lab-structural-findings-body] .cutlab-finding__heading')).toHaveLength(1);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-structural-findings-body] .cutlab-finding__lead')?.textContent).toBe('You have no ramp cards yet; the suggested floor is 1.');
    expect(document.querySelectorAll('[data-cut-lab-structural-findings-body] .kb-chip')).toHaveLength(7);
    const structuralCardPill = document.querySelector<HTMLButtonElement>('[data-cut-lab-structural-findings-body] button[data-cut-lab-chip-card="Counterspell"]');
    const structuralCardPills = document.querySelectorAll<HTMLButtonElement>('[data-cut-lab-structural-findings-body] button[data-cut-lab-chip-card="Counterspell"]');
    const structuralInertEvidence = Array.from(document.querySelectorAll<HTMLElement>('[data-cut-lab-structural-findings-body] span.kb-chip')).find(chip => chip.textContent === '1 card below the floor');
    const structuralUnicodeEvidence = Array.from(document.querySelectorAll<HTMLElement>('[data-cut-lab-structural-findings-body] span.kb-chip')).find(chip => chip.textContent === 'Kommand Tower');
    const structuralInvalidManaValueEvidence = Array.from(document.querySelectorAll<HTMLElement>('[data-cut-lab-structural-findings-body] span.kb-chip'))
      .filter(chip => [
        'Counterspell · MV ',
        'Counterspell · MV unknown',
        'Counterspell · MV 2 extra',
        'Counterspell · MV 2.123',
      ].includes(chip.textContent ?? ''));
    const checkbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Counterspell"]');
    const hiddenInput = document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]');
    const modalLockButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');
    expect(structuralCardPill?.textContent).toBe('Counterspell · MV 2');
    expect(structuralCardPills).toHaveLength(1);
    expect(structuralCardPill?.getAttribute('aria-pressed')).toBe('false');
    expect(structuralInertEvidence?.tagName).toBe('SPAN');
    expect(structuralUnicodeEvidence?.tagName).toBe('SPAN');
    expect(structuralInvalidManaValueEvidence).toHaveLength(4);
    structuralCardPill?.click();
    expect(showModalCalls).toBe(1);
    modalLockButton?.click();
    expect(checkbox?.checked).toBe(true);
    expect(structuralCardPill?.getAttribute('aria-pressed')).toBe('true');
    expect(structuralCardPill?.classList.contains('cutlab-role-chip--locked')).toBe(true);
    expect(JSON.parse(hiddenInput?.value ?? '').pool.find((card: { name: string }) => card.name === 'Counterspell')?.isLocked).toBe(true);
    modalLockButton?.click();
    expect(checkbox?.checked).toBe(false);
    expect(structuralCardPill?.getAttribute('aria-pressed')).toBe('false');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-degradation="combo"]')?.classList.contains('hidden')).toBe(false);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-degradation="category"]')?.classList.contains('hidden')).toBe(true);

    activeForm = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    activeButton = activeForm?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    activeForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: activeButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-findings-count-slot] .cutlab-findings-count')?.textContent).toBe('');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-findings-count-slot] .cutlab-findings-count')?.classList.contains('hidden')).toBe(true);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-structural-findings-body] p')?.textContent).toBe("No structural issues found. Your pool's curve, themes, finishers, and role coverage all look self-supporting at the current floors.");
    expect(document.querySelector<HTMLElement>('[data-cut-lab-degradation="combo"]')?.classList.contains('hidden')).toBe(true);
  });

  it('re-enables buttons and renders the neutral error line on a non-OK response without mutating state', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: false,
      json: async () => ({ message: 'Could not apply decision.' }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const originalStateValues = Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value);

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(button?.disabled).toBe(false);
    expect(document.querySelector<HTMLDivElement>('.cutlab-proposal')?.getAttribute('aria-busy')).toBeNull();
    expect(document.querySelector<HTMLElement>('[data-cut-lab-decision-error]')?.textContent).toBe('Could not apply decision.');
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value)).toEqual(originalStateValues);
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Sol Ring');
  });

  it('uses server-provided round banner copy and authoritative metric units when patching deltas', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        nextProposal: {
          ...buildPatch().nextProposal,
          roundBannerBody: 'Server banner copy wins.',
        },
        proposalDeltas: {
          cardName: 'Arcane Signet',
          changedFamilyCount: 1,
          deltas: [
            {
              label: 'Screw',
              delta: 2.5,
              direction: 'Up',
              isMeaningful: true,
              kind: 'Screw',
              unit: 'Percent',
            },
          ],
        },
      }) }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('.cutlab-round-banner p:last-child')?.textContent).toBe('Server banner copy wins.');
    expect(document.querySelector<HTMLElement>('.cutlab-delta__sentence')?.textContent).toContain('2.5%');
    expect(document.querySelector<HTMLElement>('.cutlab-delta__value span:last-child')?.textContent).toBe('2.5%');
  });

  it('posts restore decisions and removes the row while updating sticky counts on success', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        cutLabStateJson: '{"version":3}',
        cardsRemaining: 12,
        cutsMade: [],
      }) }),
    });

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const restoreForm = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]')[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(fetchMock).toHaveBeenCalledWith('/api/cut-lab/decide', expect.objectContaining({
      body: JSON.stringify({
        cutLabStateJson: '{"version":1}',
        cardName: 'Mana Crypt',
        decision: 'restore',
      }),
    }));
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-remaining]')?.textContent).toBe('12 to cut');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.textContent).toBe('0 cuts so far');
    expect(document.querySelector('details.cutlab-cuts-made')).toBeNull();
  });

  it('pluralizes sticky and cuts-made wording for many counts', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        cardsRemaining: 10,
        cutsMade: [
          { cardName: 'Sol Ring', roundKey: 'round-1', roundLabel: 'Round 1 · Obvious cuts', ordinal: 2 },
          { cardName: 'Mana Crypt', roundKey: 'round-1', roundLabel: 'Round 1 · Obvious cuts', ordinal: 1 },
        ],
      }) }),
    });

    const acceptButton = document.querySelector<HTMLButtonElement>('.cutlab-proposal [data-cut-lab-decision="accept"]');
    const acceptForm = acceptButton?.closest('form');
    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.textContent).toBe('2 cuts so far');
    expect(document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made summary')?.textContent).toBe('Cuts made · 2 cards');
  });

  it('rebuilds cuts-made rows as popup triggers and omits the lock button for cut cards', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        cardTextByCardName: {
          'Sol Ring': {
            typeLine: 'Artifact',
            manaCost: '{1}',
            oracleText: '{T}: Add {C}{C}.',
            cmc: 1,
            castPercent: 99,
          },
        },
      }) }),
    });

    const acceptButton = document.querySelector<HTMLButtonElement>('.cutlab-proposal [data-cut-lab-decision="accept"]');
    const acceptForm = acceptButton?.closest('form');
    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    const cutTrigger = document.querySelector<HTMLElement>('.cutlab-cuts-made__row [data-cutlab-card-open="Sol Ring"]');
    const castability = document.querySelector<HTMLElement>('[data-cutlab-modal-castability]');

    expect(cutTrigger).not.toBeNull();

    cutTrigger?.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(showModalCalls).toBe(1);
    expect(document.getElementById('cutlab-card-modal-title')?.textContent).toBe('Sol Ring');
    expect(castability?.textContent).toBe('CMC 1 · Cast by turn 1: 99% at your current pool size');
    expect(document.querySelector('[data-cutlab-modal-lock]')).toBeNull();
  });

  it('preserves cached popup card details when an ajax patch only updates castability fields', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        cardTextByCardName: {
          'Sol Ring': {
            cmc: 4,
            castPercent: 92.1,
          },
        },
      }) }),
    });

    const acceptButton = document.querySelector<HTMLButtonElement>('.cutlab-proposal [data-cut-lab-decision="accept"]');
    const acceptForm = acceptButton?.closest('form');
    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    document.querySelector<HTMLButtonElement>('.cutlab-cuts-made__row [data-cutlab-card-open="Sol Ring"]')?.click();

    expect(showModalCalls).toBe(1);
    expect(document.querySelector<HTMLElement>('[data-cutlab-modal-meta]')?.textContent).toBe('Artifact · {1}');
    expect(document.querySelector<HTMLElement>('[data-cutlab-modal-oracle]')?.textContent).toContain('{T}: Add {C}{C}.');
    expect(document.querySelector<HTMLElement>('[data-cutlab-modal-castability]')?.textContent).toBe('CMC 4 · Cast by turn 4: 92% at your current pool size');
    expect(document.querySelector<HTMLElement>('[data-cutlab-modal-castability]')?.hidden).toBe(false);
  });

  it('keeps the land disclosure collapsed by default and refreshes its text from the patch', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        currentCount: 108,
        actualLands: 38,
        targetLands: 40,
      }) }),
    });

    const disclosure = document.querySelector<HTMLDetailsElement>('[data-cut-lab-lands-disclosure]');
    expect(disclosure?.open).toBe(false);

    const acceptButton = document.querySelector<HTMLButtonElement>('.cutlab-proposal [data-cut-lab-decision="accept"]');
    const acceptForm = acceptButton?.closest('form');
    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    disclosure!.open = true;
    expect(document.querySelector<HTMLElement>('[data-cut-lab-lands-text]')?.textContent).toBe('Lands: 38/40 (95%) as your pool stands now (108 cards).');
  });

  it('renders restore errors beside the cuts-made list and keeps the row intact on a non-OK response', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: false,
      json: async () => ({}),
    });

    const restoreForm = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]')[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    const cutsMade = document.querySelector<HTMLDetailsElement>('details.cutlab-cuts-made');
    expect(cutsMade?.querySelector<HTMLElement>('[data-cut-lab-decision-error]')?.textContent).toBe("Couldn't recalculate this cut — nothing changed. Try again.");
    expect(cutsMade?.querySelectorAll('.cutlab-cuts-made__row')).toHaveLength(1);
    expect(cutsMade?.textContent).toContain('Mana Crypt');
  });

  it('blocks cross-form submissions while any decision request is in flight', async () => {
    buildDecisionFixture();
    const pendingResponse = deferred<{ ok: boolean; json: () => Promise<CutLabPatchResponse> }>();
    fetchMock.mockReturnValue(pendingResponse.promise);

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const acceptForm = forms[0];
    const acceptButton = acceptForm.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    const restoreForm = forms[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    acceptForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    const decisionButtons = Array.from(document.querySelectorAll<HTMLButtonElement>(
      'form[action="/cut-lab/decide"] button[type="submit"]',
    ));
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(decisionButtons.every(button => button.disabled)).toBe(true);

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(fetchMock).toHaveBeenCalledTimes(1);

    pendingResponse.resolve({
      ok: true,
      json: async () => ({ patch: buildPatch() }),
    });
    await flushDecisionSubmit();

    expect(Array.from(document.querySelectorAll<HTMLButtonElement>(
      'form[action="/cut-lab/decide"] button[type="submit"]',
    )).every(button => !button.disabled)).toBe(true);
  });

  it('keeps sticky locked and current counts visible when a terminal response arrives', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        nextProposal: {
          isTerminal: true,
          isAtTarget: true,
          isNothingToCut: false,
          cardName: '',
          roundKey: '',
          roundLabel: '',
          findingCount: 0,
          findingChips: [],
        },
        proposalDeltas: null,
        floorWarnings: [],
        cardsRemaining: 0,
        currentCount: 100,
        canBuildExport: true,
      }) }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector('.cutlab-sticky-bar')).not.toBeNull();
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-locked]')?.textContent).toBe('1 locked');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-current]')?.textContent).toBe('100/100 cards');
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-round]')?.hidden).toBe(true);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-remaining]')?.hidden).toBe(true);
    expect(document.querySelector<HTMLElement>('[data-cut-lab-sticky-accepted]')?.hidden).toBe(true);
  });

  it('toggles the export tab enabled state based on cards remaining after a decision', async () => {
    buildDecisionFixture();
    const exportTab = document.getElementById('cut-lab-step-tab-5') as HTMLButtonElement | null;
    fetchMock
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ patch: buildPatch({
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
          proposalDeltas: null,
          floorWarnings: [],
          cardsRemaining: 0,
          canBuildExport: true,
        }) }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ patch: buildPatch({
          cardsRemaining: 3,
          canBuildExport: false,
        }) }),
      });

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const acceptForm = forms[0];
    const acceptButton = acceptForm.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');
    acceptForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(exportTab?.disabled).toBe(false);
    expect(exportTab?.getAttribute('aria-disabled')).toBe('false');

    const restoreButton = document.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');
    const restoreForm = restoreButton?.closest('form');
    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(exportTab?.disabled).toBe(true);
    expect(exportTab?.getAttribute('aria-disabled')).toBe('true');
  });

  it('updates the export card-count status from not-at-target to exactly 100 after a decision', async () => {
    buildDecisionFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        currentCount: 100,
        canBuildExport: true,
        cardsRemaining: 0,
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
        proposalDeltas: null,
        floorWarnings: [],
      }) }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    const exportStatus = document.querySelector<HTMLElement>('[data-cut-lab-export-count]');
    expect(exportStatus?.textContent?.trim()).toBe('✅ Card count = 100');
    expect(exportStatus?.querySelector('span')).toBeNull();
  });

  it('updates the export card-count status from exactly 100 back to not-at-target after a restore decision', async () => {
    buildDecisionFixture();
    const exportStatus = document.querySelector<HTMLElement>('[data-cut-lab-export-count]');
    if (exportStatus) {
      exportStatus.innerHTML = '<strong>✅ Card count = 100</strong>';
    }

    const exportTab = document.getElementById('cut-lab-step-tab-5') as HTMLButtonElement | null;
    if (exportTab) {
      exportTab.disabled = false;
      exportTab.setAttribute('aria-disabled', 'false');
    }

    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        currentCount: 101,
        canBuildExport: false,
        cardsRemaining: 1,
      }) }),
    });

    const restoreButton = document.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');
    const restoreForm = restoreButton?.closest('form');
    restoreForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(exportStatus?.querySelector('strong')?.textContent).toBe('❌ Card count = 101');
    expect(exportStatus?.querySelector('span')?.textContent).toBe('Reach 100 cards to unlock the finished-list export.');
  });

  it('shows restore confirmation copy with the card name and clears it after the next successful patch', async () => {
    buildDecisionFixture();
    fetchMock
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ patch: buildPatch({
          cutLabStateJson: '{"version":3}',
          cardsRemaining: 12,
          cutsMade: [],
        }) }),
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ patch: buildPatch() }),
      });

    const forms = document.querySelectorAll<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const restoreForm = forms[3];
    const restoreButton = restoreForm.querySelector<HTMLButtonElement>('[data-cut-lab-restore]');

    restoreForm.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: restoreButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector<HTMLElement>('[data-cut-lab-restore-confirmation]')?.textContent).toBe('Mana Crypt restored — metrics recalculating…');

    const acceptForm = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const acceptButton = acceptForm?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    acceptForm?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: acceptButton ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(document.querySelector('[data-cut-lab-restore-confirmation]')).toBeNull();
  });

  it('confirms restart rounds, posts JSON, and applies the returned patch', async () => {
    buildDecisionFixture();
    confirmMock.mockReturnValue(true);
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ patch: buildPatch({
        cutLabStateJson: '{"version":9}',
        nextProposal: {
          ...buildPatch().nextProposal,
          cardName: 'Arcane Signet',
          roundKey: 'round-2',
          roundLabel: 'Round 2 · Structural choices',
        },
      }) }),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/restart-rounds"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-restart-rounds]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(confirmMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith('/api/cut-lab/restart-rounds', expect.objectContaining({
      method: 'POST',
      headers: expect.objectContaining({
        'Content-Type': 'application/json',
        RequestVerificationToken: 'token-123',
      }),
      body: JSON.stringify({
        cutLabStateJson: '{"version":1}',
      }),
    }));
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Arcane Signet');
    expect(Array.from(document.querySelectorAll<HTMLInputElement>('input[name="CutLabStateJson"]')).map(input => input.value)).toEqual([
      '{"version":9}',
      '{"version":9}',
      '{"version":9}',
      '{"version":9}',
      '{"version":9}',
      '{"version":9}',
    ]);
  });

  it('does nothing when restart rounds confirmation is cancelled', async () => {
    buildDecisionFixture();
    confirmMock.mockReturnValue(false);

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/restart-rounds"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-restart-rounds]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    expect(confirmMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).not.toHaveBeenCalled();
    expect(document.querySelector<HTMLElement>('.cutlab-proposal__heading')?.textContent).toBe('Proposed cut: Sol Ring');
  });
});
