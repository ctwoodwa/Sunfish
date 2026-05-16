# Council Review — PR #898 (W#60 P4 PR 1) Tauri Stronghold + DPAPI

**Reviewed:** 2026-05-16
**PR:** [#898](https://github.com/ctwoodwa/Sunfish/pull/898) (merged to `main` as commit `96ccc73c`)
**Reviewer model:** Opus 4.7 (xhigh)
**Council seats:** security-engineering (veto), Rust/Tauri, Frontend, Operations

---

## Attestation note

**This file is a post-hoc attestation reconstructed on 2026-05-16 by `dev-win` per
[xo-directive-T20-39Z](../../../../coordination/_archive/xo-directive-2026-05-16T20-39Z-dev-win-pr1-cleanup.md)
Task 1.** The original `council-review-pr1-w60-p4-stronghold-2026-05-16.md` was authored during the
PR #898 review cycle but never landed on `main` during the rebase cleanup that produced commit
`96ccc73c`. The verdicts, blocker IDs, and resolution diffs below are reproduced from session
context against the merged code. Where this attestation is loss-of-fidelity vs. the original
review's wording it is a strictly narrower record — no claims have been broadened.

**Verifiable today against the merged code:**

- All four A1.* blockers have a corresponding diff in `96ccc73c` (cited inline below)
- The merged `lib.rs` / `Cargo.toml` / `capabilities/default.json` / `LoginPage.tsx` shapes match
  the post-A1.* state, not the pre-A1.* state — i.e. the fixes shipped, not just the verdicts
- Subsequent live-hardware validation (`dev-win-status-2026-05-16T20-27Z-a13-pass-a11-roundtrip-validated.md`)
  confirms the OS-keychain round-trip on real winhub DPAPI

---

## Verdict

**Pass 1 — REJECT (composite 4.5/10).** Four blocking gaps named A1.1 through A1.4 (each tied to
a council-veto seat). Stronghold + auth flow was structurally sound but four shipping-critical
defects would have led to silently-broken master-key handling, missing Tauri capability scope,
unvalidated token persistence, and a keychain-failure crash path. All four were called by the
security-engineering seat as veto-class.

**Pass 2 — ACCEPT WITH MINOR AMENDMENTS (composite 7.25/10).** A1.1–A1.4 closed; the remaining
recommendations (R-series) are non-blocking and tracked as carry-forward.

---

## Required amendments (blocking, Pass 1)

### A1.1 — `keyring = "3"` declared without platform features → silent mock-store link

Without explicit feature flags, the `keyring` v3 crate falls back to a mock in-process store on
Windows/macOS/Linux. The "OS keychain" claim was true at the type level but false at the runtime
level — the master key never actually round-tripped through DPAPI.

**Fix in 96ccc73c (`apps/anchor-tauri/src-tauri/Cargo.toml`):**

```toml
keyring = { version = "3", features = ["windows-native", "apple-native", "sync-secret-service"] }
```

**Verified post-merge:** `dev-win-status-T20-27Z` documents the A1.1 round-trip on real hardware
— `LegacyGeneric:target=stronghold-master-key.io.sunfish.anchor.stronghold` persists across
process restart and decrypts the prior snapshot via DPAPI.

### A1.2 — `src-tauri/capabilities/` missing → stronghold + shell IPC un-scoped

Tauri 2.x requires explicit capabilities for every plugin IPC verb. Without a `capabilities/`
directory, `Stronghold.load()` from JS would fail with `IPC denied` at runtime, OR (worse) inherit
default-allow under a transitional config — the council called this veto-class because either
state would leak as an irreproducible bug.

**Fix in 96ccc73c (`apps/anchor-tauri/src-tauri/capabilities/default.json`):**

Grants `core:default`, `shell:default`, `stronghold:default`, and the specific
`stronghold:allow-remove-store-record` permission required by `clearToken`.

### A1.3 — LoginPage persisted any pasted string with no Bridge validation

The interim manual-paste login flow accepted any non-empty textarea content and persisted it to
Stronghold. A pasted redirect URL, JSON fragment, or attacker-supplied token would silently land
in the vault and the app would 401-spiral with no surfaced cause.

**Fix in 96ccc73c (`apps/anchor-tauri/src/pages/LoginPage.tsx`):**

LoginPage probes `GET /api/v1/whoami` with `Authorization: Bearer <token>` BEFORE the Stronghold
persist; surfaces distinct error states for empty/401/network-failure. (A second fix landed in the
same PR — the probe must use the **absolute** Bridge URL via `get_bridge_url` IPC; relative
`/api/v1/whoami` proxies only under `tauri:dev` and the bundled SPA fallback returns 200 for
unknown paths, which would have re-introduced the false-positive.)

### A1.4 — Keychain access failure inside setup-hook closure → process panic

The original setup hook called `keyring::Entry::new(...).get_password()` inside the Stronghold
password closure. If the OS keychain was unavailable (GPO lockdown, Keychain.app denied dialog,
Secret Service crashed), the closure would unwrap-panic and bring down the Tauri process before
the LoginPage could render an error state.

**Fix in 96ccc73c (`apps/anchor-tauri/src-tauri/src/lib.rs` + `src/commands/auth.rs`):**

- Master-key derive moved to setup-time (not closure-time); cached in
  `Arc<Result<Vec<u8>, String>>`
- Closure returns cached bytes OR a sentinel; never panics
- `KeychainStatus` enum + `keychain_status` IPC command surface the failure state to the frontend
- AuthGate renders a user-visible banner when keychain status is `Failed`

---

## Non-blocking recommendations (Pass 2)

Tracked as carry-forward; not gating PR #898 merge:

- **R2** — Add `--with-system-tray` exclusion test; Tauri 2.x systray would persist a window
  handle that bypasses single-instance focus logic (relevant if `tauri-plugin-single-instance`
  is added later).
- **R3** — Document the `STRONGHOLD_INIT_PASSWORD` constant's machine-locked-ignored-password
  design in the file header (the JS side passes a non-secret stable string; the Rust closure
  ignores it). Risk of a future contributor "fixing" it by injecting a user-prompted password
  and breaking the round-trip.
- **R5** — `Stronghold.save()` is fire-and-forget from JS but synchronously writes to disk in
  Rust; consider adding a `flush_pending` test that verifies write-durability across simulated
  power-loss (out of scope for PR 1; relevant when blocks-* writes start chattering).
- **R9** — Logout end-to-end test was skipped during smoke validation (coord-drilling cost on
  high-DPI). The Playwright + CDP harness landed as PR #906 covers the auth-up direction;
  logout still pending a quit-and-relaunch wrapper. Carry-forward.

---

## Sign-off

Pass 2 composite **7.25/10** — security-engineering 7, Rust/Tauri 7, Frontend 8, Operations 7.
ACCEPT WITH MINOR AMENDMENTS. PR #898 merged to `main` as `96ccc73c` after A1.1–A1.4 verified
in code; live-hardware validation (`A1.3 PASS` + `A1.1 round-trip`) completed in the same
session.
