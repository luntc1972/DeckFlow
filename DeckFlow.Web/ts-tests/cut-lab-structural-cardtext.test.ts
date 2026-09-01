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
        roles?: string[];
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
          <tr data-cut-lab-card="Counterspell" data-cut-lab-type-line="Instant" data-cut-lab-role="interaction-targeted" data-cut-lab-quantity="1" data-cut-lab-commander="false">
            <td data-label="Select"><input type="checkbox" data-cut-lab-lock-card="Counterspell" /></td>
            <td data-label="Card"><strong>1 × Counterspell</strong></td>
            <td data-label="Package">
              <select data-cut-lab-package-card="Counterspell"><option value="">Unlocked pool</option></select>
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
    <button type="button" id="cut-lab-step-tab-5" class="prompt-step-tab" disabled aria-disabled="true">Export</button>
    <script type="application/json" id="cutlab-card-text-data">{"Counterspell":{"typeLine":"Instant","manaCost":"{U}{U}","setCode":"TMP","collectorNumber":"55","oracleText":"Counter target spell.","comboContext":"Infinite cards"}}</script>
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

const buildPatch = (comboContext = 'Infinite cards'): CutLabPatchResponse => ({
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
    comboBadgeByCardName: {
      Counterspell: {
        badgeState: 'CompletePiece',
        context: comboContext,
      },
    },
    structuralFindings: [
      {
        kind: 'WeakFloorCase',
        heading: 'Weak floor cases',
        items: [
          {
            kind: 'WeakFloorCase',
            heading: 'Weak floor cases',
            lead: 'Interaction is below the floor.',
            evidence: ['Counterspell'],
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

// Must stay character-identical to the Razor twins help note in Views/Deck/CutLab.cshtml.
const twinsHelpNote = 'Slot Congestion means these cards share the same role, card type, and exact mana value. Treat them as review candidates, not automatic cuts — a card here may also be combo-protected.';

const buildTwinsPatch = (): CutLabPatchResponse => {
  const response = buildPatch();
  response.patch!.structuralFindings = [
    {
      kind: 'FunctionalTwins',
      heading: 'Slot Congestion',
      items: [
        {
          kind: 'FunctionalTwins',
          heading: 'Slot Congestion',
          lead: 'Three interaction instants share the Targeted removal role, card type, and exact mana value 1 — treat them as review candidates, not an automatic cut.',
          evidence: ['Counterspell'],
          roles: ['Targeted removal'],
        },
      ],
    },
  ];

  return response;
};

describe('cut-lab structural card popup data', () => {
  it('rebuilds structural evidence chips as popup triggers that preserve combo badges', async () => {
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

    const rebuiltChip = document.querySelector<HTMLButtonElement>('[data-cut-lab-structural-findings-body] button[data-cut-lab-chip-card="Counterspell"]');
    const rebuiltBadge = rebuiltChip?.querySelector<HTMLSpanElement>('.cutlab-combo-badge');
    expect(rebuiltChip?.childNodes[0]?.textContent).toBe('Counterspell');
    expect(rebuiltBadge?.textContent).toBe('Combo piece');
    expect(rebuiltChip?.dataset.cutlabCardOpen).toBe('Counterspell');
  });

  it('refreshes popup combo context from the patch map when the combo context changes', async () => {
    buildFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildPatch('Infinite mana'),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    const rebuiltChip = document.querySelector<HTMLButtonElement>('[data-cut-lab-structural-findings-body] button[data-cut-lab-chip-card="Counterspell"]');
    const comboLine = document.querySelector<HTMLElement>('[data-cutlab-modal-combo]');

    rebuiltChip?.click();

    expect(showModalCalls).toBe(1);
    expect(comboLine?.textContent).toBe('Infinite mana');
  });

  it('renderStructuralFindings_FunctionalTwins_RendersSameHelpNoteAsRazor', async () => {
    buildFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildTwinsPatch(),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    const notes = document.querySelectorAll<HTMLElement>('[data-cut-lab-structural-findings-body] .cutlab-finding p.manabase-help');
    expect(notes.length).toBe(1);
    expect(notes[0]?.textContent).toBe(twinsHelpNote);
  });

  // Why: T-041-03. Pins the AJAX-rendered Slot Congestion copy and role line, and pins the
  // absence of the legacy overclaiming strings so a future edit that reintroduces
  // "Functional twins" or "costliest group" prose in the TS render path fails loudly.
  it('renderStructuralFindings_FunctionalTwins_UsesSlotCongestionAndRendersRoles_NoLegacyWording', async () => {
    buildFixture();
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => buildTwinsPatch(),
    });

    const form = document.querySelector<HTMLFormElement>('form[action="/cut-lab/decide"]');
    const button = form?.querySelector<HTMLButtonElement>('[data-cut-lab-decision="accept"]');

    form?.dispatchEvent(new SubmitEvent('submit', {
      bubbles: true,
      cancelable: true,
      submitter: button ?? undefined,
    }));
    await flushDecisionSubmit();

    const body = document.querySelector<HTMLElement>('[data-cut-lab-structural-findings-body]');
    const heading = body?.querySelector<HTMLElement>('.cutlab-finding__heading');
    const rolesLine = body?.querySelector<HTMLElement>('.cutlab-finding__roles');

    expect(heading?.textContent).toBe('Slot Congestion');
    expect(rolesLine?.textContent).toBe('Role: Targeted removal');
    expect(body?.textContent).toContain('exact mana value');
    expect(body?.textContent).not.toContain('Functional twins');
    expect(body?.textContent).not.toContain('costliest group');
  });
});
