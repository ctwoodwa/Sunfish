/**
 * Ad-hoc boot-timing profile. Launches anchor-tauri (via the run-smoke.ps1
 * wrapper), attaches via CDP, waits for AuthGate to resolve to LoginPage
 * (the cold-start critical path), then dumps both the JS-side
 * `window.__anchorBootTimings` array AND the Rust-side
 * `%LOCALAPPDATA%\io.sunfish.anchor\boot-timing.log` file.
 *
 * Not part of the regression suite (`smoke/anchor.spec.ts`). Run via:
 *
 *   $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS="--remote-debugging-port=9222"
 *   & .\src-tauri\target\x86_64-pc-windows-msvc\debug\anchor-tauri.exe
 *   # (other shell)
 *   npx playwright test smoke/profile-boot.spec.ts --config=smoke/playwright.config.ts
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { test } from './fixtures'

test('boot timing profile', async ({ page }) => {
  test.setTimeout(90_000)

  // Wait for the AuthGate to settle into LoginPage (the cold-start path
  // we're profiling). If a token's already in Stronghold from a prior run,
  // AppLayout will render instead — handle either.
  await page.waitForFunction(
    () => {
      const t = (window as Window).__anchorBootTimings
      if (!t) return false
      return t.some((e) => e.label === 'AuthGate setLoaded(true)')
    },
    null,
    { timeout: 60_000 },
  )

  const jsTimings = await page.evaluate(() => (window as Window).__anchorBootTimings ?? [])

  console.log('\n=== JS boot timings ===')
  for (const e of jsTimings) {
    console.log(`[+${String(Math.round(e.t)).padStart(6)}ms] js   ${e.label}`)
  }

  // Read the Rust-side log file.
  const localAppData = process.env.LOCALAPPDATA
  if (localAppData) {
    const logPath = join(localAppData, 'io.sunfish.anchor', 'boot-timing.log')
    try {
      const rustLog = readFileSync(logPath, 'utf-8')
      console.log('\n=== Rust boot timings ===')
      console.log(rustLog)
    } catch (e) {
      console.log(`\n(no Rust timing log at ${logPath} — ${e})`)
    }
  }

  // Always passes; this is a profiling probe, not a regression test.
})
