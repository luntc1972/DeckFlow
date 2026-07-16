// Wires the share bar: copy-to-clipboard and (mobile) native Web Share.
// Real <a> intent links (Reddit/X/Bluesky) work without this script.
(() => {
  const bar = document.querySelector<HTMLElement>('[data-share-bar]');
  if (!bar) {
    return;
  }

  const copyButton = bar.querySelector<HTMLButtonElement>('.share-bar__copy');
  if (copyButton) {
    copyButton.addEventListener('click', async () => {
      const url = copyButton.dataset.shareUrl ?? '';
      const text = copyButton.dataset.shareText ?? '';
      const payload = text ? `${text}\n${url}` : url;
      const original =
        copyButton.dataset.copyOriginalText ?? copyButton.textContent?.trim() ?? 'Copy link';
      copyButton.dataset.copyOriginalText = original;
      try {
        await navigator.clipboard.writeText(payload);
        copyButton.textContent = 'Copied';
        copyButton.classList.add('is-copied');
      } catch {
        copyButton.textContent = 'Copy failed';
        copyButton.classList.add('is-copy-failed');
      }
      window.setTimeout(() => {
        copyButton.textContent = original;
        copyButton.classList.remove('is-copied', 'is-copy-failed');
      }, 2000);
    });
  }

  const nativeButton = bar.querySelector<HTMLButtonElement>('.share-bar__native');
  if (nativeButton && typeof navigator.share === 'function') {
    nativeButton.hidden = false;
    nativeButton.addEventListener('click', async () => {
      try {
        await navigator.share({
          title: 'DeckFlow',
          text: nativeButton.dataset.shareText ?? '',
          url: nativeButton.dataset.shareUrl ?? '',
        });
      } catch {
        // User cancelled (AbortError) or share failed — no-op.
      }
    });
  }
})();
