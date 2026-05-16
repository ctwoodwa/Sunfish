import { test, expect } from './fixtures'

/**
 * anchor-tauri WebView2 smoke tests.
 *
 * Each test connects to a running Tauri build via CDP and drives the real
 * webview — same code path that ships, including Stronghold + OS keychain +
 * Tauri IPC. Bridge calls are intercepted via Playwright's `page.route()`
 * so we don't need a separate mock server.
 *
 * Preconditions (handled by `scripts/run-smoke.ps1`):
 *   - anchor-tauri.exe is running with `--remote-debugging-port=9222`
 *   - Stronghold snapshot is cleared (fresh first-launch state) so the
 *     LoginPage renders. Tests that need an authenticated state set up
 *     their own token via `setup()`.
 *
 * Note on isolation: connectOverCDP gives a SHARED browser context. State
 * (Stronghold, authStore, etc.) persists across tests in the same run.
 * Tests are ordered so the auth flow runs in dependency order. For full
 * isolation, the wrapper script can quit/relaunch Tauri between specs.
 */

test.describe('LoginPage smoke (no token)', () => {
  // Clear any route handlers a previous test installed so each test starts
  // from a known interception baseline.
  test.beforeEach(async ({ page }) => {
    await page.unrouteAll({ behavior: 'ignoreErrors' })
  })

  test('AuthGate renders LoginPage with token input and Sign in button', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Sign in to Anchor' })).toBeVisible()
    await expect(page.getByLabel('Bridge auth token')).toBeVisible()
    await expect(page.getByRole('button', { name: /Sign in/ })).toBeEnabled()
    // Theme-token sanity — body should pick up the theme bg/fg vars, not the
    // legacy hardcoded utilities. We can't assert a specific color because the
    // active theme depends on the OS setting at run time, so we just check
    // that the body has a non-empty computed bg and an applied color-scheme
    // (proves index.css's `:root { color-scheme: light dark }` shipped).
    const cs = await page.evaluate(() => ({
      bg: getComputedStyle(document.body).backgroundColor,
      colorScheme: getComputedStyle(document.documentElement).colorScheme,
    }))
    expect(cs.bg).toBeTruthy()
    expect(cs.colorScheme).toMatch(/light|dark/)
  })

  test('empty submit shows "Token cannot be empty" without Bridge call', async ({ page }) => {
    let bridgeHits = 0
    await page.route('**/api/v1/whoami', (r) => {
      bridgeHits++
      r.fulfill({ status: 200, body: '{}' })
    })

    await page.getByLabel('Bridge auth token').fill('')
    await page.getByRole('button', { name: /Sign in/ }).click()

    await expect(page.getByText('Token cannot be empty.')).toBeVisible()
    expect(bridgeHits).toBe(0)
  })

  test('Bridge rejects token → distinct "Bridge rejected the token" error, stays on LoginPage', async ({
    page,
  }) => {
    await page.route('**/api/v1/whoami', (r) =>
      r.fulfill({ status: 401, body: JSON.stringify({ error: 'unauthorized' }) }),
    )

    await page.getByLabel('Bridge auth token').fill('definitely-not-a-real-token')
    await page.getByRole('button', { name: /Sign in/ }).click()

    await expect(page.getByText(/Bridge rejected the token: 401/)).toBeVisible({ timeout: 15_000 })
    await expect(page.getByRole('heading', { name: 'Sign in to Anchor' })).toBeVisible()
  })

  test('Network failure → distinct "Could not reach Bridge" error, stays on LoginPage', async ({
    page,
  }) => {
    await page.route('**/api/v1/whoami', (r) => r.abort('connectionrefused'))

    await page.getByLabel('Bridge auth token').fill('any-token')
    await page.getByRole('button', { name: /Sign in/ }).click()

    await expect(page.getByText(/Could not reach Bridge at/)).toBeVisible({ timeout: 15_000 })
    await expect(page.getByRole('heading', { name: 'Sign in to Anchor' })).toBeVisible()
  })

  test('PASS path: Bridge 200 + Stronghold persist → AuthGate transitions to AppLayout', async ({
    page,
  }) => {
    test.setTimeout(90_000)
    // Mock whoami 200 and all sync endpoints with empty arrays so the
    // Properties tab empty-state renders.
    await page.route('**/api/v1/whoami', (r) =>
      r.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          user: 'smoke',
          role: 'owner',
          defaultCompany: 'mock-co',
          availableCompanies: ['mock-co'],
        }),
      }),
    )
    // Catch-all for anything else under /api/v1/ so a sync call doesn't hang
    // the auth flow waiting on a real Bridge that isn't there.
    await page.route('**/api/v1/**', (r) => {
      if (r.request().url().includes('/whoami')) return r.fallback()
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    })

    // Confirm the 200 actually fires before we wait on the post-success path.
    const whoamiResp = page.waitForResponse(
      (resp) => resp.url().includes('/api/v1/whoami') && resp.status() === 200,
      { timeout: 15_000 },
    )
    await page.getByLabel('Bridge auth token').fill('smoke-pass-token-playwright')
    await page.getByRole('button', { name: /Sign in/ }).click()
    await whoamiResp

    // After whoami 200, the LoginPage flow is:
    //   1. persistToken() — Stronghold.load() + insert() + save()  ~10–30s cold
    //   2. invoke('set_bridge_token', …) — Rust state, sub-ms
    //   3. setToken() → zustand → AuthGate re-renders → AppLayout mounts
    // Cold Stronghold first-launch can be slow (iota_stronghold engine init +
    // OS-keychain read), so give the AppLayout up to 60s to render.
    //
    // We assert the AUTH-TRANSITION invariants only — LoginPage gone, AppLayout
    // chrome present. Asserting empty-state copy in `main` would couple this
    // test to the data-layer mock shape (React Query envelopes, route handlers
    // per fetcher, etc.); separate per-tab smoke specs can layer that on.
    await expect(page.getByRole('link', { name: 'Properties' })).toBeVisible({ timeout: 60_000 })
    await expect(page.getByRole('button', { name: /Logout/ })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Sign in to Anchor' })).toBeHidden()

    // Capture a screenshot of the rendered AppLayout. Useful for visual
    // verification of theme-token rendering (dark/light) without requiring a
    // reviewer to build + run locally. Saved to smoke-artifacts/ which is in
    // .gitignore — Playwright also auto-attaches it to the HTML report.
    await page.screenshot({
      path: 'smoke-artifacts/applayout-post-login.png',
      fullPage: true,
    })
  })
})
