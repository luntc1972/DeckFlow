import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import '../wwwroot/ts/cut-lab';

interface CutLabPatchResponse {
  patch?: {
    cutLabStateJson: string;
    currentCount: number;
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
    floorWarnings: Array<{ message: string }>;
    cardsRemaining: number;
    cutsMade: Array<{ cardName: string; roundKey: string; roundLabel: string; ordinal: number }>;
    comboBadgeByCardName: Record<string, { badgeState: 'CompletePiece' | 'NeedsPartner'; context: string }>;
    structuralFindings: Array<{
      kind: string;
      heading: string;
      items: Array<{
        kind: string;
        heading: string;
        lead: string;
        evidence: string[];
      }>;
    }>;
    comboDataAvailable: boolean;
    categoryDataAvailable: boolean;
    whatifCardOutOptions: string[];
    whatifCardInOptions: string[];
    quantityTuners: Array<unknown>;
    addableBasics: string[];
  } | null;
}

let fetchMock: ReturnType<typeof vi.fn>;
let showModalCalls = 0;

beforeAll(() => {
  fetchMock = vi.fn();
  vi.stubGlobal('fetch', fetchMock);
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
  showModalCalls = 0;
});

const flushDecisionSubmit = async (): Promise<void> => {
  await Promise.resolve();
  await Promise.resolve();
  await new Promise(resolve => window.setTimeout(resolve, 0));
};

