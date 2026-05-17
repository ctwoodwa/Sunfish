# anchor-tauri smoke tests

End-to-end smoke tests for the bundled anchor-tauri build, driven via Playwright over the Chrome DevTools Protocol against the running WebView2 instance.

## What this is (and isn't)

**Is:** Playwright tests that attach to a real `anchor-tauri.exe` running in normal-launch mode. The webview is the actual Tauri WebView2 (Edge/Chromium engine) — every `fetch()`, `invoke()`, Stronghold call, and OS-keychain access exercises production code paths. Bridge endpoints are intercepted via Playwright's `page.route()` so no separate mock server is needed.

**Is NOT:** unit tests (those live in `src/**/*.test.ts`, run via Vitest), or a browser-based simulation (Playwright is not launching its own Chromium — it's attaching to the existing WebView2). The Tauri-Rust layer is fully live.

## Why this exists alongside `mcp-windows-computer`

| | `mcp-windows-computer` (coord-based) | Playwright + CDP (this) |
|---|---|---|
| **Element targeting** | Screen pixel coordinates | DOM selectors (`getByRole`, `getByLabel`, `getByText`) |
| **Stability** | Brittle to window position, DPI scaling, theme-induced layout shifts | Auto-wait, auto-retry; no coord math |
| **Iteration speed** | ~10s per click + screenshot cycle | <1s per assertion |
| **CI-friendly** | No (interactive desktop only) | Yes (headed WebView2 runs in workflow runners) |
| **Generality** | Any Windows app (native, browser, MSI installer, etc.) | Tauri/Electron/browser only |
| **What it validates** | Real binary + real OS + real input | Real binary + real OS + DOM-level assertions |

Use this Playwright suite for **per-PR regression smoke** on the React+Tauri surface. Use `mcp-windows-computer` for **out-of-webview testing** — native taskbar/icons, MSI installer flow, Windows-specific UX, Stronghold corruption recovery — where the DOM doesn't help.

## Prerequisites

- Node 20+ + `npm install` in `apps/anchor-tauri/` (Playwright is already a devDependency)
- A built `anchor-tauri.exe` at `src-tauri/target/x86_64-pc-windows-msvc/debug/anchor-tauri.exe`. The `npm run smoke` script builds it for you; `npm run smoke:nobuild` skips the build when you're iterating on the spec.
- PowerShell 7+ (`pwsh`) on PATH. The wrapper script is PowerShell; Bash/zsh equivalents can be added if needed.
- Windows. WebView2 CDP attachment is Windows-only; macOS/Linux Tauri smoke gets its own config when those targets become CI-runnable.

## Usage

From `apps/anchor-tauri/`:

```powershell
# Full cycle: build + clear state + launch with CDP + run Playwright + teardown
npm run smoke

# Iterate on spec without rebuilding the Rust binary
npm run smoke:nobuild

# Run only Playwright against an already-running Tauri with CDP enabled
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"
& .\src-tauri\target\x86_64-pc-windows-msvc\debug\anchor-tauri.exe
# (in another shell)
npm run smoke:test
```

HTML report is written to `apps/anchor-tauri/playwright-report/`. Open `playwright-report/index.html` after a run.

## What the suite covers (today)

`smoke/anchor.spec.ts`:

Local-first cold boot (no token):
1. **AppLayout renders immediately** — Properties nav link + "Connect to Bridge" link visible without any Bridge call; theme tokens applied.
2. **Cold boot does not call Bridge** — reload-and-assert that the app shell is up before any whoami fetch is required.

Connect-to-Bridge flow (`/settings/bridge`):
3. **Empty submit validation** — clicking Connect with an empty textarea shows "Token cannot be empty" without making any Bridge call.
4. **Bridge 401 rejection** — Playwright intercepts `/api/v1/whoami` with 401; ConnectBridgePage shows "Bridge rejected the token: 401" and stays.
5. **Network failure** — Playwright aborts the fetch with `connectionrefused`; ConnectBridgePage shows "Could not reach Bridge at …" and stays.
6. **Continue offline button** — bypasses the connect flow back to the app shell without touching Bridge.
7. **PASS path** — `/api/v1/whoami` returns 200; token persists via Stronghold; user lands back on Properties; header shows Disconnect (not Connect). Test ends with Disconnect to leave clean state for next run.

Each spec uses `page.route()` for Bridge mocking so there's no separate Python HTTP server to manage — Playwright handles request interception transparently.

## What's NOT covered yet (carry-forward)

- **A1.1 round-trip across process restart** — `connectOverCDP` attaches to ONE running process. A round-trip test needs a wrapper that quits Tauri, relaunches, and reconnects Playwright to the new instance. Doable as a `test.beforeAll(async () => { await spawnAndAttach(); })` orchestration; not implemented yet.
- **Disconnect across relaunch** — Disconnect → quit + relaunch → still offline with Connect link. Same restart-mid-test problem as above.
- **Keychain-failure banner (A1.4)** — requires either mocking Tauri's `keychain_status` IPC (Playwright can't override Rust state) or forcing a real keychain failure (GPO toggle, denied Keychain dialog). Better validated via Rust integration test.
- **Surface Pro cross-device round-trip** — different hardware = different keychain. Future work when there's a Tauri-CI runner with the right target.

## File layout

```
apps/anchor-tauri/
├── smoke/
│   ├── README.md            ← you are here
│   ├── playwright.config.ts ← Playwright config (testDir, reporters, etc.)
│   ├── fixtures.ts          ← `connectOverCDP` fixture exposing the Tauri page
│   └── anchor.spec.ts       ← the actual smoke tests
├── scripts/
│   └── run-smoke.ps1        ← orchestration: build → launch with CDP → wait → test → teardown
└── package.json             ← `npm run smoke`, `smoke:nobuild`, `smoke:test`
```

## Adding a new test

1. Stick it in `smoke/anchor.spec.ts` or a new `smoke/<feature>.spec.ts`.
2. Import from `./fixtures` (not `@playwright/test` directly) so you get the Tauri-attached `page`.
3. Prefer `page.route()` for Bridge mocking over a separate server — keeps the spec self-contained.
4. Prefer `getByRole` / `getByLabel` / `getByText` over CSS selectors so the tests survive component refactors.
5. If you need DOM-level assertions on the Rust state, expose a `__test_*` window global behind an `import.meta.env.DEV` gate (not for production builds).

## Troubleshooting

**`No browser contexts found at http://localhost:9222`** — anchor-tauri isn't running with the CDP env var set. Run `npm run smoke` to use the wrapper, or set `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` and launch the exe yourself.

**`CDP endpoint never came up within 30s`** — anchor-tauri crashed during boot, or the WebView2 didn't honor the env var (older WebView2 runtimes don't support remote debugging). Try `npm run tauri:dev` to debug the boot path first.

**Tests pass locally but fail in CI** — your CI runner may not have a graphics surface for WebView2. Use a Windows runner with a desktop session (`windows-latest` on GitHub Actions works; minimal Server Core images don't).

**Stronghold persist times out** — `iota_stronghold` engine init on cold first-launch is slow (10–15s on modest hardware). The PASS-path test's `toBeVisible({ timeout: 30_000 })` accounts for this; bump higher if you're on a slow runner.
