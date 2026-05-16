import { defineConfig } from '@playwright/test'

/**
 * Playwright config for anchor-tauri WebView2 smoke tests.
 *
 * Strategy: attach to an already-running anchor-tauri.exe via Chrome
 * DevTools Protocol on `:9222`. Tauri's underlying WebView2 (Edge/Chromium)
 * exposes CDP when launched with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=
 * "--remote-debugging-port=9222"`. The wrapper script `scripts/run-smoke.ps1`
 * handles launching + waiting for the port + invoking `npx playwright test`.
 *
 * We do NOT launch our own browser — Playwright connects to the existing
 * WebView2 process so we're testing the real Tauri build (real IPC, real
 * Stronghold, real OS keychain), not a synthetic browser.
 */
export default defineConfig({
  testDir: '.',
  testMatch: '**/*.spec.ts',
  timeout: 60_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list'], ['html', { outputFolder: '../playwright-report', open: 'never' }]],
  use: {
    // Connect to the running anchor-tauri WebView2 via CDP. No browser binary
    // launches — Playwright attaches over WebSocket to localhost:9222.
    connectOptions: {
      wsEndpoint: '', // unused for connectOverCDP — see test fixtures
    },
    actionTimeout: 10_000,
    navigationTimeout: 10_000,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  // Single project — desktop-Windows-only since we attach to a Windows-bound
  // Tauri build. macOS/Linux Tauri smoke gets its own config when those
  // targets become CI-runnable.
  projects: [
    {
      name: 'tauri-webview2-win',
    },
  ],
})
