/**
 * Logged-in cold-start profile. Mirrors `profile-boot.spec.ts` but waits for
 * the AppLayout to mount instead of stopping at LoginPage — captures the path
 * where Stronghold has an existing snapshot to decrypt + AuthGate then triggers
 * AppLayout mount + AppLayout fires whoami + React Query starts fetching.
 *
 * Precondition: a snapshot must exist in %APPDATA%\io.sunfish.anchor\
 * (run the regular PASS-path test first to persist a token, then relaunch).
 */
import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { test } from './fixtures'

test('boot timing profile — logged-in path', async ({ page }) => {
  test.setTimeout(120_000)

  // Wait until AppLayout renders (the slow path under measurement).
  await page.waitForFunction(
    () => {
      const t = (window as Window).__anchorBootTimings
      if (!t) return false
      // AppLayout mount isn't instrumented yet, but its presence in the DOM
      // is the signal that the AuthGate-to-app transition is done.
      return document.querySelector('a[href="/properties"]') !== null
    },
    null,
    { timeout: 90_000 },
  )

  const jsTimings = await page.evaluate(() => (window as Window).__anchorBootTimings ?? [])

  console.log('\n=== JS boot timings (logged-in path) ===')
  for (const e of jsTimings) {
    console.log(`[+${String(Math.round(e.t)).padStart(6)}ms] js   ${e.label}`)
  }

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
})
