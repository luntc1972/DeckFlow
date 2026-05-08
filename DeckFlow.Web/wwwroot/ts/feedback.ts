(function () {
    const form = document.querySelector<HTMLFormElement>(".feedback-form");
    if (!form) return;
    form.addEventListener("submit", () => {
        const btn = form.querySelector<HTMLButtonElement>("button[type='submit']");
        if (btn) {
            btn.disabled = true;
            btn.textContent = "Sending…";
        }
    });
}());
