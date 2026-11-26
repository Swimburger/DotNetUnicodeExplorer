# .NET String & Unicode Explorer

An in-browser quick reference for seeing how .NET represents text as UTF-16 code units, Unicode scalar values (`Rune`), extended grapheme clusters (`StringInfo`), encoded bytes, and normalization forms.

The production site is [unicode.swimburger.net](https://unicode.swimburger.net/).

## Why this exists

A visible “character” can be one UTF-16 code unit, a surrogate pair, several Unicode scalar values, or a complete grapheme cluster. Answering a small question normally means opening a scratch project and writing several loops. This tool runs those .NET APIs side-by-side and keeps all input in the browser.

## Features

- Text and C# escaped input modes
- Curated emoji, combining-mark, script, whitespace, and compatibility examples
- Synchronized `StringInfo`, `Rune`, and UTF-16 views
- UTF-8, UTF-16 LE/BE, and UTF-32 LE/BE byte output
- NFC and NFD comparison, with clear NFKC/NFKD runtime support status
- Copyable escaped strings, bytes, normalized values, and C# code
- Responsive, semantic tables and keyboard-accessible controls
- Static deployment on Cloudflare Workers with Brotli sidecar support

Formal Unicode character names are intentionally out of scope because the .NET base class library does not expose the Unicode Character Database name property.

## Prerequisites

- .NET SDK 11.0.100 or later in the 11.0 feature band
- Node.js 24 or later
- pnpm 12.23.0
- The `wasm-tools` .NET workload for optimized publishing

## Develop

```sh
dotnet workload install wasm-tools
dotnet run
```

## Build and test

```sh
pnpm install
dotnet test --configuration Release
pnpm run build:cloudflare
pnpm exec playwright install chromium
pnpm run test:e2e
```

The published Blazor app is written to `dist/wwwroot`, and `cf build` creates the deployable Cloudflare Build Output.

## Deploy

Authenticate the Cloudflare CLI and deploy the prebuilt output:

```sh
pnpm run deploy
```

The Worker uses the custom domain `unicode.swimburger.net`, compatibility date `2026-11-26`, Workers Logs, and sampled traces.

## Privacy

The application has no product analytics or server-side input processing. Input is evaluated entirely inside the browser by Blazor WebAssembly. Cloudflare may retain standard request metadata through Workers Logs and sampled traces, but the text entered into the explorer is never sent with those requests.

## Browser runtime note

The .NET browser runtime supports canonical normalization (NFC and NFD), but marks compatibility normalization (NFKC and NFKD) unsupported. The explorer keeps all four cards visible and labels the compatibility forms as unavailable rather than substituting a JavaScript implementation. The shared core library computes all four forms when it runs on a supported non-browser .NET runtime.

## License

[MIT](LICENSE)
