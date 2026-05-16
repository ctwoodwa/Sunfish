import { test as base, chromium, type Browser, type Page } from '@playwright/test'

/**
 * Playwright fixture that connects to the already-running anchor-tauri
 * WebView2 via Chrome DevTools Protocol on `localhost:9222`. The wrapper
 * script (`scripts/run-smoke.ps1`) launches the Tauri build with the
 * `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS` env var set and waits for the
 * port to be ready before invoking `npx playwright test`.
 *
 * `page` here is the actual Tauri WebView2 page — every fetch, `invoke()`,
 * Stronghold call, and IPC round-trip exercises the real binary, not a
 * synthetic browser stand-in.
 */
export const test = base.extend<{
  tauriBrowser: Browser
  page: Page
}>({
  tauriBrowser: async ({}, use) => {
    const wsEndpointUrl = process.env.TAURI_CDP_URL ?? 'http://127.0.0.1:9222'
    const browser = await chromium.connectOverCDP(wsEndpointUrl)
    await use(browser)
    // Don't close — the Tauri process owns the browser; closing here would
    // tear down the user's anchor-tauri.exe.
  },
  page: async ({ tauriBrowser }, use) => {
    const contexts = tauriBrowser.contexts()
    if (contexts.length === 0) {
      throw new Error(
        `No browser contexts found at ${process.env.TAURI_CDP_URL ?? 'http://127.0.0.1:9222'} — ` +
          `is anchor-tauri running with WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222?`,
      )
    }
    const context = contexts[0]!
    const pages = context.pages()
    if (pages.length === 0) {
      throw new Error('Tauri context has no pages — webview not yet loaded?')
    }
    // anchor-tauri opens a single window; use its first page.
    await use(pages[0]!)
  },
})

export { expect } from '@playwright/test'
