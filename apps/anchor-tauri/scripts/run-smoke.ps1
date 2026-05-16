<#
.SYNOPSIS
    Launches anchor-tauri.exe with WebView2 CDP enabled, waits for the
    DevTools endpoint, runs Playwright smoke tests, then cleans up.

.DESCRIPTION
    Replaces hours of coord-based smoke-testing with a 30-LOC orchestration:
      1. Wipes the Stronghold snapshot so the LoginPage renders fresh
      2. Launches the latest x64 debug build with
         WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222
      3. Polls http://localhost:9222/json/version until WebView2 answers
         (so Playwright connect doesn't race the webview boot)
      4. Runs `npx playwright test` against `smoke/playwright.config.ts`
      5. Captures the Tauri PID and tears it down at exit regardless of
         pass/fail (so a failed run doesn't leave the window orphaned)

    Re-runnable. Idempotent. Safe to invoke from CI.

.PARAMETER Headed
    No-op for this script (Tauri webview is always headed). Reserved for
    future browser-spawning variants.

.PARAMETER CdpPort
    Override the CDP port. Default 9222.

.PARAMETER SkipBuild
    Skip the `npm run tauri:build --debug` step. Useful when iterating on
    smoke tests against an already-built binary.

.EXAMPLE
    .\scripts\run-smoke.ps1
    .\scripts\run-smoke.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [switch]$Headed,
    [int]$CdpPort = 9222,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$appRoot = (Resolve-Path "$PSScriptRoot\..").Path
$exe = Join-Path $appRoot 'src-tauri\target\x86_64-pc-windows-msvc\debug\anchor-tauri.exe'
$strongholdSnap = Join-Path $env:APPDATA 'io.sunfish.anchor\anchor.stronghold'

function Stop-AnchorTauri {
    Get-Process anchor-tauri -ErrorAction SilentlyContinue | ForEach-Object {
        try { Stop-Process -Id $_.Id -Force -ErrorAction Stop } catch { }
    }
    # Also kill WebView2 child processes scoped to anchor's user-data-dir.
    # WebView2 reuses an existing browser process per user-data-dir, so a
    # leftover msedgewebview2.exe from a prior run would attach the next
    # launch to a browser that was NOT spawned with --remote-debugging-port.
    Get-CimInstance Win32_Process -Filter "Name='msedgewebview2.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match 'io\.sunfish\.anchor' } |
        ForEach-Object {
            try { Stop-Process -Id $_.ProcessId -Force -ErrorAction Stop } catch { }
        }
}

# --- 1. Build (unless skipped) ----------------------------------------------
if (-not $SkipBuild) {
    Write-Host "[smoke] building x64 debug…" -ForegroundColor Cyan
    Push-Location $appRoot
    try {
        & npm run tauri:build -- --debug --target x86_64-pc-windows-msvc | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "tauri:build failed with exit $LASTEXITCODE" }
    } finally {
        Pop-Location
    }
}
if (-not (Test-Path $exe)) {
    throw "anchor-tauri.exe missing at $exe — rebuild required (or omit -SkipBuild)."
}

# --- 2. Clear running instances + Stronghold snapshot ----------------------
Write-Host "[smoke] cleaning previous state…" -ForegroundColor Cyan
Stop-AnchorTauri
Start-Sleep -Milliseconds 500
if (Test-Path $strongholdSnap) {
    Remove-Item $strongholdSnap -Force
    Write-Host "[smoke]   removed stale Stronghold snapshot"
}

# --- 3. Launch with CDP enabled --------------------------------------------
Write-Host "[smoke] launching anchor-tauri with CDP on :$CdpPort…" -ForegroundColor Cyan
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$CdpPort"
$tauri = Start-Process -FilePath $exe -PassThru
Write-Host "[smoke]   PID $($tauri.Id)"

# --- 4. Wait for CDP endpoint ----------------------------------------------
# Use 127.0.0.1 explicitly: WebView2 binds IPv4-only, and `localhost` on
# Windows can resolve to ::1 (IPv6) first, which is closed.
$cdpUrl = "http://127.0.0.1:$CdpPort/json/version"
$timeoutSec = 60
$deadline = (Get-Date).AddSeconds($timeoutSec)
$ready = $false
while ((Get-Date) -lt $deadline) {
    try {
        $r = Invoke-WebRequest -Uri $cdpUrl -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { $ready = $true; break }
    } catch { }
    Start-Sleep -Milliseconds 500
}
if (-not $ready) {
    Stop-AnchorTauri
    throw "CDP endpoint $cdpUrl never came up within ${timeoutSec}s"
}
Write-Host "[smoke]   CDP up; running Playwright…" -ForegroundColor Cyan

# --- 5. Run Playwright -----------------------------------------------------
$env:TAURI_CDP_URL = "http://127.0.0.1:$CdpPort"
Push-Location $appRoot
$smokeExit = 0
try {
    & npx playwright test --config=smoke/playwright.config.ts
    $smokeExit = $LASTEXITCODE
} finally {
    Pop-Location
    Write-Host "[smoke] tearing down Tauri (PID $($tauri.Id))…" -ForegroundColor Cyan
    Stop-AnchorTauri
}

if ($smokeExit -ne 0) {
    Write-Host "[smoke] FAILED (exit $smokeExit)" -ForegroundColor Red
    exit $smokeExit
}
Write-Host "[smoke] PASS" -ForegroundColor Green
