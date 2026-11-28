const storageKey = "swimburger.unicode.state.v1";
let shortcutController;

function encodeState(text) {
  const bytes = new TextEncoder().encode(text);
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replace(/=+$/, "");
}

function decodeState(text) {
  if (!/^[A-Za-z0-9_-]+$/.test(text) || text.length > 12_000) return null;
  const base64 = text.replaceAll("-", "+").replaceAll("_", "/").padEnd(Math.ceil(text.length / 4) * 4, "=");
  const binary = atob(base64);
  const bytes = Uint8Array.from(binary, character => character.charCodeAt(0));
  return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
}

globalThis.unicodeUi = {
  async copyText(text) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      return false;
    }
  },
  attachAnalyzeShortcut(inputId) {
    shortcutController?.abort();
    shortcutController = new AbortController();
    document.getElementById(inputId)?.addEventListener("keydown", event => {
      if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
        event.preventDefault();
        event.currentTarget.closest("form")?.requestSubmit();
      }
    }, { signal: shortcutController.signal });
  },
  readSharedState() {
    try {
      const encoded = new URLSearchParams(location.hash.slice(1)).get("state");
      return encoded ? decodeState(encoded) : null;
    } catch {
      return null;
    }
  },
  async copyShareLink(stateJson) {
    try {
      const encoded = encodeState(stateJson);
      if (encoded.length > 8_000) return "too-long";
      const url = new URL(location.href);
      url.hash = new URLSearchParams({ state: encoded }).toString();
      history.replaceState(null, "", url);
      await navigator.clipboard.writeText(url.href);
      return "copied";
    } catch {
      return "failed";
    }
  },
  clearShareState() {
    if (!location.hash) return;
    history.replaceState(null, "", `${location.pathname}${location.search}`);
  },
  readStoredState() {
    try { return localStorage.getItem(storageKey); } catch { return null; }
  },
  writeStoredState(stateJson) {
    try { localStorage.setItem(storageKey, stateJson); } catch { /* Storage can be unavailable or full. */ }
  },
  forgetStoredState() {
    try { localStorage.removeItem(storageKey); } catch { /* Storage can be unavailable. */ }
  }
};

document.addEventListener("click", (event) => {
  if (event.target.closest("#blazor-error-ui .dismiss")) {
    document.querySelector("#blazor-error-ui")?.setAttribute("hidden", "");
  }
});

Blazor.start({
  loadBootResource(type, _name, defaultUri) {
    return type === "dotnetjs" ? `${defaultUri}?v=unicode-product-v2` : defaultUri;
  }
});
