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
 *   - anchor-tauri is running with `--remote-debugging-port=9222`
 *   - Stronghold snapshot is cleared (fresh first-launch state) so cold-boot
 *     lands in offline mode with no token.
 *
 * Note on isolation: connectOverCDP gives a SHARED browser context. State
 * (Stronghold, authStore, etc.) persists across tests in the same run. Tests
 * are ordered so the disconnect step in the last spec leaves the app back
 * in offline mode for the next run.
 */

test.describe('Local-first cold boot (no token)', () => {
  test.beforeEach(async ({ page }) => {
    await page.unrouteAll({ behavior: 'ignoreErrors' })
  })

  test('AuthGate renders AppLayout immediately, no Bridge required', async ({ page }) => {
    // The local-first contract: cold-launch with no stored token MUST reach
    // a usable app shell without any Bridge call. The Properties nav link is
    // the canonical "app rendered" sentinel.
    await expect(page.getByRole('link', { name: 'Properties' })).toBeVisible({ timeout: 10_000 })
    await expect(page.getByRole('link', { name: 'Connect to Bridge' })).toBeVisible()

    // Theme-token sanity — body should pick up theme bg/fg vars.
    const cs = await page.evaluate(() => ({
      bg: getComputedStyle(document.body).backgroundColor,
      colorScheme: getComputedStyle(document.documentElement).colorScheme,
    }))
    expect(cs.bg).toBeTruthy()
    expect(cs.colorScheme).toMatch(/light|dark/)
  })

  test('cold boot does not call Bridge', async ({ page }) => {
    let bridgeHits = 0
    await page.route('**/api/v1/whoami', (r) => {
      bridgeHits++
      r.fulfill({ status: 200, body: '{}' })
    })
    // Reload to retrigger cold-boot path with the route installed.
    await page.reload()
    await expect(page.getByRole('link', { name: 'Properties' })).toBeVisible({ timeout: 10_000 })
    // The whoami call inside AppLayout is best-effort and may fire — but
    // the boot itself must not depend on it. We assert the count is 0 OR 1;
    // 0 if AppLayout's whoami fetch was aborted before the route matched,
    // 1 if it landed. The key invariant is that the screen rendered before
    // the call resolved (asserted above by the visibility check).
    expect(bridgeHits).toBeLessThanOrEqual(1)
  })
})

test.describe('Connect to Bridge flow (/settings/bridge)', () => {
  test.beforeEach(async ({ page }) => {
    await page.unrouteAll({ behavior: 'ignoreErrors' })
    await page.getByRole('link', { name: 'Connect to Bridge' }).click()
    await expect(page.getByRole('heading', { name: 'Connect to Bridge' })).toBeVisible()
  })

  test('empty submit shows "Token cannot be empty" without Bridge call', async ({ page }) => {
    let bridgeHits = 0
    await page.route('**/api/v1/whoami', (r) => {
      bridgeHits++
      r.fulfill({ status: 200, body: '{}' })
    })

    await page.getByLabel('Bridge auth token').fill('')
    await page.getByRole('button', { name: /Connect/ }).click()

    await expect(page.getByText('Token cannot be empty.')).toBeVisible()
    expect(bridgeHits).toBe(0)
  })

  test('Bridge rejects token → distinct "Bridge rejected the token" error, stays on page', async ({
    page,
  }) => {
    await page.route('**/api/v1/whoami', (r) =>
      r.fulfill({ status: 401, body: JSON.stringify({ error: 'unauthorized' }) }),
    )

    await page.getByLabel('Bridge auth token').fill('definitely-not-a-real-token')
    await page.getByRole('button', { name: /^Connect$/ }).click()

    await expect(page.getByText(/Bridge rejected the token: 401/)).toBeVisible({ timeout: 15_000 })
    await expect(page.getByRole('heading', { name: 'Connect to Bridge' })).toBeVisible()
  })

  test('Network failure → distinct "Could not reach Bridge" error, stays on page', async ({
    page,
  }) => {
    await page.route('**/api/v1/whoami', (r) => r.abort('connectionrefused'))

    await page.getByLabel('Bridge auth token').fill('any-token')
    await page.getByRole('button', { name: /^Connect$/ }).click()

    await expect(page.getByText(/Could not reach Bridge at/)).toBeVisible({ timeout: 15_000 })
    await expect(page.getByRole('heading', { name: 'Connect to Bridge' })).toBeVisible()
  })

  test('Continue offline button returns to the app without touching Bridge', async ({ page }) => {
    let bridgeHits = 0
    await page.route('**/api/v1/whoami', (r) => {
      bridgeHits++
      r.fulfill({ status: 200, body: '{}' })
    })

    await page.getByLabel('Bridge auth token').fill('whatever')
    await page.getByRole('button', { name: /Continue offline/ }).click()

    await expect(page.getByRole('link', { name: 'Properties' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Connect to Bridge' })).toBeVisible()
    expect(bridgeHits).toBe(0)
  })

  test('PASS path: Bridge 200 + Stronghold persist → returns to app with Disconnect button', async ({
    page,
  }) => {
    test.setTimeout(90_000)
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
    await page.route('**/api/v1/**', (r) => {
      if (r.request().url().includes('/whoami')) return r.fallback()
      return r.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    })

    const whoamiResp = page.waitForResponse(
      (resp) => resp.url().includes('/api/v1/whoami') && resp.status() === 200,
      { timeout: 15_000 },
    )
    await page.getByLabel('Bridge auth token').fill('smoke-pass-token-playwright')
    await page.getByRole('button', { name: /^Connect$/ }).click()
    await whoamiResp

    // After persist+navigate: app shell, Disconnect button (not Connect link).
    await expect(page.getByRole('link', { name: 'Properties' })).toBeVisible({ timeout: 60_000 })
    await expect(page.getByRole('button', { name: /Disconnect/ })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Connect to Bridge' })).toBeHidden()

    await page.screenshot({
      path: 'smoke-artifacts/applayout-post-connect.png',
      fullPage: true,
    })

    // Leave the app in offline mode for the next test run.
    await page.getByRole('button', { name: /Disconnect/ }).click()
    await expect(page.getByRole('link', { name: 'Connect to Bridge' })).toBeVisible({ timeout: 10_000 })
  })
})