const buildFixture = (): void => {
  document.body.innerHTML = `
    <form data-cache-key="cut-lab" data-cut-lab-decide-action="/cut-lab/decide" data-cut-lab-decide-api="/api/cut-lab/decide">
      <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
      <textarea name="PrimaryPlan">Keep the interaction dense.</textarea>
      <textarea name="SecondaryPlan"></textarea>
      <input type="radio" name="Bracket" value="3" checked />
      <input type="radio" name="PlayExperience" value="Focused" checked />
      <table>
        <tbody>
          <tr data-cut-lab-card="Counterspell" data-cut-lab-type-line="Instant" data-cut-lab-role="interaction" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Counterspell" /></td>
            <td data-label="Card"><strong>1 × Counterspell</strong></td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Counterspell"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
          <tr data-cut-lab-card="Command Tower" data-cut-lab-type-line="Land" data-cut-lab-role="lands ramp" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Command Tower" /></td>
            <td data-label="Card"><strong>1 × Command Tower</strong></td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Command Tower"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
          <tr data-cut-lab-card="Commander" data-cut-lab-type-line="Legendary Creature" data-cut-lab-role="payoffs wincons" data-cut-lab-quantity="1" data-cut-lab-commander="true">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Commander" checked disabled /></td>
            <td data-label="Card"><strong>1 × Commander</strong></td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Commander"><option value="">Unlocked pool</option></select>
            </td>
          </tr>
        </tbody>
      </table>
    </form>
    <section class="result-panel">
      <div class="cutlab-proposal" data-cut-lab-card="Sol Ring" data-cut-lab-round="round-1">
        <div class="cutlab-proposal__actions">
          <form method="post" action="/cut-lab/decide">
            <input type="hidden" name="__RequestVerificationToken" value="token-123" />
            <input type="hidden" name="CutLabStateJson" value="{&quot;version&quot;:1}" />
            <input type="hidden" name="CardName" value="Sol Ring" />
            <input type="hidden" name="RoundKey" value="round-1" />
            <input type="hidden" name="Decision" value="accept" />
            <button type="submit" data-cut-lab-decision="accept" data-cut-lab-card="Sol Ring">Accept cut</button>
          </form>
        </div>
      </div>
    </section>
    <section class="result-panel" data-cut-lab-structural-findings>
      <div class="panel-heading">
        <div><h2>Structural findings</h2></div>
        <div data-cut-lab-findings-count-slot><span class="cutlab-findings-count"></span></div>
      </div>
      <div data-cut-lab-structural-findings-body>
        <p>Old body</p>
      </div>
      <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="combo">Combo data unavailable right now.</p>
      <p class="cutlab-degradation-note hidden" data-cut-lab-degradation="category">Community category data unavailable.</p>
    </section>
    <div class="cutlab-sticky-bar">
      <span data-cut-lab-sticky-round>Round 1</span>
      <span data-cut-lab-sticky-remaining>12 to cut</span>
      <span data-cut-lab-sticky-accepted>0 cut so far</span>
    </div>
    <div class="cutlab-round-banner"></div>
    <button type="button" id="cut-lab-step-tab-4" class="is-disabled" disabled aria-disabled="true">Export</button>
    <script type="application/json" id="cutlab-card-text-data">{"Counterspell":{"typeLine":"Instant","manaCost":"{U}{U}","setCode":"TMP","collectorNumber":"55","oracleText":"Counter target spell."}}</script>
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
};

const buildPatch = (): CutLabPatchResponse => ({
  patch: {
    cutLabStateJson: '{"version":2}',
    currentCount: 111,
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
      findingChips: ['Weak floor cases'],
    },
    proposalDeltas: null,
    floorWarnings: [],
    cardsRemaining: 11,
    cutsMade: [],
    comboBadgeByCardName: {},
    structuralFindings: [
      {
        kind: 'WeakFloorCase',
        heading: 'Weak floor cases',
        items: [
          {
            kind: 'WeakFloorCase',
            heading: 'Weak floor cases',
            lead: 'Interaction is below the floor.',
            evidence: ['Counterspell', 'Curve congestion at MV 2'],
          },
        ],
      },
    ],
    comboDataAvailable: true,
    categoryDataAvailable: true,
    whatifCardOutOptions: [],
    whatifCardInOptions: [],
    quantityTuners: [],
    addableBasics: [],
  },
});

const applyStructuralPatch = async (): Promise<HTMLElement> => {
  buildFixture();
  fetchMock.mockResolvedValue({
    ok: true,
    json: async () => buildPatch(),
  });

  const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
  const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

  form?.dispatchEvent(new SubmitEvent('submit', {
    bubbles: true,
    cancelable: true,
    submitter: button ?? undefined,
  }));
  await flushDecisionSubmit();

  const findingsBody = document.querySelector<HTMLElement>('[data-cut-lab-structural-findings-body]');
  expect(findingsBody).not.toBeNull();
  return findingsBody!;
};

describe('cut-lab structural evidence locking', () => {
  it('renders matched structural evidence as a lockable button and unmatched evidence as an inert span', async () => {
    const findingsBody = await applyStructuralPatch();

    const matchedButton = findingsBody.querySelector<HTMLButtonElement>('button[data-cut-lab-chip-card="Counterspell"]');
    const inertSpan = Array.from(findingsBody.querySelectorAll<HTMLSpanElement>('span.kb-chip'))
      .find(chip => chip.textContent === 'Curve congestion at MV 2');

    expect(matchedButton).not.toBeNull();
    expect(matchedButton?.getAttribute('aria-pressed')).toBe('false');
    expect(inertSpan).not.toBeNull();
    expect(inertSpan?.dataset.cutLabChipCard).toBeUndefined();
    expect(inertSpan?.getAttribute('aria-pressed')).toBeNull();
    expect(findingsBody.querySelector('button[data-cut-lab-chip-card="Curve congestion at MV 2"]')).toBeNull();
  });

  it('opens the modal from a matched chip and locks through the canonical pool checkbox while inert spans remain no-ops', async () => {
    const findingsBody = await applyStructuralPatch();
    const matchedSelector = 'button[data-cut-lab-chip-card="Counterspell"]';
    const counterspellCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Counterspell"]');
    const commandTowerCheckbox = document.querySelector<HTMLInputElement>('input[data-cut-lab-lock-card="Command Tower"]');
    const inertSpan = Array.from(findingsBody.querySelectorAll<HTMLSpanElement>('span.kb-chip'))
      .find(chip => chip.textContent === 'Curve congestion at MV 2');
    const modalLockButton = document.querySelector<HTMLButtonElement>('[data-cutlab-modal-lock]');

    expect(counterspellCheckbox?.checked).toBe(false);
    expect(commandTowerCheckbox?.checked).toBe(false);

    document.querySelector<HTMLButtonElement>(matchedSelector)?.click();
    expect(showModalCalls).toBe(1);
    modalLockButton?.click();
    expect(counterspellCheckbox?.checked).toBe(true);
    expect(document.querySelector<HTMLButtonElement>(matchedSelector)?.getAttribute('aria-pressed')).toBe('true');

    modalLockButton?.click();
    expect(counterspellCheckbox?.checked).toBe(false);
    expect(document.querySelector<HTMLButtonElement>(matchedSelector)?.getAttribute('aria-pressed')).toBe('false');

    inertSpan?.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    expect(counterspellCheckbox?.checked).toBe(false);
    expect(commandTowerCheckbox?.checked).toBe(false);
  });
});
