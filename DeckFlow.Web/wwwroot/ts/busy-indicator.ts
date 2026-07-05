// Busy-indicator / progress overlay (Phase 82 SRP split — concern #2 of deck-sync.ts's
// 6-concern violation per 82-REVIEW.md). Extracted verbatim from deck-sync.ts (lines 655-826) —
// contains no legacy AI-platform-prefixed identifiers, so it required no changes during the
// Phase 85 naming cleanup.
//
// Compiles under tsconfig's `module: "none"` — no import/export. Every view that loads
// deck-sync.js also loads this script first (see the `<script>` ordering in the Razor views), so
// in the browser deck-sync.ts's bootstrapDeckSync() could call these functions by bare name (tsc
// unifies all `wwwroot/ts/*.ts` files into one type-checking program and the browser shares one
// global scope across separate <script> tags) — but Vitest/esbuild treats each dynamically-
// imported .ts file as its own isolated ES module and does NOT share bare top-level identifiers
// across them, so cross-file calls instead go through `window.*` (see the bottom of this file and
// deck-sync.ts's `interface Window` comment) — the same bridge pattern this project already uses
// for `window.DeckFlow`. Tests exercising this file's behavior import it before deck-sync.ts (see
// ts-tests/busy-overlay-pageshow.test.ts and ts-tests/busy-indicator-progress.test.ts).
let busyProgressTimer: number | undefined;
let busyHideTimer: number | undefined;

const formatProgressText = (steps: string[], index: number) => `Step ${index + 1}/${steps.length}: ${steps[index]}`;

const clearBusyProgress = (): void => {
  if (busyProgressTimer !== undefined) {
    window.clearInterval(busyProgressTimer);
    busyProgressTimer = undefined;
  }
};

const hideBusyIndicator = (): void => {
  const container = document.getElementById('busy-indicator');
  const progressNode = document.getElementById('busy-indicator-progress');
  if (!container) {
    return;
  }

  container.classList.add('hidden');
  if (progressNode) {
    progressNode.textContent = '';
    delete progressNode.dataset.currentIndex;
  }

  clearBusyProgress();
  if (busyHideTimer !== undefined) {
    window.clearTimeout(busyHideTimer);
    busyHideTimer = undefined;
  }
};

// Why: the bridge intercept runs in capture, but the busy overlay shows later in
// bubble. Abort-path hides must be deferred to a macrotask so they run after the
// bubble-phase showBusyIndicator() listener.
const abortBridgeBusy = (): void => {
  window.setTimeout(hideBusyIndicator, 0);
};

const scheduleBusyHide = (durationMs: number): void => {
  if (!durationMs || durationMs <= 0) {
    return;
  }

  if (busyHideTimer !== undefined) {
    window.clearTimeout(busyHideTimer);
  }

  busyHideTimer = window.setTimeout(() => {
    hideBusyIndicator();
  }, durationMs);
};

const showBusyIndicator = (
  title?: string,
  message?: string,
  progressSteps?: string[],
  durationMs?: number,
  holdFinalStep = false
): void => {
  const container = document.getElementById('busy-indicator');
  const titleNode = document.getElementById('busy-indicator-title');
  const messageNode = document.getElementById('busy-indicator-message');
  const progressNode = document.getElementById('busy-indicator-progress');
  if (!container || !titleNode || !messageNode) {
    return;
  }

  titleNode.textContent = title || 'Working';
  messageNode.textContent = message || 'Request in progress.';
  container.classList.remove('hidden');

  clearBusyProgress();
  if (progressNode) {
    if (progressSteps && progressSteps.length > 0) {
      const finalIndex = progressSteps.length - 1;
      let currentIndex = 0;
      progressNode.textContent = formatProgressText(progressSteps, currentIndex);
      progressNode.dataset.currentIndex = currentIndex.toString();

      busyProgressTimer = window.setInterval(() => {
        currentIndex++;

        if (currentIndex > finalIndex) {
          currentIndex = holdFinalStep ? finalIndex : 0;
        }

        progressNode.dataset.currentIndex = currentIndex.toString();
        progressNode.textContent = formatProgressText(progressSteps, currentIndex);

        if (holdFinalStep && currentIndex === finalIndex) {
          clearBusyProgress();
        }
      }, 4000);
    } else {
      progressNode.textContent = '';
    }
  }
  if (durationMs && durationMs > 0) {
    scheduleBusyHide(durationMs);
  }
};

