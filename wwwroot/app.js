globalThis.unicodeUi = {
  async copyText(text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      return false;
    }
  }
};

document.addEventListener("click", (event) => {
  if (event.target.closest("#blazor-error-ui .dismiss")) {
    document.querySelector("#blazor-error-ui")?.setAttribute("hidden", "");
  }
});
