((): void => {
  'use strict';

  // MEDIUM-11: mark the reduced-cost box as "touched" the moment the user edits it, so the server
  // can tell a deliberately cleared box (reject the suggestions) apart from an untouched pre-fill and
  // stop silently refilling it. One-shot: the flag only ever goes false -> true for this render.
  const box = document.getElementById('manabase-cost-overrides') as HTMLTextAreaElement | null;
  const touched = document.getElementById('manabase-overrides-touched') as HTMLInputElement | null;
  if (box === null || touched === null) {
    return;
  }

  box.addEventListener(
    'input',
    (): void => {
      touched.value = 'true';
    },
    { once: true },
  );
})();