const registerBusyIndicator = (): void => {
  document.querySelectorAll<HTMLFormElement>('form[data-busy-title]').forEach(form => {
    form.addEventListener('submit', (event: Event) => {
      const submitter = (event as SubmitEvent).submitter;

      // Release pass of a min-display delay (see below): the overlay is already up, so let this
      // re-fired submit navigate without re-showing or re-delaying.
      if (form.dataset.busyMinReleased === 'true') {
        delete form.dataset.busyMinReleased;
        return;
      }

      if (submitter?.hasAttribute('data-no-busy')) {
        return;
      }
      // A submit button may override the form-level busy copy (e.g. a "Load" button on the same
      // form that does less work than the primary submit). Fall back to the form's attributes.
      const attr = (name: string): string | null =>
        (submitter instanceof HTMLElement ? submitter.getAttribute(name) : null) ?? form.getAttribute(name);
      const title = attr('data-busy-title');
      const message = attr('data-busy-message');
      const stepsAttr = attr('data-busy-progress');
      const steps = stepsAttr
        ? stepsAttr
            .split('|')
            .map(step => step.trim())
            .filter(step => step.length > 0)
        : [];
      const durationAttr = form.getAttribute('data-busy-duration');
      const duration = durationAttr ? parseInt(durationAttr, 10) : undefined;
      const holdFinalAttr = form.getAttribute('data-busy-hold-final-step');
      const holdFinalStep = holdFinalAttr !== null && holdFinalAttr.toLowerCase() === 'true';
      showBusyIndicator(
        title ?? undefined,
        message ?? undefined,
        steps.length > 0 ? steps : undefined,
        duration,
        holdFinalStep
      );

      // Optional minimum display floor: a full-page POST that returns quickly (e.g. cached cards)
      // navigates before the overlay is ever perceived, so it just flashes. When the form opts in via
      // data-busy-min-ms, hold the submit briefly so the "Analyzing…" state is actually seen, then
      // re-fire it. Skipped when another handler already owns this submit (event.defaultPrevented —
      // the Moxfield extension-bypass path, which is already slow enough to show the overlay).
      const minMs = parseInt(form.getAttribute('data-busy-min-ms') ?? '', 10);
      if (Number.isFinite(minMs) && minMs > 0 && !event.defaultPrevented) {
        event.preventDefault();
        window.setTimeout(() => {
          form.dataset.busyMinReleased = 'true';
          if (typeof form.requestSubmit === 'function') {
            // Preserve which button submitted (its formaction, e.g. the Load vs Analyze endpoint).
            form.requestSubmit(
              submitter instanceof HTMLButtonElement || submitter instanceof HTMLInputElement
                ? submitter
                : undefined
            );
          } else {
            form.submit();
          }
        }, minMs);
      }
    });
  });

  window.addEventListener('pageshow', () => {
    hideBusyIndicator();
  });
};

// Why: cross-file bridge (Phase 82 SRP split) — deck-sync.ts's bootstrap calls these by their
// `window.*` names, and moxfield-extension-bridge.ts's abort paths call `window.abortBridgeBusy?.()`.
// See deck-sync.ts's `interface Window` comment for why this goes through `window` rather than a
// bare cross-file identifier.
interface Window {
  hideBusyIndicator?: () => void;
  registerBusyIndicator?: () => void;
  abortBridgeBusy?: () => void;
}

window.hideBusyIndicator = hideBusyIndicator;
window.registerBusyIndicator = registerBusyIndicator;
window.abortBridgeBusy = abortBridgeBusy;
